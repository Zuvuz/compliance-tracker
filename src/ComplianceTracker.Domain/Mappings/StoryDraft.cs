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

    // US-108 R2: the form a draft takes, after the project's method (US-104)
    public enum DraftForm
    {
        UserStory,      // GitHub user stories: role, "As a … I want … so that …", rules, examples
        UseCase,        // Unified Process: name, primary actor, preconditions, main success scenario, extensions, postconditions
        Requirement     // a requirement specification: title, description, acceptance criteria
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

        // US-108: the form, the other requirements it covers (R4: at most MaxCovered in all), how it fits the intended purpose
        // (R3), a use case's postconditions, and whether it was drafted for the gaps of a regulation
        public const int MaxCovered = 3;
        public DraftForm Form { get; private set; } = DraftForm.UserStory;
        public Guid[] AlsoCovers { get; private set; } = [];
        public string? Fits { get; private set; }
        public string Postconditions { get; private set; } = "";
        public bool FromGaps { get; private set; }

        public IReadOnlyList<Guid> RequirementIds => [RequirementId, .. AlsoCovers];

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

        // US-108 R2–R4: a draft for the gaps, in the project's form, covering up to MaxCovered requirements
        public static StoryDraft ForGaps(
            Guid projectId, DraftForm form, IReadOnlyList<Guid> covers, string? title, string? role, string? story, string? rules, string? examples,
            string? postconditions, string? fits, string model, string promptVersion, DateTime at)
        {
            if (!Enum.IsDefined(form))
            {
                throw new DomainException("Choose the form of the draft");
            }

            var distinct = covers.Where(id => id != Guid.Empty).Distinct().ToList();
            if (distinct.Count == 0)
            {
                throw new DomainException("A draft covers at least one requirement");
            }

            if (distinct.Count > MaxCovered)
            {
                throw new DomainException($"A draft covers at most {MaxCovered} requirements");
            }

            var draft = new StoryDraft
            {
                ProjectId = projectId,
                RequirementId = distinct[0],
                Form = form,
                Model = model,
                PromptVersion = promptVersion,
                CreatedAt = at
            };
            draft.Set(title, role, story, rules, examples);
            draft.AlsoCovers = distinct.Skip(1).ToArray();
            draft.Postconditions = Cut((postconditions ?? "").Trim(), MaxTextLength);
            draft.Fits = string.IsNullOrWhiteSpace(fits) ? null : Cut(fits.Trim(), 300);
            draft.FromGaps = true;
            return draft;
        }

        // R4: a person accepts it, with their changes; it then waits for the issue to come in
        public void Accept(string? title, string? role, string? story, string? rules, string? examples, Guid userId, DateTime at, UserRole? userRole = null,
            string? postconditions = null)
        {
            EnsureOpen();
            var before = (Title, Role, Story, Rules, Examples, Postconditions);
            Set(title, role, story, rules, examples);
            if (postconditions is not null)
            {
                Postconditions = Cut(postconditions.Trim(), MaxTextLength);
            }

            EditedByPerson = before != (Title, Role, Story, Rules, Examples, Postconditions);
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

        // US-108 R7: a use case or a requirement, as text for the team's own document; the tracker does not write the document
        public string CopyText(IEnumerable<string> codes)
            => Form switch
            {
                DraftForm.UseCase => $"""
                    {Title}

                    Primary actor: {Role}

                    Preconditions:
                    {Rules}

                    Main success scenario:
                    {Story}

                    Extensions:
                    {Examples}

                    Postconditions:
                    {Postconditions}

                    Covers: {string.Join(", ", codes)}
                    """,
                DraftForm.Requirement => $"""
                    {Title}

                    {Story}

                    Acceptance criteria:
                    {Rules}

                    Covers: {string.Join(", ", codes)}
                    """,
                _ => IssueBody(string.Join(", ", codes))
            };

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
                throw new DomainException(Form switch
                {
                    DraftForm.UseCase => "A draft needs a name and a main success scenario",
                    DraftForm.Requirement => "A draft needs a title and a description",
                    _ => "A draft needs a title and a user story"
                });
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
