using ComplianceTracker.Infrastructure.Persistence;

namespace ComplianceTracker.Web.Diagnostics
{
    // US-139: the current request's path, for the slow-query log
    public sealed class HttpRequestPath : ICurrentRequestPath
    {
        private readonly IHttpContextAccessor _http;

        public HttpRequestPath(IHttpContextAccessor http)
        {
            _http = http;
        }

        public string? Path => _http.HttpContext?.Request.Path.Value;
    }
}
