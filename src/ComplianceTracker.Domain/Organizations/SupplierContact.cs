using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Organizations
{
    // US-207 R2: a person at a supplier the organisation can write to, e.g. the account manager or the security contact.
    // One of a supplier's contact persons is primary; one or more can be security contacts.
    public class SupplierContact
    {
        public const int MaxNameLength = 200;
        public const int MaxRoleLength = 100;
        public const int MaxEmailLength = 254;
        public const int MaxPhoneLength = 30;

        // The roles offered on the page; another role can be typed
        public static readonly IReadOnlyList<string> KnownRoles = ["Account manager", "Security contact", "Data protection officer", "Support", "Management"];

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid SupplierId { get; private set; }
        public string Name { get; private set; } = "";
        public string? Role { get; private set; }
        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public bool IsPrimary { get; private set; }
        public bool IsSecurityContact { get; private set; }

        // For EF Core only
        private SupplierContact() { }

        internal static SupplierContact Create(Guid supplierId, string? name, string? role, string? email, string? phone, bool securityContact)
        {
            var contact = new SupplierContact { SupplierId = supplierId };
            contact.Change(name, role, email, phone, securityContact);
            return contact;
        }

        internal void Change(string? name, string? role, string? email, string? phone, bool securityContact)
        {
            var cleanName = (name ?? "").Trim();
            if (cleanName.Length == 0)
            {
                throw new DomainException("Give the contact person's name");
            }

            if (cleanName.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            var cleanRole = (role ?? "").Trim();
            if (cleanRole.Length > MaxRoleLength)
            {
                throw new DomainException($"The role must be {MaxRoleLength} characters or fewer");
            }

            Name = cleanName;
            Role = cleanRole.Length == 0 ? null : cleanRole;
            Email = SupplierContacts.CleanEmail(email);
            Phone = SupplierContacts.CleanPhone(phone);
            IsSecurityContact = securityContact;
        }

        internal void MakePrimary(bool primary) => IsPrimary = primary;

        // e.g. "Mette Lund (Security contact)", for the log: never the e-mail or the phone (R6)
        public string LogName => Role is null ? Name : $"{Name} ({Role})";
    }

    // US-207 R3: the checks of an e-mail address, a phone number and a website. Pure, so the edge cases are tested on their own.
    public static partial class SupplierContacts
    {
        // One @, something before it, and a domain with a dot after it; no spaces
        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
        private static partial Regex EmailPattern();

        // A leading + and a country code may come first; digits, spaces, hyphens and brackets; at least six digits
        [GeneratedRegex(@"^\+?[0-9 ()\-]+$")]
        private static partial Regex PhonePattern();

        public static string? CleanEmail(string? email)
        {
            var clean = (email ?? "").Trim();
            if (clean.Length == 0)
            {
                return null;
            }

            if (clean.Length > SupplierContact.MaxEmailLength || !EmailPattern().IsMatch(clean))
            {
                throw new DomainException($"Not a valid e-mail address: {clean}");
            }

            return clean;
        }

        public static string? CleanPhone(string? phone)
        {
            var clean = string.Join(" ", (phone ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (clean.Length == 0)
            {
                return null;
            }

            if (clean.Length > SupplierContact.MaxPhoneLength || !PhonePattern().IsMatch(clean) || clean.Count(char.IsDigit) < 6
                || clean.IndexOf('+', 1) > 0)
            {
                throw new DomainException($"Not a valid phone number: {clean}. Write it like +45 12 34 56 78");
            }

            return clean;
        }

        // R1: an absolute http or https address
        public static string? CleanWebsite(string? website)
        {
            var clean = (website ?? "").Trim();
            if (clean.Length == 0)
            {
                return null;
            }

            if (!clean.Contains("://", StringComparison.Ordinal))
            {
                clean = "https://" + clean;
            }

            if (clean.Length > 500 || !Uri.TryCreate(clean, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || !uri.Host.Contains('.'))
            {
                throw new DomainException($"Not a valid website: {(website ?? "").Trim()}");
            }

            return uri.ToString();
        }

        // R4: the member's own e-mail program, with the subject filled in; the tracker sends nothing
        public static string MailTo(string email, string subject)
            => $"mailto:{email}?subject={Uri.EscapeDataString(subject)}";
    }
}
