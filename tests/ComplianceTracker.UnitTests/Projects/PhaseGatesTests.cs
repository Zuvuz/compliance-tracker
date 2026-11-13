using System.Text;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.UnitTests.Projects;

// Unit tests of the checks of the gate to Development (US-071 R1). Every test follows Arrange / Act / Assert.
public class PhaseGatesTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();

    private static Project NewProject(string? scope = null)
        => Project.Create(OrganizationId, "Acme Portal", null, "A customer portal", scope, LifecyclePhase.Planning);

    private static Project WithFramework(string? scope = "The portal")
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(OrganizationId, "Security policy", CatalogFile.Create("policy.pdf", bytes), Anna, Monday);
        var version = framework.Versions.Single();
        version.AddRequirement("1.1", "Passwords", null);
        version.Activate(Anna, Monday);

        var project = NewProject(scope);
        project.UseFramework(framework, version, Anna, Monday);
        return project;
    }

    private static Dictionary<string, (string Text, GateCheckState State)> Checks(Project project, IReadOnlyList<string>? unclassified = null, int risks = 0)
        => PhaseGates.DevelopmentChecks(project, unclassified ?? [], risks).ToDictionary(c => c.Key, c => (c.Text, c.State));

    [Fact]
    public void A_new_project_misses_scope_framework_and_risks()
    {
        var checks = Checks(NewProject());

        Assert.Equal(["scope", "frameworks", "classification", "risks"], checks.Keys);
        Assert.Equal(3, checks.Values.Count(c => c.State == GateCheckState.NotMet));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_missing_scope_is_not_met(string? scope)
        => Assert.Equal(GateCheckState.NotMet, Checks(NewProject(scope))["scope"].State);

    [Fact]
    public void With_nothing_to_classify_the_classification_is_met()
        => Assert.Equal(("Classified under each regulation it follows", GateCheckState.Met), Checks(NewProject())["classification"]);

    [Fact]
    public void Each_unclassified_regulation_is_named()
        => Assert.Equal(("Not classified under CRA, AI Act", GateCheckState.NotMet), Checks(WithFramework(), ["CRA", "AI Act"])["classification"]);

    [Fact]
    public void Frameworks_and_risks_are_counted()
    {
        var checks = Checks(WithFramework(), risks: 2);

        Assert.Equal(("Follows 1 framework", GateCheckState.Met), checks["frameworks"]);
        Assert.Equal(("2 risks in the register", GateCheckState.Met), checks["risks"]);
    }

    [Fact]
    public void One_risk_is_singular()
        => Assert.Equal("1 risk in the register", Checks(NewProject(), risks: 1)["risks"].Text);

    [Fact]
    public void A_complete_project_meets_every_check()
        => Assert.All(Checks(WithFramework(), risks: 1).Values, c => Assert.Equal(GateCheckState.Met, c.State));
}
