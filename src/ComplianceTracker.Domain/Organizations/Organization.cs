using ComplianceTracker.Domain.Projects;
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

        // Every user who has been a member, also removed ones, so names stay on history (US-090)
        public IReadOnlyList<User> Users => _users;

        // US-090: the members now: who can sign in, act and be chosen, e.g. as a risk's owner
        public IReadOnlyList<User> Members => _users.Where(u => u.IsActive).ToList();

        // US-036 R5: whether the organisation's decisions on AI suggestions may be exported to improve the AI.
        // Off until an Admin switches it on.
        public bool ShareAiFeedback { get; private set; }

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

        // US-036 R6: only an Admin may change it. Returns false when nothing changed.
        public bool SetShareAiFeedback(bool share, Guid userId)
        {
            if (_users.FirstOrDefault(u => u.Id == userId)?.Role != UserRole.Admin)
            {
                throw new DomainException("Only an Admin can change this setting");
            }

            if (ShareAiFeedback == share)
            {
                return false;
            }

            ShareAiFeedback = share;
            return true;
        }

        // ---------------------------------------------------------- Members and roles (US-009, US-010)

        // US-009 R2, R5: a person joins through an invitation. A person is a member of one organisation only (B16);
        // the caller checks the other organisations.
        public User AddMember(UserIdentity identity, UserRole role)
        {
            if (_users.FirstOrDefault(u => u.ExternalId == identity.ExternalId) is { } known)
            {
                if (known.IsActive)
                {
                    throw new DomainException("You are already a member of this organisation");
                }

                // US-090 R7: a removed member who is invited again comes back, with the history under the same name
                known.Restore(role);
                return known;
            }

            var user = new User(identity.ExternalId, identity.Name, identity.Email, role);
            _users.Add(user);
            return user;
        }

        // US-010 R2, R3: an Admin changes a member's role. Returns the change, or null if the role is the same.
        // Only what the member may do from now on changes; their earlier decisions keep the role they had then.
        public ProjectChange? ChangeRole(Guid memberId, UserRole role, Guid changedById)
        {
            if (_users.FirstOrDefault(u => u.Id == changedById)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            return SetRole(memberId, role);
        }

        // US-090 R1, R2: an Admin removes a member. The member is kept, so their name stays on what they did (R4).
        public User RemoveMember(Guid memberId, Guid removedById, DateTime at)
        {
            if (_users.FirstOrDefault(u => u.Id == removedById)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId)
                ?? throw new DomainException("Member not found");

            // R2: the organisation always keeps at least one Admin (US-010 R3)
            if (member.Role == UserRole.Admin && Members.Count(u => u.Role == UserRole.Admin) == 1)
            {
                throw new DomainException("Give another member the Admin role first");
            }

            member.Remove(removedById, at);
            return member;
        }

        // US-010: the system admin (the tracker's owner) changes a role, e.g. when an organisation has locked itself out.
        // The caller checks that the person is the system admin.
        public ProjectChange? ChangeRoleAsSystemAdmin(Guid memberId, UserRole role) => SetRole(memberId, role);

        private ProjectChange? SetRole(Guid memberId, UserRole role)
        {
            if (!Enum.IsDefined(role))
            {
                throw new DomainException("Choose a role");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId)
                ?? throw new DomainException("Member not found");

            if (member.Role == role)
            {
                return null;
            }

            // R3: the organisation always keeps at least one Admin
            if (member.Role == UserRole.Admin && Members.Count(u => u.Role == UserRole.Admin) == 1)
            {
                throw new DomainException("The organisation must keep at least one organisation admin");
            }

            var change = new ProjectChange(member.Name, RolePermissions.Title(member.Role), RolePermissions.Title(role));
            member.Role = role;
            return change;
        }

    }
}
