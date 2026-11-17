using System.Net;
using System.Text.Json;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Application.WorkItems;

namespace ComplianceTracker.Infrastructure.WorkItems
{
    // US-148 R3, R8: GitHub's SBOM from the dependency graph, read-only (B12), with the project's token.
    // GET /repos/{owner}/{repo}/dependency-graph/sbom answers {"sbom": { …SPDX 2.3… }}; the SPDX document is returned.
    public partial class GitHubWorkItemSource : IGitHubSbom
    {
        async Task<GitHubSbomRead> IGitHubSbom.ReadAsync(string repository, string? token, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{repository}/dependency-graph/sbom");
            if (token is not null)
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new WorkItemSourceException("GitHub could not be reached. Try again later", error);
            }

            using (response)
            {
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    return new GitHubSbomRead(null,
                        $"GitHub has no dependency graph for {repository}, or the token cannot read it. Turn it on under Settings → Security");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new GitHubSbomRead(null, $"GitHub answered with an error ({(int)response.StatusCode})");
                }

                var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                try
                {
                    using var document = JsonDocument.Parse(body);
                    return document.RootElement.TryGetProperty("sbom", out var sbom) && sbom.ValueKind == JsonValueKind.Object
                        ? new GitHubSbomRead(JsonSerializer.SerializeToUtf8Bytes(sbom), null)
                        : new GitHubSbomRead(null, "GitHub's answer had no SBOM");
                }
                catch (JsonException)
                {
                    return new GitHubSbomRead(null, "GitHub's answer could not be read");
                }
            }
        }
    }
}
