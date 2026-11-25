using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-213: storage for competences, people without a user, training records and policy confirmations (implemented in
    // Infrastructure). Add only registers; IUnitOfWork saves.
    public interface ICompetenceRepository
    {
        Task<IReadOnlyList<Competence>> ListCompetencesAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<StaffPerson>> ListPeopleAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TrainingRecord>> ListTrainingAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PolicyAcknowledgement>> ListAcknowledgementsAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(Competence competence, CancellationToken cancellationToken = default);

        Task AddAsync(StaffPerson person, CancellationToken cancellationToken = default);

        Task AddAsync(TrainingRecord record, CancellationToken cancellationToken = default);

        Task AddAsync(PolicyAcknowledgement acknowledgement, CancellationToken cancellationToken = default);
    }

    // One row of the matrix: a member (with their role) or a person without a user. Key is "user:{id}" or "person:{id}".
    public sealed record MatrixPerson(string Key, string Name, UserRole? Role, IReadOnlyDictionary<Guid, CompetenceState> States)
    {
        public int Missing => States.Values.Count(s => s is CompetenceState.Missing or CompetenceState.Expired);
    }

    // A policy with the members who have and have not confirmed its current version
    public sealed record PolicyReading(OrganisationDocument Policy, IReadOnlyList<string> Read, IReadOnlyList<string> NotRead);

    public sealed record CompetenceView(
        IReadOnlyList<Competence> Competences,
        IReadOnlyList<MatrixPerson> People,
        IReadOnlyList<PolicyReading> Policies,
        IReadOnlyList<OrganisationDocument> MyPoliciesToRead,
        bool CanChange,
        DateOnly Today);

    public class CompetenceService
    {
        private readonly ICompetenceRepository _competences;
        private readonly IOrganisationDocumentRepository _documents;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public CompetenceService(ICompetenceRepository competences, IOrganisationDocumentRepository documents, CurrentMember member, IActivityLogger log,
            IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _competences = competences;
            _documents = documents;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private DateOnly Today => DateOnly.FromDateTime(Now);

        // Every member sees the matrix, who has read the policies, and the policies they must read themself
        public async Task<CompetenceView> ViewAsync(CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var competences = (await _competences.ListCompetencesAsync(organization.Id, cancellationToken)).Where(c => c.IsActive)
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var training = await _competences.ListTrainingAsync(organization.Id, cancellationToken);
            var people = new List<MatrixPerson>();

            // R5: only the members and people who are still here
            foreach (var member in organization.Members)
            {
                var own = training.Where(t => t.UserId == member.Id).ToList();
                people.Add(new MatrixPerson($"user:{member.Id}", member.Name, member.Role,
                    competences.ToDictionary(c => c.Id, c => CompetenceMatrix.StateOf(c, member.Role, own, Today))));
            }

            foreach (var person in (await _competences.ListPeopleAsync(organization.Id, cancellationToken)).Where(p => p.IsActive))
            {
                var own = training.Where(t => t.StaffPersonId == person.Id).ToList();
                people.Add(new MatrixPerson($"person:{person.Id}", person.Name, null,
                    competences.ToDictionary(c => c.Id, c => CompetenceMatrix.StateOf(c, null, own, Today))));
            }

            var policies = await PoliciesAsync(organization, cancellationToken);
            var acknowledgements = await _competences.ListAcknowledgementsAsync(organization.Id, cancellationToken);
            var readings = policies.Select(p =>
            {
                var read = organization.Members.Where(m => HasRead(p, m.Id, acknowledgements)).Select(m => m.Name).Order(StringComparer.OrdinalIgnoreCase).ToList();
                var notRead = organization.Members.Where(m => !HasRead(p, m.Id, acknowledgements)).Select(m => m.Name).Order(StringComparer.OrdinalIgnoreCase).ToList();
                return new PolicyReading(p, read, notRead);
            }).ToList();

            return new CompetenceView(competences,
                people.OrderByDescending(p => p.Missing).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                readings,
                policies.Where(p => !HasRead(p, user.Id, acknowledgements)).ToList(),
                await _member.CanAsync(Permission.DecideCompliance, cancellationToken), Today);
        }

        // R1
        public async Task<Competence> AddCompetenceAsync(string? name, int? renewalMonths, IReadOnlyList<UserRole>? roles, bool forPeopleWithoutUser,
            CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var competence = Competence.Create(organization.Id, name, renewalMonths, roles, forPeopleWithoutUser, Now);
            if ((await _competences.ListCompetencesAsync(organization.Id, cancellationToken))
                .Any(c => c.IsActive && string.Equals(c.Name, competence.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DomainException($"{competence.Name} is already a competence");
            }

            await _competences.AddAsync(competence, cancellationToken);
            await _log.LogAsync(ActivityAction.CompetenceChanged, organization.Id,
                $"Added the competence {competence.Name} ({(competence.RenewalMonths is { } m ? $"renewed every {m} months" : "no renewal")})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return competence;
        }

        public async Task RemoveCompetenceAsync(Guid competenceId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var competence = (await _competences.ListCompetencesAsync(organization.Id, cancellationToken)).FirstOrDefault(c => c.Id == competenceId)
                             ?? throw new DomainException("Competence not found");
            competence.Remove(Now);
            await _log.LogAsync(ActivityAction.CompetenceChanged, organization.Id, $"Removed the competence {competence.Name}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task AddPersonAsync(string? name, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var person = StaffPerson.Create(organization.Id, name, Now);
            await _competences.AddAsync(person, cancellationToken);
            await _log.LogAsync(ActivityAction.CompetenceChanged, organization.Id, $"Added {person.Name} (no user)", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5
        public async Task RemovePersonAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var person = (await _competences.ListPeopleAsync(organization.Id, cancellationToken)).FirstOrDefault(p => p.Id == personId)
                         ?? throw new DomainException("Person not found");
            person.Remove(Now);
            await _log.LogAsync(ActivityAction.CompetenceChanged, organization.Id, $"Removed {person.Name} (no user)", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R2: "user:{id}" or "person:{id}"
        public async Task RecordTrainingAsync(Guid competenceId, string? person, DateOnly? trainedOn, string? evidence, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var competence = (await _competences.ListCompetencesAsync(organization.Id, cancellationToken)).FirstOrDefault(c => c.Id == competenceId && c.IsActive)
                             ?? throw new DomainException("Competence not found");
            var (userId, personId, name) = await PersonAsync(organization, person, cancellationToken);
            var record = TrainingRecord.Create(organization.Id, competence, userId, personId, trainedOn, evidence, user.Id, Today, Now);
            await _competences.AddAsync(record, cancellationToken);
            await _log.LogAsync(ActivityAction.CompetenceChanged, organization.Id,
                $"{name} trained in {competence.Name} on {record.TrainedOn:dd-MM-yyyy}{(record.ValidUntil is { } until ? $", valid until {until:dd-MM-yyyy}" : "")}",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R3, R4: the member confirms the current version themself; there is no way to confirm for others
        public async Task ConfirmPolicyAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var policy = (await PoliciesAsync(organization, cancellationToken)).FirstOrDefault(p => p.Id == documentId)
                         ?? throw new DomainException("Policy not found");
            if (HasRead(policy, user.Id, await _competences.ListAcknowledgementsAsync(organization.Id, cancellationToken)))
            {
                return;
            }

            await _competences.AddAsync(PolicyAcknowledgement.Confirm(organization.Id, policy.Id, user.Id, policy.Version, Now), cancellationToken);
            await _log.LogAsync(ActivityAction.PolicyConfirmed, organization.Id, $"{user.Name} read {policy.Title} version {policy.Version}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R6, for the objectives (US-209): the share of the members' confirmations of the current policies; null without
        // policies or members
        public static decimal? ShareRead(IReadOnlyList<OrganisationDocument> policies, IReadOnlyList<User> members, IReadOnlyList<PolicyAcknowledgement> acknowledgements)
        {
            var total = policies.Count * members.Count;
            var read = policies.Sum(p => members.Count(m => HasRead(p, m.Id, acknowledgements)));
            return TrackerMeasures.Percent(read, total);
        }

        public static bool HasRead(OrganisationDocument policy, Guid userId, IEnumerable<PolicyAcknowledgement> acknowledgements)
            => CompetenceMatrix.HasRead(policy.Version, acknowledgements.Where(a => a.DocumentId == policy.Id && a.UserId == userId));

        private async Task<IReadOnlyList<OrganisationDocument>> PoliciesAsync(Organization organization, CancellationToken cancellationToken)
            => (await _documents.ListAsync(organization.Id, cancellationToken: cancellationToken))
                .Where(d => d.IsActive && d.Kind == OrganisationDocumentKind.Policy)
                .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private async Task<(Guid? UserId, Guid? PersonId, string Name)> PersonAsync(Organization organization, string? key, CancellationToken cancellationToken)
        {
            var parts = (key ?? "").Split(':');
            if (parts.Length == 2 && Guid.TryParse(parts[1], out var id))
            {
                if (parts[0] == "user" && organization.Members.FirstOrDefault(m => m.Id == id) is { } member)
                {
                    return (member.Id, null, member.Name);
                }

                if (parts[0] == "person" && (await _competences.ListPeopleAsync(organization.Id, cancellationToken)).FirstOrDefault(p => p.Id == id && p.IsActive) is { } person)
                {
                    return (null, person.Id, person.Name);
                }
            }

            throw new DomainException("Choose the person who was trained");
        }
    }
}
