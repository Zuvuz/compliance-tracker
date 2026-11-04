using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Application.WorkItems
{
    // US-032: reads the requirements in one kind of file, e.g. Excel (implemented in Infrastructure).
    // Format errors the user can fix, such as a missing column, are thrown as DomainException.
    public interface IRequirementDocumentReader
    {
        // The extension it reads, e.g. ".xlsx"
        string Extension { get; }

        // US-034: what kind of document it reads: requirements or use cases
        DocumentKind Kind { get; }

        // Whether it uses the ID pattern (US-033: Word does, a spreadsheet has an ID column)
        bool UsesIdPattern { get; }

        RequirementReadResult Read(byte[] content, IdPattern idPattern);
    }

    // One requirement found in the file, and where: the row in a spreadsheet, the paragraph in a Word document
    public record ReadRequirement(DocumentWorkItem Item, int Position);

    // R3: what was found, and the rows that were skipped and why, e.g. "Row 7 skipped: no title".
    // PositionName names the positions in error messages, e.g. "row" → "(rows 4 and 9)".
    public record RequirementReadResult(IReadOnlyList<ReadRequirement> Items, IReadOnlyList<string> Skipped, string PositionName = "row");

    // Storage for requirement specifications (implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IRequirementDocumentRepository
    {
        // The project's specifications with their versions, oldest first
        Task<IReadOnlyList<RequirementDocument>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        // A new specification
        Task AddAsync(RequirementDocument document, CancellationToken cancellationToken = default);

        // The file of a new version, stored apart from the version
        Task AddContentAsync(Guid versionId, byte[] content, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid versionId, CancellationToken cancellationToken = default);
    }

    // US-032: uploads a requirement specification as a new version, and turns its requirements into work items.
    // Requirements are matched on their ID against the specification's earlier versions (R5); nothing is deleted.
    public class RequirementDocumentService
    {
        private readonly IProjectRepository _projects;
        private readonly IWorkItemRepository _workItems;
        private readonly IRequirementDocumentRepository _documents;
        private readonly IEnumerable<IRequirementDocumentReader> _readers;
        private readonly IWorkItemNotifier _notifier;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public RequirementDocumentService(
            IProjectRepository projects,
            IWorkItemRepository workItems,
            IRequirementDocumentRepository documents,
            IEnumerable<IRequirementDocumentReader> readers,
            IWorkItemNotifier notifier,
            CurrentMember member,
            IActivityLogger log,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _projects = projects;
            _workItems = workItems;
            _documents = documents;
            _readers = readers;
            _notifier = notifier;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1-R10. documentId: the specification this is a new version of; without it, the one with the same file name,
        // or a new specification. idPattern: how IDs look in a Word document (US-033 R2); empty means the default.
        // kind: requirements, or Unified Process use cases (US-034). Returns the new version. A broken rule changes nothing.
        public async Task<RequirementDocumentVersion> UploadAsync(
            Guid projectId, string fileName, byte[] content, Guid? documentId, string? idPattern = null,
            DocumentKind kind = DocumentKind.RequirementSpecification, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.EditProjects, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            // R1: size, type and content
            var file = RequirementFile.Create(fileName, content);
            var reader = _readers.FirstOrDefault(r => r.Extension == file.Extension && r.Kind == kind)
                ?? throw new DomainException(kind == DocumentKind.UseCases ? "A use case document must be a .docx file" : RequirementFile.AllowedMessage);

            // R2, R3, R4: read and check the whole file before anything is changed
            var pattern = IdPattern.Create(idPattern, kind);
            var read = reader.Read(file.Content, pattern);
            RefuseDuplicates(read);
            if (read.Items.Count == 0 && read.Skipped.Count == 0)
            {
                throw new DomainException("No requirements found in the file");
            }

            // R10: a new version of the chosen specification, or of the one with the same name
            var documents = await _documents.ListAsync(project.Id, cancellationToken);
            var document = documentId is { } id
                ? documents.FirstOrDefault(d => d.Id == id) ?? throw new DomainException("Requirement specification not found")
                : documents.FirstOrDefault(d => d.Name.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));
            if (document is not null && document.Kind != kind)
            {
                throw new DomainException(document.Kind == DocumentKind.UseCases
                    ? $"{document.Name} is a use case document"
                    : $"{document.Name} is a requirement specification");
            }

            var isNew = document is null;
            document ??= RequirementDocument.Create(project.Id, file.FileName, Now, kind);
            var number = document.NextVersionNumber;

            // R5: matched on ID within this specification only
            var existing = isNew
                ? new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase)
                : (await _workItems.ListByDocumentAsync(project.Id, document.Id, cancellationToken))
                    .ToDictionary(w => w.ExternalReference, StringComparer.OrdinalIgnoreCase);

            int added = 0, changed = 0, removed = 0;
            foreach (var requirement in read.Items)
            {
                if (existing.Remove(requirement.Item.Reference.Trim(), out var workItem))
                {
                    changed += workItem.UpdateFromDocument(requirement.Item, number, Now) ? 1 : 0;
                }
                else
                {
                    await _workItems.AddAsync(
                        WorkItem.FromDocument(project.Id, document.Id, document.WorkItemType, requirement.Item, number, Now), cancellationToken);
                    added++;
                }
            }

            foreach (var gone in existing.Values)
            {
                removed += gone.MarkRemovedIn(number) ? 1 : 0;
            }

            var version = document.AddVersion(file, new ImportCounts(added, changed, removed), read.Skipped, user.Id, Now,
                reader.UsesIdPattern ? pattern : null);
            if (isNew)
            {
                await _documents.AddAsync(document, cancellationToken);
            }

            await _documents.AddContentAsync(version.Id, file.Content, cancellationToken);

            // R9
            await _log.LogAsync(ActivityAction.RequirementDocumentUploaded, project.Id,
                $"{file.FileName} version {version.Number}: {added} added, {changed} changed, {removed} removed, {read.Skipped.Count} skipped",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Open project pages update themselves, like after a GitHub sync (US-018 R7)
            await _notifier.WorkItemsChangedAsync(project.Id, cancellationToken);
            return version;
        }

        // R4: an ID twice refuses the whole file, e.g. "REQ-012 appears twice (rows 4 and 9)"
        private static void RefuseDuplicates(RequirementReadResult read)
        {
            var duplicates = read.Items
                .GroupBy(r => r.Item.Reference.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g =>
                {
                    var positions = g.Select(r => r.Position.ToString()).ToList();
                    var times = positions.Count == 2 ? "twice" : $"{positions.Count} times";
                    var list = string.Join(", ", positions.Take(positions.Count - 1)) + " and " + positions[^1];
                    return $"{g.Key} appears {times} ({read.PositionName}s {list})";
                })
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new DomainException(string.Join("; ", duplicates));
            }
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    }
}
