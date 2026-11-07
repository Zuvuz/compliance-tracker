using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;
using System.Text;

namespace ComplianceTracker.Domain.Tests.Scanning;

// Edge-case unit tests of US-122: upload tokens and what a pipeline's scan keeps. Arrange / Act / Assert.
public class ScanUploadTokenTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public void A_new_token_is_random_and_only_its_hash_is_kept()
    {
        // Arrange / Act
        var (first, secret) = ScanUploadToken.Create(ProjectId, " GitHub Actions ", Anna, Now);
        var (_, other) = ScanUploadToken.Create(ProjectId, "GitHub Actions", Anna, Now);

        // Assert: R2
        Assert.StartsWith(ScanUploadToken.Prefix, secret);
        Assert.True(secret.Length >= ScanUploadToken.Prefix.Length + 43); // 32 bytes in base64url
        Assert.NotEqual(secret, other);
        Assert.Equal(ScanUploadToken.HashOf(secret), first.Hash);
        Assert.DoesNotContain(secret, first.Hash);
        Assert.Equal("GitHub Actions", first.Name);
        Assert.Null(first.LastUsedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void A_token_needs_a_name(string? name)
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => ScanUploadToken.Create(ProjectId, name, Anna, Now));
    }

    [Fact]
    public void A_name_that_is_too_long_is_refused()
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => ScanUploadToken.Create(ProjectId, new string('x', ScanUploadToken.MaxNameLength + 1), Anna, Now));
    }

    [Fact]
    public void A_token_allows_only_its_own_project_and_only_until_revoked()
    {
        // Arrange
        var (token, _) = ScanUploadToken.Create(ProjectId, "CI", Anna, Now);

        // Act / Assert: R1, R3
        Assert.True(token.Allows(ProjectId));
        Assert.False(token.Allows(Guid.NewGuid()));
        token.Revoke(Anna, Now.AddMinutes(1));
        Assert.False(token.Allows(ProjectId));
        Assert.Throws<DomainException>(() => token.Revoke(Anna, Now.AddMinutes(2)));
    }

    [Fact]
    public void The_hash_depends_on_every_character()
    {
        // Arrange / Act / Assert
        Assert.NotEqual(ScanUploadToken.HashOf("ctu_a"), ScanUploadToken.HashOf("ctu_A"));
        Assert.Equal(64, ScanUploadToken.HashOf("").Length);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(29, false)]
    [InlineData(30, true)] // R6: the 31st upload
    [InlineData(500, true)]
    public void At_most_30_uploads_an_hour(int uploads, bool reached)
    {
        // Arrange / Act / Assert
        Assert.Equal(reached, ScanUploadToken.LimitReached(uploads));
    }

    private static Scan NewScan() => Scan.Create(ProjectId, "Trivy", "trivy.sarif", Encoding.UTF8.GetBytes("{}"), null, Now, ScanSource.CiUpload);

    [Theory]
    [InlineData("3f2a9c1", "2.3.1", "Commit 3f2a9c1 · version 2.3.1")]
    [InlineData("3F2A9C1D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B", null, "Commit 3f2a9c1")] // 40 characters, upper case
    [InlineData(null, " 2.3.1 ", "Version 2.3.1")]
    [InlineData("  ", "", null)]
    public void A_pipeline_scan_keeps_its_commit_and_version(string? commit, string? version, string? build)
    {
        // Arrange
        var scan = NewScan();

        // Act
        scan.FromPipeline(Guid.NewGuid(), commit, version);

        // Assert: R4
        Assert.Equal(build, scan.Build);
    }

    [Theory]
    [InlineData("3f2a9c")] // too short
    [InlineData("3f2a9c1d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1")] // 41 characters
    [InlineData("not-a-sha")]
    [InlineData("3f2a9c1; rm -rf /")]
    public void A_commit_that_is_not_a_SHA_is_refused(string commit)
    {
        // Arrange
        var scan = NewScan();

        // Act / Assert
        Assert.Throws<DomainException>(() => scan.FromPipeline(Guid.NewGuid(), commit, null));
        Assert.Null(scan.UploadTokenId);
    }

    [Fact]
    public void A_version_that_is_too_long_or_on_several_lines_is_refused()
    {
        // Arrange
        var scan = NewScan();

        // Act / Assert
        Assert.Throws<DomainException>(() => scan.FromPipeline(Guid.NewGuid(), null, new string('1', Scan.MaxVersionLength + 1)));
        Assert.Throws<DomainException>(() => scan.FromPipeline(Guid.NewGuid(), null, "2.3.1\nInjected"));
    }
}
