using System.Globalization;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
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

    // US-205 R13: the check of a cross-mapping: the model reads both requirements and says how much they ask for the same
    public static class MappingCheckPrompt
    {
        public const string Version = "mapping-check-v1";
        public const string Marker = "Compare the two requirements";

        public static string For(Requirement from, Requirement to)
            => $"""
                {Marker} below. Do they ask for the same thing, so that meeting one meets the other?
                Use only these texts. Answer in exactly this shape:
                VERDICT: Same | Partly | Related
                DIFFERS: <what one asks for that the other does not, or "Nothing">

                Requirement {from.Code}: {from.Title}
                {from.Description}

                Other requirement {to.Code}: {to.Title}
                {to.Description}
                """;

        public static (MappingStrength Strength, string? Differs) Parse(string answer)
        {
            string? verdict = null, differs = null;
            foreach (var raw in (answer ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim().TrimStart('*', '-', ' ');
                if (line.StartsWith("VERDICT:", StringComparison.OrdinalIgnoreCase))
                {
                    verdict = line["VERDICT:".Length..].Trim().ToLowerInvariant().Trim('.', '"', '*', ' ');
                }
                else if (line.StartsWith("DIFFERS:", StringComparison.OrdinalIgnoreCase))
                {
                    differs = line["DIFFERS:".Length..].Trim();
                }
            }

            return verdict switch
            {
                null or "" => throw new AiUnavailableException("The AI's answer had no verdict"),
                _ when verdict.StartsWith("same") => (MappingStrength.Same, differs),
                _ when verdict.StartsWith("partly") || verdict.StartsWith("partial") => (MappingStrength.Partly, differs),
                _ when verdict.StartsWith("related") || verdict.StartsWith("different") || verdict.StartsWith("not") => (MappingStrength.Related, differs),
                _ => throw new AiUnavailableException("The AI's answer had no verdict")
            };
        }
    }

    // US-206 R2: one requirement with its pending matches in every other chosen framework
    public record MatchGroup(FollowedVersion FromVersion, Requirement From, IReadOnlyList<GroupMatch> Matches);

    public record GroupMatch(MappingRow Row, FollowedVersion ToVersion);

    // US-206 R3, R4: a project that follows both sides of an approved match, and whether the match is limited to it
    public record ProjectChoice(Guid Id, string Name, bool Chosen);

    public record ApprovedMatch(MappingRow Row, FollowedVersion FromVersion, FollowedVersion ToVersion, IReadOnlyList<ProjectChoice> Projects)
    {
        // e.g. "All projects" or "Compliance Tracker, Acme Portal"
        public string AppliesTo => Row.Mapping.ProjectIds.Length == 0
            ? "All projects"
            : string.Join(", ", Projects.Where(p => p.Chosen).Select(p => p.Name));
    }

    // US-206: the Cross-mapping page for two or more frameworks at once
    public record CrossMappingSetView(IReadOnlyList<FollowedVersion> Versions, IReadOnlyList<FollowedVersion> Chosen,
        IReadOnlyList<MatchGroup> Groups, IReadOnlyList<ApprovedMatch> Approved);

    // US-037: the AI suggests which requirements in two framework versions ask for the same thing; a person decides.
    // US-206: or in several at once, every pair in one run.
    public class CrossMappingService
    {
        // US-205 R14: only the best match or two per requirement
        public const int PerRequirement = 2;

        // US-206 R1: more would ask the local AI to compare too many pairs in one go
        public const int MostAtOnce = 5;

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
        public Task<int> SuggestAsync(Guid fromVersionId, Guid toVersionId, CancellationToken cancellationToken = default)
            => SuggestManyAsync([fromVersionId, toVersionId], cancellationToken);

        // US-206 R1: two or more versions in one run: every pair, in the order chosen, from one embedding of all their texts
        public async Task<int> SuggestManyAsync(IReadOnlyList<Guid> versionIds, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var chosen = Choose(await FollowedAsync(organization.Id, cancellationToken), versionIds);

            var requirements = chosen.ToDictionary(v => v.Version.Id, v => v.Version.Requirements.Where(r => !r.IsRecommendation).ToList());
            if (requirements.Values.Any(r => r.Count == 0))
            {
                throw new DomainException(chosen.Count == 2 ? "Both frameworks need requirements to compare" : "Every framework needs requirements to compare");
            }

            var all = chosen.SelectMany(v => requirements[v.Version.Id]).ToList();
            var model = _models.Embedding(AiTask.Embedding);
            IReadOnlyList<float[]> embedded;
            try
            {
                embedded = await model.EmbedAsync(all.Select(MappingSuggestionService.RequirementText).ToList(), cancellationToken);
            }
            catch (AiUnavailableException)
            {
                throw new DomainException("The local AI cannot be reached right now, so nothing was compared");
            }

            var vectorOf = all.Select((r, i) => (r.Id, Vector: embedded[i])).ToDictionary(x => x.Id, x => x.Vector);
            var existing = await _mappings.ListAsync(organization.Id, cancellationToken);
            var made = new List<RequirementMapping>();
            var checking = true; // US-205 R7: once the checking model cannot be reached, the rest is not checked either
            foreach (var (from, to) in Pairs(chosen))
            {
                var pair = existing.Where(m => SamePair(m, from.Version.Id, to.Version.Id)).ToList();
                foreach (var pending in pair.Where(m => m.IsPending))
                {
                    pending.Replace();
                }

                var fromRequirements = requirements[from.Version.Id];
                var toRequirements = requirements[to.Version.Id];
                // US-205 R14: nothing below "Partly" is suggested
                var candidates = Best(fromRequirements, toRequirements, fromRequirements.Concat(toRequirements).Select(r => vectorOf[r.Id]).ToList(),
                        Math.Max(_limits.MinimumScoreForStories, RequirementMapping.PartlyFrom),
                        (a, b) => pair.Any(m => !m.IsPending && m.Status != SuggestionStatus.Replaced && m.Joins(a, b)))
                    .Select(m => (Found: m, Mapping: RequirementMapping.Suggest(organization.Id, from.Version.Id, to.Version.Id, m.From.Id, m.To.Id, m.Score, m.Rank,
                        Rationale(m.From, m.To, m.Score), model.Name, _clock.GetUtcNow().UtcDateTime)))
                    .ToList();

                // US-205 R13: the checking model reads both requirements; "Related" is dropped, and without the model a match
                // stays as the texts' likeness says, marked "not checked"
                foreach (var (found, mapping) in candidates)
                {
                    if (checking && await CheckAsync(found.From, found.To, cancellationToken) is { } answer)
                    {
                        if (answer.Strength == MappingStrength.Related)
                        {
                            continue;
                        }

                        mapping.Checked(answer.Strength, answer.Differs, _models.Writer(AiTask.EvidenceCheck).Name);
                    }
                    else
                    {
                        checking = false;
                    }

                    made.Add(mapping);
                }
            }

            await _mappings.AddRangeAsync(made, cancellationToken);
            await _log.LogAsync(ActivityAction.MappingsSuggested, organization.Id,
                $"{made.Count} cross-mapping suggestions between {Names(chosen.Select(v => v.Label).ToList())} ({model.Name})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return made.Count;
        }

        // US-205 R13: one pair read by the checking model; null when it cannot be reached or answers in a wrong format
        private async Task<(MappingStrength Strength, string? Differs)?> CheckAsync(Requirement from, Requirement to, CancellationToken cancellationToken)
        {
            try
            {
                return MappingCheckPrompt.Parse(await _models.Writer(AiTask.EvidenceCheck).WriteAsync(MappingCheckPrompt.For(from, to), cancellationToken));
            }
            catch (AiUnavailableException)
            {
                return null;
            }
        }

        // US-205 R14: "Reject all not checked" clears the matches the model did not read, in one step with a reason
        public async Task<int> RejectUncheckedAsync(IReadOnlyList<Guid> versionIds, RejectionReason? reason, string? comment, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var chosen = versionIds.Distinct().ToHashSet();
            var unchecked_ = (await _mappings.ListAsync(organization.Id, cancellationToken))
                .Where(m => m.IsPending && !m.IsChecked && chosen.Contains(m.FromVersionId) && chosen.Contains(m.ToVersionId))
                .ToList();
            if (unchecked_.Count == 0)
            {
                throw new DomainException("No match waits that is not checked");
            }

            var at = _clock.GetUtcNow().UtcDateTime;
            foreach (var mapping in unchecked_)
            {
                mapping.Reject(reason, comment, user.Id, user.Role, at);
            }

            await _log.LogAsync(ActivityAction.MappingRejected, organization.Id, $"{unchecked_.Count} cross-mappings that were not checked ({reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return unchecked_.Count;
        }

        // US-206 R1: the versions chosen, in the order given, each once; at least two and at most MostAtOnce
        public static IReadOnlyList<FollowedVersion> Choose(IReadOnlyList<FollowedVersion> followed, IReadOnlyList<Guid> versionIds)
        {
            var chosen = new List<FollowedVersion>();
            foreach (var id in versionIds.Distinct())
            {
                chosen.Add(followed.FirstOrDefault(v => v.Version.Id == id) ?? throw new DomainException("Choose a framework the organisation follows"));
            }

            if (chosen.Count < 2)
            {
                throw new DomainException("Choose two different frameworks");
            }

            if (chosen.Count > MostAtOnce)
            {
                throw new DomainException($"Choose at most {MostAtOnce} frameworks at once");
            }

            return chosen;
        }

        // US-206 R1: every pair once, the earlier chosen on the left: A, B, C → (A, B), (A, C), (B, C)
        public static IReadOnlyList<(T From, T To)> Pairs<T>(IReadOnlyList<T> items)
        {
            var pairs = new List<(T, T)>();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    pairs.Add((items[i], items[j]));
                }
            }

            return pairs;
        }

        // e.g. "CRA v1, NIS2 v1 and ISO v1"
        public static string Names(IReadOnlyList<string> names)
            => names.Count <= 1 ? string.Join("", names) : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";

        // A match between the two versions, whichever side it was suggested from
        public static bool SamePair(RequirementMapping mapping, Guid a, Guid b)
            => (mapping.FromVersionId == a && mapping.ToVersionId == b) || (mapping.FromVersionId == b && mapping.ToVersionId == a);

        // US-206 R2, R3: the chosen frameworks' pending matches grouped by requirement, and the approved ones with their projects.
        // Null outside an organisation; with fewer than two chosen, only the versions to choose from.
        public async Task<CrossMappingSetView?> ForSetAsync(IReadOnlyList<Guid> versionIds, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return null;
            }

            var versions = await FollowedAsync(organization.Id, cancellationToken);
            var chosen = versionIds.Distinct().Select(id => versions.FirstOrDefault(v => v.Version.Id == id)).OfType<FollowedVersion>().ToList();
            if (chosen.Count < 2)
            {
                return new CrossMappingSetView(versions, chosen, [], []);
            }

            var versionOf = chosen.SelectMany(v => v.Version.Requirements.Select(r => (r.Id, Version: v))).ToDictionary(x => x.Id, x => x.Version);
            var requirementOf = chosen.SelectMany(v => v.Version.Requirements).ToDictionary(r => r.Id);
            var rows = (await _mappings.ListAsync(organization.Id, cancellationToken))
                .Where(m => requirementOf.ContainsKey(m.FromRequirementId) && requirementOf.ContainsKey(m.ToRequirementId))
                .Select(m => new MappingRow(m, requirementOf[m.FromRequirementId], requirementOf[m.ToRequirementId]))
                .ToList();

            var order = chosen.SelectMany(v => v.Version.Requirements).Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
            var groups = rows.Where(r => r.Mapping.IsPending)
                .GroupBy(r => r.From.Id)
                .OrderBy(g => order[g.Key])
                .Select(g => new MatchGroup(versionOf[g.Key], requirementOf[g.Key],
                    g.OrderBy(r => order[r.To.Id]).Select(r => new GroupMatch(r, versionOf[r.To.Id])).ToList()))
                .ToList();

            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsArchived).ToList();
            var approved = rows.Where(r => r.Mapping.Status == SuggestionStatus.Approved)
                .OrderBy(r => order[r.From.Id]).ThenBy(r => order[r.To.Id])
                .Select(r => new ApprovedMatch(r, versionOf[r.From.Id], versionOf[r.To.Id],
                    projects.Where(p => Follows(p, r.Mapping))
                        .Select(p => new ProjectChoice(p.Id, p.IsOrganisationArea ? "Organisation" : p.Name, r.Mapping.ProjectIds.Contains(p.Id)))
                        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();

            return new CrossMappingSetView(versions, chosen, groups, approved);
        }

        // US-206 R2: one decision for all of a requirement's pending matches in the chosen frameworks
        public async Task<int> DecideAllAsync(Guid fromRequirementId, IReadOnlyList<Guid> versionIds, bool approve, RejectionReason? reason, string? comment,
            CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var chosen = versionIds.Distinct().ToHashSet();
            var pending = (await _mappings.ListAsync(organization.Id, cancellationToken))
                .Where(m => m.IsPending && m.FromRequirementId == fromRequirementId && chosen.Contains(m.FromVersionId) && chosen.Contains(m.ToVersionId))
                .ToList();
            if (pending.Count == 0)
            {
                throw new DomainException("Nothing waits for a decision");
            }

            var at = _clock.GetUtcNow().UtcDateTime;
            foreach (var mapping in pending)
            {
                if (approve)
                {
                    mapping.Approve(user.Id, user.Role, at);
                    await _log.LogAsync(ActivityAction.MappingApproved, organization.Id, $"Cross-mapping {mapping.Strength}: {await PairAsync(organization.Id, mapping, cancellationToken)}", cancellationToken);
                }
                else
                {
                    mapping.Reject(reason, comment, user.Id, user.Role, at);
                    await _log.LogAsync(ActivityAction.MappingRejected, organization.Id, $"Cross-mapping: {await PairAsync(organization.Id, mapping, cancellationToken)} ({reason})", cancellationToken);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return pending.Count;
        }

        // US-206 R3, R4: limit an approved match to projects that follow both sides; none chosen means all of them
        public async Task ChangeProjectsAsync(Guid mappingId, IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken = default)
        {
            var (organization, _, mapping) = await DecideAsync(mappingId, cancellationToken);
            var projects = await _projects.ListAsync(organization.Id, cancellationToken);
            var chosen = projectIds.Distinct().Select(id => projects.FirstOrDefault(p => p.Id == id)).ToList();
            if (chosen.Any(p => p is null || !Follows(p, mapping)))
            {
                throw new DomainException("Choose projects that follow both frameworks");
            }

            if (!mapping.LimitTo(projectIds))
            {
                return;
            }

            var names = chosen.OfType<Domain.Projects.Project>().Select(p => p.IsOrganisationArea ? "Organisation" : p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            await _log.LogAsync(ActivityAction.MappingProjectsChanged, organization.Id,
                $"{await PairAsync(organization.Id, mapping, cancellationToken)}: {(names.Count == 0 ? "all projects" : string.Join(", ", names))}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3: a project follows a match when it uses both versions
        public static bool Follows(Domain.Projects.Project project, RequirementMapping mapping)
            => project.Frameworks.Any(f => f.FrameworkVersionId == mapping.FromVersionId) && project.Frameworks.Any(f => f.FrameworkVersionId == mapping.ToVersionId);

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
