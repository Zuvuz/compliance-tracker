using System.Text;

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

        // Not a PDF, even if it is uploaded with a .pdf name
        public static byte[] TextFile() => Encoding.UTF8.GetBytes("This is not a PDF");
    }
}
