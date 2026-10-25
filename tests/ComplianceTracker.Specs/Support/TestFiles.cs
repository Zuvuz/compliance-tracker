using System.Text;

namespace ComplianceTracker.Specs.Support
{
    public static class TestFiles
    {
        private static readonly byte[] PdfStart = Encoding.ASCII.GetBytes("%PDF-1.7\n");

        public static byte[] Pdf(int sizeInBytes = 1024)
        {
            var bytes = new byte[Math.Max(sizeInBytes, PdfStart.Length)];
            PdfStart.CopyTo(bytes, 0);
            return bytes;
        }

        public static byte[] PdfOfMegabytes(int megabytes) => Pdf(megabytes * 1024 * 1024);

        public static byte[] TextFile() => Encoding.UTF8.GetBytes("This is not a PDF");
    }
}
