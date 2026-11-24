using ComplianceTracker.Application.Reports;

namespace ComplianceTracker.Application.Isms
{
    // US-214: the inventory of information assets as a workbook (ISO/IEC 27001 A.5.9), the same report model as the other
    // reports. R3: an asset without an owner is marked; R5: the personal data column is the list for GDPR art. 30. Pure.
    public static class AssetInventoryReport
    {
        public const string Title = "Inventory of information assets";
        public const string Kind = "assets";
        public const string NoOwner = "No owner";

        public static ReportDocument Build(AssetsView view, string organisation, DateTime now)
            => new($"{Title} – {organisation}",
            [
                ("Assets", view.Assets.Count.ToString()),
                ("Without an owner", view.Assets.Count(a => !a.HasOwner).ToString()),
                ("With personal data", view.Assets.Count(a => a.Asset.HoldsPersonalData).ToString()),
                ("Classification scheme", string.Join(" < ", view.Scheme.Levels)),
                ("Made", $"{now.ToLocalTime():dd-MM-yyyy HH:mm}")
            ],
            [
                new ReportSheet("Assets", ["Asset", "Kind", "Owner", "Classification", "Personal data", "Projects", "Suppliers", "Risks", "Description"], 2,
                    view.Assets.Select(a => new ReportRow(
                    [
                        a.Asset.Name,
                        a.Asset.Kind.ToString(),
                        a.OwnerName ?? NoOwner,
                        a.Asset.Classification,
                        a.Asset.HoldsPersonalData ? "Yes" : "No",
                        string.Join("; ", a.Projects),
                        string.Join("; ", a.Suppliers),
                        string.Join("; ", a.Risks.Select(r => r.Title)),
                        a.Asset.Description ?? ""
                    ], a.HasOwner ? ReportStatus.Normal : ReportStatus.Bad)).ToList())
            ]);
    }
}
