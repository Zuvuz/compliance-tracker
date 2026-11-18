namespace ComplianceTracker.Web.Models
{
    // US-203 R2: the risk matrix shows the open risks before the measures (the default) or after them (by the residual)
    public static class RiskMatrix
    {
        public const string Before = "before";
        public const string After = "after";

        public static bool IsAfter(string? matrix) => string.Equals(matrix, After, StringComparison.OrdinalIgnoreCase);
    }
}
