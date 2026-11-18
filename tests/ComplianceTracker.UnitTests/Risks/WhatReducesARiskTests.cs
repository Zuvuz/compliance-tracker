using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Risks;

// US-202: actions under the measure they carry out. Edge cases. Arrange / Act / Assert.
public class WhatReducesARiskTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    [Fact]
    public void An_action_is_added_under_a_measure_or_under_other_actions()
    {
        var risk = Risk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);

        var under = risk.AddAction("Better passwords", Anna, null, Anna, Monday, measure.Id);
        var other = risk.AddAction("Train the staff", Anna, null, Anna, Monday);

        Assert.Equal([under], risk.ActionsUnder(measure.Id));
        Assert.Equal([other], risk.ActionsUnder(null));
    }

    [Fact]
    public void An_action_cannot_go_under_a_measure_of_another_risk()
    {
        var risk = Risk();
        var elsewhere = Risk().AddMeasure(Guid.NewGuid(), null);

        Assert.Equal("The measure is not linked to the risk",
            Assert.Throws<DomainException>(() => risk.AddAction("Better passwords", Anna, null, Anna, Monday, elsewhere.Id)).Message);
        Assert.Empty(risk.Actions);
    }

    [Fact]
    public void An_action_moves_to_another_measure_and_back_to_other_actions()
    {
        var risk = Risk();
        var first = risk.AddMeasure(Guid.NewGuid(), null);
        var second = risk.AddMeasure(null, Guid.NewGuid());
        var action = risk.AddAction("Better passwords", Anna, null, Anna, Monday, first.Id);

        risk.MoveAction(action.Id, second.Id);
        Assert.Equal(second.Id, action.MeasureId);

        risk.MoveAction(action.Id, null);
        Assert.Null(action.MeasureId);
        Assert.Throws<DomainException>(() => risk.MoveAction(action.Id, null)); // already there
    }

    [Fact]
    public void A_measure_with_actions_cannot_be_removed_until_they_are_moved()
    {
        var risk = Risk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);
        var action = risk.AddAction("Better passwords", Anna, null, Anna, Monday, measure.Id);
        risk.AddAction("Shorter sessions", Anna, null, Anna, Monday, measure.Id);

        Assert.Equal("Move or remove the 2 actions under the measure first",
            Assert.Throws<DomainException>(() => risk.RemoveMeasure(measure.Id)).Message);

        risk.MoveAction(action.Id, null);
        Assert.Equal("Move or remove the 1 action under the measure first",
            Assert.Throws<DomainException>(() => risk.RemoveMeasure(measure.Id)).Message);
    }

    [Fact]
    public void A_measure_whose_actions_are_removed_can_be_removed()
    {
        var risk = Risk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);
        var action = risk.AddAction("Better passwords", Anna, null, Anna, Monday, measure.Id);

        risk.RemoveAction(action.Id);
        risk.RemoveMeasure(measure.Id);

        Assert.Empty(risk.Measures);
    }

    [Fact]
    public void Done_actions_come_last_under_a_measure()
    {
        var risk = Risk();
        var measure = risk.AddMeasure(Guid.NewGuid(), null);
        var done = risk.AddAction("Done one", Anna, new DateOnly(2026, 11, 1), Anna, Monday, measure.Id);
        risk.LinkAction(done.Id, Guid.NewGuid(), null);
        risk.CompleteAction(done.Id, true, Anna, Monday);
        var open = risk.AddAction("Open one", Anna, null, Anna, Monday, measure.Id);

        Assert.Equal([open, done], risk.ActionsUnder(measure.Id));
    }

    private static Risk Risk() => Domain.Risks.Risk.Create(Guid.NewGuid(), "Session Hijacking", null, 3, 4, Anna, RiskTreatment.Reduce, Anna, Monday);
}
