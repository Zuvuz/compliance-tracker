using System.Text;
using System.Text.Json;

namespace ComplianceTracker.Specs.Support
{
    // US-079: small SBOM files for the scenarios, as Syft or Trivy write them (trimmed)
    public static class TestSboms
    {
        public record Component(string Name, string Version, string Purl, string License);

        // The first components a real Trivy SBOM of the tracker would list
        public static readonly Component[] Known =
        [
            new("openssl", "3.0.1", "pkg:deb/debian/openssl@3.0.1", "Apache-2.0"),
            new("Npgsql", "10.0.3", "pkg:nuget/Npgsql@10.0.3", "PostgreSQL"),
            new("lodash", "4.17.21", "pkg:npm/lodash@4.17.21", "MIT"),
            new("zlib", "1.3.1", "pkg:deb/debian/zlib@1.3.1", "Zlib")
        ];

        public static byte[] CycloneDx(int count, string specVersion = "1.6")
            => Json(new
            {
                bomFormat = "CycloneDX",
                specVersion,
                version = 1,
                metadata = new { component = new { type = "application", name = "Acme Portal" } },
                components = Known.Take(count).Select(c => new
                {
                    type = "library",
                    name = c.Name,
                    version = c.Version,
                    purl = c.Purl,
                    licenses = new[] { new { license = new { id = c.License } } }
                })
            });

        public static byte[] Spdx(int count)
            => Json(new
            {
                spdxVersion = "SPDX-2.3",
                dataLicense = "CC0-1.0",
                SPDXID = "SPDXRef-DOCUMENT",
                name = "acme-portal",
                packages = Known.Take(count).Select((c, i) => new
                {
                    name = c.Name,
                    SPDXID = $"SPDXRef-Package-{i}",
                    versionInfo = c.Version,
                    licenseConcluded = c.License,
                    supplier = "Organization: Example",
                    externalRefs = new[] { new { referenceCategory = "PACKAGE-MANAGER", referenceType = "purl", referenceLocator = c.Purl } }
                })
            });

        private static byte[] Json(object value) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
    }
}
