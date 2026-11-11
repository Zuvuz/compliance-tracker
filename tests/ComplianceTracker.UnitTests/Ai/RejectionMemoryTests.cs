using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Ai;

// Unit tests of RejectionMemory.Note (US-145 R4). Every test follows Arrange / Act / Assert.
public class RejectionMemoryTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Nothing_changed_gives_no_note()
        => Assert.Null(RejectionMemory.Note("a|r", "a|r", "Anna", Monday, RejectionReason.NotRelevant, "document"));

    [Fact]
    public void A_rejection_without_fingerprint_gives_no_note()
        => Assert.Null(RejectionMemory.Note(null, "a|r", "Anna", Monday, RejectionReason.NotRelevant, "document"));

    [Fact]
    public void A_changed_item_names_the_person_the_reason_and_the_item()
    {
        var note = RejectionMemory.Note("a|r", "b|r", "Anna", Monday, RejectionReason.NotRelevant, "document");

        Assert.StartsWith("Rejected before by Anna on ", note);
        Assert.Contains("(NotRelevant)", note);
        Assert.EndsWith("The document has changed since.", note);
    }

    [Fact]
    public void A_changed_requirement_says_so()
        => Assert.EndsWith("The requirement's text has changed since.", RejectionMemory.Note("a|r", "a|s", "Anna", Monday, null, "evidence"));

    [Fact]
    public void Both_changed_says_both()
        => Assert.EndsWith("The evidence and the requirement's text have changed since.", RejectionMemory.Note("a|r", "b|s", "Anna", Monday, null, "evidence"));

    [Fact]
    public void An_unknown_date_and_reason_still_give_a_note()
    {
        var note = RejectionMemory.Note("a|r", "b|r", "Unknown", null, null, "story");

        Assert.Equal("Rejected before by Unknown on ?. The story has changed since.", note);
    }
}
