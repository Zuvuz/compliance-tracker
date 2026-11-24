using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-209: an objective, its target and its measurements. Every test follows Arrange / Act / Assert.
public class SecurityObjectiveTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 23);

    private static SecurityObjective Coverage()
        => SecurityObjective.Create(Organisation, "Requirement coverage", Anna, TrackerMeasure.RequirementCoverage, 90, null,
            ObjectiveDirection.HigherIsBetter, ObjectiveFrequency.Monthly, null, Monday);

    private static SecurityObjective HighRisks()
        => SecurityObjective.Create(Organisation, "Open high risks", Anna, TrackerMeasure.OpenHighRisks, 2, null,
            ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Monthly, null, Monday);

    private static SecurityObjective Phishing()
        => SecurityObjective.Create(Organisation, "Phishing test click rate", Anna, null, 5, "%",
            ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Quarterly, null, Monday);

    // ---------------------------------------------------------- R1

    [Fact]
    public void A_tracker_measure_brings_its_unit()
    {
        Assert.Equal("%", Coverage().Unit);
        Assert.Equal("", HighRisks().Unit);
        Assert.False(Coverage().IsManual);
        Assert.True(Phishing().IsManual);
    }

    [Fact]
    public void An_objective_needs_a_text_an_owner_a_target_a_direction_and_a_frequency()
    {
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, " ", Anna, null, 5, "", ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Yearly, null, Monday));
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, "X", null, null, 5, "", ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Yearly, null, Monday));
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, "X", Anna, null, null, "", ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Yearly, null, Monday));
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, "X", Anna, null, 5, "", null, ObjectiveFrequency.Yearly, null, Monday));
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, "X", Anna, null, 5, "", ObjectiveDirection.LowerIsBetter, null, null, Monday));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_target_in_per_cent_is_between_0_and_100(int target)
    {
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, "X", Anna, TrackerMeasure.EvidenceCoverage, target, null,
            ObjectiveDirection.HigherIsBetter, ObjectiveFrequency.Yearly, null, Monday));
    }

    [Fact]
    public void A_measure_that_does_not_exist_is_refused()
    {
        Assert.Throws<DomainException>(() => SecurityObjective.Create(Organisation, "X", Anna, (TrackerMeasure)99, 5, null,
            ObjectiveDirection.HigherIsBetter, ObjectiveFrequency.Yearly, null, Monday));
    }

    // ---------------------------------------------------------- R4

    [Theory]
    [InlineData(95, ObjectiveState.OnTarget)]
    [InlineData(90, ObjectiveState.OnTarget)]
    [InlineData(86, ObjectiveState.CloseToTarget)]
    [InlineData(81, ObjectiveState.CloseToTarget)]
    [InlineData(70, ObjectiveState.OffTarget)]
    public void Higher_is_better_is_green_at_the_target_and_yellow_within_ten_per_cent(int value, ObjectiveState state)
        => Assert.Equal(state, Coverage().StateOf(value));

    [Theory]
    [InlineData(2, ObjectiveState.OnTarget)]
    [InlineData(4, ObjectiveState.OffTarget)]
    public void Lower_is_better_is_red_above_the_margin(int value, ObjectiveState state)
        => Assert.Equal(state, HighRisks().StateOf(value));

    [Fact]
    public void A_target_of_zero_with_lower_is_better_has_no_margin()
    {
        var objective = SecurityObjective.Create(Organisation, "No overdue actions", Anna, TrackerMeasure.OverdueRiskActions, 0, null,
            ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Monthly, null, Monday);

        Assert.Equal(ObjectiveState.OnTarget, objective.StateOf(0));
        Assert.Equal(ObjectiveState.OffTarget, objective.StateOf(1));
    }

    [Fact]
    public void An_objective_without_measurements_is_not_measured()
        => Assert.Equal(ObjectiveState.NotMeasured, Phishing().State);

    // ---------------------------------------------------------- R2, R3

    [Fact]
    public void An_objective_never_measured_is_due()
        => Assert.True(Phishing().IsDueForMeasurement(Today));

    [Fact]
    public void A_quarterly_objective_is_due_three_months_after_the_last_measurement()
    {
        var objective = Phishing();
        objective.Record(12, new DateOnly(2026, 7, 1), Anna, null, Monday);

        Assert.Equal(new DateOnly(2026, 10, 1), objective.NextMeasurementOn);
        Assert.False(objective.IsDueForMeasurement(new DateOnly(2026, 9, 30)));
        Assert.True(objective.IsDueForMeasurement(Today));
    }

    // ---------------------------------------------------------- R6

    [Fact]
    public void A_correction_is_a_new_measurement_and_the_old_one_stays()
    {
        var objective = Coverage();
        objective.Record(86, Today, null, null, Monday);

        objective.Record(88, Today, Anna, "one framework was counted twice", Monday.AddMinutes(5));

        Assert.Equal(2, objective.Measurements.Count);
        Assert.Equal(86, objective.Measurements[0].Value);
        Assert.Equal(88, objective.Last!.Value);
        Assert.False(objective.Last.ByTracker);
    }

    [Fact]
    public void A_person_correcting_the_tracker_must_say_why()
    {
        var objective = Coverage();

        var error = Assert.Throws<DomainException>(() => objective.Record(88, Today, Anna, " ", Monday));

        Assert.Equal("Say why you correct the tracker's measurement", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_value_must_be_there_and_fit_the_unit(int? value)
        => Assert.Throws<DomainException>(() => Phishing().Record(value, Today, Anna, null, Monday));

    [Fact]
    public void A_removed_objective_takes_no_measurements()
    {
        var objective = Phishing();
        objective.Remove(Monday);

        Assert.False(objective.IsDueForMeasurement(Today));
        Assert.Throws<DomainException>(() => objective.Record(3, Today, Anna, null, Monday));
    }

    [Fact]
    public void A_change_names_what_changed()
    {
        var objective = Phishing();

        var changed = objective.Change("Phishing test click rate", Anna, 3, "%", ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Monthly, null);

        Assert.Equal(["target", "frequency"], changed);
    }
}
