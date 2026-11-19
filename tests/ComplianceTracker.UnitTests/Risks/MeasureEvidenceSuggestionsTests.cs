using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.UnitTests.Risks;

// US-161: the project's evidence suggested for a suggested measure. Every test follows Arrange / Act / Assert.
public class MeasureEvidenceSuggestionsTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Monday);

    // A pretend model: the evidence titled in Scores gets that score against every requirement; the rest 0
    private sealed class Models(Dictionary<string, double> scores, bool down = false, double minimum = 0.5) : IAiModels, IEmbeddingModel, IAiLimits
    {
        public string Name => "fake-embed";
        public double MinimumScoreForStories => minimum;
        public double MinimumScoreForEvidence => minimum;
        public int MinimumDecisionsForEvaluation => 1;

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (down)
            {
                throw new AiUnavailableException("down");
            }

            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(t =>
            {
                // The measure's requirement ("I.2(b) …") or story ("[US-004] …") is the query; everything else is evidence
                var title = t.Split('\n')[0];
                if (title.StartsWith("I.2(b)", StringComparison.Ordinal) || title.StartsWith("[US-", StringComparison.Ordinal))
                {
                    return new[] { 1f, 0f };
                }

                var score = scores.TryGetValue(title, out var s) ? s : 0.0;
                return new[] { (float)score, (float)Math.Sqrt(1 - score * score) };
            }).ToList());
        }

        public IEmbeddingModel Embedding(AiTask task) => this;
        public IRationaleWriter Writer(AiTask task) => throw new NotSupportedException();
        public IReranker? Reranker() => null;
        public IReadOnlyList<AiTaskModel> Overview() => [];
    }

    private static (ProjectCompliance Compliance, Requirement Requirement, WorkItem Story) Project_()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA demo", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        var requirement = framework.LatestVersion.AddRequirement("I.2(b)", "Secure by default", null);
        var story = WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Harden defaults", "", "", "", "user-story", false), Monday);
        return (new ProjectCompliance([new FrameworkCompliance(framework, framework.LatestVersion, [new RequirementCoverage(requirement, [])])], [story]), requirement, story);
    }

    private static EvidenceRow Row(string title, DateOnly? validUntil = null, Requirement? linkedTo = null, WorkItem? story = null)
    {
        var evidence = Evidence.ForLink(Project, title, "https://example.com/" + Guid.NewGuid(), validUntil, Anna, null, Monday);
        var links = linkedTo is null ? new List<LinkedRequirement>() : [new LinkedRequirement(EvidenceLink.Create(Project, evidence.Id, linkedTo.Id, Anna, null, Monday), linkedTo, true)];
        return new EvidenceRow(evidence, evidence.IsExpiredOn(Today), links, [])
        {
            Stories = story is null ? [] : [new LinkedStory(EvidenceStoryLink.Create(Project, evidence.Id, story.Id, Anna, Monday), story)]
        };
    }

    private static RiskMeasureSuggestion Measure(Guid? requirementId, Guid? workItemId = null)
        => RiskMeasureSuggestion.Create(Project, Guid.NewGuid(), requirementId, workItemId, 0.74, "fake-embed", Monday);

    [Fact]
    public async Task The_best_three_over_the_limit_are_suggested_best_first()
    {
        var (compliance, requirement, _) = Project_();
        var measure = Measure(requirement.Id);
        var models = new Models(new() { ["A"] = 0.9, ["B"] = 0.8, ["C"] = 0.7, ["D"] = 0.6, ["E"] = 0.4 });

        var view = await new MeasureEvidenceSuggestions(models, models).ForAsync([measure], [Row("E"), Row("C"), Row("A"), Row("D"), Row("B")], compliance, Today);

        Assert.Equal(["A", "B", "C"], view.For(measure.Id).Select(e => e.Evidence.Title));
        Assert.Null(view.Unavailable);
    }

    [Fact]
    public async Task Evidence_already_on_the_requirement_expired_or_below_the_limit_is_left_out()
    {
        var (compliance, requirement, _) = Project_();
        var measure = Measure(requirement.Id);
        var models = new Models(new() { ["On it"] = 0.9, ["Expired"] = 0.9, ["Weak"] = 0.3, ["Good"] = 0.7 });

        var view = await new MeasureEvidenceSuggestions(models, models).ForAsync([measure],
            [Row("On it", linkedTo: requirement), Row("Expired", validUntil: Today.AddDays(-1)), Row("Weak"), Row("Good")], compliance, Today);

        Assert.Equal(["Good"], view.For(measure.Id).Select(e => e.Evidence.Title));
    }

    [Fact]
    public async Task A_story_measure_leaves_out_the_evidence_already_on_the_story()
    {
        var (compliance, _, story) = Project_();
        var measure = Measure(null, story.Id);
        var models = new Models(new() { ["Shows it"] = 0.9, ["Other"] = 0.8 });

        var view = await new MeasureEvidenceSuggestions(models, models).ForAsync([measure], [Row("Shows it", story: story), Row("Other")], compliance, Today);

        Assert.Equal(["Other"], view.For(measure.Id).Select(e => e.Evidence.Title));
    }

    [Fact]
    public async Task No_evidence_in_the_project_asks_the_AI_nothing()
    {
        var (compliance, requirement, _) = Project_();
        var models = new Models([], down: true);

        var view = await new MeasureEvidenceSuggestions(models, models).ForAsync([Measure(requirement.Id)], [], compliance, Today);

        Assert.Same(MeasureEvidenceView.None, view);
    }

    [Fact]
    public async Task The_AI_down_says_so()
    {
        var (compliance, requirement, _) = Project_();
        var models = new Models([], down: true);

        var view = await new MeasureEvidenceSuggestions(models, models).ForAsync([Measure(requirement.Id)], [Row("A")], compliance, Today);

        Assert.Equal(MeasureEvidenceSuggestions.NotAvailable, view.Unavailable);
    }

    [Fact]
    public void A_measure_whose_requirement_or_story_is_gone_has_nothing_to_compare_with()
    {
        var (compliance, _, _) = Project_();

        Assert.Null(MeasureEvidenceSuggestions.QueryOf(Measure(Guid.NewGuid()), compliance));
        Assert.Null(MeasureEvidenceSuggestions.QueryOf(Measure(null, Guid.NewGuid()), compliance));
        Assert.Null(MeasureEvidenceSuggestions.QueryOf(Measure(Guid.NewGuid()), null));
    }

    [Fact]
    public void Picked_evidence_and_new_evidence_together_are_refused()
    {
        var both = new MeasureEvidence("x", "https://example.com/x", null, null, null, Guid.NewGuid());

        Assert.Equal("Pick a suggested evidence or give a new one, not both", Assert.Throws<DomainException>(both.Check).Message);
        Assert.True(new MeasureEvidence(null, null, null, null, null, Guid.NewGuid()).IsGiven);
    }
}
