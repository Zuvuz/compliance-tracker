using System.Text;
using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Compliance;

// Unit tests of the AI's evidence check (US-038). Every test follows Arrange / Act / Assert.
public class EvidenceCheckTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Project = Guid.NewGuid();

    private static EvidenceCheck Check(EvidenceVerdict verdict = EvidenceVerdict.PartlyCovers)
        => EvidenceCheck.Record(Project, Guid.NewGuid(), Guid.NewGuid(), verdict, "the recovery codes", "Users sign in with MFA", "abc", "model", "v1", Anna, Monday);

    // ---------------------------------------------------------- The decision (R2, R3)

    [Fact]
    public void A_new_check_is_pending_and_counts_for_nothing()
    {
        var check = Check(EvidenceVerdict.DoesNotCover);

        Assert.Equal((EvidenceCheckStatus.Pending, false, false), (check.Status, check.IsDecided, check.RefutesEvidence));
    }

    [Fact]
    public void Confirming_takes_the_AIs_verdict()
    {
        var check = Check();

        check.Confirm(Anna, UserRole.ComplianceManager, Monday);

        Assert.Equal((EvidenceCheckStatus.Confirmed, EvidenceVerdict.PartlyCovers, UserRole.ComplianceManager), (check.Status, check.DecidedVerdict, check.DecidedRole));
    }

    [Fact]
    public void Overruling_needs_another_verdict_and_a_reason()
    {
        var check = Check();

        Assert.Equal("Choose the verdict", Assert.Throws<DomainException>(() => check.Overrule(null, "x", Anna, null, Monday)).Message);
        Assert.Equal("That is the AI's verdict; confirm it instead",
            Assert.Throws<DomainException>(() => check.Overrule(EvidenceVerdict.PartlyCovers, "x", Anna, null, Monday)).Message);
        Assert.Equal("Give a reason for overruling the AI",
            Assert.Throws<DomainException>(() => check.Overrule(EvidenceVerdict.Covers, "  ", Anna, null, Monday)).Message);
    }

    [Fact]
    public void A_confirmed_or_overruled_does_not_cover_refutes_the_evidence()
    {
        var confirmed = Check(EvidenceVerdict.DoesNotCover);
        confirmed.Confirm(Anna, null, Monday);
        var overruled = Check(EvidenceVerdict.Covers);
        overruled.Overrule(EvidenceVerdict.DoesNotCover, "It is the old login", Anna, null, Monday);

        Assert.True(confirmed.RefutesEvidence);
        Assert.True(overruled.RefutesEvidence);
        Assert.Equal("It is the old login", overruled.DecisionReason);
    }

    [Fact]
    public void A_check_is_decided_once()
    {
        var check = Check();
        check.Confirm(Anna, null, Monday);

        Assert.Throws<DomainException>(() => check.Confirm(Anna, null, Monday));
        Assert.Throws<DomainException>(() => check.Overrule(EvidenceVerdict.Covers, "x", Anna, null, Monday));
    }

    [Fact]
    public void Long_texts_are_cut()
    {
        var check = EvidenceCheck.Record(Project, Guid.NewGuid(), Guid.NewGuid(), EvidenceVerdict.Covers, new string('m', 3000), new string('q', 3000), "abc", "m", "v1", Anna, Monday);

        Assert.Equal((EvidenceCheck.MaxTextLength, EvidenceCheck.MaxTextLength), (check.Missing.Length, check.Quotes.Length));
    }

    // ---------------------------------------------------------- Reading the AI's answer (R1)

    [Theory]
    [InlineData("VERDICT: Covers", EvidenceVerdict.Covers)]
    [InlineData("VERDICT: Partly covers", EvidenceVerdict.PartlyCovers)]
    [InlineData("verdict: partially", EvidenceVerdict.PartlyCovers)]
    [InlineData("VERDICT: Does not cover", EvidenceVerdict.DoesNotCover)]
    [InlineData("VERDICT: **Not covered**", EvidenceVerdict.DoesNotCover)]
    public void The_verdict_is_read_in_the_models_own_words(string line, EvidenceVerdict expected)
        => Assert.Equal(expected, EvidenceCheckPrompt.Parse($"{line}\nMISSING: Nothing\nQUOTES: x").Verdict);

    [Fact]
    public void Missing_and_quotes_may_run_over_several_lines()
    {
        var (_, missing, quotes) = EvidenceCheckPrompt.Parse("VERDICT: Partly covers\nMISSING: the recovery codes\nand the audit log\nQUOTES:\n- Users sign in with MFA\n- Codes are printed");

        Assert.Equal("the recovery codes and the audit log", missing);
        Assert.Equal("Users sign in with MFA\nCodes are printed", quotes);
    }

    [Fact]
    public void An_answer_without_a_verdict_is_refused()
        => Assert.Throws<AiUnavailableException>(() => EvidenceCheckPrompt.Parse("I think it is fine"));

    [Fact]
    public void The_prompt_has_the_requirement_and_the_evidence_or_says_only_the_title_is_known()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var requirement = Framework.Create(Guid.NewGuid(), "CRA", CatalogFile.Create("cra.pdf", bytes), Anna, Monday)
            .LatestVersion.AddRequirement("I.2(d)", "Protection from unauthorised access", "Authentication");

        var prompt = EvidenceCheckPrompt.For(requirement, "MFA config", null);

        Assert.Contains("Requirement I.2(d): Protection from unauthorised access", prompt);
        Assert.Contains("(only the title is known)", prompt);
        Assert.StartsWith(EvidenceCheckPrompt.Marker, prompt.TrimStart());
    }

    // ---------------------------------------------------------- What counts as evidence (R3)

    [Fact]
    public void Evidence_refuted_by_a_person_does_not_count_but_a_pending_check_does()
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var requirement = Framework.Create(Guid.NewGuid(), "CRA", CatalogFile.Create("cra.pdf", bytes), Anna, Monday)
            .LatestVersion.AddRequirement("I.2(d)", "Access", null);
        var evidence = Evidence.ForLink(Project, "MFA config", "https://example.com/mfa", null, Anna, null, Monday);
        var refuted = Check(EvidenceVerdict.DoesNotCover);
        refuted.Confirm(Anna, null, Monday);

        var pending = new RequirementCoverage(requirement, []) { Evidence = [new EvidenceItem(evidence, false) { Check = Check(EvidenceVerdict.DoesNotCover) }] };
        var decided = new RequirementCoverage(requirement, []) { Evidence = [new EvidenceItem(evidence, false) { Check = refuted }] };

        Assert.True(pending.HasEvidence);
        Assert.False(decided.HasEvidence);
    }
}
