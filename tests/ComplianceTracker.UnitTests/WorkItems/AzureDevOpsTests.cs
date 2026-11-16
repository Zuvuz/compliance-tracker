using System.Text.Json;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Infrastructure.WorkItems;

namespace ComplianceTracker.UnitTests.WorkItems;

// US-030: edge cases of the Azure DevOps link, the query, reading a work item and the HTML fields. Arrange / Act / Assert.
public class AzureDevOpsTests
{
    // ---------------------------------------------------------- The link (R1)

    [Theory]
    [InlineData("acme", "acme")]
    [InlineData(" https://dev.azure.com/acme/ ", "acme")]
    [InlineData("dev.azure.com/acme-it", "acme-it")]
    public void The_organisation_is_taken_with_or_without_the_address(string input, string expected)
        => Assert.Equal(expected, AzureDevOpsLink.CleanOrganization(input));

    [Theory]
    [InlineData("")]
    [InlineData("acme corp")]
    [InlineData("-acme")]
    [InlineData("acme/portal")]
    public void A_wrong_organisation_is_refused(string input)
        => Assert.Throws<DomainException>(() => AzureDevOpsLink.CleanOrganization(input));

    [Theory]
    [InlineData("Portal")]
    [InlineData("Customer Portal 2")]
    [InlineData("portal.web_v2")]
    public void A_project_name_with_spaces_dots_and_underscores_is_fine(string name)
        => Assert.Equal(name, AzureDevOpsLink.CleanProject(name));

    [Theory]
    [InlineData("")]
    [InlineData("Portal/Web")]
    [InlineData(".hidden")]
    public void A_wrong_project_name_is_refused(string name)
        => Assert.Throws<DomainException>(() => AzureDevOpsLink.CleanProject(name));

    [Fact]
    public void The_work_item_link_points_at_dev_azure_com()
        => Assert.Equal("https://dev.azure.com/acme/Customer%20Portal/_workitems/edit/120", AzureDevOpsLink.WorkItemUrl("acme", "Customer Portal", 120));

    [Fact]
    public void Linking_without_a_token_is_refused()
    {
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", "", "Test purpose", "", LifecyclePhase.Planning);

        Assert.Throws<DomainException>(() => project.LinkAzureDevOps("acme", "Portal", null, " ", Guid.NewGuid(), DateTime.UtcNow));
        Assert.Null(project.AzureDevOps);
    }

    [Fact]
    public void A_too_long_area_path_is_refused()
    {
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", "", "Test purpose", "", LifecyclePhase.Planning);

        Assert.Throws<DomainException>(() => project.LinkAzureDevOps("acme", "Portal", new string('a', 401), "x", Guid.NewGuid(), DateTime.UtcNow));
    }

    // ---------------------------------------------------------- The query (R4)

    [Fact]
    public void The_query_reads_only_user_story_types()
    {
        var wiql = AzureDevOpsWorkItemSource.Wiql("Portal", null);

        Assert.Contains("[System.WorkItemType] IN ('User Story', 'Product Backlog Item', 'Requirement')", wiql);
        Assert.DoesNotContain("AreaPath", wiql);
    }

    [Fact]
    public void A_quote_in_the_area_path_cannot_change_the_query()
    {
        var wiql = AzureDevOpsWorkItemSource.Wiql("Portal", "Web' OR 1=1 --");

        Assert.Contains("[System.AreaPath] UNDER 'Web'' OR 1=1 --'", wiql);
    }

    // ---------------------------------------------------------- One work item (R4, R5, R7)

    private static AzureWorkItem Item(int id, string? criteria, string state = "Active", string type = "User Story", string tags = "", string description = "")
        => new(id, new Dictionary<string, JsonElement>
        {
            ["System.Title"] = JsonSerializer.SerializeToElement("Sign in with MitID"),
            ["System.WorkItemType"] = JsonSerializer.SerializeToElement(type),
            ["System.State"] = JsonSerializer.SerializeToElement(state),
            ["System.Tags"] = JsonSerializer.SerializeToElement(tags),
            ["System.Description"] = JsonSerializer.SerializeToElement(description),
            ["Microsoft.VSTS.Common.AcceptanceCriteria"] = criteria is null ? JsonSerializer.SerializeToElement<string?>(null) : JsonSerializer.SerializeToElement(criteria)
        });

    [Fact]
    public void An_item_with_acceptance_criteria_is_read_with_its_link_type_and_tags()
    {
        var (item, skipped) = AzureDevOpsWorkItemSource.ToWorkItem("acme", "Portal", Item(120, "<div>Given a citizen</div>", tags: "security; mitid"));

        Assert.Null(skipped);
        Assert.Equal("acme/Portal#120", item!.ExternalReference);
        Assert.Equal("https://dev.azure.com/acme/Portal/_workitems/edit/120", item.Url);
        Assert.Equal("Given a citizen", item.AcceptanceCriteria);
        Assert.Equal("User Story, security, mitid", item.Labels);
        Assert.False(item.IsClosed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<div><br></div>")]
    public void An_item_without_acceptance_criteria_is_skipped_with_the_reason(string? criteria)
    {
        var (item, skipped) = AzureDevOpsWorkItemSource.ToWorkItem("acme", "Portal", Item(122, criteria));

        Assert.Null(item);
        Assert.Equal(("acme/Portal#122", "no acceptance criteria"), (skipped!.ExternalReference, skipped.Reason));
    }

    [Theory]
    [InlineData("Closed", true)]
    [InlineData("Done", true)]
    [InlineData("Resolved", true)]
    [InlineData("Removed", true)]
    [InlineData("Active", false)]
    [InlineData("New", false)]
    [InlineData("Committed", false)]
    public void The_done_states_of_every_process_close_the_item(string state, bool closed)
        => Assert.Equal(closed, AzureDevOpsWorkItemSource.IsClosed(state));

    // ---------------------------------------------------------- HTML fields (R5)

    [Fact]
    public void Html_becomes_plain_text_with_line_breaks_and_bullets()
    {
        var text = AzureDevOpsWorkItemSource.PlainText("<div>Given a citizen&nbsp;with MitID</div><ul><li>When she signs in</li><li>Then it &amp; works</li></ul><script>x</script>");

        Assert.Equal("Given a citizen with MitID\n- When she signs in\n- Then it & works\nx", text);
    }

    [Fact]
    public void Empty_html_is_empty_text()
        => Assert.Equal("", AzureDevOpsWorkItemSource.PlainText("  "));

    // ---------------------------------------------------------- Two sources (R11)

    [Fact]
    public void Two_sources_add_up_and_keep_a_failure()
    {
        var total = new SyncResult(Added: 1, Skipped: 1).Add(SyncResult.Failed("The token was refused by Azure DevOps"));

        Assert.Equal(1, total.Added);
        Assert.Equal("The token was refused by Azure DevOps", total.Error);
    }

    [Fact]
    public void Two_failures_are_both_kept()
        => Assert.Equal("a; b", SyncResult.Failed("a").Add(SyncResult.Failed("b")).Error);
}
