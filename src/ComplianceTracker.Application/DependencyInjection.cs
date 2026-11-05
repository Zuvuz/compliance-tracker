using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Users;
using ComplianceTracker.Application.WorkItems;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ComplianceTracker.Application
{
    // Registers the Application layer's use cases and queries (US-029 R2).
    // The web app calls AddApplication() once in Program.cs, next to AddInfrastructure().
    // A new use case or query only has to be added here, not in the web app.
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // The clock (US-016). TryAdd, so tests can register a fixed clock first.
            services.TryAddSingleton(TimeProvider.System);

            // Scoped = one instance per HTTP request, sharing the request's DbContext through the repositories
            services.AddScoped<CurrentMember>();
            services.AddScoped<IActivityLogger, ActivityLogger>();

            // Use cases (change data)
            services.AddScoped<OrganizationService>();
            services.AddScoped<UserActivityService>();
            services.AddScoped<FrameworkService>();
            services.AddScoped<ProjectService>();
            services.AddScoped<WorkItemSyncService>();
            services.AddScoped<RequirementDocumentService>();
            services.AddScoped<ComplianceService>();
            services.AddScoped<EvidenceService>(); // US-059
            services.AddScoped<EvidenceQueries>();
            services.AddScoped<EvidenceSuggestionService>(); // US-058
            services.AddScoped<VersionCarryOver>(); // US-069 R6
            services.AddScoped<MappingSuggestionService>();
            services.AddScoped<RationaleService>();
            services.AddScoped<AiFeedbackService>();
            services.AddScoped<RiskService>();
            services.AddScoped<MemberService>();
            services.AddScoped<GetStartedService>(); // US-096
            services.AddScoped<SystemAdminService>();
            services.AddScoped<Feedback.FeedbackService>(); // US-063

            // Queries (read data for the pages)
            services.AddScoped<OrganizationQueries>();
            services.AddScoped<FrameworkQueries>();
            services.AddScoped<ProjectQueries>();
            services.AddScoped<ActivityLog.ActivityLogIntegrityQueries>(); // US-091
            services.AddScoped<ActivityLog.ActivityLogQueries>(); // US-102
            services.AddScoped<WorkItemQueries>();
            services.AddScoped<ComplianceQueries>();
            services.AddScoped<RiskQueries>();
            services.AddScoped<Security.SecurityEventService>(); // US-073
            services.AddScoped<Security.SecurityEventQueries>();
            services.AddScoped<Reports.TechnicalDocumentationQueries>(); // US-056
            services.AddScoped<MemberQueries>();

            return services;
        }
    }
}
