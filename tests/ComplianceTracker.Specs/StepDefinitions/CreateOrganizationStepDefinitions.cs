using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Specs.Support;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-001: the rules for creating an organisation, tested directly against OrganizationService
    // with in-memory repositories. Fast, no database or web app.
    [Binding]
    public class CreateOrganizationStepDefinitions : IDisposable
    {
        // Fields pass data from one step to the next ("the baton"). Reqnroll creates a new instance per scenario.
        private readonly InMemoryActivityLogRepository _activityLog = new();

        // The Application layer as the app builds it (AddApplication), with in-memory storage.
        // The signed-in user is whoever the scenario says it is ("I am ...").
        private readonly TestCurrentUser _signedIn = new(TestUsers.Identity("Nobody"));
        private readonly ServiceProvider _services;

        private string _currentUser = "";

        // Set when creation succeeds ...
        private Organization? _organization;

        // ... or when a rule rejects it, so a "Then" step can check the reason
        private DomainException? _error;

        public CreateOrganizationStepDefinitions(ScenarioClock clock)
        {
            var organizations = new InMemoryOrganizationRepository();
            _services = TestServices.Create(clock, _signedIn, services =>
            {
                services.AddSingleton<IOrganizationRepository>(organizations);
                services.AddSingleton<IActivityLogRepository>(_activityLog);
                services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
            }, validateAll: false); // only organisations are faked; the other use cases are not used here
        }

        public void Dispose() => _services.Dispose();

        // ---------------------------------------------------------- Given

        [Given("I am {string}")]
        public void GivenIAm(string name)
        {
            _currentUser = name;
        }

        // Another organisation created by a different user, so only the CVR clashes
        [Given("an organization {string} exists with CVR {string}")]
        public async Task GivenAnOrganizationExistsWithCvr(string name, string cvr)
        {
            await CreateAsAsync("Setup", name, cvr, Sector.Other, OrganizationSize.Small);
        }

        // ---------------------------------------------------------- When

        [When("I create the organization {string} with CVR {string}, sector {string} and size {string}")]
        public Task WhenICreateTheOrganization(string name, string cvr, string sector, string size)
            // Turns the text from the Gherkin, e.g. "Manufacturing", into the enum value
            => TryCreateAsync(name, cvr, Enum.Parse<Sector>(sector), Enum.Parse<OrganizationSize>(size));

        [When("I create an organization with no name and CVR {string}")]
        public Task WhenICreateAnOrganizationWithNoName(string cvr)
            => TryCreateAsync("", cvr, Sector.Manufacturing, OrganizationSize.Small);

        [When("I create the organization {string} with CVR {string}, no sector and size {string}")]
        public Task WhenICreateTheOrganizationWithNoSector(string name, string cvr, string size)
            => TryCreateAsync(name, cvr, null, Enum.Parse<OrganizationSize>(size));

        // US-016 R1: size left out
        [When("I create the organization {string} with CVR {string}, sector {string} and no size")]
        public Task WhenICreateTheOrganizationWithNoSize(string name, string cvr, string sector)
            => TryCreateAsync(name, cvr, Enum.Parse<Sector>(sector), null);

        // A broken rule is not a test failure here: the error is saved and checked by a "Then" step
        private async Task TryCreateAsync(string name, string cvr, Sector? sector, OrganizationSize? size)
        {
            try
            {
                _organization = await CreateAsAsync(_currentUser, name, cvr, sector, size);
            }
            catch (DomainException ex)
            {
                _error = ex;
            }
        }

        // Signs in as the user and runs the use case in its own scope, like one request in the app
        private async Task<Organization> CreateAsAsync(string userName, string name, string cvr, Sector? sector, OrganizationSize? size)
        {
            _signedIn.Identity = TestUsers.Identity(userName);
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<OrganizationService>().CreateAsync(name, cvr, sector, size);
        }

        // ---------------------------------------------------------- Then

        [Then("the organization {string} exists")]
        public void ThenTheOrganizationExists(string name)
        {
            Assert.NotNull(_organization);
            Assert.Equal(name, _organization.Name);
        }

        [Then("{string} is a user in {string} with the role {string}")]
        public void ThenIsAUserInWithTheRole(string userName, string organizationName, string role)
        {
            Assert.NotNull(_organization);
            Assert.Equal(organizationName, _organization.Name);

            var user = Assert.Single(_organization.Users, u => u.Name == userName);
            var parsedRole = Enum.Parse<UserRole>(role);
            Assert.Equal(parsedRole, user.Role);
        }

        [Then("the creation is rejected with the reason {string}")]
        public void ThenTheCreationIsRejectedWithTheReason(string reason)
        {
            Assert.NotNull(_error);
            Assert.Equal(reason, _error.Message);
        }

        // US-016 R2: the entry has exactly the time the clock said
        [Then("the activity log entry for {string} has the time {string}")]
        public void ThenTheActivityLogEntryHasTheTime(string organizationName, string time)
        {
            Assert.NotNull(_organization);
            Assert.Equal(organizationName, _organization.Name);

            var entry = Assert.Single(_activityLog.Entries, e => e.OrganizationId == _organization.Id);
            Assert.Equal(ClockStepDefinitions.Parse(time).UtcDateTime, entry.OccurredAt);
        }

        // US-001 R5: the creation is logged with the right user, action and organisation
        [Then("an activity log entry exists with user {string}, action {string} and organization {string}")]
        public void ThenAnActivityLogEntryExists(string userName, string action, string organizationName)
        {
            Assert.NotNull(_organization);
            Assert.Equal(organizationName, _organization.Name);

            var user = Assert.Single(_organization.Users, u => u.Name == userName);
            var parsedAction = Enum.Parse<ActivityAction>(action);

            Assert.Contains(_activityLog.Entries, e =>
                e.OrganizationId == _organization.Id &&
                e.UserId == user.Id &&
                e.Action == parsedAction);
        }
    }
}