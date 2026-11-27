using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Web;

// US-225: which top bar item a page belongs to, and which Compliance page is open. Arrange / Act / Assert.
public class TopBarTests
{
    [Theory]
    [InlineData("Review", "Index", TopBar.Home)]
    [InlineData("Projects", "Details", TopBar.Projects)]
    [InlineData("Risks", "Details", TopBar.Projects)]
    [InlineData("Frameworks", "Index", TopBar.Compliance)]
    [InlineData("GapAnalysis", "Index", TopBar.Compliance)]
    [InlineData("Checklists", "Index", TopBar.Compliance)]
    [InlineData("Organisation", "Index", TopBar.Compliance)]
    [InlineData("Organisation", "Profile", TopBar.Organisation)]
    [InlineData("Users", "Index", TopBar.Organisation)]
    [InlineData("Settings", "Index", TopBar.Organisation)]
    [InlineData("Isms", "Scope", TopBar.Isms)]
    public void A_page_belongs_to_one_item(string controller, string action, string item)
        => Assert.Equal(item, TopBar.SectionOf(controller, action, false, null));

    [Fact]
    public void An_unknown_page_belongs_to_none()
        => Assert.Null(TopBar.SectionOf("Search", "Index", false, null));

    [Theory]
    [InlineData("risks", "Projects", TopBar.Isms)]
    [InlineData(null, "Risks", TopBar.Isms)]
    [InlineData("compliance", "Projects", TopBar.Compliance)]
    [InlineData("overview", "Projects", TopBar.Compliance)]
    public void The_organisation_area_is_compliance_except_its_risks(string? tab, string controller, string item)
        => Assert.Equal(item, TopBar.SectionOf(controller, "Details", true, tab));

    [Theory]
    [InlineData("compliance", "Requirements")]
    [InlineData("link", "Link")] // US-220
    [InlineData("evidence", "Evidence")]
    [InlineData("reports", "Reports")]
    [InlineData("frameworks", "Frameworks")]
    [InlineData("nis2-scope", "Nis2")]
    [InlineData("overview", "Nis2")]
    public void The_area_tab_marks_its_compliance_page(string tab, string page)
        => Assert.Equal(page, TopBar.ComplianceOpen("Projects", true, tab));

    [Theory]
    [InlineData("GapAnalysis", "GapAnalysis")]
    [InlineData("Checklists", "Checklists")]
    [InlineData("Frameworks", "Frameworks")]
    public void A_compliance_controller_marks_its_page(string controller, string page)
        => Assert.Equal(page, TopBar.ComplianceOpen(controller, false, null));
}
