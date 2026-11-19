using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.UnitTests.Projects;

// #52: the phase card's main button goes to the next phase
public class LifecyclePhasesTests
{
    [Theory]
    [InlineData(LifecyclePhase.Planning, LifecyclePhase.Design)]
    [InlineData(LifecyclePhase.Design, LifecyclePhase.Development)]
    [InlineData(LifecyclePhase.Development, LifecyclePhase.Verification)]
    [InlineData(LifecyclePhase.Verification, LifecyclePhase.Released)]
    [InlineData(LifecyclePhase.Released, LifecyclePhase.EndOfSupport)]
    public void The_next_phase_follows_the_order(LifecyclePhase phase, LifecyclePhase next)
        => Assert.Equal(next, LifecyclePhases.Next(phase));

    [Fact]
    public void Nothing_comes_after_end_of_support()
        => Assert.Null(LifecyclePhases.Next(LifecyclePhase.EndOfSupport));

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(99)]
    public void A_value_that_is_not_a_phase_has_no_next(int value)
        => Assert.Null(LifecyclePhases.Next((LifecyclePhase)value));
}
