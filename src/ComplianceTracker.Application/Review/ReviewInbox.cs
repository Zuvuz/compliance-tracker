using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Approvals;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Security;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Review
{
    // US-169 R3: the kinds of things that wait for a person's decision, and where each is decided
    public enum ReviewKind
    {
        StorySuggestions,
        NeedsAction,
        NotApplicable,
        EvidenceChecks,
        StoryDrafts,
        EvidenceSuggestions,
        RiskDrafts,
        MeasureSuggestions,
        SecurityEventSuggestions,
        Approvals,

        // US-195 R5: evidence, documents and supplier evidence without a review date or end date
        WithoutDate
    }

    // One line of the inbox: a kind in a project and how many wait
    public record ReviewItem(Guid ProjectId, string ProjectName, ReviewKind Kind, int Count);

    public static class ReviewKinds
    {
        // R4: the right a member needs to decide each kind
        public static Permission RightFor(ReviewKind kind) => kind switch
        {
            ReviewKind.StorySuggestions or ReviewKind.NeedsAction or ReviewKind.NotApplicable or ReviewKind.EvidenceChecks => Permission.DecideCompliance,
            ReviewKind.RiskDrafts or ReviewKind.MeasureSuggestions => Permission.ManageRisks,
            _ => Permission.LinkWork
        };

        public static string Title(ReviewKind kind, int count) => kind switch
        {
            ReviewKind.StorySuggestions => Plural(count, "AI story suggestion", "AI story suggestions"),
            ReviewKind.NeedsAction => Plural(count, "requirement marked as needing action", "requirements marked as needing action"),
            ReviewKind.NotApplicable => Plural(count, "\"not applicable\" suggestion", "\"not applicable\" suggestions"),
            ReviewKind.EvidenceChecks => Plural(count, "AI evidence check", "AI evidence checks"),
            ReviewKind.StoryDrafts => Plural(count, "AI story draft", "AI story drafts"),
            ReviewKind.EvidenceSuggestions => Plural(count, "AI evidence suggestion", "AI evidence suggestions"),
            ReviewKind.RiskDrafts => Plural(count, "AI risk draft", "AI risk drafts"),
            ReviewKind.MeasureSuggestions => Plural(count, "AI measure suggestion", "AI measure suggestions"),
            ReviewKind.SecurityEventSuggestions => Plural(count, "suggested security event", "suggested security events"),
            ReviewKind.WithoutDate => Plural(count, "item without a review date", "items without a review date"),
            _ => Plural(count, "approval", "approvals")
        };

        private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
    }

    // US-169 R3, R4: everything that waits for the signed-in member's decision, per project of the organisation. Closed
    // projects are left out. A fixed number of queries per project (one per kind).
    public class ReviewInbox
    {
        private readonly IProjectRepository _projects;
        private readonly IMappingSuggestionRepository _stories;
        private readonly INeedsActionSuggestionRepository _needsAction;
        private readonly IApplicabilitySuggestionRepository _applicability;
        private readonly IEvidenceCheckRepository _checks;
        private readonly IStoryDraftRepository _drafts;
        private readonly IEvidenceLinkSuggestionRepository _evidence;
        private readonly IRiskDraftRepository _riskDrafts;
        private readonly IRiskMeasureSuggestionRepository _measures;
        private readonly ISecurityEventSuggestionRepository _events;
        private readonly ApprovalService _approvals;
        private readonly CurrentMember _member;
        private readonly IEvidenceRepository _allEvidence; // US-195
        private readonly Organizations.IOrganisationDocumentRepository _documents;
        private readonly Organizations.ISupplierRepository _suppliers;

        public ReviewInbox(IProjectRepository projects, IMappingSuggestionRepository stories, INeedsActionSuggestionRepository needsAction,
            IApplicabilitySuggestionRepository applicability, IEvidenceCheckRepository checks, IStoryDraftRepository drafts,
            IEvidenceLinkSuggestionRepository evidence, IRiskDraftRepository riskDrafts, IRiskMeasureSuggestionRepository measures,
            ISecurityEventSuggestionRepository events, ApprovalService approvals, CurrentMember member, IEvidenceRepository allEvidence,
            Organizations.IOrganisationDocumentRepository documents, Organizations.ISupplierRepository suppliers)
        {
            _allEvidence = allEvidence;
            _documents = documents;
            _suppliers = suppliers;
            _projects = projects;
            _stories = stories;
            _needsAction = needsAction;
            _applicability = applicability;
            _checks = checks;
            _drafts = drafts;
            _evidence = evidence;
            _riskDrafts = riskDrafts;
            _measures = measures;
            _events = events;
            _approvals = approvals;
            _member = member;
        }

        public async Task<IReadOnlyList<ReviewItem>> ForMeAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return [];
            }

            var rights = new Dictionary<Permission, bool>();
            async Task<bool> MayAsync(ReviewKind kind)
            {
                var right = ReviewKinds.RightFor(kind);
                if (!rights.TryGetValue(right, out var may))
                {
                    may = rights[right] = await _member.CanAsync(right, cancellationToken);
                }

                return may;
            }

            // US-227 R4: each kind is read for all the projects at once, so the number of queries does not grow with them
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsClosed).OrderBy(p => p.Name).ToList();
            var ids = projects.Select(p => p.Id).ToList();
            var stories = (await _stories.ListForProjectsAsync(ids, cancellationToken)).ToLookup(s => s.ProjectId);
            var needsAction = (await _needsAction.ListForProjectsAsync(ids, cancellationToken)).ToLookup(s => s.ProjectId);
            var applicability = (await _applicability.ListForProjectsAsync(ids, cancellationToken)).ToLookup(s => s.ProjectId);
            var checks = (await _checks.ListForProjectsAsync(ids, cancellationToken)).ToLookup(c => c.ProjectId);
            var drafts = (await _drafts.ListForProjectsAsync(ids, cancellationToken)).ToLookup(d => d.ProjectId);
            var evidenceSuggestions = (await _evidence.ListForProjectsAsync(ids, cancellationToken)).ToLookup(s => s.ProjectId);
            var riskDrafts = (await _riskDrafts.ListForProjectsAsync(ids, cancellationToken)).ToLookup(d => d.ProjectId);
            var measures = (await _measures.ListForProjectsAsync(ids, cancellationToken)).ToLookup(s => s.ProjectId);
            var events = (await _events.ListForProjectsAsync(ids, cancellationToken)).ToLookup(s => s.ProjectId);
            var evidence = (await _allEvidence.ListActiveForProjectsAsync(ids, cancellationToken)).ToLookup(e => e.ProjectId);
            var approvalsOf = (await _approvals.PendingAsync(null, cancellationToken)).Where(a => a.CanApprove && a.Request.ProjectId is not null)
                .ToLookup(a => a.Request.ProjectId!.Value);

            var items = new List<ReviewItem>();
            foreach (var project in projects)
            {
                async Task AddAsync(ReviewKind kind, Func<Task<int>> count)
                {
                    if (await MayAsync(kind) && await count() is > 0 and var n)
                    {
                        items.Add(new ReviewItem(project.Id, project.Name, kind, n));
                    }
                }

                await AddAsync(ReviewKind.StorySuggestions, () => Task.FromResult(stories[project.Id].Count(s => s.IsPending)));
                await AddAsync(ReviewKind.NeedsAction, () => Task.FromResult(needsAction[project.Id].Count(m => m.IsPending)));
                await AddAsync(ReviewKind.NotApplicable, () => Task.FromResult(applicability[project.Id].Count(s => s.IsPending)));
                await AddAsync(ReviewKind.EvidenceChecks, () => Task.FromResult(checks[project.Id].Count(c => !c.IsDecided)));
                await AddAsync(ReviewKind.StoryDrafts, () => Task.FromResult(drafts[project.Id].Count(d => d.IsOpen)));
                await AddAsync(ReviewKind.EvidenceSuggestions, () => Task.FromResult(evidenceSuggestions[project.Id].Count(s => s.IsPending)));
                await AddAsync(ReviewKind.RiskDrafts, () => Task.FromResult(riskDrafts[project.Id].Count(d => d.IsOpen)));
                await AddAsync(ReviewKind.MeasureSuggestions, () => Task.FromResult(measures[project.Id].Count(s => s.IsPending)));
                await AddAsync(ReviewKind.SecurityEventSuggestions, () => Task.FromResult(events[project.Id].Count(s => s.IsPending)));
                // US-195 R5: what has no date; the organisation area also counts its documents and its suppliers' evidence
                await AddAsync(ReviewKind.WithoutDate, async () =>
                {
                    var count = evidence[project.Id].Count(e => e.ValidUntil is null && e.OrganisationDocumentId is null);
                    if (project.IsOrganisationArea)
                    {
                        count += (await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken)).Count(d => d.IsActive && d.ReviewDueOn is null);
                        count += (await _suppliers.ListAsync(organization.Id, cancellationToken))
                            .Where(s => s.IsActive && s.Criticality != Domain.Organizations.SupplierCriticality.Low)
                            .Sum(s => s.Evidence.Count(e => e.ValidUntil is null));
                    }

                    return count;
                });
                var approvals = approvalsOf[project.Id].Count();
                if (approvals > 0)
                {
                    items.Add(new ReviewItem(project.Id, project.Name, ReviewKind.Approvals, approvals));
                }
            }

            return items;
        }
    }
}
