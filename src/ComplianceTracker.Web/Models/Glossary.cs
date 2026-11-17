namespace ComplianceTracker.Web.Models
{
    // US-173: one term of the glossary: what it means in one or two sentences, and where it comes from (R2)
    public sealed record GlossaryTerm(string Term, string Explanation, string Source)
    {
        public string Anchor => StatusBadges.Slug(Term);
    }

    // US-173: the terms the pages use, explained once. A "?" after a term (<term>) shows the explanation; the Help page
    // lists them all.
    public static class Glossary
    {
        public static readonly IReadOnlyList<GlossaryTerm> Terms =
        [
            new("Audit package", "One locked ZIP with everything an auditor asks for: the reports, the evidence, the activity log and a dossier per framework, signed when the organisation has a seal.", "ISO/IEC 27001 9.2; CRA Annex VII"),
            new("Coverage", "The share of a framework's applicable requirements that a user story or evidence covers. Requirements that do not apply and recommendations do not count.", "The tracker (US-021)"),
            new("Covered", "At least one user story or piece of evidence that counts is linked to the requirement.", "The tracker (US-021, US-142)"),
            new("Covered, needs review", "The requirement is linked, but only by links that wait for a person to confirm them after a new version of the framework.", "The tracker (US-110)"),
            new("Evidence", "A file or a link that shows a requirement is met, e.g. a policy, a test report or a configuration. Expired evidence does not count.", "ISO/IEC 27001 7.5; CRA Annex VII"),
            new("Fingerprint", "The SHA-256 of a file. Changing a single byte changes it, so it shows that a locked report or a piece of evidence is unchanged.", "ISO/IEC 27001 A.5.33"),
            new("Lifecycle phase", "Where the product is: Planning, Design, Development, Verification, Released or End of support. Each phase has a checklist from the rules the project follows.", "CRA Art. 13; ISO/IEC 27001 A.8.25"),
            new("Locked report", "A report kept exactly as it was made, with its fingerprint, so you can show later what was reported.", "ISO/IEC 27001 A.5.33"),
            new("Measure", "What reduces a risk: a requirement of the project or a user story. A measure is backed when a story or evidence stands behind it.", "ISO/IEC 27001 6.1.3"),
            new("Needs action", "The AI marked the requirement as applying and not yet met. A person decides whether to create a story or dismiss it.", "The tracker (US-155); AI Act Art. 14"),
            new("Not applicable", "The requirement does not apply to the product, with a reason (and optionally the risk that justifies it). It does not count in the coverage.", "ISO/IEC 27001 6.1.3 d; CRA Art. 13(3)"),
            new("Residual risk", "The risk that is left after the measures. Set by the risk owner, never higher than the risk before the measures.", "ISO/IEC 27001 6.1.3 f, 8.3"),
            new("Risk level", "Likelihood × impact (1–5 each): Low up to 4, Medium up to 9, High up to 16, Critical above.", "ISO/IEC 27001 6.1.2"),
            new("SBOM", "A software bill of materials: the components of one version of the product, as CycloneDX or SPDX.", "CRA Annex I, Part II (1)"),
            new("Seal", "The organisation's certificate, used to sign reports and audit packages so a receiver can see who made them and that they are unchanged.", "eIDAS Art. 35–36"),
            new("Statement of Applicability", "The list of ISO/IEC 27001 Annex A controls with whether each applies, why, and how it is met.", "ISO/IEC 27001 6.1.3 d"),
            new("Treatment", "What is done about a risk: reduce it with measures, accept it, avoid it or transfer it.", "ISO/IEC 27001 6.1.3")
        ];

        public static GlossaryTerm? Find(string term)
            => Terms.FirstOrDefault(t => string.Equals(t.Term, term, StringComparison.OrdinalIgnoreCase));
    }
}
