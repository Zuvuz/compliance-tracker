using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of the gates between lifecycle phases (US-071, US-072). Every test follows Arrange / Act / Assert.
public class PhaseGateTests
{
    private static readonly ProjectChange ToDevelopment = new("Phase", "Planning", "Development");

    private static PhaseGate Gate(params GateCheckState[] states)
        => new("Ready for Development", LifecyclePhase.Planning, LifecyclePhase.Development,
            states.Select((s, i) => new GateCheck($"check{i}", $"Check {i}", s)).ToList());

    [Fact]
    public void A_gate_with_every_check_met_needs_no_reason()
    {
        var gate = Gate(GateCheckState.Met, GateCheckState.Met);

        Assert.True(gate.IsMet);
        Assert.Null(gate.Pass(null));
        Assert.Equal("Phase: Planning → Development, all checks met", gate.Describe(ToDevelopment, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_check_not_met_needs_a_reason(string? reason)
    {
        var error = Assert.Throws<DomainException>(() => Gate(GateCheckState.NotMet, GateCheckState.Met).Pass(reason));

        Assert.Equal("Give a reason, as 1 check is not met", error.Message);
    }

    [Fact]
    public void The_message_counts_the_checks_not_met()
        => Assert.Equal("Give a reason, as 3 checks are not met",
            Assert.Throws<DomainException>(() => Gate(GateCheckState.NotMet, GateCheckState.NotMet, GateCheckState.NotMet).Pass(null)).Message);

    [Fact]
    public void The_reason_is_trimmed_and_logged_with_the_count()
    {
        var gate = Gate(GateCheckState.NotMet, GateCheckState.Met);

        var reason = gate.Pass("  Risk workshop next week ");

        Assert.Equal("Phase: Planning → Development, 1 check not met: Risk workshop next week", gate.Describe(ToDevelopment, reason));
    }

    [Fact]
    public void A_reason_over_1000_characters_is_refused()
        => Assert.Throws<DomainException>(() => Gate(GateCheckState.NotMet).Pass(new string('x', 1001)));

    [Fact]
    public void A_check_not_available_yet_does_not_count()
    {
        var gate = Gate(GateCheckState.Met, GateCheckState.NotAvailable);

        Assert.True(gate.IsMet);
        Assert.Empty(gate.NotMet);
    }

    [Fact]
    public void A_gate_without_checks_is_met()
        => Assert.True(Gate().IsMet);
}
