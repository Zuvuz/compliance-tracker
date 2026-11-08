using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Domain.Tests.Approvals;

// Edge-case unit tests of US-135: four-eyes approval requests, and the checks done before asking. Arrange / Act / Assert.
public class ApprovalRequestTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Bo = Guid.NewGuid();

    private static ApprovalRequest Request(string? summary = "Accept the risk \"Stolen sessions\" (High)", string? payload = "{\"riskId\":\"x\"}")
        => ApprovalRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ApprovalAction.AcceptRisk, Guid.NewGuid(), "7", summary, payload, Anna, Monday);

    [Fact]
    public void A_new_request_is_pending_and_expires_after_7_days()
    {
        // Arrange / Act
        var request = Request();

        // Assert
        Assert.True(request.IsPending);
        Assert.Equal(Monday.AddDays(7), request.ExpiresAt);
        Assert.Null(request.DecidedById);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_request_must_say_what_will_happen(string? summary)
    {
        // Act / Assert
        Assert.Equal("Say what will happen", Assert.Throws<DomainException>(() => Request(summary)).Message);
    }

    [Fact]
    public void A_very_long_summary_is_cut_with_an_ellipsis()
    {
        // Act
        var request = Request(new string('a', 600));

        // Assert
        Assert.Equal(ApprovalRequest.MaxSummaryLength, request.Summary.Length);
        Assert.EndsWith("…", request.Summary);
    }

    [Fact]
    public void An_empty_payload_becomes_an_empty_object()
    {
        // Act / Assert
        Assert.Equal("{}", Request(payload: " ").Payload);
    }

    [Fact]
    public void Another_person_approves()
    {
        // Arrange
        var request = Request();

        // Act
        request.Approve(Bo, Monday.AddHours(1));

        // Assert
        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal(Bo, request.DecidedById);
        Assert.Equal(Monday.AddHours(1), request.DecidedAt);
    }

    [Fact]
    public void You_cannot_approve_your_own_request()
    {
        // Arrange
        var request = Request();

        // Act / Assert
        Assert.Equal("Another person must approve this", Assert.Throws<DomainException>(() => request.Approve(Anna, Monday)).Message);
        Assert.True(request.IsPending);
    }

    [Fact]
    public void A_request_cannot_be_approved_twice()
    {
        // Arrange
        var request = Request();
        request.Approve(Bo, Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => request.Approve(Guid.NewGuid(), Monday));
    }

    [Fact]
    public void A_second_of_the_seventh_day_is_still_open_and_the_end_of_it_is_not()
    {
        // Arrange
        var almost = Request();
        var late = Request();

        // Act
        almost.Approve(Bo, Monday.AddDays(7).AddSeconds(-1));

        // Assert
        Assert.Equal(ApprovalStatus.Approved, almost.Status);
        Assert.Throws<DomainException>(() => late.Approve(Bo, Monday.AddDays(7)));
    }

    [Fact]
    public void An_expired_request_is_marked_once_at_its_expiry_time()
    {
        // Arrange
        var request = Request();

        // Act
        var first = request.ExpireIfDue(Monday.AddDays(9));
        var again = request.ExpireIfDue(Monday.AddDays(10));

        // Assert
        Assert.True(first);
        Assert.False(again);
        Assert.Equal(ApprovalStatus.Expired, request.Status);
        Assert.Equal(Monday.AddDays(7), request.DecidedAt);
    }

    [Fact]
    public void A_request_that_is_not_due_does_not_expire()
    {
        // Arrange
        var request = Request();

        // Act / Assert
        Assert.False(request.ExpireIfDue(Monday.AddDays(6)));
        Assert.True(request.IsPending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejecting_needs_a_reason(string? reason)
    {
        // Arrange
        var request = Request();

        // Act / Assert
        Assert.Equal("Give a reason for rejecting", Assert.Throws<DomainException>(() => request.Reject(Bo, reason, Monday)).Message);
    }

    [Fact]
    public void The_person_who_asked_withdraws_instead_of_rejecting()
    {
        // Arrange
        var request = Request();

        // Act / Assert
        Assert.Throws<DomainException>(() => request.Reject(Anna, "Changed my mind", Monday));
        request.Withdraw(Anna, Monday);
        Assert.Equal(ApprovalStatus.Cancelled, request.Status);
        Assert.Equal("Withdrawn", request.DecisionNote);
    }

    [Fact]
    public void Only_the_person_who_asked_can_withdraw()
    {
        // Arrange
        var request = Request();

        // Act / Assert
        Assert.Throws<DomainException>(() => request.Withdraw(Bo, Monday));
        Assert.True(request.IsPending);
    }

    [Fact]
    public void A_changed_subject_cancels_the_request_without_a_person()
    {
        // Arrange
        var request = Request();

        // Act
        request.Cancel("the risk changed after the request was made", Monday);

        // Assert
        Assert.Equal(ApprovalStatus.Cancelled, request.Status);
        Assert.Null(request.DecidedById);
        Assert.Throws<DomainException>(() => request.Cancel("again", Monday));
    }

    [Fact]
    public void A_long_rejection_reason_is_cut()
    {
        // Arrange
        var request = Request();

        // Act
        request.Reject(Bo, new string('r', 2000), Monday);

        // Assert
        Assert.Equal(ApprovalRequest.MaxReasonLength, request.DecisionNote!.Length);
    }

    // ---------------------------------------------------------- What needs approval (R2)

    [Theory]
    [InlineData(3, 3, false)] // 9: Medium
    [InlineData(3, 4, true)] // 12: High
    [InlineData(4, 4, true)] // 16: High, the top of the band
    [InlineData(5, 5, true)] // 25: Critical
    public void Accepting_a_High_or_Critical_risk_needs_approval(int likelihood, int impact, bool needs)
    {
        // Arrange
        var risk = Risk.Create(Guid.NewGuid(), "Stolen sessions", null, likelihood, impact, Anna, RiskTreatment.Reduce, Anna, Monday);

        // Act / Assert
        Assert.Equal(needs, risk.NeedsApprovalToAccept);
    }

    [Fact]
    public void The_residual_level_decides_when_it_is_given()
    {
        // Arrange: inherently Critical, Low after the measures
        var risk = Risk.Create(Guid.NewGuid(), "Stolen sessions", null, 5, 5, Anna, RiskTreatment.Reduce, Anna, Monday);
        risk.Update("Stolen sessions", null, 5, 5, Anna, RiskTreatment.Reduce, 1, 2);

        // Act / Assert
        Assert.False(risk.NeedsApprovalToAccept);
    }

    [Fact]
    public void Checking_an_acceptance_changes_nothing_and_needs_a_reason()
    {
        // Arrange
        var risk = Risk.Create(Guid.NewGuid(), "Stolen sessions", null, 3, 4, Anna, RiskTreatment.Reduce, Anna, Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => risk.CheckAccept(" "));
        risk.CheckAccept("Short-lived tokens");
        Assert.Equal(RiskStatus.Open, risk.Status);
    }

    [Fact]
    public void Checking_end_of_support_changes_nothing()
    {
        // Arrange
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", null, "Purpose", null, LifecyclePhase.Development);
        var today = new DateOnly(2032, 1, 10);

        // Act
        project.CheckEndSupport(new DateOnly(2026, 12, 31), null, today);

        // Assert
        Assert.Equal(LifecyclePhase.Development, project.Phase);
        Assert.Null(project.RetainUntil);
    }

    [Fact]
    public void Checking_end_of_support_refuses_a_retention_date_too_early()
    {
        // Arrange
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", null, "Purpose", null, LifecyclePhase.Development);

        // Act / Assert
        Assert.Throws<DomainException>(() => project.CheckEndSupport(new DateOnly(2026, 12, 31), new DateOnly(2030, 1, 1), new DateOnly(2032, 1, 10)));
        Assert.Throws<DomainException>(() => project.CheckEndSupport(new DateOnly(2033, 1, 1), null, new DateOnly(2032, 1, 10)));
    }
}
