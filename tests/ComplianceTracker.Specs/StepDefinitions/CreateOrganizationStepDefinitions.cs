using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using ComplianceTracker.Specs.Support;
using Reqnroll;
using Xunit;

namespace ComplianceTracker.Specs.StepDefinitions
{
    [Binding]
    public class CreateOrganizationStepDefinitions
    {
        // Felter: "stafetten" mellem steps. Ny instans pr. scenarie.
        private readonly InMemoryActivityLogRepository _activityLog = new();
        private readonly OrganizationService _service;

        private string _currentUser = "";
        private Organization? _organization;
        private DomainException? _error;

        public CreateOrganizationStepDefinitions()
        {
            _service = new OrganizationService(new InMemoryOrganizationRepository(), _activityLog);
        }

        // ---------------------------------------------------------- Given

        [Given("I am {string}")]
        public void GivenIAm(string name)
        {
            _currentUser = name;
        }

        [Given("an organization {string} exists with CVR {string}")]
        public void GivenAnOrganizationExistsWithCvr(string name, string cvr)
        {
            _service.Create(name, cvr, Sector.Other, OrganizationSize.Small, "Setup");
        }

        // ---------------------------------------------------------- When

        [When("I create the organization {string} with CVR {string}, sector {string} and size {string}")]
        public void WhenICreateTheOrganization(string name, string cvr, string sector, string size)
        {
            var parsedSector = Enum.Parse<Sector>(sector);
            var parsedSize = Enum.Parse<OrganizationSize>(size);

            try
            {
                _organization = _service.Create(name, cvr, parsedSector, parsedSize, _currentUser);
            }
            catch (DomainException ex)
            {
                _error = ex;
            }
        }

        [When("I create an organization with no name and CVR {string}")]
        public void WhenICreateAnOrganizationWithNoName(string cvr)
        {
            try
            {
                _organization = _service.Create("", cvr, Sector.Manufacturing, OrganizationSize.Small, _currentUser);
            }
            catch (DomainException ex)
            {
                _error = ex;
            }
        }

        [When("I create the organization {string} with CVR {string}, no sector and size {string}")]
        public void WhenICreateTheOrganizationWithNoSector(string name, string cvr, string size)
        {
            var parsedSize = Enum.Parse<OrganizationSize>(size);

            try
            {
                _organization = _service.Create(name, cvr, null, parsedSize, _currentUser);
            }
            catch (DomainException ex)
            {
                _error = ex;
            }
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