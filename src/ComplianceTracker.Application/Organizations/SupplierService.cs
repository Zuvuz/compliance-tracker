using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-191: storage for the suppliers and their evidence files (implemented in Infrastructure)
    public interface ISupplierRepository
    {
        // The organisation's suppliers with their evidence, removed ones too
        Task<IReadOnlyList<Supplier>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<Supplier?> GetAsync(Guid organizationId, Guid supplierId, CancellationToken cancellationToken = default);

        Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);

        Task AddFileAsync(Guid evidenceId, byte[] content, CancellationToken cancellationToken = default);

        Task<byte[]?> GetFileAsync(Guid evidenceId, CancellationToken cancellationToken = default);
    }

    // US-207 R1: a supplier's company details as typed on the page
    public sealed record CompanyDetails(string? RegistrationNumber, string? Address, string? Country, string? Website, string? SecurityEmail);

    // US-207 R2: a contact person as typed on the page
    public sealed record ContactDetails(string? Name, string? Role, string? Email, string? Phone, bool Primary, bool SecurityContact);

    // One supplier on the page: its status (R1, R3) and the SBOM components it delivers (R4)
    public sealed record SupplierView(Supplier Supplier, SupplierStatus Status, int Components);

    public class SupplierService
    {
        private readonly ISupplierRepository _suppliers;
        private readonly IProjectRepository _projects;
        private readonly ISbomRepository _sboms;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public SupplierService(ISupplierRepository suppliers, IProjectRepository projects, ISbomRepository sboms, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _suppliers = suppliers;
            _projects = projects;
            _sboms = sboms;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // R1, R3, R4: the active suppliers, the most critical first; any member may read them
        public async Task<IReadOnlyList<SupplierView>> ListAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var suppliers = (await _suppliers.ListAsync(organization.Id, cancellationToken)).Where(s => s.IsActive).ToList();
            var components = new List<Domain.Scanning.SbomComponent>();
            if (suppliers.Count > 0)
            {
                foreach (var project in (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsClosed))
                {
                    components.AddRange(SbomService.Latest(await _sboms.ListAsync(project.Id, cancellationToken)).SelectMany(s => s.Components));
                }
            }

            return suppliers
                .Select(s => new SupplierView(s, SupplierStatus.Of(s, Today), Delivered(s.Name, components)))
                .OrderBy(v => v.Supplier.Criticality)
                .ThenBy(v => v.Status.State == SupplierState.Ok)
                .ThenBy(v => v.Supplier.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // R4: the components whose supplier field names the supplier, e.g. "Acme Libraries Inc." for "Acme Libraries"
        public static int Delivered(string supplier, IEnumerable<Domain.Scanning.SbomComponent> components)
            => components.Count(c => c.Supplier is { } named && named.Contains(supplier, StringComparison.OrdinalIgnoreCase));

        // US-207 R1: the company's details can be given right away
        public async Task<Supplier> AddAsync(string? name, string? delivers, SupplierCriticality? criticality, string? contact,
            IReadOnlyList<string>? rules, CancellationToken cancellationToken = default, CompanyDetails? company = null)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var supplier = Supplier.Create(organization.Id, name, delivers, criticality, contact, rules, user.Id, Now);
            if (company is not null)
            {
                supplier.ChangeCompany(company.RegistrationNumber, company.Address, company.Country, company.Website, company.SecurityEmail);
            }

            if ((await _suppliers.ListAsync(organization.Id, cancellationToken)).Any(s => s.IsActive && string.Equals(s.Name, supplier.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException($"{supplier.Name} is already a supplier");
            }

            await _suppliers.AddAsync(supplier, cancellationToken);
            await _log.LogAsync(ActivityAction.SupplierAdded, supplier.Id, $"{supplier.Name} ({supplier.Criticality})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return supplier;
        }

        // R3: a file with its end date; the file is checked like any evidence file
        public async Task AddEvidenceAsync(Guid supplierId, SupplierEvidenceKind? kind, string? title, DateOnly? validUntil, string fileName,
            byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var supplier = await _suppliers.GetAsync(organization.Id, supplierId, cancellationToken) ?? throw new DomainException("Supplier not found");
            var file = EvidenceFile.Create(fileName, content);
            var evidence = supplier.AddEvidence(kind, title, validUntil, file.FileName, file.ContentType, file.Content.Length, file.Sha256, user.Id, Now);
            await _suppliers.AddFileAsync(evidence.Id, file.Content, cancellationToken);
            await _log.LogAsync(ActivityAction.SupplierEvidenceAdded, supplier.Id,
                $"{supplier.Name}: {evidence.Title}{(evidence.ValidUntil is { } until ? $", valid until {until:dd-MM-yyyy}" : "")}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-207 R1, R6: the log names what changed, never the values
        public async Task ChangeCompanyAsync(Guid supplierId, CompanyDetails company, CancellationToken cancellationToken = default)
        {
            var supplier = await ManagedAsync(supplierId, cancellationToken);
            var changed = supplier.ChangeCompany(company.RegistrationNumber, company.Address, company.Country, company.Website, company.SecurityEmail);
            if (changed.Count == 0)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.SupplierDetailsChanged, supplier.Id, $"{supplier.Name}: {string.Join(", ", changed)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // US-207 R2, R3, R6
        public async Task AddContactAsync(Guid supplierId, ContactDetails contact, CancellationToken cancellationToken = default)
        {
            var supplier = await ManagedAsync(supplierId, cancellationToken);
            var added = supplier.AddContact(contact.Name, contact.Role, contact.Email, contact.Phone, contact.Primary, contact.SecurityContact);
            await _log.LogAsync(ActivityAction.SupplierContactAdded, supplier.Id, $"{supplier.Name}: {Describe(added)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ChangeContactAsync(Guid supplierId, Guid contactId, ContactDetails contact, CancellationToken cancellationToken = default)
        {
            var supplier = await ManagedAsync(supplierId, cancellationToken);
            var changed = supplier.ChangeContact(contactId, contact.Name, contact.Role, contact.Email, contact.Phone, contact.Primary, contact.SecurityContact);
            await _log.LogAsync(ActivityAction.SupplierContactChanged, supplier.Id, $"{supplier.Name}: {Describe(changed)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveContactAsync(Guid supplierId, Guid contactId, CancellationToken cancellationToken = default)
        {
            var supplier = await ManagedAsync(supplierId, cancellationToken);
            var removed = supplier.RemoveContact(contactId);
            await _log.LogAsync(ActivityAction.SupplierContactRemoved, supplier.Id, $"{supplier.Name}: {removed.LogName}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R6: e.g. "Mette Lund (Security contact), primary, security contact"; no e-mail or phone
        public static string Describe(SupplierContact contact)
            => contact.LogName + (contact.IsPrimary ? ", primary" : "") + (contact.IsSecurityContact ? ", security contact" : "");

        private async Task<Supplier> ManagedAsync(Guid supplierId, CancellationToken cancellationToken)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            return await _suppliers.GetAsync(organization.Id, supplierId, cancellationToken) ?? throw new DomainException("Supplier not found");
        }

        public async Task RemoveAsync(Guid supplierId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var supplier = await _suppliers.GetAsync(organization.Id, supplierId, cancellationToken) ?? throw new DomainException("Supplier not found");
            supplier.Remove(Now);
            await _log.LogAsync(ActivityAction.SupplierRemoved, supplier.Id, supplier.Name, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Any member may read an evidence file of their own organisation's supplier
        public async Task<(string FileName, string ContentType, byte[] Content)?> FileAsync(Guid supplierId, Guid evidenceId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            if (await _suppliers.GetAsync(organization.Id, supplierId, cancellationToken) is not { } supplier
                || supplier.Evidence.FirstOrDefault(e => e.Id == evidenceId) is not { } evidence
                || await _suppliers.GetFileAsync(evidence.Id, cancellationToken) is not { } content)
            {
                return null;
            }

            return (evidence.FileName, evidence.ContentType, content);
        }
    }
}
