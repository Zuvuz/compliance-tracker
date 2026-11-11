using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Domain.Tests.Common;

// Unit tests of US-145: a rejection holds until what was judged changes. Every test follows Arrange / Act / Assert.
public class RejectionFingerprintTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    // ---------------------------------------------------------- Fingerprint

    [Fact]
    public void The_same_text_gives_the_same_fingerprint()
        => Assert.Equal(Fingerprint.Of("Access policy", "v1"), Fingerprint.Of("Access policy", "v1"));

    [Fact]
    public void Case_and_spacing_are_not_a_change()
        => Assert.Equal(Fingerprint.Of("Access  policy ", "V1"), Fingerprint.Of("access policy", "v1"));

    [Fact]
    public void Another_word_is_a_change()
        => Assert.NotEqual(Fingerprint.Of("Access policy"), Fingerprint.Of("Access procedure"));

    [Fact]
    public void Moving_text_between_parts_is_a_change()
        => Assert.NotEqual(Fingerprint.Of("a b", "c"), Fingerprint.Of("a", "b c"));

    [Fact]
    public void Null_parts_count_as_empty()
        => Assert.Equal(Fingerprint.Of("Title", null), Fingerprint.Of("Title", ""));

    [Fact]
    public void A_fingerprint_is_short_and_has_no_separator()
    {
        var fingerprint = Fingerprint.Of("x");

        Assert.Equal(16, fingerprint.Length);
        Assert.DoesNotContain('|', fingerprint);
    }

    [Theory]
    [InlineData("a|r", "a|r", null)]
    [InlineData(null, "a|r", null)]
    [InlineData("a|r", "b|r", "item")]
    [InlineData("a|r", "a|s", "requirement")]
    [InlineData("a|r", "b|s", "both")]
    [InlineData("old-format", "a|r", "both")]
    public void What_changed_names_the_side(string? stored, string current, string? expected)
        => Assert.Equal(expected, Fingerprint.WhatChanged(stored, current));

    // ---------------------------------------------------------- A rejection

    private static MappingSuggestion Pending()
        => MappingSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.8, 1, "fake-embed", "v1", Monday);

    private static MappingSuggestion Rejected()
    {
        var suggestion = Pending();
        suggestion.Reject(RejectionReason.NotRelevant, null, Anna, Monday);
        return suggestion;
    }

    [Fact]
    public void An_unchanged_pair_is_still_rejected()
    {
        var suggestion = Rejected();
        suggestion.JudgedOn("a|r");

        Assert.True(suggestion.StillRejects("a|r"));
    }

    [Fact]
    public void A_changed_pair_is_no_longer_rejected()
    {
        var suggestion = Rejected();
        suggestion.JudgedOn("a|r");

        Assert.False(suggestion.StillRejects("b|r"));
    }

    [Fact]
    public void A_rejection_from_before_fingerprints_holds()
        => Assert.True(Rejected().StillRejects("anything|at all"));

    [Fact]
    public void A_pending_suggestion_rejects_nothing()
        => Assert.False(Pending().StillRejects("a|r"));

    [Fact]
    public void Only_a_rejection_keeps_what_was_judged()
        => Assert.Throws<DomainException>(() => Pending().JudgedOn("a|r"));

    [Fact]
    public void Undoing_a_rejection_stops_it_blocking()
    {
        var suggestion = Rejected();
        suggestion.JudgedOn("a|r");

        suggestion.UndoRejection();

        Assert.Equal(SuggestionStatus.RejectionUndone, suggestion.Status);
        Assert.False(suggestion.StillRejects("a|r"));
    }

    [Fact]
    public void A_rejection_is_undone_only_once()
    {
        var suggestion = Rejected();
        suggestion.UndoRejection();

        Assert.Throws<DomainException>(suggestion.UndoRejection);
    }

    [Fact]
    public void An_approved_suggestion_cannot_be_undone()
    {
        var suggestion = Pending();
        suggestion.Approve(Anna, Monday);

        Assert.Throws<DomainException>(suggestion.UndoRejection);
    }

    [Fact]
    public void A_blank_note_is_no_note_and_a_long_one_is_cut()
    {
        var suggestion = Pending();

        suggestion.NoteEarlierRejection("  ");
        Assert.Null(suggestion.EarlierRejection);

        suggestion.NoteEarlierRejection(new string('x', 400));
        Assert.Equal(300, suggestion.EarlierRejection!.Length);
    }

    // The same rules hold for the other kinds of suggestions
    [Fact]
    public void A_document_suggestion_comes_back_when_the_document_changes()
    {
        var suggestion = DocumentLinkSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "I.2(d)", 0.8, 1, "fake-embed", "document-v1", Monday);
        suggestion.Reject(RejectionReason.NotRelevant, null, Anna, Monday);
        suggestion.JudgedOn("sha1|req");

        Assert.True(suggestion.StillRejects("sha1|req"));
        Assert.False(suggestion.StillRejects("sha2|req"));
    }

    [Fact]
    public void A_measure_suggestion_comes_back_when_the_risk_changes()
    {
        var suggestion = RiskMeasureSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 0.8, "fake-embed", Monday);
        suggestion.Reject(RejectionReason.NotRelevant, null, Anna, Monday);
        suggestion.JudgedOn("risk|req");

        Assert.False(suggestion.StillRejects("risk2|req"));
    }

    [Fact]
    public void An_evidence_story_suggestion_can_be_undone()
    {
        var suggestion = EvidenceStorySuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.8, 1, "fake-embed", Monday);
        suggestion.Reject(RejectionReason.NotRelevant, null, Anna, Monday);

        suggestion.UndoRejection();

        Assert.Equal(SuggestionStatus.RejectionUndone, suggestion.Status);
    }
}
