using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-195: edge cases of the default review dates, and the evidence item that needs something covered. Arrange / Act / Assert.
public class ReviewDefaultsTests
{
    [Fact]
    public void The_standard_defaults_are_twelve_months_and_three_years_for_certificates()
    {
        Assert.Equal(12, ReviewDefaults.Standard.MonthsFor(ReviewDateKind.EvidenceValidity));
        Assert.Equal(36, ReviewDefaults.Standard.MonthsFor(ReviewDateKind.AuditCertificate));
    }

    [Fact]
    public void The_date_is_today_plus_the_months()
        => Assert.Equal(new DateOnly(2027, 11, 15), ReviewDefaults.Standard.DateFor(ReviewDateKind.EvidenceValidity, new DateOnly(2026, 11, 15)));

    [Fact]
    public void A_leap_day_moves_to_the_last_day_of_february()
        => Assert.Equal(new DateOnly(2029, 2, 28), ReviewDefaults.Standard.DateFor(ReviewDateKind.DocumentReview, new DateOnly(2028, 2, 29)));

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-1)]
    public void A_default_outside_1_to_60_months_is_refused(int months)
        => Assert.Throws<DomainException>(() => new ReviewDefaults(months, 12, 12, 36).Checked());

    [Fact]
    public void The_organisation_keeps_its_defaults_and_refuses_a_bad_one_without_changing()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new UserIdentity("anna", "Anna", "anna@acme.example"));
        organization.SetReviewDefaults(new ReviewDefaults(24, 6, 12, 36));

        Assert.Throws<DomainException>(() => organization.SetReviewDefaults(new ReviewDefaults(0, 6, 12, 36)));
        Assert.Equal(new ReviewDefaults(24, 6, 12, 36), organization.ReviewDefaults);
    }

    // ---------------------------------------------------------- The audit check (R5)

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    public void The_audit_check_counts_evidence_without_an_end_date(int withoutDate, bool ok)
    {
        var line = AuditReadiness.Dates(withoutDate).Lines.Single();

        Assert.Equal(ok, line.Ok);
        Assert.Equal(ReadinessFix.Evidence, line.Fix);
    }

    // ---------------------------------------------------------- The fix: the evidence item needs something covered

    [Theory]
    [InlineData(0, 0, ChecklistState.Missing)]   // nothing covered: not "every covered requirement has evidence" yet
    [InlineData(2, 0, ChecklistState.Done)]
    [InlineData(2, 1, ChecklistState.Missing)]
    public void Every_covered_requirement_has_evidence_needs_something_covered(int covered, int withoutEvidence, ChecklistState state)
    {
        var facts = new ChecklistFacts(false, new HashSet<string>(), 0, 1, 0, withoutEvidence, false, 0, false, false, false) { Covered = covered };

        Assert.Equal(state, ChecklistRules.State(BuiltInChecklist.Find("cra-evidence")!, null, facts));
    }
}
