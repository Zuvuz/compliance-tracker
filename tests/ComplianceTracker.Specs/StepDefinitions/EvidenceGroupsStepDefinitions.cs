using ComplianceTracker.Specs.Support;

namespace ComplianceTracker.Specs.StepDefinitions
{
    // US-201: Views/Projects/_EvidenceTab.cshtml (details.evidence-group with data-group, open, .evidence-group-name,
    // .evidence-group-count, .evidence-group-waiting, and the cards .evidence-item[data-title] inside).
    [Binding]
    public class EvidenceGroupsStepDefinitions
    {
        private readonly WebScenario _web;

        public EvidenceGroupsStepDefinitions(WebScenario web) => _web = web;

        private IEnumerable<AngleSharp.Dom.IElement> Groups() => _web.Page!.QuerySelectorAll("#evidence-list details.evidence-group");

        private static string Heading(AngleSharp.Dom.IElement group)
            => $"{group.QuerySelector(".evidence-group-name")?.TextContent.Trim()} {group.QuerySelector(".evidence-group-count")?.TextContent.Trim()}";

        private AngleSharp.Dom.IElement Group(string name)
            => Groups().FirstOrDefault(g => g.QuerySelector(".evidence-group-name")?.TextContent.Trim() == name)
                ?? throw new Xunit.Sdk.XunitException($"The Evidence tab has no group '{name}'. It has: {string.Join(", ", Groups().Select(Heading))}");

        [Then("the evidence groups are {string}")]
        public void ThenTheGroupsAre(string groups)
            => Assert.Equal(groups, string.Join(", ", Groups().Select(Heading)));

        [Then("every evidence group is folded")]
        public void ThenEveryGroupIsFolded()
            => Assert.All(Groups(), g => Assert.False(g.HasAttribute("open"), $"{Heading(g)} is open"));

        [Then("the evidence group {string} is open and says {string}")]
        public void ThenTheGroupIsOpen(string name, string text)
        {
            var group = Group(name);
            Assert.True(group.HasAttribute("open"));
            Assert.Contains(text, group.QuerySelector("summary")?.TextContent ?? "", StringComparison.Ordinal);
        }

        [Then("the evidence {string} is in the group {string}")]
        public void ThenTheEvidenceIsInTheGroup(string title, string name)
            => Assert.Contains(Group(name).QuerySelectorAll(".evidence-item"),
                card => (card.GetAttribute("data-title") ?? "").StartsWith(title, StringComparison.Ordinal));
    }
}
