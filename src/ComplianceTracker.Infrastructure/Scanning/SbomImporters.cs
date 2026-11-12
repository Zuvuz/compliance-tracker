using System.Globalization;
using System.Text.Json;
using ComplianceTracker.Application.Scanning;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Scanning;

namespace ComplianceTracker.Infrastructure.Scanning
{
    // US-079 R1: reads a CycloneDX JSON SBOM, 1.4 or newer (https://cyclonedx.org/docs/1.6/json/), as Syft and Trivy write it.
    // Nested components are read too.
    public class CycloneDxSbomImporter : ISbomImporter
    {
        private const int MaxDepth = 20;

        public bool CanRead(string fileName, byte[] content)
            => SbomJson.Root(content) is { } root
               && SbomJson.Text(root, "bomFormat") == "CycloneDX";

        public SbomRead Read(byte[] content)
        {
            var root = SbomJson.Root(content) ?? throw new DomainException("The file is not valid JSON");
            var spec = SbomJson.Text(root, "specVersion") ?? "";
            if (!decimal.TryParse(spec, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || number < 1.4m)
            {
                throw new DomainException("Only CycloneDX 1.4 or newer can be read");
            }

            var components = new List<SbomComponentRead>();
            if (root.TryGetProperty("components", out var list))
            {
                Add(list, components, 0);
            }

            return new SbomRead($"CycloneDX {spec}", components);
        }

        private static void Add(JsonElement list, List<SbomComponentRead> into, int depth)
        {
            if (list.ValueKind != JsonValueKind.Array || depth > MaxDepth)
            {
                return;
            }

            foreach (var component in list.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.Object))
            {
                if (SbomJson.Text(component, "name") is { } name)
                {
                    into.Add(new SbomComponentRead(name, SbomJson.Text(component, "version"), SbomJson.Text(component, "purl"),
                        License(component), component.TryGetProperty("supplier", out var supplier) ? SbomJson.Text(supplier, "name") : null));
                }

                if (component.TryGetProperty("components", out var nested))
                {
                    Add(nested, into, depth + 1);
                }
            }
        }

        // "licenses": [{ "license": { "id": "MIT" } }, { "license": { "name": "…" } }, { "expression": "MIT OR Apache-2.0" }]
        private static string? License(JsonElement component)
        {
            if (!component.TryGetProperty("licenses", out var licenses) || licenses.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var names = licenses.EnumerateArray()
                .Where(l => l.ValueKind == JsonValueKind.Object)
                .Select(l => SbomJson.Text(l, "expression")
                    ?? (l.TryGetProperty("license", out var license) ? SbomJson.Text(license, "id") ?? SbomJson.Text(license, "name") : null))
                .Where(n => n is not null)
                .Distinct()
                .ToList();
            return names.Count == 0 ? null : string.Join(" AND ", names);
        }
    }

    // US-079 R1: reads an SPDX 2.3 JSON SBOM (https://spdx.github.io/spdx-spec/v2.3/), as Syft and Trivy write it
    public class SpdxSbomImporter : ISbomImporter
    {
        public bool CanRead(string fileName, byte[] content)
            => SbomJson.Root(content) is { } root
               && SbomJson.Text(root, "spdxVersion") is { } version
               && version.StartsWith("SPDX-", StringComparison.Ordinal);

        public SbomRead Read(byte[] content)
        {
            var root = SbomJson.Root(content) ?? throw new DomainException("The file is not valid JSON");
            var version = SbomJson.Text(root, "spdxVersion");
            if (version != "SPDX-2.3")
            {
                throw new DomainException("Only SPDX 2.3 can be read");
            }

            var components = new List<SbomComponentRead>();
            if (root.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Array)
            {
                foreach (var package in packages.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object))
                {
                    if (SbomJson.Text(package, "name") is not { } name)
                    {
                        continue;
                    }

                    components.Add(new SbomComponentRead(name, SbomJson.Text(package, "versionInfo"), PackageUrl(package),
                        Known(SbomJson.Text(package, "licenseConcluded")) ?? Known(SbomJson.Text(package, "licenseDeclared")),
                        Supplier(SbomJson.Text(package, "supplier"))));
                }
            }

            return new SbomRead("SPDX 2.3", components);
        }

        // "externalRefs": [{ "referenceType": "purl", "referenceLocator": "pkg:npm/lodash@4.17.21" }]
        private static string? PackageUrl(JsonElement package)
            => package.TryGetProperty("externalRefs", out var refs) && refs.ValueKind == JsonValueKind.Array
                ? refs.EnumerateArray()
                    .Where(r => r.ValueKind == JsonValueKind.Object && SbomJson.Text(r, "referenceType") == "purl")
                    .Select(r => SbomJson.Text(r, "referenceLocator"))
                    .FirstOrDefault(p => p is not null)
                : null;

        private static string? Known(string? value) => value is null or "NOASSERTION" or "NONE" ? null : value;

        // "Organization: Acme ApS" or "Person: Anna" → "Acme ApS"
        private static string? Supplier(string? value)
        {
            if (Known(value) is not { } text)
            {
                return null;
            }

            var colon = text.IndexOf(':');
            return colon >= 0 ? text[(colon + 1)..].Trim() : text;
        }
    }

    internal static class SbomJson
    {
        // The root object, or null when the content is not a JSON object
        public static JsonElement? Root(byte[] content)
        {
            try
            {
                using var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64 });
                return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string? Text(JsonElement element, string property)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : null;
    }
}
