using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Infrastructure.Reports;

namespace ComplianceTracker.UnitTests.Reports;

// US-180: edge cases of the audit package's manifest, its signature and the check. Arrange / Act / Assert.
public class PackageManifestTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly PackageSigner Signer = new();

    private static byte[] Pfx(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.Export(X509ContentType.Pfx, "secret");
    }

    private static readonly Dictionary<string, byte[]> Files = new()
    {
        ["00-contents.pdf"] = Encoding.UTF8.GetBytes("contents"),
        ["evidence/a.pdf"] = Encoding.UTF8.GetBytes("evidence a"),
        ["CRA-dossier.pdf"] = Encoding.UTF8.GetBytes("dossier")
    };

    private static byte[] Package(Dictionary<string, byte[]> files, bool signed = true, Action<Dictionary<string, byte[]>>? after = null,
        byte[]? signWith = null)
    {
        var manifest = PackageManifest.Create("Audit package", "Acme ApS", "Acme Portal", "Anna", Monday, signed, files.Select(f => (f.Key, f.Value)));
        var json = PackageManifest.ToJson(manifest);
        var all = new Dictionary<string, byte[]>(files) { [PackageManifest.ManifestPath] = json };
        if (signed)
        {
            all[PackageManifest.SignaturePath] = Signer.Sign(json, signWith ?? Pfx("Acme ApS"), "secret", Monday);
        }

        after?.Invoke(all);
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in all)
            {
                using var stream = archive.CreateEntry(path).Open();
                stream.Write(content);
            }
        }

        return memory.ToArray();
    }

    [Fact]
    public void An_unchanged_signed_package_is_unchanged_and_names_the_signer()
    {
        var check = PackageManifest.Check(Package(Files), Signer);

        Assert.True(check.Unchanged);
        Assert.Equal("Acme ApS", check.Signature!.Subject);
        Assert.Equal(Monday, check.Signature.SignedAtUtc);
        Assert.Equal(3, check.Manifest!.Files.Count);
    }

    [Fact]
    public void The_manifest_lists_the_files_sorted_and_not_itself()
    {
        var manifest = PackageManifest.Create("P", "O", "Pr", "Anna", Monday, true,
            [("b.pdf", [1]), ("a.pdf", [2]), (PackageManifest.ManifestPath, [3]), (PackageManifest.SignaturePath, [4])]);

        Assert.Equal(["a.pdf", "b.pdf"], manifest.Files.Select(f => f.Path));
    }

    [Fact]
    public void A_changed_file_is_listed()
    {
        var check = PackageManifest.Check(Package(Files, after: f => f["evidence/a.pdf"] = Encoding.UTF8.GetBytes("evidence b")), Signer);

        Assert.False(check.Unchanged);
        Assert.True(check.SignatureValid);
        Assert.Equal([(FileDifference.Changed, "evidence/a.pdf")], check.Differences);
    }

    [Fact]
    public void A_missing_and_an_extra_file_are_listed()
    {
        var check = PackageManifest.Check(Package(Files, after: f =>
        {
            f.Remove("CRA-dossier.pdf");
            f["extra.txt"] = [1];
        }), Signer);

        Assert.Equal([(FileDifference.Missing, "CRA-dossier.pdf"), (FileDifference.Added, "extra.txt")], check.Differences);
    }

    [Fact]
    public void A_manifest_changed_after_signing_has_no_valid_signature()
    {
        var check = PackageManifest.Check(Package(Files, after: f =>
            f[PackageManifest.ManifestPath] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f[PackageManifest.ManifestPath]).Replace("Anna", "Bo"))), Signer);

        Assert.True(check.Signed);
        Assert.False(check.SignatureValid);
        Assert.False(check.Unchanged);
    }

    [Fact]
    public void A_manifest_signed_with_another_certificate_names_that_signer()
    {
        var check = PackageManifest.Check(Package(Files, signWith: Pfx("Mallory Ltd")), Signer);

        // The signature is valid, but by someone else than the manifest says: the page shows the signer, not the manifest's name
        Assert.Equal("Mallory Ltd", check.Signature!.Subject);
        Assert.NotEqual(check.Manifest!.Organisation, check.Signature.Subject);
    }

    [Fact]
    public void A_signature_from_another_manifest_is_not_valid()
    {
        var other = PackageManifest.ToJson(PackageManifest.Create("Other", "Acme ApS", "X", "Anna", Monday, true, []));
        var check = PackageManifest.Check(Package(Files, after: f => f[PackageManifest.SignaturePath] = Signer.Sign(other, Pfx("Acme ApS"), "secret", Monday)), Signer);

        Assert.False(check.SignatureValid);
    }

    [Fact]
    public void Without_a_seal_the_files_can_be_checked_but_the_package_is_not_unchanged()
    {
        var check = PackageManifest.Check(Package(Files, signed: false), Signer);

        Assert.False(check.Signed);
        Assert.True(check.FilesMatch);
        Assert.False(check.Unchanged);
        Assert.False(check.Manifest!.Signed);
    }

    [Fact]
    public void A_package_without_a_manifest_says_so()
    {
        var check = PackageManifest.Check(Package(Files, after: f => f.Remove(PackageManifest.ManifestPath)), Signer);

        Assert.Equal("The package has no manifest.json, so it cannot be checked", check.Problem);
    }

    [Fact]
    public void A_manifest_that_is_not_json_is_a_problem()
    {
        var check = PackageManifest.Check(Package(Files, after: f => f[PackageManifest.ManifestPath] = "{ not json"u8.ToArray()), Signer);

        Assert.NotNull(check.Problem);
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_a_problem()
        => Assert.Equal("The file is not a ZIP file", PackageManifest.Check("hello"u8.ToArray(), Signer).Problem);

    [Fact]
    public void An_empty_file_is_refused()
        => Assert.Throws<DomainException>(() => PackageManifest.Check([], Signer));

    [Fact]
    public void A_garbage_signature_is_not_valid()
        => Assert.Null(Signer.Verify("data"u8.ToArray(), [1, 2, 3]));
}
