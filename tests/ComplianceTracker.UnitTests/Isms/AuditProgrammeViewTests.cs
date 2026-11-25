using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.UnitTests.Isms;

// US-211: the "Missing" line and how planned controls are typed. Every test follows Arrange / Act / Assert.
public class AuditProgrammeViewTests
{
    private static AuditProgrammeView View(params (string, CoverageState)[] coverage)
        => new(null, coverage, [], new Dictionary<Guid, string>(), [], [], true, new DateOnly(2027, 1, 1));

    [Fact]
    public void The_missing_line_names_the_clauses_and_counts_the_controls()
    {
        var view = View(("8", CoverageState.Planned), ("9", CoverageState.Missing), ("10", CoverageState.Missing),
            ("A.5.1", CoverageState.Missing), ("A.5.2", CoverageState.Missing), ("A.8.13", CoverageState.Done));

        Assert.Equal("Missing: 9, 10 and 2 controls", view.MissingText);
    }

    [Fact]
    public void One_missing_control_is_singular()
        => Assert.Equal("Missing: 1 control", View(("A.5.1", CoverageState.Missing)).MissingText);

    [Fact]
    public void Nothing_missing_has_no_line()
        => Assert.Null(View(("9", CoverageState.Done)).MissingText);

    [Fact]
    public void Controls_are_split_on_commas_semicolons_and_lines()
        => Assert.Equal(["A.5.1", "A.8.13", "A 5.2"], AuditProgrammeService.Split("A.5.1, A.8.13;\nA 5.2"));
}
