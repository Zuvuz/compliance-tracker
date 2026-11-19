namespace ComplianceTracker.Domain.Mappings
{
    // US-204 R2: what a piece of evidence shows. Documentation describes how we work (a policy, a procedure, a design
    // diagram); proof shows that it happened (a file, a link, test results, a scan, an SBOM, a locked report).
    public enum EvidenceKind
    {
        Proof,
        Documentation
    }

    public static class EvidenceKinds
    {
        // R2: organisation documents and diagrams are documentation; everything else is proof, until a person says otherwise
        public static EvidenceKind DefaultOf(Evidence evidence)
            => evidence.IsFromOrganisation || evidence.IsDiagram ? EvidenceKind.Documentation : EvidenceKind.Proof;

        public static string NameOf(EvidenceKind kind) => kind == EvidenceKind.Documentation ? "Documentation" : "Proof";

        // R7: the heading of each list on a requirement
        public static string ListName(EvidenceKind kind) => kind == EvidenceKind.Documentation ? "Documented by" : "Proven by";

        // R10: on a story's evidence card
        public static string CardText(EvidenceKind kind) => kind == EvidenceKind.Documentation ? "Describes" : "Shows it is done";
    }
}
