using System.Globalization;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Ai
{
    // Storage for cross-mappings (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IRequirementMappingRepository
    {
        Task<IReadOnlyList<RequirementMapping>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<RequirementMapping> mappings, CancellationToken cancellationToken = default);
    }

    // A framework version the organisation follows, as the page lists it
    public record FollowedVersion(Framework Framework, FrameworkVersion Version)
    {
        public string Label => $"{Framework.ShortName} v{Version.Number}";
    }

    // One suggestion or match with its two requirements
    public record MappingRow(RequirementMapping Mapping, Requirement From, Requirement To);

    // The Cross-mapping page for one pair (R1–R3): the versions to choose from, the pending suggestions, the approved matches
    public record CrossMappingView(IReadOnlyList<FollowedVersion> Versions, FollowedVersion? From, FollowedVersion? To,
        IReadOnlyList<MappingRow> Pending, IReadOnlyList<MappingRow> Approved);

    // US-037: the AI suggests which requirements in two framework versions ask for the same thing; a person decides
    public class CrossMappingService
    {
        public const int PerRequirement = 4;

        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IRequirementMappingRepository _mappings;
        private readonly IAiModels _models;
        private readonly IAiLimits _limits;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public CrossMappingService(IProjectRepository projects, IFrameworkRepository frameworks, IRequirementMappingRepository mappings, IAiModels models,
            IAiLimits limits, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _mappings = mappings;
            _models = models;
            _limits = limits;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R6: every member sees the page; null outside an organisation
        public async Task<CrossMappingView?> ForPairAsync(Guid? fromVersionId, Guid? toVersionId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return null;
            }

            var versions = await FollowedAsync(organization.Id, cancellationToken);
            var from = versions.FirstOrDefault(v => v.Version.Id == fromVersionId);
            var to = versions.FirstOrDefault(v => v.Version.Id == toVersionId);
            if (from is null || to is null || from.Version.Id == to.Version.Id)
            {
                return new CrossMappingView(versions, from, to, [], []);
            }

            var requirements = from.Version.Requirements.Concat(to.Version.Requirements).ToDictionary(r => r.Id);
            var rows = (await _mappings.ListAsync(organization.Id, cancellationToken))
                .Where(m => m.FromVersionId == from.Version.Id && m.ToVersionId == to.Version.Id)
                .Where(m => requirements.ContainsKey(m.FromRequirementId) && requirements.ContainsKey(m.ToRequirementId))
                .Select(m => new MappingRow(m, requirements[m.FromRequirementId], requirements[m.ToRequirementId]))
                .ToList();
            var order = from.Version.Requirements.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
            return new CrossMappingView(versions, from, to,
                rows.Where(r => r.Mapping.IsPending).OrderBy(r => order[r.From.Id]).ThenBy(r => r.Mapping.Rank).ToList(),
                rows.Where(r => r.Mapping.Status == SuggestionStatus.Approved).OrderBy(r => order[r.From.Id]).ToList());
        }

        // R1–R4: compares every requirement of one version with the other's, by their texts' vectors (the Embedding task's
        // model); the best 4 per requirement over the limit become suggestions. Pending ones of the pair are replaced; a
        // decided pair is never suggested again.
        public async Task<int> SuggestAsync(Guid fromVersionId, Guid toVersionId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var versions = await FollowedAsync(organization.Id, cancellationToken);
            var from = versions.FirstOrDefault(v => v.Version.Id == fromVersionId) ?? throw new DomainException("Choose a framework the organisation follows");
            var to = versions.FirstOrDefault(v => v.Version.Id == toVersionId) ?? throw new DomainException("Choose a framework the organisation follows");
            if (from.Version.Id == to.Version.Id)
            {
                throw new DomainException("Choose two different frameworks");
            }

            var fromRequirements = from.Version.Requirements.Where(r => !r.IsRecommendation).ToList();
            var toRequirements = to.Version.Requirements.Where(r => !r.IsRecommendation).ToList();
            if (fromRequirements.Count == 0 || toRequirements.Count == 0)
            {
                throw new DomainException("Both frameworks need requirements to compare");
            }

            var model = _models.Embedding(AiTask.Embedding);
            IReadOnlyList<float[]> vectors;
            try
            {
                vectors = await model.EmbedAsync(fromRequirements.Concat(toRequirements).Select(MappingSuggestionService.RequirementText).ToList(), cancellationToken);
            }
            catch (AiUnavailableException)
            {
                throw new DomainException("The local AI cannot be reached right now, so nothing was compared");
            }

            var existing = (await _mappings.ListAsync(organization.Id, cancellationToken))
                .Where(m => m.FromVersionId == from.Version.Id && m.ToVersionId == to.Version.Id).ToList();
            foreach (var pending in existing.Where(m => m.IsPending))
            {
                pending.Replace();
            }

            var made = Best(fromRequirements, toRequirements, vectors, Math.Max(_limits.MinimumScoreForStories, RequirementMapping.RelatedFrom),
                    (a, b) => existing.Any(m => !m.IsPending && m.Status != SuggestionStatus.Replaced && m.Joins(a, b)))
                .Select(m => RequirementMapping.Suggest(organization.Id, from.Version.Id, to.Version.Id, m.From.Id, m.To.Id, m.Score, m.Rank,
                    Rationale(m.From, m.To, m.Score), model.Name, _clock.GetUtcNow().UtcDateTime))
                .ToList();

            await _mappings.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.MappingsSuggested, organization.Id,
                $"{made.Count} cross-mapping suggestions between {from.Label} and {to.Label} ({model.Name})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return made.Count;
        }

        // R2: for each requirement on the left, the best 4 on the right over the limit, best first; decided pairs are left out
        public static IReadOnlyList<(Requirement From, Requirement To, double Score, int Rank)> Best(
            IReadOnlyList<Requirement> from, IReadOnlyList<Requirement> to, IReadOnlyList<float[]> vectors, double minimum, Func<Guid, Guid, bool> decided)
        {
            var result = new List<(Requirement, Requirement, double, int)>();
            for (var i = 0; i < from.Count; i++)
            {
                var rank = 0;
                foreach (var (candidate, score) in to.Select((r, j) => (r, Similarity.Cosine(vectors[i], vectors[from.Count + j])))
                             .Where(c => c.Item2 >= minimum && !decided(from[i].Id, c.r.Id))
                             .OrderByDescending(c => c.Item2)
                             .Take(PerRequirement))
                {
                    result.Add((from[i], candidate, score, ++rank));
                }
            }

            return result;
        }

        // R2: a short reason, without asking a text model for each of hundreds of pairs
        public static string Rationale(Requirement from, Requirement to, double score)
            => $"{RequirementMapping.StrengthOf(score)}: \"{from.Code} {from.Title}\" and \"{to.Code} {to.Title}\" are worded alike (similarity {score.ToString("0.00", CultureInfo.InvariantCulture)})";

        public async Task ApproveAsync(Guid mappingId, CancellationToken cancellationToken = default)
        {
            var (organization, user, mapping) = await DecideAsync(mappingId, cancellationToken);
            mapping.Approve(user.Id, user.Role, _clock.GetUtcNow().UtcDateTime);
            await _log.LogAsync(ActivityAction.MappingApproved, organization.Id, $"Cross-mapping {mapping.Strength}: {await PairAsync(organization.Id, mapping, cancellationToken)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectAsync(Guid mappingId, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (organization, user, mapping) = await DecideAsync(mappingId, cancellationToken);
            mapping.Reject(reason, comment, user.Id, user.Role, _clock.GetUtcNow().UtcDateTime);
            await _log.LogAsync(ActivityAction.MappingRejected, organization.Id, $"Cross-mapping: {await PairAsync(organization.Id, mapping, cancellationToken)} ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The versions the organisation follows: those its projects (and its organisation area) use
        private async Task<IReadOnlyList<FollowedVersion>> FollowedAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            var used = (await _projects.ListAsync(organizationId, cancellationToken)).SelectMany(p => p.Frameworks).ToList();
            var frameworks = await _frameworks.ListAvailableWithRequirementsAsync(organizationId, used.Select(u => u.FrameworkId).Distinct().ToList(), cancellationToken);
            var versionIds = used.Select(u => u.FrameworkVersionId).ToHashSet();
            return frameworks.SelectMany(f => f.Versions.Where(v => versionIds.Contains(v.Id)).Select(v => new FollowedVersion(f, v)))
                .OrderBy(v => v.Framework.Name, StringComparer.Ordinal).ThenBy(v => v.Version.Number).ToList();
        }

        private async Task<(Domain.Organizations.Organization Organization, User User, RequirementMapping Mapping)> DecideAsync(Guid mappingId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var mapping = (await _mappings.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(m => m.Id == mappingId)
                ?? throw new DomainException("Suggestion not found");
            return (organization, user, mapping);
        }

        private async Task<string> PairAsync(Guid organizationId, RequirementMapping mapping, CancellationToken cancellationToken)
        {
            var versions = await FollowedAsync(organizationId, cancellationToken);
            string Name(Guid versionId, Guid requirementId)
                => versions.FirstOrDefault(v => v.Version.Id == versionId) is { } v
                   && v.Version.Requirements.FirstOrDefault(r => r.Id == requirementId) is { } r ? $"{v.Framework.ShortName} {r.Code}" : "?";
            return $"{Name(mapping.FromVersionId, mapping.FromRequirementId)} ↔ {Name(mapping.ToVersionId, mapping.ToRequirementId)}";
        }
    }
}
