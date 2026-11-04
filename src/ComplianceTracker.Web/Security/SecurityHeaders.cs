using Microsoft.Extensions.Options;

namespace ComplianceTracker.Web.Security
{
    // US-087: tells the browser what the pages may load and do, on every response (pages, files, errors).
    // ASVS V14.4. The pages have no inline scripts or styles, so the policy needs no 'unsafe-inline'.
    public static class SecurityHeaders
    {
        // R1: the Content-Security-Policy. form-action also allows Keycloak, as signing in and out
        // redirects a form post there (R2). data: images are Bootstrap's own icons in its CSS.
        public static string ContentSecurityPolicy(string? keycloakOrigin)
            => string.Join("; ",
                "default-src 'self'",
                "script-src 'self'",
                "style-src 'self'",
                "img-src 'self' data:",
                "font-src 'self'",
                "connect-src 'self'",          // the live update (SignalR, US-018)
                "object-src 'none'",
                "base-uri 'self'",
                "frame-ancestors 'none'",      // no clickjacking
                "form-action 'self'" + (keycloakOrigin is null ? "" : " " + keycloakOrigin));

        public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";

        // Sets the headers just before each response starts, so they are also on static files and error pages
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            var keycloak = app.ApplicationServices.GetRequiredService<IOptions<KeycloakOptions>>().Value;
            var policy = ContentSecurityPolicy(Origin(keycloak.Authority));

            return app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers;
                    headers.ContentSecurityPolicy = policy;
                    headers.XContentTypeOptions = "nosniff";
                    headers.XFrameOptions = "DENY"; // for browsers without frame-ancestors
                    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                    headers["Permissions-Policy"] = PermissionsPolicy;
                    return Task.CompletedTask;
                });
                await next();
            });
        }

        // "https://keycloak.localhost:8543/realms/x" → "https://keycloak.localhost:8543"
        private static string? Origin(string authority)
            => Uri.TryCreate(authority, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
}
