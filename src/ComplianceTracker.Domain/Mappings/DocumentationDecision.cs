using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // US-204 R5, R8: whether documentation alone is enough for a requirement in an organisation, e.g. NIS2 Art. 21(2)(a)
    // asks for the policy itself. A person who decides compliance marks it, or removes the built-in mark, with a reason.
    // Rows are only added: the newest decision of an organisation for a requirement counts, so the history is kept.
    public class DocumentationDecision
    {
        public const int MaxReasonLength = 500;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid RequirementId { get; private set; }

        // true: documentation is enough; false: proof is needed (also when the built-in mark is removed)
        public bool Enough { get; private set; }
        public string Reason { get; private set; } = "";

        public Guid DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime DecidedAt { get; private set; }

        // For EF Core only
        private DocumentationDecision() { }

        public static DocumentationDecision Decide(
            Guid organizationId, Guid requirementId, bool enough, string reason, Guid userId, UserRole? role, DateTime at)
        {
            var cleaned = (reason ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException(enough
                    ? "Give a reason why documentation is enough"
                    : "Give a reason why proof is needed");
            }

            if (cleaned.Length > MaxReasonLength)
            {
                throw new DomainException($"The reason must be {MaxReasonLength} characters or fewer");
            }

            return new DocumentationDecision
            {
                OrganizationId = organizationId,
                RequirementId = requirementId,
                Enough = enough,
                Reason = cleaned,
                DecidedById = userId,
                DecidedRole = role,
                DecidedAt = at
            };
        }
    }

    // R8: the built-in requirements that ask for a policy, a plan or a procedure itself, so documentation is enough
    public static class DocumentationRules
    {
        public const string BuiltInReason = "The requirement asks for the document itself";

        // NIS2 Art. 21(2)(a) policies on risk analysis and information security; (f) policies and procedures to assess the measures
        private static readonly HashSet<string> Nis2 = ["Art. 21(2)(a)", "Art. 21(2)(f)"];

        // CRA Annex I part II (5) a policy on coordinated vulnerability disclosure; Art. 31 the technical documentation
        private static readonly HashSet<string> Cra = ["II.5", "Art.31"];

        // AI Act Art. 11 the technical documentation; Art. 17 a quality management system of written policies and procedures
        private static readonly HashSet<string> AiAct = ["Art. 11", "Art. 17"];

        public static bool BuiltInEnough(Framework framework, Requirement requirement)
            => BuiltInEnough(framework.IsBuiltIn, framework.Name, requirement.Code, requirement.Implements);

        // An uploaded framework is never marked: its organisation marks what it wants (R5)
        public static bool BuiltInEnough(bool builtIn, string frameworkName, string code, string? implements)
        {
            if (!builtIn)
            {
                return false;
            }

            if (frameworkName.StartsWith("CRA", StringComparison.Ordinal))
            {
                return Cra.Contains(code);
            }

            if (frameworkName.StartsWith("AI Act", StringComparison.Ordinal))
            {
                return AiAct.Contains(code);
            }

            // NIS2 itself, and the layers that implement it (the Danish law, 2024/2690): by the NIS2 point they implement
            return frameworkName.StartsWith("NIS", StringComparison.Ordinal)
                && (Nis2.Contains(code) || implements is { } point && Nis2.Contains(point));
        }

        // R5, R8: the newest decision wins over the built-in mark; null decision → the built-in mark
        public static (bool Enough, string? Reason, bool BuiltIn) Resolve(Framework framework, Requirement requirement, DocumentationDecision? decision)
            => decision is not null
                ? (decision.Enough, decision.Reason, false)
                : BuiltInEnough(framework, requirement) ? (true, BuiltInReason, true) : (false, null, false);
    }
}
