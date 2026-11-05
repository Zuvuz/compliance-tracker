using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Projects
{
    // US-026: how a regulation the project follows applies to it, e.g. "CRA: Important, class I" or "AI Act: High-risk",
    // with the reason. Never changed: a new classification replaces the old one, so the history is kept (R4).
    // The class is a code from the regulation's list (RegulationClasses in Application); "NotApplicable" for every one.
    public class ProjectClassification
    {
        public const int MaxReasonLength = 2000;
        public const string NotApplicable = "NotApplicable";

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid FrameworkId { get; private set; }

        // e.g. "ImportantClassI"
        public string Class { get; private set; } = "";

        // R3, e.g. "Annex III, class I, point 2: a password manager"
        public string Reason { get; private set; } = "";

        public Guid ClassifiedById { get; private set; }
        public UserRole? ClassifiedRole { get; private set; }
        public DateTime ClassifiedAt { get; private set; }

        // For EF Core only
        private ProjectClassification() { }

        // The caller checks that the class belongs to the regulation (R2)
        public static ProjectClassification Create(
            Guid projectId, Guid frameworkId, string classCode, string? reason, Guid userId, UserRole? role, DateTime at)
        {
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("A reason is required");
            }

            if (text.Length > MaxReasonLength)
            {
                throw new DomainException($"The reason must be {MaxReasonLength} characters or fewer");
            }

            if (string.IsNullOrWhiteSpace(classCode))
            {
                throw new DomainException("Choose a class");
            }

            return new ProjectClassification
            {
                ProjectId = projectId,
                FrameworkId = frameworkId,
                Class = classCode.Trim(),
                Reason = text,
                ClassifiedById = userId,
                ClassifiedRole = role,
                ClassifiedAt = at
            };
        }
    }
}
