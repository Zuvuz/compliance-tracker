using ComplianceTracker.Domain.Common;
using System.Security.Cryptography;

namespace ComplianceTracker.Domain.Scanning
{
    // US-078: one uploaded scan result (SARIF) of a project: which tool, when, the file's fingerprint, and how the
    // findings changed. Its own aggregate; the file is stored next to it (ScanFile) so the list does not load it.
    public class Scan
    {
        public const long MaxSizeInBytes = 50 * 1024 * 1024;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string Tool { get; private set; } = "";
        public string FileName { get; private set; } = "";
        public string Sha256 { get; private set; } = "";
        public DateTime ScannedAt { get; private set; }
        // US-121: null when the scheduled sync fetched it from GitHub
        public Guid? UploadedById { get; private set; }

        // US-121: where the scan came from
        public ScanSource Source { get; private set; } = ScanSource.Upload;

        // US-122 R4: what was built, when a pipeline sent it, and with which token
        public string? CommitSha { get; private set; }
        public string? Version { get; private set; }
        public Guid? UploadTokenId { get; private set; }

        public const int MaxVersionLength = 100;

        // R7: what the import did
        public int New { get; private set; }
        public int Fixed { get; private set; }
        public int Open { get; private set; }
        public int OpenCritical { get; private set; }

        // For EF Core only
        private Scan() { }

        // R1: the importer has read the file; the caller checks the format
        public static Scan Create(Guid projectId, string tool, string fileName, byte[] content, Guid? userId, DateTime at,
            ScanSource source = ScanSource.Upload)
        {
            if (content.Length == 0)
            {
                throw new DomainException("The file is empty");
            }

            if (content.Length > MaxSizeInBytes)
            {
                throw new DomainException("The scan is larger than 50 MB");
            }

            return new Scan
            {
                ProjectId = projectId,
                Tool = string.IsNullOrWhiteSpace(tool) ? "Unknown tool" : Cut(tool.Trim(), 200),
                FileName = Cut(Path.GetFileName(fileName ?? "scan.sarif"), 255),
                Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                ScannedAt = at,
                UploadedById = userId,
                Source = source
            };
        }

        // US-122 R4: the commit is a Git SHA (7 to 40 hex characters); both are optional
        public void FromPipeline(Guid tokenId, string? commit, string? version)
        {
            var sha = string.IsNullOrWhiteSpace(commit) ? null : commit.Trim().ToLowerInvariant();
            if (sha is not null && (sha.Length is < 7 or > 40 || !sha.All(Uri.IsHexDigit)))
            {
                throw new DomainException("The commit must be a Git SHA of 7 to 40 hex characters");
            }

            var text = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
            if (text?.Length > MaxVersionLength)
            {
                throw new DomainException($"The version must be {MaxVersionLength} characters or fewer");
            }

            if (text is not null && text.Any(char.IsControl))
            {
                throw new DomainException("The version must be on one line");
            }

            UploadTokenId = tokenId;
            CommitSha = sha;
            Version = text;
        }

        // e.g. "Commit 3f2a9c1 · version 2.3.1"; null when it has neither
        public string? Build => (CommitSha, Version) switch
        {
            (null, null) => null,
            ({ } c, null) => $"Commit {c[..Math.Min(7, c.Length)]}",
            (null, { } v) => $"Version {v}",
            ({ } c, { } v) => $"Commit {c[..Math.Min(7, c.Length)]} · version {v}"
        };

        public void Record(int added, int fixedCount, int open, int openCritical)
        {
            New = added;
            Fixed = fixedCount;
            Open = open;
            OpenCritical = openCritical;
        }

        // R7, e.g. "Trivy: 3 new, 0 fixed, 3 open (1 critical)"
        public string Summary => $"{Tool}: {New} new, {Fixed} fixed, {Open} open" + (OpenCritical > 0 ? $" ({OpenCritical} critical)" : "");

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }

    // US-121: where a scan came from. Stored as text.
    public enum ScanSource
    {
        Upload,
        GitHubCodeScanning,
        GitHubDependabot,

        // US-122
        CiUpload
    }

    // The uploaded file, by the scan's id
    public class ScanFile
    {
        public Guid ScanId { get; private set; }
        public byte[] Content { get; private set; } = [];

        // For EF Core only
        private ScanFile() { }

        public ScanFile(Guid scanId, byte[] content)
        {
            ScanId = scanId;
            Content = content;
        }
    }

    // R2: one result of a tool, followed across scans by its fingerprint (R3)
    public class Finding
    {
        public const int MaxReasonLength = 1000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public string Tool { get; private set; } = "";
        public string Fingerprint { get; private set; } = "";
        public string RuleId { get; private set; } = "";
        public FindingSeverity Severity { get; private set; }
        public string Message { get; private set; } = "";
        public string? Location { get; private set; }

        public FindingState State { get; private set; } = FindingState.Open;
        public Guid FirstScanId { get; private set; }
        public Guid LastScanId { get; private set; }
        public DateTime? FixedAt { get; private set; }

        public string? AcceptedReason { get; private set; }
        public Guid? AcceptedById { get; private set; }
        public DateTime? AcceptedAt { get; private set; }

        public bool IsOpen => State == FindingState.Open;

        // For EF Core only
        private Finding() { }

        public static Finding Create(Guid projectId, string tool, string fingerprint, string ruleId, FindingSeverity severity,
            string message, string? location, Guid scanId)
            => new()
            {
                ProjectId = projectId,
                Tool = Cut(tool, 200),
                Fingerprint = Cut(fingerprint, 200),
                RuleId = Cut(string.IsNullOrWhiteSpace(ruleId) ? "(no rule)" : ruleId, 200),
                Severity = severity,
                Message = Cut(string.IsNullOrWhiteSpace(message) ? "(no message)" : message.Trim(), 2000),
                Location = location is null ? null : Cut(location, 500),
                FirstScanId = scanId,
                LastScanId = scanId
            };

        // R3: reported again. A fixed finding that comes back is open again; an accepted one stays accepted.
        public void SeenIn(Guid scanId)
        {
            LastScanId = scanId;
            if (State == FindingState.Fixed)
            {
                State = FindingState.Open;
                FixedAt = null;
            }
        }

        // R3: no longer reported by the same tool
        public bool MissingFrom(Guid scanId, DateTime at)
        {
            if (State != FindingState.Open)
            {
                return false;
            }

            State = FindingState.Fixed;
            FixedAt = at;
            LastScanId = scanId;
            return true;
        }

        // R4
        public void Accept(string? reason, Guid userId, DateTime at)
        {
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("A reason is required");
            }

            if (State == FindingState.Accepted)
            {
                throw new DomainException("The finding is already accepted");
            }

            State = FindingState.Accepted;
            AcceptedReason = Cut(text, MaxReasonLength);
            AcceptedById = userId;
            AcceptedAt = at;
        }

        // US-121 R4: dismissed in the source (GitHub); no person of the organisation decided it here.
        // Returns false when it is accepted already.
        public bool AcceptFromSource(string reason, DateTime at)
        {
            if (State == FindingState.Accepted)
            {
                return false;
            }

            State = FindingState.Accepted;
            AcceptedReason = Cut(string.IsNullOrWhiteSpace(reason) ? "Dismissed in the source" : reason.Trim(), MaxReasonLength);
            AcceptedById = null;
            AcceptedAt = at;
            return true;
        }

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }

    // Stored as text
    public enum FindingSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum FindingState
    {
        Open,
        Fixed,
        Accepted
    }
}
