using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Organizations
{
    // The company that uses the tracker. Its members are users with a role (US-001).
    public class Organization
    {
        // Private list, so members can only be added through methods on this class
        private readonly List<User> _users = new();

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; private set; } = "";

        // Danish company registration number: exactly 8 digits, unique across the tracker
        public string Cvr { get; private set; } = "";

        // Sector and size decide later which frameworks are relevant (e.g. NIS2)
        public Sector Sector { get; private set; }
        public OrganizationSize Size { get; private set; }

        public IReadOnlyList<User> Users => _users;

        // For EF Core only. Everyone else must use Create, so the rules always run.
        private Organization() { }

        // Validates the fields and makes the signed-in creator the first Admin (US-001, US-004).
        // The rule that a CVR is unique needs the database and lives in OrganizationService.
        public static Organization Create(string name, string cvr, Sector? sector, OrganizationSize? size, UserIdentity creator)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Name is required");
            }

            // IsAsciiDigit only accepts 0-9. IsDigit would also accept digits from other alphabets, e.g. "١٢٣٤٥٦٧٨".
            if (cvr.Length != 8 || !cvr.All(char.IsAsciiDigit))
            {
                throw new DomainException("CVR must be 8 digits");
            }

            if (sector is null)
            {
                throw new DomainException("Sector is required");
            }

            // US-001 R3: size is required too (US-016)
            if (size is null)
            {
                throw new DomainException("Size is required");
            }

            if (string.IsNullOrWhiteSpace(creator.ExternalId))
            {
                throw new DomainException("Creator must be signed in");
            }

            // Everything is checked before the object is built, so a half-created organisation never exists
            var organization = new Organization
            {
                Name = name,
                Cvr = cvr,
                Sector = sector.Value,
                Size = size.Value
            };

            organization._users.Add(new User(creator.ExternalId, creator.Name, creator.Email, UserRole.Admin));

            return organization;
        }
    }
}
