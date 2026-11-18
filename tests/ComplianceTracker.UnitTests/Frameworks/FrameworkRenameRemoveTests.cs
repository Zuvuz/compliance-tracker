using System.Text;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.UnitTests.Frameworks;

// #52: an uploaded framework can be renamed and removed, and a table file's columns are guessed. Edge cases. Arrange / Act / Assert.
public class FrameworkRenameRemoveTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    [Fact]
    public void A_framework_is_renamed_and_the_old_name_is_returned()
    {
        var framework = Uploaded("Fake ISO");

        var old = framework.Rename("  ISO27001 – Demo ");

        Assert.Equal("Fake ISO", old);
        Assert.Equal("ISO27001 – Demo", framework.Name);
        Assert.Equal("iso27001", FrameworkSlugs.Slug(framework.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Fake ISO")]
    public void An_empty_or_the_same_name_is_refused(string name)
        => Assert.Throws<DomainException>(() => Uploaded("Fake ISO").Rename(name));

    [Fact]
    public void A_name_longer_than_the_limit_is_refused()
        => Assert.Throws<DomainException>(() => Uploaded("Fake ISO").Rename(new string('a', Framework.MaxNameLength + 1)));

    [Fact]
    public void A_framework_is_removed_once_and_remembers_who_and_when()
    {
        var framework = Uploaded("Fake ISO");

        framework.Remove(Anna, Monday);

        Assert.True(framework.IsRemoved);
        Assert.Equal((Anna, Monday), (framework.RemovedById!.Value, framework.RemovedAt!.Value));
        Assert.Throws<DomainException>(() => framework.Remove(Anna, Monday));
    }

    // ---------------------------------------------------------- Guessing the columns

    [Fact]
    public void Usual_column_names_are_guessed()
        => Assert.Equal(new TableColumns("Code", "Title", "Text"), TableColumns.Guess(["Code", "Title", "Text"]));

    [Fact]
    public void Danish_names_and_other_orders_are_guessed()
        => Assert.Equal(new TableColumns("Krav-ID", "Titel", "Beskrivelse", "Forælder"),
            TableColumns.Guess(["Beskrivelse", "Forælder", "Titel", "Krav-ID"]));

    [Fact]
    public void Names_that_start_like_a_known_name_are_guessed()
        => Assert.Equal(new TableColumns("Control ID", "Control name", "Requirement text"),
            TableColumns.Guess(["Control ID", "Control name", "Requirement text"]));

    [Fact]
    public void Unknown_names_give_the_first_three_columns()
        => Assert.Equal(new TableColumns("A", "B", "C"), TableColumns.Guess(["A", "B", "C", "D"]));

    [Fact]
    public void Fewer_than_three_columns_reuse_the_last()
        => Assert.Equal(new TableColumns("Code", "Title", "Title"), TableColumns.Guess(["Code", "Title"]));

    private static Framework Uploaded(string name)
        => Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.csv", Encoding.UTF8.GetBytes("Code;Title;Text\nA.1;x;y\n")), Anna, Monday);
}
