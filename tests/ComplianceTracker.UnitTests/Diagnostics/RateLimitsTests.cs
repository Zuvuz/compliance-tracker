using ComplianceTracker.Web.Diagnostics;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.UnitTests.Diagnostics;

// Unit tests of the request limits (US-097). Every test follows Arrange / Act / Assert.
public class RateLimitsTests
{
    [Theory]
    [InlineData("GET", "/Projects", null, RequestKind.Free)]
    [InlineData("HEAD", "/", null, RequestKind.Free)]
    [InlineData("POST", "/health", null, RequestKind.Free)]
    [InlineData("POST", "/Projects/AskTheAi", "application/x-www-form-urlencoded", RequestKind.Post)]
    [InlineData("POST", "/Projects/UploadScan", "multipart/form-data; boundary=x", RequestKind.Upload)]
    [InlineData("post", "/Risks/Import", "MULTIPART/FORM-DATA", RequestKind.Upload)]
    [InlineData("POST", "/api/projects/1/scans", null, RequestKind.Post)]
    public void Only_changes_are_limited_and_a_form_with_a_file_is_an_upload(string method, string path, string? contentType, RequestKind expected)
        => Assert.Equal(expected, RateLimits.KindOf(method, path, contentType));

    [Theory]
    [InlineData("anna-sub", "10.0.0.1", "user:anna-sub")]
    [InlineData(null, "10.0.0.1", "ip:10.0.0.1")]
    [InlineData("", null, "ip:unknown")]
    public void The_limit_is_per_user_or_per_address(string? user, string? ip, string expected)
        => Assert.Equal(expected, RateLimits.KeyOf(user, ip));

    [Fact]
    public void The_defaults_are_10_AI_20_uploads_and_120_changes_a_minute()
    {
        var options = new RateLimitOptions();

        Assert.Equal((10, 20, 120), (options.AiPerOrganisationPerMinute, options.UploadsPerUserPerMinute, options.PostsPerUserPerMinute));
    }

    [Fact]
    public void A_limit_of_zero_still_lets_one_through()
        => Assert.Equal(1, RateLimits.PerMinute(0).PermitLimit);

    [Fact]
    public void The_AI_limit_is_per_organisation()
    {
        using var limiter = new AiRateLimiter(Options.Create(new RateLimitOptions { AiPerOrganisationPerMinute = 2 }));
        var acme = Guid.NewGuid();
        var beta = Guid.NewGuid();

        var acmes = new[] { limiter.TryTake(acme), limiter.TryTake(acme), limiter.TryTake(acme) };

        Assert.Equal([true, true, false], acmes);
        Assert.True(limiter.TryTake(beta));
    }
}
