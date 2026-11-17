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

        // US-148: how many SBOMs a token sent since a time (the limit per hour, as for scans)
        Task<int> CountByTokenSinceAsync(Guid tokenId, DateTime since, CancellationToken cancellationToken = default);
    }

    // US-148 R3, R8: GitHub's SBOM of a repository from its dependency graph (SPDX 2.3 JSON), or why there is none
    public interface IGitHubSbom
    {
        Task<GitHubSbomRead> ReadAsync(string repository, string? token, CancellationToken cancellationToken = default);
    }

    public sealed record GitHubSbomRead(byte[]? Content, string? Unavailable);

    // US-148: how a pipeline's SBOM went; the API turns it into the HTTP status
    public sealed record PipelineSbomResult(PipelineUploadStatus Status, string Message, Sbom? Sbom = null);

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
        private readonly IScanUploadTokenRepository _tokens; // US-148
        private readonly IGitHubSbom _github; // US-148
        private readonly ISecretProtector _secrets; // US-148
        private readonly IScanRepository _scans;
        private readonly IEnumerable<ISbomImporter> _importers;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        private readonly Reports.IReportSnapshotRepository _reports; // US-177

        public SbomService(IProjectRepository projects, ISbomRepository sboms, IScanRepository scans, IEnumerable<ISbomImporter> importers,
            CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock,
            IScanUploadTokenRepository tokens, IGitHubSbom github, ISecretProtector secrets, Reports.IReportSnapshotRepository reports)
        {
            _reports = reports;
            _tokens = tokens;
            _github = github;
            _secrets = secrets;
            _projects = projects;
            _sboms = sboms;
            _scans = scans;
            _importers = importers;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // US-177 R2-R4: an SBOM uploaded by mistake is withdrawn with a reason, by a member who may upload SBOMs, while no
        // locked report used it; the previous SBOM of the version is the current one again
        public async Task WithdrawAsync(Guid projectId, Guid sbomId, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken) ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var sboms = await _sboms.ListAsync(project.Id, cancellationToken);
            var sbom = sboms.FirstOrDefault(s => s.Id == sbomId) ?? throw new DomainException("SBOM not found");
            var locked = await _reports.ListAsync(organization.Id, project.Id, cancellationToken);
            if (locked.Any(r => sbom.UsedByReportLockedAt(r.LockedAt, sboms)))
            {
                throw new DomainException("Used by a locked report; upload a corrected SBOM instead");
            }

            sbom.Withdraw(reason, user.Id, _clock.GetUtcNow().UtcDateTime);
            await _log.LogAsync(ActivityAction.SbomWithdrawn, project.Id, $"SBOM {sbom.FileName} for version {sbom.ProductVersion} withdrawn ({sbom.WithdrawnReason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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

            var sbom = await ImportAsync(project, user.Id, productVersion, fileName, content, SbomSource.Upload, null, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return sbom;
        }

        public const string NotValid = "The token is not valid for this project";
        public const string Unsupported = "Only CycloneDX (1.4 or newer) and SPDX 2.3 JSON files can be uploaded";

        // US-148 R1, R2, R6, R7: a pipeline sends the SBOM with the project's upload token (US-122). The file is read only
        // after the token and the size are checked. Nothing is stored when the token is not valid.
        public async Task<PipelineSbomResult> UploadFromPipelineAsync(
            Guid projectId, string? secret, string? fileName, long length, Func<CancellationToken, Task<byte[]>>? read,
            string? version, string? commit, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(secret) || !secret.StartsWith(ScanUploadToken.Prefix, StringComparison.Ordinal))
            {
                return new(PipelineUploadStatus.Unauthorized, NotValid);
            }

            var token = await _tokens.FindByHashAsync(ScanUploadToken.HashOf(secret.Trim()), cancellationToken);
            if (token is null || !token.Allows(projectId) || await _projects.GetForSyncAsync(projectId, cancellationToken) is not { } project)
            {
                return new(PipelineUploadStatus.Unauthorized, NotValid);
            }

            if (project.IsClosed)
            {
                return new(PipelineUploadStatus.Invalid, "A project at end of support cannot be changed");
            }

            if (ScanUploadToken.LimitReached(await _sboms.CountByTokenSinceAsync(token.Id, Now.AddHours(-1), cancellationToken)))
            {
                return new(PipelineUploadStatus.TooManyRequests, $"At most {ScanUploadToken.MaxUploadsPerHour} uploads per hour with one token");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                return new(PipelineUploadStatus.Invalid, "Send the product version");
            }

            if (read is null || length <= 0)
            {
                return new(PipelineUploadStatus.Invalid, "Send the SBOM as the form field \"file\"");
            }

            if (length > Sbom.MaxSizeInBytes)
            {
                return new(PipelineUploadStatus.TooLarge, "The SBOM is larger than 50 MB");
            }

            var content = await read(cancellationToken);
            var name = string.IsNullOrWhiteSpace(fileName) ? "sbom.json" : fileName;
            if (!_importers.Any(i => i.CanRead(name, content)))
            {
                return new(PipelineUploadStatus.UnsupportedFormat, Unsupported);
            }

            Sbom sbom;
            try
            {
                sbom = await ImportAsync(project, null, version, name, content, SbomSource.CiUpload, (token, commit), cancellationToken);
            }
            catch (DomainException error)
            {
                return new(PipelineUploadStatus.Invalid, error.Message);
            }

            token.Used(Now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new(PipelineUploadStatus.Created, sbom.Summary, sbom);
        }

        // US-148 R3, R8: "Fetch SBOM from GitHub" on the Scans tab. Returns what was imported, or why nothing was.
        public async Task<(Sbom? Sbom, string? Unavailable)> FetchFromGitHubAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var result = await FetchAsync(project, user.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return result;
        }

        // US-148 R4: with the scheduled GitHub sync, at most once a day per project, without a user. One project failing
        // does not stop the others.
        public async Task FetchAllFromGitHubAsync(CancellationToken cancellationToken = default)
        {
            foreach (var projectId in await _projects.ListIdsLinkedToGitHubAsync(cancellationToken))
            {
                var project = await _projects.GetForSyncAsync(projectId, cancellationToken);
                if (project?.GitHub is not { IsProject: false } || project.IsClosed)
                {
                    continue;
                }

                var last = (await _sboms.ListAsync(project.Id, cancellationToken)).Where(s => s.Source == SbomSource.GitHub).Max(s => (DateTime?)s.ImportedAt);
                if (!DueForGitHub(last, Now))
                {
                    continue;
                }

                try
                {
                    await FetchAsync(project, userId: null, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (DomainException)
                {
                    // e.g. a token that can no longer be read; the button on the Scans tab shows why
                }
            }
        }

        // R4: once a day
        public static bool DueForGitHub(DateTime? lastFetch, DateTime now) => lastFetch is not { } last || now - last >= TimeSpan.FromDays(1);

        private async Task<(Sbom? Sbom, string? Unavailable)> FetchAsync(Domain.Projects.Project project, Guid? userId, CancellationToken cancellationToken)
        {
            if (project.GitHub is not { IsProject: false } link)
            {
                throw new DomainException("Link the project to a GitHub repository first");
            }

            string? token;
            try
            {
                token = link.EncryptedToken is null ? null : _secrets.Unprotect(link.EncryptedToken);
            }
            catch (SecretUnreadableException)
            {
                throw new DomainException("The stored token can no longer be read. Enter it again");
            }

            GitHubSbomRead read;
            try
            {
                read = await _github.ReadAsync(link.Repository, token, cancellationToken);
            }
            catch (WorkItems.WorkItemSourceException error)
            {
                throw new DomainException(error.Message);
            }

            if (read.Content is not { } content)
            {
                return (null, read.Unavailable ?? $"GitHub has no dependency graph for {link.Repository}");
            }

            var sbom = await ImportAsync(project, userId, Sbom.GitHubVersion, $"github-{link.Repository.Replace('/', '-')}.spdx.json", content,
                SbomSource.GitHub, null, cancellationToken);
            return (sbom, null);
        }

        // R7: one import for every source, logged by the member or, for a pipeline or the scheduled sync, by the system
        private async Task<Sbom> ImportAsync(Domain.Projects.Project project, Guid? userId, string? version, string fileName, byte[] content,
            SbomSource source, (ScanUploadToken Token, string? Commit)? pipeline, CancellationToken cancellationToken)
        {
            var importer = _importers.FirstOrDefault(i => i.CanRead(fileName, content)) ?? throw new DomainException(Unsupported);
            var read = importer.Read(content);

            var sbom = Sbom.Create(project.Id, version, read.Format, fileName, content, read.Components, userId, Now, source);
            if (pipeline is { } sent)
            {
                sbom.FromPipeline(sent.Token.Id, sent.Token.Name, sent.Commit);
            }

            await _sboms.AddAsync(sbom, content, cancellationToken);
            var details = source == SbomSource.Upload
                ? $"{sbom.Summary} ({sbom.FileName})"
                : $"{sbom.Summary} ({sbom.SourceText}{(sbom.CommitSha is { } sha ? $", commit {sha[..Math.Min(7, sha.Length)]}" : "")}, {sbom.FileName})";
            if (userId is null)
            {
                await _log.LogSystemAsync(project.OrganizationId, ActivityAction.SbomImported, project.Id, details, cancellationToken);
            }
            else
            {
                await _log.LogAsync(ActivityAction.SbomImported, project.Id, details, cancellationToken);
            }

            return sbom;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

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
