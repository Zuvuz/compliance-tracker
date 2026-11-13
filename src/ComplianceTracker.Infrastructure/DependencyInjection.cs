using Microsoft.Extensions.DependencyInjection.Extensions;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Infrastructure.Ai;
using ComplianceTracker.Infrastructure.Frameworks;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.Infrastructure.Persistence.Repositories;
using ComplianceTracker.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using ComplianceTracker.Infrastructure.WorkItems;
using ComplianceTracker.Infrastructure.WorkItems.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Infrastructure
{
    // Connects the interfaces from Application to their database implementations.
    // The web app calls AddInfrastructure() once in Program.cs.
    public static class DependencyInjection
    {
        public const string ConnectionStringName = "ComplianceTracker";

        // US-018: GitHub's API. The integration tests replace the HTTP handler with a fake GitHub.
        public const string GitHubApiAddress = "https://api.github.com/";

        // Registers the database, the repositories and the unit of work.
        // The connection string comes from configuration (ConnectionStrings:ComplianceTracker), e.g. the
        // environment variable ConnectionStrings__ComplianceTracker. Never from appsettings.json in the repo.
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddDbContext<ComplianceTrackerDbContext>((serviceProvider, options) =>
            {
                // Read when the DbContext is first used, so the integration tests can supply their own database
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString(ConnectionStringName)
                    ?? throw new InvalidOperationException(
                        $"Connection string '{ConnectionStringName}' is missing. " +
                        $"Set the environment variable 'ConnectionStrings__{ConnectionStringName}'.");

                options.UseNpgsql(connectionString);

                // US-139: counts the request's queries and logs slow ones
                options.AddInterceptors(serviceProvider.GetRequiredService<QueryCountingInterceptor>());
            });
            services.AddScoped<RequestQueryCounter>();
            services.TryAddSingleton<ICurrentRequestPath, NoRequestPath>();
            services.AddScoped<QueryCountingInterceptor>();
            services.TryAddSingleton<IQueryCountObserver, NoQueryCountObserver>();

            // "Scoped" = one instance per HTTP request. The repositories and the unit of work
            // therefore share the same DbContext and save in the same transaction.
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IFrameworkRepository, FrameworkRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();

            // US-007: reads requirements from PDF text. Holds no state, so one instance is enough.
            services.AddSingleton<IRequirementExtractor, PdfRequirementExtractor>();

            // US-032: one reader per file type of a requirement specification. No state.
            // US-083: keyed by file type and kind, chosen through IRequirementDocumentReaderFactory
            RequirementDocumentReaderFactory.AddRequirementDocumentReaders(services);

            // US-018: the GitHub adapter (B11). A typed HttpClient, so connections are reused safely.
            // GitHub requires a User-Agent; the API version header keeps the JSON format stable.
            services.AddScoped<IWorkItemRepository, WorkItemRepository>();
            services.AddScoped<IRequirementDocumentRepository, RequirementDocumentRepository>();
            services.AddScoped<IRequirementLinkRepository, RequirementLinkRepository>();
            services.AddScoped<IRequirementApplicabilityRepository, RequirementApplicabilityRepository>();
            services.AddScoped<IEvidenceRepository, EvidenceRepository>();
            services.AddSingleton<Application.Reports.IDocumentWriter, Reports.OpenXmlDocumentWriter>(); // US-056
            services.AddSingleton<Application.Reports.ISpreadsheetWriter, Reports.OpenXmlSpreadsheetWriter>(); // US-120
            services.AddSingleton<Application.Reports.IPdfWriter, Reports.PdfPigReportWriter>(); // US-131
            services.AddScoped<Application.Feedback.IFeedbackRepository, FeedbackRepository>(); // US-063
            services.AddScoped<IEvidenceLinkSuggestionRepository, EvidenceLinkSuggestionRepository>(); // US-059
            services.AddScoped<IEvidenceStoryRepository, EvidenceStoryRepository>(); // US-146
            services.AddScoped<Application.Ai.IStoryDraftRepository, StoryDraftRepository>(); // US-106
            services.AddScoped<Application.Projects.IGapDismissalRepository, GapDismissalRepository>(); // US-119
            services.AddScoped<Application.Ai.IRiskMeasureSuggestionRepository, RiskMeasureSuggestionRepository>(); // US-106
            services.AddScoped<IEvidenceSuggestionRepository, EvidenceSuggestionRepository>(); // US-058
            services.AddSingleton<ITestResultsReader, Compliance.TestResultsReader>();
            services.AddSingleton<IEvidenceTextReader, Compliance.EvidenceTextReader>();
            services.AddScoped<IMappingSuggestionRepository, MappingSuggestionRepository>();
            services.AddScoped<IApplicabilitySuggestionRepository, ApplicabilitySuggestionRepository>(); // US-060
            services.AddScoped<IAiEvaluationRepository, AiEvaluationRepository>(); // US-116
            services.AddScoped<Application.Scanning.IScanRepository, ScanRepository>(); // US-078
            services.AddScoped<Application.Scanning.IScanUploadTokenRepository, ScanUploadTokenRepository>(); // US-122
            services.AddScoped<Application.Approvals.IApprovalRequestRepository, ApprovalRequestRepository>(); // US-135
            services.AddScoped<Application.Organizations.IOrganisationDocumentRepository, OrganisationDocumentRepository>(); // US-136
            // US-142: the NIS2 area's storage and the ids of the built-in NIS2 catalogues
            services.AddScoped<Application.Organizations.INis2AssessmentRepository, Nis2AssessmentRepository>();
            services.AddScoped<Application.Organizations.IDocumentLinkSuggestionRepository, DocumentLinkSuggestionRepository>(); // US-136 R2
            services.AddSingleton(new Application.Organizations.Nis2Catalogues(
                Persistence.BuiltInFrameworks.Nis2.FrameworkId, Persistence.BuiltInFrameworks.Nis2Denmark.FrameworkId,
                Persistence.BuiltInFrameworks.Nis2Technical.FrameworkId));
            services.AddScoped<Application.Reports.IReportSnapshotRepository, ReportSnapshotRepository>(); // US-050
            services.AddScoped<Application.Reports.IReportSigningRepository, ReportSigningRepository>(); // US-132
            services.AddSingleton<Application.Reports.IReportSealer, Reports.ReportSealer>();
            services.AddSingleton<Application.Scanning.IScanImporter, Scanning.SarifScanImporter>();
            services.AddScoped<Application.Scanning.ISbomRepository, SbomRepository>(); // US-079
            services.AddSingleton<Application.Scanning.ISbomImporter, Scanning.CycloneDxSbomImporter>();
            services.AddSingleton<Application.Scanning.ISbomImporter, Scanning.SpdxSbomImporter>();
            services.AddScoped<IRiskRepository, RiskRepository>();
            services.AddScoped<Application.Audits.IAuditRepository, AuditRepository>(); // US-134
            services.AddScoped<IRiskImportRepository, RiskImportRepository>(); // US-049
            services.AddSingleton<IRiskSheetReader, Risks.CsvRiskSheetReader>();
            services.AddSingleton<IRiskSheetReader, Risks.XlsxRiskSheetReader>();
            services.AddScoped<Application.Projects.IClassificationRepository, ClassificationRepository>(); // US-026
            services.AddScoped<Application.Security.ISecurityEventRepository, SecurityEventRepository>(); // US-073

            // US-128: suggested events, and the public lists of exploited vulnerabilities the operator switches on
            services.AddScoped<Application.Security.ISecurityEventSuggestionRepository, SecurityEventSuggestionRepository>();
            services.AddScoped<Application.Security.IExploitedListRepository, ExploitedListRepository>();
            services.AddOptions<Security.ExploitedListOptions>().BindConfiguration(Security.ExploitedListOptions.Section);
            services.AddSingleton<Application.Security.IExploitedListSettings, Security.ConfiguredExploitedListSettings>();
            void ListClient(HttpClient client)
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("compliance-tracker");
            }
            services.AddHttpClient<Security.KevExploitedList>(ListClient);
            services.AddHttpClient<Security.EuvdExploitedList>(ListClient);

            // US-137: EU acts from EUR-Lex; off until the operator sets EurLex:Enabled (needs internet)
            services.AddHttpClient<Frameworks.HttpEurLexSource>(ListClient);
            services.AddTransient<Application.Frameworks.IEurLexSource>(provider => provider.GetRequiredService<Frameworks.HttpEurLexSource>());
            services.AddTransient<Application.Security.IExploitedVulnerabilityList>(provider => provider.GetRequiredService<Security.KevExploitedList>());
            services.AddTransient<Application.Security.IExploitedVulnerabilityList>(provider => provider.GetRequiredService<Security.EuvdExploitedList>());
            services.AddScoped<IInvitationRepository, InvitationRepository>();
            services.AddScoped<IGetStartedReadModel, GetStartedReadModel>(); // US-096
            services.AddHttpClient<GitHubWorkItemSource>(client =>
            {
                client.BaseAddress = new Uri(GitHubApiAddress);
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("compliance-tracker");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            });
            services.AddTransient<IWorkItemSource>(provider => provider.GetRequiredService<GitHubWorkItemSource>());
            services.AddTransient<IPullRequestReader>(provider => provider.GetRequiredService<GitHubWorkItemSource>()); // US-058
            services.AddTransient<IGitHubProjects>(provider => provider.GetRequiredService<GitHubWorkItemSource>()); // US-107
            services.AddTransient<Application.Scanning.IGitHubSecurityAlerts>(provider => provider.GetRequiredService<GitHubWorkItemSource>()); // US-121

            // US-063: feedback becomes an issue in the tracker's own repository, with its own token (B42)
            services.AddOptions<Feedback.FeedbackOptions>().BindConfiguration(Feedback.FeedbackOptions.Section);
            services.AddHttpClient<Feedback.GitHubFeedbackIssueWriter>(client =>
            {
                client.BaseAddress = new Uri(GitHubApiAddress);
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("compliance-tracker");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            });
            services.AddTransient<Application.Feedback.IFeedbackIssueWriter>(provider => provider.GetRequiredService<Feedback.GitHubFeedbackIssueWriter>());

            // US-031: encryption of stored tokens. The keys are kept in the database (DataProtectionKeys).
            // The same keys protect the sign-in cookie and antiforgery tokens, so they also survive restarts.
            services.AddDataProtection()
                .SetApplicationName("compliance-tracker")
                .PersistKeysToDbContext<ComplianceTrackerDbContext>();
            services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

            AddAi(services);

            return services;
        }

        // US-020: the local AI. Ai:Provider picks the adapters, Ai:BaseUrl and the model names come from
        // configuration (appsettings.json), so a better model or another server needs no code change.
        private static void AddAi(IServiceCollection services)
        {
            // US-083 R4: an unknown provider stops the app at start, instead of silently using Ollama
            services.AddSingleton<IAiLimits, Ai.ConfiguredAiLimits>(); // US-105
            services.AddOptions<AiOptions>()
                .BindConfiguration(AiOptions.Section)
                .Validate(options => AiProviders.Key(options.Provider) is not null, AiProviders.UnknownMessage)
                .ValidateOnStart();

            void Configure(IServiceProvider provider, HttpClient client)
            {
                var options = provider.GetRequiredService<IOptions<AiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                if (!string.IsNullOrEmpty(options.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
                }
            }

            services.AddHttpClient<OllamaEmbeddingModel>(Configure);
            services.AddHttpClient<OllamaRationaleWriter>(Configure);
            services.AddHttpClient<OpenAiCompatibleEmbeddingModel>(Configure);
            services.AddHttpClient<OpenAiCompatibleRationaleWriter>(Configure);

            // US-083: keyed by provider name; the setting picks one (AiProviders)
            AiProviders.Add(services);

            // US-117: a model per task; ConfiguredAiModels sets the address of each client
            services.AddHttpClient(ConfiguredAiModels.HttpClientName);
            services.AddTransient<IAiModels, ConfiguredAiModels>();
        }
    }
}
