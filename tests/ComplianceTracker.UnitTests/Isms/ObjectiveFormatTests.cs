using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.UnitTests.Isms;

// US-209: how a value and a target are written, and the tracker's per cent. Every test follows Arrange / Act / Assert.
public class ObjectiveFormatTests
{
    [Theory]
    [InlineData(86, "%", "86 %")]
    [InlineData(4, "", "4")]
    [InlineData(3.5, "days", "3.5 days")]
    public void A_value_is_written_with_its_unit(double value, string unit, string text)
        => Assert.Equal(text, ObjectiveService.Format((decimal)value, unit));

    [Fact]
    public void A_target_says_which_way_is_better()
    {
        var objective = SecurityObjective.Create(Guid.NewGuid(), "Open high risks", Guid.NewGuid(), TrackerMeasure.OpenHighRisks, 2, null,
            ObjectiveDirection.LowerIsBetter, ObjectiveFrequency.Monthly, null, DateTime.UtcNow);

        Assert.Equal("≤ 2", ObjectiveService.Target(objective));
    }

    [Fact]
    public void Coverage_is_rounded_to_whole_per_cent_and_none_without_requirements()
    {
        Assert.Equal(86, TrackerMeasures.Percent(43, 50));
        Assert.Equal(67, TrackerMeasures.Percent(2, 3));
        Assert.Null(TrackerMeasures.Percent(0, 0));
    }

    [Fact]
    public void Every_measure_has_a_label()
        => Assert.All(Enum.GetValues<TrackerMeasure>(), m => Assert.False(string.IsNullOrWhiteSpace(ObjectiveService.Label(m))));
}
