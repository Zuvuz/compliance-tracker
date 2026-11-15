using ComplianceTracker.Application.Reminders;
using ComplianceTracker.Specs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-183: the reminders run like the hourly job does; the mails go to the pretend mail server (FakeMail).
    // The Reminders page: form#reminder-settings with input[name=on] and #reminder-days, #mail-not-configured.
    [Binding]
    public class ReminderStepDefinitions
    {
        private readonly WebScenario _web;

        public ReminderStepDefinitions(WebScenario web)
        {
            _web = web;
            Mail.Clear(); // the app is shared between scenarios, the mails are not
        }

        private FakeMail Mail => _web.App.Factory.Services.GetRequiredService<FakeMail>();

        [Given("the reminders have run")]
        [When("the reminders run")]
        public async Task WhenTheRemindersRun()
        {
            await using var scope = _web.App.Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ReminderService>().RunAsync();
        }

        [Given("the mail server is down")]
        public void GivenTheMailServerIsDown() => Mail.Fails = true;

        [When("the mail server is up again")]
        public void WhenTheMailServerIsUp() => Mail.Fails = false;

        [Given("no mail server is set up")]
        public void GivenNoMailServer() => Mail.IsConfigured = false;

        [Given("I have turned off every reminder I can")]
        public async Task GivenIHaveTurnedOffEveryReminder()
        {
            await _web.OpenAsync("/Reminders");
            // Only the hidden "on" for the reporting deadlines stays: the checkboxes are left unticked
            var hidden = _web.Page!.QuerySelectorAll("form#reminder-settings input[type=hidden][name=on]").Select(i => i.GetAttribute("value")!).ToList();
            var fields = new List<KeyValuePair<string, string>>(hidden.Select(v => new KeyValuePair<string, string>("on", v)))
            {
                new("__RequestVerificationToken", _web.AntiforgeryToken())
            };
            _web.Response = await _web.Client.PostAsync("/Reminders", new FormUrlEncodedContent(fields));
            _web.Page = await WebScenario.ParseAsync(_web.Response);
            Assert.NotNull(_web.Page.QuerySelector("#reminders-saved"));
        }

        [Given("I set the reminders to {int} days before")]
        public async Task GivenISetTheDays(int days)
        {
            await _web.OpenAsync("/Reminders");
            var fields = _web.Page!.QuerySelectorAll("form#reminder-settings input[name=on]:checked, form#reminder-settings input[type=hidden][name=on]")
                .Select(i => new KeyValuePair<string, string>("on", i.GetAttribute("value")!)).ToList();
            fields.Add(new("daysBefore", days.ToString()));
            fields.Add(new("__RequestVerificationToken", _web.AntiforgeryToken()));
            _web.Response = await _web.Client.PostAsync("/Reminders", new FormUrlEncodedContent(fields));
            _web.Page = await WebScenario.ParseAsync(_web.Response);
            Assert.NotNull(_web.Page.QuerySelector("#reminders-saved"));
        }

        [Then("{string} has received {int} mail(s) about {string}")]
        public void ThenHasReceived(string userName, int count, string about)
        {
            var to = TestUsers.Identity(userName).Email;
            Assert.Equal(count, Mail.Sent.Count(m => m.To == to && m.Subject.Contains(about, StringComparison.Ordinal)));
        }

        [Then("the mail to {string} does not contain {string}")]
        public void ThenTheMailDoesNotContain(string userName, string text)
        {
            var to = TestUsers.Identity(userName).Email;
            Assert.All(Mail.Sent.Where(m => m.To == to), m =>
            {
                Assert.DoesNotContain(text, m.Subject);
                Assert.DoesNotContain(text, m.Body);
            });
        }

        [Then("the Reminders page says that no mail server is set up")]
        public async Task ThenThePageSaysNoMailServer()
        {
            await _web.OpenAsync("/Reminders");
            Assert.NotNull(_web.Page!.QuerySelector("#mail-not-configured"));
        }
    }
}
