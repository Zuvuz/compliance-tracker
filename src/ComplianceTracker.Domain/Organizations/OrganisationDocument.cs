using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.Domain.Organizations
{
    // US-136: what kind of document the organisation keeps
    public enum OrganisationDocumentKind
    {
        Policy,
        Procedure,
        RiskTable,
        Other
    }

    // US-136: a policy, procedure or risk table kept once for the whole organisation. Linked to requirements by framework
    // and code, so the links survive a new version of the framework. Every project that follows such a requirement gets
    // the document as evidence (R1). A new version of the file replaces the old one in the links; the old one is kept (R3).
    public class OrganisationDocument
    {
        public const int MaxTitleLength = Evidence.MaxTitleLength;
        public const int MaxVersionLength = 50;

        private readonly List<OrganisationDocumentLink> _links = [];
        private readonly List<OrganisationDocumentVersion> _versions = [];

        public Guid Id { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public OrganisationDocumentKind Kind { get; private set; }
        public string Title { get; private set; } = "";
        public Guid OwnerId { get; private set; }

        // The current file and its version label, e.g. "v3"
        public string Version { get; private set; } = "";
        public string FileName { get; private set; } = "";
        public string ContentType { get; private set; } = "";
        public long SizeInBytes { get; private set; }
        public string Sha256 { get; private set; } = "";

        // R4: when it must be reviewed next
        public DateOnly? ReviewDueOn { get; private set; }
        public DateTime? LastReviewedAt { get; private set; }
        public Guid? LastReviewedById { get; private set; }

        public Guid AddedById { get; private set; }
        public DateTime AddedAt { get; private set; }
        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        public IReadOnlyList<OrganisationDocumentLink> Links => _links;

        public IReadOnlyList<OrganisationDocumentLink> ActiveLinks => _links.Where(l => l.IsActive).ToList();

        // R3: the earlier files, newest first
        public IReadOnlyList<OrganisationDocumentVersion> Versions => _versions.OrderByDescending(v => v.ReplacedAt).ToList();

        // For EF Core only
        private OrganisationDocument() { }

        public static OrganisationDocument Create(
            Guid organizationId, OrganisationDocumentKind kind, string? title, Guid ownerId, string? version, EvidenceFile file,
            DateOnly? reviewDueOn, Guid addedById, DateTime at)
        {
            var document = new OrganisationDocument
            {
                OrganizationId = organizationId,
                Kind = kind,
                Title = CleanTitle(title),
                OwnerId = ownerId,
                ReviewDueOn = reviewDueOn,
                AddedById = addedById,
                AddedAt = at
            };
            document.SetFile(file, version);
            return document;
        }

        // R4: the review date has passed
        public bool IsReviewDue(DateOnly today) => IsActive && ReviewDueOn is { } due && due < today;

        // R3: the old file is kept; the links stay and now point at the new file
        public void UploadNewVersion(EvidenceFile file, string? version, Guid userId, DateTime at)
        {
            EnsureActive();
            var label = CleanVersion(version);
            if (label == Version)
            {
                throw new DomainException($"The document already has version {Version}; give the new one another name");
            }

            _versions.Add(new OrganisationDocumentVersion(Id, Version, FileName, ContentType, SizeInBytes, Sha256, userId, at));
            SetFile(file, label);
        }

        // R4: reviewed now, next review on the given day
        public void MarkReviewed(DateOnly nextReviewOn, Guid userId, DateTime at)
        {
            EnsureActive();
            if (nextReviewOn <= DateOnly.FromDateTime(at))
            {
                throw new DomainException("The next review must be after today");
            }

            ReviewDueOn = nextReviewOn;
            LastReviewedAt = at;
            LastReviewedById = userId;
        }

        public OrganisationDocumentLink Link(Guid frameworkId, string? requirementCode, Guid userId, DateTime at)
        {
            EnsureActive();
            var code = (requirementCode ?? "").Trim();
            if (code.Length == 0)
            {
                throw new DomainException("Choose a requirement");
            }

            if (ActiveLinks.Any(l => l.FrameworkId == frameworkId && string.Equals(l.RequirementCode, code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException($"The document is linked to {code} already");
            }

            var link = new OrganisationDocumentLink(Id, frameworkId, code, userId, at);
            _links.Add(link);
            return link;
        }

        public OrganisationDocumentLink Unlink(Guid linkId, Guid userId, DateTime at)
        {
            var link = _links.FirstOrDefault(l => l.Id == linkId && l.IsActive) ?? throw new DomainException("Link not found");
            link.Remove(userId, at);
            return link;
        }

        // R5: the caller has shown which requirements lose the evidence
        public void Remove(Guid userId, DateTime at)
        {
            EnsureActive();
            RemovedById = userId;
            RemovedAt = at;
            foreach (var link in ActiveLinks)
            {
                link.Remove(userId, at);
            }
        }

        private void SetFile(EvidenceFile file, string? version)
        {
            Version = CleanVersion(version);
            FileName = file.FileName;
            ContentType = file.ContentType;
            SizeInBytes = file.Content.Length;
            Sha256 = file.Sha256;
        }

        private void EnsureActive()
        {
            if (!IsActive)
            {
                throw new DomainException("The document is removed");
            }
        }

        private static string CleanTitle(string? title)
        {
            var cleaned = (title ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Give the document a title");
            }

            return cleaned.Length <= MaxTitleLength ? cleaned : throw new DomainException($"The title must be {MaxTitleLength} characters or fewer");
        }

        private static string CleanVersion(string? version)
        {
            var cleaned = (version ?? "").Trim();
            if (cleaned.Length == 0)
            {
                throw new DomainException("Give the version, e.g. v1");
            }

            return cleaned.Length <= MaxVersionLength ? cleaned : throw new DomainException($"The version must be {MaxVersionLength} characters or fewer");
        }
    }

    // US-136: the document covers this requirement of this framework, in every version that has the code
    public class OrganisationDocumentLink
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid DocumentId { get; private set; }
        public Guid FrameworkId { get; private set; }
        public string RequirementCode { get; private set; } = "";
        public Guid LinkedById { get; private set; }
        public DateTime LinkedAt { get; private set; }
        public Guid? RemovedById { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        private OrganisationDocumentLink() { }

        internal OrganisationDocumentLink(Guid documentId, Guid frameworkId, string requirementCode, Guid userId, DateTime at)
        {
            DocumentId = documentId;
            FrameworkId = frameworkId;
            RequirementCode = requirementCode;
            LinkedById = userId;
            LinkedAt = at;
        }

        internal void Remove(Guid userId, DateTime at)
        {
            RemovedById = userId;
            RemovedAt = at;
        }
    }

    // US-136 R3: an earlier file of a document, kept when a new version replaced it
    public class OrganisationDocumentVersion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid DocumentId { get; private set; }
        public string Version { get; private set; } = "";
        public string FileName { get; private set; } = "";
        public string ContentType { get; private set; } = "";
        public long SizeInBytes { get; private set; }
        public string Sha256 { get; private set; } = "";
        public Guid ReplacedById { get; private set; }
        public DateTime ReplacedAt { get; private set; }

        private OrganisationDocumentVersion() { }

        internal OrganisationDocumentVersion(
            Guid documentId, string version, string fileName, string contentType, long sizeInBytes, string sha256, Guid replacedById, DateTime replacedAt)
        {
            DocumentId = documentId;
            Version = version;
            FileName = fileName;
            ContentType = contentType;
            SizeInBytes = sizeInBytes;
            Sha256 = sha256;
            ReplacedById = replacedById;
            ReplacedAt = replacedAt;
        }
    }
}
