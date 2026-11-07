using ComplianceTracker.Application.Reports;

namespace ComplianceTracker.UnitTests.Reports;

// Edge-case unit tests of US-129: which reports a project can make, and why not. Arrange / Act / Assert.
public class ProjectReportsTests
{
    [Fact]
    public void A_project_without_frameworks_can_make_no_report_and_says_why()
    {
        // Arrange / Act
        var reports = ProjectReports.For(frameworkCount: 0, followsCra: false);

        // Assert: R3, every report is still listed (R1)
        Assert.Equal([ProjectReports.Traceability, ProjectReports.TechnicalDocumentation], reports.Select(r => r.Key));
        Assert.All(reports, r => Assert.False(r.IsAvailable));
        Assert.Equal("Add a framework to the project to make a traceability report", reports[0].Unavailable);
    }

    [Fact]
    public void A_project_without_the_CRA_can_make_traceability_only()
    {
        // Arrange / Act
        var reports = ProjectReports.For(frameworkCount: 1, followsCra: false);

        // Assert
        Assert.True(reports.Single(r => r.Key == ProjectReports.Traceability).IsAvailable);
        Assert.Equal("Add the CRA to the project to make the technical documentation draft",
            reports.Single(r => r.Key == ProjectReports.TechnicalDocumentation).Unavailable);
    }

    [Fact]
    public void A_project_that_follows_the_CRA_can_make_both()
    {
        // Arrange / Act
        var reports = ProjectReports.For(frameworkCount: 2, followsCra: true);

        // Assert
        Assert.All(reports, r => Assert.True(r.IsAvailable));
    }

    [Fact]
    public void The_CRA_without_any_framework_counted_is_not_enough()
    {
        // Arrange / Act: an inconsistent input must not offer a report that cannot be made
        var reports = ProjectReports.For(frameworkCount: 0, followsCra: true);

        // Assert
        Assert.All(reports, r => Assert.False(r.IsAvailable));
    }

    [Fact]
    public void No_compliance_data_means_no_reports()
    {
        // Arrange / Act
        var reports = ProjectReports.For(compliance: null);

        // Assert
        Assert.All(reports, r => Assert.False(r.IsAvailable));
    }

    [Fact]
    public void Every_report_says_who_it_is_for_and_its_format()
    {
        // Arrange / Act
        var reports = ProjectReports.For(frameworkCount: 1, followsCra: true);

        // Assert: R1
        Assert.All(reports, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.ForWhom));
            Assert.False(string.IsNullOrWhiteSpace(r.Contents));
            Assert.False(string.IsNullOrWhiteSpace(r.Formats));
        });
    }
}
