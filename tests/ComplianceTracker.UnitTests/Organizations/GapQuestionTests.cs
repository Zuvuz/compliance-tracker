using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Organizations;

// US-189: edge cases of the NIS2 gap questions. Arrange / Act / Assert.
public class GapQuestionTests
{
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static GapAnswer Answer(string code, Maturity maturity, DateTime at, string? comment = null)
        => GapAnswer.Create(Acme, code, maturity, comment, Anna, UserRole.Admin, at);

    private static readonly HashSet<string> Nothing = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Every_nis2_measure_of_art_21_2_has_a_question()
    {
        var codes = Nis2Questions.All.Select(q => q.Code).ToHashSet();

        Assert.All("abcdefghij", point => Assert.Contains($"Art. 21(2)({point})", codes));
        Assert.Equal(codes.Count, Nis2Questions.All.Count); // no question twice
    }

    [Fact]
    public void All_open_when_nothing_is_covered_or_answered()
        => Assert.All(GapQuestionRules.Views(Nothing, []), v => Assert.True(v.IsOpen));

    [Fact]
    public void All_covered_leaves_none_open()
    {
        var all = Nis2Questions.All.Select(q => q.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(GapQuestionRules.Views(all, []), v => v.IsOpen);
    }

    [Fact]
    public void Coverage_ignores_the_case_of_the_code()
    {
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "art. 21(2)(c)" };

        Assert.True(GapQuestionRules.Views(covered, []).Single(v => v.Code == "Art. 21(2)(c)").CoveredByEvidence);
    }

    [Theory]
    [InlineData(Maturity.Defined, false)]
    [InlineData(Maturity.Implemented, true)]
    [InlineData(Maturity.Measured, true)]
    public void Three_or_more_without_evidence_needs_evidence(Maturity maturity, bool needs)
    {
        var view = GapQuestionRules.Views(Nothing, [Answer("Art. 21(2)(d)", maturity, Monday)]).Single(v => v.Code == "Art. 21(2)(d)");

        Assert.Equal(needs, view.NeedsEvidence);
    }

    [Fact]
    public void Three_or_more_with_evidence_does_not_need_more()
    {
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Art. 21(2)(d)" };

        Assert.False(GapQuestionRules.Views(covered, [Answer("Art. 21(2)(d)", Maturity.Measured, Monday)]).Single(v => v.Code == "Art. 21(2)(d)").NeedsEvidence);
    }

    [Fact]
    public void A_changed_answer_keeps_the_earlier_one_as_replaced()
    {
        var first = Answer("Art. 21(2)(h)", Maturity.None, Monday);
        var second = Answer("Art. 21(2)(h)", Maturity.Defined, Monday.AddHours(1));
        first.ReplaceBy(second);

        var view = GapQuestionRules.Views(Nothing, [first, second]).Single(v => v.Code == "Art. 21(2)(h)");

        Assert.Equal(Maturity.Defined, view.Answer!.Maturity);
        Assert.Same(first, Assert.Single(view.Earlier));
        Assert.Equal(Monday.AddHours(1), first.ReplacedAt);
    }

    [Fact]
    public void An_answer_to_another_question_cannot_replace()
        => Assert.Throws<DomainException>(() => Answer("Art. 21(2)(h)", Maturity.None, Monday).ReplaceBy(Answer("Art. 21(2)(i)", Maturity.None, Monday)));

    [Fact]
    public void An_answer_to_an_unknown_maturity_is_refused()
        => Assert.Throws<DomainException>(() => Answer("Art. 21(2)(h)", (Maturity)7, Monday));

    [Fact]
    public void A_too_long_comment_is_refused_and_a_blank_one_is_none()
    {
        Assert.Throws<DomainException>(() => Answer("Art. 21(2)(h)", Maturity.None, Monday, new string('x', GapAnswer.MaxCommentLength + 1)));
        Assert.Null(Answer("Art. 21(2)(h)", Maturity.None, Monday, "   ").Comment);
    }

    [Fact]
    public void A_question_is_found_whatever_the_case()
        => Assert.Equal("Art. 21(2)(d)", Nis2Questions.For(" art. 21(2)(D) ")!.Code);
}
