namespace ComplianceTracker.Domain.WorkItems
{
    // US-034 R3: the sections of a Unified Process use case, as the use case document has them.
    // Part of a WorkItem of type UseCase; stored with it (as JSON). Never changed in place: a new version replaces it.
    public class UseCaseSections
    {
        public string PrimaryActor { get; private set; } = "";
        public string Preconditions { get; private set; } = "";

        // The main success scenario, one step per entry, without its number
        public List<string> Steps { get; private set; } = new();

        // The extensions as written, e.g. "2a. The CPR number is wrong: the system shows an error"
        public string Extensions { get; private set; } = "";
        public string Postconditions { get; private set; } = "";

        // For EF Core only
        private UseCaseSections() { }

        public UseCaseSections(string primaryActor, string preconditions, IEnumerable<string> steps, string extensions, string postconditions)
        {
            PrimaryActor = primaryActor.Trim();
            Preconditions = preconditions.Trim();
            Steps = steps.Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            Extensions = extensions.Trim();
            Postconditions = postconditions.Trim();
        }

        // R5: the steps and the postconditions are what the use case must achieve, so they are its acceptance criteria
        public string AcceptanceCriteria
            => string.Join("\n", Steps.Select((step, i) => $"{i + 1}. {step}")
                .Concat(Postconditions.Length > 0 ? [Postconditions] : Array.Empty<string>()));

        public bool SameAs(UseCaseSections? other)
            => other is not null && PrimaryActor == other.PrimaryActor && Preconditions == other.Preconditions
               && Steps.SequenceEqual(other.Steps) && Extensions == other.Extensions && Postconditions == other.Postconditions;
    }

    // US-034: what an uploaded document holds; chosen when it is uploaded and kept for its later versions
    public enum DocumentKind
    {
        // US-032, US-033: requirements, each with an ID and a title
        RequirementSpecification,

        // US-034: Unified Process use cases
        UseCases
    }
}
