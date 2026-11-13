using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// Edge-case unit tests of US-080: the industry profiles and the choice. Arrange / Act / Assert.
public class IndustryProfileTests
{
    private static readonly IndustryFrameworks Ids = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly IndustryProfiles Profiles = new(IndustryProfiles.All, Ids);

    private static (Organization Organization, Guid AdminId, Guid DeveloperId) Acme()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Manufacturing, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@example.com"));
        var developer = organization.AddMember(new UserIdentity("carl-sub", "Carl", "carl@example.com"), UserRole.Developer);
        return (organization, organization.Users[0].Id, developer.Id);
    }

    // ---------------------------------------------------------- The profiles (R1, R2, R5, R8)

    [Fact]
    public void Software_suggests_the_CRA_switch_NIS2_check_and_an_own_ISO_copy_but_not_the_AI_Act_without_AI()
    {
        var profile = Profiles.For(Industry.Software, productsUseAi: false);

        Assert.Equal([SuggestionAction.SwitchOn, SuggestionAction.ScopeCheck, SuggestionAction.OwnCopy], profile.Frameworks.Select(f => f.Action));
        Assert.Equal(Ids.Cra, profile.Frameworks[0].FrameworkId);
        Assert.DoesNotContain(profile.Frameworks, f => f.FrameworkId == Ids.AiAct);
        Assert.Null(profile.ComingSoon);
    }

    [Fact]
    public void With_AI_in_the_products_the_AI_Act_is_suggested_after_the_CRA()
    {
        var profile = Profiles.For(Industry.Software, productsUseAi: true);

        Assert.Equal(Ids.AiAct, profile.Frameworks[1].FrameworkId);
    }

    [Fact]
    public void Every_suggestion_has_a_reason_and_every_switch_has_a_framework()
    {
        foreach (var industry in Enum.GetValues<Industry>())
        {
            var profile = Profiles.For(industry, productsUseAi: true);

            Assert.All(profile.Frameworks, f => Assert.False(string.IsNullOrWhiteSpace(f.Reason)));
            Assert.All(profile.Frameworks.Where(f => f.Action == SuggestionAction.SwitchOn), f => Assert.NotNull(f.FrameworkId));
        }
    }

    [Theory]
    [InlineData(Industry.Medtech)]
    [InlineData(Industry.Fintech)]
    public void Medtech_and_Fintech_say_what_is_coming_and_give_the_software_suggestions(Industry industry)
    {
        var profile = Profiles.For(industry, productsUseAi: false);

        Assert.False(string.IsNullOrWhiteSpace(profile.ComingSoon));
        Assert.Equal(Profiles.For(Industry.Software, false).Frameworks, profile.Frameworks);
    }

    [Fact]
    public void Other_gives_no_suggestions_scans_or_reports()
    {
        var profile = Profiles.For(Industry.Other, productsUseAi: true);

        Assert.Empty(profile.Frameworks);
        Assert.Empty(profile.ScanKinds);
        Assert.Empty(profile.Reports);
    }

    [Fact]
    public void Every_industry_has_exactly_one_profile()
        => Assert.Equal(Enum.GetValues<Industry>().Order(), IndustryProfiles.All.Select(p => p.Industry).Order());

    [Fact]
    public void An_unknown_industry_is_refused()
        => Assert.Throws<DomainException>(() => Profiles.For((Industry)99, false));

    // ---------------------------------------------------------- The choice (R1, R6)

    [Fact]
    public void Only_an_Admin_can_choose_the_industry()
    {
        var (acme, _, developer) = Acme();

        Assert.Equal("Only an Admin can choose the industry", Assert.Throws<DomainException>(() => acme.SetIndustry(Industry.Software, false, developer)).Message);
        Assert.Null(acme.Industry);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((Industry)42)]
    public void An_industry_must_be_chosen(Industry? industry)
    {
        var (acme, admin, _) = Acme();

        Assert.Equal("Choose an industry", Assert.Throws<DomainException>(() => acme.SetIndustry(industry, false, admin)).Message);
    }

    [Fact]
    public void Choosing_the_same_again_changes_nothing_but_the_AI_box_is_a_change()
    {
        var (acme, admin, _) = Acme();

        Assert.True(acme.SetIndustry(Industry.Software, false, admin));
        Assert.False(acme.SetIndustry(Industry.Software, false, admin));
        Assert.True(acme.SetIndustry(Industry.Software, true, admin));
        Assert.True(acme.ProductsUseAi);
    }
}
