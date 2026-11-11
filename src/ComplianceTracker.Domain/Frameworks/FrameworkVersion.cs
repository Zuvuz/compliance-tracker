using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Frameworks
{
    // One version of a framework. Its file never changes; a correction is a new version (US-005 R3).
    // For an uploaded framework it is a PDF (stored separately); for a built-in framework
    // it is the published regulation, found through SourceReference (US-006).
    // US-007: a new version starts as a draft whose requirements can be corrected. Activating it locks them.
    public class FrameworkVersion
    {
        // Private list, so requirements only change through the methods below (and only in a draft)
        private readonly List<Requirement> _requirements = new();

        public Guid Id { get; } = Guid.NewGuid();
        public Guid FrameworkId { get; private set; }

        // 1, 2, 3 ... counted by Framework.AddVersion
        public int Number { get; private set; }

        // The cleaned file name from CatalogFile. Empty for built-in frameworks.
        public string FileName { get; private set; } = "";

        // 0 for built-in frameworks
        public long SizeInBytes { get; private set; }

        // Fingerprint of the PDF, so an auditor can check it has not been changed. Empty for built-in frameworks.
        public string Sha256 { get; private set; } = "";

        // The member who uploaded it. Null for built-in frameworks, which come with the tracker.
        public Guid? UploadedById { get; private set; }

        // When it was uploaded, or for built-in frameworks when the regulation was published. Always UTC.
        public DateTime UploadedAt { get; private set; }

        // Where the official text is published, e.g. a EUR-Lex address. Only for built-in frameworks (US-006 R6).
        public string? SourceReference { get; private set; }

        // US-007: Draft until activated. Versions from before US-007 and built-in versions are Active.
        public FrameworkVersionStatus Status { get; private set; } = FrameworkVersionStatus.Draft;

        public bool IsActive => Status == FrameworkVersionStatus.Active;

        // When and by whom it was activated (R3, R6). Null for drafts and built-in versions' uploader.
        public DateTime? ActivatedAt { get; private set; }
        public Guid? ActivatedById { get; private set; }

        // US-010: the role of the person when they activated it
        public UserRole? ActivatedRole { get; private set; }

        // US-035: the day the version starts to apply. Null: it applies as soon as it is activated.
        public DateOnly? AppliesFrom { get; private set; }

        // US-035 R3: active, but its start date has not come yet on this day
        public bool IsUpcomingOn(DateOnly today) => IsActive && AppliesFrom > today;

        // US-035: active and its start date (if any) has come
        public bool AppliesOn(DateOnly today) => IsActive && (AppliesFrom is null || AppliesFrom <= today);

        // When the version started (or starts) to apply: the later of activation and its start date.
        // Used to find the version that applies last (Framework.CurrentVersion).
        internal DateTime StartsApplyingAt
        {
            get
            {
                var activated = ActivatedAt ?? UploadedAt;
                var from = AppliesFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return from > activated ? from.Value : activated;
            }
        }

        // US-070: what the extraction from the PDF did not use; null for versions from before US-070 and built-in ones
        public ExtractionCheck? Extraction { get; private set; }

        // US-137: how complete the draft is (an EU act from EUR-Lex, or a standard with a known profile); null otherwise
        public CompletenessCheck? Completeness { get; private set; }

        // US-137 R5 (B80): a version imported from EUR-Lex needs a second person's approval to be activated. An uploaded
        // standard checked against its profile shows the check, but one person activates it, since an organisation may
        // follow only part of a standard.
        public bool NeedsApprovalToActivate => Completeness is { IsOfEuAct: true };

        // US-137 R5: what an approval was asked for; a change to the requirements after that makes the request stale
        public string RequirementsStamp
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                string.Join("|", _requirements.OrderBy(r => r.Position).Select(r => r.Code + "/" + r.Kind + "/" + r.Title)))))[..16];

        // In the order of the document. Only loaded when asked for (IFrameworkRepository).
        public IReadOnlyList<Requirement> Requirements => _requirements.OrderBy(r => r.Position).ToList();

        // For EF Core only
        private FrameworkVersion() { }

        // Internal: only Framework can create versions, so the numbering is always right
        internal FrameworkVersion(Guid frameworkId, int number, CatalogFile file, Guid uploadedById, DateTime uploadedAt)
        {
            FrameworkId = frameworkId;
            Number = number;
            FileName = file.FileName;
            SizeInBytes = file.Content.Length;
            Sha256 = file.Sha256;
            UploadedById = uploadedById;
            UploadedAt = uploadedAt;
        }

        // ---------------------------------------------------------- Requirements (US-007)

        // R1, R2, R5: found in the PDF or added by hand. The code is unique within the version.
        // US-143 R4, R5: its kind, and the code of the requirement it sits under (which must already be in the version;
        // an unknown parent code puts it at the top)
        public Requirement AddRequirement(
            string code, string title, string? description, RequirementKind kind = RequirementKind.Requirement, string? parentCode = null)
        {
            EnsureDraft();
            EnsureUniqueCode(code, except: null);

            var requirement = new Requirement(Id, _requirements.Count == 0 ? 1 : _requirements.Max(r => r.Position) + 1,
                code, title, description);
            requirement.SetKind(kind);
            if (!string.IsNullOrWhiteSpace(parentCode)
                && _requirements.FirstOrDefault(r => string.Equals(r.Code, parentCode.Trim(), StringComparison.OrdinalIgnoreCase)) is { } parent)
            {
                requirement.SetParent(parent.Id);
            }

            _requirements.Add(requirement);
            return requirement;
        }

        // US-143 R4: puts a requirement under another by code, e.g. from a table where a parent comes after its child. An
        // unknown parent, the requirement itself or one of its own descendants leaves it where it is.
        public void PlaceUnder(string code, string? parentCode)
        {
            EnsureDraft();
            var child = _requirements.FirstOrDefault(r => string.Equals(r.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
            var parent = _requirements.FirstOrDefault(r => string.Equals(r.Code, parentCode?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (child is null || parent is null || parent == child)
            {
                return;
            }

            for (var up = parent; up is not null; up = _requirements.FirstOrDefault(r => r.Id == up.ParentId))
            {
                if (up.Id == child.Id)
                {
                    return;
                }
            }

            child.SetParent(parent.Id);
        }

        // US-143 R7: a new choice of columns reads the table again, so the draft starts over
        public void ClearRequirements()
        {
            EnsureDraft();
            _requirements.Clear();
        }

        // US-143 R5: the admin can change the kind while the version is a draft
        public void ChangeKind(Guid requirementId, RequirementKind kind)
        {
            EnsureDraft();
            Find(requirementId).SetKind(kind);
        }

        // US-143 R4: the requirements directly under one (or at the top for null), in the document's order
        public IReadOnlyList<Requirement> ChildrenOf(Guid? parentId) => Requirements.Where(r => r.ParentId == parentId).ToList();

        // US-070 R1: kept with the draft, so it can be checked before activation
        public void RecordExtraction(ExtractionCheck check)
        {
            EnsureDraft();
            Extraction = check;
        }

        // US-137: kept with the draft, so it can be checked before activation
        public void RecordCompleteness(CompletenessCheck check)
        {
            EnsureDraft();
            Completeness = check;
        }

        // US-137 R7: where an imported version came from, e.g. its EUR-Lex address
        public void RecordSource(string reference)
        {
            EnsureDraft();
            SourceReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        }

        // R2: corrects a requirement in the draft
        public void ChangeRequirement(Guid requirementId, string code, string title, string? description)
        {
            EnsureDraft();
            var requirement = Find(requirementId);
            EnsureUniqueCode(code, except: requirement);
            requirement.Set(code, title, description);
        }

        // R2: removes a wrong row from the draft
        public void RemoveRequirement(Guid requirementId)
        {
            EnsureDraft();
            var requirement = Find(requirementId);

            // US-143 R4: what sat under it moves up to its parent
            foreach (var child in _requirements.Where(r => r.ParentId == requirement.Id))
            {
                child.SetParent(requirement.ParentId);
            }

            _requirements.Remove(requirement);
        }

        // US-035 R1, R2: sets or clears the start date. Only while the version is a draft.
        public void SetAppliesFrom(DateOnly? appliesFrom)
        {
            EnsureDraft();
            AppliesFrom = appliesFrom;
        }

        // R3: locks the version and its requirements. It needs at least one requirement,
        // so a project never follows a version with nothing to assess.
        public void Activate(Guid userId, DateTime at, UserRole? role = null)
        {
            EnsureDraft();

            if (_requirements.Count == 0)
            {
                throw new DomainException("Add at least one requirement before activating");
            }

            Status = FrameworkVersionStatus.Active;
            ActivatedAt = at;
            ActivatedById = userId;
            ActivatedRole = role;
        }

        private void EnsureDraft()
        {
            if (IsActive)
            {
                throw new DomainException("An active version cannot be changed");
            }
        }

        private void EnsureUniqueCode(string code, Requirement? except)
        {
            var trimmed = code?.Trim() ?? "";
            if (_requirements.Any(r => r != except && string.Equals(r.Code, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException($"Requirement {trimmed} is already in the list");
            }
        }

        private Requirement Find(Guid requirementId)
            => _requirements.FirstOrDefault(r => r.Id == requirementId)
               ?? throw new DomainException("Requirement not found");
    }
}
