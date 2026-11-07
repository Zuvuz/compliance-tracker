using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ComplianceTracker.Infrastructure.Reports
{
    // US-132: reads the organisation's .pfx and seals a locked report with it. The sealed PDF is the locked report's pages
    // unchanged, then a page with the signers and the locked report's fingerprint, signed digitally over all of it (R4).
    public class ReportSealer : IReportSealer
    {
        public SealCertificate Read(byte[] pfx, string password)
        {
            using var certificate = Load(pfx, password);
            if (!certificate.HasPrivateKey)
            {
                throw new DomainException("The certificate has no private key; export it with the key as .pfx");
            }

            if (certificate.GetRSAPrivateKey() is null && certificate.GetECDsaPrivateKey() is null)
            {
                throw new DomainException("The certificate must have an RSA or ECDSA key");
            }

            return new SealCertificate(certificate.Subject, certificate.Thumbprint, certificate.NotBefore.ToUniversalTime(), certificate.NotAfter.ToUniversalTime());
        }

        public byte[] Seal(
            byte[] lockedPdf, string title, string fingerprint, IReadOnlyList<SignatureLine> signers, byte[] pfx, string password, string organisation, DateTime atUtc)
        {
            using var certificate = Load(pfx, password);
            var builder = new PdfDocumentBuilder();
            using (var locked = PdfDocument.Open(lockedPdf))
            {
                for (var i = 1; i <= locked.NumberOfPages; i++)
                {
                    builder.AddPage(locked, i);
                }
            }

            var regular = builder.AddStandard14Font(Standard14Font.Helvetica);
            var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
            var page = builder.AddPage(PageSize.A4);
            var y = 780.0;

            void Line(string text, double size, PdfDocumentBuilder.AddedFont font, double gap = 6)
            {
                foreach (var part in PdfText.Wrap(text, 500, t => t.Length * size * 0.5))
                {
                    page.AddText(part, size, new PdfPoint(50, y), font);
                    y -= size + gap;
                }
            }

            Line("Signatures", 18, bold, 12);
            Line(title, 11, bold);
            Line($"Locked report SHA-256: {fingerprint}", 8, regular, 14);
            foreach (var signer in signers)
            {
                Line($"{signer.Name}, {signer.Role}", 11, regular, 2);
                Line($"Signed {signer.SignedAtUtc.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)} UTC, after signing in again", 9, regular, 10);
            }

            y -= 10;
            Line($"Sealed by {organisation} with the certificate {certificate.Subject}", 9, regular, 2);
            Line($"Certificate {certificate.Thumbprint}, valid until {certificate.NotAfter.ToUniversalTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}", 9, regular, 2);
            Line($"Sealed {atUtc.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)} UTC. The digital signature covers every page of this file.", 9, regular);

            return PdfSigner.Sign(builder.Build(), certificate, organisation, $"Signed by {string.Join(" and ", signers.Select(s => s.Name))}", atUtc);
        }

        private static X509Certificate2 Load(byte[] pfx, string password)
        {
            try
            {
                return X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException)
            {
                throw new DomainException("The file is not a certificate (.pfx), or the password is wrong");
            }
        }
    }
}
