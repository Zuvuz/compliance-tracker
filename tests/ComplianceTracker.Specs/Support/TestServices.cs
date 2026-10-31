using ComplianceTracker.Application;
using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Infrastructure.Frameworks;
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
            };
    }

    // ICurrentUser for tests: whoever the scenario says is signed in
    public sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(UserIdentity identity)
        {
            Identity = identity;
        }

        public UserIdentity Identity { get; set; }
    }
}
