using System.Text;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Ai
{
    // US-108: the prompt that asks for work for a regulation's gaps, in the project's form, and the shape of the answer
    public static class GapDraftPrompt
    {
        public const string Version = "gap-drafts-v1";
        public const string Marker = "Draft the work a team should build";

        public sealed record Draft(IReadOnlyList<string> Covers, string Title, string Role, string Story, string Rules, string Examples, string Postconditions, string Fits);

        public sealed record Answer(IReadOnlyList<Draft> Drafts, IReadOnlyDictionary<string, string> NotApplicable);

        public static string For(Project project, DraftForm form, IReadOnlyList<Requirement> gaps, IReadOnlyList<string> openRisks)
        {
            var text = new StringBuilder();
            text.AppendLine($"{Marker} to meet the regulatory requirements below, for the product described here.");
            text.AppendLine($"Product: {project.Name}. Intended purpose: {(string.IsNullOrWhiteSpace(project.IntendedPurpose) ? "not given" : project.IntendedPurpose)}");
            if (openRisks.Count > 0)
            {
                text.AppendLine($"Open risks: {string.Join("; ", openRisks)}");
            }

            text.AppendLine($"Write {FormText(form)}. Use the product's own words, not the regulation's. Requirements that belong together may share one draft, at most {StoryDraft.MaxCovered}.");
            text.AppendLine("A requirement that does not fit the intended purpose gets no draft; say why instead.");
            text.AppendLine("Answer only in blocks of exactly this shape, one per draft:");
            text.AppendLine("DRAFT");
            text.AppendLine("COVERS: <requirement codes, separated by commas>");
            text.AppendLine("TITLE: <a short title>");
            text.AppendLine(form switch
            {
                DraftForm.UseCase => "ACTOR: <the primary actor>\nPRECONDITIONS: <…>\nSCENARIO: <the main success scenario as numbered steps>\nEXTENSIONS: <e.g. 3a. …>\nPOSTCONDITIONS: <…>",
                DraftForm.Requirement => "DESCRIPTION: <what the product shall do>\nACCEPTANCE: <acceptance criteria>",
                _ => "ROLE: <who>\nSTORY: As a … I want … so that …\nRULES: R1: …\nEXAMPLES: <one example per rule>"
            });
            text.AppendLine("FITS: <one line on how it fits the product>");
            text.AppendLine("END");
            text.AppendLine("and for a requirement that does not fit: NOT APPLICABLE: <code>: <reason>");
            text.AppendLine();
            foreach (var requirement in gaps)
            {
                text.AppendLine($"Requirement {requirement.Code}: {requirement.Title}");
                if (!string.IsNullOrWhiteSpace(requirement.Description))
                {
                    text.AppendLine(requirement.Description.Length <= 600 ? requirement.Description : requirement.Description[..600]);
                }
            }

            return text.ToString();
        }

        private static string FormText(DraftForm form) => form switch
        {
            DraftForm.UseCase => "Unified Process use cases",
            DraftForm.Requirement => "requirements for a requirement specification",
            _ => "user stories"
        };

        // Blocks without a title, a body or a known code are left out; a code is used once (the first draft that names it)
        public static Answer Parse(string answer, DraftForm form, IReadOnlyCollection<string> gapCodes)
        {
            var known = new HashSet<string>(gapCodes, StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var drafts = new List<Draft>();
            var notApplicable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, StringBuilder>? block = null;
            string? field = null;

            foreach (var raw in (answer ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim().TrimStart('*').TrimEnd('*').Trim();
                if (line.StartsWith("NOT APPLICABLE:", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line["NOT APPLICABLE:".Length..].Trim();
                    var colon = rest.IndexOf(':');
                    if (colon > 0 && known.Contains(rest[..colon].Trim()) && !used.Contains(rest[..colon].Trim()))
                    {
                        notApplicable[rest[..colon].Trim()] = rest[(colon + 1)..].Trim();
                    }

                    continue;
                }

                if (line.Equals("DRAFT", StringComparison.OrdinalIgnoreCase))
                {
                    block = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
                    field = null;
                    continue;
                }

                if (block is null)
                {
                    continue;
                }

                if (line.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    if (Make(block, form, known, used) is { } draft)
                    {
                        drafts.Add(draft);
                    }

                    block = null;
                    continue;
                }

                var label = Fields.FirstOrDefault(f => line.StartsWith(f + ":", StringComparison.OrdinalIgnoreCase));
                if (label is not null)
                {
                    field = label;
                    block[field] = new StringBuilder(line[(label.Length + 1)..].Trim());
                }
                else if (field is not null && line.Length > 0)
                {
                    block[field].Append('\n').Append(line);
                }
            }

            if (block is not null && Make(block, form, known, used) is { } last)
            {
                drafts.Add(last);
            }

            foreach (var code in drafts.SelectMany(d => d.Covers))
            {
                notApplicable.Remove(code);
            }

            return new Answer(drafts, notApplicable);
        }

        private static readonly string[] Fields =
            ["COVERS", "TITLE", "ROLE", "STORY", "RULES", "EXAMPLES", "ACTOR", "PRECONDITIONS", "SCENARIO", "EXTENSIONS", "POSTCONDITIONS", "DESCRIPTION", "ACCEPTANCE", "FITS"];

        private static Draft? Make(Dictionary<string, StringBuilder> block, DraftForm form, HashSet<string> known, HashSet<string> used)
        {
            string Get(string name) => block.TryGetValue(name, out var value) ? value.ToString().Trim() : "";

            var covers = Get("COVERS").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => known.FirstOrDefault(k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase)))
                .OfType<string>().Where(c => !used.Contains(c)).Distinct(StringComparer.OrdinalIgnoreCase).Take(StoryDraft.MaxCovered).ToList();
            var (role, story, rules, examples) = form switch
            {
                DraftForm.UseCase => (Get("ACTOR"), Get("SCENARIO"), Get("PRECONDITIONS"), Get("EXTENSIONS")),
                DraftForm.Requirement => ("", Get("DESCRIPTION"), Get("ACCEPTANCE"), ""),
                _ => (Get("ROLE"), Get("STORY"), Get("RULES"), Get("EXAMPLES"))
            };
            if (covers.Count == 0 || Get("TITLE").Length == 0 || story.Length == 0)
            {
                return null;
            }

            covers.ForEach(c => used.Add(c));
            return new Draft(covers, Get("TITLE"), role, story, rules, examples, form == DraftForm.UseCase ? Get("POSTCONDITIONS") : "", Get("FITS"));
        }
    }

    // US-108: the form of the drafts: the project's method (R2); with several methods a person chooses
    public static class DraftForms
    {
        public static IReadOnlyList<DraftForm> Of(RequirementMethods methods)
        {
            var forms = new List<DraftForm>();
            if (methods.HasFlag(RequirementMethods.GitHubUserStories))
            {
                forms.Add(DraftForm.UserStory);
            }

            if (methods.HasFlag(RequirementMethods.UseCases))
            {
                forms.Add(DraftForm.UseCase);
            }

            if (methods.HasFlag(RequirementMethods.RequirementSpecification))
            {
                forms.Add(DraftForm.Requirement);
            }

            return forms.Count == 0 ? [DraftForm.UserStory] : forms;
        }

        public static DraftForm Choose(RequirementMethods methods, DraftForm? chosen)
        {
            var forms = Of(methods);
            if (chosen is { } form)
            {
                return forms.Contains(form) ? form : throw new DomainException("The project does not use that form");
            }

            return forms.Count == 1 ? forms[0] : throw new DomainException("Choose the form of the drafts");
        }

        public static string NameOf(DraftForm form) => form switch
        {
            DraftForm.UseCase => "Use case",
            DraftForm.Requirement => "Requirement",
            _ => "User story"
        };
    }

    // US-108: "Draft for the gaps" on one regulation of a project
    public class GapDraftService
    {
        // R1: the requirements of one request, so the drafts stay reviewable and the prompt small
        public const int PerRequest = 10;

        private readonly IProjectRepository _projects;
        private readonly ComplianceQueries _compliance;
        private readonly IStoryDraftRepository _drafts;
        private readonly IApplicabilitySuggestionRepository _applicability;
        private readonly Risks.IRiskRepository _risks;
        private readonly IRationaleWriter _writer;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public GapDraftService(IProjectRepository projects, ComplianceQueries compliance, IStoryDraftRepository drafts, IApplicabilitySuggestionRepository applicability,
            Risks.IRiskRepository risks, IAiModels models, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _compliance = compliance;
            _drafts = drafts;
            _applicability = applicability;
            _risks = risks;
            _writer = models.Writer(AiTask.GapDrafts); // US-117: its own model, a larger one may be needed for drafts
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public sealed record Result(int Drafts, int NotApplicable, int Left);

        // R6, R7: one draft as the drafts page shows it, with the codes it covers
        public sealed record DraftView(StoryDraft Draft, IReadOnlyList<string> Codes)
        {
            public string CopyText => Draft.CopyText(Codes);
        }

        // The drafts page: the open drafts, and the accepted use cases and requirements that wait for the team's document
        public sealed record DraftsPage(Project Project, IReadOnlyList<DraftView> Open, IReadOnlyList<DraftView> Waiting);

        // Any member may read them (R9: the Auditor read-only); null for another organisation's project
        public async Task<DraftsPage?> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var compliance = await _compliance.ForProjectAsync(project.Id, cancellationToken);
            var codes = (compliance?.Frameworks ?? []).SelectMany(f => f.Requirements.Select(r => (r.Requirement.Id, Code: $"{f.Framework.ShortName} {r.Requirement.Code}")))
                .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Code);
            var drafts = (await _drafts.ListAsync(project.Id, cancellationToken))
                .Select(d => new DraftView(d, d.RequirementIds.Select(id => codes.GetValueOrDefault(id, "?")).ToList()))
                .OrderBy(d => d.Draft.Form).ThenBy(d => d.Draft.CreatedAt)
                .ToList();
            return new DraftsPage(project, drafts.Where(d => d.Draft.IsOpen).ToList(),
                drafts.Where(d => d.Draft.Status == StoryDraftStatus.Accepted && d.Draft.WorkItemId is null).ToList());
        }

        // R1–R5, R8–R10
        public async Task<Result> DraftAsync(Guid projectId, Guid frameworkId, DraftForm? chosen, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.LinkWork, cancellationToken); // R9
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException($"{project.Name} is closed");
            }

            var form = DraftForms.Choose(project.RequirementMethods, chosen);
            var compliance = await _compliance.ForProjectAsync(project.Id, cancellationToken) ?? throw new DomainException("Project not found");
            var framework = compliance.Frameworks.FirstOrDefault(f => f.Framework.Id == frameworkId) ?? throw new DomainException("The project does not follow that regulation");
            var drafts = await _drafts.ListAsync(project.Id, cancellationToken);
            var gaps = Gaps(framework, drafts, form);
            if (gaps.Count == 0)
            {
                return new Result(0, 0, 0);
            }

            var batch = gaps.Take(PerRequest).ToList();
            var risks = (await _risks.ListAsync(project.Id, cancellationToken)).Where(r => r.Status != Domain.Risks.RiskStatus.Closed).Select(r => r.Title).Take(10).ToList();
            var answer = GapDraftPrompt.Parse(await _writer.WriteAsync(GapDraftPrompt.For(project, form, batch, risks), cancellationToken), form,
                batch.Select(r => r.Code).ToList()); // R10: AiUnavailableException reaches the page, nothing changes

            var byCode = batch.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);
            var now = _clock.GetUtcNow().UtcDateTime;
            var made = answer.Drafts.Select(d => StoryDraft.ForGaps(project.Id, form, d.Covers.Select(c => byCode[c].Id).ToList(), d.Title, d.Role, d.Story, d.Rules,
                d.Examples, d.Postconditions, d.Fits, _writer.Name, GapDraftPrompt.Version, now)).ToList();
            foreach (var draft in made)
            {
                await _drafts.AddAsync(draft, cancellationToken);
            }

            // R5: "not applicable" is a suggestion a person decides (US-060 R6); one that waits already is not repeated
            var waiting = (await _applicability.ListAsync(project.Id, cancellationToken)).Where(s => s.IsPending).Select(s => s.RequirementId).ToHashSet();
            var notApplicable = answer.NotApplicable.Where(n => !waiting.Contains(byCode[n.Key].Id))
                .Select(n => ApplicabilitySuggestion.Create(project.Id, framework.Framework.Id, byCode[n.Key].Id, n.Value, $"Intended purpose: {project.IntendedPurpose}",
                    null, _writer.Name, GapDraftPrompt.Version, now))
                .ToList();
            await _applicability.AddRangeAsync(notApplicable, cancellationToken);

            await _log.LogAsync(ActivityAction.GapDraftsMade, project.Id,
                $"{made.Count} {DraftForms.NameOf(form).ToLowerInvariant()} drafts and {notApplicable.Count} \"not applicable\" suggestions for {framework.Framework.ShortName} ({_writer.Name}, {GapDraftPrompt.Version})",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new Result(made.Count, notApplicable.Count, Math.Max(0, gaps.Count - batch.Count));
        }

        // R1, R8: applicable, not recommendations, not covered, no story the AI already matched (its suggestions), not covered by
        // an open or accepted draft, and not discarded in this form until a person asks again
        public static IReadOnlyList<Requirement> Gaps(FrameworkCompliance framework, IReadOnlyList<StoryDraft> drafts, DraftForm form)
        {
            var taken = drafts.Where(d => d.Status != StoryDraftStatus.Discarded || (d.FromGaps && d.Form == form))
                .SelectMany(d => d.RequirementIds).ToHashSet();
            return framework.Requirements
                .Where(r => r.IsApplicable && !r.Requirement.IsRecommendation && !r.IsCovered && !r.IsCoveredNeedingReview && r.Suggestions.Count == 0)
                .Where(r => r.ApplicabilitySuggestion is null && !taken.Contains(r.Requirement.Id))
                .Select(r => r.Requirement)
                .ToList();
        }
    }
}
