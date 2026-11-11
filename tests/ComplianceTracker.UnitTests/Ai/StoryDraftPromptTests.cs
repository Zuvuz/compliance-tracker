using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.UnitTests.Ai;

// Edge-case unit tests of US-106 R3: how the AI's draft answer is read. Arrange / Act / Assert.
public class StoryDraftPromptTests
{
    private static Requirement Requirement()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return Framework.Create(Guid.NewGuid(), "CRA", CatalogFile.Create("cra.pdf", bytes), Guid.NewGuid(), DateTime.UtcNow)
            .LatestVersion.AddRequirement("I.2(m)", "Secure removal of data", "Users can remove all data.");
    }

    [Fact]
    public void The_labelled_parts_are_read_also_over_several_lines()
    {
        // Arrange
        const string answer = "TITLE: Delete my account\nROLE: User\nSTORY: As a user, I want to delete my account,\nso that nothing is left.\nRULES: R1: data is gone\nR2: settings are gone\nEXAMPLES: Anna deletes her account";

        // Act
        var (title, role, story, rules, examples) = StoryDraftPrompt.Parse(answer, Requirement());

        // Assert
        Assert.Equal("Delete my account", title);
        Assert.Equal("User", role);
        Assert.Equal("As a user, I want to delete my account,\nso that nothing is left.", story);
        Assert.Equal("R1: data is gone\nR2: settings are gone", rules);
        Assert.Equal("Anna deletes her account", examples);
    }

    [Fact]
    public void An_answer_without_labels_is_the_story_with_a_title_from_the_requirement()
    {
        // Act
        var (title, _, story, rules, _) = StoryDraftPrompt.Parse("As a user, I want my data removed.", Requirement());

        // Assert
        Assert.Equal("Meet I.2(m) Secure removal of data", title);
        Assert.Equal("As a user, I want my data removed.", story);
        Assert.Equal("", rules);
    }

    [Fact]
    public void A_colon_inside_a_part_is_not_a_new_label()
    {
        // Act
        var (_, _, story, _, _) = StoryDraftPrompt.Parse("STORY: As a user, I want this: removal.", Requirement());

        // Assert
        Assert.Equal("As a user, I want this: removal.", story);
    }

    [Fact]
    public void The_prompt_holds_the_requirement_and_the_purpose()
    {
        // Act
        var prompt = StoryDraftPrompt.For(Requirement(), "A portal for customers");

        // Assert
        Assert.Contains("Requirement I.2(m): Secure removal of data", prompt);
        Assert.Contains("A portal for customers", prompt);
        Assert.StartsWith(StoryDraftPrompt.Marker, prompt);
    }
}
