using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Reports;

namespace ComplianceTracker.UnitTests.Reports;

// US-198: edge cases of "Since last" in an audit package, which compares with the reports of the last package.
// Arrange / Act / Assert.
public class PackageSinceLastTests
{
    private const string Cra = "CRA – Regulation (EU) 2024/2847";
    private const string AiAct = "AI Act – Regulation (EU) 2024/1689";
    private static readonly DateTime October = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static LastManagementReport Last(params (string Name, int Percent)[] figures)
        => new(October, figures.ToDictionary(f => f.Name, f => f.Percent));

    [Fact]
    public void The_first_package_says_it_has_nothing_to_compare_with()
        => Assert.Equal("First package: nothing to compare with", ManagementPriorities.SinceLast(Cra, 60, null, inPackage: true));

    [Fact]
    public void Outside_a_package_the_old_text_stays()
        => Assert.Equal("No locked report to compare with", ManagementPriorities.SinceLast(Cra, 60, null));

    [Fact]
    public void The_second_package_shows_the_change_since_the_first()
        => Assert.Equal("↑ +15 since 01-10-2026", ManagementPriorities.SinceLast(Cra, 75, Last((Cra, 60)), inPackage: true));

    [Fact]
    public void An_unchanged_figure_says_so()
        => Assert.Equal("= unchanged since 01-10-2026", ManagementPriorities.SinceLast(Cra, 60, Last((Cra, 60)), inPackage: true));

    [Fact]
    public void A_framework_added_since_the_last_package_is_new()
        => Assert.Equal("New", ManagementPriorities.SinceLast(AiAct, 10, Last((Cra, 60)), inPackage: true));

    [Fact]
    public void A_framework_no_longer_followed_is_listed_by_name()
        => Assert.Equal([AiAct], ManagementPriorities.NoLongerFollowedSince(Last((Cra, 60), (AiAct, 30)), [Cra]));

    [Fact]
    public void Nothing_is_listed_as_no_longer_followed_without_an_earlier_package()
        => Assert.Empty(ManagementPriorities.NoLongerFollowedSince(null, [Cra]));

    [Fact]
    public void A_package_compares_with_a_report_that_shares_a_regulation()
    {
        var figures = new Dictionary<string, int> { [Cra] = 60 };

        Assert.True(ManagementPriorities.SharesRegulations(figures, [Cra, AiAct]));
        Assert.False(ManagementPriorities.SameRegulations(figures, [Cra, AiAct]));
    }

    [Fact]
    public void A_report_with_none_of_the_regulations_is_not_compared_with()
        => Assert.False(ManagementPriorities.SharesRegulations(new Dictionary<string, int> { [AiAct] = 30 }, [Cra]));

    [Fact]
    public void The_status_sheet_of_a_package_lists_a_framework_no_longer_followed()
    {
        var facts = new ManagementFacts(0, 0, 0, null) { Last = Last((Cra, 60), (AiAct, 30)), InPackage = true };

        var status = ManagementPriorities.Sheets([], facts)[0];

        // No framework is followed now, so both are listed, by their short names
        Assert.Equal(["AI Act", "CRA"], status.Rows.Where(r => r.Cells.Contains("No longer followed")).Select(r => r.Cells[0]).ToList());
    }

    [Fact]
    public void A_report_locked_with_a_package_knows_the_package()
    {
        var package = Guid.NewGuid();

        var locked = ReportSnapshot.Lock(Guid.NewGuid(), Guid.NewGuid(), "risk-assessment", "Risk assessment", "risk.pdf", "application/pdf",
            [1, 2, 3], Guid.NewGuid(), null, October, packageId: package);

        Assert.Equal(package, locked.PackageId);
        Assert.Null(ReportSnapshot.Lock(Guid.NewGuid(), Guid.NewGuid(), "risk-assessment", "Risk assessment", "risk.pdf", "application/pdf",
            [1, 2, 3], Guid.NewGuid(), null, October).PackageId);
    }
}
