using System.Text.RegularExpressions;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Application.Frameworks
{
    // US-137 R1: an act as fetched from EUR-Lex: the package as it came (kept as the version's file), and its Formex files
    public sealed record EurLexAct(string Celex, byte[] Package, IReadOnlyList<string> FormexFiles);

    // US-137: fetches an EU act's Formex XML from the EU Publications Office (implemented in Infrastructure). It needs
    // internet, so the operator switches it on; tests use a fake with saved files.
    public interface IEurLexSource
    {
        bool IsEnabled { get; }

        // Throws a DomainException when the act cannot be fetched or has no Formex
        Task<EurLexAct> FetchAsync(string celex, CancellationToken cancellationToken = default);
    }

    public static partial class Celex
    {
        public const string Off = "Import from EUR-Lex is switched off. The operator can switch it on; it needs internet.";

        // "32024R2847" (a regulation), "32022L2555" (a directive), "32016R0679"; also a consolidated "02016R0679-20160504"
        [GeneratedRegex(@"^[0-9]{5}[A-Z]{1,2}[0-9]{4}(?:-[0-9]{8})?$")]
        private static partial Regex Number();

        public static string Check(string? celex)
        {
            var cleaned = (celex ?? "").Trim().ToUpperInvariant();
            if (!Number().IsMatch(cleaned))
            {
                throw new DomainException("Give a CELEX number, e.g. 32024R2847 for the CRA");
            }

            return cleaned;
        }

        // R7: where the act can be read
        public static string Address(string celex) => $"https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:{celex}";
    }
}
