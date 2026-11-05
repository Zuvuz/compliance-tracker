using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Mappings;

// Unit tests of EvidenceLink and EvidenceLinkSuggestion (US-059): one evidence linked to many requirements, history kept,
// and the AI's suggestions that a person decides on. Every test follows Arrange / Act / Assert.
public class EvidenceLinkTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid EvidenceId = Guid.NewGuid();
    private static readonly Guid Requirement = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_link_keeps_who_linked_it_and_their_role()
    {
        // Arrange / Act
        var link = EvidenceLink.Create(Project, EvidenceId, Requirement, Anna, UserRole.Developer, At);

        // Assert
        Assert.Equal((Project, EvidenceId, Requirement), (link.ProjectId, link.EvidenceId, link.RequirementId));
        Assert.Equal((Anna, UserRole.Developer, At), (link.LinkedById, link.LinkedRole, link.LinkedAt));
        Assert.Equal(LinkSource.Manual, link.Source);
        Assert.True(link.IsActive);
    }

    [Fact]
    public void Unlinking_keeps_the_link_as_history()
    {
        // Arrange
        var link = EvidenceLink.Create(Project, EvidenceId, Requirement, Anna, null, At);

        // Act
        link.Remove(Anna, At.AddDays(1));

        // Assert
        Assert.False(link.IsActive);
        Assert.Equal(At.AddDays(1), link.RemovedAt);
        Assert.Throws<DomainException>(() => link.Remove(Anna, At.AddDays(2)));
    }

    [Fact]
    public void Carrying_a_link_to_a_new_version_keeps_who_and_when()
    {
        // Arrange
        var link = EvidenceLink.Create(Project, EvidenceId, Requirement, Anna, UserRole.Admin, At, LinkSource.AiApproved);
        var newRequirement = Guid.NewGuid();

        // Act
        var carried = link.CarryTo(newRequirement);

        // Assert
        Assert.Equal(newRequirement, carried.RequirementId);
        Assert.Equal((EvidenceId, Anna, UserRole.Admin, At, LinkSource.AiApproved),
            (carried.EvidenceId, carried.LinkedById, carried.LinkedRole, carried.LinkedAt, carried.Source));
        Assert.NotEqual(link.Id, carried.Id);
    }

    [Fact]
    public void A_suggestion_is_approved_once()
    {
        // Arrange
        var suggestion = EvidenceLinkSuggestion.Create(Project, EvidenceId, Requirement, 0.81234, 1, "nomic-embed-text", "evidence-v1", At);

        // Act
        suggestion.Approve(Anna, At, UserRole.Developer);

        // Assert
        Assert.Equal(SuggestionStatus.Approved, suggestion.Status);
        Assert.Equal(0.8123, suggestion.Score);
        Assert.Throws<DomainException>(() => suggestion.Approve(Anna, At));
    }

    [Fact]
    public void Rejecting_a_suggestion_needs_a_reason()
    {
        // Arrange
        var suggestion = EvidenceLinkSuggestion.Create(Project, EvidenceId, Requirement, 0.4, 1, "nomic-embed-text", "evidence-v1", At);

        // Act / Assert
        Assert.Throws<DomainException>(() => suggestion.Reject(null, null, Anna, At));
        Assert.Throws<DomainException>(() => suggestion.Reject(RejectionReason.Other, " ", Anna, At));
        suggestion.Reject(RejectionReason.NotRelevant, null, Anna, At);
        Assert.Equal(SuggestionStatus.Rejected, suggestion.Status);
    }

    [Fact]
    public void A_suggestion_keeps_the_limit_it_was_made_with()
    {
        // Arrange / Act (US-105 R5)
        var evidence = EvidenceLinkSuggestion.Create(Project, EvidenceId, Requirement, 0.8, 1, "nomic-embed-text", "evidence-v1", At, 0.55);
        var story = MappingSuggestion.Create(Project, Guid.NewGuid(), Requirement, 0.8, 1, "nomic-embed-text", "mapping-v1", At, 0.5);

        // Assert
        Assert.Equal(0.55, evidence.MinimumScore);
        Assert.Equal(0.5, story.MinimumScore);
    }
}
