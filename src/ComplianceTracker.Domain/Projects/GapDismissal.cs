using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Projects
{
    // US-119 R5: a gap a person dismissed, with the reason. The gap is found by its key (e.g. "requirement:{id}:not-covered")
    // and stays away only while its source is as it was (the stamp); when the source changes, the gap can come back.
    public class GapDismissal
    {
        public const int MaxKeyLength = 200;
        public const int MaxStampLength = 200;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string Key { get; private set; } = "";
        public string Stamp { get; private set; } = "";
        public string Reason { get; private set; } = "";
        public Guid DismissedById { get; private set; }
        public UserRole? DismissedRole { get; private set; }
        public DateTime DismissedAt { get; private set; }

        // For EF Core only
        private GapDismissal() { }

        public static GapDismissal Create(Guid projectId, string key, string stamp, string? reason, Guid userId, DateTime at, UserRole? role = null)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
            {
                throw new DomainException("The gap was not found");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason for dismissing the gap");
            }

            return new GapDismissal
            {
                ProjectId = projectId,
                Key = key.Trim(),
                Stamp = (stamp ?? "").Length <= MaxStampLength ? stamp ?? "" : stamp![..MaxStampLength],
                Reason = reason.Trim().Length <= 1000 ? reason.Trim() : reason.Trim()[..1000],
                DismissedById = userId,
                DismissedRole = role,
                DismissedAt = at
            };
        }

        // R5: still in force while the source has the same stamp
        public bool Hides(string key, string stamp) => Key == key && Stamp == stamp;
    }
}
