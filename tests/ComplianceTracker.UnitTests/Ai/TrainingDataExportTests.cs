using ComplianceTracker.Application.Ai;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.UnitTests.Ai;

// Edge-case unit tests of US-123: pseudonyms, the period, the kinds, the masking of names and the sharing rule.
// Arrange / Act / Assert.
public class TrainingDataExportTests
{
    private static readonly HashSet<TrainingKind> All = [TrainingKind.Story, TrainingKind.Evidence, TrainingKind.NotApplicable];

    [Fact]
    public void Pseudonyms_are_stable_within_one_export_and_numbered_per_organisation()
    {
        // Arrange
        var pseudonyms = new Pseudonyms();
        var (acme, beta) = (Guid.NewGuid(), Guid.NewGuid());
        var (portal, app, other) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var first = pseudonyms.Project(acme, portal);
        var second = pseudonyms.Project(acme, app);
        var betaProject = pseudonyms.Project(beta, other);

        // Assert: R5
        Assert.Equal("org-1/project-1", first);
        Assert.Equal("org-1/project-2", second);
        Assert.Equal("org-2/project-1", betaProject);
        Assert.Equal(first, pseudonyms.Project(acme, portal));
        Assert.Equal("org-1", pseudonyms.Organization(acme));
    }

    [Fact]
    public void A_new_export_starts_the_numbers_again()
    {
        // Arrange
        var acme = Guid.NewGuid();
        new Pseudonyms().Organization(Guid.NewGuid());

        // Act / Assert: nothing carries over between exports
        Assert.Equal("org-1", new Pseudonyms().Organization(acme));
    }

    [Theory]
    [InlineData("2026-10-01T00:00:00Z", true)] // the first day, at midnight
    [InlineData("2026-10-31T23:59:59Z", true)] // the last day is included
    [InlineData("2026-09-30T23:59:59Z", false)]
    [InlineData("2026-11-01T00:00:00Z", false)]
    public void The_period_is_whole_days_with_both_ends_included(string decidedAt, bool included)
    {
        // Arrange
        var request = new TrainingDataRequest(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), All);

        // Act / Assert: R6
        Assert.Equal(included, request.Includes(DateTime.Parse(decidedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));
    }

    [Fact]
    public void Without_a_period_every_decision_is_included_and_the_file_name_says_so()
    {
        // Arrange
        var request = new TrainingDataRequest(null, null, All);

        // Act / Assert
        Assert.True(request.Includes(DateTime.MinValue));
        Assert.True(request.Includes(DateTime.MaxValue));
        Assert.Equal("training-data-start-to-now.jsonl", request.FileName);
    }

    [Fact]
    public void Unknown_or_repeated_kinds_are_ignored()
    {
        // Arrange / Act
        var kinds = TrainingDataExport.Kinds(["story", " STORY ", "everything", null!, "not-applicable"]);

        // Assert
        Assert.Equal(new[] { TrainingKind.Story, TrainingKind.NotApplicable }, kinds.Order());
        Assert.Empty(TrainingDataExport.Kinds(null));
    }

    [Theory]
    [InlineData("Anna signs in with OpenID", "[person] signs in with OpenID")]
    [InlineData("Ask anna.jensen@acme.dk or ANNA", "Ask [e-mail] or [person]")]
    [InlineData("Anna Jensen and Jensen approve", "[person] and [person] approve")]
    [InlineData("The Board and Annabel", "The Board and Annabel")] // whole words only; "Bo" is not in "Board"
    [InlineData("Bo signs off", "[person] signs off")]
    [InlineData("", "")]
    public void Names_and_e_mails_are_masked_in_the_texts(string text, string masked)
    {
        // Arrange
        var names = PersonMask.NamesOf(["Anna Jensen", "Bo", " ", null]);

        // Act / Assert: R4
        Assert.Equal(masked, PersonMask.Apply(text, names));
    }

    [Fact]
    public void A_line_needs_both_texts()
    {
        // Arrange / Act / Assert: an empty text teaches nothing
        Assert.False(Line.HasTexts("", "SP-1 Sign in"));
        Assert.False(Line.HasTexts("[US-004] Sign in", "  "));
        Assert.True(Line.HasTexts("[US-004] Sign in", "SP-1 Sign in"));
    }

    [Fact]
    public void A_decision_from_before_sharing_was_switched_on_again_is_not_used()
    {
        // Arrange
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new ComplianceTracker.Domain.Users.UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        var on = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        organization.SetShareAiFeedback(false, organization.Users[0].Id, on.AddDays(-1));
        organization.SetShareAiFeedback(true, organization.Users[0].Id, on);
        var project = Guid.NewGuid();

        // Act / Assert: R2, also at the exact moment
        Assert.False(AiSharing.MayUse(organization, new HashSet<Guid>(), project, on.AddTicks(-1)));
        Assert.True(AiSharing.MayUse(organization, new HashSet<Guid>(), project, on));
        Assert.False(AiSharing.MayUse(organization, new HashSet<Guid> { project }, project, on.AddDays(1)));
    }
}
