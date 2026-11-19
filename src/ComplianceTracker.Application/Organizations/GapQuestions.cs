using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-189: storage for the answers (implemented in Infrastructure)
    public interface IGapAnswerRepository
    {
        // Every answer of the organisation, replaced ones too, newest first
        Task<IReadOnlyList<GapAnswer>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);

        Task AddAsync(GapAnswer answer, CancellationToken cancellationToken = default);
    }

    // One question as the page shows it: whether evidence already covers it (R2), the current answer and the earlier ones
    // (R5), and whether the answer claims more than the evidence shows (R4)
    public sealed record GapQuestionView(string Code, string Question, bool CoveredByEvidence, GapAnswer? Answer, IReadOnlyList<GapAnswer> Earlier)
    {
        // R4: "implemented" or better, but nothing approved proves it
        public bool NeedsEvidence => Answer is { Maturity: >= Maturity.Implemented } && !CoveredByEvidence;

        public bool IsOpen => !CoveredByEvidence && Answer is null;
    }

    // US-189: the questions for what the documents do not show. Pure rules apart from the service, so they are tested alone.
    public static class GapQuestionRules
    {
        // R2, R5: one view per built-in question; "covered" holds the requirement codes approved evidence covers
        public static IReadOnlyList<GapQuestionView> Views(IReadOnlySet<string> covered, IReadOnlyList<GapAnswer> answers)
            => Nis2Questions.All.Select(q =>
            {
                var mine = answers.Where(a => string.Equals(a.RequirementCode, q.Code, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.AnsweredAt).ToList();
                return new GapQuestionView(q.Code, q.Text, covered.Contains(q.Code), mine.FirstOrDefault(a => a.IsCurrent), mine.Where(a => !a.IsCurrent).ToList());
            }).ToList();
    }

    public class GapQuestionService
    {
        private readonly IGapAnswerRepository _answers;
        private readonly OrganisationAreaService _area;
        private readonly ComplianceQueries _compliance;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public GapQuestionService(IGapAnswerRepository answers, OrganisationAreaService area, ComplianceQueries compliance, CurrentMember member,
            IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _answers = answers;
            _area = area;
            _compliance = compliance;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1, R2, R4, R5: every NIS2 question for the signed-in member's organisation
        public async Task<IReadOnlyList<GapQuestionView>> QuestionsAsync(CancellationToken cancellationToken = default)
        {
            var (organization, _) = await _member.RequireAsync(cancellationToken);
            return GapQuestionRules.Views(await CoveredAsync(cancellationToken), await _answers.ListAsync(organization.Id, cancellationToken));
        }

        // The NIS2 requirement codes that approved evidence or links cover in the organisation area. US-204: the gap analysis
        // asks whether a measure is in place, so documentation counts here, though the Compliance tab still asks for proof.
        public async Task<IReadOnlySet<string>> CoveredAsync(CancellationToken cancellationToken = default)
        {
            var area = await _area.EnsureAsync(cancellationToken);
            var compliance = await _compliance.ForProjectAsync(area.Id, cancellationToken);
            return (compliance?.Frameworks ?? [])
                .Where(f => f.Framework.ShortName == "NIS2")
                .SelectMany(f => f.Requirements)
                .Where(r => r.IsCovered || r.HasEvidence)
                .Select(r => r.Requirement.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // R3, R5: an answer is an assessment (DecideCompliance); the earlier answer is kept as replaced
        public async Task AnswerAsync(string code, Maturity? maturity, string? comment, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var question = Nis2Questions.For(code) ?? throw new DomainException("Choose a question");
            if (maturity is null)
            {
                throw new DomainException("Choose a maturity from 0 to 4");
            }

            var answer = GapAnswer.Create(organization.Id, question.Code, maturity.Value, comment, user.Id, user.Role, _clock.GetUtcNow().UtcDateTime);
            foreach (var earlier in (await _answers.ListAsync(organization.Id, cancellationToken)).Where(a => a.IsCurrent && a.RequirementCode == question.Code))
            {
                earlier.ReplaceBy(answer);
            }

            await _answers.AddAsync(answer, cancellationToken);
            await _log.LogAsync(ActivityAction.GapQuestionAnswered, organization.Id,
                $"{question.Code}: {Nis2Questions.Title(answer.Maturity)}", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
