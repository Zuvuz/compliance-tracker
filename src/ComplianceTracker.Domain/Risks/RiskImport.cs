using System.Security.Cryptography;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Risks
{
    // US-049: an uploaded risk register (CSV or .xlsx) waiting to be confirmed. It is kept after the import, so the log
    // and the risks can point at the file they came from. The file is stored next to it (RiskImportFile).
    public class RiskImport
    {
        public const long MaxSizeInBytes = 1024 * 1024;
        public const int MaxRows = 500;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string FileName { get; private set; } = "";
        public string Sha256 { get; private set; } = "";
        public Guid CreatedById { get; private set; }
        public DateTime CreatedAt { get; private set; }

        // R7: set once, when the rows became risks
        public DateTime? ConfirmedAt { get; private set; }
        public int ImportedCount { get; private set; }

        public bool IsConfirmed => ConfirmedAt is not null;

        // For EF Core only
        private RiskImport() { }

        // R8: at most 1 MB
        public static RiskImport Create(Guid projectId, string? fileName, byte[] content, Guid userId, DateTime at)
        {
            if (content.Length == 0)
            {
                throw new DomainException("The file is empty");
            }

            if (content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The file is larger than 1 MB");
            }

            var name = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "risks.csv" : fileName.Trim());
            return new RiskImport
            {
                ProjectId = projectId,
                FileName = name.Length <= 255 ? name : name[^255..],
                Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                CreatedById = userId,
                CreatedAt = at
            };
        }

        // R7: an import is confirmed once
        public void Confirm(int importedCount, DateTime at)
        {
            if (IsConfirmed)
            {
                throw new DomainException("The file has already been imported");
            }

            ImportedCount = importedCount;
            ConfirmedAt = at;
        }
    }

    public class RiskImportFile
    {
        public Guid ImportId { get; private set; }
        public byte[] Content { get; private set; } = [];

        // For EF Core only
        private RiskImportFile() { }

        public RiskImportFile(Guid importId, byte[] content)
        {
            ImportId = importId;
            Content = content;
        }
    }
}
