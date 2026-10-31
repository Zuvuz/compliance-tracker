using System.Text;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ComplianceTracker.Specs.Support
{
    // Files for the upload tests (US-005). A PDF is recognised by starting with "%PDF-" (ASVS V5.2.2).
    public static class TestFiles
    {
        private static readonly byte[] PdfStart = Encoding.ASCII.GetBytes("%PDF-1.7\n");

        // A "PDF" of the given size: the PDF signature followed by zeros. Enough for the app's checks.
        public static byte[] Pdf(int sizeInBytes = 1024)
        {
            var bytes = new byte[Math.Max(sizeInBytes, PdfStart.Length)];
            PdfStart.CopyTo(bytes, 0);
            return bytes;
        }

        // Used to test the 20 MB limit
        public static byte[] PdfOfMegabytes(int megabytes) => Pdf(megabytes * 1024 * 1024);

        // US-007: a real PDF with one line of text per entry, top to bottom, so the requirement extraction can read it
        public static byte[] PdfWithText(params string[] lines)
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);

            var y = 800.0;
            foreach (var line in lines)
            {
                page.AddText(line, 11, new PdfPoint(50, y), font);
                y -= 20;
            }

            return builder.Build();
        }

        // Not a PDF, even if it is uploaded with a .pdf name
        public static byte[] TextFile() => Encoding.UTF8.GetBytes("This is not a PDF");
    }
}
