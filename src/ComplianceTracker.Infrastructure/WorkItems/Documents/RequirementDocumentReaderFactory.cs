using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.WorkItems;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-083 (B36): the readers are keyed services, e.g. "UseCases:.docx". The factory resolves one by its key.
    // IServiceProvider is only used here in Infrastructure, never in Application (no service locator there).
    public class RequirementDocumentReaderFactory : IRequirementDocumentReaderFactory
    {
        private readonly IServiceProvider _services;

        public RequirementDocumentReaderFactory(IServiceProvider services, IEnumerable<Registration> registrations)
        {
            _services = services;
            Supported = registrations.Select(r => (r.Extension, r.Kind)).ToList();
        }

        public IReadOnlyCollection<(string Extension, DocumentKind Kind)> Supported { get; }

        public IRequirementDocumentReader? For(string extension, DocumentKind kind)
            => _services.GetKeyedService<IRequirementDocumentReader>(Key(extension, kind));

        public static string Key(string extension, DocumentKind kind) => $"{kind}:{extension.ToLowerInvariant()}";

        // Registers the readers and the factory. Used by the app and by the tests that build the use cases themselves.
        public static IServiceCollection AddRequirementDocumentReaders(IServiceCollection services)
        {
            Add<ExcelRequirementReader>(services, ".xlsx", DocumentKind.RequirementSpecification); // US-032
            Add<CsvRequirementReader>(services, ".csv", DocumentKind.RequirementSpecification);    // US-032
            Add<WordRequirementReader>(services, ".docx", DocumentKind.RequirementSpecification);  // US-033
            Add<UseCaseReader>(services, ".docx", DocumentKind.UseCases);                          // US-034
            services.AddSingleton<IRequirementDocumentReaderFactory, RequirementDocumentReaderFactory>();
            return services;
        }

        // The readers hold no state, so one instance of each is enough
        private static void Add<TReader>(IServiceCollection services, string extension, DocumentKind kind)
            where TReader : class, IRequirementDocumentReader
        {
            services.AddKeyedSingleton<IRequirementDocumentReader, TReader>(Key(extension, kind));
            services.AddSingleton(new Registration(extension, kind));
        }

        // What a registration gave a reader for, so the factory can list what it supports
        public sealed record Registration(string Extension, DocumentKind Kind);
    }
}
