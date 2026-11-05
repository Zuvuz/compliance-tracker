using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace ComplianceTracker.Web.Security
{
    // US-093: where to report a vulnerability in the tracker. Settings in the section "SecurityContact"
    // (appsettings.json), so an installation run by someone else can point to its own contact (R6).
    public class SecurityContactOptions
    {
        public const string Section = "SecurityContact";

        // RFC 9116 "Contact": mailto: or https: addresses, in the order to try them
        public List<string> Contacts { get; set; } = [];

        // The disclosure policy (SECURITY.md)
        public string Policy { get; set; } = "";

        // How long the file is valid; RFC 9116 asks for less than a year
        public int ValidDays { get; set; } = 180;
    }

    // US-093 R2: /.well-known/security.txt (RFC 9116), served without sign-in on every installation
    public static class SecurityTxt
    {
        public const string Path = "/.well-known/security.txt";

        public static string Content(SecurityContactOptions options, DateTimeOffset now)
        {
            var text = new StringBuilder();
            foreach (var contact in options.Contacts.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                text.Append("Contact: ").Append(contact.Trim()).Append('\n');
            }

            // The file expires so a forgotten installation does not point to an old contact for ever
            var expires = now.UtcDateTime.Date.AddDays(Math.Clamp(options.ValidDays, 1, 364));
            text.Append("Expires: ").Append(expires.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append('\n');
            if (!string.IsNullOrWhiteSpace(options.Policy))
            {
                text.Append("Policy: ").Append(options.Policy.Trim()).Append('\n');
            }

            text.Append("Preferred-Languages: en, da\n");
            return text.ToString();
        }

        public static IEndpointRouteBuilder MapSecurityTxt(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet(Path, (IOptions<SecurityContactOptions> options, TimeProvider clock)
                    => Results.Text(Content(options.Value, clock.GetUtcNow()), "text/plain; charset=utf-8"))
                .AllowAnonymous();
            return endpoints;
        }
    }
}
