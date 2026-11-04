using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.WorkItems
{
    // US-032: a requirement specification uploaded to a project, e.g. "kravspec-web.xlsx" (R10: a project can have several).
    // Each upload is a new version (R5); every version's file is kept (R7). The requirements in it are WorkItems
    // with Source = Document. Imported, never synced: a new version only comes when someone uploads it (B35).
    public class RequirementDocument
    {
        private readonly List<RequirementDocumentVersion> _versions = new();

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }

        // The file name of the first version. Names the specification on the page.
        public string Name { get; private set; } = "";

        public DateTime CreatedAt { get; private set; }

        // US-033: the ID pattern the newest version of a Word document was read with; null for a spreadsheet
        public string? IdPattern { get; private set; }

        public IReadOnlyList<RequirementDocumentVersion> Versions => _versions;

        public RequirementDocumentVersion? Latest => _versions.MaxBy(v => v.Number);

        // The number the next upload gets
        public int NextVersionNumber => _versions.Count == 0 ? 1 : _versions.Max(v => v.Number) + 1;

        // For EF Core only
        private RequirementDocument() { }

        public static RequirementDocument Create(Guid projectId, string name, DateTime at)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("A requirement specification needs a name");
            }

            return new RequirementDocument { ProjectId = projectId, Name = name.Trim(), CreatedAt = at };
        }

        // R5, R6, R7: one upload, with its file and what it did to the requirements.
        // idPattern: the pattern a Word document was read with (US-033), or null for a spreadsheet.
        public RequirementDocumentVersion AddVersion(
            RequirementFile file, ImportCounts counts, IReadOnlyList<string> skipped, Guid userId, DateTime at, IdPattern? idPattern = null)
        {
            IdPattern = idPattern?.Value;
            var version = new RequirementDocumentVersion(Id, NextVersionNumber, file, counts, skipped, userId, at);
            _versions.Add(version);
            return version;
        }
    }

    // One uploaded version of a requirement specification. Never changed afterwards.
    // The file's content is stored apart (Infrastructure), so listing versions never loads the files.
    public class RequirementDocumentVersion
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid DocumentId { get; private set; }
        public int Number { get; private set; }

        public string FileName { get; private set; } = "";
        public string ContentType { get; private set; } = "";
        public long SizeInBytes { get; private set; }

        // Fingerprint of the file, so an auditor can prove which version was imported
        public string Sha256 { get; private set; } = "";

        public Guid UploadedById { get; private set; }
        public DateTime UploadedAt { get; private set; }

        // R6: what the upload did
        public int Added { get; private set; }
        public int Changed { get; private set; }
        public int Removed { get; private set; }

        // R3: the rows that were skipped and why, e.g. "Row 4 skipped: no ID"
        public List<string> SkippedRows { get; private set; } = new();

        // For EF Core only
        private RequirementDocumentVersion() { }

        internal RequirementDocumentVersion(Guid documentId, int number, RequirementFile file, ImportCounts counts,
            IReadOnlyList<string> skipped, Guid userId, DateTime at)
        {
            DocumentId = documentId;
            Number = number;
            FileName = file.FileName;
            ContentType = file.ContentType;
            SizeInBytes = file.Content.Length;
            Sha256 = file.Sha256;
            Added = counts.Added;
            Changed = counts.Changed;
            Removed = counts.Removed;
            SkippedRows = skipped.ToList();
            UploadedById = userId;
            UploadedAt = at;
        }

        // R6, e.g. "Version 2: 1 added, 1 changed, 1 removed, 0 skipped"
        public string Summary => $"Version {Number}: {Added} added, {Changed} changed, {Removed} removed, {SkippedRows.Count} skipped";
    }

    // R6: how many requirements an upload added, changed and marked as removed
    public record ImportCounts(int Added, int Changed, int Removed);
}
