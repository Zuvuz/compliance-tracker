using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Risks
{
    // US-049 R9: reads one kind of spreadsheet into rows of cells; the first row is the header
    public interface IRiskSheetReader
    {
        bool CanRead(string fileName, byte[] content);

        // Throws DomainException with a message for the user when the file cannot be read
        IReadOnlyList<IReadOnlyList<string>> Read(byte[] content);
    }

    // Storage for imports waiting to be confirmed (implemented in Infrastructure)
    public interface IRiskImportRepository
    {
        Task AddAsync(RiskImport import, byte[] content, CancellationToken cancellationToken = default);

        Task<RiskImport?> GetAsync(Guid projectId, Guid importId, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid importId, CancellationToken cancellationToken = default);
    }

    // R3: what the preview shows before the import is confirmed
    public record RiskImportPreview(RiskImport Import, IReadOnlyList<RiskImportRow> Rows, IReadOnlyList<string> FileErrors)
    {
        public IReadOnlyList<string> Errors => [.. FileErrors, .. Rows.SelectMany(r => r.Errors)];
        public bool CanImport => Errors.Count == 0 && Rows.Count > 0 && !Import.IsConfirmed;
        public int Duplicates => Rows.Count(r => r.IsDuplicate);

        // R4: duplicates are skipped unless the member chooses "Import anyway"
        public IReadOnlyList<RiskImportRow> ToImport(bool importAnyway) => Rows.Where(r => importAnyway || !r.IsDuplicate).ToList();
    }

    // US-049: imports an existing risk register into a project's risks (US-042), all or nothing
    public class RiskImportService
    {
        private readonly IProjectRepository _projects;
        private readonly IRiskRepository _risks;
        private readonly IRiskImportRepository _imports;
        private readonly IEnumerable<IRiskSheetReader> _readers;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public RiskImportService(IProjectRepository projects, IRiskRepository risks, IRiskImportRepository imports, IEnumerable<IRiskSheetReader> readers,
            CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _risks = risks;
            _imports = imports;
            _readers = readers;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R8: only members with ManageRisks may import
        public async Task<bool> CanImportAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is not null && await _member.CanAsync(Permission.ManageRisks, cancellationToken);

        // R8, R9: stores the file for the preview; nothing becomes a risk yet
        public async Task<RiskImport> UploadAsync(Guid projectId, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (_, user, project) = await OpenAsync(projectId, cancellationToken);
            var import = RiskImport.Create(project.Id, fileName, content, user.Id, Now);
            _ = Reader(import.FileName, content).Read(content); // a file that cannot be read is refused at once
            await _imports.AddAsync(import, content, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return import;
        }

        // R2–R5: every row with its level and errors
        public async Task<RiskImportPreview> PreviewAsync(Guid projectId, Guid importId, CancellationToken cancellationToken = default)
        {
            var (organization, _, project) = await OpenAsync(projectId, cancellationToken);
            return await PreviewAsync(organization, project, importId, cancellationToken);
        }

        // R2, R4, R6, R7: all rows become risks, or none; logged once
        public async Task<int> ConfirmAsync(Guid projectId, Guid importId, bool importAnyway, CancellationToken cancellationToken = default)
        {
            var (organization, user, project) = await OpenAsync(projectId, cancellationToken);
            var preview = await PreviewAsync(organization, project, importId, cancellationToken);
            if (preview.Import.IsConfirmed)
            {
                throw new DomainException("The file has already been imported");
            }

            if (!preview.CanImport)
            {
                throw new DomainException("Fix the errors in the file and upload it again");
            }

            var rows = preview.ToImport(importAnyway);
            var now = Now;
            foreach (var row in rows)
            {
                var risk = Risk.Create(project.Id, row.Title, row.Description, row.Likelihood!.Value, row.Impact!.Value,
                    row.Owner?.Id ?? user.Id, row.Treatment, user.Id, now);
                risk.FromImport(preview.Import.FileName, row.Owner is null ? row.OwnerText : null);

                // R6: an accepted risk keeps its reason; a High or Critical one still needs a second person (US-135), so it
                // stays open with the treatment Accept until someone asks for and gets the approval
                if (row.Treatment == RiskTreatment.Accept && !risk.NeedsApprovalToAccept)
                {
                    risk.Accept(row.Reason, user.Id, now, user.Role);
                }

                risk.MarkReviewed(user.Id, row.LastReviewed is { } reviewed ? reviewed.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc) : now);
                await _risks.AddAsync(risk, cancellationToken);
            }

            preview.Import.Confirm(rows.Count, now);
            var skipped = preview.Rows.Count - rows.Count;
            await _log.LogAsync(ActivityAction.RisksImported, project.Id,
                $"Imported {rows.Count} {(rows.Count == 1 ? "risk" : "risks")} from {preview.Import.FileName}"
                + (skipped > 0 ? $"; {skipped} already in the project skipped" : ""), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        private async Task<RiskImportPreview> PreviewAsync(Organization organization, Project project, Guid importId, CancellationToken cancellationToken)
        {
            var import = await _imports.GetAsync(project.Id, importId, cancellationToken)
                ?? throw new DomainException("Import not found");
            var content = await _imports.GetContentAsync(import.Id, cancellationToken) ?? [];
            var sheet = Reader(import.FileName, content).Read(content);
            var existing = (await _risks.ListAsync(project.Id, cancellationToken)).Select(r => r.Title);
            var today = DateOnly.FromDateTime(Now);
            return new RiskImportPreview(import, RiskImportRows.Read(sheet, organization.Members, existing, today), RiskImportRows.FileErrors(sheet));
        }

        private IRiskSheetReader Reader(string fileName, byte[] content)
            => _readers.FirstOrDefault(r => r.CanRead(fileName, content))
               ?? throw new DomainException("Only CSV files and Excel workbooks (.xlsx) can be imported");

        private async Task<(Organization Organization, User User, Project Project)> OpenAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageRisks, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            return (organization, user, project);
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
