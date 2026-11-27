using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ComplianceTracker.UnitTests.Web;

// Edge-case unit tests of US-228: where a form on a project page goes back to. Arrange / Act / Assert.
public class ReturnAddressTests
{
    private static readonly Guid Project = Guid.NewGuid();

    private static HttpRequest Form(params (string Name, string Value)[] fields)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(fields.ToDictionary(f => f.Name, f => new StringValues(f.Value)));
        return context.Request;
    }

    private static HttpRequest Query(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    private static Dictionary<string, object?> Route(ReturnAddress address)
        => address.RouteValues(Project).Where(v => v.Value is not null).ToDictionary(v => v.Key, v => v.Value);

    // ---------------------------------------------------------- R1: each page

    [Fact]
    public void The_compliance_tab_goes_back_to_its_framework_and_filter()
        => Assert.Equal(new Dictionary<string, object?> { ["id"] = Project, ["tab"] = "compliance", ["sub"] = "cra", ["show"] = "not-covered" },
            Route(ReturnAddress.From(Form(("back", "compliance"), ("sub", "cra"), ("show", "not-covered")))!));

    [Fact]
    public void A_requirement_page_goes_back_to_the_requirement_and_without_a_framework_to_all()
    {
        Assert.Equal(new Dictionary<string, object?> { ["id"] = Project, ["tab"] = "compliance", ["sub"] = "cra", ["req"] = "I.2(d)" },
            Route(ReturnAddress.From(Form(("back", "requirement"), ("sub", "cra"), ("req", " I.2(d) "), ("show", "not-covered")))!));
        Assert.Equal(ComplianceFilters.AllSub, ReturnAddress.ForRequirement(null, "A.5.1").RouteValues(Project)["sub"]);
    }

    [Fact]
    public void A_requirement_page_without_a_code_is_the_framework_list()
        => Assert.Equal(ReturnPage.Compliance, ReturnAddress.Parse("requirement", "cra", "  ", null)!.Page);

    [Fact]
    public void The_link_page_keeps_its_framework_requirement_and_own_filter_and_all_means_no_framework()
    {
        Assert.Equal(new Dictionary<string, object?> { ["id"] = Project, ["tab"] = "link", ["sub"] = "cra", ["req"] = "I.1", ["show"] = "has-suggestions" },
            Route(ReturnAddress.From(Form(("back", "link"), ("sub", "cra"), ("req", "I.1"), ("show", "HAS-SUGGESTIONS")))!));
        Assert.Equal("not-covered", ReturnAddress.Parse("link", null, null, "needs-review")!.Show); // not a Link page filter
        Assert.Null(ReturnAddress.Parse("link", ComplianceFilters.AllSub, null, null)!.RouteValues(Project)["sub"]);
    }

    [Fact]
    public void The_evidence_tab_goes_back_to_one_piece_of_evidence_or_the_list()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id.ToString(), ReturnAddress.Parse("evidence", id.ToString(), null, null)!.RouteValues(Project)["sub"]);
        Assert.Null(ReturnAddress.Parse("evidence", "not-an-id", null, null)!.Sub);
        Assert.Equal("evidence", ReturnAddress.ForEvidence().RouteValues(Project)["tab"]);
    }

    // ---------------------------------------------------------- R3: nothing unknown is used

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gap")]       // the gap analysis page's own "back" (US-188), not a project page
    [InlineData("https://evil.example/")]
    [InlineData("risks")]     // a tab the address does not know
    public void A_missing_or_unknown_page_is_no_address(string? back)
        => Assert.Null(ReturnAddress.Parse(back, "cra", "I.1", null));

    [Fact]
    public void A_framework_longer_than_a_framework_can_be_and_an_unknown_compliance_filter_are_dropped()
    {
        var address = ReturnAddress.Parse("compliance", new string('x', ReturnAddress.MaxSubLength + 1), null, "everything")!;

        Assert.Null(address.Sub);
        Assert.Null(address.Show);
        Assert.Null(ReturnAddress.Parse("compliance", "cra", null, "all")!.Show);
    }

    [Fact]
    public void The_page_name_is_read_whatever_its_case_and_spaces()
        => Assert.Equal(ReturnPage.Link, ReturnAddress.Parse(" LINK ", null, null, null)!.Page);

    [Fact]
    public void A_get_request_reads_the_address_from_the_query()
        => Assert.Equal(ReturnPage.Link, ReturnAddress.From(Query("?back=link&sub=cra&req=I.1"))!.Page);

    // ---------------------------------------------------------- Forms without an address: as before

    [Fact]
    public void A_form_without_an_address_goes_to_the_requirement_page_when_it_sent_a_code_and_otherwise_to_the_list()
    {
        Assert.Equal(ReturnPage.Requirement, ReturnAddress.FallbackToCompliance(null, "I.1", "not-covered").Page);
        Assert.Equal(ReturnAddress.ForCompliance("cra", "not-covered"), ReturnAddress.FallbackToCompliance("cra", "", "not-covered"));
    }

    // ---------------------------------------------------------- R4, R5: carried along

    [Fact]
    public void The_fields_are_the_ones_a_form_sends_and_reading_them_again_gives_the_same_address()
    {
        var address = ReturnAddress.ForLink("cra", "I.1", "has-suggestions");
        var fields = address.Fields();

        Assert.Equal(["back", "sub", "req", "show"], fields.Keys);
        Assert.Equal(address, ReturnAddress.From(Form(fields.Select(f => (f.Key, f.Value)).ToArray())));
        Assert.Equal(["back"], ReturnAddress.ForEvidence().Fields().Keys); // empty fields are left out
    }

    [Fact]
    public void An_undo_carries_the_address_and_replaces_older_fields()
    {
        var values = new Dictionary<string, string> { ["projectId"] = Project.ToString(), ["sub"] = "", ["show"] = "old" };

        ReturnAddress.ForRequirement("cra", "I.1").AddTo(values);

        Assert.Equal(new Dictionary<string, string> { ["projectId"] = Project.ToString(), ["back"] = "requirement", ["sub"] = "cra", ["req"] = "I.1" }, values);
    }
}
