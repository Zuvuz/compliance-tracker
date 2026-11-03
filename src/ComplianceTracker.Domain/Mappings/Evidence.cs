using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    // Proof that a requirement of a project is met (US-057): a link (PR, CI run, document) or an uploaded file.
    // In the MVP it belongs to the requirement in a project, not to a control (B33).
    // Its own aggregate, like RequirementLink. Removing sets RemovedAt; evidence is never deleted, so the history is kept.
    // A file's content is stored apart from this (Infrastructure), so listing evidence never loads the files.
    public class Evidence
    {
        public const int MaxTitleLength = 200;
        public const int MaxUrlLength = 2000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid RequirementId { get; private set; }

        public string Title { get; private set; } = "";

        // Exactly one of Url and FileName is set (R1)
        public string? Url { get; private set; }
        public string? FileName { get; private set; }
        public string? ContentType { get; private set; }
        public long? SizeInBytes { get; private set; }
        public string? Sha256 { get; private set; }

        // R3: after this day it no longer counts
        public DateOnly? ValidUntil { get; private set; }

        public Guid AddedById { get; private set; }

        // US-010: the role of the person when they added it
        public UserRole? AddedRole { get; private set; }
        public DateTime AddedAt { get; private set; }

        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        public bool IsFile => FileName is not null;

        // For EF Core only
        private Evidence() { }

        // R1: a link must be an absolute http or https address
        public static Evidence ForLink(
            Guid projectId, Guid requirementId, string title, string url, DateOnly? validUntil, Guid addedById, UserRole? role, DateTime at)
        {
            var address = (url ?? "").Trim();
            if (address.Length == 0)
            {
                throw new DomainException("Give a link or a file");
            }

            if (address.Length > MaxUrlLength
                || !Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new DomainException("The link must start with http:// or https://");
            }

            var evidence = New(projectId, requirementId, title, validUntil, addedById, role, at);
            evidence.Url = uri.ToString();
            return evidence;
        }

        // R2: the file has already been checked by EvidenceFile
        public static Evidence ForFile(
            Guid projectId, Guid requirementId, string title, EvidenceFile file, DateOnly? validUntil, Guid addedById, UserRole? role, DateTime at)
        {
            var evidence = New(projectId, requirementId, title, validUntil, addedById, role, at);
            evidence.FileName = file.FileName;
            evidence.ContentType = file.ContentType;
            evidence.SizeInBytes = file.Content.Length;
            evidence.Sha256 = file.Sha256;
            return evidence;
        }

        // R3: valid through the "valid until" day itself
        public bool IsExpiredOn(DateOnly today) => ValidUntil is { } until && until < today;

        // R5
        public void Remove(Guid userId, DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The evidence is already removed");
            }

            RemovedById = userId;
            RemovedAt = at;
        }

        private static Evidence New(
            Guid projectId, Guid requirementId, string title, DateOnly? validUntil, Guid addedById, UserRole? role, DateTime at)
        {
            var cleaned = (title ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Give the evidence a title");
            }

            if (cleaned.Length > MaxTitleLength)
            {
                throw new DomainException($"The title must be {MaxTitleLength} characters or fewer");
            }

            return new Evidence
            {
                ProjectId = projectId,
                RequirementId = requirementId,
                Title = cleaned,
                ValidUntil = validUntil,
                AddedById = addedById,
                AddedRole = role,
                AddedAt = at
            };
        }
    }
}
