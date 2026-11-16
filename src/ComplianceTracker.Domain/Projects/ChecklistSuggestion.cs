using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Projects
{
    // US-193: the AI's suggestion that a requirement of a framework the organisation uploaded is a lifecycle activity of a
    // phase. Approved, it becomes one of the organisation's checklist items (US-192) with the framework and the code as
    // its source. Decided once.
    public class ChecklistSuggestion
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid FrameworkId { get; private set; }
        public string RequirementCode { get; private set; } = "";
        public LifecyclePhase Phase { get; private set; }
        public string Text { get; private set; } = "";
        public double Score { get; private set; }
        public string EmbeddingModel { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
        public Guid? DecidedById { get; private set; }
        public DateTime? DecidedAt { get; private set; }

        // The checklist item an approval made
        public Guid? ItemId { get; private set; }

        public bool IsPending => Status == SuggestionStatus.Pending;

        // For EF Core only
        private ChecklistSuggestion() { }

        public static ChecklistSuggestion Create(Guid organizationId, Guid frameworkId, string code, LifecyclePhase phase, string text, double score,
            string model, DateTime at)
        {
            var cleanText = (text ?? "").Trim();
            return new ChecklistSuggestion
            {
                OrganizationId = organizationId,
                FrameworkId = frameworkId,
                RequirementCode = (code ?? "").Trim(),
                Phase = phase,
                Text = cleanText.Length <= OrganisationChecklistItem.MaxTextLength ? cleanText : cleanText[..OrganisationChecklistItem.MaxTextLength],
                Score = Math.Round(Math.Clamp(double.IsNaN(score) ? 0 : score, 0, 1), 4),
                EmbeddingModel = model,
                CreatedAt = at
            };
        }

        public void Approve(Guid itemId, Guid userId, DateTime at)
        {
            EnsurePending();
            Status = SuggestionStatus.Approved;
            ItemId = itemId;
            DecidedById = userId;
            DecidedAt = at;
        }

        public void Reject(Guid userId, DateTime at)
        {
            EnsurePending();
            Status = SuggestionStatus.Rejected;
            DecidedById = userId;
            DecidedAt = at;
        }

        private void EnsurePending()
        {
            if (!IsPending)
            {
                throw new DomainException("The suggestion has already been decided");
            }
        }
    }

    // US-193 R2: what each phase is about, as the AI compares requirements with it. The first line is the phase's name.
    public static class PhaseDescriptions
    {
        public static string For(LifecyclePhase phase) => phase switch
        {
            LifecyclePhase.Planning => "Planning\nScope, intended purpose, classification, risk assessment and the requirements that apply, before anything is built.",
            LifecyclePhase.Design => "Design\nSecurity requirements, architecture, threat modelling, data protection by design and secure defaults.",
            LifecyclePhase.Development => "Development\nSecure coding, code review, managing components and dependencies, separating environments, handling vulnerabilities.",
            LifecyclePhase.Verification => "Verification\nSecurity testing, penetration testing, vulnerability scanning, SBOM, test reports before release.",
            LifecyclePhase.Released => "Released\nRelease, conformity, documentation for users, support period, security updates, incident and vulnerability reporting, monitoring.",
            LifecyclePhase.EndOfSupport => "EndOfSupport\nEnd of support, telling users, keeping documentation, deleting or handing over data.",
            _ => phase.ToString()
        };
    }
}
