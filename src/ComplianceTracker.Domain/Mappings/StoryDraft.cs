using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Mappings
{
    public enum StoryDraftStatus
    {
        Draft,
        Accepted,
        Discarded
    }

    // US-106 R3–R6: a user story the AI drafted for one requirement that nothing in the project matched. It is marked
    // "AI-generated" and is never a story until a person has read it, may have changed every field, and accepted it (R4).
    // An accepted draft becomes an issue a person creates (B12: the tracker does not write to GitHub); when the issue comes
    // in with the next sync, it is linked to the requirement with the source "AI-drafted" (R5). Discarding needs a reason
    // and keeps the draft as history (R6).
    public class StoryDraft
    {
        public const int MaxTitleLength = 200;
        public const int MaxTextLength = 4000;

        public Guid Id { get; } = Guid.NewGuid();
        public Guid ProjectId { get; private set; }
        public Guid RequirementId { get; private set; }
        public string Title { get; private set; } = "";
        public string Role { get; private set; } = "";
        public string Story { get; private set; } = "";
        public string Rules { get; private set; } = "";
        public string Examples { get; private set; } = "";
        public string Model { get; private set; } = "";
        public string PromptVersion { get; private set; } = "";
        public DateTime CreatedAt { get; private set; }

        public StoryDraftStatus Status { get; private set; } = StoryDraftStatus.Draft;

        // R4: true once a person changed the AI's text
        public bool EditedByPerson { get; private set; }
        public Guid? DecidedById { get; private set; }
        public UserRole? DecidedRole { get; private set; }
        public DateTime? DecidedAt { get; private set; }
        public string? DiscardReason { get; private set; }

        // R5: the story the accepted draft became, once it was synced and linked
        public Guid? WorkItemId { get; private set; }

        public bool IsOpen => Status == StoryDraftStatus.Draft;

        // For EF Core only
        private StoryDraft() { }

        public static StoryDraft Write(
            Guid projectId, Guid requirementId, string? title, string? role, string? story, string? rules, string? examples,
            string model, string promptVersion, DateTime at)
        {
            var draft = new StoryDraft
            {
                ProjectId = projectId,
                RequirementId = requirementId,
                Model = model,
                PromptVersion = promptVersion,
                CreatedAt = at
            };
            draft.Set(title, role, story, rules, examples);
            return draft;
        }

        // R4: a person accepts it, with their changes; it then waits for the issue to come in
        public void Accept(string? title, string? role, string? story, string? rules, string? examples, Guid userId, DateTime at, UserRole? userRole = null)
        {
            EnsureOpen();
            var before = (Title, Role, Story, Rules, Examples);
            Set(title, role, story, rules, examples);
            EditedByPerson = before != (Title, Role, Story, Rules, Examples);
            Status = StoryDraftStatus.Accepted;
            DecidedById = userId;
            DecidedRole = userRole;
            DecidedAt = at;
        }

        // R6
        public void Discard(string? reason, Guid userId, DateTime at, UserRole? userRole = null)
        {
            EnsureOpen();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Give a reason for discarding the draft");
            }

            Status = StoryDraftStatus.Discarded;
            DiscardReason = Cut(reason.Trim(), 1000);
            DecidedById = userId;
            DecidedRole = userRole;
            DecidedAt = at;
        }

        // R5: the issue made from the accepted draft came in with the sync
        public void LinkedTo(Guid workItemId)
        {
            if (Status != StoryDraftStatus.Accepted)
            {
                throw new DomainException("Only an accepted draft becomes a story");
            }

            WorkItemId ??= workItemId;
        }

        // The issue's body in the shape of the user story template (role, story, rules, examples)
        public string IssueBody(string requirementCode)
            => $"""
                ### Role

                {Role}

                ### User Story

                {Story}

                ### Rules

                {Rules}

                ### Examples

                {Examples}

                _Drafted by the AI for {requirementCode} in the compliance tracker, then read and accepted by a person._
                """;

        private void Set(string? title, string? role, string? story, string? rules, string? examples)
        {
            var cleanTitle = (title ?? "").Trim();
            var cleanStory = (story ?? "").Trim();
            if (cleanTitle.Length == 0 || cleanStory.Length == 0)
            {
                throw new DomainException("A draft needs a title and a user story");
            }

            Title = Cut(cleanTitle, MaxTitleLength);
            Role = Cut((role ?? "").Trim(), 200);
            Story = Cut(cleanStory, MaxTextLength);
            Rules = Cut((rules ?? "").Trim(), MaxTextLength);
            Examples = Cut((examples ?? "").Trim(), MaxTextLength);
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new DomainException("The draft has already been decided");
            }
        }

        private static string Cut(string text, int length) => text.Length <= length ? text : text[..length];
    }
}
