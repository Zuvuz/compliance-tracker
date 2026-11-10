using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace ComplianceTracker.Specs.Support
{
    // US-143 R9: made-up files that copy the layout of ISO standards. They hold no ISO text: every title and sentence is
    // invented, only the numbering follows the standards (codes and counts are facts, not protected text).
    public static class StandardFiles
    {
        public const string Watermark = "Licensed to Acme ApS – single user licence only";

        // ISO/IEC 27001:2022's numbering of Annex A: 37 + 8 + 14 + 34 = 93 controls
        public static IEnumerable<string> AnnexACodes()
            => new[] { (5, 37), (6, 8), (7, 14), (8, 34) }.SelectMany(t => Enumerable.Range(1, t.Item2).Select(n => $"A.{t.Item1}.{n}"));

        // A management system standard: foreword, introduction, clauses 1–3 (left out), clauses 4–10 with sub-clauses and
        // lettered points, NOTEs, Annex A with the 93 controls, an informative annex and a bibliography, a watermark on
        // every page
        public static byte[] ManagementSystemPdf(string? without = null)
        {
            var lines = new List<string>
            {
                "Contents",
                "4 Context of the organization ........ 1",
                "Foreword",
                "This made-up document copies only the layout of a standard. It is written for tests.",
                "Introduction",
                "0.1 General",
                "This part explains the purpose and shall not become a requirement.",
                "1 Scope",
                "This document specifies made-up rules for tests.",
                "2 Normative references",
                "There are no normative references in this document.",
                "3 Terms and definitions",
                "3.1 Test term",
                "a word that only exists for testing",
                "4 Context of the organization",
                "4.1 Understanding the context",
                "The organization shall work out the matters that affect its purpose.",
                "4.2 Interested parties",
                "The organization shall list the parties that care about its security.",
                "6 Planning",
                "6.1 Actions on risks",
                "6.1.2 Risk assessment",
                "The organization shall define and apply a risk assessment process that:",
                "a) sets the criteria for accepting risks;",
                "b) gives results that are consistent and can be compared;",
                "c) finds the risks to confidentiality, integrity and availability;",
                "NOTE The criteria can be set per business unit.",
                "d) analyses the risks found;",
                "e) evaluates the risks against the criteria.",
                "The organization shall keep documented information about the process.",
                "10 Improvement",
                "10.1 Continual improvement",
                "The organization shall keep improving the management system.",
                "Annex A (normative)",
                "Controls reference",
                "The controls in this annex shall be compared with the controls the organization chose."
            };

            foreach (var code in AnnexACodes().Where(c => c != without))
            {
                lines.Add($"{code} Made-up control {code}");
                lines.Add($"A rule for {code} shall be set and followed.");
            }

            lines.AddRange(
            [
                "Annex B (informative)",
                "B.1 Guidance",
                "The organization shall read this guidance, but it is informative.",
                "Bibliography",
                "[1] A made-up reference"
            ]);

            return TestFiles.PdfWithPages(Pages(lines, 34).Select(p => p.Prepend(Watermark).ToArray()).ToArray());
        }

        // A code of practice where the controls say "should" (ISO/IEC 27002's way), plus one "shall"
        public static byte[] CodeOfPracticePdf()
            => TestFiles.PdfWithText(
                "5 Organizational controls",
                "5.1 Policies for information security",
                "A policy for information security should be defined and approved by management.",
                "5.2 Roles and responsibilities",
                "Roles shall be defined and given to people.");

        // A quality model in Word (ISO/IEC 25010's way): characteristics with subcharacteristics, no "shall"
        public static byte[] QualityModelWord()
        {
            string[] lines =
            [
                "This made-up document describes a product quality model for tests.",
                "4 Product quality model",
                "4.1 Maintainability",
                "How easily the made-up product can be changed by the people who keep it.",
                "4.1.1 Modularity",
                "How well the parts can be changed on their own.",
                "4.1.2 Reusability",
                "How well a part can be used again.",
                "4.1.3 Analysability",
                "How easily a change or fault can be understood.",
                "4.1.4 Modifiability",
                "How easily the product can be changed without faults.",
                "4.1.5 Testability",
                "How easily tests can be set up and run.",
                "4.2 Portability",
                "How easily the made-up product moves to another place."
            ];

            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Word.Document(new Word.Body(lines.Select(l => new Word.Paragraph(new Word.Run(new Word.Text(l))))));
                main.Document.Save();
            }

            return stream.ToArray();
        }

        // A control list (Statement of Applicability) in Excel, with an empty row in the middle
        public static byte[] ControlListExcel()
        {
            var rows = new List<string[]> { new[] { "Control", "Name", "Description" } };
            var codes = AnnexACodes().ToList();
            for (var i = 0; i < codes.Count; i++)
            {
                if (i == 40)
                {
                    rows.Add(["", "", ""]);
                }

                rows.Add([codes[i], $"Made-up control {codes[i]}", $"A rule for {codes[i]} shall be set."]);
            }

            using var stream = new MemoryStream();
            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = document.AddWorkbookPart();
                workbook.Workbook = new Workbook();
                var sheetPart = workbook.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                uint number = 1;
                foreach (var row in rows)
                {
                    var r = new Row { RowIndex = number };
                    for (var c = 0; c < row.Length; c++)
                    {
                        r.Append(new Cell
                        {
                            CellReference = $"{(char)('A' + c)}{number}",
                            DataType = CellValues.InlineString,
                            InlineString = new InlineString(new Text(row[c]))
                        });
                    }

                    data.Append(r);
                    number++;
                }

                sheetPart.Worksheet = new Worksheet(data);
                workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheetPart), SheetId = 1, Name = "SoA" }));
                workbook.Workbook.Save();
            }

            return stream.ToArray();
        }

        // A scanned PDF: pages, but no text
        public static byte[] ScannedPdf() => TestFiles.PdfWithPages([], []);

        private static IEnumerable<string[]> Pages(List<string> lines, int perPage)
            => lines.Chunk(perPage);
    }
}
