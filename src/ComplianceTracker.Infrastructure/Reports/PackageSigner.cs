using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using ComplianceTracker.Application.Reports;

namespace ComplianceTracker.Infrastructure.Reports
{
    // US-180: a detached CMS signature (PKCS #7, SHA-256) over the audit package's manifest.json, with the organisation's
    // seal (US-132). Anyone can check it with OpenSSL: openssl cms -verify -binary -inform DER -in manifest.json.p7s
    // -content manifest.json -noverify
    public sealed class PackageSigner : IPackageSigner
    {
        public byte[] Sign(byte[] data, byte[] pfx, string password, DateTime atUtc)
        {
            using var certificate = X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.EphemeralKeySet);
            var cms = new SignedCms(new ContentInfo(data), detached: true);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
            {
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
                IncludeOption = X509IncludeOption.EndCertOnly
            };
            signer.SignedAttributes.Add(new Pkcs9SigningTime(atUtc));
            cms.ComputeSignature(signer);
            return cms.Encode();
        }

        // The signature is checked over the data; the certificate's chain is not, as the seal may be the organisation's own
        public PackageSignature? Verify(byte[] data, byte[] signature)
        {
            try
            {
                var cms = new SignedCms(new ContentInfo(data), detached: true);
                cms.Decode(signature);
                cms.CheckSignature(verifySignatureOnly: true);
                var info = cms.SignerInfos[0];
                DateTime? at = null;
                foreach (var attribute in info.SignedAttributes)
                {
                    if (attribute.Oid.Value == "1.2.840.113549.1.9.5") // signing time
                    {
                        at = new Pkcs9SigningTime(attribute.Values[0].RawData).SigningTime.ToUniversalTime();
                    }
                }

                return info.Certificate is { } certificate ? new PackageSignature(certificate.GetNameInfo(X509NameType.SimpleName, false), at) : null;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
