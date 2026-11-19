using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-115 R2: where a requirement the organisation follows is met. Organisation-wide: once, in the organisation area, and
    // the products inherit it. Carried out in the projects: each product proves it, and the organisation gathers the proof.
    public enum RequirementScope
    {
        OrganisationWide,
        CarriedOutInProjects
    }

    // US-115 R2, R7: a person who decides compliance changes where a requirement is met, with a reason. Rows are only added:
    // the newest decision of an organisation for a requirement counts, so the history is kept.
    public class RequirementScopeDecision
    {
        public const int MaxReasonLength = 500;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid RequirementId { get; private set; }
        public RequirementScope Scope { get; private set; }
        public string Reason { get; private set; } = "";
        public Guid DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime DecidedAt { get; private set; }

        // For EF Core only
        private RequirementScopeDecision() { }

        public static RequirementScopeDecision Decide(
            Guid organizationId, Guid requirementId, RequirementScope scope, string reason, Guid userId, UserRole? role, DateTime at)
        {
            if (!Enum.IsDefined(scope))
            {
                throw new DomainException("Choose where the requirement is met");
            }

            var cleaned = (reason ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Give a reason why the requirement is met there");
            }

            if (cleaned.Length > MaxReasonLength)
            {
                throw new DomainException($"The reason must be {MaxReasonLength} characters or fewer");
            }

            return new RequirementScopeDecision
            {
                OrganizationId = organizationId,
                RequirementId = requirementId,
                Scope = scope,
                Reason = cleaned,
                DecidedById = userId,
                DecidedRole = role,
                DecidedAt = at
            };
        }

        public static string NameOf(RequirementScope scope)
            => scope == RequirementScope.CarriedOutInProjects ? "Carried out in the projects" : "Organisation-wide";
    }

    // US-115 R2, "up": a requirement carried out in the projects is proven in some products and not yet in others.
    // Complete when every product that follows it has proof; a product that opted out (R4) is not counted.
    public sealed record CarriedOutStatus(IReadOnlyList<string> ProvenIn, IReadOnlyList<string> NoProofYet)
    {
        public bool IsComplete => ProvenIn.Count > 0 && NoProofYet.Count == 0;

        // e.g. "Proven in Acme Portal, Compliance Tracker · No proof yet: Acme App"
        public string Text
            => (ProvenIn.Count == 0 ? "Proven in no project yet" : $"Proven in {string.Join(", ", ProvenIn)}")
               + (NoProofYet.Count == 0 ? "" : $" · No proof yet: {string.Join(", ", NoProofYet)}");
    }
}
