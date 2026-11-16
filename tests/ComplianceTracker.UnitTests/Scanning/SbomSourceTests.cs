using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;

namespace ComplianceTracker.UnitTests.Scanning;

// US-148: edge cases of where an SBOM came from, a pipeline's commit, and how often GitHub is asked. Arrange / Act / Assert.
public class SbomSourceTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static Sbom NewSbom(SbomSource source = SbomSource.Upload, Guid? userId = null)
        => Sbom.Create(Guid.NewGuid(), "2.1.0", "CycloneDX 1.6", "sbom.cdx.json", [1, 2, 3],
            [new SbomComponentRead("openssl", "3.0.1", "pkg:generic/openssl@3.0.1", null, null)], userId, Monday, source);

    [Fact]
    public void An_uploaded_sbom_says_upload()
        => Assert.Equal("Upload", NewSbom(userId: Guid.NewGuid()).SourceText);

    [Fact]
    public void A_pipeline_sbom_names_the_token_and_keeps_the_commit_in_lower_case()
    {
        var sbom = NewSbom(SbomSource.CiUpload);

        sbom.FromPipeline(Guid.NewGuid(), "GitHub Actions", " 3F2A9C1 ");

        Assert.Equal("Pipeline (GitHub Actions)", sbom.SourceText);
        Assert.Equal("3f2a9c1", sbom.CommitSha);
        Assert.Null(sbom.UploadedById);
    }

    [Fact]
    public void A_pipeline_sbom_without_a_commit_is_fine()
    {
        var sbom = NewSbom(SbomSource.CiUpload);

        sbom.FromPipeline(Guid.NewGuid(), "Jenkins", null);

        Assert.Null(sbom.CommitSha);
    }

    [Theory]
    [InlineData("abc12")]
    [InlineData("not-a-sha")]
    [InlineData("0123456789012345678901234567890123456789a")]
    public void A_commit_that_is_not_a_git_sha_is_refused(string commit)
        => Assert.Throws<DomainException>(() => NewSbom(SbomSource.CiUpload).FromPipeline(Guid.NewGuid(), "CI", commit));

    [Fact]
    public void A_github_sbom_says_github()
        => Assert.Equal("GitHub", NewSbom(SbomSource.GitHub).SourceText);

    [Fact]
    public void Without_a_version_the_sbom_is_refused()
        => Assert.Throws<DomainException>(() => Sbom.Create(Guid.NewGuid(), " ", "SPDX 2.3", "x.json", [1], [], null, Monday, SbomSource.CiUpload));

    [Fact]
    public void GitHub_is_asked_when_it_never_was()
        => Assert.True(SbomService.DueForGitHub(null, Monday));

    [Fact]
    public void GitHub_is_not_asked_again_the_same_day()
        => Assert.False(SbomService.DueForGitHub(Monday.AddHours(-23), Monday));

    [Fact]
    public void GitHub_is_asked_again_after_a_day()
        => Assert.True(SbomService.DueForGitHub(Monday.AddDays(-1), Monday));
}
