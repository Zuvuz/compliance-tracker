using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // Storage for the organisation's documents (US-136, implemented in Infrastructure). Add only registers; IUnitOfWork saves.
    public interface IOrganisationDocumentRepository
    {
        // With links and earlier versions; removed ones too when asked for
        Task<IReadOnlyList<OrganisationDocument>> ListAsync(Guid organizationId, bool withRemoved = false, CancellationToken cancellationToken = default);

        Task<OrganisationDocument?> GetAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken = default);

        Task AddAsync(OrganisationDocument document, byte[] content, CancellationToken cancellationToken = default);

        // R3: the new current file; the old content stays under its version
        Task AddContentAsync(Guid documentId, string sha256, byte[] content, CancellationToken cancellationToken = default);

        Task<byte[]?> GetContentAsync(Guid documentId, string sha256, CancellationToken cancellationToken = default);

        // The projects' evidence made from the organisation's documents
        Task<IReadOnlyList<Evidence>> ListProjectEvidenceAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<EvidenceLink>> ListProjectLinksAsync(Guid organizationId, CancellationToken cancellationToken = default);
    }

    // A requirement a document can be linked to, e.g. ("ISO/IEC 27001:2022", "A.5.15", "Access control")
    public record LinkableRequirement(Guid FrameworkId, string Framework, string Code, string Title);

    // What the Documents page shows
    public record OrganisationDocumentsView(
        IReadOnlyList<OrganisationDocument> Documents,
        IReadOnlyList<LinkableRequirement> Requirements,
        IReadOnlyDictionary<Guid, string> Members,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> ProjectsUsing,
        bool CanManage,
        DateOnly Today)
    {
        // US-136 R2: the AI's pending suggestions per document, best first
        // US-145 R6: the rejected suggestions per document, so a person can take one back
        public IReadOnlyDictionary<Guid, IReadOnlyList<Domain.Organizations.DocumentLinkSuggestion>> Rejected { get; init; } =
            new Dictionary<Guid, IReadOnlyList<Domain.Organizations.DocumentLinkSuggestion>>();

        public IReadOnlyDictionary<Guid, IReadOnlyList<DocumentSuggestionView>> Suggestions { get; init; } =
            new Dictionary<Guid, IReadOnlyList<DocumentSuggestionView>>();
    }

    // US-136: the organisation's policies, procedures and risk tables, kept once and shared with the projects as evidence.
    // Changes need ManageFrameworks (the organisation's own compliance material, like its frameworks).
    public class OrganisationDocumentService
    {
        private readonly IOrganisationDocumentRepository _documents;
        private readonly IEvidenceRepository _evidence;
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public OrganisationDocumentService(
            IOrganisationDocumentRepository documents, IEvidenceRepository evidence, IProjectRepository projects,
            IFrameworkRepository frameworks, CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _documents = documents;
            _evidence = evidence;
            _projects = projects;
            _frameworks = frameworks;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        public async Task<OrganisationDocument> UploadAsync(
            OrganisationDocumentKind kind, string? title, Guid ownerId, string? version, string fileName, byte[] content, DateOnly? reviewDueOn,
            CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            EnsureMember(organization, ownerId);
            var file = EvidenceFile.Create(fileName, content);

            var document = OrganisationDocument.Create(organization.Id, kind, title, ownerId, version, file, reviewDueOn, user.Id, Now);
            await _documents.AddAsync(document, file.Content, cancellationToken);
            await _log.LogAsync(ActivityAction.OrganisationDocumentUploaded, organization.Id, $"{document.Kind}: {document.Title} {document.Version}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return document;
        }

        // R3
        public async Task UploadNewVersionAsync(Guid documentId, string? version, string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, user, document) = await OpenAsync(documentId, cancellationToken);
            var file = EvidenceFile.Create(fileName, content);
            var old = document.Version;

            document.UploadNewVersion(file, version, user.Id, Now);
            await _documents.AddContentAsync(document.Id, file.Sha256, file.Content, cancellationToken);
            await _log.LogAsync(ActivityAction.OrganisationDocumentUploaded, organization.Id, $"{document.Title}: {old} → {document.Version}", cancellationToken);
            await SyncAsync(organization.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R4
        public async Task MarkReviewedAsync(Guid documentId, DateOnly nextReviewOn, CancellationToken cancellationToken = default)
        {
            var (organization, user, document) = await OpenAsync(documentId, cancellationToken);
            document.MarkReviewed(nextReviewOn, user.Id, Now);
            await _log.LogAsync(ActivityAction.OrganisationDocumentReviewed, organization.Id, $"{document.Title}: next review {nextReviewOn:dd-MM-yyyy}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R1: by framework and code, so the link holds in every version of the framework that has the code
        public async Task LinkAsync(Guid documentId, Guid frameworkId, string? requirementCode, CancellationToken cancellationToken = default)
        {
            var (organization, user, document) = await OpenAsync(documentId, cancellationToken);
            var requirement = (await LinkableAsync(organization.Id, cancellationToken))
                .FirstOrDefault(r => r.FrameworkId == frameworkId && string.Equals(r.Code, (requirementCode ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException("The requirement is not in a framework the organisation uses");

            document.Link(frameworkId, requirement.Code, user.Id, Now);
            await _log.LogAsync(ActivityAction.OrganisationDocumentLinked, organization.Id, $"{document.Title} ↔ {requirement.Code} ({requirement.Framework})", cancellationToken);
            await SyncAsync(organization.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task UnlinkAsync(Guid documentId, Guid linkId, CancellationToken cancellationToken = default)
        {
            var (organization, user, document) = await OpenAsync(documentId, cancellationToken);
            var link = document.Unlink(linkId, user.Id, Now);
            await _log.LogAsync(ActivityAction.OrganisationDocumentUnlinked, organization.Id, $"{document.Title} ↔ {link.RequirementCode}", cancellationToken);
            await SyncAsync(organization.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: the page has shown which requirements lose the evidence and asked for confirmation
        public async Task RemoveAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var (organization, user, document) = await OpenAsync(documentId, cancellationToken);
            var codes = string.Join(", ", document.ActiveLinks.Select(l => l.RequirementCode));
            document.Remove(user.Id, Now);
            await _log.LogAsync(ActivityAction.OrganisationDocumentRemoved, organization.Id,
                codes.Length > 0 ? $"{document.Title} (was evidence for {codes})" : document.Title, cancellationToken);
            await SyncAsync(organization.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<OrganisationDocumentsView?> ViewAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return null;
            }

            var documents = await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken);
            var evidence = await _documents.ListProjectEvidenceAsync(organization.Id, cancellationToken);
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).ToDictionary(p => p.Id, p => p.Name);
            var using_ = documents.ToDictionary(
                d => d.Id,
                d => (IReadOnlyList<string>)evidence.Where(e => e.OrganisationDocumentId == d.Id && e.IsActive)
                    .Select(e => projects.GetValueOrDefault(e.ProjectId, "?")).Distinct().OrderBy(n => n).ToList());

            return new OrganisationDocumentsView(
                documents,
                await LinkableAsync(organization.Id, cancellationToken),
                organization.Users.ToDictionary(u => u.Id, u => u.Name),
                using_,
                await _member.CanAsync(Permission.ManageFrameworks, cancellationToken),
                _clock.Today());
        }

        // R4: "Needs attention" for the organisation and its projects
        public async Task<IReadOnlyList<OrganisationDocument>> DueForReviewAsync(CancellationToken cancellationToken = default)
            => await _member.FindOrganizationAsync(cancellationToken) is { } organization
                ? (await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken)).Where(d => d.IsReviewDue(_clock.Today())).ToList()
                : [];

        // The file for a member of the organisation; the project's evidence downloads it too
        public async Task<(string FileName, string ContentType, byte[] Content)?> ContentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _documents.GetAsync(organization.Id, documentId, cancellationToken) is not { } document)
            {
                return null;
            }

            return await _documents.GetContentAsync(document.Id, document.Sha256, cancellationToken) is { } content
                ? (document.FileName, document.ContentType, content)
                : null;
        }

        // R1, R3, R5: makes each project's evidence match the documents: a document linked to a requirement the project
        // follows is evidence for it; a new title or file is shown; an unlinked or removed document is taken off. Called by
        // every change here and when a project starts following a framework version. Does not save.
        public async Task SyncAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            var documents = (await _documents.ListAsync(organizationId, withRemoved: true, cancellationToken: cancellationToken)).ToDictionary(d => d.Id);
            var evidence = await _documents.ListProjectEvidenceAsync(organizationId, cancellationToken);
            var links = await _documents.ListProjectLinksAsync(organizationId, cancellationToken);
            var projects = (await _projects.ListAsync(organizationId, cancellationToken)).Where(p => !p.IsClosed).ToList();
            var versions = await RequirementsByVersionAsync(organizationId, projects, cancellationToken);
            var now = Now;

            foreach (var project in projects)
            {
                // What the project should have: (document, requirement id)
                var wanted = new HashSet<(Guid Document, Guid Requirement)>();
                foreach (var followed in project.Frameworks)
                {
                    if (!versions.TryGetValue(followed.FrameworkVersionId, out var requirements))
                    {
                        continue;
                    }

                    foreach (var document in documents.Values.Where(d => d.IsActive))
                    {
                        foreach (var link in document.ActiveLinks.Where(l => l.FrameworkId == followed.FrameworkId))
                        {
                            if (requirements.TryGetValue(link.RequirementCode, out var requirementId))
                            {
                                wanted.Add((document.Id, requirementId));
                            }
                        }
                    }
                }

                foreach (var documentId in wanted.Select(w => w.Document).Distinct())
                {
                    var document = documents[documentId];
                    var copy = evidence.FirstOrDefault(e => e.ProjectId == project.Id && e.OrganisationDocumentId == documentId && e.IsActive);
                    if (copy is null)
                    {
                        copy = Evidence.FromOrganisation(project.Id, document, now);
                        await _evidence.AddAsync(copy, null, cancellationToken);
                        evidence = [.. evidence, copy];
                    }
                    else
                    {
                        copy.Follow(document);
                    }

                    foreach (var (_, requirementId) in wanted.Where(w => w.Document == documentId))
                    {
                        if (!links.Any(l => l.EvidenceId == copy.Id && l.RequirementId == requirementId && l.IsActive))
                        {
                            var added = EvidenceLink.Create(project.Id, copy.Id, requirementId, document.AddedById, null, now);
                            await _evidence.AddLinkAsync(added, cancellationToken);
                            links = [.. links, added];
                        }
                    }
                }

                // Taken off: links no longer wanted, and copies with no link left
                foreach (var copy in evidence.Where(e => e.ProjectId == project.Id && e.IsFromOrganisation && e.IsActive).ToList())
                {
                    var keep = wanted.Where(w => w.Document == copy.OrganisationDocumentId).Select(w => w.Requirement).ToHashSet();
                    var removedBy = documents.TryGetValue(copy.OrganisationDocumentId!.Value, out var source) ? source.RemovedById ?? source.AddedById : copy.AddedById;
                    foreach (var link in links.Where(l => l.EvidenceId == copy.Id && l.IsActive && !keep.Contains(l.RequirementId)))
                    {
                        link.Remove(removedBy, now);
                    }

                    if (keep.Count == 0)
                    {
                        copy.Remove(removedBy, now);
                    }
                }
            }
        }

        // For each framework version the projects follow: requirement code → id
        private async Task<Dictionary<Guid, Dictionary<string, Guid>>> RequirementsByVersionAsync(
            Guid organizationId, IReadOnlyList<Project> projects, CancellationToken cancellationToken)
        {
            var frameworkIds = projects.SelectMany(p => p.Frameworks).Select(f => f.FrameworkId).Distinct().ToList();
            var frameworks = frameworkIds.Count == 0 ? [] : await _frameworks.ListAvailableWithRequirementsAsync(organizationId, frameworkIds, cancellationToken);
            return frameworks.SelectMany(f => f.Versions)
                .ToDictionary(v => v.Id, v => v.Requirements.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase));
        }

        // The requirements of the newest active version of every framework the organisation can use
        private async Task<IReadOnlyList<LinkableRequirement>> LinkableAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            var available = await _frameworks.ListAsync(organizationId, cancellationToken);
            var frameworks = available.Count == 0 ? [] : await _frameworks.ListAvailableWithRequirementsAsync(organizationId, available.Select(f => f.Id).ToList(), cancellationToken);
            var today = _clock.Today();
            return frameworks
                .Select(f => (Framework: f, Version: f.Versions.Where(v => v.IsActive).OrderByDescending(v => v.Number).FirstOrDefault()))
                .Where(x => x.Version is not null)
                .SelectMany(x => x.Version!.Requirements.Select(r => new LinkableRequirement(x.Framework.Id, x.Framework.Name, r.Code, r.Title)))
                .OrderBy(r => r.Framework).ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<(Organization Organization, User User, OrganisationDocument Document)> OpenAsync(Guid documentId, CancellationToken cancellationToken)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageFrameworks, cancellationToken);
            var document = await _documents.GetAsync(organization.Id, documentId, cancellationToken)
                ?? throw new DomainException("Document not found");
            return (organization, user, document);
        }

        private static void EnsureMember(Organization organization, Guid userId)
        {
            if (organization.Members.All(u => u.Id != userId))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }
        }
    }
}
