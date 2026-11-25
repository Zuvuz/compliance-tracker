using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;

namespace ComplianceTracker.UnitTests.Isms;

// US-210 R2: the input pack has every input of ISO/IEC 27001 9.3.2. Every test follows Arrange / Act / Assert.
public class ReviewInputPackTests
{
    private static ReviewFacts Empty() => new([], [], [], [], [], [], null, [], null);

    [Fact]
    public void Every_input_of_the_standard_is_there_even_when_empty()
    {
        var pack = ManagementReviewService.Compose(Empty());

        Assert.Equal(
            ["Actions from the last review", "Changes in the context", "Needs of the interested parties", "Objectives", "Nonconformities", "Audit results",
             "Feedback from interested parties", "Risks", "Opportunities for improvement"],
            pack.Select(i => i.Title));
        Assert.All(pack, i => Assert.Equal([ReviewInput.NothingToReport], i.Lines));
    }

    [Fact]
    public void The_written_inputs_are_split_into_lines()
    {
        var pack = ManagementReviewService.Compose(Empty() with { Feedback = "Customers ask for ISO 27001\n\n  The auditor praised the log  " });

        Assert.Equal(["Customers ask for ISO 27001", "The auditor praised the log"], pack.Single(i => i.Title == "Feedback from interested parties").Lines);
    }

    [Fact]
    public void Gathered_lines_are_kept_as_they_are()
    {
        var pack = ManagementReviewService.Compose(Empty() with { Risks = ["Ransomware (Critical, Webshop)"] });

        Assert.Equal(["Ransomware (Critical, Webshop)"], pack.Single(i => i.Title == "Risks").Lines);
        Assert.Equal("9.3.2 f", pack.Single(i => i.Title == "Risks").Clause);
    }
}
