using ComplianceTracker.Domain.Users;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.Infrastructure.Persistence.Repositories
{
    // EF Core implementation of IActivityLogRepository. Append-only, like the interface.
    public class ActivityLogRepository : IActivityLogRepository
    {
        private readonly ComplianceTrackerDbContext _db;

        public ActivityLogRepository(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        // Only adds the entry to EF's change tracker. It is saved by IUnitOfWork.SaveChangesAsync.
        public async Task AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default)
            => await _db.ActivityLog.AddAsync(entry, cancellationToken);

        // The shadow key "Id" is a running number, so it breaks ties between entries with the same time
        // US-091: the running number is the order the entries were written in
        public async Task<IReadOnlyList<ActivityLogEntry>> ListInOrderWrittenAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => await _db.ActivityLog
                .AsNoTracking()
                .Where(e => e.OrganizationId == organizationId)
                .OrderBy(e => EF.Property<long>(e, "Id"))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ActivityLogEntry>> ListAboutAsync(Guid organizationId, Guid subjectId, CancellationToken cancellationToken = default)
            => await _db.ActivityLog
                .AsNoTracking()
                .Where(e => e.OrganizationId == organizationId && e.SubjectId == subjectId)
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => EF.Property<long>(e, "Id"))
                .ToListAsync(cancellationToken);

        // US-101 R1-R4, R8: filters, sorts and pages in PostgreSQL; the text is always a parameter (US-094)
        public async Task<(IReadOnlyList<ActivityLogEntry> Entries, int Total)> SearchAsync(ActivityLogSearch search, CancellationToken cancellationToken = default)
        {
            var entries = _db.ActivityLog.AsNoTracking().Where(e => e.OrganizationId == search.OrganizationId);
            if (search.SubjectId is { } subject)
            {
                entries = entries.Where(e => e.SubjectId == subject);
            }

            if (!string.IsNullOrWhiteSpace(search.Text))
            {
                // R1: in the details or the user's name, without regard to case; % and _ in the text are not wildcards
                var pattern = "%" + search.Text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                var users = search.UsersNamed;
                var subjects = search.SubjectsNamed ?? [];
                entries = entries.Where(e => (e.Details != null && EF.Functions.ILike(e.Details, pattern, "\\"))
                                             || (e.UserId != null && users.Contains(e.UserId.Value))
                                             || (e.SubjectId != null && subjects.Contains(e.SubjectId.Value)));
            }

            if (search.NotSubjects is { } notSubjects)
            {
                entries = entries.Where(e => e.SubjectId == null || !notSubjects.Contains(e.SubjectId.Value));
            }

            if (search.FromUtc is { } from)
            {
                entries = entries.Where(e => e.OccurredAt >= from);
            }

            if (search.ToUtc is { } to)
            {
                entries = entries.Where(e => e.OccurredAt < to);
            }

            if (search.Actions is { } actions)
            {
                entries = entries.Where(e => actions.Contains(e.Action));
            }

            if (search.SystemOnly)
            {
                entries = entries.Where(e => e.UserId == null);
            }
            else if (search.UserId is { } user)
            {
                entries = entries.Where(e => e.UserId == user);
            }

            var total = await entries.CountAsync(cancellationToken);
            var ordered = search.Sort switch
            {
                ActivitySort.Oldest => entries.OrderBy(e => e.OccurredAt).ThenBy(e => EF.Property<long>(e, "Id")),
                ActivitySort.Action => entries.OrderBy(e => e.Action).ThenByDescending(e => e.OccurredAt),
                ActivitySort.User => from e in entries
                                     join u in _db.Set<User>() on e.UserId equals (Guid?)u.Id into users
                                     from u in users.DefaultIfEmpty()
                                     orderby u.Name, e.OccurredAt descending
                                     select e,
                _ => entries.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => EF.Property<long>(e, "Id"))
            };

            return (await ordered.Skip(search.Skip).Take(search.Take).ToListAsync(cancellationToken), total);
        }
    }
}
