using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.WorkItems;
using System.Text;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-034: reads Unified Process use cases from a .docx file, built on the Word reading from US-033.
    // R2: a use case starts at a heading with an ID matching the pattern (default "UC-" and a number); the rest is the name.
    // R3: the sections are found by their labels, in English or Danish. Numbered lines in the main success scenario
    // become steps; so do the items of a numbered list in Word.
    // R4: a use case without a main success scenario is skipped. R5: its steps and postconditions are its acceptance criteria.
    public class UseCaseReader : IRequirementDocumentReader
    {
        private enum Section { Description, PrimaryActor, Preconditions, MainScenario, Extensions, Postconditions }

        // R3: each label with its section, e.g. "Primary actor: Nurse" or "Hovedforløb:"
        private static readonly (Regex Label, Section Section)[] Labels =
        [
            (Label(@"primary actor|primær aktør"), Section.PrimaryActor),
            (Label(@"preconditions?|forudsætninger"), Section.Preconditions),
            (Label(@"main success scenario|hovedforløb"), Section.MainScenario),
            (Label(@"extensions|udvidelser"), Section.Extensions),
            (Label(@"postconditions?|resultat"), Section.Postconditions)
        ];

        // "1. The nurse opens the form", "2) ..." → "The nurse opens the form"
        private static readonly Regex NumberedStep = new(@"^\d+[.)]\s+(?<step>.+)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public string Extension => ".docx";

        public DocumentKind Kind => DocumentKind.UseCases;

        public bool UsesIdPattern => true;

        public RequirementReadResult Read(byte[] content, IdPattern idPattern)
        {
            var items = new List<ReadRequirement>();
            var skipped = new List<string>();
            Draft? current = null;

            void Finish()
            {
                if (current is null)
                {
                    return;
                }

                if (current.Name.Length == 0)
                {
                    skipped.Add($"{current.Id} skipped: no name");
                }
                else if (current.Steps.Count == 0)
                {
                    skipped.Add($"{current.Id} skipped: no main success scenario");
                }
                else
                {
                    var sections = new UseCaseSections(
                        current.Text(Section.PrimaryActor), current.Text(Section.Preconditions), current.Steps,
                        current.Text(Section.Extensions), current.Text(Section.Postconditions));
                    items.Add(new ReadRequirement(
                        new DocumentWorkItem(current.Id, current.Name, current.Text(Section.Description), sections.AcceptanceCriteria, sections),
                        current.Position));
                }

                current = null;
            }

            foreach (var paragraph in WordParagraphs.Read(content))
            {
                if (paragraph.Text.Length == 0)
                {
                    continue;
                }

                if (paragraph.IsHeading)
                {
                    Finish();
                    if (idPattern.MatchStart(paragraph.Text) is { } start)
                    {
                        current = new Draft(start.Id, start.Title, paragraph.Position);
                    }

                    continue;
                }

                if (current is null)
                {
                    continue;
                }

                if (Labels.Select(l => (l.Section, Match: l.Label.Match(paragraph.Text))).FirstOrDefault(l => l.Match.Success) is { Match: not null } label)
                {
                    current.Section = label.Section;
                    current.Add(label.Match.Groups["rest"].Value, paragraph.IsListItem);
                }
                else
                {
                    current.Add(paragraph.Text, paragraph.IsListItem);
                }
            }

            Finish();

            if (items.Count == 0 && skipped.Count == 0)
            {
                throw new DomainException("No use cases found. Check the ID pattern");
            }

            return new RequirementReadResult(items, skipped, "paragraph");
        }

        private static Regex Label(string names)
            => new($@"^({names})\s*:\s*(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        // A use case while its paragraphs are read
        private sealed class Draft(string id, string name, int position)
        {
            private readonly Dictionary<Section, StringBuilder> _text = new();

            public string Id { get; } = id;
            public string Name { get; } = name;
            public int Position { get; } = position;
            public Section Section { get; set; } = Section.Description;
            public List<string> Steps { get; } = new();

            // R3: in the main success scenario every numbered line or list item is a step; elsewhere text is kept as lines
            public void Add(string line, bool isListItem)
            {
                line = line.Trim();
                if (line.Length == 0)
                {
                    return;
                }

                if (Section == Section.MainScenario)
                {
                    var numbered = NumberedStep.Match(line);
                    if (numbered.Success)
                    {
                        Steps.Add(numbered.Groups["step"].Value);
                    }
                    else if (isListItem || Steps.Count == 0)
                    {
                        Steps.Add(line);
                    }
                    else
                    {
                        Steps[^1] += " " + line; // a line that continues the step above
                    }

                    return;
                }

                if (!_text.TryGetValue(Section, out var text))
                {
                    _text[Section] = text = new StringBuilder();
                }

                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(line);
            }

            public string Text(Section section) => _text.TryGetValue(section, out var text) ? text.ToString() : "";
        }
    }
}
