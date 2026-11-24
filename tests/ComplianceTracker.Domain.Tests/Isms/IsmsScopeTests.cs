using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-208: the ISMS scope, the projects inside and outside it, its versions and the interested parties. Every test follows
// Arrange / Act / Assert.
public class IsmsScopeTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Webshop = Guid.NewGuid();
    private static readonly Guid Intranet = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

    private static IsmsScope Described(string text = "Our SaaS products")
    {
        var scope = IsmsScope.Create(Organisation, Monday);
        scope.Describe(text, null, null, null, null, null, null, Monday);
        return scope;
    }

    // ---------------------------------------------------------- R1: the description

    [Fact]
    public void Describing_returns_the_fields_that_changed()
    {
        var scope = IsmsScope.Create(Organisation, Monday);

        var changed = scope.Describe("Our SaaS products", "Aarhus", null, null, "NIS2", Anna, new DateOnly(2027, 11, 1), Monday.AddMinutes(1));

        Assert.Equal(["Scope", "Locations", "External issues", "Owner", "Next review"], changed);
    }

    [Fact]
    public void Describing_with_the_same_values_changes_nothing()
    {
        var scope = Described();
        var before = scope.ChangedAt;

        var changed = scope.Describe(" Our SaaS products ", "", "  ", null, null, null, null, Monday.AddHours(1));

        Assert.Empty(changed);
        Assert.Equal(before, scope.ChangedAt);
    }

    [Fact]
    public void A_scope_text_over_the_limit_is_refused()
    {
        var scope = IsmsScope.Create(Organisation, Monday);

        var error = Assert.Throws<DomainException>(() => scope.Describe(new string('x', IsmsScope.MaxTextLength + 1), null, null, null, null, null, null, Monday));

        Assert.Equal($"The scope must be {IsmsScope.MaxTextLength} characters or fewer", error.Message);
    }

    [Fact]
    public void A_scope_text_exactly_at_the_limit_is_kept()
    {
        var scope = IsmsScope.Create(Organisation, Monday);

        scope.Describe(new string('x', IsmsScope.MaxTextLength), null, null, null, null, null, null, Monday);

        Assert.Equal(IsmsScope.MaxTextLength, scope.Text.Length);
    }

    // ---------------------------------------------------------- R2, R3: projects inside and outside

    [Fact]
    public void A_project_nobody_has_decided_on_is_not_inside()
    {
        var scope = Described();

        Assert.Equal((ScopeState.NotDecided, false), (scope.StateOf(Webshop), scope.IsInside(Webshop)));
    }

    [Fact]
    public void A_project_outside_needs_a_reason()
    {
        var scope = Described();

        var error = Assert.Throws<DomainException>(() => scope.Exclude(Intranet, "   ", Monday));

        Assert.Equal("Give the reason the project is outside the scope", error.Message);
    }

    [Fact]
    public void Moving_a_project_inside_removes_its_reason()
    {
        var scope = Described();
        scope.Exclude(Intranet, "shut down in March", Monday);

        scope.Include(Intranet, Monday.AddMinutes(1));

        var project = Assert.Single(scope.Projects);
        Assert.Equal((true, (string?)null), (project.IsInside, project.Reason));
    }

    [Fact]
    public void Putting_a_project_inside_twice_changes_nothing_the_second_time()
    {
        var scope = Described();
        scope.Include(Webshop, Monday);

        var changed = scope.Include(Webshop, Monday.AddMinutes(1));

        Assert.False(changed);
        Assert.Single(scope.Projects);
    }

    [Fact]
    public void The_same_reason_again_changes_nothing_but_a_new_reason_does()
    {
        var scope = Described();
        scope.Exclude(Intranet, "shut down in March", Monday);

        Assert.Equal((false, true), (scope.Exclude(Intranet, "shut down in March", Monday), scope.Exclude(Intranet, "sold", Monday)));
    }

    // ---------------------------------------------------------- 4.4: versions

    [Fact]
    public void Approving_makes_version_1_and_then_version_2_after_a_change()
    {
        var scope = Described();
        scope.Include(Webshop, Monday);

        var first = scope.Approve(Anna, Monday.AddMinutes(1));
        scope.Exclude(Intranet, "sold", Monday.AddMinutes(2));
        var second = scope.Approve(Anna, Monday.AddMinutes(3));

        Assert.Equal((1, 2), (first, second));
    }

    [Fact]
    public void An_approved_scope_cannot_be_approved_again_without_a_change()
    {
        var scope = Described();
        scope.Include(Webshop, Monday);
        scope.Approve(Anna, Monday.AddMinutes(1));

        var error = Assert.Throws<DomainException>(() => scope.Approve(Anna, Monday.AddMinutes(2)));

        Assert.Equal("This version of the scope is approved already", error.Message);
    }

    [Fact]
    public void A_scope_without_text_cannot_be_approved()
    {
        var scope = IsmsScope.Create(Organisation, Monday);
        scope.Include(Webshop, Monday);

        var error = Assert.Throws<DomainException>(() => scope.Approve(Anna, Monday));

        Assert.Equal("Describe the scope before it is approved", error.Message);
    }

    [Fact]
    public void A_scope_with_only_projects_outside_cannot_be_approved()
    {
        var scope = Described();
        scope.Exclude(Intranet, "sold", Monday);

        var error = Assert.Throws<DomainException>(() => scope.Approve(Anna, Monday));

        Assert.Equal("Put at least one project inside the scope before it is approved", error.Message);
    }

    [Fact]
    public void A_change_after_approval_shows_the_scope_changed_since()
    {
        var scope = Described();
        scope.Include(Webshop, Monday);
        scope.Approve(Anna, Monday.AddMinutes(1));

        scope.Describe("Our SaaS products and the support desk", null, null, null, null, null, null, Monday.AddMinutes(2));

        Assert.True(scope.ChangedSinceApproval);
    }

    // ---------------------------------------------------------- R5: review

    [Theory]
    [InlineData("2026-11-22", false)] // tomorrow
    [InlineData("2026-11-21", false)] // today
    [InlineData("2026-11-20", true)]  // yesterday
    public void The_review_is_overdue_only_after_its_date(string due, bool overdue)
    {
        var scope = IsmsScope.Create(Organisation, Monday);
        scope.Describe("Our SaaS products", null, null, null, null, null, DateOnly.Parse(due), Monday);

        Assert.Equal(overdue, scope.IsReviewOverdue(new DateOnly(2026, 11, 21)));
    }

    [Fact]
    public void A_scope_without_a_review_date_is_never_overdue()
    {
        Assert.False(Described().IsReviewOverdue(new DateOnly(2099, 1, 1)));
    }

    // ---------------------------------------------------------- R4: interested parties

    [Fact]
    public void Empty_requirement_rows_are_left_out()
    {
        var party = InterestedParty.Create(Organisation, "Customers", InterestedPartyKind.Customer,
            [("ISO 27001 certificate", true), ("", true), ("  ", false), ("Data in the EU", false)], Monday);

        Assert.Equal([("ISO 27001 certificate", true, 0), ("Data in the EU", false, 1)],
            party.Requirements.Select(r => (r.Text, r.Handled, r.Order)));
        Assert.Equal(1, party.Handled);
    }

    [Fact]
    public void A_party_without_requirements_is_refused()
    {
        var error = Assert.Throws<DomainException>(() => InterestedParty.Create(Organisation, "Customers", InterestedPartyKind.Customer, [("", true)], Monday));

        Assert.Equal("Give at least one requirement of the interested party", error.Message);
    }

    [Fact]
    public void The_same_requirement_twice_is_refused_whatever_the_case()
    {
        var error = Assert.Throws<DomainException>(() => InterestedParty.Create(Organisation, "Customers", InterestedPartyKind.Customer,
            [("ISO 27001", true), ("iso 27001", false)], Monday));

        Assert.Equal("The same requirement is given twice", error.Message);
    }

    [Fact]
    public void A_kind_that_does_not_exist_is_refused()
    {
        var error = Assert.Throws<DomainException>(() => InterestedParty.Create(Organisation, "Customers", (InterestedPartyKind)99, [("ISO 27001", true)], Monday));

        Assert.Equal("Choose the kind of interested party", error.Message);
    }

    [Fact]
    public void More_requirements_than_the_limit_are_refused()
    {
        var many = Enumerable.Range(1, InterestedParty.MaxRequirements + 1).Select(i => ((string?)$"Requirement {i}", false));

        var error = Assert.Throws<DomainException>(() => InterestedParty.Create(Organisation, "Customers", InterestedPartyKind.Customer, many, Monday));

        Assert.Equal($"An interested party can have at most {InterestedParty.MaxRequirements} requirements", error.Message);
    }

    [Fact]
    public void A_removed_party_cannot_be_changed_or_removed_again()
    {
        var party = InterestedParty.Create(Organisation, "Customers", InterestedPartyKind.Customer, [("ISO 27001", true)], Monday);
        party.Remove(Monday);

        Assert.Throws<DomainException>(() => party.Change("Clients", InterestedPartyKind.Customer, [("ISO 27001", true)]));
        Assert.Throws<DomainException>(() => party.Remove(Monday));
    }
}
