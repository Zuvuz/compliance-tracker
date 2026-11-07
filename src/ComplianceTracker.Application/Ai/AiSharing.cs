using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Application.Ai
{
    // US-130 R5: which decisions may be used to improve the AI (measuring, US-116; training, US-123). Only while the
    // organisation shares, only from when it last switched sharing on, and never from a project that is left out.
    public static class AiSharing
    {
        public static bool MayUse(Organization organization, IReadOnlySet<Guid> excludedProjects, MappingSuggestion decision)
            => decision.DecidedAt is { } decidedAt && MayUse(organization, excludedProjects, decision.ProjectId, decidedAt);

        // US-123: the same rule for any kind of decision (stories, evidence, "not applicable")
        public static bool MayUse(Organization organization, IReadOnlySet<Guid> excludedProjects, Guid projectId, DateTime decidedAt)
            => organization.ShareAiFeedback
               && !excludedProjects.Contains(projectId)
               && (organization.ShareAiFeedbackSince is not { } since || decidedAt >= since);
    }
}
