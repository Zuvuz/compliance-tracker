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
        // US-197 R3: a PDF the package made itself (not an uploaded file), so it is sealed when the organisation has a seal
        public bool Made { get; init; }

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
        private readonly AuditReadinessService _readiness; // US-178
        private readonly ISpreadsheetWriter _spreadsheets; // US-179
        private readonly Projects.PhaseGates _gates; // US-194
        private readonly IPdfWriter _pdf; // US-197
        private readonly IReportSealer _sealer; // US-197
        private readonly IReportSigningRepository _signings; // US-197: the organisation's seal
        private readonly ISecretProtector _secrets; // US-197
        private readonly IPackageSigner _packageSigner; // US-180
        private readonly Projects.IClassificationRepository _classifications; // US-181
        private readonly AccessLogService _accessLog; // US-219

        public AuditPackageService(
            ReportFiles files, ProjectQueries projects, ComplianceQueries compliance, IEvidenceRepository evidence, IRequirementDocumentRepository documents,
            ISbomRepository sboms, AuditService audits, IActivityLogRepository log, IReportSnapshotRepository snapshots, CurrentMember member,
            IActivityLogger logger, IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock, AuditReadinessService readiness,
            ISpreadsheetWriter spreadsheets, Projects.PhaseGates gates, IPdfWriter pdf, IReportSealer sealer, IReportSigningRepository signings,
            ISecretProtector secrets, IPackageSigner packageSigner, Projects.IClassificationRepository classifications, AccessLogService accessLog)
        {
            _accessLog = accessLog;
            _classifications = classifications;
            _packageSigner = packageSigner;
            _pdf = pdf;
            _sealer = sealer;
            _signings = signings;
            _secrets = secrets;
            _gates = gates;
            _spreadsheets = spreadsheets;
            _readiness = readiness;
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

        private static void AddReport(List<ReportFile> reports, ReportFile? report)
        {
            if (report is not null)
            {
                reports.Add(report);
            }
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
            var reports = new List<ReportFile>(); // US-198 R1: locked with the package

            // The reports, made now like a download
            if (chosen.Count == 0)
            {
                parts.Add(new PackagePart("01-traceability.pdf", null, "Traceability (US-052)", "The project follows no framework"));
            }

            foreach (var framework in chosen)
            {
                var slug = FrameworkSlugs.Slug(framework.Framework.Name);
                var traceability = await _files.TraceabilityAsync(project.Id, ReportAudience.Auditor, slug, inPackage: true, cancellationToken);
                AddReport(reports, traceability?.Pdf);
                parts.Add(new PackagePart($"01-traceability-{slug}.pdf", traceability?.Pdf.Content, $"Traceability for an auditor, {FrameworkSlugs.Short(framework.Framework.Name)} (US-052)") { Made = true });
            }

            var management = chosen.Count == 0 ? null : await _files.TraceabilityAsync(project.Id, ReportAudience.Management, chosen.Count == 1 ? FrameworkSlugs.Slug(chosen[0].Framework.Name) : null, inPackage: true, cancellationToken);
            AddReport(reports, management?.Pdf);
            parts.Add(new PackagePart("02-compliance-status.pdf", management?.Pdf.Content, "Compliance status: the management report (US-141)",
                management is null ? "The project follows no framework" : null) { Made = true });

            var soa = await _files.StatementOfApplicabilityAsync(project.Id, cancellationToken);
            AddReport(reports, soa?.Pdf);
            parts.Add(new PackagePart("03-statement-of-applicability.pdf", soa?.Pdf.Content, "Statement of Applicability (US-053)",
                soa is null ? "The organisation has no ISO/IEC 27001 framework" : null) { Made = true });

            var risks = await _files.RiskAssessmentAsync(project.Id, cancellationToken);
            AddReport(reports, risks?.Pdf);
            parts.Add(new PackagePart("04-risk-assessment.pdf", risks?.Pdf.Content, "Risk assessment (US-054)") { Made = true });

            var now = _clock.GetUtcNow().UtcDateTime;
            string NameOf(Guid? id) => organization.Users.FirstOrDefault(u => u.Id == id)?.Name ?? "Unknown";
            byte[] Pdf(GeneratedDocument document, string name)
                => _pdf.FromDocument(document, new PdfHeader(organization.Name, project.Name, name, now, _currentUser.Identity.Name));

            // US-197 R3, R4: the organisation's seal, when it has one that can be used now
            var seal = await _signings.GetActiveSealAsync(organization.Id, cancellationToken);
            if (seal?.WhyNotUsable(now) is not null)
            {
                seal = null;
            }

            // US-194 R2, R3: the lifecycle, every phase's checklist and the phase changes. US-197 R2: as PDF.
            var lifecycle = await _gates.LifecycleAsync(project.Id, compliance.Frameworks, cancellationToken);
            if (lifecycle is not null)
            {
                parts.Add(new PackagePart("lifecycle.pdf", Pdf(RequirementDossier.FromText("lifecycle.pdf", Projects.LifecycleReport.Text(project.Name, lifecycle, NameOf)), "Lifecycle"),
                    "The lifecycle phase, its checklists and the phase changes (US-194)") { Made = true });
            }

            // US-181 R1: a CRA project's package has the technical documentation and the declaration of conformity, as drafts
            CraProduct? craProduct = null;
            if (chosen.FirstOrDefault(f => ManagementPriorities.IsCra(f.Framework.Name)) is { } cra)
            {
                var technical = await _files.TechnicalDocumentationAsync(project.Id, cancellationToken);
                AddReport(reports, technical?.Pdf);
                parts.Add(new PackagePart(CraDeclaration.TechnicalDocumentationFileName, technical?.Pdf.Content,
                    "CRA technical documentation, Annex VII (draft, US-056)", technical is null ? "The draft could not be made" : null) { Made = true });

                var classCode = (await _classifications.ListAsync(project.Id, cancellationToken)).LastOrDefault(c => c.FrameworkId == cra.Framework.Id)?.Class;
                var version = SbomService.Latest(await _sboms.ListAsync(project.Id, cancellationToken)).FirstOrDefault()?.ProductVersion;
                craProduct = new CraProduct(project.Name, version, project.IntendedPurpose, organization.Name, organization.Cvr, organization.Country, classCode,
                    chosen.Where(f => f != cra).Select(f => f.Framework.Name).ToList());
                parts.Add(new PackagePart(CraDeclaration.FileName, Pdf(CraDeclaration.Build(craProduct), "EU declaration of conformity (draft)"),
                    "CRA EU declaration of conformity, Annex V (draft, US-181)") { Made = true });
            }

            parts.Add(new PackagePart($"05-activity-log-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv",
                await ActivityCsvAsync(organization, project, start, end, cancellationToken), $"Activity log of the project, {start:dd-MM-yyyy} – {end:dd-MM-yyyy} (US-055)"));

            // US-219 R8: who opened the project's sensitive pages in the period, when and for how long; only when the one who
            // makes the package may see the access log (R4)
            var accessLogPath = $"05-access-log-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv";
            parts.Add(AccessLogRules.CanSee(user.Role)
                ? new PackagePart(accessLogPath,
                    CsvFile.Write(AccessLogService.Columns, AccessLogService.CsvRows(await _accessLog.ForPeriodAsync(organization, project.Id, start, end, cancellationToken))),
                    $"Access log of the project, {start:dd-MM-yyyy} – {end:dd-MM-yyyy} (US-219)")
                : new PackagePart(accessLogPath, null, "Access log of the project (US-219)",
                    "Only an Organisation admin, a Compliance manager or an Auditor may see the access log"));

            // Every evidence file with its fingerprint, stored once (US-179 R3); links are listed in the contents
            var links = new List<string>();
            var evidenceFiles = new Dictionary<Guid, (string Path, string Sha256)>();
            var evidencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var evidence in (await _evidence.ListActiveAsync(project.Id, cancellationToken)).OrderBy(e => e.Title, StringComparer.Ordinal))
            {
                if (!evidence.IsFile)
                {
                    links.Add($"{evidence.Title}: {evidence.Url}");
                    continue;
                }

                var file = await _compliance.EvidenceFileAsync(project.Id, evidence.Id, cancellationToken);
                var path = AuditPackageIndex.EvidencePath(Safe(evidence.Title), evidence.FileName!, evidencePaths);
                var part = new PackagePart(path, file?.Content, $"Evidence \"{evidence.Title}\" (US-057)",
                    file is null ? "The file could not be read" : null);
                parts.Add(part);
                if (part.Sha256 is { } sha)
                {
                    evidenceFiles[evidence.Id] = (path, sha);
                }
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

            var title = $"Audit package – {project.Name}, {start:dd-MM-yyyy} – {end:dd-MM-yyyy}";

            // US-197 R1: one dossier per framework, instead of a folder with a README per requirement (US-179 R2).
            // US-179 R4: the index as HTML and Excel stays.
            var rows = AuditPackageIndex.Rows(chosen, evidenceFiles);
            foreach (var framework in chosen.Select(f => FrameworkSlugs.Short(f.Framework.Name)).Distinct())
            {
                var dossier = RequirementDossier.Build(framework, project.Name, rows.Where(r => r.Framework == framework).ToList(), lifecycle, NameOf, seal?.Subject, now);
                parts.Insert(0, new PackagePart(dossier.FileName, Pdf(dossier, $"{framework} dossier"),
                    $"{framework}: every requirement with its status, stories and evidence, and the lifecycle checklist (US-197)") { Made = true });
            }

            if (rows.Count > 0)
            {
                parts.Insert(0, new PackagePart(AuditPackageIndex.XlsxPath, _spreadsheets.ToXlsx(AuditPackageIndex.Sheet(title, rows)), "Index of the requirements and their evidence, as a workbook (US-179)"));
                parts.Insert(0, new PackagePart(AuditPackageIndex.HtmlPath, Encoding.UTF8.GetBytes(AuditPackageIndex.Html(title, rows)), "Index of the requirements and their evidence; open it in a browser (US-179)"));
            }

            // US-178 R3: what an auditor asks for and what is missing, in the package itself. US-197 R2: as PDF.
            var check = await _readiness.CheckAsync(project.Id, frameworks, cancellationToken);
            parts.Insert(0, new PackagePart("00-check.pdf", Pdf(RequirementDossier.FromText("00-check.pdf", (check is null ? "No check" : AuditReadiness.Text(check, today))
                + (craProduct is null ? "" : Environment.NewLine + CraDeclaration.CheckText(craProduct))), "Check"),
                "What an auditor asks for, and what is missing (US-178)") { Made = true });

            // US-197 R3: every PDF the package made is sealed before the contents list their SHA-256
            parts = parts.Select(p => p.Made && seal is not null && p.Content is not null ? p with { Content = Seal(p, seal, organization.Name, now) } : p).ToList();

            var contents = Contents(title, organization.Name, project.Name, chosen.Select(f => f.Framework.Name).ToList(), start, end,
                _currentUser.Identity.Name, now, parts, links, RequirementDossier.SealNote(seal?.Subject) + " " + ManifestNote(seal is not null));
            var contentsPart = new PackagePart("00-contents.pdf", Pdf(RequirementDossier.FromText("00-contents.pdf", contents), "Contents"), "Contents") { Made = true };
            if (seal is not null)
            {
                contentsPart = contentsPart with { Content = Seal(contentsPart, seal, organization.Name, now) };
            }

            // US-180 R1, R2: manifest.json with every file's SHA-256 (the paths as they are in the ZIP), signed with the seal
            var files = PackageManifest.Read(Zip([contentsPart, .. parts]));
            var manifest = PackageManifest.Create(title, organization.Name, project.Name, _currentUser.Identity.Name, now, seal is not null,
                files.Select(f => (f.Key, f.Value)));
            var manifestJson = PackageManifest.ToJson(manifest);
            List<PackagePart> signature = [new PackagePart(PackageManifest.ManifestPath, manifestJson, "Every file with its SHA-256 (US-180)")];
            if (seal is not null)
            {
                var (pfx, password) = Certificate(seal);
                signature.Add(new PackagePart(PackageManifest.SignaturePath, _packageSigner.Sign(manifestJson, pfx, password, now),
                    "The organisation's signature over manifest.json (US-180)"));
            }

            var zip = Zip([contentsPart, .. parts, .. signature]);

            // R3, R5: kept like a locked report; the fingerprint is logged
            var snapshot = ReportSnapshot.Lock(organization.Id, project.Id, Kind, title,
                $"audit-package-{FrameworkSlugs.Slug(project.Name)}-{today:yyyy-MM-dd}.zip", "application/zip", zip, user.Id, user.Role, now);
            await _snapshots.AddAsync(snapshot, zip, cancellationToken);

            // US-198 R1: every report in the package is locked too, with its fingerprint, and listed under the package
            foreach (var report in reports)
            {
                var locked = ReportSnapshot.Lock(organization.Id, project.Id, report.Kind, report.Title, report.FileName, report.ContentType,
                    report.Content, user.Id, user.Role, now, report.Figures, snapshot.Id);
                await _snapshots.AddAsync(locked, report.Content, cancellationToken);
            }
            await _logger.LogAsync(ActivityAction.AuditPackageCreated, project.Id, $"{title}: SHA-256 {snapshot.Sha256}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return snapshot;
        }

        // US-197 R3: the organisation's seal over a PDF the package made; the seal page names the file and its fingerprint
        private byte[] Seal(PackagePart part, Domain.Reports.OrganisationSeal seal, string organization, DateTime now)
        {
            var (pfx, password) = Certificate(seal);
            return _sealer.Seal(part.Content!, part.Path, part.Sha256!, [], pfx, password, organization, now);
        }

        private (byte[] Pfx, string Password) Certificate(Domain.Reports.OrganisationSeal seal)
            => (Convert.FromBase64String(_secrets.Unprotect(seal.ProtectedCertificate)), _secrets.Unprotect(seal.ProtectedPassword));

        // US-180 R2: what the contents say about the manifest
        public static string ManifestNote(bool signed)
            => signed
                ? "manifest.json lists every file with its SHA-256 and is signed (manifest.json.p7s); the receiver can check the package on the tracker's \"Verify an audit package\" page."
                : "Unsigned package: the files can be checked against manifest.json, the maker cannot.";

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
            string madeBy, DateTime now, IReadOnlyList<PackagePart> parts, IReadOnlyList<string> links, string? sealNote = null)
        {
            var text = new StringBuilder();
            text.AppendLine(title).AppendLine();
            text.AppendLine($"Organisation: {organization}");
            text.AppendLine($"Project:      {project}");
            text.AppendLine($"Frameworks:   {(frameworks.Count == 0 ? "None" : string.Join(", ", frameworks))}");
            text.AppendLine($"Period:       {from:dd-MM-yyyy} – {to:dd-MM-yyyy}");
            text.AppendLine($"Made:         {now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)} by {madeBy}");
            text.AppendLine().AppendLine("The package's own SHA-256 is shown in the tracker and in its activity log; each file's is below.");
            if (sealNote is not null)
            {
                text.AppendLine(sealNote); // US-197 R4
            }

            text.AppendLine();

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
