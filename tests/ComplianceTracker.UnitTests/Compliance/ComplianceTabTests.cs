using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects.Tabs;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Http;

namespace ComplianceTracker.UnitTests.Compliance;

// Edge-case unit tests of US-232: the Compliance tab's sub-tabs, the handler that picks the list or a requirement's page,
// and the address it reads. Arrange / Act / Assert.
public class ComplianceTabTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private const string Cra = "CRA – Regulation (EU) 2024/2847";
    private const string Nis2 = "NIS2 – Directive (EU) 2022/2555";

    private static FrameworkCompliance Framework(string name, params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Domain.Frameworks.Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        var rows = codes.Select(c => new RequirementCoverage(framework.LatestVersion.AddRequirement(c, "Title " + c, null), [])).ToList();
        return new FrameworkCompliance(framework, framework.LatestVersion, rows);
    }

    private static ProjectCompliance Compliance(params FrameworkCompliance[] frameworks) => new(frameworks, []);

    private static ProjectShellView Shell()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small, new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        var project = Project.Create(organization.Id, "Acme Portal", null, "A customer portal", null, LifecyclePhase.Planning);
        return new ProjectShellView(project, organization, ProjectTabs.Compliance, null);
    }

    private static ProjectTabContext Context(string? sub, string? req, string query = "")
        => new(Shell(), sub, req, new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)), "da");

    // ---------------------------------------------------------- The sub-tabs (R3, R4)

    [Fact]
    public void Without_a_sub_tab_the_first_framework_is_shown()
    {
        var compliance = Compliance(Framework(Cra, "I.1"), Framework(Nis2, "21.2"));

        Assert.True(ComplianceAddresses.IsKnownSub(compliance, null));
        Assert.Same(compliance.Frameworks[0], ComplianceAddresses.Active(compliance, null));
    }

    [Fact]
    public void A_frameworks_slug_is_its_sub_tab()
    {
        var compliance = Compliance(Framework(Cra, "I.1"), Framework(Nis2, "21.2"));
        var nis2 = FrameworkNames.Slug(Nis2);

        Assert.True(ComplianceAddresses.IsKnownSub(compliance, nis2));
        Assert.Same(compliance.Frameworks[1], ComplianceAddresses.Active(compliance, nis2));
    }

    [Theory]
    [InlineData("unknown-framework")]
    [InlineData("CRA")] // the slug is lower case
    [InlineData("")]
    public void An_unknown_sub_tab_is_not_found(string sub)
        => Assert.False(ComplianceAddresses.IsKnownSub(Compliance(Framework(Cra, "I.1"), Framework(Nis2, "21.2")), sub));

    [Fact]
    public void All_is_a_sub_tab_only_with_more_than_one_framework()
    {
        Assert.True(ComplianceAddresses.IsKnownSub(Compliance(Framework(Cra), Framework(Nis2)), ComplianceAddresses.AllSub));
        Assert.False(ComplianceAddresses.IsKnownSub(Compliance(Framework(Cra)), ComplianceAddresses.AllSub));
    }

    [Fact]
    public void A_project_without_frameworks_has_only_the_tab_itself()
    {
        var empty = Compliance();

        Assert.True(ComplianceAddresses.IsKnownSub(empty, null));
        Assert.False(ComplianceAddresses.IsKnownSub(empty, "cra"));
        Assert.Null(ComplianceAddresses.Active(empty, null));
    }

    // ---------------------------------------------------------- The address

    [Fact]
    public void The_address_gives_the_filter_the_search_and_the_language()
    {
        var address = ComplianceProjectTab.AddressFrom(Context("cra", null, "?show=NEEDS-ACTION&q=%20session"));

        Assert.Equal(new ComplianceAddress("cra", ComplianceFilters.NeedsAction, " session", "da"), address);
    }

    [Theory]
    [InlineData("?show=sideways")]
    [InlineData("?show=")]
    [InlineData("")]
    public void An_unknown_or_missing_filter_shows_everything_and_an_empty_search_is_none(string query)
    {
        var address = ComplianceProjectTab.AddressFrom(Context(null, null, query + (query.Length > 0 ? "&" : "?") + "q="));

        Assert.Equal(ComplianceFilters.All, address.Filter);
        Assert.Null(address.Search);
    }

    // ---------------------------------------------------------- The handler

    private sealed class Loader<TRequest, TResult>(TResult? result) : IProjectTabLoader<TRequest, TResult> where TResult : class
    {
        public List<TRequest> Requests { get; } = [];

        public Task<TResult?> LoadAsync(Guid projectId, TRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Without_a_requirement_the_list_is_loaded()
    {
        var compliance = Compliance(Framework(Cra, "I.1"));
        var list = new Loader<ComplianceTabRequest, ComplianceTabDto>(new ComplianceTabDto(compliance, [], null, DateOnly.FromDateTime(Monday)));
        var page = new Loader<RequirementPageRequest, RequirementPageDto>(null);

        var view = await new ComplianceProjectTab(list, page).LoadAsync(Context("cra", null), CancellationToken.None);

        var model = Assert.IsType<ComplianceTabViewModel>(view!.Model);
        Assert.Equal("_ComplianceTab", view.Partial);
        Assert.NotNull(model.List);
        Assert.Null(model.Page);
        Assert.Equal([new ComplianceTabRequest("cra")], list.Requests);
        Assert.Empty(page.Requests);
    }

    [Fact]
    public async Task A_requirement_in_the_address_loads_its_page_with_the_sub_tab()
    {
        var cra = Framework(Cra, "I.2(d)");
        var compliance = Compliance(cra);
        var list = new Loader<ComplianceTabRequest, ComplianceTabDto>(null);
        var page = new Loader<RequirementPageRequest, RequirementPageDto>(new RequirementPageDto(compliance, cra, cra.Requirements[0], [], []));

        var view = await new ComplianceProjectTab(list, page).LoadAsync(Context("cra", "I.2(d)"), CancellationToken.None);

        var model = Assert.IsType<ComplianceTabViewModel>(view!.Model);
        Assert.NotNull(model.Page);
        Assert.Same(cra, model.ActiveCompliance); // the sub-tab above the page is shown active
        Assert.Equal([new RequirementPageRequest("cra", "I.2(d)")], page.Requests);
        Assert.Empty(list.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("X.99")]
    public async Task What_the_loader_does_not_find_is_not_found(string? requirement)
    {
        var tab = new ComplianceProjectTab(
            new Loader<ComplianceTabRequest, ComplianceTabDto>(null), new Loader<RequirementPageRequest, RequirementPageDto>(null));

        Assert.Null(await tab.LoadAsync(Context("unknown-framework", requirement), CancellationToken.None));
    }

    // ---------------------------------------------------------- The model

    [Fact]
    public void The_All_sub_tab_shows_every_framework_and_none_is_active_alone()
    {
        var compliance = Compliance(Framework(Cra), Framework(Nis2));

        var model = new ComplianceTabViewModel(Shell(), compliance, new ComplianceAddress(ComplianceAddresses.AllSub, ComplianceFilters.All, null, "en"));

        Assert.True(model.ShowsAll);
        Assert.False(new ComplianceTabViewModel(Shell(), Compliance(Framework(Cra)), model.Address).ShowsAll);
    }

    [Fact]
    public void A_requirements_page_offers_no_frameworks_to_add()
    {
        var cra = Framework(Cra, "I.1");
        var model = new ComplianceTabViewModel(Shell(), Compliance(cra), new ComplianceAddress(null, ComplianceFilters.All, null, "en"))
        {
            Page = new RequirementPageDto(Compliance(cra), cra, cra.Requirements[0], [], [])
        };

        Assert.Empty(model.CanBeAdded);
        Assert.Null(model.Reassessment);
    }
}
