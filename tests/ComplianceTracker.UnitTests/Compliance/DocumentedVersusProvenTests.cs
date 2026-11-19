using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.WorkItems;
using ComplianceTracker.Web.Models;

namespace ComplianceTracker.UnitTests.Compliance;

// US-204: documentation describes how we work; proof shows that it happened. Every test follows Arrange / Act / Assert.
public class DocumentedVersusProvenTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Monday);

    private static Requirement Requirement(string code = "I.2(d)")
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "CRA demo", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        return framework.LatestVersion.AddRequirement(code, "Title " + code, null);
    }

    private static Evidence Link(string title = "Report to CSIRT", DateOnly? validUntil = null)
        => Evidence.ForLink(Project, title, "https://example.com/" + Guid.NewGuid(), validUntil, Anna, null, Monday);

    private static Evidence Procedure()
    {
        var file = EvidenceFile.Create("procedure.pdf", Encoding.ASCII.GetBytes("%PDF-1.4 procedure"));
        var document = OrganisationDocument.Create(Guid.NewGuid(), OrganisationDocumentKind.Policy, "Incident handling procedure", Anna, "v1", file, null, Anna, Monday);
        return Evidence.FromOrganisation(Project, document, Monday);
    }

    private static EvidenceItem Item(Evidence evidence) => new(evidence, evidence.IsExpiredOn(Today));

    private static RequirementCoverage Row(bool organisationArea = false, bool enough = false, bool linked = false, params Evidence[] evidence)
        => new(Requirement(), linked ? [LinkedStory()] : [])
        {
            EvidenceCovers = organisationArea,
            Documentation = (enough, enough ? "Asks for the document" : null, false),
            Evidence = evidence.Select(Item).ToList()
        };

    private static LinkedWorkItem LinkedStory()
        => new(RequirementLink.Create(Project, Guid.NewGuid(), Guid.NewGuid(), Anna, Monday),
            WorkItem.Create(Project, WorkItemSource.GitHub, new ExternalWorkItem("#4", "https://github.com/x/y/issues/4", "[US-004] Report incidents", "", "", "", "user-story", false), Monday));

    // ---------------------------------------------------------- The kind (R2)

    [Fact]
    public void A_link_or_a_file_is_proof_by_default()
        => Assert.Equal(EvidenceKind.Proof, Link().Kind);

    [Fact]
    public void An_organisation_document_is_documentation_by_default()
        => Assert.Equal(EvidenceKind.Documentation, Procedure().Kind);

    [Fact]
    public void A_diagram_is_documentation_also_when_it_could_not_be_read()
    {
        var diagram = Link("Architecture");
        diagram.DiagramUnreadable("not a drawio file");

        Assert.Equal(EvidenceKind.Documentation, diagram.Kind);
    }

    [Fact]
    public void A_person_can_change_the_kind_and_it_is_remembered()
    {
        var report = Procedure();

        var changed = report.ChangeKind(EvidenceKind.Proof, Anna, Monday);

        Assert.True(changed);
        Assert.Equal((EvidenceKind.Proof, EvidenceKind.Proof, Anna, Monday), (report.Kind, report.ChosenKind!.Value, report.KindChangedById!.Value, report.KindChangedAt!.Value));
    }

    [Fact]
    public void Changing_to_the_kind_it_already_is_changes_nothing()
    {
        var link = Link();

        Assert.False(link.ChangeKind(EvidenceKind.Proof, Anna, Monday));
        Assert.Null(link.ChosenKind);
    }

    [Fact]
    public void Removed_evidence_cannot_change_kind()
    {
        var link = Link();
        link.Remove(Anna, Monday);

        Assert.Equal("The evidence is removed", Assert.Throws<DomainException>(() => link.ChangeKind(EvidenceKind.Documentation, Anna, Monday)).Message);
    }

    [Fact]
    public void An_unknown_kind_is_refused()
        => Assert.Throws<DomainException>(() => Link().ChangeKind((EvidenceKind)7, Anna, Monday));

    // ---------------------------------------------------------- Proven or documented (R3, R4, R6, R9)

    [Fact]
    public void Only_documentation_is_documented_with_proof_missing()
    {
        var row = Row(evidence: Procedure());

        Assert.Equal((false, true, RequirementCoverage.DocumentedProofMissing), (row.IsProven, row.IsDocumentedOnly, row.ProofState));
    }

    [Fact]
    public void Only_proof_is_proven()
    {
        var row = Row(evidence: Link());

        Assert.Equal((true, false, "Proven"), (row.IsProven, row.IsDocumentedOnly, row.ProofState));
    }

    [Fact]
    public void Proof_and_documentation_is_proven_with_both_lists()
    {
        var row = Row(evidence: [Procedure(), Link()]);

        Assert.True(row.IsProven);
        Assert.Equal(["Report to CSIRT"], row.ProvenBy.Select(e => e.Evidence.Title));
        Assert.Equal(["Incident handling procedure"], row.DocumentedBy.Select(e => e.Evidence.Title));
    }

    [Fact]
    public void Expired_proof_with_valid_documentation_is_documented_with_proof_missing()
    {
        var row = Row(evidence: [Procedure(), Link(validUntil: Today.AddDays(-1))]);

        Assert.Equal((false, RequirementCoverage.DocumentedProofMissing), (row.IsProven, row.ProofState));
    }

    [Fact]
    public void No_evidence_has_no_proof_state()
        => Assert.Null(Row().ProofState);

    [Fact]
    public void A_requirement_that_does_not_apply_has_no_proof_state()
    {
        var row = Row(evidence: Link()) with { NotApplicableReason = "No network" };

        Assert.Null(row.ProofState);
    }

    // ---------------------------------------------------------- "Documentation is enough" (R5, R8)

    [Fact]
    public void Documentation_is_enough_proves_it_alone()
    {
        var row = Row(enough: true, evidence: Procedure());

        Assert.Equal((true, "Proven"), (row.IsProven, row.ProofState));
    }

    [Fact]
    public void Documentation_is_enough_still_needs_the_documentation()
        => Assert.False(Row(enough: true).IsProven);

    // ---------------------------------------------------------- The organisation area and a product (R9)

    [Fact]
    public void In_the_organisation_area_documentation_alone_does_not_cover()
    {
        var row = Row(organisationArea: true, evidence: Procedure());

        Assert.False(row.IsCovered);
        Assert.Equal(RequirementCoverage.DocumentedProofMissing, RequirementPages.Status(row));
    }

    [Fact]
    public void In_the_organisation_area_proof_covers()
    {
        var row = Row(organisationArea: true, evidence: Link());

        Assert.True(row.IsCovered);
        Assert.Equal("Covered", RequirementPages.Status(row));
    }

    [Fact]
    public void In_a_product_a_story_covers_and_proof_is_a_state_of_its_own()
    {
        var row = Row(linked: true, evidence: Procedure());

        Assert.Equal(("Covered", RequirementCoverage.DocumentedProofMissing), (RequirementPages.Status(row), row.ProofState));
    }

    [Fact]
    public void In_a_product_documentation_alone_is_not_covered()
        => Assert.Equal("Not covered", RequirementPages.Status(Row(evidence: Procedure())));

    // ---------------------------------------------------------- The step list asks for proof (US-200)

    [Fact]
    public void The_evidence_step_asks_for_proof_when_there_is_only_documentation()
    {
        var step = RequirementPath.Steps(Row(evidence: Procedure()), [], "/evidence").Single(s => s.Text == "Evidence on the requirement");

        Assert.Equal((false, "Only documentation: add proof that it is done", "Add proof"), (step.Done, step.Missing, step.ActionText));
    }

    [Fact]
    public void The_evidence_step_is_done_with_proof()
        => Assert.True(RequirementPath.Steps(Row(evidence: Link()), [], "/evidence").Single(s => s.Text == "Evidence on the requirement").Done);
}

// US-204 R5, R8: the decision and the built-in marks
public class DocumentationDecisionTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, "Give a reason why documentation is enough")]
    [InlineData(false, "Give a reason why proof is needed")]
    public void A_decision_needs_a_reason(bool enough, string message)
        => Assert.Equal(message, Assert.Throws<DomainException>(() => DocumentationDecision.Decide(Guid.NewGuid(), Guid.NewGuid(), enough, "  ", Anna, null, Monday)).Message);

    [Fact]
    public void A_reason_that_is_too_long_is_refused()
        => Assert.Throws<DomainException>(() => DocumentationDecision.Decide(Guid.NewGuid(), Guid.NewGuid(), true, new string('x', DocumentationDecision.MaxReasonLength + 1), Anna, null, Monday));

    [Fact]
    public void The_reason_is_trimmed_and_kept()
    {
        var decision = DocumentationDecision.Decide(Guid.NewGuid(), Guid.NewGuid(), true, "  Asks for the policy  ", Anna, null, Monday);

        Assert.Equal((true, "Asks for the policy", Anna, Monday), (decision.Enough, decision.Reason, decision.DecidedById, decision.DecidedAt));
    }

    [Theory]
    [InlineData("NIS2 – Directive (EU) 2022/2555", "Art. 21(2)(a)", null, true)]
    [InlineData("NIS2 – Directive (EU) 2022/2555", "Art. 21(2)(f)", null, true)]
    [InlineData("NIS2 – Directive (EU) 2022/2555", "Art. 21(2)(b)", null, false)]
    [InlineData("NIS 2-loven – Lov nr. 434 af 6. maj 2025 (Denmark)", "§ 6, stk. 1, nr. 1", "Art. 21(2)(a)", true)]
    [InlineData("NIS 2-loven – Lov nr. 434 af 6. maj 2025 (Denmark)", "§ 6, stk. 1, nr. 2", "Art. 21(2)(b)", false)]
    [InlineData("NIS2 technical measures – Implementing Regulation (EU) 2024/2690", "Annex 1", "Art. 21(2)(a)", true)]
    [InlineData("CRA – Regulation (EU) 2024/2847", "II.5", null, true)]
    [InlineData("CRA – Regulation (EU) 2024/2847", "Art.31", null, true)]
    [InlineData("CRA – Regulation (EU) 2024/2847", "I.2(d)", null, false)]
    [InlineData("AI Act – Regulation (EU) 2024/1689", "Art. 11", null, true)]
    [InlineData("AI Act – Regulation (EU) 2024/1689", "Art. 17", null, true)]
    [InlineData("AI Act – Regulation (EU) 2024/1689", "Art. 9", null, false)]
    public void The_built_in_requirements_that_ask_for_a_document_are_marked(string framework, string code, string? implements, bool enough)
        => Assert.Equal(enough, DocumentationRules.BuiltInEnough(true, framework, code, implements));

    [Fact]
    public void An_uploaded_framework_is_never_marked_even_with_the_same_name_and_code()
        => Assert.False(DocumentationRules.BuiltInEnough(false, "CRA – Regulation (EU) 2024/2847", "II.5", null));

    [Fact]
    public void A_decision_wins_over_the_built_in_mark()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Framework.Create(Guid.NewGuid(), "ISO demo", CatalogFile.Create("f.pdf", bytes), Anna, Monday);
        var requirement = framework.LatestVersion.AddRequirement("5.2", "Policy", null);
        var decision = DocumentationDecision.Decide(framework.OwnerOrganizationId!.Value, requirement.Id, true, "It asks for the policy", Anna, null, Monday);

        Assert.Equal((false, null, false), DocumentationRules.Resolve(framework, requirement, null));
        Assert.Equal((true, "It asks for the policy", false), DocumentationRules.Resolve(framework, requirement, decision));
    }
}
