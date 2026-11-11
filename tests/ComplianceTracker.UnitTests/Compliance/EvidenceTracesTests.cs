using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.Compliance;

// Edge-case unit tests of US-146: the thread of one piece of evidence (risks → requirements → stories). Arrange / Act / Assert.
public class EvidenceTracesTests
{
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private sealed class World
    {
        public readonly FrameworkVersion Version;
        public readonly List<RequirementCoverage> Rows = [];
        public readonly List<WorkItem> Stories = [];
        public readonly Evidence Evidence = Evidence.ForLink(Project, "Hardening guide", "https://example.org/guide", null, Anna, null, Monday);

        public World()
        {
            var bytes = new byte[100];
            Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
            Version = Framework.Create(Guid.NewGuid(), "CRA – Regulation (EU) 2024/2847", CatalogFile.Create("cra.pdf", bytes), Anna, Monday).LatestVersion;
        }

        public Requirement Requirement(string code, params WorkItem[] storiesLinked)
        {
            var requirement = Version.AddRequirement(code, "Title " + code, null);
            Rows.Add(new RequirementCoverage(requirement,
                storiesLinked.Select(s => new LinkedWorkItem(RequirementLink.Create(Project, requirement.Id, s.Id, Anna, Monday), s)).ToList()));
            return requirement;
        }

        public WorkItem Story(string title, bool removed = false)
        {
            var story = WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem(title.Split(' ')[0], "https://github.com/x/y/issues/1", title, "", "", "", "", false), Monday);
            if (removed)
            {
                typeof(WorkItem).GetProperty(nameof(WorkItem.State))!.SetValue(story, WorkItemState.Removed);
            }

            Stories.Add(story);
            return story;
        }

        public EvidenceTrace Trace(IEnumerable<Requirement> proves, IEnumerable<WorkItem> linkedStories, params Risk[] risks)
        {
            var row = new EvidenceRow(Evidence, false,
                proves.Select(r => new LinkedRequirement(EvidenceLink.Create(Project, Evidence.Id, r.Id, Anna, null, Monday), r, true)).ToList(), [])
            {
                Stories = linkedStories.Select(s => new LinkedStory(EvidenceStoryLink.Create(Project, Evidence.Id, s.Id, Anna, Monday), s)).ToList()
            };
            var compliance = new ProjectCompliance([new FrameworkCompliance(FrameworkOf(), Version, Rows)], Stories);
            return EvidenceTraces.For(row, compliance, risks);
        }

        private Framework FrameworkOf()
        {
            var bytes = new byte[100];
            Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
            return Framework.Create(Guid.NewGuid(), "CRA – Regulation (EU) 2024/2847", CatalogFile.Create("cra.pdf", bytes), Anna, Monday);
        }
    }

    private static Risk Risk(string title, int likelihood = 3, int impact = 4)
        => Domain.Risks.Risk.Create(Project, title, null, likelihood, impact, Anna, RiskTreatment.Reduce, Anna, Monday);

    [Fact]
    public void Nothing_linked_gives_an_empty_trace()
    {
        // Arrange
        var world = new World();

        // Act
        var trace = world.Trace([], []);

        // Assert
        Assert.True(trace.IsEmpty);
    }

    [Fact]
    public void A_risk_is_reached_through_a_requirement_the_evidence_proves()
    {
        // Arrange
        var world = new World();
        var requirement = world.Requirement("I.2(b)");
        var risk = Risk("No secure default config");
        risk.AddMeasure(requirement.Id, null);

        // Act
        var trace = world.Trace([requirement], [], risk);

        // Assert
        var entry = Assert.Single(trace.Risks);
        Assert.Equal(("No secure default config", "via I.2(b)", "High"), (entry.Title, entry.Via, entry.Detail));
    }

    [Fact]
    public void A_risk_reached_through_two_paths_is_shown_once_with_both()
    {
        // Arrange
        var world = new World();
        var story = world.Story("[US-061] Secure defaults");
        var first = world.Requirement("I.2(a)");
        var second = world.Requirement("I.2(b)", story);
        var risk = Risk("Weak config");
        risk.AddMeasure(first.Id, null);
        risk.AddMeasure(null, story.Id);

        // Act
        var trace = world.Trace([first, second], [], risk);

        // Assert
        Assert.Equal("via I.2(a), [US-061] Secure defaults", Assert.Single(trace.Risks).Via);
    }

    [Fact]
    public void A_risk_whose_action_names_the_evidence_is_reached()
    {
        // Arrange
        var world = new World();
        var risk = Risk("Phishing");
        var action = risk.AddAction("Train staff", Anna, null, Anna, Monday);
        risk.LinkAction(action.Id, null, world.Evidence.Id);

        // Act
        var trace = world.Trace([], [], risk);

        // Assert
        Assert.Equal("via the action \"Train staff\"", Assert.Single(trace.Risks).Via);
    }

    [Fact]
    public void Risks_come_highest_first_and_an_unrelated_risk_is_left_out()
    {
        // Arrange
        var world = new World();
        var requirement = world.Requirement("I.2(b)");
        var low = Risk("Low one", 2, 2);
        var high = Risk("High one", 5, 5);
        low.AddMeasure(requirement.Id, null);
        high.AddMeasure(requirement.Id, null);

        // Act
        var trace = world.Trace([requirement], [], low, high, Risk("Elsewhere"));

        // Assert
        Assert.Equal(["High one", "Low one"], trace.Risks.Select(r => r.Title));
    }

    [Fact]
    public void A_story_linked_to_the_evidence_comes_before_stories_via_requirements_and_each_once()
    {
        // Arrange
        var world = new World();
        var direct = world.Story("[US-004] Sign in");
        var viaBoth = world.Story("[US-061] Secure defaults");
        var first = world.Requirement("I.2(a)", viaBoth, direct);
        var second = world.Requirement("I.2(b)", viaBoth);

        // Act
        var trace = world.Trace([first, second], [direct]);

        // Assert
        Assert.Equal(["[US-004] Sign in", "[US-061] Secure defaults"], trace.Stories.Select(s => s.Title));
        Assert.Equal(("linked to the evidence", "via I.2(a), I.2(b)"), (trace.Stories[0].Via, trace.Stories[1].Via));
    }

    [Fact]
    public void A_removed_story_is_not_in_the_trace()
    {
        // Arrange
        var world = new World();
        var removed = world.Story("[US-009] Gone", removed: true);
        var requirement = world.Requirement("I.2(b)", removed);

        // Act
        var trace = world.Trace([requirement], []);

        // Assert
        Assert.Empty(trace.Stories);
    }
}
