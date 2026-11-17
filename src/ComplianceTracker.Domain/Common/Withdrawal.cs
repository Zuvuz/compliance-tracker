namespace ComplianceTracker.Domain.Common
{
    // US-177 R4: an upload made by mistake is withdrawn with a reason; it is kept in the history, never deleted (R2)
    public static class Withdrawal
    {
        public const int MaxReasonLength = 500;

        public static string Reason(string? reason)
        {
            var text = (reason ?? "").Trim();
            if (text.Length == 0)
            {
                throw new DomainException("Give a reason for withdrawing it");
            }

            if (text.Length > MaxReasonLength)
            {
                throw new DomainException($"The reason can be at most {MaxReasonLength} characters");
            }

            return text;
        }
    }
}
