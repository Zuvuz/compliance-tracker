using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.WorkItems;
using System.Text;
using System.Text.RegularExpressions;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-033: reads the requirements in a .docx requirement specification.
    // R2: a requirement starts at a heading or paragraph that begins with an ID matching the pattern; the rest is the title.
    // R3: the text up to the next requirement or heading is the description; after "Acceptance criteria:" it is the criteria.
    // R4: an ID inside the text ("see REQ-004") does not start a requirement, as the pattern only matches at the start.
    public class WordRequirementReader : IRequirementDocumentReader
    {
        // R3: the line that starts the acceptance criteria, in English or Danish, with any text after it
        private static readonly Regex CriteriaLabel = new(@"^(acceptance criteria|acceptkriterier)\s*:\s*(?<rest>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public string Extension => ".docx";

        public DocumentKind Kind => DocumentKind.RequirementSpecification;

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

                if (current.Title.Length == 0)
                {
                    skipped.Add($"{current.Id} skipped: no title");
                }
                else
                {
                    items.Add(new ReadRequirement(
                        new DocumentWorkItem(current.Id, current.Title, current.Description.ToString().Trim(), current.Criteria.ToString().Trim()),
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

                if (idPattern.MatchStart(paragraph.Text) is { } start)
                {
                    Finish();
                    current = new Draft(start.Id, start.Title, paragraph.Position);
                }
                else if (paragraph.IsHeading)
                {
                    Finish(); // another heading ends the requirement, e.g. "3. Non-functional requirements"
                }
                else if (current is not null && CriteriaLabel.Match(paragraph.Text) is { Success: true } label)
                {
                    current.InCriteria = true;
                    AppendLine(current.Criteria, label.Groups["rest"].Value);
                }
                else if (current is not null)
                {
                    AppendLine(current.InCriteria ? current.Criteria : current.Description, paragraph.Text);
                }
            }

            Finish();

            // R6: nothing that looks like a requirement; most likely the IDs follow another pattern
            if (items.Count == 0 && skipped.Count == 0)
            {
                throw new DomainException("No requirements found. Check the ID pattern");
            }

            return new RequirementReadResult(items, skipped, "paragraph");
        }

        private static void AppendLine(StringBuilder text, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(line.Trim());
        }

        // A requirement while its paragraphs are read
        private sealed class Draft(string id, string title, int position)
        {
            public string Id { get; } = id;
            public string Title { get; } = title;
            public int Position { get; } = position;
            public StringBuilder Description { get; } = new();
            public StringBuilder Criteria { get; } = new();
            public bool InCriteria { get; set; }
        }
    }
}
