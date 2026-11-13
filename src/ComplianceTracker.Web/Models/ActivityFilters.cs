using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Domain.ActivityLog;
using System.Globalization;

namespace ComplianceTracker.Web.Models
{
    // US-101 R5, R9: the Activity tab's filters as they stand in the address, e.g.
    // ?q=MFA&from=2026-11-01&to=2026-11-04&group=evidence&user=system&sort=oldest&page=2.
    // A value that cannot be read is ignored, and the page says so.
    public static class ActivityFilters
    {
        public const string System = "system";

        public static readonly IReadOnlyList<(ActivitySort Sort, string Slug, string Title)> Sorts =
        [
            (ActivitySort.Newest, "newest", "Newest first"),
            (ActivitySort.Oldest, "oldest", "Oldest first"),
            (ActivitySort.User, "user", "User"),
            (ActivitySort.Action, "action", "Action")
        ];

        public static string Slug(ActivityGroup group) => group.ToString().ToLowerInvariant();

        public static (ActivityLogQuery Query, IReadOnlyList<string> Ignored) Parse(
            string? q, string? from, string? to, string? group, string? user, string? sort, string? page, IEnumerable<string>? hide = null)
        {
            var ignored = new List<string>();

            DateOnly? Date(string? value, string name)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }

                ignored.Add($"The date '{value}' was not understood and is ignored");
                return null;
            }

            ActivityGroup? chosenGroup = null;
            if (!string.IsNullOrWhiteSpace(group))
            {
                chosenGroup = Enum.GetValues<ActivityGroup>().Cast<ActivityGroup?>().FirstOrDefault(g => Slug(g!.Value) == group.ToLowerInvariant());
                if (chosenGroup is null)
                {
                    ignored.Add($"The action group '{group}' was not understood and is ignored");
                }
            }

            Guid? userId = null;
            var systemOnly = false;
            if (!string.IsNullOrWhiteSpace(user))
            {
                if (user.Equals(System, StringComparison.OrdinalIgnoreCase))
                {
                    systemOnly = true;
                }
                else if (Guid.TryParse(user, out var id))
                {
                    userId = id;
                }
                else
                {
                    ignored.Add($"The user '{user}' was not understood and is ignored");
                }
            }

            var chosenSort = ActivitySort.Newest;
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var match = Sorts.FirstOrDefault(s => s.Slug == sort.ToLowerInvariant());
                if (match.Slug is null)
                {
                    ignored.Add($"The sort '{sort}' was not understood and is ignored");
                }
                else
                {
                    chosenSort = match.Sort;
                }
            }

            var pageNumber = 1;
            if (!string.IsNullOrWhiteSpace(page) && (!int.TryParse(page, out pageNumber) || pageNumber < 1))
            {
                ignored.Add($"The page '{page}' was not understood and is ignored");
                pageNumber = 1;
            }

            // #52: hide=sign-in&hide=members (or hide=sign-in,members, as a page link carries it)
            var hidden = new List<ActivityGroup>();
            foreach (var slug in (hide ?? []).SelectMany(h => (h ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                var match = Enum.GetValues<ActivityGroup>().Cast<ActivityGroup?>().FirstOrDefault(g => Slug(g!.Value) == slug.ToLowerInvariant());
                if (match is null)
                {
                    ignored.Add($"The action group '{slug}' was not understood and is ignored");
                }
                else if (!hidden.Contains(match.Value))
                {
                    hidden.Add(match.Value);
                }
            }

            var query = new ActivityLogQuery(
                string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
                Date(from, "from"),
                Date(to, "to"),
                chosenGroup,
                userId,
                systemOnly,
                chosenSort,
                pageNumber,
                hidden.Count == 0 ? null : hidden);
            return (query, ignored);
        }

        // The query's values for an address, e.g. for the next page
        public static Dictionary<string, string?> RouteValues(ActivityLogQuery query, int? page = null) => new()
        {
            ["q"] = query.Search,
            ["from"] = query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["group"] = query.Group is { } g ? Slug(g) : null,
            ["hide"] = query.Hidden is { Count: > 0 } hidden ? string.Join(",", hidden.Select(Slug)) : null,
            ["user"] = query.SystemOnly ? System : query.UserId?.ToString(),
            ["sort"] = query.Sort == ActivitySort.Newest ? null : Sorts.First(s => s.Sort == query.Sort).Slug,
            ["page"] = (page ?? query.Page) == 1 ? null : (page ?? query.Page).ToString(CultureInfo.InvariantCulture)
        };
    }
}
