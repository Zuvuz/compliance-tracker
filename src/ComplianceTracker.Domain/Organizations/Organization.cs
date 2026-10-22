using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Organizations
{
    public class Organization
    {
        private readonly List<User> _users = new();

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; private set; } = "";
        public string Cvr { get; private set; } = "";
        public Sector Sector { get; private set; }
        public OrganizationSize Size { get; private set; }
        public IReadOnlyList<User> Users => _users;

        private Organization() { }

        public static Organization Create(string name, string cvr, Sector? sector, OrganizationSize size, string creatorName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Name is required");
            }

            if (cvr.Length != 8 || !cvr.All(char.IsDigit))
            {
                throw new DomainException("CVR must be 8 digits");
            }

            if (sector is null)
            {
                throw new DomainException("Sector is required");
            }

            var organization = new Organization
            {
                Name = name,
                Cvr = cvr,
                Sector = sector.Value,
                Size = size
            };

            organization._users.Add(new User(creatorName, UserRole.Admin));

            return organization;
        }
    }
}
