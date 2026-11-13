using ComplianceTracker.Application.Compliance;

namespace ComplianceTracker.Application.Reports
{
    // US-129 R1, R3: a report on the project's Reports tab, who it is for, what it holds, and why it cannot be made yet
    public record ProjectReport(string Key, string Title, string ForWhom, string Contents, string Formats, string? Unavailable)
    {
        public bool IsAvailable => Unavailable is null;
    }

    // US-129: the reports a project can make. Later reports (US-051, US-053–US-055, US-076) join this list.
    public static class ProjectReports
    {
        public const string Traceability = "traceability";
        public const string TechnicalDocumentation = "technical-documentation";
        public const string RiskAssessment = "risk-assessment"; // US-054
        public const string StatementOfApplicability = "statement-of-applicability"; // US-053

        public static IReadOnlyList<ProjectReport> For(int frameworkCount, bool followsCra) =>
        [
            new(Traceability, "Traceability", "The team, an auditor or the management",
                "Each requirement with the user stories and evidence that cover it. For the management, a summary.",
                "Excel, CSV or PDF",
                frameworkCount > 0 ? null : "Add a framework to the project to make a traceability report"),
            new(TechnicalDocumentation, "Technical documentation (draft)", "The manufacturer's technical file",
                "The structure of CRA Annex VII, filled in from the project's data.",
                "Word or PDF",
                followsCra && frameworkCount > 0 ? null : "Add the CRA to the project to make the technical documentation draft"),
            new(RiskAssessment, "Risk assessment", "A market surveillance authority, an auditor or the management",
                "The criteria, every risk by level with its measures and actions, the accepted risks with who accepted them, and the closed ones.",
                "Excel, CSV or PDF",
                null)
        ];

        public static IReadOnlyList<ProjectReport> For(ProjectCompliance? compliance)
            => For(compliance?.Frameworks.Count ?? 0, compliance is not null && TechnicalDocumentationQueries.CraOf(compliance) is not null);

        // US-053: the organisation area also has the Statement of Applicability, across the organisation's projects
        public static IReadOnlyList<ProjectReport> ForOrganisation(ProjectCompliance? compliance, bool hasIso27001) =>
        [
            .. For(compliance),
            new(StatementOfApplicability, "Statement of Applicability", "The certification auditor (ISO/IEC 27001)",
                "Every Annex A control: whether it applies and why not, how it is implemented, and the projects and stories that meet it.",
                "Excel, CSV or PDF",
                hasIso27001 ? null : "Upload ISO/IEC 27001 as a framework and follow it to make the Statement of Applicability")
        ];
    }
}
