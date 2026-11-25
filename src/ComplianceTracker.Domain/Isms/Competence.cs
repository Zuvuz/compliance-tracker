using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Isms
{
    // US-213 R2: where a person stands with a competence on a day
    public enum CompetenceState
    {
        NotRequired,
        Valid,
        Expired,
        Missing
    }

    // US-213 R1: a competence the organisation needs (ISO/IEC 27001 7.2, 7.3), the roles that need it, whether the people
    // without a user need it too, and how often it must be renewed (null: once is enough).
    public class Competence
    {
        public const int MaxNameLength = 200;
        public const int MaxRenewalMonths = 60;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public int? RenewalMonths { get; private set; }
        public List<UserRole> RequiredRoles { get; private set; } = [];
        public bool ForPeopleWithoutUser { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private Competence() { }

        public static Competence Create(Guid organizationId, string? name, int? renewalMonths, IEnumerable<UserRole>? roles, bool forPeopleWithoutUser,
            DateTime at)
        {
            var clean = (name ?? "").Trim();
            if (clean.Length == 0)
            {
                throw new DomainException("Give the competence a name");
            }

            if (clean.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            if (renewalMonths is < 1 or > MaxRenewalMonths)
            {
                throw new DomainException($"A renewal is every 1 to {MaxRenewalMonths} months, or never");
            }

            var required = (roles ?? []).Where(r => Enum.IsDefined(r)).Distinct().OrderBy(r => r).ToList();
            if (required.Count == 0 && !forPeopleWithoutUser)
            {
                throw new DomainException("Choose the roles that need the competence");
            }

            return new Competence
            {
                OrganizationId = organizationId,
                Name = clean,
                RenewalMonths = renewalMonths,
                RequiredRoles = required,
                ForPeopleWithoutUser = forPeopleWithoutUser,
                CreatedAt = at
            };
        }

        // R1: a member with one of the roles; a person without a user when the competence says so
        public bool IsRequiredFor(UserRole? role) => role is { } r ? RequiredRoles.Contains(r) : ForPeopleWithoutUser;

        // R2: the validity ends after the renewal interval
        public DateOnly? ValidUntil(DateOnly trainedOn) => RenewalMonths is { } months ? trainedOn.AddMonths(months) : null;

        public void Remove(DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The competence has been removed");
            }

            RemovedAt = at;
        }
    }

    // US-213: someone who works for the organisation without a user, e.g. a cleaner; added by name only
    public class StaffPerson
    {
        public const int MaxNameLength = 200;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = "";
        public DateTime AddedAt { get; private set; }
        public DateTime? RemovedAt { get; private set; }

        public bool IsActive => RemovedAt is null;

        // For EF Core only
        private StaffPerson() { }

        public static StaffPerson Create(Guid organizationId, string? name, DateTime at)
        {
            var clean = (name ?? "").Trim();
            if (clean.Length == 0)
            {
                throw new DomainException("Give the person's name");
            }

            if (clean.Length > MaxNameLength)
            {
                throw new DomainException($"The name must be {MaxNameLength} characters or fewer");
            }

            return new StaffPerson { OrganizationId = organizationId, Name = clean, AddedAt = at };
        }

        // R5: the history stays, but the person is no longer counted
        public void Remove(DateTime at)
        {
            if (!IsActive)
            {
                throw new DomainException("The person has been removed");
            }

            RemovedAt = at;
        }
    }

    // US-213 R2: a person was trained in a competence on a date; valid until the renewal interval has passed. Never changed.
    public class TrainingRecord
    {
        public const int MaxEvidenceLength = 500;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid CompetenceId { get; private set; }

        // Exactly one of the two: a member or a person without a user
        public Guid? UserId { get; private set; }
        public Guid? StaffPersonId { get; private set; }

        public DateOnly TrainedOn { get; private set; }
        public DateOnly? ValidUntil { get; private set; }
        public string? Evidence { get; private set; }
        public Guid RecordedById { get; private set; }
        public DateTime RecordedAt { get; private set; }

        // For EF Core only
        private TrainingRecord() { }

        public static TrainingRecord Create(Guid organizationId, Competence competence, Guid? userId, Guid? staffPersonId, DateOnly? trainedOn,
            string? evidence, Guid recordedById, DateOnly today, DateTime at)
        {
            if ((userId is null) == (staffPersonId is null))
            {
                throw new DomainException("Choose the person who was trained");
            }

            if (trainedOn is null)
            {
                throw new DomainException("Give the date of the training");
            }

            if (trainedOn > today)
            {
                throw new DomainException("The training date cannot be in the future");
            }

            if (!competence.IsActive)
            {
                throw new DomainException("The competence has been removed");
            }

            var clean = (evidence ?? "").Trim();
            if (clean.Length > MaxEvidenceLength)
            {
                throw new DomainException($"The evidence must be {MaxEvidenceLength} characters or fewer");
            }

            return new TrainingRecord
            {
                OrganizationId = organizationId,
                CompetenceId = competence.Id,
                UserId = userId,
                StaffPersonId = staffPersonId,
                TrainedOn = trainedOn.Value,
                ValidUntil = competence.ValidUntil(trainedOn.Value),
                Evidence = clean.Length == 0 ? null : clean,
                RecordedById = recordedById,
                RecordedAt = at
            };
        }

        public bool IsValidOn(DateOnly day) => ValidUntil is not { } until || until > day;
    }

    // US-213 R3, R4: a member confirmed they have read a version of a policy (US-136). Only the member themself confirms.
    public class PolicyAcknowledgement
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }
        public Guid DocumentId { get; private set; }
        public Guid UserId { get; private set; }
        public string Version { get; private set; } = "";
        public DateTime ConfirmedAt { get; private set; }

        // For EF Core only
        private PolicyAcknowledgement() { }

        public static PolicyAcknowledgement Confirm(Guid organizationId, Guid documentId, Guid userId, string version, DateTime at)
            => new() { OrganizationId = organizationId, DocumentId = documentId, UserId = userId, Version = version, ConfirmedAt = at };
    }

    // US-213 R2, R5: the state of each person and competence on a day. Pure.
    public static class CompetenceMatrix
    {
        // The newest training decides; a competence not required shows as valid only when the person has a valid training
        public static CompetenceState StateOf(Competence competence, UserRole? role, IEnumerable<TrainingRecord> records, DateOnly today)
        {
            var newest = records.Where(r => r.CompetenceId == competence.Id).OrderByDescending(r => r.TrainedOn).ThenByDescending(r => r.RecordedAt)
                .FirstOrDefault();
            var required = competence.IsRequiredFor(role);
            if (newest is null)
            {
                return required ? CompetenceState.Missing : CompetenceState.NotRequired;
            }

            if (newest.IsValidOn(today))
            {
                return CompetenceState.Valid;
            }

            return required ? CompetenceState.Expired : CompetenceState.NotRequired;
        }

        // R3: the newest version must be confirmed
        public static bool HasRead(string currentVersion, IEnumerable<PolicyAcknowledgement> acknowledgements)
            => acknowledgements.Any(a => a.Version == currentVersion);
    }
}
