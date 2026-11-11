using System.Globalization;
using ComplianceTracker.Application.Reports;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ComplianceTracker.Infrastructure.Reports
{
    // US-131: the reports as PDF with PdfPig's writer (already used to read framework PDFs; Apache-2.0, free for
    // commercial use). Pages are laid out first and drawn afterwards, so every page can say "page x of y" (R3).
    // Wide tables are printed in landscape; a table that runs over a page repeats its header (R4).
    public class PdfPigReportWriter : IPdfWriter
    {
        private const double Margin = 40;
        private const double HeaderHeight = 38;
        private const double FooterHeight = 24;
        private const double BodySize = 9;
        private const double LineGap = 2.5;
        private const double CellPadding = 3;

        public byte[] FromReport(ReportDocument document, PdfHeader header)
        {
            var landscape = document.Sheets.Any(s => s.Kind == ReportSheetKind.Table && s.Columns.Count > 6);
            var layout = new Layout(landscape);

            layout.Heading(document.Title, 16);
            foreach (var (label, value) in document.Summary)
            {
                layout.Paragraph($"{label}: {value}", BodySize, bold: false);
            }

            foreach (var sheet in document.Sheets)
            {
                layout.Space(10);
                layout.Heading(sheet.Name, 12);
                switch (sheet.Kind)
                {
                    // US-141: short points as a list, key figures as tiles
                    case ReportSheetKind.Points:
                        layout.Space(2);
                        foreach (var row in sheet.Rows)
                        {
                            layout.Paragraph("• " + (row.Cells.Count > 0 ? row.Cells[0] : ""), 10, bold: false, indent: 6);
                        }

                        break;
                    case ReportSheetKind.Tiles:
                        layout.Tiles(sheet);
                        break;
                    default:
                        layout.Table(sheet);
                        break;
                }
            }

            return layout.Render(header);
        }

        public byte[] FromDocument(GeneratedDocument document, PdfHeader header)
        {
            var layout = new Layout(landscape: false);
            foreach (var block in document.Blocks)
            {
                switch (block.Kind)
                {
                    case DocumentBlockKind.Title:
                        layout.Heading(block.Text, 18);
                        break;
                    case DocumentBlockKind.Heading1:
                        layout.Space(8);
                        layout.Heading(block.Text, 13);
                        break;
                    case DocumentBlockKind.Heading2:
                        layout.Space(4);
                        layout.Heading(block.Text, 11);
                        break;
                    case DocumentBlockKind.Bullet:
                        layout.Paragraph("• " + block.Text, BodySize, bold: false, indent: 10);
                        break;
                    case DocumentBlockKind.Paragraph:
                        layout.Paragraph(block.Text, BodySize, bold: false);
                        break;
                    default:
                        // R5 of US-056: a part to complete by hand stands out
                        layout.Paragraph(block.Text, BodySize, bold: true, colour: (150, 90, 0));
                        break;
                }
            }

            return layout.Render(header);
        }

        // ---------------------------------------------------------- Layout

        private sealed record Op(Action<PdfPageBuilder, Fonts> Draw);

        private sealed record Fonts(PdfDocumentBuilder.AddedFont Regular, PdfDocumentBuilder.AddedFont Bold);

        // Measures with the font metrics of Helvetica (the standard font), so layout needs no page
        private static class Metrics
        {
            private static readonly PdfDocumentBuilder Builder = new();
            private static readonly PdfDocumentBuilder.AddedFont RegularFont = Builder.AddStandard14Font(Standard14Font.Helvetica);
            private static readonly PdfDocumentBuilder.AddedFont BoldFont = Builder.AddStandard14Font(Standard14Font.HelveticaBold);
            private static readonly PdfPageBuilder Page = Builder.AddPage(PageSize.A4);
            private static readonly Dictionary<(char, bool), double> Widths = [];
            private static readonly object Gate = new();

            public static double Width(string text, double size, bool bold)
            {
                double total = 0;
                lock (Gate)
                {
                    foreach (var c in text)
                    {
                        if (!Widths.TryGetValue((c, bold), out var width))
                        {
                            var letters = Page.MeasureText(c.ToString(), 1000, new PdfPoint(0, 0), bold ? BoldFont : RegularFont);
                            width = letters.Count == 0 ? 500 : (letters[^1].EndBaseLine.X - letters[0].StartBaseLine.X) / 1000;
                            Widths[(c, bold)] = width;
                        }

                        total += width;
                    }
                }

                return total * size;
            }
        }

        private sealed class Layout
        {
            private readonly bool _landscape;
            private readonly List<List<Op>> _pages = [];
            private double _y;

            public Layout(bool landscape)
            {
                _landscape = landscape;
                NewPage();
            }

            private double PageWidth => _landscape ? 841.89 : 595.28;
            private double PageHeight => _landscape ? 595.28 : 841.89;
            private double ContentWidth => PageWidth - 2 * Margin;
            private double Top => PageHeight - Margin - HeaderHeight;
            private double Bottom => Margin + FooterHeight;
            private List<Op> Current => _pages[^1];

            private void NewPage()
            {
                _pages.Add([]);
                _y = Top;
            }

            private void Ensure(double height)
            {
                if (_y - height < Bottom && _y < Top)
                {
                    NewPage();
                }
            }

            public void Space(double height) => _y -= height;

            public void Heading(string text, double size) => Paragraph(text, size, bold: true);

            public void Paragraph(string text, double size, bool bold, double indent = 0, (byte R, byte G, byte B)? colour = null)
            {
                foreach (var line in PdfText.Wrap(text, ContentWidth - indent, t => Metrics.Width(t, size, bold)))
                {
                    var height = size + LineGap;
                    Ensure(height);
                    _y -= size;
                    var (x, y) = (Margin + indent, _y);
                    var shown = line;
                    Current.Add(new Op((page, fonts) =>
                    {
                        if (colour is { } c)
                        {
                            page.SetTextAndFillColor(c.R, c.G, c.B);
                        }

                        page.AddText(shown, size, new PdfPoint(x, y), bold ? fonts.Bold : fonts.Regular);
                        page.ResetColor();
                    }));
                    _y -= LineGap;
                }
            }

            // R4: column widths from the content, rows wrapped, the header repeated on each new page
            public void Table(ReportSheet sheet)
            {
                if (sheet.Columns.Count == 0)
                {
                    return;
                }

                var widths = ColumnWidths(sheet);
                var header = Cells(sheet.Columns, widths, bold: true);
                DrawRow(header, widths, bold: true, status: null);

                foreach (var row in sheet.Rows)
                {
                    var cells = Cells(row.Cells, widths, bold: false);
                    var height = RowHeight(cells);
                    if (_y - height < Bottom)
                    {
                        NewPage();
                        DrawRow(header, widths, bold: true, status: null);
                    }

                    DrawRow(cells, widths, bold: false, status: sheet.StatusColumn >= 0 ? (sheet.StatusColumn, row.Status) : null);
                }

                if (sheet.Rows.Count == 0)
                {
                    Paragraph("No rows", BodySize, bold: false);
                }
            }

            // US-141: key figures as tiles, four to a row: the value large (in its status colour) and the label under it
            public void Tiles(ReportSheet sheet)
            {
                const int perRow = 4;
                const double gap = 8;
                const double height = 42;
                var width = (ContentWidth - (perRow - 1) * gap) / perRow;
                for (var start = 0; start < sheet.Rows.Count; start += perRow)
                {
                    Space(4);
                    Ensure(height);
                    var top = _y;
                    foreach (var (row, i) in sheet.Rows.Skip(start).Take(perRow).Select((r, i) => (r, i)))
                    {
                        var left = Margin + i * (width + gap);
                        var label = Fit(PdfText.Clean(row.Cells.Count > 0 ? row.Cells[0] : ""), 8, false, width - 2 * CellPadding - 4);
                        var value = Fit(PdfText.Clean(row.Cells.Count > 1 ? row.Cells[1] : ""), 15, true, width - 2 * CellPadding - 4);
                        var colour = Colour(row.Status);
                        Current.Add(new Op((page, fonts) =>
                        {
                            page.SetTextAndFillColor(245, 247, 250);
                            page.SetStrokeColor(200, 205, 212);
                            page.DrawRectangle(new PdfPoint(left, top - height), width, height, 0.5, fill: true);
                            page.ResetColor();
                            if (colour is { } c)
                            {
                                page.SetTextAndFillColor(c.R, c.G, c.B);
                            }

                            page.AddText(value, 15, new PdfPoint(left + 6, top - 20), fonts.Bold);
                            page.ResetColor();
                            page.SetTextAndFillColor(90, 90, 90);
                            page.AddText(label, 8, new PdfPoint(left + 6, top - 34), fonts.Regular);
                            page.ResetColor();
                        }));
                    }

                    _y = top - height;
                }
            }

            private double[] ColumnWidths(ReportSheet sheet)
            {
                // The longest text in each column (capped), shared out over the page width
                var wanted = sheet.Columns.Select((name, i) =>
                {
                    var longest = sheet.Rows.Select(r => i < r.Cells.Count ? r.Cells[i] : "").Append(name)
                        .Max(t => Math.Min(Metrics.Width(PdfText.Clean(t), BodySize, false), 220));
                    return Math.Max(longest + 2 * CellPadding, 40);
                }).ToArray();
                var total = wanted.Sum();
                return wanted.Select(w => w / total * ContentWidth).ToArray();
            }

            private static IReadOnlyList<IReadOnlyList<string>> Cells(IReadOnlyList<string> texts, double[] widths, bool bold)
                => widths.Select((w, i) => PdfText.Wrap(i < texts.Count ? texts[i] : "", w - 2 * CellPadding, t => Metrics.Width(t, BodySize, bold))).ToList();

            private static double RowHeight(IReadOnlyList<IReadOnlyList<string>> cells)
                => cells.Max(c => c.Count) * (BodySize + LineGap) + 2 * CellPadding;

            private void DrawRow(IReadOnlyList<IReadOnlyList<string>> cells, double[] widths, bool bold, (int Column, ReportStatus Status)? status)
            {
                var height = RowHeight(cells);
                if (_y - height < Bottom)
                {
                    // A single row taller than a page is cut off rather than looping forever
                    NewPage();
                }

                var top = _y;
                var x = Margin;
                for (var i = 0; i < widths.Length; i++)
                {
                    var (left, width, lines) = (x, widths[i], cells[i]);
                    var colour = status is { } s && s.Column == i ? Colour(s.Status) : null;
                    Current.Add(new Op((page, fonts) =>
                    {
                        if (bold)
                        {
                            page.SetTextAndFillColor(235, 238, 242);
                            page.DrawRectangle(new PdfPoint(left, top - height), width, height, 0.5, fill: true);
                            page.ResetColor();
                        }

                        page.SetStrokeColor(190, 195, 200);
                        page.DrawRectangle(new PdfPoint(left, top - height), width, height, 0.5, fill: false);
                        page.ResetColor();
                        if (colour is { } c)
                        {
                            page.SetTextAndFillColor(c.R, c.G, c.B);
                        }

                        var y = top - CellPadding - BodySize;
                        foreach (var line in lines)
                        {
                            if (line.Length > 0)
                            {
                                page.AddText(line, BodySize, new PdfPoint(left + CellPadding, y), bold ? fonts.Bold : fonts.Regular);
                            }

                            y -= BodySize + LineGap;
                        }

                        page.ResetColor();
                    }));
                    x += width;
                }

                _y -= height;
            }

            private static (byte R, byte G, byte B)? Colour(ReportStatus status) => status switch
            {
                ReportStatus.Good => (25, 120, 60),
                ReportStatus.Bad => (180, 30, 40),
                ReportStatus.Warning => (160, 100, 0),
                ReportStatus.Muted => (120, 120, 120),
                _ => null
            };

            // R3: organisation, project, report, when and by whom at the top; "page x of y" at the bottom
            public byte[] Render(PdfHeader header)
            {
                var builder = new PdfDocumentBuilder();
                var fonts = new Fonts(builder.AddStandard14Font(Standard14Font.Helvetica), builder.AddStandard14Font(Standard14Font.HelveticaBold));
                var size = PageSize.A4;
                var made = $"Made {header.MadeAtUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)} by {header.MadeBy}";
                var top = PdfText.Clean($"{header.Organisation} · {header.Project} · {header.ReportName}");

                for (var i = 0; i < _pages.Count; i++)
                {
                    var page = builder.AddPage(size, isPortrait: !_landscape);
                    page.SetTextAndFillColor(90, 90, 90);
                    page.AddText(Fit(top, 8, false), 8, new PdfPoint(Margin, PageHeight - Margin - 8), fonts.Regular);
                    page.AddText(PdfText.Clean(made), 8, new PdfPoint(Margin, PageHeight - Margin - 20), fonts.Regular);
                    var number = $"Page {i + 1} of {_pages.Count}";
                    page.AddText(number, 8, new PdfPoint(PageWidth - Margin - Metrics.Width(number, 8, false), Margin), fonts.Regular);
                    page.ResetColor();
                    page.SetStrokeColor(200, 200, 200);
                    page.DrawLine(new PdfPoint(Margin, PageHeight - Margin - 28), new PdfPoint(PageWidth - Margin, PageHeight - Margin - 28), 0.5);
                    page.ResetColor();

                    foreach (var op in _pages[i])
                    {
                        op.Draw(page, fonts);
                    }
                }

                return builder.Build();
            }

            private string Fit(string text, double size, bool bold) => Fit(text, size, bold, ContentWidth);

            private static string Fit(string text, double size, bool bold, double max)
            {
                if (Metrics.Width(text, size, bold) <= max)
                {
                    return text;
                }

                var cut = text;
                while (cut.Length > 1 && Metrics.Width(cut + "…", size, bold) > max)
                {
                    cut = cut[..^1];
                }

                return cut + "…";
            }
        }
    }
}
