using System.Security.Cryptography;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Reports
{
    // US-050: a report locked as it was made: its file, who locked it with their role, when, and the SHA-256 fingerprint of
    // the file (R1). Never changed or deleted (R2); the database refuses it too. A new version is a new snapshot.
    public class ReportSnapshot
    {
        public const int MaxTitleLength = 300;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid? ProjectId { get; private set; }

        // Which report, e.g. "traceability:auditor:cra" or "technical-documentation"
        public string Kind { get; private set; } = "";
        public string Title { get; private set; } = "";
        public string FileName { get; private set; } = "";
        public string ContentType { get; private set; } = "";
        public long SizeInBytes { get; private set; }

        // Hex SHA-256 of the file
        public string Sha256 { get; private set; } = "";

        public Guid LockedById { get; private set; }
        public UserRole? LockedRole { get; private set; }
        public DateTime LockedAt { get; private set; }

        // For EF Core only
        private ReportSnapshot() { }

        public static ReportSnapshot Lock(
            Guid organizationId, Guid? projectId, string kind, string title, string fileName, string contentType, byte[] content,
            Guid lockedById, UserRole? role, DateTime at)
        {
            if (content.Length == 0)
            {
                throw new DomainException("The report is empty");
            }

            var cleaned = (title ?? "").Trim();
            if (cleaned.Length == 0 || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(fileName))
            {
                throw new DomainException("The report needs a kind, a title and a file name");
            }

            return new ReportSnapshot
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Kind = kind.Trim(),
                Title = cleaned.Length <= MaxTitleLength ? cleaned : cleaned[..MaxTitleLength],
                FileName = fileName.Trim(),
                ContentType = contentType,
                SizeInBytes = content.Length,
                Sha256 = Fingerprint(content),
                LockedById = lockedById,
                LockedRole = role,
                LockedAt = at
            };
        }

        public static string Fingerprint(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        // R4: the file is exactly the locked one
        public bool Matches(byte[] content) => Fingerprint(content) == Sha256;
    }
}
