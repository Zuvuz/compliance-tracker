using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Domain.Users;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Application.Compliance
{
    // Storage for evidence suggestions (US-058, implemented in Infrastructure)
    public interface IEvidenceSuggestionRepository
    {
        // Every suggestion of the project, in every status
        Task<IReadOnlyList<EvidenceSuggestion>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<EvidenceSuggestion> suggestions, CancellationToken cancellationToken = default);
    }

    // R1, R9: reads a test results file (TRX or JUnit XML). Throws DomainException when the file is neither.
    public interface ITestResultsReader
    {
        IReadOnlyList<TestCaseResult> Read(byte[] content);
    }

    // One test: its name, whether it passed, and its tags, e.g. "cra-I.2(d)" (with or without the "@")
    public record TestCaseResult(string Name, bool Passed, IReadOnlyList<string> Tags);

    // R3: the merged pull requests that closed an issue (implemented by the GitHub adapter, read-only, US-031 token)
    public interface IPullRequestReader
    {
        // Throws WorkItemSourceException when GitHub cannot be read
        Task<IReadOnlyList<ClosingPullRequest>> ClosingPullRequestsAsync(
            string repository, int issueNumber, string? token, CancellationToken cancellationToken = default);
    }

    public record ClosingPullRequest(int Number, string Title, string Url);

    // What an upload of test results found
    public record TestRunResult(int Suggested, int Blocked, IReadOnlyList<string> UnknownTags);

    // US-058: evidence suggestions from passing tests and from the pull requests that closed stories. Tags and links
    // decide them; a person with LinkWork accepts (it becomes normal evidence, US-057/US-059) or rejects (R4, R5).
    public class EvidenceSuggestionService
    {
        // R9
        public const long MaxResultsSize = 10 * 1024 * 1024;

        // GitHub's limits: at most this many closed stories are asked about per sync
        public const int MaxStoriesPerSync = 20;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IRequirementApplicabilityRepository _applicability;
        private readonly IRequirementLinkRepository _links;
        private readonly IWorkItemRepository _workItems;
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceSuggestionRepository _suggestions;
        private readonly ITestResultsReader _results;
        private readonly IPullRequestReader _pullRequests;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public EvidenceSuggestionService(
            IProjectRepository projects,
            IFrameworkRepository frameworks,
            IRequirementApplicabilityRepository applicability,
            IRequirementLinkRepository links,
            IWorkItemRepository workItems,
            IEvidenceRepository evidence,
            IEvidenceSuggestionRepository suggestions,
            ITestResultsReader results,
            IPullRequestReader pullRequests,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _applicability = applicability;
            _links = links;
            _workItems = workItems;
            _evidence = evidence;
            _suggestions = suggestions;
            _results = results;
            _pullRequests = pullRequests;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R2, R6, R9: one suggestion per requirement in the run. A tag is "<framework>-<code>", e.g. "cra-I.2(d)",
        // where the framework is the short name of one the project follows, as in the Compliance tab's address.
        public async Task<TestRunResult> UploadTestResultsAsync(Guid projectId, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (_, _, project) = await OpenProjectAsync(projectId, cancellationToken);
            if (content.Length == 0)
            {
                throw new DomainException("Choose a test results file");
            }

            if (content.Length > MaxResultsSize)
            {
                throw new DomainException("The test results file is larger than 10 MB");
            }

            var tests = _results.Read(content);
            var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var requirements = await RequirementsByTagAsync(project, cancellationToken);
            var notApplicable = await NotApplicableAsync(project, cancellationToken);
            var existing = await _suggestions.ListAsync(project.Id, cancellationToken);

            var perRequirement = new Dictionary<Guid, (Requirement Requirement, List<string> Passing, List<string> Failing)>();
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var test in tests)
            {
                foreach (var tag in test.Tags.Select(t => t.TrimStart('@')).Distinct())
                {
                    var dash = tag.IndexOf('-');
                    if (dash <= 0)
                    {
                        continue;
                    }

                    var (slug, code) = (tag[..dash].ToLowerInvariant(), tag[(dash + 1)..]);
                    if (!requirements.TryGetValue(slug, out var codes))
                    {
                        continue; // not a framework the project follows, e.g. "US-004"
                    }

                    if (!codes.TryGetValue(code, out var requirement))
                    {
                        unknown.Add("@" + tag); // R1: listed but ignored
                        continue;
                    }

                    if (notApplicable.Contains(requirement.Id))
                    {
                        continue; // R6
                    }

                    if (!perRequirement.TryGetValue(requirement.Id, out var found))
                    {
                        found = (requirement, new List<string>(), new List<string>());
                        perRequirement[requirement.Id] = found;
                    }

                    (test.Passed ? found.Passing : found.Failing).Add(test.Name);
                }
            }

            var made = perRequirement.Values
                .Where(r => !existing.Any(s => s.RequirementId == r.Requirement.Id && s.ExternalId == sha256)) // the same file twice
                .Select(r => EvidenceSuggestion.FromTestRun(project.Id, r.Requirement.Id, sha256, fileName, r.Passing, r.Failing, Now))
                .ToList();

            await _suggestions.AddRangeAsync(made, cancellationToken);
            var blocked = made.Count(s => s.Status == EvidenceSuggestionStatus.Blocked);
            await _log.LogAsync(ActivityAction.EvidenceSuggested, project.Id,
                $"Test results {fileName}: {made.Count - blocked} suggestions, {blocked} blocked by failing tests", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new TestRunResult(made.Count - blocked, blocked, unknown.ToList());
        }

        // R3: after a GitHub sync (US-018). For closed stories linked to requirements, the merged pull requests that
        // closed them; one suggestion per requirement and pull request, never again after a decision (R5).
        // The caller saves. GitHub errors are ignored here: the sync itself already reports them.
        public async Task<int> SuggestFromPullRequestsAsync(Project project, string? token, CancellationToken cancellationToken = default)
        {
            if (project.GitHub is not { } github)
            {
                return 0;
            }

            var links = await _links.ListActiveAsync(project.Id, cancellationToken);
            var notApplicable = await NotApplicableAsync(project, cancellationToken);
            var existing = await _suggestions.ListAsync(project.Id, cancellationToken);
            var closed = (await _workItems.ListAsync(project.Id, WorkItemSource.GitHub, cancellationToken))
                .Where(w => w.State == WorkItemState.Closed && links.Any(l => l.WorkItemId == w.Id))
                .Where(w => !existing.Any(s => s.Source == EvidenceSourceKind.PullRequest && s.ExternalId.StartsWith(w.ExternalReference + ">", StringComparison.Ordinal)))
                .Take(MaxStoriesPerSync)
                .ToList();

            var made = new List<EvidenceSuggestion>();
            foreach (var story in closed)
            {
                var hash = story.ExternalReference.LastIndexOf('#');
                if (hash < 0 || !int.TryParse(story.ExternalReference[(hash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                {
                    continue;
                }

                IReadOnlyList<ClosingPullRequest> pullRequests;
                try
                {
                    pullRequests = await _pullRequests.ClosingPullRequestsAsync(github.Repository, number, token, cancellationToken);
                }
                catch (WorkItemSourceException)
                {
                    continue;
                }

                foreach (var pullRequest in pullRequests)
                {
                    foreach (var link in links.Where(l => l.WorkItemId == story.Id && !notApplicable.Contains(l.RequirementId)))
                    {
                        if (existing.Concat(made).Any(s => s.RequirementId == link.RequirementId && s.ExternalId.EndsWith(">" + github.Repository + "#" + pullRequest.Number, StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        made.Add(EvidenceSuggestion.FromPullRequest(project.Id, link.RequirementId, story.ExternalReference,
                            github.Repository, pullRequest.Number, pullRequest.Title, pullRequest.Url, Now));
                    }
                }
            }

            if (made.Count > 0)
            {
                await _suggestions.AddRangeAsync(made, cancellationToken);
                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.EvidenceSuggested, project.Id,
                    $"{made.Count} suggestions from pull requests", cancellationToken);
            }

            return made.Count;
        }

        // R4: it becomes normal evidence with a link to the requirement. A test run becomes a Markdown file that lists
        // the passing scenarios and the results file's SHA-256; a pull request becomes a link.
        public async Task AcceptAsync(Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireAsync(project, suggestionId, cancellationToken);
            var requirement = (await RequirementsAsync(organizationId, project, cancellationToken)).GetValueOrDefault(suggestion.RequirementId)
                ?? throw new DomainException("The requirement is not in a framework version the project follows");
            if ((await NotApplicableAsync(project, cancellationToken)).Contains(requirement.Id))
            {
                throw new DomainException($"{requirement.Code} does not apply to the project");
            }

            Evidence evidence;
            byte[]? content = null;
            if (suggestion.Source == EvidenceSourceKind.PullRequest)
            {
                evidence = Evidence.ForLink(project.Id, suggestion.Title, suggestion.Url!, null, user.Id, user.Role, Now);
            }
            else
            {
                var file = EvidenceFile.Create($"test-run-{suggestion.CreatedAt:yyyy-MM-dd}-{requirement.Code}.md", TestRunReport(suggestion, requirement));
                evidence = Evidence.ForFile(project.Id, suggestion.Title, file, null, user.Id, user.Role, Now);
                content = file.Content;
            }

            suggestion.Accept(evidence.Id, user.Id, user.Role, Now);
            await _evidence.AddAsync(evidence, content, cancellationToken);
            await _evidence.AddLinkAsync(EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, user.Id, user.Role, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.EvidenceSuggestionAccepted, project.Id, $"{requirement.Code}: {suggestion.Title}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5
        public async Task RejectAsync(Guid projectId, Guid suggestionId, string? reason, CancellationToken cancellationToken = default)
        {
            var (organizationId, user, project) = await OpenProjectAsync(projectId, cancellationToken);
            var suggestion = await RequireAsync(project, suggestionId, cancellationToken);
            var code = (await RequirementsAsync(organizationId, project, cancellationToken)).GetValueOrDefault(suggestion.RequirementId)?.Code ?? "?";

            suggestion.Reject(reason, user.Id, user.Role, Now);
            await _log.LogAsync(ActivityAction.EvidenceSuggestionRejected, project.Id, $"{code}: {suggestion.Title} ({suggestion.RejectionReason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static byte[] TestRunReport(EvidenceSuggestion suggestion, Requirement requirement)
        {
            var text = new StringBuilder();
            text.AppendLine($"# {suggestion.Title}");
            text.AppendLine();
            text.AppendLine($"- Requirement: {requirement.Code} {requirement.Title}");
            text.AppendLine($"- Results file: {suggestion.ResultsFileName}");
            text.AppendLine($"- SHA-256 of the results file: {suggestion.ExternalId}");
            text.AppendLine($"- Uploaded: {suggestion.CreatedAt:dd-MM-yyyy HH:mm} UTC");
            text.AppendLine();
            text.AppendLine($"## Passing scenarios ({suggestion.PassingCount})");
            text.AppendLine();
            foreach (var name in (suggestion.PassingScenarios ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                text.AppendLine($"- {name}");
            }

            return Encoding.UTF8.GetBytes(text.ToString());
        }

        // framework slug -> code -> requirement, for the versions the project follows now
        private async Task<Dictionary<string, Dictionary<string, Requirement>>> RequirementsByTagAsync(Project project, CancellationToken cancellationToken)
        {
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(
                project.OrganizationId, project.Frameworks.Select(f => f.FrameworkId).ToList(), cancellationToken);
            var result = new Dictionary<string, Dictionary<string, Requirement>>(StringComparer.Ordinal);
            foreach (var used in project.Frameworks)
            {
                var framework = frameworks.FirstOrDefault(f => f.Id == used.FrameworkId);
                var version = framework?.Versions.FirstOrDefault(v => v.Id == used.FrameworkVersionId);
                if (framework is not null && version is not null)
                {
                    result[FrameworkSlugs.Slug(framework.Name)] = version.Requirements.ToDictionary(r => r.Code, StringComparer.Ordinal);
                }
            }

            return result;
        }

        private async Task<Dictionary<Guid, Requirement>> RequirementsAsync(Guid organizationId, Project project, CancellationToken cancellationToken)
            => (await RequirementsByTagAsync(project, cancellationToken)).Values.SelectMany(c => c.Values).ToDictionary(r => r.Id);

        private async Task<HashSet<Guid>> NotApplicableAsync(Project project, CancellationToken cancellationToken)
            => (await _applicability.ListActiveAsync(project.Id, cancellationToken)).Select(a => a.RequirementId).ToHashSet();

        private async Task<EvidenceSuggestion> RequireAsync(Project project, Guid suggestionId, CancellationToken cancellationToken)
            => (await _suggestions.ListAsync(project.Id, cancellationToken)).FirstOrDefault(s => s.Id == suggestionId)
               ?? throw new DomainException("The suggestion was not found");

        private async Task<(Guid OrganizationId, Domain.Users.User User, Project Project)> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (organization.Id, user, project);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
