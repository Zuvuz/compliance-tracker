using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.UnitTests.Risks;

// US-158: edge cases of the evidence given while approving a measure. Arrange / Act / Assert.
public class MeasureEvidenceTests
{
    [Theory]
    [InlineData(null, 0, false)]                          // nothing
    [InlineData("   ", 0, false)]                         // a blank link
    [InlineData("https://example.com/policy", 0, true)]   // a link
    [InlineData(null, 10, true)]                          // a file
    public void Evidence_is_given_by_a_link_or_a_file(string? url, int bytes, bool given)
        => Assert.Equal(given, new MeasureEvidence("Policy", url, "policy.pdf", new byte[bytes], null).IsGiven);

    [Fact]
    public void A_title_alone_is_not_evidence()
        => Assert.False(new MeasureEvidence("Policy", null, null, null, null).IsGiven);

    [Fact]
    public void A_link_and_a_file_together_are_refused()
        => Assert.Equal("Give a link or a file, not both",
            Assert.Throws<DomainException>(() => new MeasureEvidence("x", "https://example.com", "x.pdf", [1, 2], null).Check()).Message);

    [Theory]
    [InlineData("https://example.com", 0)]
    [InlineData(null, 3)]
    [InlineData("  ", 3)]
    public void A_link_or_a_file_alone_passes_the_check(string? url, int bytes)
    {
        var evidence = new MeasureEvidence("x", url, "x.pdf", new byte[bytes], null);

        var error = Record.Exception(evidence.Check);

        Assert.Null(error);
    }
}
