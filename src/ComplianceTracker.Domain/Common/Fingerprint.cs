using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.Common
{
    // US-145 R1, R3: a short fingerprint of what was judged, so a rejection can tell whether either side has changed since.
    // Case and spacing do not count as a change. A pair's fingerprint is "item|requirement".
    public static class Fingerprint
    {
        public static string Of(params string?[] parts)
        {
            var text = string.Join("\n", parts.Select(p => string.Join(' ', (p ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
        }

        public static string Pair(string item, string requirement) => $"{item}|{requirement}";

        // R4: which side changed: "item", "requirement" or "both"; null when nothing did (or nothing was stored)
        public static string? WhatChanged(string? stored, string current)
        {
            if (stored is null || stored == current)
            {
                return null;
            }

            var (before, now) = (stored.Split('|'), current.Split('|'));
            if (before.Length != 2 || now.Length != 2)
            {
                return "both";
            }

            return (before[0] != now[0], before[1] != now[1]) switch
            {
                (true, true) => "both",
                (true, false) => "item",
                _ => "requirement"
            };
        }
    }
}
