using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Infrastructure.Frameworks;
using ComplianceTracker.Infrastructure.Security;
using ComplianceTracker.Infrastructure.WorkItems;
using Microsoft.AspNetCore.DataProtection;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.Support
{
    // Builds the Application layer for tests that call the use cases directly, without the web app (US-029).
    // It uses the same AddApplication() as the web app, so the tests also prove the registrations work.
    // The caller decides where data is stored: the real test database, or in-memory fakes.
    public static class TestServices
    {
        // validateAll: check that every service can be created, like the web app does (US-029 R5).
        // Off only for in-memory storage that fakes just the repositories a test needs.
        public static ServiceProvider Create(TimeProvider clock, ICurrentUser currentUser, Action<IServiceCollection> storage, bool validateAll = true)
        {
            var services = new ServiceCollection();
            services.AddSingleton(clock);            // before AddApplication, so it replaces TimeProvider.System
            services.AddSingleton(currentUser);
            storage(services);
            services.AddApplication();

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = validateAll, ValidateScopes = true });
        }

        // Storage in the PostgreSQL test database. Interceptors can e.g. make saving fail (US-016 R3).
        public static Action<IServiceCollection> Database(string connectionString, params IInterceptor[] interceptors)
            => services =>
            {
                services.AddScoped(_ => TestDatabase.CreateContext(connectionString, interceptors));
                services.AddScoped<IOrganizationRepository, OrganizationRepository>();
                services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
                services.AddScoped<IFrameworkRepository, FrameworkRepository>();
                services.AddScoped<IProjectRepository, ProjectRepository>();
                services.AddScoped<IUnitOfWork, UnitOfWork>();
                services.AddSingleton<IRequirementExtractor, PdfRequirementExtractor>(); // US-007
                services.AddSingleton<IEurLexSource, NoEurLex>(); // US-137: never reaches a network

                // US-018: not used by these tests, but the use cases must be creatable. Never reaches a network.
                services.AddScoped<IWorkItemRepository, WorkItemRepository>();
                services.AddScoped<IRequirementDocumentRepository, RequirementDocumentRepository>(); // US-032
                ComplianceTracker.Infrastructure.WorkItems.Documents.RequirementDocumentReaderFactory.AddRequirementDocumentReaders(services); // US-083
                services.AddScoped<IRequirementLinkRepository, RequirementLinkRepository>(); // US-021
                services.AddScoped<IRequirementApplicabilityRepository, RequirementApplicabilityRepository>(); // US-043
                services.AddScoped<IEvidenceRepository, EvidenceRepository>(); // US-057
                services.AddScoped<IEvidenceLinkSuggestionRepository, EvidenceLinkSuggestionRepository>(); // US-059
                services.AddScoped<IEvidenceStoryRepository, EvidenceStoryRepository>(); // US-146
                services.AddScoped<ComplianceTracker.Application.Ai.IStoryDraftRepository, StoryDraftRepository>(); // US-106
                services.AddScoped<ComplianceTracker.Application.Ai.IRiskMeasureSuggestionRepository, RiskMeasureSuggestionRepository>(); // US-106
                services.AddScoped<IEvidenceSuggestionRepository, EvidenceSuggestionRepository>(); // US-058
                services.AddSingleton<ITestResultsReader, ComplianceTracker.Infrastructure.Compliance.TestResultsReader>();
                services.AddSingleton<IPullRequestReader>(provider => (GitHubWorkItemSource)provider.GetRequiredService<IWorkItemSource>());
                services.AddSingleton<IGitHubProjects>(provider => (GitHubWorkItemSource)provider.GetRequiredService<IWorkItemSource>()); // US-107
                services.AddSingleton<ComplianceTracker.Application.Scanning.IGitHubSecurityAlerts>(provider => (GitHubWorkItemSource)provider.GetRequiredService<IWorkItemSource>()); // US-121
                services.AddSingleton<IEvidenceTextReader, ComplianceTracker.Infrastructure.Compliance.EvidenceTextReader>();

                // US-020: not used by these tests, but the use cases must be creatable
                services.AddScoped<IMappingSuggestionRepository, MappingSuggestionRepository>();
                services.AddScoped<IApplicabilitySuggestionRepository, ApplicabilitySuggestionRepository>(); // US-060
                services.AddScoped<IAiEvaluationRepository, AiEvaluationRepository>(); // US-116
                services.AddScoped<ComplianceTracker.Application.Scanning.IScanRepository, ScanRepository>(); // US-078
                services.AddScoped<ComplianceTracker.Application.Scanning.IScanUploadTokenRepository, ScanUploadTokenRepository>(); // US-122
                services.AddScoped<ComplianceTracker.Application.Approvals.IApprovalRequestRepository, ApprovalRequestRepository>(); // US-135
                services.AddScoped<ComplianceTracker.Application.Organizations.IOrganisationDocumentRepository, OrganisationDocumentRepository>(); // US-136
                services.AddScoped<ComplianceTracker.Application.Organizations.INis2AssessmentRepository, Nis2AssessmentRepository>(); // US-142
                services.AddScoped<ComplianceTracker.Application.Organizations.IDocumentLinkSuggestionRepository, DocumentLinkSuggestionRepository>(); // US-136 R2
                services.AddSingleton(new ComplianceTracker.Application.Organizations.Nis2Catalogues(
                    ComplianceTracker.Infrastructure.Persistence.BuiltInFrameworks.Nis2.FrameworkId,
                    ComplianceTracker.Infrastructure.Persistence.BuiltInFrameworks.Nis2Denmark.FrameworkId,
                    ComplianceTracker.Infrastructure.Persistence.BuiltInFrameworks.Nis2Technical.FrameworkId));
                services.AddScoped<ComplianceTracker.Application.Reports.IReportSnapshotRepository, ReportSnapshotRepository>(); // US-050
                services.AddScoped<ComplianceTracker.Application.Reports.IReportSigningRepository, ReportSigningRepository>(); // US-132
                services.AddSingleton<ComplianceTracker.Application.Reports.IReportSealer, ComplianceTracker.Infrastructure.Reports.ReportSealer>();
                services.AddSingleton<ComplianceTracker.Application.Reports.IPdfWriter, ComplianceTracker.Infrastructure.Reports.PdfPigReportWriter>(); // US-131
                services.AddSingleton<ComplianceTracker.Application.Scanning.IScanImporter, ComplianceTracker.Infrastructure.Scanning.SarifScanImporter>();
                services.AddScoped<IRiskRepository, RiskRepository>(); // US-042
                services.AddScoped<IClassificationRepository, ClassificationRepository>(); // US-026
                services.AddScoped<ComplianceTracker.Application.Security.ISecurityEventRepository, SecurityEventRepository>(); // US-073
                services.AddScoped<ComplianceTracker.Application.Security.ISecurityEventSuggestionRepository, SecurityEventSuggestionRepository>(); // US-128
                services.AddScoped<ComplianceTracker.Application.Security.IExploitedListRepository, ExploitedListRepository>();
                services.AddSingleton<ComplianceTracker.Application.Security.IExploitedListSettings>(new FakeExploitedLists()); // none switched on
                services.AddScoped<IInvitationRepository, InvitationRepository>(); // US-009
                services.AddScoped<IGetStartedReadModel, GetStartedReadModel>(); // US-096
                services.AddSingleton<FakeAi>();
                services.AddSingleton<IAiLimits>(provider => provider.GetRequiredService<FakeAi>()); // US-105
                services.AddSingleton<IEmbeddingModel>(provider => provider.GetRequiredService<FakeAi>());
                services.AddSingleton<IRationaleWriter>(provider => provider.GetRequiredService<FakeAi>());
                services.AddSingleton<IAiModels>(provider => new FakeAiModels(provider.GetRequiredService<FakeAi>())); // US-117
                services.AddSingleton<IRationaleQueue, NoRationaleQueue>();
                services.AddSingleton<IWorkItemSource>(new GitHubWorkItemSource(new HttpClient { BaseAddress = new Uri("http://github.invalid/") }));
                services.AddSingleton<IWorkItemNotifier, NoWorkItemNotifier>();

                // US-063: not used by these tests, but the use cases must be creatable. Never reaches a network.
                services.AddScoped<ComplianceTracker.Application.Feedback.IFeedbackRepository, FeedbackRepository>();
                services.AddSingleton<ComplianceTracker.Application.Feedback.IFeedbackIssueWriter>(new ComplianceTracker.Infrastructure.Feedback.GitHubFeedbackIssueWriter(
                    new HttpClient { BaseAddress = new Uri("http://github.invalid/") },
                    Microsoft.Extensions.Options.Options.Create(new ComplianceTracker.Infrastructure.Feedback.FeedbackOptions())));

                // US-031: real encryption, with keys that only live while the test runs
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
            };

        private sealed class NoRationaleQueue : IRationaleQueue
        {
            public void Wake()
            {
            }
        }

        private sealed class NoWorkItemNotifier : IWorkItemNotifier
        {
            public Task WorkItemsChangedAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    // ICurrentUser for tests: whoever the scenario says is signed in
    public sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(UserIdentity identity)
        {
            Identity = identity;
        }

        public UserIdentity Identity { get; set; }

        public bool IsSystemAdmin { get; set; }
    }

    // US-137: the use cases must be creatable; these tests never import from EUR-Lex
    internal sealed class NoEurLex : IEurLexSource
    {
        public bool IsEnabled => false;

        public Task<EurLexAct> FetchAsync(string celex, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No EUR-Lex in these tests");
    }
}
