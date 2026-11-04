using ComplianceTracker.Application.WorkItems;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.WorkItems;

namespace ComplianceTracker.Infrastructure.WorkItems.Documents
{
    // US-032 R2, R3: turns the rows of a spreadsheet (Excel or CSV) into requirements.
    // The first row holds the column names; English and Danish names are accepted, without regard to case.
    public static class RequirementTable
    {
        private static readonly string[] IdNames = ["id", "krav-id"];
        private static readonly string[] TitleNames = ["title", "titel"];
        private static readonly string[] DescriptionNames = ["description", "beskrivelse"];
        private static readonly string[] CriteriaNames = ["acceptance criteria", "acceptkriterier"];

        // rows: each row's number in the file (1 = the header) and its cells from the first column
        public static RequirementReadResult Read(IEnumerable<(int Number, IReadOnlyList<string> Cells)> rows)
        {
            using var enumerator = rows.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new DomainException("The file has no rows");
            }

            var header = enumerator.Current.Cells.Select(c => c.Trim().ToLowerInvariant()).ToList();
            var id = Column(header, IdNames) ?? throw new DomainException("The column ID is missing");
            var title = Column(header, TitleNames) ?? throw new DomainException("The column Title is missing");
            var description = Column(header, DescriptionNames);
            var criteria = Column(header, CriteriaNames);

            var items = new List<ReadRequirement>();
            var skipped = new List<string>();
            while (enumerator.MoveNext())
            {
                var (number, cells) = enumerator.Current;
                if (cells.All(string.IsNullOrWhiteSpace))
                {
                    continue; // an empty row is not a requirement and not worth a message
                }

                var reference = Cell(cells, id);
                var name = Cell(cells, title);
                if (reference.Length == 0)
                {
                    skipped.Add($"Row {number} skipped: no ID");
                }
                else if (name.Length == 0)
                {
                    skipped.Add($"Row {number} skipped: no title");
                }
                else
                {
                    items.Add(new ReadRequirement(
                        new DocumentWorkItem(reference, name, Cell(cells, description), Cell(cells, criteria)), number));
                }
            }

            return new RequirementReadResult(items, skipped);
        }

        private static int? Column(List<string> header, string[] names)
        {
            var index = header.FindIndex(names.Contains);
            return index < 0 ? null : index;
        }

        private static string Cell(IReadOnlyList<string> cells, int? column)
            => column is { } index && index < cells.Count ? cells[index].Trim() : "";
    }
}
