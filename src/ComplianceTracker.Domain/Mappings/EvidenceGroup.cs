namespace ComplianceTracker.Domain.Mappings
{
    // US-201: where a piece of evidence came from, set when it is made. Everything before US-201 was marked by the migration.
    public enum EvidenceSource
    {
        Uploaded,     // a file or a link added by a person, or a pull request accepted from a suggestion
        TestResults,  // a test run accepted from a suggestion (US-058)
        Sbom,         // an SBOM used as evidence (US-079 R4)
        Scan,         // a scan used as evidence (US-079 R5)
        Report        // a locked management report used as evidence (US-050 R8)
    }

    // US-201 R1: the groups of the Evidence tab, in the order they are shown
    public enum EvidenceGroup
    {
        Files,
        Links,
        TestResults,
        Sboms,
        Scans,
        Reports,
        Diagrams,
        OrganisationDocuments
    }

    public static class EvidenceGroups
    {
        // R2: every piece of evidence is in exactly one group; where it came from wins over what it is
        public static EvidenceGroup Of(Evidence evidence) => evidence switch
        {
            { IsFromOrganisation: true } => EvidenceGroup.OrganisationDocuments,
            { Source: EvidenceSource.TestResults } => EvidenceGroup.TestResults,
            { Source: EvidenceSource.Sbom } => EvidenceGroup.Sboms,
            { Source: EvidenceSource.Scan } => EvidenceGroup.Scans,
            { Source: EvidenceSource.Report } => EvidenceGroup.Reports,
            { IsDiagram: true } => EvidenceGroup.Diagrams,
            { IsFile: true } => EvidenceGroup.Files,
            _ => EvidenceGroup.Links
        };

        public static string NameOf(EvidenceGroup group) => group switch
        {
            EvidenceGroup.Files => "Files",
            EvidenceGroup.Links => "Links",
            EvidenceGroup.TestResults => "Test results",
            EvidenceGroup.Sboms => "SBOMs",
            EvidenceGroup.Scans => "Scans",
            EvidenceGroup.Reports => "Reports",
            EvidenceGroup.Diagrams => "Diagrams",
            _ => "Organisation documents"
        };

        // e.g. "test-results", for ids and the browser's memory
        public static string SlugOf(EvidenceGroup group) => group switch
        {
            EvidenceGroup.TestResults => "test-results",
            EvidenceGroup.OrganisationDocuments => "organisation-documents",
            _ => group.ToString().ToLowerInvariant()
        };
    }
}
