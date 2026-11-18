using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Mappings;

// US-201: which group a piece of evidence is in, and a group's attention. Edge cases. Arrange / Act / Assert.
public class EvidenceGroupTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();

    [Theory]
    [InlineData(EvidenceSource.Uploaded, EvidenceGroup.Files)]
    [InlineData(EvidenceSource.TestResults, EvidenceGroup.TestResults)]
    [InlineData(EvidenceSource.Sbom, EvidenceGroup.Sboms)]
    [InlineData(EvidenceSource.Scan, EvidenceGroup.Scans)]
    [InlineData(EvidenceSource.Report, EvidenceGroup.Reports)]
    public void A_file_is_in_the_group_of_where_it_came_from(EvidenceSource source, EvidenceGroup group)
        => Assert.Equal(group, File("x.pdf", source).Group);

    [Fact]
    public void A_link_is_in_links_and_is_uploaded()
    {
        var link = Evidence.ForLink(Project, "PR 42", "https://github.com/acme/portal/pull/42", null, Anna, null, Monday);

        Assert.Equal((EvidenceSource.Uploaded, EvidenceGroup.Links), (link.Source, link.Group));
    }

    [Fact]
    public void A_diagram_is_in_diagrams_but_an_sbom_stays_an_sbom()
    {
        var diagram = File("flow.drawio", EvidenceSource.Uploaded);
        diagram.DiagramUnreadable("Not a diagram we can read");
        var sbom = File("sbom.json", EvidenceSource.Sbom);
        sbom.DiagramUnreadable("Not a diagram we can read");

        Assert.Equal(EvidenceGroup.Diagrams, diagram.Group);
        Assert.Equal(EvidenceGroup.Sboms, sbom.Group);
    }

    [Fact]
    public void The_groups_follow_the_fixed_order_and_leave_out_empty_ones()
    {
        var rows = new[]
        {
            Row(File("a.pdf", EvidenceSource.Sbom)),
            Row(Evidence.ForLink(Project, "PR", "https://example.com/pr", null, Anna, null, Monday)),
            Row(File("b.pdf", EvidenceSource.Uploaded)),
            Row(File("c.pdf", EvidenceSource.Uploaded))
        };

        var groups = EvidenceGroupView.Of(rows);

        Assert.Equal(["Files", "Links", "SBOMs"], groups.Select(g => g.Name));
        Assert.Equal([2, 1, 1], groups.Select(g => g.Rows.Count));
        Assert.Equal("sboms", groups[2].Slug);
    }

    [Fact]
    public void A_group_needs_attention_when_something_in_it_is_expired()
    {
        var group = EvidenceGroupView.Of([Row(File("a.pdf", EvidenceSource.Uploaded)), Row(File("b.pdf", EvidenceSource.Uploaded), expired: true)]).Single();

        Assert.Equal((0, 1, true), (group.Waiting, group.Expired, group.NeedsAttention));
    }

    [Fact]
    public void A_group_with_nothing_waiting_or_expired_needs_no_attention()
        => Assert.False(EvidenceGroupView.Of([Row(File("a.pdf", EvidenceSource.Uploaded))]).Single().NeedsAttention);

    [Theory]
    [InlineData(EvidenceGroup.TestResults, "Test results", "test-results")]
    [InlineData(EvidenceGroup.OrganisationDocuments, "Organisation documents", "organisation-documents")]
    [InlineData(EvidenceGroup.Files, "Files", "files")]
    public void A_group_has_a_name_and_a_slug(EvidenceGroup group, string name, string slug)
        => Assert.Equal((name, slug), (EvidenceGroups.NameOf(group), EvidenceGroups.SlugOf(group)));

    private static Evidence File(string name, EvidenceSource source)
        => Evidence.ForFile(Project, name, EvidenceFile.Create(name, Encoding.ASCII.GetBytes("%PDF-1.7\n")), null, Anna, null, Monday, source);

    private static EvidenceRow Row(Evidence evidence, bool expired = false) => new(evidence, expired, [], []);
}
