using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.UnitTests.Isms;

// US-212: the filters and the order of the register. Every test follows Arrange / Act / Assert.
public class NonconformityRegisterTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static Nonconformity Raise(string title, NonconformitySeverity severity, NonconformitySource source = NonconformitySource.Other, int minutes = 0)
        => Nonconformity.Raise(Organisation, title, null, source, null, severity, Anna, Monday.AddMinutes(minutes));

    private static Nonconformity Closed(string title)
    {
        var nonconformity = Raise(title, NonconformitySeverity.Minor);
        nonconformity.Analyse(null, "cause");
        nonconformity.PlanAction("Action", Anna, Today, Today);
        nonconformity.MarkActionDone(Monday);
        nonconformity.Check("ok", true, Today, Anna, Monday);
        return nonconformity;
    }

    [Fact]
    public void The_closed_ones_are_hidden_unless_asked_for()
    {
        var all = new[] { Raise("Open", NonconformitySeverity.Minor), Closed("Closed") };

        Assert.Equal(["Open"], NonconformityService.Filter(all, new NonconformityFilter(), Today).Select(n => n.Title));
        Assert.Equal(2, NonconformityService.Filter(all, new NonconformityFilter(ShowClosed: true), Today).Count);
        Assert.Equal(["Closed"], NonconformityService.Filter(all, new NonconformityFilter(NonconformityStatus.Effective), Today).Select(n => n.Title));
    }

    [Fact]
    public void The_overdue_come_first_then_the_most_severe()
    {
        var overdue = Raise("Overdue", NonconformitySeverity.Observation);
        overdue.PlanAction("Action", Anna, Today, Today);
        var all = new[] { Raise("Minor", NonconformitySeverity.Minor), Raise("Major", NonconformitySeverity.Major), overdue };

        var shown = NonconformityService.Filter(all, new NonconformityFilter(), Today.AddDays(1));

        Assert.Equal(["Overdue", "Major", "Minor"], shown.Select(n => n.Title));
    }

    [Fact]
    public void The_source_filter_keeps_only_that_source()
    {
        var all = new[] { Raise("Audit", NonconformitySeverity.Minor, NonconformitySource.InternalAudit), Raise("Complaint", NonconformitySeverity.Minor, NonconformitySource.Complaint) };

        var shown = NonconformityService.Filter(all, new NonconformityFilter(Source: NonconformitySource.Complaint), Today);

        Assert.Equal("Complaint", shown.Single().Title);
    }

    [Theory]
    [InlineData(NonconformitySource.InternalAudit, "Internal audit")]
    [InlineData(NonconformitySource.SecurityEvent, "Security event")]
    [InlineData(NonconformitySource.Complaint, "Complaint")]
    public void A_source_has_a_readable_label(NonconformitySource source, string label)
        => Assert.Equal(label, NonconformityService.Label(source));
}
