namespace ComplianceTracker.Web.Models
{
    // US-147 R2: a cell of the risk matrix in an address, e.g. "3x4" (likelihood 3, impact 4). Anything else is no cell.
    public static class RiskCells
    {
        public static (int Likelihood, int Impact)? Parse(string? cell)
        {
            var parts = (cell ?? "").Split('x', 'X');
            return parts.Length == 2 && int.TryParse(parts[0], out var likelihood) && int.TryParse(parts[1], out var impact)
                   && likelihood is >= Domain.Risks.Risk.MinScale and <= Domain.Risks.Risk.MaxScale
                   && impact is >= Domain.Risks.Risk.MinScale and <= Domain.Risks.Risk.MaxScale
                ? (likelihood, impact)
                : null;
        }

        public static string Format(int likelihood, int impact) => $"{likelihood}x{impact}";
    }
}
