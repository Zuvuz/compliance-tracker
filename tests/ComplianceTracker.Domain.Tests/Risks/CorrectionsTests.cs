using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Scanning;
using ComplianceTracker.Domain.Security;

namespace ComplianceTracker.Domain.Tests.Risks;

// US-176: edge cases of taking back what was made by mistake. Arrange / Act / Assert.
public class CorrectionsTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static Risk Risk() => Domain.Risks.Risk.Create(Guid.NewGuid(), "Typo risk", null, 3, 4, Anna, RiskTreatment.Reduce, Anna, Monday);

    // ---------------------------------------------------------- Withdraw a risk (R1, R2, R3)

    [Fact]
    public void An_open_risk_is_withdrawn_with_its_reason_and_closed()
    {
        var risk = Risk();

        risk.Withdraw("  Created by mistake ", Monday);

        Assert.Equal((RiskStatus.Closed, true, "Created by mistake", "Withdrawn"), (risk.Status, risk.IsWithdrawn, risk.ClosedReason, risk.DisplayStatus));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Withdrawing_needs_a_reason(string? reason)
        => Assert.Equal("Say why the risk is withdrawn", Assert.Throws<DomainException>(() => Risk().Withdraw(reason, Monday)).Message);

    [Fact]
    public void An_accepted_or_closed_risk_cannot_be_withdrawn()
    {
        var accepted = Risk();
        accepted.Accept("We live with it", Anna, Monday);
        var closed = Risk();
        closed.Close("Gone", Monday);

        Assert.Throws<DomainException>(() => accepted.Withdraw("x", Monday));
        Assert.Throws<DomainException>(() => closed.Withdraw("x", Monday));
    }

    // ---------------------------------------------------------- Reopen (R1)

    [Fact]
    public void An_accepted_risk_reopens_to_be_reduced_again_without_the_acceptance()
    {
        var risk = Risk();
        risk.Accept("Accepted by mistake", Anna, Monday);

        risk.Reopen("Wrong risk accepted");

        Assert.Equal((RiskStatus.Open, RiskTreatment.Reduce, null, null), (risk.Status, risk.Treatment, risk.AcceptedReason, risk.AcceptedById));
    }

    [Fact]
    public void A_closed_risk_reopens_without_its_closing_reason()
    {
        var risk = Risk();
        risk.Close("Gone", Monday);

        risk.Reopen("Still there");

        Assert.Equal((RiskStatus.Open, null, null), (risk.Status, risk.ClosedReason, risk.ClosedAt));
    }

    [Fact]
    public void An_open_or_withdrawn_risk_cannot_be_reopened_and_a_reason_is_needed()
    {
        var withdrawn = Risk();
        withdrawn.Withdraw("Typo", Monday);
        var closed = Risk();
        closed.Close("Gone", Monday);

        Assert.Equal("The risk is open already", Assert.Throws<DomainException>(() => Risk().Reopen("x")).Message);
        Assert.Equal("A withdrawn risk cannot be reopened; add it again", Assert.Throws<DomainException>(() => withdrawn.Reopen("x")).Message);
        Assert.Equal("Say why the risk is reopened", Assert.Throws<DomainException>(() => closed.Reopen(" ")).Message);
    }

    // ---------------------------------------------------------- Actions (R1)

    [Fact]
    public void An_action_can_be_removed()
    {
        var risk = Risk();
        var action = risk.AddAction("Wrong action", Anna, null, Anna, Monday);

        risk.RemoveAction(action.Id);

        Assert.Empty(risk.Actions);
    }

    [Fact]
    public void Unlinking_the_last_link_makes_the_action_planned_again()
    {
        var risk = Risk();
        var action = risk.AddAction("Turn on MFA", Anna, null, Anna, Monday);
        var link = risk.LinkAction(action.Id, Guid.NewGuid(), null);

        risk.UnlinkAction(action.Id, link.Id);

        Assert.Equal((0, RiskActionStatus.Planned), (action.Links.Count, action.Status));
    }

    [Fact]
    public void A_done_action_keeps_its_proof_and_an_unknown_link_is_not_found()
    {
        var risk = Risk();
        var action = risk.AddAction("Turn on MFA", Anna, null, Anna, Monday);
        var link = risk.LinkAction(action.Id, null, Guid.NewGuid());

        Assert.Equal("Link not found", Assert.Throws<DomainException>(() => risk.UnlinkAction(action.Id, Guid.NewGuid())).Message);
        risk.CompleteAction(action.Id, hasProof: true, Anna, Monday);
        Assert.Equal("A done action keeps the proof it was done with", Assert.Throws<DomainException>(() => risk.UnlinkAction(action.Id, link.Id)).Message);
    }

    // ---------------------------------------------------------- A finding (R1)

    [Fact]
    public void An_accepted_finding_is_open_again_without_the_acceptance()
    {
        var finding = Finding.Create(Guid.NewGuid(), "trivy", "fp", "CVE-1", FindingSeverity.High, "msg", null, Guid.NewGuid());
        finding.Accept("Not reachable", Anna, Monday);

        finding.UndoAccept();

        Assert.Equal((FindingState.Open, null, null), (finding.State, finding.AcceptedReason, finding.AcceptedById));
        Assert.Equal("The finding is not accepted", Assert.Throws<DomainException>(finding.UndoAccept).Message);
    }

    // ---------------------------------------------------------- A security event (R1, R4)

    private static SecurityEvent Event()
        => SecurityEvent.Record(Guid.NewGuid(), SecurityEventKind.SevereIncident, "Test entry", null, Monday, Anna, null, Monday);

    [Fact]
    public void An_event_recorded_by_mistake_is_closed_with_the_reason()
    {
        var securityEvent = Event();

        securityEvent.MarkRecordedInError("A test entry", Anna, Monday.AddHours(1));

        Assert.Equal((true, false, "Recorded in error: A test entry"), (securityEvent.RecordedInError, securityEvent.IsOpen, securityEvent.ClosedReason));
    }

    [Fact]
    public void An_event_with_a_sent_report_cannot_be_recorded_in_error()
    {
        var securityEvent = Event();
        securityEvent.MarkSent(SecurityReportKind.EarlyWarning, Monday.AddHours(2), "CSIRT-1", Monday.AddHours(2));

        Assert.Equal("A report was sent for this event; add a correction instead",
            Assert.Throws<DomainException>(() => securityEvent.MarkRecordedInError("Oops", Anna, Monday.AddHours(3))).Message);
    }

    [Fact]
    public void Recorded_in_error_needs_a_reason()
        => Assert.Equal("Say why the event was recorded in error", Assert.Throws<DomainException>(() => Event().MarkRecordedInError(" ", Anna, Monday)).Message);
}
