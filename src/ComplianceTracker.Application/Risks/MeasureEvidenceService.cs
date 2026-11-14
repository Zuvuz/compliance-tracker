using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Risks
{
    // US-158: the evidence a person adds while approving a measure the AI suggested
    public record MeasureEvidence(string? Title, string? Url, string? FileName, byte[]? Content, DateOnly? ValidUntil)
    {
        // R1: nothing typed and no file means no evidence
        public bool IsGiven => !string.IsNullOrWhiteSpace(Url) || Content is { Length: > 0 };

        // R3: the rule of US-057, checked before anything is saved
        public void Check()
        {
            if (!string.IsNullOrWhiteSpace(Url) && Content is { Length: > 0 })
            {
                throw new DomainException("Give a link or a file, not both");
            }
        }
    }

    // US-158: approve a suggested measure and prove it in one step. The evidence goes on the requirement the measure
    // points at, or on the story when the measure is a story (US-146). Refused evidence approves nothing (R3).
    public class MeasureEvidenceService
    {
        private readonly AskTheAiService _ai;
        private readonly ComplianceService _compliance;
        private readonly EvidenceService _evidence;
        private readonly CurrentMember _member;

        public MeasureEvidenceService(AskTheAiService ai, ComplianceService compliance, EvidenceService evidence, CurrentMember member)
        {
            _ai = ai;
            _compliance = compliance;
            _evidence = evidence;
            _member = member;
        }

        public async Task ApproveAsync(Guid projectId, Guid suggestionId, MeasureEvidence? evidence, CancellationToken cancellationToken = default)
        {
            if (evidence is not { IsGiven: true })
            {
                await _ai.ApproveMeasureAsync(projectId, suggestionId, cancellationToken); // R1: as today
                return;
            }

            // R5: adding evidence needs LinkWork; R3: the suggestion must still wait, and the evidence must be valid,
            // before anything is saved
            await _member.RequireAsync(Permission.LinkWork, cancellationToken);
            evidence.Check();
            var suggestion = await _ai.PendingMeasureAsync(projectId, suggestionId, cancellationToken);
            var title = string.IsNullOrWhiteSpace(evidence.Title) ? evidence.FileName ?? evidence.Url ?? "" : evidence.Title.Trim();

            if (suggestion.RequirementId is { } requirementId)
            {
                // R2: on the requirement, through the evidence rules of US-057
                if (evidence.Content is { Length: > 0 } content)
                {
                    await _compliance.AddEvidenceFileAsync(projectId, requirementId, title, evidence.FileName ?? "", content, evidence.ValidUntil, cancellationToken);
                }
                else
                {
                    await _compliance.AddEvidenceLinkAsync(projectId, requirementId, title, evidence.Url!.Trim(), evidence.ValidUntil, cancellationToken);
                }
            }
            else if (suggestion.WorkItemId is { } storyId)
            {
                // R2: a measure that is a story: the evidence is linked to the story (US-146)
                var added = await _evidence.AddAsync(projectId, title, evidence.Content is { Length: > 0 } ? null : evidence.Url, evidence.FileName,
                    evidence.Content, evidence.ValidUntil, cancellationToken);
                await _evidence.LinkStoryAsync(projectId, added.Evidence.Id, storyId, cancellationToken);
            }

            await _ai.ApproveMeasureAsync(projectId, suggestionId, cancellationToken);
        }
    }
}
