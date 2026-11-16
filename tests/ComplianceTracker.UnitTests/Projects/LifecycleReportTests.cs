using System.Reflection;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Projects;

// US-194: edge cases of the lifecycle in the reports. Arrange / Act / Assert.
public class LifecycleReportTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static string Name(Guid? id) => id == Anna ? "Anna" : "Unknown";

    private static readonly ChecklistFacts Nothing = new(false, new HashSet<string>(), 0, 1, 0, 0, false, 0, false, false, false);

    private static ActivityLogEntry Entry(ActivityAction action, string details, DateTime at)
    {
        var entry = (ActivityLogEntry)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ActivityLogEntry));
        void Set(string name, object? value)
            => typeof(ActivityLogEntry).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(entry, value);
        Set(nameof(ActivityLogEntry.Action), action);
        Set(nameof(ActivityLogEntry.Details), details);
        Set(nameof(ActivityLogEntry.OccurredAt), at);
        Set(nameof(ActivityLogEntry.UserId), (Guid?)Anna);
        return entry;
    }

    private static ProjectLifecycle Lifecycle(LifecyclePhase phase, params ProjectChecklistEntry[] entries)
        => new(phase, ChecklistRules.Lines(BuiltInChecklist.For(["CRA"]), entries, Nothing), []);

    // ---------------------------------------------------------- Phase changes (R3)

    [Fact]
    public void A_phase_change_gets_the_missing_items_logged_next_to_it()
    {
        var history = LifecycleReport.History(
        [
            Entry(ActivityAction.ProjectPhaseChanged, "Phase: Design → Development", Monday),
            Entry(ActivityAction.PhaseChangedWithItemsMissing, "Design → Development, missing: Threat model made (CRA Art. 13(3))", Monday.AddSeconds(1))
        ], Name);

        var change = Assert.Single(history);
        Assert.Equal("Design → Development", change.Change);
        Assert.Equal("Threat model made (CRA Art. 13(3))", change.Missing);
        Assert.Equal("Anna", change.By);
    }

    [Fact]
    public void A_change_with_nothing_missing_has_no_missing_text_and_changes_come_in_order()
    {
        var history = LifecycleReport.History(
        [
            Entry(ActivityAction.ProjectPhaseChanged, "Phase: Development → Design", Monday.AddDays(1)),
            Entry(ActivityAction.ProjectPhaseChanged, "Phase: Design → Development", Monday)
        ], Name);

        Assert.Equal(["Design → Development", "Development → Design"], history.Select(h => h.Change));
        Assert.All(history, h => Assert.Null(h.Missing));
    }

    [Fact]
    public void A_missing_entry_much_later_belongs_to_no_change()
    {
        var history = LifecycleReport.History(
        [
            Entry(ActivityAction.ProjectPhaseChanged, "Phase: Design → Development", Monday),
            Entry(ActivityAction.PhaseChangedWithItemsMissing, "x, missing: y", Monday.AddHours(2))
        ], Name);

        Assert.Null(Assert.Single(history).Missing);
    }

    // ---------------------------------------------------------- Checklist (R1, R2, R4)

    [Fact]
    public void The_summary_counts_the_current_phase_without_turned_off_items()
    {
        var off = ProjectChecklistEntry.For(Guid.NewGuid(), "cra-disclosure", Monday);
        off.TurnOff("By the customer", Monday);

        var lifecycle = Lifecycle(LifecyclePhase.Verification, off);

        Assert.Equal("Verification · 0 of 3 done", lifecycle.Summary); // US-195: "every covered requirement has evidence" needs something covered
    }

    [Fact]
    public void A_phase_with_no_items_shows_only_the_phase()
        => Assert.Equal("Planning", new ProjectLifecycle(LifecyclePhase.Planning, [], []).Summary);

    [Fact]
    public void The_sheet_gives_the_reason_for_a_turned_off_item_and_who_ticked_an_item()
    {
        var off = ProjectChecklistEntry.For(Guid.NewGuid(), "cra-disclosure", Monday);
        off.TurnOff("By the customer", Monday);
        var done = ProjectChecklistEntry.For(Guid.NewGuid(), "cra-threat-model", Monday);
        done.Tick(Anna, UserRole.Admin, "STRIDE", null, Monday);

        var rows = LifecycleReport.Sheet(Lifecycle(LifecyclePhase.Design, off, done), Name).Rows;

        Assert.Contains(rows, r => r.Cells[1] == "Coordinated vulnerability disclosure policy published" && r.Cells[3] == "Turned off" && r.Cells[4] == "Reason: By the customer");
        Assert.Contains(rows, r => r.Cells[1] == "Threat model made" && r.Cells[3] == "Done" && r.Cells[4] == "Anna 16-11-2026: STRIDE");
    }

    [Fact]
    public void Optional_tests_nobody_chose_are_left_out()
        => Assert.DoesNotContain(LifecycleReport.Sheet(Lifecycle(LifecyclePhase.Verification), Name).Rows, r => r.Cells[1] == "Penetration test done");

    [Fact]
    public void The_text_says_when_no_phase_change_is_recorded()
        => Assert.Contains("None recorded", LifecycleReport.Text("Acme Portal", Lifecycle(LifecyclePhase.Planning), Name));

    // ---------------------------------------------------------- The audit check (R5)

    [Fact]
    public void The_audit_check_names_what_is_missing_in_the_current_phase()
    {
        var line = AuditReadiness.Lifecycle(Lifecycle(LifecyclePhase.Verification)).Lines.Single();

        Assert.False(line.Ok);
        Assert.Contains("SBOM uploaded", line.Detail);
        Assert.Equal(ReadinessFix.Overview, line.Fix);
    }
}
