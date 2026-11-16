using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.UnitTests.Projects;

// US-193: edge cases of the AI's checklist items for uploaded frameworks. Arrange / Act / Assert.
public class ChecklistSuggestionTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Framework = Guid.NewGuid();

    [Fact]
    public void The_best_phase_over_the_limit_is_chosen()
    {
        var best = ChecklistSuggestionRules.BestPhase([0.1, 0.2, 0.3, 0.9, 0.4, 0.0], 0.5);

        Assert.Equal((LifecyclePhase.Verification, 0.9), best);
    }

    [Fact]
    public void A_requirement_that_fits_no_phase_well_enough_is_left_out()
        => Assert.Null(ChecklistSuggestionRules.BestPhase([0.1, 0.2, 0.3, 0.4, 0.45, 0.0], 0.5));

    [Fact]
    public void Without_a_limit_nothing_at_all_is_still_left_out()
        => Assert.Null(ChecklistSuggestionRules.BestPhase([0, 0, 0, 0, 0, 0], 0));

    [Fact]
    public void A_score_per_phase_is_needed()
        => Assert.Throws<ArgumentException>(() => ChecklistSuggestionRules.BestPhase([0.9], 0.5));

    [Fact]
    public void Items_whose_requirement_left_the_framework_are_found_and_others_kept()
    {
        var gone = OrganisationChecklistItem.Create(Acme, LifecyclePhase.Verification, "Penetration test done", Framework, "Customer", "SR-12", Monday);
        var kept = OrganisationChecklistItem.Create(Acme, LifecyclePhase.Verification, "Yearly review", Framework, "Customer", "SR-14", Monday);
        var own = OrganisationChecklistItem.Create(Acme, LifecyclePhase.Design, "Architecture review", null, null, "", Monday);
        var other = OrganisationChecklistItem.Create(Acme, LifecyclePhase.Design, "Other", Guid.NewGuid(), "Other", "SR-12", Monday);

        var found = ChecklistSuggestionRules.Gone([gone, kept, own, other], Framework, new HashSet<string> { "SR-14" });

        Assert.Same(gone, Assert.Single(found));
    }

    [Fact]
    public void A_suggestion_is_decided_once()
    {
        var suggestion = ChecklistSuggestion.Create(Acme, Framework, "SR-12", LifecyclePhase.Verification, "Penetration testing", 0.9, "fake", Monday);
        suggestion.Reject(Guid.NewGuid(), Monday);

        Assert.Throws<DomainException>(() => suggestion.Approve(Guid.NewGuid(), Guid.NewGuid(), Monday));
    }

    [Fact]
    public void A_long_requirement_title_is_cut_to_the_item_length_and_the_score_kept_between_0_and_1()
    {
        var suggestion = ChecklistSuggestion.Create(Acme, Framework, "SR-12", LifecyclePhase.Design, new string('a', 500), 1.7, "fake", Monday);

        Assert.Equal(OrganisationChecklistItem.MaxTextLength, suggestion.Text.Length);
        Assert.Equal(1, suggestion.Score);
    }

    [Fact]
    public void Every_phase_has_a_description_that_starts_with_its_name()
        => Assert.All(Enum.GetValues<LifecyclePhase>(), p => Assert.StartsWith(p.ToString(), PhaseDescriptions.For(p)));
}
