using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Scanning
{
    // US-079: reads one SBOM format. Picked by its content (R1), like the scan importers (US-083).
    public interface ISbomImporter
    {
        // Whether the file is in this importer's format
        bool CanRead(string fileName, byte[] content);

        // Throws DomainException with a message for the user when the file cannot be read
        SbomRead Read(byte[] content);
    }

    // e.g. ("CycloneDX 1.6", [openssl 3.0.1, …])
    public record SbomRead(string Format, IReadOnlyList<SbomComponentRead> Components);

    // Storage for SBOMs (implemented in Infrastructure)
    public interface ISbomRepository
    {
        // Newest first, with their components
        Task<IReadOnlyList<Sbom>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid sbomId, CancellationToken cancellationToken = default);

        Task AddAsync(Sbom sbom, byte[] content, CancellationToken cancellationToken = default);
    }

    // One component on the page, with the open findings that name it (R6)
    public record SbomComponentView(SbomComponent Component, IReadOnlyList<Finding> OpenFindings)
    {
        // e.g. "1 open critical finding" or "2 open findings"
        public string? FindingsText => OpenFindings.Count == 0
            ? null
            : OpenFindings.Max(f => f.Severity) is var worst && worst >= FindingSeverity.High
                ? $"{OpenFindings.Count} open {(OpenFindings.Count == 1 ? "finding" : "findings")} ({worst.ToString().ToLowerInvariant()})"
                : $"{OpenFindings.Count} open {(OpenFindings.Count == 1 ? "finding" : "findings")}";
    }

    // R2, R3, R6: the shown SBOM per product version (newest first), and the components of the one chosen
    public record SbomOverview(IReadOnlyList<Sbom> LatestPerVersion, Sbom? Shown, IReadOnlyList<SbomComponentView> Components)
    {
        public bool HasAny => LatestPerVersion.Count > 0;
    }

    public class SbomService
    {
        private readonly IProjectRepository _projects;
        private readonly ISbomRepository _sboms;
        private readonly IScanRepository _scans;
        private readonly IEnumerable<ISbomImporter> _importers;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public SbomService(IProjectRepository projects, ISbomRepository sboms, IScanRepository scans, IEnumerable<ISbomImporter> importers,
            CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _sboms = sboms;
            _scans = scans;
            _importers = importers;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R2, R7: reads the file, stores it for the product version, and logs it
        public async Task<Sbom> UploadAsync(Guid projectId, string? productVersion, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            if (content.Length > Sbom.MaxSizeInBytes)
            {
                throw new DomainException("The SBOM is larger than 50 MB");
            }

            var importer = _importers.FirstOrDefault(i => i.CanRead(fileName, content))
                ?? throw new DomainException("Only CycloneDX (1.4 or newer) and SPDX 2.3 JSON files can be uploaded");
            var read = importer.Read(content);

            var sbom = Sbom.Create(project.Id, productVersion, read.Format, fileName, content, read.Components, user.Id, _clock.GetUtcNow().UtcDateTime);
            await _sboms.AddAsync(sbom, content, cancellationToken);
            await _log.LogAsync(ActivityAction.SbomImported, project.Id, $"{sbom.Summary} ({sbom.FileName})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return sbom;
        }

        // The SBOM section of the Scans tab (every member); "version" picks the shown SBOM, else the newest.
        // Null for other organisations' projects.
        public async Task<SbomOverview?> ForProjectAsync(Guid projectId, string? version, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var latest = Latest(await _sboms.ListAsync(project.Id, cancellationToken));
            var shown = latest.FirstOrDefault(s => string.Equals(s.ProductVersion, version?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? latest.FirstOrDefault();
            if (shown is null)
            {
                return new SbomOverview([], null, []);
            }

            var open = (await _scans.ListFindingsAsync(project.Id, cancellationToken)).Where(f => f.IsOpen).ToList();
            return new SbomOverview(latest, shown, Match(shown, open));
        }

        // R2: the newest SBOM of each version, newest first
        public static IReadOnlyList<Sbom> Latest(IEnumerable<Sbom> sboms)
            => sboms.GroupBy(s => s.ProductVersion, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.ImportedAt).First())
                .OrderByDescending(s => s.ImportedAt)
                .ToList();

        // R3, R6: the components by name, each with the open findings whose message or location names it
        public static IReadOnlyList<SbomComponentView> Match(Sbom sbom, IReadOnlyList<Finding> openFindings)
            => sbom.Components
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Version, StringComparer.Ordinal)
                .Select(c => new SbomComponentView(c, openFindings
                    .Where(f => f.IsOpen && (c.IsNamedIn(f.Message) || c.IsNamedIn(f.Location) || c.IsNamedIn($"{f.Message} {f.Location}")))
                    .OrderByDescending(f => f.Severity)
                    .ToList()))
                .ToList();

        // R4: the file, to add it as evidence (US-057); null for other organisations' projects
        public async Task<(Sbom Sbom, byte[] Content)?> FileAsync(Guid projectId, Guid sbomId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project
                || (await _sboms.ListAsync(project.Id, cancellationToken)).FirstOrDefault(s => s.Id == sbomId) is not { } sbom
                || await _sboms.GetContentAsync(sbom.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (sbom, content);
        }
    }
}
