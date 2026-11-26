using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// US-226: the Documents page's filter, policies by default, and which top bar item the moved pages belong to.
// Arrange / Act / Assert.
public class DocumentKindsTests
{
    [Theory]
    [InlineData(null, "Policy")]
    [InlineData("", "Policy")]
    [InlineData("nonsense", "Policy")]
    [InlineData("procedure", "Procedure")]
    [InlineData("ALL", "all")]
    public void An_unknown_or_missing_filter_is_policies(string? kind, string clean)
        => Assert.Equal(clean, DocumentKinds.Clean(kind));

    [Theory]
    [InlineData("all", OrganisationDocumentKind.RiskTable, true)]
    [InlineData("Policy", OrganisationDocumentKind.Policy, true)]
    [InlineData("Policy", OrganisationDocumentKind.Other, false)]
    public void The_filter_shows_its_kind(string filter, OrganisationDocumentKind kind, bool shows)
        => Assert.Equal(shows, DocumentKinds.Shows(filter, kind));

    [Theory]
    [InlineData("Documents")]
    [InlineData("Suppliers")]
    [InlineData("Isms")]
    public void The_documents_and_suppliers_are_isms(string controller)
        => Assert.Equal(TopBar.Isms, TopBar.SectionOf(controller, "Index", false, null));
}
