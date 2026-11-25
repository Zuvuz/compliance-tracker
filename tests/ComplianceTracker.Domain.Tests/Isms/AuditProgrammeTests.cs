using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-211: the internal audit programme, its planned audits, the control owners and the coverage. Every test follows
// Arrange / Act / Assert.
public class AuditProgrammeTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateOnly Start = new(2027, 1, 1);

    private static AuditProgramme Programme(int years = 3) => AuditProgramme.Create(Organisation, Start, years);

    private static PlannedAudit Plan(AuditProgramme programme, string[] clauses, string[] controls, Guid? auditor = null, string? reason = null)
        => programme.Plan(new DateOnly(2027, 3, 1), clauses, controls, [], auditor ?? Anna, null, auditor == Bo ? "Bo" : "Anna", reason);

    // ---------------------------------------------------------- R1

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(null)]
    public void A_programme_lasts_one_to_five_years(int? years)
        => Assert.Throws<DomainException>(() => AuditProgramme.Create(Organisation, Start, years));

    [Fact]
    public void A_three_year_programme_ends_the_day_before_its_third_anniversary()
        => Assert.Equal(new DateOnly(2029, 12, 31), Programme().EndsOn);

    [Fact]
    public void A_shorter_cycle_cannot_leave_a_planned_audit_outside()
    {
        var programme = Programme();
        programme.Plan(new DateOnly(2028, 6, 1), ["9"], [], [], Anna, null, "Anna", null);

        Assert.Throws<DomainException>(() => programme.Change(Start, 1));
    }

    // ---------------------------------------------------------- R2

    [Fact]
    public void A_planned_audit_covers_at_least_one_clause_or_control()
    {
        var error = Assert.Throws<DomainException>(() => Plan(Programme(), [], []));

        Assert.Equal("Choose at least one clause or control to audit", error.Message);
    }

    [Fact]
    public void A_planned_audit_must_be_inside_the_cycle()
        => Assert.Throws<DomainException>(() => Programme().Plan(new DateOnly(2026, 12, 31), ["9"], [], [], Anna, null, "Anna", null));

    [Theory]
    [InlineData("3")]
    [InlineData("11")]
    public void A_clause_is_4_to_10(string clause)
        => Assert.Throws<DomainException>(() => Plan(Programme(), [clause], []));

    [Theory]
    [InlineData("a.8.13", "A.8.13")]
    [InlineData(" A 5.1 ", "A.5.1")]
    [InlineData("A.8", "A.8")]
    [InlineData("8.13", null)]
    [InlineData("Backup", null)]
    public void A_control_is_written_as_in_annex_a(string text, string? code)
        => Assert.Equal(code, AuditProgramme.ControlCode(text));

    [Fact]
    public void An_audit_needs_an_auditor()
        => Assert.Throws<DomainException>(() => Programme().Plan(new DateOnly(2027, 3, 1), ["9"], [], [], null, " ", "", null));

    // ---------------------------------------------------------- R3

    [Fact]
    public void The_coverage_shows_what_is_not_planned()
    {
        var programme = Programme();
        Plan(programme, ["9", "10"], ["A.8.13"]);

        var coverage = programme.Coverage(["A.5.1", "A.8.13"]).ToDictionary(c => c.Item, c => c.State);

        Assert.Equal(CoverageState.Planned, coverage["9"]);
        Assert.Equal(CoverageState.Missing, coverage["4"]);
        Assert.Equal(CoverageState.Planned, coverage["A.8.13"]);
        Assert.Equal(CoverageState.Missing, coverage["A.5.1"]);
    }

    [Fact]
    public void A_done_audit_makes_its_items_done_and_controls_come_in_the_standard_order()
    {
        var programme = Programme();
        Plan(programme, ["9"], []).MarkDone(Guid.NewGuid());

        var coverage = programme.Coverage(["A.5.10", "A.5.2"]);

        Assert.Contains(("9", CoverageState.Done), coverage);
        Assert.Equal(["4", "5", "6", "7", "8", "9", "10", "A.5.2", "A.5.10"], coverage.Select(c => c.Item));
    }

    // ---------------------------------------------------------- R4

    [Fact]
    public void An_auditor_who_owns_a_control_is_warned()
    {
        var programme = Programme();
        programme.SetOwner("A.8.13", Bo);

        var error = Assert.Throws<DomainException>(() => Plan(programme, [], ["A.8.13"], Bo));

        Assert.StartsWith("Bo owns this control: A.8.13", error.Message);
        Assert.Empty(programme.Audits);
    }

    [Fact]
    public void The_plan_is_kept_with_a_reason()
    {
        var programme = Programme();
        programme.SetOwner("A.8.13", Bo);

        var audit = Plan(programme, [], ["a.8.13"], Bo, "the only one who knows the backup system");

        Assert.Equal("the only one who knows the backup system", audit.ImpartialityReason);
    }

    [Fact]
    public void Removing_an_owner_leaves_the_control_without_one()
    {
        var programme = Programme();
        programme.SetOwner("A.8.13", Bo);

        programme.SetOwner("A.8.13", null);

        Assert.Null(programme.OwnerOf("A.8.13"));
    }

    // ---------------------------------------------------------- R5, R6

    [Fact]
    public void A_planned_audit_past_its_date_is_overdue_until_it_is_done()
    {
        var audit = Plan(Programme(), ["9"], []);

        Assert.True(audit.IsOverdue(new DateOnly(2027, 3, 2)));
        audit.MarkDone(Guid.NewGuid());
        Assert.False(audit.IsOverdue(new DateOnly(2027, 3, 2)));
        Assert.Throws<DomainException>(() => audit.MarkDone(Guid.NewGuid()));
    }

    [Fact]
    public void A_done_audit_cannot_be_removed()
    {
        var programme = Programme();
        var audit = Plan(programme, ["9"], []);
        audit.MarkDone(Guid.NewGuid());

        Assert.Throws<DomainException>(() => programme.Remove(audit.Id));
    }
}
