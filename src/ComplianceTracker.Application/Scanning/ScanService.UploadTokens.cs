using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Scanning
{
    public interface IScanUploadTokenRepository
    {
        Task<IReadOnlyList<ScanUploadToken>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task<ScanUploadToken?> GetAsync(Guid projectId, Guid tokenId, CancellationToken cancellationToken = default);

        // By the SHA-256 of the token; any project
        Task<ScanUploadToken?> FindByHashAsync(string hash, CancellationToken cancellationToken = default);

        Task AddAsync(ScanUploadToken token, CancellationToken cancellationToken = default);
    }

    // US-122: how an upload with a token went; the API turns it into the HTTP status
    public enum PipelineUploadStatus
    {
        Created,
        Unauthorized,
        TooLarge,
        UnsupportedFormat,
        TooManyRequests,
        Invalid
    }

    public record PipelineUploadResult(PipelineUploadStatus Status, string Message, Scan? Scan = null);

    // US-122: upload tokens for build pipelines, and the upload they make
    public partial class ScanService
    {
        // R2: the token is returned once; EditProjects (R7: logged without the token)
        public async Task<string> CreateUploadTokenAsync(Guid projectId, string? name, CancellationToken cancellationToken = default)
        {
            var (userId, project) = await OpenProjectAsync(projectId, Permission.EditProjects, cancellationToken);
            var (token, secret) = ScanUploadToken.Create(project.Id, name, userId, Now);

            await _tokens.AddAsync(token, cancellationToken);
            await _log.LogAsync(ActivityAction.ScanUploadTokenCreated, project.Id, token.Name, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return secret;
        }

        // R3
        public async Task RevokeUploadTokenAsync(Guid projectId, Guid tokenId, CancellationToken cancellationToken = default)
        {
            var (userId, project) = await OpenProjectAsync(projectId, Permission.EditProjects, cancellationToken);
            var token = await _tokens.GetAsync(project.Id, tokenId, cancellationToken)
                ?? throw new DomainException("Token not found");

            token.Revoke(userId, Now);
            await _log.LogAsync(ActivityAction.ScanUploadTokenRevoked, project.Id, token.Name, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R1, R3–R6: the pipeline's upload. No signed-in member: the token is the only proof. The file is read (read) only
        // after the token and the size (length) are checked.
        public async Task<PipelineUploadResult> UploadFromPipelineAsync(
            Guid projectId, string? secret, string? fileName, long length, Func<CancellationToken, Task<byte[]>>? read,
            string? commit, string? version, CancellationToken cancellationToken = default)
        {
            const string NotValid = "The token is not valid for this project";
            if (string.IsNullOrWhiteSpace(secret) || !secret.StartsWith(ScanUploadToken.Prefix, StringComparison.Ordinal))
            {
                return new(PipelineUploadStatus.Unauthorized, NotValid);
            }

            var token = await _tokens.FindByHashAsync(ScanUploadToken.HashOf(secret.Trim()), cancellationToken);
            if (token is null || !token.Allows(projectId)
                || await _projects.GetForSyncAsync(projectId, cancellationToken) is not { } project)
            {
                return new(PipelineUploadStatus.Unauthorized, NotValid);
            }

            if (project.IsClosed)
            {
                return new(PipelineUploadStatus.Invalid, "A project at end of support cannot be changed");
            }

            if (ScanUploadToken.LimitReached(await _scans.CountByTokenSinceAsync(token.Id, Now.AddHours(-1), cancellationToken)))
            {
                return new(PipelineUploadStatus.TooManyRequests, $"At most {ScanUploadToken.MaxUploadsPerHour} uploads per hour with one token");
            }

            if (read is null || length <= 0)
            {
                return new(PipelineUploadStatus.Invalid, "Send the SARIF file as the form field \"file\"");
            }

            if (length > Scan.MaxSizeInBytes)
            {
                return new(PipelineUploadStatus.TooLarge, "The scan is larger than 50 MB");
            }

            var content = await read(cancellationToken);

            var name = string.IsNullOrWhiteSpace(fileName) ? "scan.sarif" : fileName;
            if (!_importers.Any(i => i.CanRead(name, content)))
            {
                return new(PipelineUploadStatus.UnsupportedFormat, "Only SARIF 2.1.0 files can be uploaded");
            }

            Scan scan;
            try
            {
                scan = await ImportAsync(project, null, name, content, ScanSource.CiUpload, cancellationToken, (token, commit, version));
            }
            catch (DomainException error)
            {
                return new(PipelineUploadStatus.Invalid, error.Message);
            }

            token.Used(Now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new(PipelineUploadStatus.Created, scan.Summary, scan);
        }

        private async Task<(Guid UserId, Domain.Projects.Project Project)> OpenProjectAsync(
            Guid projectId, Permission permission, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(permission, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (user.Id, project);
        }
    }
}
