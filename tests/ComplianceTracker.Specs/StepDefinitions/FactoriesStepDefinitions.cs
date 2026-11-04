using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Specs.Support;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-083: the rules for factories (B36), checked on the compiled code and on the web app's container
    [Binding]
    public sealed class FactoriesStepDefinitions : IDisposable
    {
        // Only start-up is tested, so the app never connects to this database
        private const string UnusedDatabase = "Host=unused;Database=unused";

        private readonly ScenarioClock _clock;
        private readonly Dictionary<string, string?> _settings = new();
        private List<Type> _types = [];
        private TestApp? _app;
        private Exception? _startError;
        private IRequirementDocumentReaderFactory? _readers;

        public FactoriesStepDefinitions(ScenarioClock clock)
        {
            _clock = clock;
        }

        public void Dispose() => _app?.Dispose();

        // ---------------------------------------------------------- R5

        [When("the Application and Domain assemblies are read")]
        public void WhenTheAssembliesAreRead()
            => _types = typeof(Application.DependencyInjection).Assembly.GetTypes()
                .Concat(typeof(Domain.Common.DomainException).Assembly.GetTypes())
                .ToList();

        // No constructor, method, field or property takes or holds the container (no service locator)
        [Then("no class depends on IServiceProvider")]
        public void ThenNoClassDependsOnIServiceProvider()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            static bool IsContainer(Type type) => type == typeof(IServiceProvider) || type == typeof(IServiceScopeFactory);

            var offenders = _types
                .SelectMany(type =>
                    type.GetConstructors(all).SelectMany(c => c.GetParameters()).Where(p => IsContainer(p.ParameterType)).Select(_ => type.FullName + " (constructor)")
                    .Concat(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => (m, p))).Where(x => IsContainer(x.p.ParameterType)).Select(x => $"{type.FullName}.{x.m.Name}"))
                    .Concat(type.GetFields(all).Where(f => IsContainer(f.FieldType)).Select(f => $"{type.FullName}.{f.Name}"))
                    .Concat(type.GetProperties(all).Where(p => IsContainer(p.PropertyType)).Select(p => $"{type.FullName}.{p.Name}")))
                // DependencyInjection.AddApplication registers services; it takes IServiceCollection, not the provider
                .Distinct()
                .ToList();

            Assert.True(offenders.Count == 0, "These use IServiceProvider in Application or Domain:\n" + string.Join("\n", offenders));
        }

        // ---------------------------------------------------------- R1, R6: document readers

        [When("the readers are listed from the factory")]
        public void WhenTheReadersAreListed()
        {
            StartApp();
            Assert.Null(_startError);
            _readers = _app!.Factory.Services.GetRequiredService<IRequirementDocumentReaderFactory>();
        }

        [Then("each pair of file type and kind has exactly one reader")]
        public void ThenEachPairHasExactlyOneReader()
        {
            var supported = _readers!.Supported;
            Assert.NotEmpty(supported);
            Assert.Equal(supported.Count, supported.Distinct().Count());
            Assert.All(supported, pair => Assert.NotNull(_readers.For(pair.Extension, pair.Kind)));
        }

        // A reader registered under the wrong key would read files it was not made for
        [Then("each reader says it reads its own file type and kind")]
        public void ThenEachReaderReadsItsOwnPair()
            => Assert.All(_readers!.Supported, pair =>
            {
                var reader = _readers.For(pair.Extension, pair.Kind)!;
                Assert.Equal(pair.Extension, reader.Extension);
                Assert.Equal(pair.Kind, reader.Kind);
            });

        // ---------------------------------------------------------- R4: AI providers

        [Given("the setting {string} is {string}")]
        public void GivenTheSettingIs(string name, string value)
            => _settings[name] = value;

        [When("the app with that setting starts")]
        public void WhenTheAppWithThatSettingStarts()
            => StartApp();

        [Then("it stops with {string}")]
        public void ThenItStopsWith(string message)
        {
            Assert.NotNull(_startError);
            Assert.Contains(message, Messages(_startError));
        }

        // ---------------------------------------------------------- Helpers

        // Building the app runs the start-up checks (ValidateOnStart and the DI validation)
        private void StartApp()
        {
            _app = TestApp.WithTestSignIn(UnusedDatabase, _clock, _settings);
            try
            {
                _ = _app.Factory.Services;
            }
            catch (Exception error)
            {
                _startError = error;
            }
        }

        private static string Messages(Exception error)
        {
            var messages = new List<string>();
            for (Exception? e = error; e is not null; e = e.InnerException)
            {
                messages.Add(e.Message);
                if (e is AggregateException aggregate)
                {
                    messages.AddRange(aggregate.InnerExceptions.Select(Messages));
                }
            }

            return string.Join(" | ", messages);
        }
    }
}
