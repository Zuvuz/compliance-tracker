using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.Domain.Tests.Isms;

// US-210: the management review, its snapshot, decisions and locked minutes. Every test follows Arrange / Act / Assert.
public class ManagementReviewTests
{
    private static readonly Guid Organisation = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 12, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 12, 10);

    private static ManagementReview Planned() => ManagementReview.Plan(Organisation, Today, ["Anna", "CEO"], Monday);

    private static ManagementReview Held()
    {
        var review = Planned();
        review.Hold(Today, [new ReviewInput("9.3.2 d", "Objectives", ["Coverage: 86 %"]), new ReviewInput("9.3.2 d", "Audit results", [])], Today);
        return review;
    }

    // ---------------------------------------------------------- R1

    [Fact]
    public void A_review_needs_a_date_and_a_participant()
    {
        Assert.Throws<DomainException>(() => ManagementReview.Plan(Organisation, null, ["Anna"], Monday));
        var error = Assert.Throws<DomainException>(() => ManagementReview.Plan(Organisation, Today, [" ", null], Monday));
        Assert.Equal("Name at least one participant", error.Message);
    }

    [Fact]
    public void Participants_are_trimmed_and_named_once()
        => Assert.Equal(["Anna", "CEO"], ManagementReview.Plan(Organisation, Today, [" Anna", "anna", "CEO"], Monday).Participants);

    // ---------------------------------------------------------- R2, R3

    [Fact]
    public void An_empty_input_says_nothing_to_report_when_it_is_kept()
    {
        var review = Held();

        Assert.Equal([ReviewInput.NothingToReport], review.Inputs.Single(i => i.Title == "Audit results").Lines);
        Assert.Equal(ReviewStatus.Held, review.Status);
    }

    [Fact]
    public void A_review_cannot_be_held_in_the_future_or_twice()
    {
        Assert.Throws<DomainException>(() => Planned().Hold(Today.AddDays(1), [], Today));
        Assert.Throws<DomainException>(() => Held().Hold(Today, [], Today));
    }

    [Fact]
    public void After_it_is_held_the_date_and_inputs_are_kept()
    {
        var review = Held();

        Assert.Throws<DomainException>(() => review.Change(Today.AddDays(1), ["Anna"]));
        Assert.Throws<DomainException>(() => review.Describe("new feedback", null));
    }

    // ---------------------------------------------------------- R4

    [Fact]
    public void A_decision_can_have_an_action_with_an_owner_and_a_deadline()
    {
        var review = Held();

        var decision = review.Decide("Hire a security engineer", Anna, new DateOnly(2027, 3, 1), Monday);

        Assert.True(decision.IsOpen);
        Assert.Throws<DomainException>(() => review.Decide("Half an action", Anna, null, Monday));
    }

    [Fact]
    public void A_decision_needs_the_review_to_be_held()
        => Assert.Throws<DomainException>(() => Planned().Decide("Hire", null, null, Monday));

    // ---------------------------------------------------------- R5

    [Fact]
    public void Locked_minutes_cannot_be_changed_but_actions_can_be_done()
    {
        var review = Held();
        var decision = review.Decide("Hire a security engineer", Anna, new DateOnly(2027, 3, 1), Monday);
        review.Lock(Monday);

        Assert.Throws<DomainException>(() => review.Decide("Another", null, null, Monday));
        Assert.Throws<DomainException>(() => review.Lock(Monday));
        review.MarkActionDone(decision.Id, Monday);
        Assert.False(decision.IsOpen);
    }

    [Fact]
    public void A_decision_without_an_action_cannot_be_done()
    {
        var review = Held();
        var decision = review.Decide("Keep the scope", null, null, Monday);

        Assert.Throws<DomainException>(() => review.MarkActionDone(decision.Id, Monday));
    }

    // ---------------------------------------------------------- R6

    [Fact]
    public void A_review_is_due_twelve_months_after_the_last_one_held()
    {
        var review = ManagementReview.Plan(Organisation, new DateOnly(2025, 11, 1), ["Anna"], Monday);
        review.Hold(new DateOnly(2025, 11, 1), [], new DateOnly(2025, 11, 1));

        Assert.False(ManagementReview.ReviewIsDue([review], new DateOnly(2026, 10, 31)));
        Assert.True(ManagementReview.ReviewIsDue([review], new DateOnly(2026, 11, 21)));
    }

    [Fact]
    public void Without_any_review_one_is_due()
        => Assert.True(ManagementReview.ReviewIsDue([Planned()], Today));
}
