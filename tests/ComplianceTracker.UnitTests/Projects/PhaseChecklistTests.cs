using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Projects;

// US-047: edge cases of the lifecycle checklists. Arrange / Act / Assert.
public class PhaseChecklistTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Project = Guid.NewGuid();

    private static ChecklistFacts Facts(bool sbom = false, int risks = 0, params string[] unclassified)
        => new(false, unclassified.ToHashSet(), risks, 1, 0, 0, false, 0, sbom, false, false);

    private static ChecklistItem Item(string key) => BuiltInChecklist.Find(key)!;

    // ---------------------------------------------------------- Phases (R1)

    [Fact]
    public void There_are_six_phases_in_order()
        => Assert.Equal(["Planning", "Design", "Development", "Verification", "Released", "EndOfSupport"], Enum.GetNames<LifecyclePhase>());

    [Fact]
    public void Every_item_has_its_own_key_and_a_regulation()
    {
        Assert.Equal(BuiltInChecklist.All.Count, BuiltInChecklist.All.Select(i => i.Key).Distinct().Count());
        Assert.All(BuiltInChecklist.All, i => Assert.False(string.IsNullOrWhiteSpace(i.Regulation)));
    }

    // ---------------------------------------------------------- Which items (R2)

    [Fact]
    public void A_project_with_no_framework_gets_only_the_optional_tests()
        => Assert.All(BuiltInChecklist.For([]), i => Assert.True(i.Optional));

    [Fact]
    public void The_iso_items_follow_any_version_of_iso_27001()
        => Assert.Contains(BuiltInChecklist.For(["ISO/IEC 27001:2022"]), i => i.Key == "iso-testing");

    [Fact]
    public void Items_that_look_alike_in_two_regulations_are_both_listed()
    {
        var items = BuiltInChecklist.For(["CRA", "ISO/IEC 27001:2022"]).Where(i => i.Phase == LifecyclePhase.Planning && i.Check == ChecklistCheck.Risks).ToList();

        Assert.Equal(["CRA", "ISO/IEC 27001"], items.Select(i => i.Regulation));
    }

    // ---------------------------------------------------------- States (R4, R8, R9)

    [Fact]
    public void An_automatic_item_follows_the_data()
    {
        Assert.Equal(ChecklistState.Missing, ChecklistRules.State(Item("cra-sbom"), null, Facts()));
        Assert.Equal(ChecklistState.Done, ChecklistRules.State(Item("cra-sbom"), null, Facts(sbom: true)));
    }

    [Fact]
    public void Classification_is_per_regulation()
    {
        var facts = Facts(unclassified: "AI Act");

        Assert.Equal(ChecklistState.Done, ChecklistRules.State(Item("cra-class"), null, facts));
        Assert.Equal(ChecklistState.Missing, ChecklistRules.State(Item("ai-class"), null, facts));
    }

    [Fact]
    public void A_manual_item_is_done_when_ticked_and_missing_again_when_unticked()
    {
        var entry = ProjectChecklistEntry.For(Project, "cra-threat-model", Monday);
        entry.Tick(Guid.NewGuid(), UserRole.Admin, "STRIDE", null, Monday);
        Assert.Equal(ChecklistState.Done, ChecklistRules.State(Item("cra-threat-model"), entry, Facts()));

        entry.Untick(Monday);
        Assert.Equal(ChecklistState.Missing, ChecklistRules.State(Item("cra-threat-model"), entry, Facts()));
        Assert.Null(entry.Comment);
    }

    [Fact]
    public void A_turned_off_item_is_off_even_when_its_data_is_there()
    {
        var entry = ProjectChecklistEntry.For(Project, "cra-sbom", Monday);
        entry.TurnOff("Not a software product", Monday);

        Assert.Equal(ChecklistState.TurnedOff, ChecklistRules.State(Item("cra-sbom"), entry, Facts(sbom: true)));
    }

    [Fact]
    public void An_optional_test_is_not_chosen_until_turned_on()
    {
        var entry = ProjectChecklistEntry.For(Project, "opt-pentest", Monday);
        Assert.Equal(ChecklistState.NotChosen, ChecklistRules.State(Item("opt-pentest"), null, Facts()));

        entry.TurnOn(Monday);
        Assert.Equal(ChecklistState.Missing, ChecklistRules.State(Item("opt-pentest"), entry, Facts()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Turning_off_needs_a_reason(string reason)
        => Assert.Throws<DomainException>(() => ProjectChecklistEntry.For(Project, "cra-sbom", Monday).TurnOff(reason, Monday));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a link")]
    [InlineData("ftp://example.com/file")]
    public void The_evidence_link_must_be_a_web_address(string url)
        => Assert.Throws<DomainException>(() => ProjectChecklistEntry.For(Project, "cra-threat-model", Monday).Tick(Guid.NewGuid(), UserRole.Admin, null, url, Monday));

    // ---------------------------------------------------------- Moving on (R5, R6)

    [Fact]
    public void Moving_on_lists_what_is_missing_in_the_phases_left_behind()
    {
        var lines = ChecklistRules.Lines(BuiltInChecklist.For(["CRA"]), [], Facts());

        var missing = ChecklistRules.MissingBefore(lines, LifecyclePhase.Design, LifecyclePhase.Verification);

        Assert.Contains(missing, l => l.Item.Key == "cra-threat-model");       // Design
        Assert.Contains(missing, l => l.Item.Key == "cra-vulnerability-handling"); // Development
        Assert.DoesNotContain(missing, l => l.Item.Phase == LifecyclePhase.Verification);
    }

    [Fact]
    public void Moving_back_lists_nothing()
    {
        var lines = ChecklistRules.Lines(BuiltInChecklist.For(["CRA"]), [], Facts());

        Assert.Empty(ChecklistRules.MissingBefore(lines, LifecyclePhase.Verification, LifecyclePhase.Design));
    }

    [Fact]
    public void A_gate_logs_the_missing_items_apart()
    {
        var gate = new PhaseGate("Ready for Verification", LifecyclePhase.Development, LifecyclePhase.Verification, []) { ChecklistMissing = ["Threat model made (CRA Art. 13(3))"] };

        Assert.True(gate.IsMet); // missing checklist items warn, but need no reason
        Assert.Equal("Development → Verification, missing: Threat model made (CRA Art. 13(3))", gate.DescribeMissing());
    }
}
