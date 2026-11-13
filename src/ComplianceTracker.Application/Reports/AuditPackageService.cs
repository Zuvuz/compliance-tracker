using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Audits;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Reports;

namespace ComplianceTracker.Application.Reports
{
    // One file of the package, or a part that is not in it and why (R6)
    public record PackagePart(string Path, byte[]? Content, string Description, string? NotIncluded = null)
    {
        public string? Sha256 => Content is null ? null : Convert.ToHexString(SHA256.HashData(Content)).ToLowerInvariant();
    }

    // US-076: one locked ZIP with everything an auditor asks for. It is kept like a locked report (US-050), so it is listed
    // with its fingerprint, can be downloaded and checked, and never changes (B95).
    public class AuditPackageService
    {
        public const string Kind = "audit-package";

        private readonly ReportFiles _files;
        private readonly ProjectQueries _projects;
        private readonly ComplianceQueries _compliance;
        private readonly IEvidenceRepository _evidence;
        private readonly IRequirementDocumentRepository _documents;
        private readonly ISbomRepository _sboms;
        private readonly AuditService _audits;
        private readonly IActivityLogRepository _log;
        private readonly IReportSnapshotRepository _snapshots;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _clock;

        public AuditPackageService(
            ReportFiles files, ProjectQueries projects, ComplianceQueries compliance, IEvidenceRepository evidence, IRequirementDocumentRepository documents,
            ISbomRepository sboms, AuditService audits, IActivityLogRepository log, IReportSnapshotRepository snapshots, CurrentMember member,
            IActivityLogger logger, IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock)
        {
            _files = files;
            _projects = projects;
            _compliance = compliance;
            _evidence = evidence;
            _documents = documents;
            _sboms = sboms;
            _audits = audits;
            _log = log;
            _snapshots = snapshots;
            _member = member;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _clock = clock;
        }

        // R1–R5: for every member, also the Auditor (R4). "frameworks" are slugs; none means all the project follows.
        public async Task<ReportSnapshot> CreateAsync(Guid projectId, IReadOnlyCollection<string>? frameworks, DateOnly? from, DateOnly? to,
            CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var project = await _projects.GetAsync(projectId, cancellationToken) ?? throw new DomainException("Project not found");
            var compliance = await _compliance.ForProjectAsync(project.Id, cancellationToken) ?? throw new DomainException("Project not found");

            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var (start, end) = Period(from, to, today);
            var chosen = compliance.Frameworks
                .Where(f => frameworks is null || frameworks.Count == 0 || frameworks.Contains(FrameworkSlugs.Slug(f.Framework.Name)))
                .ToList();
            if (compliance.Frameworks.Count > 0 && chosen.Count == 0)
            {
                throw new DomainException("Choose at least one of the project's frameworks");
            }

            var parts = new List<PackagePart>();

            // The reports, made now like a download
            if (chosen.Count == 0)
            {
                parts.Add(new PackagePart("01-traceability.pdf", null, "Traceability (US-052)", "The project follows no framework"));
            }

            foreach (var framework in chosen)
            {
                var slug = FrameworkSlugs.Slug(framework.Framework.Name);
                var traceability = await _files.TraceabilityAsync(project.Id, ReportAudience.Auditor, slug, cancellationToken);
                parts.Add(new PackagePart($"01-traceability-{slug}.pdf", traceability?.Pdf.Content, $"Traceability for an auditor, {FrameworkSlugs.Short(framework.Framework.Name)} (US-052)"));
            }

            var management = chosen.Count == 0 ? null : await _files.TraceabilityAsync(project.Id, ReportAudience.Management, chosen.Count == 1 ? FrameworkSlugs.Slug(chosen[0].Framework.Name) : null, cancellationToken);
            parts.Add(new PackagePart("02-compliance-status.pdf", management?.Pdf.Content, "Compliance status: the management report (US-141)",
                management is null ? "The project follows no framework" : null));

            var soa = await _files.StatementOfApplicabilityAsync(project.Id, cancellationToken);
            parts.Add(new PackagePart("03-statement-of-applicability.pdf", soa?.Pdf.Content, "Statement of Applicability (US-053)",
                soa is null ? "The organisation has no ISO/IEC 27001 framework" : null));

            var risks = await _files.RiskAssessmentAsync(project.Id, cancellationToken);
            parts.Add(new PackagePart("04-risk-assessment.pdf", risks?.Pdf.Content, "Risk assessment (US-054)"));

            parts.Add(new PackagePart($"05-activity-log-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv",
                await ActivityCsvAsync(organization, project, start, end, cancellationToken), $"Activity log of the project, {start:dd-MM-yyyy} – {end:dd-MM-yyyy} (US-055)"));

            // Every evidence file with its fingerprint; links are listed in the contents
            var links = new List<string>();
            foreach (var evidence in (await _evidence.ListActiveAsync(project.Id, cancellationToken)).OrderBy(e => e.Title, StringComparer.Ordinal))
            {
                if (!evidence.IsFile)
                {
                    links.Add($"{evidence.Title}: {evidence.Url}");
                    continue;
                }

                var file = await _compliance.EvidenceFileAsync(project.Id, evidence.Id, cancellationToken);
                parts.Add(new PackagePart($"evidence/{Safe(evidence.Title)}-{evidence.FileName}", file?.Content, $"Evidence \"{evidence.Title}\" (US-057)",
                    file is null ? "The file could not be read" : null));
            }

            foreach (var document in (await _documents.ListAsync(project.Id, cancellationToken)).Where(d => d.Latest is not null))
            {
                var version = document.Latest!;
                parts.Add(new PackagePart($"requirement-documents/{Safe(document.Name)}-v{version.Number}-{version.FileName}",
                    await _documents.GetContentAsync(version.Id, cancellationToken), $"Requirement document \"{document.Name}\", version {version.Number} (US-032)"));
            }

            foreach (var sbom in SbomService.Latest(await _sboms.ListAsync(project.Id, cancellationToken)))
            {
                parts.Add(new PackagePart($"sbom/{Safe(sbom.ProductVersion)}-{sbom.FileName}", await _sboms.GetContentAsync(sbom.Id, cancellationToken),
                    $"SBOM for version {sbom.ProductVersion}: {sbom.Summary} (US-079)"));
            }

            var audits = await _audits.ForProjectAsync(project.Id, cancellationToken);
            foreach (var audit in audits?.All ?? [])
            {
                var report = await _audits.ReportAsync(project.Id, audit.Id, cancellationToken);
                parts.Add(new PackagePart($"audits/{audit.HeldOn:yyyy-MM-dd}-{Safe(audit.Scope)}-{audit.ReportFileName}", report?.File.Content,
                    $"{AuditService.Describe(audit)}{(audits!.CorrectedBy(audit) is null ? "" : " (corrected later)")}"
                    + string.Concat(audit.Findings.Select(f => $"\n      Finding: {f.Title}, due {f.DueOn:dd-MM-yyyy}, {(f.IsOpen ? "open" : $"closed: {f.CloseReason}")}"))));
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var title = $"Audit package – {project.Name}, {start:dd-MM-yyyy} – {end:dd-MM-yyyy}";
            var contents = Contents(title, organization.Name, project.Name, chosen.Select(f => f.Framework.Name).ToList(), start, end,
                _currentUser.Identity.Name, now, parts, links);
            var zip = Zip([new PackagePart("00-contents.txt", Encoding.UTF8.GetBytes(contents), "Contents"), .. parts]);

            // R3, R5: kept like a locked report; the fingerprint is logged
            var snapshot = ReportSnapshot.Lock(organization.Id, project.Id, Kind, title,
                $"audit-package-{FrameworkSlugs.Slug(project.Name)}-{today:yyyy-MM-dd}.zip", "application/zip", zip, user.Id, user.Role, now);
            await _snapshots.AddAsync(snapshot, zip, cancellationToken);
            await _logger.LogAsync(ActivityAction.AuditPackageCreated, project.Id, $"{title}: SHA-256 {snapshot.Sha256}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return snapshot;
        }

        // R2: without dates the last 12 months; one date alone reaches 12 months from it, not past today
        public static (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to, DateOnly today)
        {
            var end = to ?? (from is { } f && f.AddYears(1) < today ? f.AddYears(1) : today);
            var start = from ?? end.AddYears(-1);
            if (start > end)
            {
                throw new DomainException("The period must start before it ends");
            }

            return (start, end);
        }

        // R1, R3, R6: the contents page: every file with its SHA-256, the links, and what is not included and why
        public static string Contents(string title, string organization, string project, IReadOnlyList<string> frameworks, DateOnly from, DateOnly to,
            string madeBy, DateTime now, IReadOnlyList<PackagePart> parts, IReadOnlyList<string> links)
        {
            var text = new StringBuilder();
            text.AppendLine(title).AppendLine();
            text.AppendLine($"Organisation: {organization}");
            text.AppendLine($"Project:      {project}");
            text.AppendLine($"Frameworks:   {(frameworks.Count == 0 ? "None" : string.Join(", ", frameworks))}");
            text.AppendLine($"Period:       {from:dd-MM-yyyy} – {to:dd-MM-yyyy}");
            text.AppendLine($"Made:         {now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)} by {madeBy}");
            text.AppendLine().AppendLine("The package's own SHA-256 is shown in the tracker and in its activity log; each file's is below.").AppendLine();

            text.AppendLine("Included");
            foreach (var part in parts.Where(p => p.Content is not null))
            {
                text.AppendLine($"  {part.Path}").AppendLine($"      {part.Description}").AppendLine($"      SHA-256 {part.Sha256}");
            }

            if (links.Count > 0)
            {
                text.AppendLine().AppendLine("Evidence that is a link");
                foreach (var link in links)
                {
                    text.AppendLine($"  {link}");
                }
            }

            var missing = parts.Where(p => p.Content is null).ToList();
            if (missing.Count > 0)
            {
                text.AppendLine().AppendLine("Not included");
                foreach (var part in missing)
                {
                    text.AppendLine($"  {part.Description}: {part.NotIncluded ?? "Not available"}");
                }
            }

            return text.ToString();
        }

        // The files in a fixed order, with a fixed time, so the same parts give the same package
        public static byte[] Zip(IEnumerable<PackagePart> parts)
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in parts.Where(p => p.Content is not null))
                {
                    var path = part.Path;
                    for (var n = 2; !used.Add(path); n++)
                    {
                        path = $"{Path.GetDirectoryName(part.Path)?.Replace('\\', '/')}/{n}-{Path.GetFileName(part.Path)}".TrimStart('/');
                    }

                    var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                    entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var stream = entry.Open();
                    stream.Write(part.Content);
                }
            }

            return memory.ToArray();
        }

        // A file name part without folders or odd characters
        public static string Safe(string text)
        {
            var cleaned = new string(text.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-').ToArray()).Trim('-', '.');
            while (cleaned.Contains("--", StringComparison.Ordinal))
            {
                cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
            }

            cleaned = cleaned.Length <= 60 ? cleaned : cleaned[..60];
            return cleaned.Length == 0 ? "file" : cleaned;
        }

        private async Task<byte[]> ActivityCsvAsync(Domain.Organizations.Organization organization, Domain.Projects.Project project, DateOnly from, DateOnly to,
            CancellationToken cancellationToken)
        {
            var (entries, _) = await _log.SearchAsync(new ActivityLogSearch(organization.Id, project.Id, null, [],
                from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime(), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime(),
                null, null, false, ActivitySort.Oldest, 0, ActivityLogExportService.MaxEntries), cancellationToken);
            var subjects = new Dictionary<Guid, ActivitySubject> { [project.Id] = new("Project", project.Name, project.Id) };
            return CsvFile.Write(ActivityLogExportService.Columns, ActivityLogExportService.Rows(entries, subjects, organization.Users));
        }
    }
}
