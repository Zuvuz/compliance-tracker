using System.Text;
using ComplianceTracker.Domain.Approvals;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.Domain.Tests.Frameworks;

// Edge-case unit tests of US-137 in the domain: the completeness check on a draft, the approval it needs, and the EUR-Lex
// package as a version's file. Arrange / Act / Assert.
public class CompletenessTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static readonly CompletenessCheck Check = new("EUR-Lex 32016R0679", 2, 0, 3, 3, 4, [new("Recital (1)", "Recital")], null, []);

    private static FrameworkVersion Draft()
        => Framework.Create(OrganizationId, "GDPR", CatalogFile.FromEurLex("32016R0679", [1, 2, 3]), AnnaId, Monday).LatestVersion;

    [Fact]
    public void A_version_needs_approval_only_with_a_completeness_check()
    {
        // Arrange
        var version = Draft();

        // Act
        var before = version.NeedsApprovalToActivate;
        version.RecordCompleteness(Check);

        // Assert
        Assert.False(before);
        Assert.True(version.NeedsApprovalToActivate);
    }

    [Fact]
    public void The_check_and_source_are_kept_only_while_a_draft()
    {
        // Arrange
        var version = Draft();
        version.AddRequirement("Art. 5(1)(a)", "Lawfulness", null);
        version.Activate(AnnaId, Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => version.RecordCompleteness(Check));
        Assert.Throws<DomainException>(() => version.RecordSource("https://eur-lex.europa.eu"));
    }

    [Fact]
    public void The_stamp_changes_when_a_requirement_changes_and_not_otherwise()
    {
        // Arrange
        var version = Draft();
        var requirement = version.AddRequirement("Art. 5(1)(a)", "Lawfulness", null);
        var first = version.RequirementsStamp;

        // Act
        var again = version.RequirementsStamp;
        version.ChangeKind(requirement.Id, RequirementKind.Recommendation);
        var afterKind = version.RequirementsStamp;
        version.ChangeRequirement(requirement.Id, "Art. 5(1)(a)", "Lawfulness and fairness", null);
        var afterTitle = version.RequirementsStamp;

        // Assert
        Assert.Equal(first, again);
        Assert.Equal(16, first.Length);
        Assert.NotEqual(first, afterKind);
        Assert.NotEqual(afterKind, afterTitle);
    }

    [Fact]
    public void The_source_is_trimmed_and_a_blank_one_is_none()
    {
        // Arrange
        var version = Draft();

        // Act
        version.RecordSource("  https://eur-lex.europa.eu/x  ");
        var kept = version.SourceReference;
        version.RecordSource(" ");

        // Assert
        Assert.Equal("https://eur-lex.europa.eu/x", kept);
        Assert.Null(version.SourceReference);
    }

    [Fact]
    public void Left_out_parts_are_counted_by_reason_most_first()
    {
        // Arrange
        var check = Check with { LeftOut = [new("Recital (1)", "Recital"), new("Art. 4(1)", "Definition"), new("Recital (2)", "Recital")] };

        // Act / Assert
        Assert.Equal([("Recital", 2), ("Definition", 1)], check.LeftOutByReason);
        Assert.False(check.HasGaps);
        Assert.Equal("EUR-Lex 32016R0679: 2 articles, 0 annexes; 4 requirements; 3 parts left out", check.Summary);
    }

    // ---------------------------------------------------------- The EUR-Lex package as a file

    [Fact]
    public void An_eur_lex_package_is_kept_as_the_versions_file_but_cannot_be_uploaded()
    {
        // Act
        var file = CatalogFile.FromEurLex("32024R2847", Encoding.ASCII.GetBytes("PK"));

        // Assert
        Assert.Equal(("32024R2847.formex.zip", CatalogFormat.Formex, "application/zip"), (file.FileName, file.Format, file.ContentType));
        Assert.Equal(CatalogFormat.Formex, CatalogFile.FormatOf("32024R2847.formex.zip"));
        Assert.Throws<DomainException>(() => CatalogFile.Create("32024R2847.formex.zip", file.Content));
        Assert.Throws<DomainException>(() => CatalogFile.FromEurLex("32024R2847", []));
    }

    // ---------------------------------------------------------- R5: an organisation's approval request

    [Fact]
    public void An_approval_request_can_be_about_the_organisation_and_has_no_project()
    {
        // Act
        var request = ApprovalRequest.Create(OrganizationId, null, ApprovalAction.ActivateFramework, Guid.NewGuid(), "stamp", "Activate GDPR version 1", "{}", AnnaId, Monday);

        // Assert
        Assert.Null(request.ProjectId);
        Assert.Equal("The request is not about a project", Assert.Throws<DomainException>(() => request.ProjectOf).Message);
    }
}
