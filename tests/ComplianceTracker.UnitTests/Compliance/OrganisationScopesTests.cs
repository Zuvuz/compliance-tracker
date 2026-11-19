using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Compliance;

// US-115: where a requirement the organisation follows is met, and the products' proof of those carried out in the
// projects. Every test follows Arrange / Act / Assert.
public class OrganisationScopesTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Monday);
    private static readonly (Guid Id, string Name) Portal = (Guid.NewGuid(), "Acme Portal");
    private static readonly (Guid Id, string Name) App = (Guid.NewGuid(), "Acme App");

    private static Framework Catalogue(string name, params string[] codes)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Org, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday, FrameworkLevel.Organisation);
        foreach (var code in codes)
        {
            framework.LatestVersion.AddRequirement(code, $"Title {code}", null);
        }

        return framework;
    }

    private static FrameworkCompliance Compliance(Framework framework)
        => new(framework, framework.LatestVersion, framework.LatestVersion.Requirements.Select(r => new RequirementCoverage(r, []) { EvidenceCovers = true }).ToList());

    private static (EvidenceLink, Evidence) Proof((Guid Id, string Name) project, Requirement requirement, DateOnly? validUntil = null, EvidenceKind kind = EvidenceKind.Proof)
    {
        var evidence = Evidence.ForLink(project.Id, "SAST scan", "https://example.com/" + Guid.NewGuid(), validUntil, Anna, null, Monday);
        if (kind != EvidenceKind.Proof)
        {
            evidence.ChangeKind(kind, Anna, Monday);
        }

        return (EvidenceLink.Create(project.Id, evidence.Id, requirement.Id, Anna, null, Monday), evidence);
    }

    private static RequirementCoverage CarriedOut(Framework framework, IReadOnlyList<(EvidenceLink, Evidence)> links, IReadOnlyList<RequirementApplicability>? optOuts = null)
    {
        var requirement = framework.LatestVersion.Requirements[0];
        var decision = RequirementScopeDecision.Decide(Org, requirement.Id, RequirementScope.CarriedOutInProjects, "Done in each product", Anna, null, Monday);
        var scoped = OrganisationScopes.Scope([Compliance(framework)], new Dictionary<Guid, RequirementScopeDecision> { [requirement.Id] = decision });
        return OrganisationScopes.CarriedOut(scoped, [Portal, App], links, optOuts ?? [], Today)[0].Requirements[0];
    }

    // ---------------------------------------------------------- The defaults (R1, R2)

    [Theory]
    [InlineData("ISO/IEC 27001:2022", "A.8.25", true)]
    [InlineData("ISO/IEC 27001:2022", "A.8.32", true)]
    [InlineData("ISO27001 – Demo", "a.8.28", true)]
    [InlineData("ISO/IEC 27001:2022", "A.8.24", false)]
    [InlineData("ISO/IEC 27001:2022", "A.8.33", false)]
    [InlineData("ISO/IEC 27001:2022", "A.5.1", false)]
    [InlineData("ISO/IEC 27001:2022", "5.2", false)]
    [InlineData("ISO/IEC 27002:2022", "8.25", false)]
    [InlineData("NIS2 – Directive (EU) 2022/2555", "A.8.25", false)]
    public void Only_ISO_27001s_development_and_change_controls_are_carried_out_in_the_projects_by_default(string framework, string code, bool carried)
        => Assert.Equal(carried, FrameworkProfiles.CarriedOutInProjectsByDefault(framework, code));

    [Fact]
    public void Everything_else_is_organisation_wide_and_a_decision_wins_over_the_default()
    {
        var iso = Catalogue("ISO/IEC 27001:2022", "A.5.1", "A.8.25");
        var (policy, development) = (iso.LatestVersion.Requirements[0], iso.LatestVersion.Requirements[1]);
        var decision = RequirementScopeDecision.Decide(Org, development.Id, RequirementScope.OrganisationWide, "One central team builds everything", Anna, null, Monday);

        Assert.Equal((RequirementScope.OrganisationWide, null), OrganisationScopes.Resolve(iso, policy, null));
        Assert.Equal(RequirementScope.CarriedOutInProjects, OrganisationScopes.Resolve(iso, development, null).Scope);
        Assert.Equal((RequirementScope.OrganisationWide, "One central team builds everything"), OrganisationScopes.Resolve(iso, development, decision));
    }

    // ---------------------------------------------------------- "Up": the products' proof

    [Fact]
    public void Proof_in_every_product_covers_the_organisations_requirement()
    {
        var framework = Catalogue("NIS2", "Art. 21(2)(e)");
        var requirement = framework.LatestVersion.Requirements[0];

        var row = CarriedOut(framework, [Proof(Portal, requirement), Proof(App, requirement)]);

        Assert.Equal((true, true, "Proven in Acme App, Acme Portal"), (row.CarriedOut!.IsComplete, row.IsCovered, row.CarriedOut.Text));
    }

    [Fact]
    public void A_product_without_proof_keeps_it_uncovered_and_is_named()
    {
        var framework = Catalogue("NIS2", "Art. 21(2)(e)");

        var row = CarriedOut(framework, [Proof(Portal, framework.LatestVersion.Requirements[0])]);

        Assert.False(row.IsCovered);
        Assert.Equal("Proven in Acme Portal · No proof yet: Acme App", row.CarriedOut!.Text);
    }

    [Fact]
    public void A_product_that_opted_out_is_not_counted()
    {
        var framework = Catalogue("NIS2", "Art. 21(2)(e)");
        var requirement = framework.LatestVersion.Requirements[0];
        var optOut = RequirementApplicability.NotApplicable(App.Id, requirement.Id, "No software is developed", Anna, Monday);

        var row = CarriedOut(framework, [Proof(Portal, requirement)], [optOut]);

        Assert.True(row.IsCovered);
        Assert.Equal("Proven in Acme Portal", row.CarriedOut!.Text);
    }

    [Fact]
    public void No_proof_at_all_is_not_complete_even_when_every_product_opted_out()
    {
        var framework = Catalogue("NIS2", "Art. 21(2)(e)");
        var requirement = framework.LatestVersion.Requirements[0];
        var optOuts = new[] { Portal, App }.Select(p => RequirementApplicability.NotApplicable(p.Id, requirement.Id, "Outside the scope", Anna, Monday)).ToList();

        var row = CarriedOut(framework, [], optOuts);

        Assert.Equal((false, false, "Proven in no project yet"), (row.CarriedOut!.IsComplete, row.IsCovered, row.CarriedOut.Text));
    }

    [Fact]
    public void Expired_proof_or_documentation_alone_does_not_count()
    {
        var framework = Catalogue("NIS2", "Art. 21(2)(e)");
        var requirement = framework.LatestVersion.Requirements[0];

        var row = CarriedOut(framework, [Proof(Portal, requirement, Today.AddDays(-1)), Proof(App, requirement, kind: EvidenceKind.Documentation)]);

        Assert.Equal("Proven in no project yet · No proof yet: Acme App, Acme Portal", row.CarriedOut!.Text);
    }

    [Fact]
    public void An_organisation_wide_requirement_gathers_nothing_from_the_products()
    {
        var framework = Catalogue("NIS2", "Art. 21(2)(a)");
        var scoped = OrganisationScopes.Scope([Compliance(framework)], new Dictionary<Guid, RequirementScopeDecision>());

        var row = OrganisationScopes.CarriedOut(scoped, [Portal], [Proof(Portal, framework.LatestVersion.Requirements[0])], [], Today)[0].Requirements[0];

        Assert.Equal((RequirementScope.OrganisationWide, null, false), (row.Scope!.Value, row.CarriedOut, row.IsCovered));
    }

    // ---------------------------------------------------------- The decision (R2, R7)

    [Fact]
    public void A_decision_needs_a_reason_and_a_known_scope()
    {
        Assert.Equal("Give a reason why the requirement is met there",
            Assert.Throws<DomainException>(() => RequirementScopeDecision.Decide(Org, Guid.NewGuid(), RequirementScope.CarriedOutInProjects, " ", Anna, null, Monday)).Message);
        Assert.Throws<DomainException>(() => RequirementScopeDecision.Decide(Org, Guid.NewGuid(), (RequirementScope)9, "x", Anna, null, Monday));
        Assert.Throws<DomainException>(() => RequirementScopeDecision.Decide(Org, Guid.NewGuid(), RequirementScope.OrganisationWide, new string('x', 501), Anna, null, Monday));
    }

    [Theory]
    [InlineData(RequirementScope.OrganisationWide, "Organisation-wide")]
    [InlineData(RequirementScope.CarriedOutInProjects, "Carried out in the projects")]
    public void The_scopes_have_names(RequirementScope scope, string name)
        => Assert.Equal(name, RequirementScopeDecision.NameOf(scope));
}
