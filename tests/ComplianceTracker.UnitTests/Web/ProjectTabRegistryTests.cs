using ComplianceTracker.Application.Projects.Tabs;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ComplianceTracker.UnitTests.Web;

// Unit tests of the project page's tab registry and the Risks tab's address (US-229). Every test follows Arrange / Act / Assert.
public class ProjectTabRegistryTests
{
    private sealed class Tab(string slug, ProjectTabAreas areas) : IProjectTab
    {
        public string Slug => slug;
        public ProjectTabAreas Areas => areas;
        public Task<ProjectTabView?> LoadAsync(ProjectTabContext context, CancellationToken cancellationToken) => Task.FromResult<ProjectTabView?>(null);
    }

    private static ProjectTabContext Context(string query)
        => new(null!, null, null, new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)));

    // ---------------------------------------------------------- The registry

    [Fact]
    public void A_tab_is_found_on_the_pages_it_exists_on()
    {
        var both = new Tab(ProjectTabs.Risks, ProjectTabAreas.Both);
        var projectOnly = new Tab(ProjectTabs.Scans, ProjectTabAreas.Project);
        var registry = new ProjectTabRegistry([both, projectOnly]);

        Assert.Same(both, registry.Find(ProjectTabs.Risks, organisationArea: false));
        Assert.Same(both, registry.Find(ProjectTabs.Risks, organisationArea: true));
        Assert.Same(projectOnly, registry.Find(ProjectTabs.Scans, organisationArea: false));
        Assert.Null(registry.Find(ProjectTabs.Scans, organisationArea: true));
    }

    [Fact]
    public void A_tab_without_a_handler_is_not_found_so_the_old_code_shows_it()
        => Assert.Null(new ProjectTabRegistry([]).Find(ProjectTabs.Compliance, organisationArea: false));

    // US-235 R4: the project's Overview and the area's Overview share the slug, one handler per page
    [Fact]
    public void Two_handlers_may_share_a_slug_on_different_pages()
    {
        var project = new Tab(ProjectTabs.Overview, ProjectTabAreas.Project);
        var area = new Tab(ProjectTabs.Overview, ProjectTabAreas.OrganisationArea);
        var registry = new ProjectTabRegistry([project, area]);

        Assert.Same(project, registry.Find(ProjectTabs.Overview, organisationArea: false));
        Assert.Same(area, registry.Find(ProjectTabs.Overview, organisationArea: true));
    }

    [Theory]
    [InlineData(ProjectTabAreas.Project, ProjectTabAreas.Both)]
    [InlineData(ProjectTabAreas.Both, ProjectTabAreas.OrganisationArea)]
    [InlineData(ProjectTabAreas.Project, ProjectTabAreas.Project)]
    public void Two_handlers_for_the_same_slug_on_the_same_page_are_refused(ProjectTabAreas first, ProjectTabAreas second)
        => Assert.Throws<InvalidOperationException>(() => new ProjectTabRegistry([new Tab(ProjectTabs.Risks, first), new Tab(ProjectTabs.Risks, second)]));

    // Every handler in the web app names a real tab of the pages it says it is on, and no two clash
    [Fact]
    public void Every_handler_names_a_tab_of_its_pages()
    {
        var handlers = typeof(IProjectTab).Assembly.GetTypes()
            .Where(t => typeof(IProjectTab).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .Select(t => (IProjectTab)Activator.CreateInstance(t, t.GetConstructors().Single().GetParameters().Select(_ => (object?)null).ToArray())!)
            .ToList();

        Assert.Contains(handlers, h => h is RisksProjectTab);
        _ = new ProjectTabRegistry(handlers);
        foreach (var handler in handlers)
        {
            if (handler.Areas.HasFlag(ProjectTabAreas.Project))
            {
                Assert.Equal(handler.Slug, ProjectTabs.Find(handler.Slug, organisationArea: false));
            }

            if (handler.Areas.HasFlag(ProjectTabAreas.OrganisationArea))
            {
                Assert.Equal(handler.Slug, ProjectTabs.Find(handler.Slug, organisationArea: true));
            }
        }
    }

    // ---------------------------------------------------------- The address

    [Fact]
    public void A_value_is_the_first_one_and_an_empty_one_is_no_value()
    {
        var context = Context("?status=Closed&status=Open&level=&due=%20");

        Assert.Equal("Closed", context.Value("status"));
        Assert.Null(context.Value("level"));
        Assert.Null(context.Value("due"));
        Assert.Null(context.Value("missing"));
    }

    [Fact]
    public void The_Risks_tab_reads_every_filter_from_the_address()
    {
        var risk = Guid.NewGuid();

        var request = RisksProjectTab.RequestFrom(Context($"?status=Accepted&level=High&due=1&nothing=1&cell=3x4&matrix=AFTER&risk={risk}"));

        Assert.Equal(new RiskTabRequest("Accepted", "High", true, true, (3, 4), true, risk), request);
    }

    [Fact]
    public void Without_filters_the_Risks_tab_shows_the_register_as_it_is()
        => Assert.Equal(new RiskTabRequest(), RisksProjectTab.RequestFrom(Context("")));

    [Theory]
    [InlineData("?due=yes&nothing=true")]
    [InlineData("?cell=6x1")]
    [InlineData("?cell=3-4")]
    [InlineData("?matrix=sideways")]
    [InlineData("?risk=not-a-guid")]
    public void An_unknown_flag_cell_matrix_or_risk_is_no_filter(string query)
        => Assert.Equal(new RiskTabRequest(), RisksProjectTab.RequestFrom(Context(query)));
}
