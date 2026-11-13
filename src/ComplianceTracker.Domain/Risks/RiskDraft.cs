using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Risks
{
    public enum RiskDraftStatus
    {
        Draft,
        Accepted,
        Discarded
    }

    // US-112: a risk the AI agent proposes for a project, with what it is based on and why (R2). It never becomes a risk
    // by itself: a person with ManageRisks accepts it, after editing it if needed (R1, R4), or discards it with a reason (R5).
    public class RiskDraft
    {
        public const int MaxTitleLength = 200;
        public const int MaxTextLength = 2000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string Title { get; private set; } = "";
        public string Threat { get; private set; } = "";
        public string Asset { get; private set; } = "";
        public int Likelihood { get; private set; }
        public int Impact { get; private set; }

        // R2: e.g. "CRA class Important, class I; I.2(d) not covered" and the agent's reasoning
        public string BasedOn { get; private set; } = "";
        public string Reasoning { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public RiskDraftStatus Status { get; private set; } = RiskDraftStatus.Draft;
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public string? DiscardReason { get; private set; }
        public Guid? RiskId { get; private set; }

        public bool IsOpen => Status == RiskDraftStatus.Draft;

        public RiskLevel Level => Risk.LevelOf(Likelihood * Impact);

        // For EF Core only
        private RiskDraft() { }

        public static RiskDraft Create(Guid projectId, string? title, string? threat, string? asset, int likelihood, int impact,
            string? basedOn, string? reasoning, string model, string promptVersion, DateTime at)
        {
            var cleaned = title?.Trim() ?? "";
            if (cleaned.Length == 0)
            {
                throw new DomainException("A draft needs a title");
            }

            return new RiskDraft
            {
                ProjectId = projectId,
                Title = Cut(cleaned, MaxTitleLength),
                Threat = Cut(threat, MaxTextLength),
                Asset = Cut(asset, MaxTitleLength),
                // The agent's scale may be off; the person sees and can change it before accepting (R4)
                Likelihood = Math.Clamp(likelihood, Risk.MinScale, Risk.MaxScale),
                Impact = Math.Clamp(impact, Risk.MinScale, Risk.MaxScale),
                BasedOn = Cut(basedOn, MaxTextLength),
                Reasoning = Cut(reasoning, MaxTextLength),
                Model = model,
                PromptVersion = promptVersion,
                CreatedAt = at
            };
        }

        // R1, R4: the risk the person made from it
        public void Accept(Guid riskId, Guid userId, UserRole? role, DateTime at)
        {
            EnsureOpen();
            Status = RiskDraftStatus.Accepted;
            RiskId = riskId;
            Decided(userId, role, at);
        }

        // R5: with a reason, and not proposed again
        public void Discard(string? reason, Guid userId, UserRole? role, DateTime at)
        {
            EnsureOpen();
            var cleaned = reason?.Trim();
            if (string.IsNullOrEmpty(cleaned))
            {
                throw new DomainException("Give a reason for discarding the draft");
            }

            Status = RiskDraftStatus.Discarded;
            DiscardReason = Cut(cleaned, 1000);
            Decided(userId, role, at);
        }

        private void Decided(Guid userId, UserRole? role, DateTime at)
        {
            DecidedById = userId;
            DecidedRole = role;
            DecidedAt = at;
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new DomainException("The draft has already been decided");
            }
        }

        private static string Cut(string? text, int max)
        {
            var cleaned = text?.Trim() ?? "";
            return cleaned.Length <= max ? cleaned : cleaned[..max];
        }
    }
}
