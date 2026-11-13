using System.Threading.RateLimiting;
using ComplianceTracker.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ComplianceTracker.Web.Diagnostics
{
    // US-097 R5: the limits are settings ("RateLimits"), so they can change without a new release
    public sealed class RateLimitOptions
    {
        public const string Section = "RateLimits";

        public int AiPerOrganisationPerMinute { get; set; } = 10;
        public int UploadsPerUserPerMinute { get; set; } = 20;
        public int PostsPerUserPerMinute { get; set; } = 120;
    }

    // What kind of request it is, for the limits
    public enum RequestKind
    {
        Free,
        Upload,
        Post
    }

    // US-097: limits on heavy actions with ASP.NET Core's own rate limiter, so one user or a script cannot slow the
    // tracker or the local AI down for everyone. Nothing is changed when a limit is hit (R4).
    public static class RateLimits
    {
        public const string TooManyRequests = "Too many requests. Try again in a minute";

        // R2, R3, R6: GETs, /health and static files are never limited; a form with a file is an upload.
        // The pipeline's upload API (/api/…) has its own limit per token (US-122 R6), so it is left to that.
        public static RequestKind KindOf(string method, string path, string? contentType)
        {
            if (!HttpMethods.IsPost(method) || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                return RequestKind.Free;
            }

            return contentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true ? RequestKind.Upload : RequestKind.Post;
        }

        // R2, R3: per user (Keycloak's "sub"), or per IP address when not signed in
        public static string KeyOf(string? userId, string? ipAddress) => userId is { Length: > 0 } ? $"user:{userId}" : $"ip:{ipAddress ?? "unknown"}";

        public static void AddRateLimits(this WebApplicationBuilder builder)
        {
            builder.Services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.Section);
            builder.Services.AddSingleton<AiRateLimiter>();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, cancellationToken) => WriteTooManyAsync(context.HttpContext, cancellationToken);
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitOptions>>().Value;
                    var kind = KindOf(context.Request.Method, context.Request.Path.Value ?? "", context.Request.ContentType);
                    if (kind == RequestKind.Free)
                    {
                        return RateLimitPartition.GetNoLimiter("free");
                    }

                    var key = KeyOf(context.User.FindFirst("sub")?.Value, context.Connection.RemoteIpAddress?.ToString());
                    var permits = kind == RequestKind.Upload ? limits.UploadsPerUserPerMinute : limits.PostsPerUserPerMinute;
                    return RateLimitPartition.GetFixedWindowLimiter($"{kind}:{key}", _ => PerMinute(permits));
                });
            });
        }

        public static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
        {
            PermitLimit = Math.Max(1, permits),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        };

        // R4: a short page that says what happened; the status is 429
        public static async ValueTask WriteTooManyAsync(HttpContext context, CancellationToken cancellationToken)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Too many requests</title></head>"
                + $"<body><h1 id=\"too-many-requests\">{TooManyRequests}</h1><p><a href=\"javascript:history.back()\">Go back</a></p></body></html>",
                cancellationToken);
        }
    }

    // US-097 R1: the AI actions, per organisation, so one organisation cannot keep the local AI busy for the others
    public sealed class AiRateLimiter : IDisposable
    {
        private readonly PartitionedRateLimiter<Guid> _limiter;

        public AiRateLimiter(Microsoft.Extensions.Options.IOptions<RateLimitOptions> options)
        {
            var permits = options.Value.AiPerOrganisationPerMinute;
            _limiter = PartitionedRateLimiter.Create<Guid, Guid>(organizationId => RateLimitPartition.GetFixedWindowLimiter(organizationId, _ => RateLimits.PerMinute(permits)));
        }

        public bool TryTake(Guid organizationId)
        {
            using var lease = _limiter.AttemptAcquire(organizationId);
            return lease.IsAcquired;
        }

        public void Dispose() => _limiter.Dispose();
    }

    // US-097 R1: put on an action that asks the local AI
    public sealed class AiRateLimitAttribute : TypeFilterAttribute
    {
        public AiRateLimitAttribute() : base(typeof(AiRateLimitFilter))
        {
        }

        private sealed class AiRateLimitFilter(AiRateLimiter limiter, CurrentMember member) : IAsyncActionFilter
        {
            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                if (await member.FindOrganizationAsync(context.HttpContext.RequestAborted) is { } organization && !limiter.TryTake(organization.Id))
                {
                    await RateLimits.WriteTooManyAsync(context.HttpContext, context.HttpContext.RequestAborted);
                    context.Result = new EmptyResult();
                    return;
                }

                await next();
            }
        }
    }
}
