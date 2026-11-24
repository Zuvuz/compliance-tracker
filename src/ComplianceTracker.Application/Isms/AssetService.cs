using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-214: storage for the assets and the classification scheme (implemented in Infrastructure). Add only registers;
    // IUnitOfWork saves.
    public interface IAssetRepository
    {
        Task<ClassificationScheme?> GetSchemeAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddSchemeAsync(ClassificationScheme scheme, CancellationToken cancellationToken = default);

        // The organisation's assets, removed ones too
        Task<IReadOnlyList<Asset>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(Asset asset, CancellationToken cancellationToken = default);
    }

    // An asset as typed on the page
    public sealed record AssetInput(string? Name, AssetKind? Kind, Guid? OwnerId, string? Classification, bool HoldsPersonalData, string? Description,
        IReadOnlyList<Guid>? ProjectIds = null, IReadOnlyList<Guid>? SupplierIds = null, IReadOnlyList<Guid>? RiskIds = null);

    // The page's filters; null shows all
    public sealed record AssetFilter(AssetKind? Kind = null, string? Classification = null, bool PersonalData = false, bool NoOwner = false);

    // One asset on the page, with the names of what it is linked to (R3: OwnerName is null when the owner left)
    public sealed record AssetLine(Asset Asset, string? OwnerName, IReadOnlyList<string> Projects, IReadOnlyList<string> Suppliers,
        IReadOnlyList<(Guid Id, string Title)> Risks)
    {
        public bool HasOwner => OwnerName is not null;
    }

    // A risk the asset can be linked to, with its project
    public sealed record RiskOption(Guid Id, string Title, string Project);

    public sealed record AssetsView(
        IReadOnlyList<AssetLine> Assets,
        int Total,
        ClassificationScheme Scheme,
        AssetFilter Filter,
        IReadOnlyDictionary<Guid, string> Members,
        IReadOnlyList<(Guid Id, string Name)> Projects,
        IReadOnlyList<(Guid Id, string Name)> Suppliers,
        IReadOnlyList<RiskOption> Risks,
        bool CanChange);

    // R4: the assets a risk concerns and the highest classification among them
    public sealed record RiskAssets(IReadOnlyList<Asset> Assets, string? Highest);

    public class AssetService
    {
        private readonly IAssetRepository _assets;
        private readonly IProjectRepository _projects;
        private readonly IRiskRepository _risks;
        private readonly ISupplierRepository _suppliers;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly ISpreadsheetWriter _spreadsheets;
        private readonly IEnumerable<IRiskSheetReader> _readers;

        public AssetService(IAssetRepository assets, IProjectRepository projects, IRiskRepository risks, ISupplierRepository suppliers, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock, ISpreadsheetWriter spreadsheets, IEnumerable<IRiskSheetReader> readers)
        {
            _assets = assets;
            _projects = projects;
            _risks = risks;
            _suppliers = suppliers;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _spreadsheets = spreadsheets;
            _readers = readers;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // Every member may read the inventory
        public async Task<AssetsView> ViewAsync(AssetFilter? filter = null, CancellationToken cancellationToken = default)
        {
            filter ??= new AssetFilter();
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var scheme = await _assets.GetSchemeAsync(organization.Id, cancellationToken) ?? ClassificationScheme.Create(organization.Id);
            var projects = (await _projects.ListAsync(organization.Id, cancellationToken)).Where(p => !p.IsOrganisationArea).ToList();
            var suppliers = (await _suppliers.ListAsync(organization.Id, cancellationToken)).Where(s => s.IsActive).ToList();
            var risks = new List<RiskOption>();
            foreach (var project in projects.Where(p => !p.IsClosed))
            {
                risks.AddRange((await _risks.ListAsync(project.Id, cancellationToken)).Where(r => !r.IsWithdrawn).Select(r => new RiskOption(r.Id, r.Title, project.Name)));
            }

            var members = organization.Members.ToDictionary(u => u.Id, u => u.Name);
            var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);
            var supplierNames = suppliers.ToDictionary(s => s.Id, s => s.Name);
            var riskTitles = risks.ToDictionary(r => r.Id, r => r.Title);
            var all = (await _assets.ListAsync(organization.Id, cancellationToken)).Where(a => a.IsActive)
                .Select(a => new AssetLine(a, members.GetValueOrDefault(a.OwnerId),
                    a.ProjectIds.Where(projectNames.ContainsKey).Select(id => projectNames[id]).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                    a.SupplierIds.Where(supplierNames.ContainsKey).Select(id => supplierNames[id]).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                    a.RiskIds.Where(riskTitles.ContainsKey).Select(id => (id, riskTitles[id])).ToList()))
                .ToList();
            var shown = Filter(all, filter, scheme);
            return new AssetsView(shown, all.Count, scheme, filter, members,
                projects.Where(p => !p.IsClosed).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => (p.Id, p.Name)).ToList(),
                suppliers.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => (s.Id, s.Name)).ToList(),
                risks.OrderBy(r => r.Project, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                await _member.CanAsync(Permission.DecideCompliance, cancellationToken));
        }

        // The filters, and the order: R3 the assets without an owner first, then the highest classification, then the name
        public static IReadOnlyList<AssetLine> Filter(IEnumerable<AssetLine> lines, AssetFilter filter, ClassificationScheme scheme)
            => lines
                .Where(l => filter.Kind is null || l.Asset.Kind == filter.Kind)
                .Where(l => string.IsNullOrWhiteSpace(filter.Classification) || string.Equals(l.Asset.Classification, filter.Classification.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(l => !filter.PersonalData || l.Asset.HoldsPersonalData)
                .Where(l => !filter.NoOwner || !l.HasOwner)
                .OrderBy(l => l.HasOwner)
                .ThenByDescending(l => scheme.RankOf(l.Asset.Classification))
                .ThenBy(l => l.Asset.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // R1, R2, R6
        public async Task<Asset> AddAsync(AssetInput input, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var scheme = await SchemeAsync(organization.Id, cancellationToken);
            RequireMember(organization.Members, input.OwnerId);
            var asset = Asset.Create(organization.Id, input.Name, input.Kind, input.OwnerId, input.Classification, input.HoldsPersonalData,
                input.Description, scheme, Now);
            var existing = await _assets.ListAsync(organization.Id, cancellationToken);
            RequireUnique(existing, asset);
            await LinkAsync(organization.Id, asset, input, cancellationToken);
            await _assets.AddAsync(asset, cancellationToken);
            await _log.LogAsync(ActivityAction.AssetChanged, organization.Id,
                $"Added {asset.Name} ({asset.Kind}, {asset.Classification}{(asset.HoldsPersonalData ? ", personal data" : "")})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return asset;
        }

        // The log names what changed, not the values
        public async Task ChangeAsync(Guid assetId, AssetInput input, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var scheme = await SchemeAsync(organization.Id, cancellationToken);
            var existing = await _assets.ListAsync(organization.Id, cancellationToken);
            var asset = existing.FirstOrDefault(a => a.Id == assetId && a.IsActive) ?? throw new DomainException("Asset not found");
            if (input.OwnerId != asset.OwnerId)
            {
                RequireMember(organization.Members, input.OwnerId);
            }

            var changed = asset.Change(input.Name, input.Kind, input.OwnerId, input.Classification, input.HoldsPersonalData, input.Description, scheme).ToList();
            RequireUnique(existing, asset);
            if (await LinkAsync(organization.Id, asset, input, cancellationToken))
            {
                changed.Add("links");
            }

            if (changed.Count == 0)
            {
                return;
            }

            await _log.LogAsync(ActivityAction.AssetChanged, organization.Id, $"Changed {asset.Name}: {string.Join(", ", changed)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveAsync(Guid assetId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var asset = (await _assets.ListAsync(organization.Id, cancellationToken)).FirstOrDefault(a => a.Id == assetId && a.IsActive)
                        ?? throw new DomainException("Asset not found");
            asset.Remove(Now);
            await _log.LogAsync(ActivityAction.AssetChanged, organization.Id, $"Removed {asset.Name}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: the levels, lowest first; a level in use cannot be removed
        public async Task ChangeSchemeAsync(IReadOnlyList<string?>? levels, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var scheme = await SchemeAsync(organization.Id, cancellationToken);
            var inUse = (await _assets.ListAsync(organization.Id, cancellationToken)).Where(a => a.IsActive).Select(a => a.Classification);
            if (!scheme.Change(levels, inUse))
            {
                return;
            }

            await _log.LogAsync(ActivityAction.ClassificationSchemeChanged, organization.Id, string.Join(" < ", scheme.Levels), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Import from a spreadsheet (CSV or Excel). One bad row stops the import, and the errors name the rows.
        public async Task<int> ImportAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var reader = _readers.FirstOrDefault(r => r.CanRead(fileName, content))
                         ?? throw new DomainException("Choose a CSV or Excel (.xlsx) file");
            var sheet = reader.Read(content);
            var fileErrors = AssetImportRows.FileErrors(sheet);
            if (fileErrors.Count > 0)
            {
                throw new DomainException(fileErrors[0]);
            }

            var scheme = await SchemeAsync(organization.Id, cancellationToken);
            var existing = (await _assets.ListAsync(organization.Id, cancellationToken)).Where(a => a.IsActive).Select(a => a.Name);
            var rows = AssetImportRows.Read(sheet, organization.Users, scheme, existing);
            var bad = rows.Where(r => !r.IsValid).ToList();
            if (bad.Count > 0)
            {
                throw new DomainException("Nothing was imported. " + string.Join("; ", bad.Take(5).Select(r => $"Row {r.Number}: {string.Join(", ", r.Errors)}"))
                                          + (bad.Count > 5 ? $"; and {bad.Count - 5} more rows" : ""));
            }

            foreach (var row in rows)
            {
                await _assets.AddAsync(Asset.Create(organization.Id, row.Name, row.Kind, row.Owner!.Id, row.Classification, row.HoldsPersonalData,
                    row.Description, scheme, Now), cancellationToken);
            }

            await _log.LogAsync(ActivityAction.AssetChanged, organization.Id, $"Imported {rows.Count} assets from {fileName}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        // The inventory as an Excel workbook, with the same filters as the page
        public async Task<ReportFile> ExcelAsync(AssetFilter? filter = null, CancellationToken cancellationToken = default)
        {
            var view = await ViewAsync(filter, cancellationToken);
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var document = AssetInventoryReport.Build(view, organization.Name, Now);
            return new ReportFile(AssetInventoryReport.Kind, document.Title, $"assets-{Now:yyyy-MM-dd}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _spreadsheets.ToXlsx(document));
        }

        // R4: the assets a risk concerns, and the highest classification among them
        public async Task<RiskAssets> ForRiskAsync(Guid riskId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            var scheme = await _assets.GetSchemeAsync(organization.Id, cancellationToken) ?? ClassificationScheme.Create(organization.Id);
            var assets = (await _assets.ListAsync(organization.Id, cancellationToken)).Where(a => a.IsActive && a.RiskIds.Contains(riskId))
                .OrderByDescending(a => scheme.RankOf(a.Classification)).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
            return new RiskAssets(assets, scheme.Highest(assets.Select(a => a.Classification)));
        }

        // The names of the assets each supplier hosts or delivers, for the Suppliers page
        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> BySupplierAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            return (await _assets.ListAsync(organization.Id, cancellationToken)).Where(a => a.IsActive)
                .SelectMany(a => a.SupplierIds.Select(id => (Supplier: id, a.Name)))
                .GroupBy(p => p.Supplier)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Name).Order(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private async Task<ClassificationScheme> SchemeAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            if (await _assets.GetSchemeAsync(organizationId, cancellationToken) is { } scheme)
            {
                return scheme;
            }

            scheme = ClassificationScheme.Create(organizationId);
            await _assets.AddSchemeAsync(scheme, cancellationToken);
            return scheme;
        }

        // Only the organisation's own projects, suppliers and risks can be linked
        private async Task<bool> LinkAsync(Guid organizationId, Asset asset, AssetInput input, CancellationToken cancellationToken)
        {
            var projects = (await _projects.ListAsync(organizationId, cancellationToken)).Where(p => !p.IsOrganisationArea).ToList();
            var projectIds = projects.Select(p => p.Id).ToHashSet();
            var supplierIds = (await _suppliers.ListAsync(organizationId, cancellationToken)).Where(s => s.IsActive).Select(s => s.Id).ToHashSet();
            var riskIds = new HashSet<Guid>();
            if (input.RiskIds is { Count: > 0 })
            {
                foreach (var project in projects)
                {
                    riskIds.UnionWith((await _risks.ListAsync(project.Id, cancellationToken)).Select(r => r.Id));
                }
            }

            if ((input.ProjectIds ?? []).Any(id => !projectIds.Contains(id)) || (input.SupplierIds ?? []).Any(id => !supplierIds.Contains(id))
                || (input.RiskIds ?? []).Any(id => !riskIds.Contains(id)))
            {
                throw new DomainException("Choose projects, suppliers and risks from the lists");
            }

            return asset.Link(input.ProjectIds, input.SupplierIds, input.RiskIds);
        }

        private static void RequireMember(IEnumerable<User> members, Guid? ownerId)
        {
            if (ownerId is { } id && members.All(m => m.Id != id))
            {
                throw new DomainException("The owner must be a member of the organisation");
            }
        }

        // R6
        private static void RequireUnique(IEnumerable<Asset> existing, Asset asset)
        {
            if (existing.Any(a => a.IsActive && a.Id != asset.Id && string.Equals(a.Name, asset.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException($"{asset.Name} is already in the inventory");
            }
        }
    }
}
