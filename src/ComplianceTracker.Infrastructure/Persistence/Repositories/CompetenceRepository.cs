using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // US-213
    public class CompetenceRepository : ICompetenceRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public CompetenceRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Competence>> ListCompetencesAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.Competences.Where(c => c.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<StaffPerson>> ListPeopleAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.StaffPeople.Where(p => p.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<TrainingRecord>> ListTrainingAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.TrainingRecords.Where(t => t.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<PolicyAcknowledgement>> ListAcknowledgementsAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.PolicyAcknowledgements.Where(a => a.OrganizationId == organizationId).ToListAsync(cancellationToken);

        public async Task AddAsync(Competence competence, CancellationToken cancellationToken = default)
            => await _db.Competences.AddAsync(competence, cancellationToken);

        public async Task AddAsync(StaffPerson person, CancellationToken cancellationToken = default)
            => await _db.StaffPeople.AddAsync(person, cancellationToken);

        public async Task AddAsync(TrainingRecord record, CancellationToken cancellationToken = default)
            => await _db.TrainingRecords.AddAsync(record, cancellationToken);

        public async Task AddAsync(PolicyAcknowledgement acknowledgement, CancellationToken cancellationToken = default)
            => await _db.PolicyAcknowledgements.AddAsync(acknowledgement, cancellationToken);
    }
}
