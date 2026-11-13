using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Compliance
{
    // Storage for the AI's evidence checks (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IEvidenceCheckRepository
    {
        // The project's checks, newest first
        Task<IReadOnlyList<EvidenceCheck>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(EvidenceCheck check, CancellationToken cancellationToken = default);
    }

    // US-038 R1: the prompt and the shape of the answer
    public static class EvidenceCheckPrompt
    {
        public const string Version = "evidence-check-v1";
        public const string Marker = "Check whether the evidence";

        public static string For(Requirement requirement, string evidenceTitle, string? evidenceText)
            => $"""
                {Marker} below covers the regulatory requirement below.
                Use only these texts. Answer in exactly this shape:
                VERDICT: Covers | Partly covers | Does not cover
                MISSING: <what the evidence does not show, or "Nothing">
                QUOTES: <one short quote from the evidence per line that the verdict is based on>

                Requirement {requirement.Code}: {requirement.Title}
                {requirement.Description}

                Evidence "{evidenceTitle}":
                {evidenceText ?? "(only the title is known)"}
                """;

        // A verdict the model words its own way is still understood; an answer without one is refused
        public static (EvidenceVerdict Verdict, string Missing, string Quotes) Parse(string answer)
        {
            string? verdict = null;
            var missing = new List<string>();
            var quotes = new List<string>();
            List<string>? current = null;
            foreach (var raw in answer.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("VERDICT:", StringComparison.OrdinalIgnoreCase))
                {
                    verdict = line["VERDICT:".Length..].Trim();
                    current = null;
                }
                else if (line.StartsWith("MISSING:", StringComparison.OrdinalIgnoreCase))
                {
                    current = missing;
                    Add(current, line["MISSING:".Length..]);
                }
                else if (line.StartsWith("QUOTES:", StringComparison.OrdinalIgnoreCase))
                {
                    current = quotes;
                    Add(current, line["QUOTES:".Length..]);
                }
                else if (current is not null)
                {
                    Add(current, line);
                }
            }

            var text = (verdict ?? "").ToLowerInvariant().Trim('.', ' ', '"', '*');
            EvidenceVerdict parsed = text.Contains("not") || text.Contains("does not") ? EvidenceVerdict.DoesNotCover
                : text.StartsWith("partly") || text.StartsWith("partial") ? EvidenceVerdict.PartlyCovers
                : text.StartsWith("cover") ? EvidenceVerdict.Covers
                : throw new AiUnavailableException("The AI's answer had no verdict");
            return (parsed, string.Join(" ", missing), string.Join("\n", quotes));

            static void Add(List<string> into, string text)
            {
                var cleaned = text.Trim().TrimStart('-', '*', ' ');
                if (cleaned.Length > 0)
                {
                    into.Add(cleaned);
                }
            }
        }
    }

    // US-038: "Check with AI" on a piece of evidence of a requirement, and the person's decision
    public class EvidenceCheckService
    {
        private readonly IProjectRepository _projects;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceCheckRepository _checks;
        private readonly IFrameworkRepository _frameworks;
        private readonly IEvidenceTextReader _text;
        private readonly IAiModels _models;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public EvidenceCheckService(IProjectRepository projects, IEvidenceRepository evidence, IEvidenceCheckRepository checks, IFrameworkRepository frameworks,
            IEvidenceTextReader text, IAiModels models, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _evidence = evidence;
            _checks = checks;
            _frameworks = frameworks;
            _text = text;
            _models = models;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R4, R6–R9: the AI reads only the evidence and the requirement; nothing is saved when it cannot be reached
        public async Task<EvidenceCheck> CheckAsync(Guid projectId, Guid evidenceId, Guid requirementId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var evidence = await _evidence.GetAsync(project.Id, evidenceId, cancellationToken) ?? throw new DomainException("Evidence not found");
            var today = DateOnly.FromDateTime(Now);
            if (!evidence.IsActive || evidence.IsExpiredOn(today))
            {
                throw new DomainException("Expired or removed evidence cannot be checked"); // R7
            }

            var requirement = await RequirementAsync(organization.Id, project, requirementId, cancellationToken);
            var content = evidence.IsFile ? await _evidence.GetContentAsync(evidence.Id, cancellationToken) : null;
            var text = content is null ? null : _text.Read(evidence.ContentType ?? "", content);

            var writer = _models.Writer(AiTask.EvidenceCheck);
            string answer;
            try
            {
                answer = await writer.WriteAsync(EvidenceCheckPrompt.For(requirement, evidence.Title, text), cancellationToken);
            }
            catch (AiUnavailableException)
            {
                throw new DomainException("The local AI cannot be reached right now, so nothing was checked"); // R8
            }

            (EvidenceVerdict Verdict, string Missing, string Quotes) parsed;
            try
            {
                parsed = EvidenceCheckPrompt.Parse(answer);
            }
            catch (AiUnavailableException)
            {
                throw new DomainException("The AI's answer could not be read; try again");
            }

            var check = EvidenceCheck.Record(project.Id, evidence.Id, requirement.Id, parsed.Verdict, parsed.Missing, parsed.Quotes,
                evidence.Sha256 ?? Domain.Common.Fingerprint.Of(evidence.Title, evidence.Url), writer.Name, EvidenceCheckPrompt.Version, user.Id, Now);
            await _checks.AddAsync(check, cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceChecked, project.Id,
                $"{requirement.Code}: {evidence.Title} – {EvidenceCheck.Text(check.AiVerdict)} (AI, {writer.Name})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return check;
        }

        // R2: DecideCompliance confirms
        public async Task ConfirmAsync(Guid projectId, Guid checkId, CancellationToken cancellationToken = default)
        {
            var (user, check) = await DecideAsync(projectId, checkId, cancellationToken);
            check.Confirm(user.Id, user.Role, Now);
            await _log.LogAsync(ActivityAction.EvidenceCheckDecided, check.ProjectId, $"Confirmed: {EvidenceCheck.Text(check.AiVerdict)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: or overrules with another verdict and a reason
        public async Task OverruleAsync(Guid projectId, Guid checkId, EvidenceVerdict? verdict, string? reason, CancellationToken cancellationToken = default)
        {
            var (user, check) = await DecideAsync(projectId, checkId, cancellationToken);
            check.Overrule(verdict, reason, user.Id, user.Role, Now);
            await _log.LogAsync(ActivityAction.EvidenceCheckDecided, check.ProjectId,
                $"Overruled: {EvidenceCheck.Text(check.AiVerdict)} → {EvidenceCheck.Text(check.DecidedVerdict!.Value)} ({check.DecisionReason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<(User User, EvidenceCheck Check)> DecideAsync(Guid projectId, Guid checkId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var check = (await _checks.ListAsync(project.Id, cancellationToken)).FirstOrDefault(c => c.Id == checkId)
                ?? throw new DomainException("Check not found");
            return (user, check);
        }

        private async Task<Requirement> RequirementAsync(Guid organizationId, Domain.Projects.Project project, Guid requirementId, CancellationToken cancellationToken)
        {
            var versions = project.Frameworks.Select(f => f.FrameworkVersionId).ToHashSet();
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(organizationId, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            return frameworks.SelectMany(f => f.Versions).Where(v => versions.Contains(v.Id)).SelectMany(v => v.Requirements).FirstOrDefault(r => r.Id == requirementId)
                ?? throw new DomainException("Requirement not found");
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
