using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Organizations
{
    public class Organization
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Cvr { get; set; } = "";
        public List<User> Users { get; } = new();

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
                Cvr = cvr
            };

            var admin = new User
            {
                Name = creatorName,
                Role = UserRole.Admin
            };
            organization.Users.Add(admin);

            return organization;
        }
    }
}
