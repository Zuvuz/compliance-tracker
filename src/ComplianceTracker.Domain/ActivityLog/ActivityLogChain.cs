using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ComplianceTracker.Domain.ActivityLog
{
    // US-091: each organisation's log entries form a chain. An entry's Hash is the SHA-256 of its content and the
    // previous entry's Hash, so a changed, removed or inserted entry breaks the chain from that point on.
    public static class ActivityLogChain
    {
        // The entry's content as one text. The time has microseconds, as PostgreSQL stores it, so it reads back the same.
        public static string HashOf(ActivityLogEntry entry, string? previousHash)
        {
            var content = string.Join('\u001f',
                previousHash ?? "",
                entry.OrganizationId.ToString("D"),
                entry.UserId?.ToString("D") ?? "",
                entry.Action.ToString(),
                DateTime.SpecifyKind(entry.OccurredAt, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
                entry.SubjectId?.ToString("D") ?? "",
                entry.Role?.ToString() ?? "",
                entry.Details ?? "");
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        }

        // R3, R4: goes through one organisation's entries in the order they were written. Entries from before the chain
        // existed (no Hash) are only allowed at the start.
        public static ChainCheck Verify(IEnumerable<ActivityLogEntry> entriesInOrder)
        {
            var unsealed = 0;
            var sealedCount = 0;
            string? previous = null;
            var started = false;

            foreach (var entry in entriesInOrder)
            {
                if (entry.Hash is null)
                {
                    if (started)
                    {
                        return new ChainCheck(sealedCount, unsealed, entry); // an entry added outside the tracker
                    }

                    unsealed++;
                    continue;
                }

                started = true;
                if (entry.PreviousHash != previous || entry.Hash != HashOf(entry, entry.PreviousHash))
                {
                    return new ChainCheck(sealedCount, unsealed, entry);
                }

                previous = entry.Hash;
                sealedCount++;
            }

            return new ChainCheck(sealedCount, unsealed, null);
        }
    }

    // How a check of the chain went: entries checked, older entries without a fingerprint, and the first that does not match
    public record ChainCheck(int Checked, int WithoutFingerprint, ActivityLogEntry? FirstBroken)
    {
        public bool IsIntact => FirstBroken is null;
    }
}
