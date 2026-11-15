using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.UnitTests.Projects;

// US-192: edge cases of the organisation's own checklist items. Arrange / Act / Assert.
public class OrganisationChecklistItemTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Acme = Guid.NewGuid();

    private static OrganisationChecklistItem Own(string text = "Architecture review held", string reference = "")
        => OrganisationChecklistItem.Create(Acme, LifecyclePhase.Design, text, null, null, reference, Monday);

    [Fact]
    public void An_own_item_is_a_manual_item_with_its_source()
    {
        var item = Own(reference: "ISMS-PRO-12").AsItem();

        Assert.Equal(ChecklistCheck.Manual, item.Check);
        Assert.Equal("Own policy ISMS-PRO-12", item.Source);
        Assert.StartsWith(OrganisationChecklistItem.KeyPrefix, item.Key);
    }

    [Fact]
    public void Its_key_never_clashes_with_a_built_in_one()
        => Assert.Null(BuiltInChecklist.Find(Own().Key));

    [Fact]
    public void An_item_tied_to_a_framework_needs_its_name()
        => Assert.Throws<DomainException>(() => OrganisationChecklistItem.Create(Acme, LifecyclePhase.Design, "A", Guid.NewGuid(), " ", "", Monday));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_item_needs_a_text(string text)
        => Assert.Throws<DomainException>(() => Own(text));

    [Fact]
    public void A_very_long_text_or_reference_is_refused()
    {
        Assert.Throws<DomainException>(() => Own(new string('a', OrganisationChecklistItem.MaxTextLength + 1)));
        Assert.Throws<DomainException>(() => Own(reference: new string('a', OrganisationChecklistItem.MaxReferenceLength + 1)));
    }

    [Fact]
    public void A_phase_is_needed()
        => Assert.Throws<DomainException>(() => OrganisationChecklistItem.Create(Acme, null, "A", null, null, "", Monday));

    [Fact]
    public void A_removed_item_cannot_change_or_be_removed_again()
    {
        var item = Own();
        item.Remove(Monday);

        Assert.False(item.IsActive);
        Assert.Throws<DomainException>(() => item.Change("B", ""));
        Assert.Throws<DomainException>(() => item.Remove(Monday));
    }

    [Fact]
    public void Own_policy_applies_to_every_project_and_a_framework_item_only_where_followed()
    {
        var project = Project.Create(Acme, "Acme Portal", "", "Purpose", "", LifecyclePhase.Design);
        var framework = OrganisationChecklistItem.Create(Acme, LifecyclePhase.Design, "Triage rota", Guid.NewGuid(), "CRA", "", Monday);

        Assert.True(Own().AppliesTo(project));
        Assert.False(framework.AppliesTo(project));
    }
}
