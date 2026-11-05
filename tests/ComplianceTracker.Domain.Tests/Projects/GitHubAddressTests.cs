using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Projects;

namespace ComplianceTracker.Domain.Tests.Projects;

// Unit tests of GitHubAddress and linking to a GitHub Project (US-107). Every test follows Arrange / Act / Assert.
public class GitHubAddressTests
{
    [Theory]
    [InlineData("https://github.com/orgs/acme/projects/3", GitHubAddressKind.Project, "acme", null, 3)]
    [InlineData("github.com/orgs/acme/projects/3/", GitHubAddressKind.Project, "acme", null, 3)]
    [InlineData("acme/portal", GitHubAddressKind.Repository, "acme", "portal", null)]
    [InlineData("https://github.com/acme/portal", GitHubAddressKind.Repository, "acme", "portal", null)]
    [InlineData("github.com/acme", GitHubAddressKind.Organization, "acme", null, null)]
    public void An_address_is_read_as_a_repository_a_project_or_an_organisation(
        string address, GitHubAddressKind kind, string owner, string? repository, int? number)
    {
        // Arrange / Act
        var parsed = GitHubAddress.Parse(address);

        // Assert
        Assert.Equal(new GitHubAddress(kind, owner, repository, number), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("github.com/orgs/acme/projects/zero")]
    [InlineData("github.com/orgs/acme/projects/0")]
    [InlineData("a/b/c")]
    [InlineData("acme!/portal")]
    public void Anything_else_is_refused(string address)
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => GitHubAddress.Parse(address));
    }

    [Fact]
    public void Linking_an_organisation_waits_for_a_project_to_be_chosen()
    {
        // Arrange
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", null, "A portal", null, LifecyclePhase.Development);

        // Act
        project.LinkGitHubProject("acme", null, null, Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        Assert.True(project.GitHub!.NeedsProjectChoice);
        project.ChooseGitHubProject(3, "Customer portal");
        Assert.Equal("github.com/orgs/acme/projects/3", project.GitHub.ProjectAddress);
        Assert.False(project.GitHub.NeedsProjectChoice);
    }

    [Fact]
    public void The_token_stays_within_the_same_organisation_only()
    {
        // Arrange
        var project = Project.Create(Guid.NewGuid(), "Acme Portal", null, "A portal", null, LifecyclePhase.Development);
        project.LinkGitHubProject("acme", 3, "Customer portal", Guid.NewGuid(), DateTime.UtcNow);
        project.GitHub!.SetToken("encrypted", DateTime.UtcNow);

        // Act
        project.LinkGitHubProject("acme", 4, "Mobile app", Guid.NewGuid(), DateTime.UtcNow);
        var sameOrganisation = project.GitHub.HasToken;
        project.LinkGitHubProject("beta", 1, "Other", Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        Assert.True(sameOrganisation);
        Assert.False(project.GitHub.HasToken);
    }
}
