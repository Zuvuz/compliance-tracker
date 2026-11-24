using ComplianceTracker.Application.Isms;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Web.Controllers;

namespace ComplianceTracker.UnitTests.Isms;

// US-208: the ISMS scope as a report, and the requirement rows of the interested-party form. Arrange / Act / Assert.
public class IsmsScopeReportTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

    private static IsmsScopeView View(IsmsScope? scope, params ScopeProjectLine[] projects)
        => new(scope, projects, [], new Dictionary<Guid, string> { [Anna] = "Anna" }, true, new DateOnly(2026, 11, 23));

    [Fact]
    public void A_scope_nobody_has_written_says_so_in_every_part()
    {
        var report = IsmsScopeReport.Build(View(null), "Demo ApS", "Anna", Now);

        Assert.Equal("ISMS scope and context – Demo ApS", report.Title);
        Assert.Contains(("Version", "Not approved yet"), report.Summary);
        Assert.All(report.Sheets[0].Rows, r => Assert.Equal("Not described", r.Cells[1]));
    }

    [Fact]
    public void An_approved_version_names_who_approved_it()
    {
        var scope = IsmsScope.Create(Guid.NewGuid(), Now);
        scope.Describe("Our SaaS products", null, null, null, null, Anna, null, Now);
        var webshop = Guid.NewGuid();
        scope.Include(webshop, Now);
        scope.Approve(Anna, Now.AddMinutes(1));

        var report = IsmsScopeReport.Build(View(scope, new ScopeProjectLine(webshop, "Webshop", false, ScopeState.Inside, null)), "Demo ApS", "Anna", Now);

        Assert.StartsWith("Version 1, approved ", report.Summary.Single(s => s.Label == "Version").Value);
        Assert.EndsWith(" by Anna", report.Summary.Single(s => s.Label == "Version").Value);
        Assert.Contains(("Owner", "Anna"), report.Summary);
    }

    [Fact]
    public void Projects_not_decided_are_shown_as_outside_and_marked()
    {
        var report = IsmsScopeReport.Build(View(null, new ScopeProjectLine(Guid.NewGuid(), "Webshop", false, ScopeState.NotDecided, null)), "Demo ApS", "Anna", Now);

        var row = Assert.Single(report.Sheets[1].Rows);
        Assert.Equal(("Not decided (outside)", ReportStatus.Warning), (row.Cells[1], row.Status));
    }

    [Fact]
    public void The_form_marks_only_the_ticked_rows_as_handled()
    {
        var lines = IsmsController.Lines(["ISO 27001", "", "Data in the EU"], [0, 7]);

        Assert.Equal([("ISO 27001", true), ("", false), ("Data in the EU", false)], lines);
    }

    [Fact]
    public void A_form_without_rows_gives_no_requirements()
    {
        Assert.Empty(IsmsController.Lines(null, null));
    }
}
