using System.Text;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComplianceTracker.LoadSeed
{
    // A user of the load test, as Keycloak's test realm knows them
    public record LoadUser(string Sub, string Name, string Email);

    // US-138: a realistic organisation for the load test, written straight to an empty test database with the domain's
    // own rules: 5 projects with the CRA, the AI Act and the organisation's ISO/IEC 27001, about 500 requirements, 1,000
    // stories, 300 pieces of evidence, 200 risks and 5,000 activity log entries. It refuses a database that has data (R6).
    public static class LoadSeed
    {
        public const string Organisation = "Load Test ApS";
        public const int Projects = 5;
        public const int StoriesPerProject = 200;
        public const int EvidencePerProject = 60;
        public const int RisksPerProject = 40;
        public const int LogEntries = 5_000;
        public const int IsoControls = 93;

        public static async Task RunAsync(ComplianceTrackerDbContext db, IReadOnlyList<LoadUser> users, DateTime now, CancellationToken cancellationToken = default)
        {
            if (users.Count == 0)
            {
                throw new InvalidOperationException("Give at least one test user");
            }

            if (await db.Organizations.AnyAsync(cancellationToken))
            {
                throw new InvalidOperationException("The database already has organisations; the load test seeds an empty test database only");
            }

            // The organisation, with the first user as Admin and the others as Compliance Managers and Developers
            var organization = Organization.Create(Organisation, "99999999", Sector.Manufacturing, OrganizationSize.Medium,
                new UserIdentity(users[0].Sub, users[0].Name, users[0].Email));
            for (var i = 1; i < users.Count; i++)
            {
                organization.AddMember(new UserIdentity(users[i].Sub, users[i].Name, users[i].Email), i % 3 == 0 ? UserRole.ComplianceManager : UserRole.Developer);
            }

            db.Organizations.Add(organization);
            var admin = organization.Members.First(u => u.ExternalId == users[0].Sub).Id;

            // The built-in regulations, and the organisation's own ISO/IEC 27001
            db.OrganizationFrameworks.Add(OrganizationFramework.Enable(organization.Id, BuiltInFrameworks.Cra.VersionId, admin, now));
            db.OrganizationFrameworks.Add(OrganizationFramework.Enable(organization.Id, BuiltInFrameworks.AiAct.VersionId, admin, now));
            var pdf = new byte[200];
            Encoding.ASCII.GetBytes("%PDF-").CopyTo(pdf, 0);
            var iso = Framework.Create(organization.Id, "ISO/IEC 27001:2022", CatalogFile.Create("iso27001.pdf", pdf), admin, now);
            for (var i = 1; i <= IsoControls; i++)
            {
                iso.LatestVersion.AddRequirement($"A.{5 + i / 40}.{i % 40 + 1}", $"Control {i}", "A control of Annex A");
            }

            iso.LatestVersion.Activate(admin, now);
            db.Frameworks.Add(iso);

            var cra = await db.Frameworks.Include(f => f.Versions).ThenInclude(v => v.Requirements).SingleAsync(f => f.Id == BuiltInFrameworks.Cra.FrameworkId, cancellationToken);
            var aiAct = await db.Frameworks.Include(f => f.Versions).ThenInclude(v => v.Requirements).SingleAsync(f => f.Id == BuiltInFrameworks.AiAct.FrameworkId, cancellationToken);
            var requirements = cra.LatestVersion.Requirements.Concat(iso.LatestVersion.Requirements).Select(r => r.Id).ToList();

            var entries = new List<ActivityLogEntry>();
            for (var p = 1; p <= Projects; p++)
            {
                var project = Project.Create(organization.Id, $"Product {p}", "A product of the load test", "A connected product sold in the EU",
                    "The firmware and the cloud service", p % 2 == 0 ? LifecyclePhase.Released : LifecyclePhase.Development);
                project.UseFramework(cra, cra.LatestVersion, admin, now);
                project.UseFramework(aiAct, aiAct.LatestVersion, admin, now);
                project.UseFramework(iso, iso.LatestVersion, admin, now);
                db.Projects.Add(project);

                for (var s = 1; s <= StoriesPerProject; s++)
                {
                    var story = WorkItem.Create(project.Id, WorkItemSource.GitHub,
                        new ExternalWorkItem($"#{s}", $"https://github.com/load/product-{p}/issues/{s}", $"[US-{s:000}] Story {s} of product {p}",
                            "As a user I want something, so that it works.", "", "", "user-story", s % 4 == 0), now);
                    db.WorkItems.Add(story);
                    if (s % 2 == 0)
                    {
                        db.RequirementLinks.Add(RequirementLink.Create(project.Id, requirements[(s + p) % requirements.Count], story.Id, admin, now));
                    }
                }

                for (var e = 1; e <= EvidencePerProject; e++)
                {
                    var evidence = Evidence.ForLink(project.Id, $"Evidence {e}", $"https://example.com/product-{p}/evidence/{e}", null, admin, UserRole.Admin, now);
                    db.Evidence.Add(evidence);
                    db.EvidenceLinks.Add(EvidenceLink.Create(project.Id, evidence.Id, requirements[(e * 3 + p) % requirements.Count], admin, UserRole.Admin, now));
                }

                for (var r = 1; r <= RisksPerProject; r++)
                {
                    db.Risks.Add(Risk.Create(project.Id, $"Risk {r} of product {p}", "A risk of the load test", r % 5 + 1, (r * 2) % 5 + 1,
                        admin, RiskTreatment.Reduce, admin, now));
                }

                for (var l = 0; l < LogEntries / Projects; l++)
                {
                    entries.Add(new ActivityLogEntry(organization.Id, admin, ActivityAction.ProjectUpdated, now.AddMinutes(-(LogEntries - entries.Count)),
                        project.Id, $"Description: version {l} → version {l + 1}", UserRole.Admin));
                }
            }

            // The log is a hash chain (US-091): each entry is sealed after the previous one, as the app does
            string? previous = null;
            foreach (var entry in entries)
            {
                entry.Seal(previous);
                previous = entry.Hash;
            }

            db.ActivityLog.AddRange(entries);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
