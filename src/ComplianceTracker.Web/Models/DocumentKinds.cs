using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Web.Models
{
    // US-226: the filter of the ISMS Documents page; Policies by default, "all" for every document
    public static class DocumentKinds
    {
        public const string All = "all";

        public static readonly IReadOnlyList<(string Value, string Title)> Filters =
        [
            (nameof(OrganisationDocumentKind.Policy), "Policies"),
            (nameof(OrganisationDocumentKind.Procedure), "Procedures"),
            (nameof(OrganisationDocumentKind.RiskTable), "Risk tables"),
            (nameof(OrganisationDocumentKind.Other), "Other"),
            (All, "All")
        ];

        // An unknown or missing value is Policies
        public static string Clean(string? kind)
            => Filters.FirstOrDefault(f => string.Equals(f.Value, kind, StringComparison.OrdinalIgnoreCase)).Value ?? nameof(OrganisationDocumentKind.Policy);

        public static bool Shows(string filter, OrganisationDocumentKind kind)
            => filter == All || string.Equals(filter, kind.ToString(), StringComparison.Ordinal);
    }
}
