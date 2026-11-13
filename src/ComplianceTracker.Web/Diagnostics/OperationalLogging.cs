using System.Diagnostics;
using ComplianceTracker.Infrastructure.Diagnostics;

namespace ComplianceTracker.Web.Diagnostics
{
    // US-098: structured logs without personal data or secrets, security events, and a warning when errors repeat
    public static partial class OperationalLogging
    {
        public const string FormatSetting = "Logging:Format";
        public const string SecurityCategory = "ComplianceTracker.Security";

        // R1, R6: JSON to stdout with the time in UTC and the scopes (the reference and the user), unless "Logging:Format"
        // is "simple" (the default in Development). The levels stay in "Logging:LogLevel".
        public static void AddOperationalLogging(this WebApplicationBuilder builder)
        {
            builder.Logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId);
            var format = builder.Configuration[FormatSetting] ?? (builder.Environment.IsDevelopment() ? "simple" : "json");
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.UseUtcTimestamp = true;
                    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z' ";
                });
            }

            builder.Services.AddSingleton<ErrorRateMonitor>();
        }

        // R3: the reference shown on the error page, and the one every log line of the request carries
        public static string Reference(HttpContext context) => Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;

        // R1, R4, R5: every request's log lines carry its reference and the signed-in user's id (the "sub" from Keycloak,
        // never a name or an e-mail); refused access is a security event; 5xx answers count towards the error rate
        public static IApplicationBuilder UseOperationalLogging(this IApplicationBuilder app)
            => app.Use(async (context, next) =>
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(SecurityCategory);
                using (logger.BeginScope(new Dictionary<string, object?>
                {
                    ["Reference"] = Reference(context),
                    ["UserId"] = context.User.FindFirst("sub")?.Value
                }))
                {
                    await next();

                    var status = context.Response.StatusCode;
                    if (status is StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound && context.GetEndpoint() is not null
                        && context.User.Identity?.IsAuthenticated == true)
                    {
                        AccessRefused(logger, status, context.Request.Method, context.Request.Path.Value ?? "");
                    }
                    else if (status == StatusCodes.Status429TooManyRequests)
                    {
                        LimitHit(logger, context.Request.Method, context.Request.Path.Value ?? "");
                    }

                    if (status >= 500)
                    {
                        context.RequestServices.GetRequiredService<ErrorRateMonitor>().Record();
                    }
                }
            });

        // R4: Keycloak said no, or the answer could not be used. The reason's type, never the token or the message.
        public static void SignInFailed(ILogger logger, Exception? failure) => SignInFailedLog(logger, failure?.GetType().Name ?? "unknown");

        [LoggerMessage(EventId = 4031, Level = LogLevel.Warning, Message = "Security event: access refused ({Status}) for {Method} {Path}")]
        private static partial void AccessRefused(ILogger logger, int status, string method, string path);

        [LoggerMessage(EventId = 4291, Level = LogLevel.Warning, Message = "Security event: request limit hit for {Method} {Path}")]
        private static partial void LimitHit(ILogger logger, string method, string path);

        [LoggerMessage(EventId = 4011, Level = LogLevel.Warning, Message = "Security event: sign-in failed ({Reason})")]
        private static partial void SignInFailedLog(ILogger logger, string reason);
    }
}
