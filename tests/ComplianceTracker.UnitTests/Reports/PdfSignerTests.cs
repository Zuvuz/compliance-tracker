using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Infrastructure.Reports;
using UglyToad.PdfPig;

namespace ComplianceTracker.UnitTests.Reports;

// Edge-case unit tests of US-132: the organisation's seal as a digital signature in the tracker's PDFs.
// Arrange / Act / Assert.
public class PdfSignerTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    public static X509Certificate2 Certificate(string name = "Acme ApS", DateTimeOffset? notAfter = null, bool withKey = true)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}, O={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), notAfter ?? DateTimeOffset.UtcNow.AddYears(1));
        return withKey ? X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx, "x"), "x") : X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
    }

    private static byte[] Report(int rows = 3)
        => new PdfPigReportWriter().FromReport(
            new ReportDocument("Traceability – Acme Portal", [("Coverage", "50 %")],
                [new ReportSheet("CRA", ["Code", "Status"], 1, Enumerable.Range(1, rows).Select(i => new ReportRow([$"I.{i}", "Covered"], ReportStatus.Good)).ToList())]),
            new PdfHeader("Acme ApS", "Acme Portal", "Traceability", Monday, "Anna"));

    [Fact]
    public void A_signed_report_is_valid_and_covers_the_whole_file()
    {
        // Arrange
        using var certificate = Certificate();

        // Act
        var signed = PdfSigner.Sign(Report(), certificate, "Acme ApS", "Approved", Monday);
        var (valid, covers, signer) = PdfSigner.Verify(signed);

        // Assert
        Assert.True(valid);
        Assert.True(covers);
        Assert.Equal(certificate.Thumbprint, signer!.Thumbprint);
    }

    [Fact]
    public void The_original_bytes_are_kept_unchanged_at_the_start()
    {
        // Arrange
        var original = Report();
        using var certificate = Certificate();

        // Act
        var signed = PdfSigner.Sign(original, certificate, "Acme ApS", "Approved", Monday);

        // Assert
        Assert.Equal(original, signed.Take(original.Length).ToArray());
    }

    [Fact]
    public void A_signed_report_still_opens_with_all_its_pages_and_text()
    {
        // Arrange
        using var certificate = Certificate();
        var original = Report(150);

        // Act
        var signed = PdfSigner.Sign(original, certificate, "Acme ApS", "Approved", Monday);

        // Assert
        using var before = PdfDocument.Open(original);
        using var after = PdfDocument.Open(signed);
        Assert.Equal(before.NumberOfPages, after.NumberOfPages);
        Assert.Contains("I.150", string.Join(" ", after.GetPages().Select(p => p.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_changed_byte_breaks_the_signature()
    {
        // Arrange
        using var certificate = Certificate();
        var signed = PdfSigner.Sign(Report(), certificate, "Acme ApS", "Approved", Monday);

        // Act: change a byte inside the original report
        signed[200] ^= 0x01;

        // Assert
        Assert.False(PdfSigner.Verify(signed).Valid);
    }

    [Fact]
    public void Bytes_added_after_the_signature_are_not_covered()
    {
        // Arrange
        using var certificate = Certificate();
        var signed = PdfSigner.Sign(Report(), certificate, "Acme ApS", "Approved", Monday);

        // Act
        var appended = signed.Concat("\n% added later\n"u8.ToArray()).ToArray();

        // Assert
        Assert.False(PdfSigner.Verify(appended).CoversWholeFile);
    }

    [Fact]
    public void A_certificate_without_a_private_key_cannot_sign()
    {
        // Arrange
        using var certificate = Certificate(withKey: false);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => PdfSigner.Sign(Report(), certificate, "Acme ApS", "Approved", Monday));
    }

    [Fact]
    public void A_report_cannot_be_signed_twice_this_way()
    {
        // Arrange
        using var certificate = Certificate();
        var signed = PdfSigner.Sign(Report(), certificate, "Acme ApS", "Approved", Monday);

        // Act / Assert: it has a form already
        Assert.Throws<InvalidOperationException>(() => PdfSigner.Sign(signed, certificate, "Acme ApS", "Again", Monday));
    }

    [Fact]
    public void A_file_that_is_not_a_tracker_PDF_is_refused()
    {
        // Arrange
        using var certificate = Certificate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => PdfSigner.Sign("not a pdf"u8.ToArray(), certificate, "Acme ApS", "Approved", Monday));
    }

    [Fact]
    public void Names_with_brackets_and_Danish_letters_are_written_safely()
    {
        // Arrange
        using var certificate = Certificate("Æble (DK)");

        // Act
        var signed = PdfSigner.Sign(Report(), certificate, "Æble (DK) \\ ApS", "Approved → sealed", Monday);

        // Assert
        Assert.True(PdfSigner.Verify(signed).Valid);
        using var document = PdfDocument.Open(signed);
        Assert.True(document.NumberOfPages > 0);
    }

    [Fact]
    public void An_unsigned_file_is_not_valid()
    {
        // Act / Assert
        Assert.False(PdfSigner.Verify(Report()).Valid);
    }
}
