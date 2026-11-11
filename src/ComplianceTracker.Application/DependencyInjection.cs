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
            services.AddScoped<AskTheAiService>(); // US-106
            services.AddScoped<StoryDraftLinker>(); // US-106 R5
            services.AddScoped<Projects.GapFinder>(); // US-119
            services.AddScoped<RationaleService>();
            services.AddScoped<AiContextBuilder>(); // US-060, US-061
            services.AddScoped<ApplicabilitySuggestionService>(); // US-060
            services.AddScoped<AiEvaluationService>(); // US-116
            services.AddScoped<TrainingDataExport>(); // US-123
            services.AddScoped<Scanning.ScanService>(); // US-078
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
            services.AddScoped<ClassificationService>(); // US-026
            services.AddScoped<Security.SecurityEventService>(); // US-073

            // US-135: four-eyes. The use cases that own an action also do it once it is approved.
            services.AddScoped<Approvals.ApprovalGate>();
            services.AddScoped<Approvals.ApprovalService>();
            services.AddScoped<Approvals.IApprovalExecutor, RiskService>();
            services.AddScoped<Approvals.IApprovalExecutor, Security.SecurityEventService>();
            services.AddScoped<Approvals.IApprovalExecutor, ProjectService>();
            services.AddScoped<Approvals.IApprovalExecutor, Frameworks.FrameworkService>(); // US-137 R5
            services.AddScoped<Compliance.IReportSnapshotSource, Compliance.ReportSnapshotSource>(); // US-146 R8
            services.AddScoped<Organizations.OrganisationDocumentService>(); // US-136
            services.AddScoped<Organizations.OrganisationAreaService>(); // US-142
            services.AddScoped<Organizations.DocumentSuggestionService>(); // US-136 R2
            services.AddScoped<Reports.ReportFiles>(); // US-131
            services.AddScoped<Reports.ReportService>(); // US-050
            services.AddScoped<Reports.ReportSigningService>(); // US-132
            services.AddScoped<Security.SecurityEventSuggestionService>(); // US-128
            services.AddScoped<Security.SecurityEventQueries>();
            services.AddScoped<Reports.TechnicalDocumentationQueries>(); // US-056
            services.AddScoped<MemberQueries>();

            return services;
        }
    }
}
