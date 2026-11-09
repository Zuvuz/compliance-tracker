using System.Text;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Projects;

// Edge-case unit tests of US-142: the organisation area, framework levels, texts per language and the country.
// Arrange / Act / Assert.
public class OrganisationAreaTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static Framework Framework(string name, FrameworkLevel level)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = ComplianceTracker.Domain.Frameworks.Framework.Create(OrganizationId, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday, level);
        var version = framework.LatestVersion;
        version.AddRequirement("Art. 21(2)(b)", "Incident handling", "How incidents are handled.");
        version.Activate(Anna, Monday);
        return framework;
    }

    private static Project Product() => Project.Create(OrganizationId, "Acme Portal", null, "Web shop", null, LifecyclePhase.Development);

    // ---------------------------------------------------------- Levels (R2)

    [Fact]
    public void An_organisation_level_framework_cannot_be_used_by_a_project()
    {
        // Arrange
        var nis2 = Framework("NIS2 – Directive (EU) 2022/2555", FrameworkLevel.Organisation);

        // Act
        var error = Assert.Throws<DomainException>(() => Product().UseFramework(nis2, nis2.LatestVersion, Anna, Monday));

        // Assert
        Assert.Equal("NIS2 applies to the organisation, not to a project", error.Message);
    }

    [Fact]
    public void A_product_framework_cannot_be_used_by_the_organisation_area()
    {
        // Arrange
        var cra = Framework("CRA – Regulation (EU) 2024/2847", FrameworkLevel.Product);

        // Act
        var error = Assert.Throws<DomainException>(() => Project.CreateOrganisationArea(OrganizationId).UseFramework(cra, cra.LatestVersion, Anna, Monday));

        // Assert
        Assert.Equal("CRA applies to a product, not to the organisation", error.Message);
    }

    [Fact]
    public void The_organisation_area_follows_an_organisation_level_framework()
    {
        // Arrange
        var area = Project.CreateOrganisationArea(OrganizationId);
        var nis2 = Framework("NIS2", FrameworkLevel.Organisation);

        // Act
        area.UseFramework(nis2, nis2.LatestVersion, Anna, Monday);

        // Assert
        Assert.NotNull(area.FrameworkInUse(nis2.Id));
        Assert.True(area.IsOrganisationArea);
        Assert.Equal("NIS2", nis2.ShortName);
    }

    [Fact]
    public void An_unknown_level_is_refused()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Framework("X", (FrameworkLevel)7));
    }

    [Fact]
    public void An_uploaded_framework_is_for_products_by_default()
    {
        // Arrange
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);

        // Act
        var framework = ComplianceTracker.Domain.Frameworks.Framework.Create(OrganizationId, "Security policy", CatalogFile.Create("p.pdf", bytes), Anna, Monday);

        // Assert
        Assert.Equal(FrameworkLevel.Product, framework.Level);
    }

    // ---------------------------------------------------------- The area has no product life (R1)

    [Fact]
    public void The_organisation_area_has_no_lifecycle_support_period_or_removal()
    {
        // Arrange
        var area = Project.CreateOrganisationArea(OrganizationId);
        var today = new DateOnly(2026, 11, 9);

        // Act / Assert
        Assert.Equal("The organisation area has no lifecycle phase", Assert.Throws<DomainException>(() => area.ChangePhase(LifecyclePhase.Released, today)).Message);
        Assert.Equal("The organisation area has no support period", Assert.Throws<DomainException>(() => area.SetSupportPeriod(today.AddYears(6), null, today)).Message);
        Assert.Equal("The organisation area cannot be removed", Assert.Throws<DomainException>(() => area.EnsureMayBeRemoved(today)).Message);
        Assert.Throws<DomainException>(() => area.EndSupport(today, null, today));
    }

    // ---------------------------------------------------------- Texts per language (R8)

    private static Requirement Requirement()
    {
        var framework = Framework("NIS2", FrameworkLevel.Organisation);
        return framework.LatestVersion.Requirements.Single();
    }

    [Fact]
    public void A_text_in_the_language_is_shown()
    {
        // Arrange
        var requirement = Requirement();
        requirement.SetText("da", "Håndtering af hændelser", "Hvordan hændelser håndteres.");

        // Act
        var shown = requirement.Show("da");

        // Assert
        Assert.Equal(("Håndtering af hændelser", "Hvordan hændelser håndteres.", true), shown);
    }

    [Fact]
    public void Without_a_text_in_the_language_English_is_shown_and_says_so()
    {
        // Act
        var shown = Requirement().Show("de");

        // Assert
        Assert.Equal(("Incident handling", "How incidents are handled.", false), shown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("EN-gb")]
    [InlineData("not-a-language")]
    public void English_or_an_unknown_language_shows_the_requirement_s_own_text(string? language)
    {
        // Act / Assert
        Assert.Equal(("Incident handling", "How incidents are handled.", true), Requirement().Show(language));
    }

    [Theory]
    [InlineData("DA", "da")]
    [InlineData(" da-DK ", "da")]
    [InlineData("de_AT", "de")]
    [InlineData("dan", null)]
    [InlineData("d1", null)]
    [InlineData(null, null)]
    public void Language_codes_are_normalised(string? input, string? expected)
    {
        // Act / Assert
        Assert.Equal(expected, RequirementText.Normalize(input));
    }

    [Fact]
    public void Setting_a_text_again_replaces_it()
    {
        // Arrange
        var requirement = Requirement();
        requirement.SetText("da", "Første", null);

        // Act
        requirement.SetText("DA", "Anden", null);

        // Assert
        Assert.Equal("Anden", Assert.Single(requirement.Texts).Title);
    }

    [Theory]
    [InlineData("en", "Title", "English is the requirement's own text")]
    [InlineData("xyz", "Title", "The language must be a two-letter code, e.g. \"da\"")]
    [InlineData("da", " ", "The translated title is required")]
    public void A_text_needs_another_language_and_a_title(string language, string title, string message)
    {
        // Act / Assert
        Assert.Equal(message, Assert.Throws<DomainException>(() => Requirement().SetText(language, title, null)).Message);
    }

    // ---------------------------------------------------------- Country (R6)

    private static Organization Acme(out Guid adminId, out Guid memberId)
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Medium,
            new UserIdentity("anna", "Anna", "anna@acme.dk"));
        adminId = organization.Members.Single().Id;
        memberId = organization.AddMember(new UserIdentity("bo", "Bo", "bo@acme.dk"), UserRole.Developer).Id;
        return organization;
    }

    [Fact]
    public void An_organisation_is_in_Denmark_until_an_admin_changes_it()
    {
        // Arrange
        var organization = Acme(out var admin, out _);

        // Act
        var changed = organization.SetCountry(" de ", admin);

        // Assert
        Assert.True(changed);
        Assert.Equal("DE", organization.Country);
        Assert.False(organization.SetCountry("DE", admin)); // nothing changes the second time
    }

    [Fact]
    public void Only_an_admin_changes_the_country()
    {
        // Arrange
        var organization = Acme(out _, out var member);

        // Act / Assert
        Assert.Equal("Only an Admin can change the country", Assert.Throws<DomainException>(() => organization.SetCountry("SE", member)).Message);
        Assert.Equal("DK", organization.Country);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("GB")]
    [InlineData("")]
    [InlineData(null)]
    public void Only_EU_countries(string? country)
    {
        // Arrange
        var organization = Acme(out var admin, out _);

        // Act / Assert
        Assert.Equal("Choose an EU country", Assert.Throws<DomainException>(() => organization.SetCountry(country, admin)).Message);
    }
}
