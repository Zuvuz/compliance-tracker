using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Scanning
{
    // US-122: a token a build pipeline uses to upload scans to one project (R1). Only its SHA-256 hash is kept (R2); it
    // can be revoked at once (R3).
    public class ScanUploadToken
    {
        public const int MaxNameLength = 100;
        public const int MaxUploadsPerHour = 30; // R6
        public const string Prefix = "ctu_";

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string Name { get; private set; } = "";

        // Hex SHA-256 of the token
        public string Hash { get; private set; } = "";
        public Guid CreatedById { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastUsedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public Guid? RevokedById { get; private set; }

        public bool IsActive => RevokedAt is null;

        // For EF Core only
        private ScanUploadToken() { }

        // R2: the token itself is returned once and never stored
        public static (ScanUploadToken Token, string Secret) Create(Guid projectId, string? name, Guid userId, DateTime now)
        {
            var cleaned = (name ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Give the token a name, e.g. the pipeline that uses it");
            }

            if (cleaned.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            var secret = Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return (new ScanUploadToken
            {
                ProjectId = projectId,
                Name = cleaned,
                Hash = HashOf(secret),
                CreatedById = userId,
                CreatedAt = now
            }, secret);
        }

        public static string HashOf(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

        // R1, R3: only an active token, and only for its own project
        public bool Allows(Guid projectId) => IsActive && ProjectId == projectId;

        // R6: the uploads of the last hour, counted by the caller
        public static bool LimitReached(int uploadsInTheLastHour) => uploadsInTheLastHour >= MaxUploadsPerHour;

        public void Used(DateTime now) => LastUsedAt = now;

        public void Revoke(Guid userId, DateTime now)
        {
            if (!IsActive)
            {
                throw new DomainException("The token is revoked already");
            }

            RevokedAt = now;
            RevokedById = userId;
        }
    }
}
