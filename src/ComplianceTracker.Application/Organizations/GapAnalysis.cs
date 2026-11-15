using System.IO.Compression;
using System.Text.RegularExpressions;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-188 R1, R4: what happened to one uploaded file
    public enum GapFileOutcome
    {
        Added,
        AlreadyUploaded,
        NotRead
    }

    // e.g. ("Backup procedure.docx", Added, "3 requirements suggested") or ("photo.heic", NotRead, "Only PDF, Word, …")
    public sealed record GapFileResult(string FileName, GapFileOutcome Outcome, string Note);

    public sealed record GapUploadResult(IReadOnlyList<GapFileResult> Files)
    {
        public int Added => Files.Count(f => f.Outcome == GapFileOutcome.Added);

        public string Summary => $"{Added} added, {Files.Count(f => f.Outcome == GapFileOutcome.AlreadyUploaded)} already uploaded, "
            + $"{Files.Count(f => f.Outcome == GapFileOutcome.NotRead)} not read";
    }

    // US-188 R1: the files of an upload, with a ZIP unpacked. Pure, so the edge cases are tested on their own.
    public static class GapFiles
    {
        public const int MaxFiles = 200;
        public const long MaxTotalBytes = 200L * 1024 * 1024;

        // The files to read, and the ones that cannot be read with the reason. Folders inside the ZIP are flattened: only
        // the file name is kept (ASVS V5.3.2), so nothing can be written outside its place.
        public static (IReadOnlyList<(string Name, byte[] Content)> Files, IReadOnlyList<GapFileResult> Problems) Expand(
            IEnumerable<(string Name, byte[] Content)> uploaded)
        {
            var files = new List<(string, byte[])>();
            var problems = new List<GapFileResult>();
            long total = 0;

            void Add(string name, byte[] content)
            {
                if (files.Count >= MaxFiles)
                {
                    problems.Add(new GapFileResult(name, GapFileOutcome.NotRead, $"More than {MaxFiles} files in one upload; upload the rest separately"));
                    return;
                }

                total += content.Length;
                if (total > MaxTotalBytes)
                {
                    problems.Add(new GapFileResult(name, GapFileOutcome.NotRead, "The upload is larger than 200 MB; upload the rest separately"));
                    return;
                }

                files.Add((name, content));
            }

            foreach (var (name, content) in uploaded)
            {
                if (!IsZip(name))
                {
                    Add(FileName(name), content);
                    continue;
                }

                try
                {
                    using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
                    foreach (var entry in zip.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
                    {
                        var entryName = FileName(entry.FullName);
                        if (entry.FullName.EndsWith('/') || entryName.Length == 0 || IsHidden(entry.FullName))
                        {
                            continue; // folders, and the system's own files such as __MACOSX/ and .DS_Store
                        }

                        if (IsZip(entryName))
                        {
                            problems.Add(new GapFileResult(entryName, GapFileOutcome.NotRead, "A ZIP inside a ZIP is not opened; upload it on its own"));
                            continue;
                        }

                        if (entry.Length > EvidenceFile.MaxSizeInBytes)
                        {
                            problems.Add(new GapFileResult(entryName, GapFileOutcome.NotRead, "The file is larger than 10 MB"));
                            continue;
                        }

                        Add(entryName, Read(entry));
                    }
                }
                catch (InvalidDataException)
                {
                    problems.Add(new GapFileResult(FileName(name), GapFileOutcome.NotRead, "The ZIP file cannot be opened"));
                }
            }

            return (files, problems);
        }

        // R1: a likely kind from the file name, e.g. "Access control policy.pdf" → Policy
        public static OrganisationDocumentKind Kind(string fileName)
        {
            var name = fileName.ToLowerInvariant();
            return name.Contains("polic") || name.Contains("politik") ? OrganisationDocumentKind.Policy
                : name.Contains("procedure") || name.Contains("instruks") || name.Contains("process") ? OrganisationDocumentKind.Procedure
                : name.Contains("risk") || name.Contains("risiko") ? OrganisationDocumentKind.RiskTable
                : OrganisationDocumentKind.Other;
        }

        // R1: the title is the file name without its extension
        public static string Title(string fileName)
        {
            var title = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Trim();
            return title.Length == 0 ? fileName : title.Length <= OrganisationDocument.MaxTitleLength ? title : title[..OrganisationDocument.MaxTitleLength];
        }

        private static bool IsZip(string name) => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        private static bool IsHidden(string path)
            => path.Replace('\\', '/').Split('/').Any(p => p.StartsWith('.') || p == "__MACOSX");

        private static string FileName(string path)
        {
            var normalised = path.Replace('\\', '/');
            return normalised[(normalised.LastIndexOf('/') + 1)..].Trim();
        }

        // Reads at most one byte more than allowed, so a ZIP that lies about its sizes cannot fill the memory
        private static byte[] Read(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                memory.Write(buffer, 0, read);
                if (memory.Length > EvidenceFile.MaxSizeInBytes)
                {
                    break;
                }
            }

            return memory.ToArray();
        }
    }

    // US-188 R2: the sentence of a document that fits a requirement best, shown with the AI's suggestion as a quote.
    // Words shared with the requirement count; short and common words do not. Pure.
    public static partial class Passages
    {
        public const int MaxLength = 300;

        private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
        {
            "that", "this", "with", "from", "have", "shall", "must", "their", "they", "which", "where", "when", "will", "into",
            "such", "other", "these", "those", "been", "being", "also", "each", "only", "more", "than", "under", "within",
            "including", "based", "should", "could", "would", "there", "about", "after", "before", "member", "states",
            "skal", "eller", "som", "med", "til", "for", "der", "det", "den", "ved", "være", "have", "fra", "efter"
        };

        public static string? Best(string? text, string requirement)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var wanted = Words(requirement).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var best = Sentences(text)
                .Select(s => (Sentence: s, Score: Words(s).Distinct(StringComparer.OrdinalIgnoreCase).Count(wanted.Contains)))
                .Where(s => s.Score > 0)
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Sentence.Length)
                .Select(s => s.Sentence)
                .FirstOrDefault();
            return best is null ? null : best.Length <= MaxLength ? best : best[..(MaxLength - 1)].TrimEnd() + "…";
        }

        private static IEnumerable<string> Sentences(string text)
            => SentenceEnd().Split(text).Select(s => Whitespace().Replace(s, " ").Trim()).Where(s => s.Length >= 10);

        private static IEnumerable<string> Words(string text)
            => WordPattern().Matches(text).Select(m => m.Value).Where(w => w.Length >= 4 && !Common.Contains(w)).Select(Stem);

        // "backups" and "backup" are the same word; good enough for a quote
        private static string Stem(string word) => word.Length > 4 && word.EndsWith('s') ? word[..^1] : word;

        [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n")]
        private static partial Regex SentenceEnd();

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();

        [GeneratedRegex(@"\p{L}[\p{L}\p{N}-]*")]
        private static partial Regex WordPattern();
    }

    // US-188: the NIS2 gap analysis's upload. Every readable file becomes one of the organisation's documents (US-136),
    // so it is stored once with its SHA-256, and the AI suggests the requirements it covers. Nothing counts until a person
    // approves a suggestion (R3). Needs ManageFrameworks, like every change to the documents.
    public class GapAnalysisService
    {
        private readonly IOrganisationDocumentRepository _documents;
        private readonly OrganisationDocumentService _documentService;
        private readonly DocumentSuggestionService _suggestions;
        private readonly CurrentMember _member;

        public GapAnalysisService(IOrganisationDocumentRepository documents, OrganisationDocumentService documentService,
            DocumentSuggestionService suggestions, CurrentMember member)
        {
            _documents = documents;
            _documentService = documentService;
            _suggestions = suggestions;
            _member = member;
        }

        public async Task<GapUploadResult> UploadAsync(IReadOnlyList<(string Name, byte[] Content)> uploaded, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            if (uploaded.Count == 0)
            {
                throw new DomainException("Choose one or more files, or a ZIP file");
            }

            var (files, problems) = GapFiles.Expand(uploaded);
            var results = new List<GapFileResult>(problems);

            // R4: a file that is already one of the documents (by its SHA-256) is recognised, also twice in one upload
            var known = (await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken))
                .Where(d => d.IsActive)
                .GroupBy(d => d.Sha256)
                .ToDictionary(g => g.Key, g => g.First().Title);
            var aiUnavailable = false;
            foreach (var (name, content) in files)
            {
                EvidenceFile file;
                try
                {
                    file = EvidenceFile.Create(name, content);
                }
                catch (DomainException e)
                {
                    results.Add(new GapFileResult(name, GapFileOutcome.NotRead, e.Message));
                    continue;
                }

                if (known.TryGetValue(file.Sha256, out var title))
                {
                    results.Add(new GapFileResult(name, GapFileOutcome.AlreadyUploaded, $"The same file as \"{title}\""));
                    continue;
                }

                var document = await _documentService.UploadAsync(GapFiles.Kind(name), GapFiles.Title(name), user.Id, "v1", name, content, null, cancellationToken);
                known[file.Sha256] = document.Title;

                string note;
                if (aiUnavailable)
                {
                    note = "Added; the AI could not be reached, ask for suggestions on the Documents page later";
                }
                else
                {
                    try
                    {
                        var count = await _suggestions.SuggestAsync(document.Id, cancellationToken: cancellationToken);
                        note = count == 0 ? "Added; the AI found no requirement it covers" : $"Added; the AI suggests {count} requirement{(count == 1 ? "" : "s")}";
                    }
                    catch (AiUnavailableException)
                    {
                        aiUnavailable = true;
                        note = "Added; the AI could not be reached, ask for suggestions on the Documents page later";
                    }
                }

                results.Add(new GapFileResult(name, GapFileOutcome.Added, note));
            }

            return new GapUploadResult(results);
        }
    }
}
