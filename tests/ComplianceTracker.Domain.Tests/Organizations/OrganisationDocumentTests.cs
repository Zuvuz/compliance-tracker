using System.Text;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Domain.Tests.Organizations;

// Edge-case unit tests of US-136: the organisation's documents and the projects' evidence made from them.
// Arrange / Act / Assert.
public class OrganisationDocumentTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Cra = Guid.NewGuid();

    private static EvidenceFile File(string text = "v1") => EvidenceFile.Create("policy.pdf", Encoding.ASCII.GetBytes("%PDF-1.4 " + text));

    private static OrganisationDocument Document(string? title = "Access control policy", string? version = "v1", DateOnly? reviewDueOn = null)
        => OrganisationDocument.Create(Guid.NewGuid(), OrganisationDocumentKind.Policy, title, Anna, version, File(), reviewDueOn, Anna, Monday);

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_document_needs_a_title(string? title)
    {
        // Act / Assert
        Assert.Equal("Give the document a title", Assert.Throws<DomainException>(() => Document(title)).Message);
    }

    [Fact]
    public void A_title_longer_than_evidence_titles_is_refused()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Document(new string('t', Evidence.MaxTitleLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_document_needs_a_version(string? version)
    {
        // Act / Assert
        Assert.Equal("Give the version, e.g. v1", Assert.Throws<DomainException>(() => Document(version: version)).Message);
    }

    [Fact]
    public void A_new_version_keeps_the_old_file_and_the_links()
    {
        // Arrange
        var document = Document();
        var link = document.Link(Cra, "I.2(d)", Anna, Monday);
        var oldSha = document.Sha256;

        // Act
        document.UploadNewVersion(File("v2"), "v2", Anna, Monday.AddDays(1));

        // Assert
        Assert.Equal("v2", document.Version);
        Assert.NotEqual(oldSha, document.Sha256);
        Assert.Equal(oldSha, document.Versions.Single().Sha256);
        Assert.Equal("v1", document.Versions.Single().Version);
        Assert.Same(link, document.ActiveLinks.Single());
    }

    [Fact]
    public void A_new_version_needs_another_name()
    {
        // Arrange
        var document = Document();

        // Act / Assert
        Assert.Throws<DomainException>(() => document.UploadNewVersion(File("v2"), " v1 ", Anna, Monday));
    }

    [Fact]
    public void The_same_requirement_is_linked_once_whatever_the_case()
    {
        // Arrange
        var document = Document();
        document.Link(Cra, "I.2(d)", Anna, Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => document.Link(Cra, "i.2(D)", Anna, Monday));
    }

    [Fact]
    public void The_same_code_in_another_framework_is_another_link()
    {
        // Arrange
        var document = Document();
        document.Link(Cra, "5.1", Anna, Monday);

        // Act
        document.Link(Guid.NewGuid(), "5.1", Anna, Monday);

        // Assert
        Assert.Equal(2, document.ActiveLinks.Count);
    }

    [Fact]
    public void An_unlinked_requirement_can_be_linked_again()
    {
        // Arrange
        var document = Document();
        var first = document.Link(Cra, "I.2(d)", Anna, Monday);
        document.Unlink(first.Id, Anna, Monday);

        // Act
        document.Link(Cra, "I.2(d)", Anna, Monday.AddHours(1));

        // Assert
        Assert.Single(document.ActiveLinks);
        Assert.Equal(2, document.Links.Count);
    }

    [Fact]
    public void A_removed_document_loses_its_links_and_cannot_change()
    {
        // Arrange
        var document = Document();
        document.Link(Cra, "I.2(d)", Anna, Monday);

        // Act
        document.Remove(Anna, Monday);

        // Assert
        Assert.Empty(document.ActiveLinks);
        Assert.Throws<DomainException>(() => document.Link(Cra, "I.2(e)", Anna, Monday));
        Assert.Throws<DomainException>(() => document.UploadNewVersion(File("v2"), "v2", Anna, Monday));
        Assert.Throws<DomainException>(() => document.Remove(Anna, Monday));
    }

    [Fact]
    public void The_review_is_due_the_day_after_the_review_date()
    {
        // Arrange
        var document = Document(reviewDueOn: new DateOnly(2026, 10, 1));

        // Act / Assert
        Assert.False(document.IsReviewDue(new DateOnly(2026, 10, 1)));
        Assert.True(document.IsReviewDue(new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public void A_document_without_a_review_date_is_never_due_and_a_removed_one_neither()
    {
        // Arrange
        var undated = Document();
        var removed = Document(reviewDueOn: new DateOnly(2020, 1, 1));
        removed.Remove(Anna, Monday);

        // Act / Assert
        Assert.False(undated.IsReviewDue(new DateOnly(2030, 1, 1)));
        Assert.False(removed.IsReviewDue(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void The_next_review_must_be_after_today()
    {
        // Arrange
        var document = Document(reviewDueOn: new DateOnly(2026, 10, 1));

        // Act / Assert
        Assert.Throws<DomainException>(() => document.MarkReviewed(DateOnly.FromDateTime(Monday), Anna, Monday));
        document.MarkReviewed(new DateOnly(2027, 11, 9), Anna, Monday);
        Assert.Equal(new DateOnly(2027, 11, 9), document.ReviewDueOn);
        Assert.Equal(Anna, document.LastReviewedById);
    }

    [Fact]
    public void The_projects_evidence_follows_the_document()
    {
        // Arrange
        var document = Document();
        var evidence = Evidence.FromOrganisation(Guid.NewGuid(), document, Monday);

        // Act
        document.UploadNewVersion(File("v2"), "v2", Anna, Monday);
        evidence.Follow(document);

        // Assert
        Assert.True(evidence.IsFromOrganisation);
        Assert.True(evidence.IsFile);
        Assert.Equal(document.Sha256, evidence.Sha256);
        Assert.Equal(document.Title, evidence.Title);
    }

    [Fact]
    public void Evidence_cannot_follow_another_document()
    {
        // Arrange
        var evidence = Evidence.FromOrganisation(Guid.NewGuid(), Document(), Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => evidence.Follow(Document("Other policy")));
    }
}
