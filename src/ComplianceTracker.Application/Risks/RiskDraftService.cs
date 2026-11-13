using System.Globalization;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Risks
{
    // Storage for the agent's drafts (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IRiskDraftRepository
    {
        Task<IReadOnlyList<RiskDraft>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<RiskDraft> drafts, CancellationToken cancellationToken = default);
    }

    // What the agent reads about the project (R2): only the organisation's own data (R6)
    public record RiskDraftContext(
        string IntendedPurpose, string? Scope, IReadOnlyList<string> Classes, IReadOnlyList<string> Uncovered,
        IReadOnlyList<string> OpenEvents, IReadOnlyList<string> SevereFindings, IReadOnlyList<string> ExistingRisks);

    // One risk as the agent wrote it
    public record DraftedRisk(string Title, string Threat, string Asset, int Likelihood, int Impact, string BasedOn, string Reasoning);

    // A run of the agent: what it made, and what it left out because a risk or a discarded draft already covers it (R3, R5)
    public record RiskDraftRun(IReadOnlyList<RiskDraft> Made, IReadOnlyList<(string Draft, string CoveredBy)> LeftOut);

    // US-112: the prompt, the answer's shape, and the rules for leaving a draft out. Pure, so they are tested on their own.
    public static class RiskDraftPrompt
    {
        public const string Version = "risk-drafts-v1";
        public const string Marker = "Draft a first cybersecurity risk assessment";
        public const int MaxDrafts = 10;

        public static string For(RiskDraftContext context)
        {
            static string List(IReadOnlyList<string> items) => items.Count == 0 ? "(none)" : string.Join("\n", items.Select(i => $"- {i}"));
            return $"""
                {Marker} for a software product, with at most {MaxDrafts} risks.
                Use only the facts below. Do not repeat a risk that is already in the register. Answer with one block per risk,
                blocks separated by a line with "---", each in exactly this shape:
                RISK: <a short title>
                THREAT: <what could happen and who could do it>
                ASSET: <what is harmed>
                LIKELIHOOD: <1 to 5>
                IMPACT: <1 to 5>
                BASED ON: <which fact below it comes from>
                WHY: <one or two sentences>

                Intended purpose: {context.IntendedPurpose}
                Scope: {context.Scope ?? "(not given)"}

                Classification:
                {List(context.Classes)}

                Requirements that are not covered yet:
                {List(context.Uncovered)}

                Open security events:
                {List(context.OpenEvents)}

                Open high or critical scan findings:
                {List(context.SevereFindings)}

                Already in the register:
                {List(context.ExistingRisks)}
                """;
        }

        // The blocks of the answer; a block without a title is left out, and at most MaxDrafts are read
        public static IReadOnlyList<DraftedRisk> Parse(string answer)
        {
            var risks = new List<DraftedRisk>();
            foreach (var block in answer.Replace("\r\n", "\n").Split("\n---", StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? current = null;
                foreach (var raw in block.Split('\n'))
                {
                    var line = raw.Trim().TrimStart('-', '*', ' ');
                    var colon = line.IndexOf(':');
                    var label = colon > 0 ? line[..colon].Trim().ToUpperInvariant() : null;
                    if (label is "RISK" or "THREAT" or "ASSET" or "LIKELIHOOD" or "IMPACT" or "BASED ON" or "WHY")
                    {
                        current = label;
                        fields[label] = line[(colon + 1)..].Trim();
                    }
                    else if (current is not null && line.Length > 0)
                    {
                        fields[current] = $"{fields[current]} {line}".Trim();
                    }
                }

                if (fields.TryGetValue("RISK", out var title) && title.Length > 0)
                {
                    risks.Add(new DraftedRisk(title, fields.GetValueOrDefault("THREAT", ""), fields.GetValueOrDefault("ASSET", ""),
                        Scale(fields.GetValueOrDefault("LIKELIHOOD")), Scale(fields.GetValueOrDefault("IMPACT")),
                        fields.GetValueOrDefault("BASED ON", ""), fields.GetValueOrDefault("WHY", "")));
                }
            }

            return risks.Take(MaxDrafts).ToList();
        }

        // R3: two titles name the same risk when one holds the other, or most of their words are the same
        public static bool Same(string a, string b)
        {
            var x = Words(a);
            var y = Words(b);
            if (x.Count == 0 || y.Count == 0)
            {
                return false;
            }

            var shared = x.Intersect(y).Count();
            return x.IsSubsetOf(y) || y.IsSubsetOf(x) || (double)shared / x.Union(y).Count() >= 0.5;
        }

        private static HashSet<string> Words(string text)
            => text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
                .Where(w => w.Length > 2 && w is not ("the" or "and" or "for" or "via" or "from" or "with"))
                .ToHashSet();

        // "3", "3 (medium)", "High" → 1–5; anything else is the middle
        private static int Scale(string? text)
        {
            var value = text?.Trim() ?? "";
            if (value.Length > 0 && int.TryParse(value[..1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return Math.Clamp(number, Risk.MinScale, Risk.MaxScale);
            }

            return value.ToLowerInvariant() switch
            {
                var v when v.StartsWith("very low") => 1,
                var v when v.StartsWith("low") => 2,
                var v when v.StartsWith("very high") || v.StartsWith("critical") => 5,
                var v when v.StartsWith("high") => 4,
                _ => 3
            };
        }
    }

    // US-112: the risk agent. It drafts only; a person decides every risk (R1).
    public class RiskDraftService
    {
        private readonly IProjectRepository _projects;
        private readonly IRiskRepository _risks;
        private readonly IRiskDraftRepository _drafts;
        private readonly ClassificationService _classifications;
        private readonly ComplianceQueries _compliance;
        private readonly ISecurityEventRepository _events;
        private readonly IScanRepository _scans;
        private readonly IAiModels _models;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public RiskDraftService(IProjectRepository projects, IRiskRepository risks, IRiskDraftRepository drafts, ClassificationService classifications,
            ComplianceQueries compliance, ISecurityEventRepository events, IScanRepository scans, IAiModels models, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _risks = risks;
            _drafts = drafts;
            _classifications = classifications;
            _compliance = compliance;
            _events = events;
            _scans = scans;
            _models = models;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1–R3, R5, R6: reads the project, asks the agent, and keeps the drafts no risk or discarded draft covers yet
        public async Task<RiskDraftRun> DraftAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageRisks, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var risks = await _risks.ListAsync(project.Id, cancellationToken);
            var drafts = await _drafts.ListAsync(project.Id, cancellationToken);
            var classes = (await _classifications.ForProjectAsync(project.Id, cancellationToken) ?? [])
                .Where(c => c.Current is not null)
                .Select(c => $"{Frameworks.FrameworkSlugs.Short(c.Framework.Name)}: {c.Regulation.TitleOf(c.Current!.Class)}").ToList();
            var uncovered = ((await _compliance.ForProjectAsync(project.Id, cancellationToken))?.Frameworks ?? [])
                .SelectMany(f => f.Requirements.Where(r => r.IsApplicable && !r.Requirement.IsRecommendation && !r.IsCovered)
                    .Select(r => $"{Frameworks.FrameworkSlugs.Short(f.Framework.Name)} {r.Requirement.Code} {r.Requirement.Title}"))
                .Take(15).ToList();
            var events = (await _events.ListAsync(project.Id, cancellationToken)).Where(e => e.IsOpen).Select(e => e.Title).Take(5).ToList();
            var findings = (await _scans.ListFindingsAsync(project.Id, cancellationToken))
                .Where(f => f.IsOpen && f.Severity >= FindingSeverity.High).Select(f => $"{f.RuleId} ({f.Severity})").Take(5).ToList();

            var context = new RiskDraftContext(project.IntendedPurpose, project.Scope, classes, uncovered, events, findings,
                risks.Where(r => r.Status != RiskStatus.Closed).Select(r => r.Title).ToList());

            var writer = _models.Writer(AiTask.RiskDrafts);
            string answer;
            try
            {
                answer = await writer.WriteAsync(RiskDraftPrompt.For(context), cancellationToken);
            }
            catch (AiUnavailableException)
            {
                throw new DomainException("The local AI cannot be reached right now, so no risks were drafted"); // R6
            }

            var run = Keep(RiskDraftPrompt.Parse(answer), risks, drafts);
            var now = _clock.GetUtcNow().UtcDateTime;
            var made = run.Kept.Select(d => RiskDraft.Create(project.Id, d.Title, d.Threat, d.Asset, d.Likelihood, d.Impact, d.BasedOn, d.Reasoning,
                writer.Name, RiskDraftPrompt.Version, now)).ToList();
            await _drafts.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.RisksDrafted, project.Id,
                $"{made.Count} {(made.Count == 1 ? "draft" : "drafts")} ({writer.Name}, {RiskDraftPrompt.Version})"
                + (run.LeftOut.Count > 0 ? $"; {run.LeftOut.Count} left out as already covered" : ""), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new RiskDraftRun(made, run.LeftOut);
        }

        // R3, R5: a draft is left out when an existing risk, an open draft or a discarded draft names the same risk
        public static (IReadOnlyList<DraftedRisk> Kept, IReadOnlyList<(string Draft, string CoveredBy)> LeftOut) Keep(
            IReadOnlyList<DraftedRisk> drafted, IReadOnlyList<Risk> risks, IReadOnlyList<RiskDraft> drafts)
        {
            var known = risks.Select(r => (r.Title, Label: $"the risk \"{r.Title}\""))
                .Concat(drafts.Where(d => d.Status != RiskDraftStatus.Accepted)
                    .Select(d => (d.Title, Label: d.IsOpen ? $"the draft \"{d.Title}\"" : $"the discarded draft \"{d.Title}\"")))
                .ToList();
            var kept = new List<DraftedRisk>();
            var leftOut = new List<(string, string)>();
            foreach (var draft in drafted)
            {
                var match = known.Concat(kept.Select(k => (k.Title, Label: $"the draft \"{k.Title}\""))).FirstOrDefault(k => RiskDraftPrompt.Same(k.Title, draft.Title));
                if (match.Title is not null)
                {
                    leftOut.Add((draft.Title, match.Label));
                }
                else
                {
                    kept.Add(draft);
                }
            }

            return (kept, leftOut);
        }

        // R1, R4: the person may change every field first; the risk is marked "AI-drafted"
        public async Task<Risk> AcceptAsync(Guid projectId, Guid draftId, RiskInput input, CancellationToken cancellationToken = default)
        {
            var (organization, user, draft) = await DraftAsync(projectId, draftId, cancellationToken);
            if (organization.Members.All(m => m.Id != input.OwnerId))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var risk = Risk.Create(draft.ProjectId, input.Title, input.Description, input.Likelihood, input.Impact, input.OwnerId, input.Treatment, user.Id, now);
            risk.FromAiDraft(draft.Id);
            draft.Accept(risk.Id, user.Id, user.Role, now);
            await _risks.AddAsync(risk, cancellationToken);
            await _log.LogAsync(ActivityAction.RiskAdded, draft.ProjectId,
                $"{risk.Title}: likelihood {risk.Likelihood}, impact {risk.Impact} ({risk.Level}), AI-drafted", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return risk;
        }

        // R5
        public async Task DiscardAsync(Guid projectId, Guid draftId, string? reason, CancellationToken cancellationToken = default)
        {
            var (_, user, draft) = await DraftAsync(projectId, draftId, cancellationToken);
            draft.Discard(reason, user.Id, user.Role, _clock.GetUtcNow().UtcDateTime);
            await _log.LogAsync(ActivityAction.RiskDraftDiscarded, draft.ProjectId, $"{draft.Title}: {draft.DiscardReason}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The open drafts on the Risks tab; empty for another organisation's project
        public async Task<IReadOnlyList<RiskDraft>> OpenAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return [];
            }

            return (await _drafts.ListAsync(project.Id, cancellationToken)).Where(d => d.IsOpen)
                .OrderByDescending(d => d.Level).ThenBy(d => d.Title, StringComparer.Ordinal).ToList();
        }

        private async Task<(Domain.Organizations.Organization Organization, User User, RiskDraft Draft)> DraftAsync(
            Guid projectId, Guid draftId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageRisks, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var draft = (await _drafts.ListAsync(project.Id, cancellationToken)).FirstOrDefault(d => d.Id == draftId)
                ?? throw new DomainException("Draft not found");
            return (organization, user, draft);
        }
    }
}
