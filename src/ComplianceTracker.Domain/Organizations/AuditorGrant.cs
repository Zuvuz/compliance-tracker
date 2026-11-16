using System.Net.Mail;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Organizations
{
    // US-182: read-only access to one project for an external auditor, for at most 30 days. It is given through an
    // invitation (US-009) with the Auditor role; accepting it limits the new member to the project and the dates.
    public class AuditorGrant
    {
        public const int MaxDays = 30;
        public const int MaxEmailLength = 320;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid ProjectId { get; private set; }
        public string Email { get; private set; } = "";
        public DateOnly From { get; private set; }
        public DateOnly Until { get; private set; }

        public Guid InvitationId { get; private set; }
        public Guid GrantedById { get; private set; }
        public DateTime GrantedAt { get; private set; }

        // The auditor's member row once the invitation was accepted
        public Guid? MemberId { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        public bool IsRevoked => RevokedAt is not null;

        // For EF Core only
        private AuditorGrant() { }

        // R1: one project, from today or later, at most 30 days
        public static AuditorGrant Create(Guid organizationId, Guid projectId, string? email, DateOnly? from, DateOnly? until, DateOnly today,
            Guid invitationId, Guid grantedById, DateTime at)
        {
            var address = (email ?? "").Trim();
            if (address.Length == 0 || address.Length > MaxEmailLength || !MailAddress.TryCreate(address, out _))
            {
                throw new DomainException("Give the auditor's e-mail address");
            }

            if (from is not { } start || until is not { } end)
            {
                throw new DomainException("Give the first and the last day of the access");
            }

            if (start < today)
            {
                throw new DomainException("The access cannot start in the past");
            }

            if (end < start)
            {
                throw new DomainException("The last day must be on or after the first");
            }

            if (end.DayNumber - start.DayNumber + 1 > MaxDays)
            {
                throw new DomainException($"The access can last at most {MaxDays} days");
            }

            return new AuditorGrant
            {
                OrganizationId = organizationId,
                ProjectId = projectId,
                Email = address,
                From = start,
                Until = end,
                InvitationId = invitationId,
                GrantedById = grantedById,
                GrantedAt = at
            };
        }

        public bool IsActiveOn(DateOnly day) => !IsRevoked && day >= From && day <= Until;

        public void Accepted(Guid memberId) => MemberId = memberId;

        // R3: at once
        public void Revoke(DateTime at)
        {
            if (IsRevoked)
            {
                throw new DomainException("The access has already been revoked");
            }

            RevokedAt = at;
        }
    }
}
