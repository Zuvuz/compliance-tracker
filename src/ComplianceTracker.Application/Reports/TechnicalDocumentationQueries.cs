using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Projects;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Frameworks;
using System.Globalization;

namespace ComplianceTracker.Application.Reports
{
    // One block of a generated document. The writer (Infrastructure) decides how each kind looks.
    public enum DocumentBlockKind
    {
        Title,
        Heading1,
        Heading2,
        Paragraph,
        Bullet,

        // R5: a part the tracker cannot fill, with a short hint
        ToBeCompleted
    }

    public record DocumentBlock(DocumentBlockKind Kind, string Text);

    public record GeneratedDocument(string FileName, IReadOnlyList<DocumentBlock> Blocks);

    // Turns a generated document into a file people can finish editing (DOCX, implemented with Open XML)
    public interface IDocumentWriter
    {
        byte[] ToDocx(GeneratedDocument document);
    }

    // US-056: a draft of the CRA technical documentation for one project, with the headings of Annex VII (R1).
    // Made from the same data as the Compliance tab (ComplianceQueries) and the risk register (RiskQueries), so the
    // draft and the tracker always agree. The draft can be made again at any time (Art. 31(2)); locking a final
    // version comes with US-050.
    public class TechnicalDocumentationQueries
    {
        public const string ToBeCompleted = "To be completed";

        private readonly ProjectQueries _projects;
        private readonly ComplianceQueries _compliance;
        private readonly RiskQueries _risks;
        private readonly CurrentMember _member;
        private readonly TimeProvider _clock;

        public TechnicalDocumentationQueries(
            ProjectQueries projects, ComplianceQueries compliance, RiskQueries risks, CurrentMember member, TimeProvider clock)
        {
            _projects = projects;
            _compliance = compliance;
            _risks = risks;
            _member = member;
            _clock = clock;
        }

        // R6: the CRA is the built-in regulation whose name starts with "CRA"
        public static FrameworkCompliance? CraOf(ProjectCompliance compliance)
            => compliance.Frameworks.FirstOrDefault(f => IsCra(f.Framework));

        public static bool IsCra(Framework framework)
            => framework.IsBuiltIn && framework.Name.StartsWith("CRA", StringComparison.Ordinal);

        // The draft, or null when the project is not found or does not follow the CRA (R6)
        public async Task<GeneratedDocument?> DraftAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projects.GetAsync(projectId, cancellationToken);
            var compliance = await _compliance.ForProjectAsync(projectId, cancellationToken);
            var risks = await _risks.ForProjectAsync(projectId, cancellationToken);
            if (project is null || compliance is null || risks is null || CraOf(compliance) is not { } cra)
            {
                return null;
            }

            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var now = _clock.GetUtcNow().UtcDateTime;
            var blocks = new List<DocumentBlock>();
            void Add(DocumentBlockKind kind, string text) => blocks.Add(new DocumentBlock(kind, text));
            void Missing(string hint) => Add(DocumentBlockKind.ToBeCompleted, $"{ToBeCompleted} – {hint}");

            var requirementCodes = compliance.Frameworks.SelectMany(f => f.Requirements).ToDictionary(r => r.Requirement.Id, r => r.Requirement.Code);
            var stories = compliance.WorkItems.ToDictionary(w => w.Id);

            // R7: what it is, when and by whom
            Add(DocumentBlockKind.Title, $"Technical documentation (draft) – {project.Name}");
            Add(DocumentBlockKind.Paragraph,
                $"Regulation (EU) 2024/2847 (Cyber Resilience Act), Annex VII. Made from the compliance tracker on "
                + $"{now.ToLocalTime().ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)} by {user.Name}, "
                + $"{organization.Name}, using version {cra.Version.Number} of the CRA in the tracker.");
            Add(DocumentBlockKind.Paragraph,
                "This is a draft. Complete the parts marked \"To be completed\" and check every section before it is used "
                + "for the conformity assessment (internal control, module A, Annex VIII).");

            // ------------------------------------------------------------ 1 (R2)
            Add(DocumentBlockKind.Heading1, "1. General description of the product");
            Add(DocumentBlockKind.Paragraph, $"Name: {project.Name}");
            Add(DocumentBlockKind.Paragraph, $"Description: {Or(project.Description, "–")}");
            Add(DocumentBlockKind.Paragraph, $"Lifecycle phase: {project.Phase}");
            Add(DocumentBlockKind.Paragraph, $"Scope: {Or(project.Scope, "–")}");
            Add(DocumentBlockKind.Heading2, "Support period");
            if (project.SupportEndsOn is { } supportEnds)
            {
                // US-025: CRA Art. 13(8), (19)
                Add(DocumentBlockKind.Paragraph, $"Supported until {supportEnds:dd-MM-yyyy}"
                    + (project.ReleasedOn is { } released ? $" (released {released:dd-MM-yyyy})" : "") + ".");
            }
            else
            {
                Missing("state the support period and its end date (Art. 13(8), (19)).");
            }
            Add(DocumentBlockKind.Heading2, "(a) Intended purpose");
            Add(DocumentBlockKind.Paragraph, Or(project.IntendedPurpose, "–"));
            Add(DocumentBlockKind.Heading2, "(b) Versions of software affecting compliance");
            Missing("name the software versions this documentation covers.");
            Add(DocumentBlockKind.Heading2, "(c) Photographs or illustrations (hardware products only)");
            Missing("add photographs or illustrations of the external features, marking and internal layout, or write that the product is software only.");
            Add(DocumentBlockKind.Heading2, "(d) User information and instructions (Annex II)");
            Missing("add the information and instructions to the user, or where they can be found.");

            // ------------------------------------------------------------ 2
            Add(DocumentBlockKind.Heading1, "2. Design, development, production and vulnerability handling");
            Add(DocumentBlockKind.Heading2, "(a) Design and development");
            if (compliance.WorkItems.Count == 0)
            {
                Add(DocumentBlockKind.Paragraph, "The tracker has no user stories for the project yet.");
            }
            else
            {
                Add(DocumentBlockKind.Paragraph, "The product is built from these user stories:");
                foreach (var story in compliance.WorkItems.OrderBy(w => w.ExternalReference, StringComparer.Ordinal))
                {
                    Add(DocumentBlockKind.Bullet, $"{story.ExternalReference} {story.Title} ({story.State})");
                }
            }

            Missing("describe the system architecture and how the software components build on or feed into each other.");
            Add(DocumentBlockKind.Heading2, "(b) Vulnerability handling");
            Missing("describe the software bill of materials, the coordinated vulnerability disclosure policy, the contact address for reporting vulnerabilities and how updates are distributed securely. Annex I Part II in section 3 shows what the tracker knows.");
            Add(DocumentBlockKind.Heading2, "(c) Production and monitoring");
            Missing("describe the production and monitoring processes and how they are validated.");

            // ------------------------------------------------------------ 3 (R3, R4)
            Add(DocumentBlockKind.Heading1, "3. Cybersecurity risk assessment");
            Add(DocumentBlockKind.Heading2, "Risks");
            if (risks.Count == 0)
            {
                Add(DocumentBlockKind.Paragraph, "The project's risk register is empty.");
            }

            foreach (var risk in risks)
            {
                var measures = risk.Measures
                    .Select(m => m.RequirementId is { } r && requirementCodes.TryGetValue(r, out var code) ? code
                        : m.WorkItemId is { } w && stories.TryGetValue(w, out var story) ? story.ExternalReference : null)
                    .OfType<string>()
                    .ToList();
                var residual = risk.ResidualLevel is { } level ? $", residual {level}" : "";
                Add(DocumentBlockKind.Bullet,
                    $"{risk.Title}: likelihood {risk.Likelihood}, impact {risk.Impact} ({risk.Level}{residual}); "
                    + $"{risk.Treatment.ToString().ToLowerInvariant()}, {risk.DisplayStatus.ToLowerInvariant()}"
                    + (measures.Count > 0 ? $"; measures: {string.Join(", ", measures)}" : ""));
            }

            Add(DocumentBlockKind.Heading2, "How the essential requirements of Annex I apply");
            foreach (var requirement in cra.Requirements.Where(r => (r.Requirement.Group ?? "").StartsWith("Part ", StringComparison.Ordinal)))
            {
                Add(DocumentBlockKind.Bullet, $"{requirement.Requirement.Code} {requirement.Requirement.Title} – {HowMet(requirement)}");
            }

            // ------------------------------------------------------------ 4–8 (R5)
            Add(DocumentBlockKind.Heading1, "4. Information used to determine the support period");
            if (project.SupportPeriodReason is { } supportReason)
            {
                Add(DocumentBlockKind.Paragraph, $"The support period is shorter than five years because: {supportReason}");
            }

            Missing("the information taken into account to determine the support period (Art. 13(8)): expected use time, user expectations and the support of integrated components.");

            Add(DocumentBlockKind.Heading1, "5. Harmonised standards, common specifications and certification schemes applied");
            var others = compliance.Frameworks.Where(f => !IsCra(f.Framework)).ToList();
            if (others.Count > 0)
            {
                Add(DocumentBlockKind.Paragraph, "The project also follows:");
                foreach (var other in others)
                {
                    Add(DocumentBlockKind.Bullet, $"{other.Framework.Name}, version {other.Version.Number}: {other.CoverageText}");
                }
            }

            Missing("list the harmonised standards, common specifications or certification schemes applied, and which parts.");

            Add(DocumentBlockKind.Heading1, "6. Test reports");
            Missing("add the reports of the tests that verify conformity with Annex I Parts I and II.");

            Add(DocumentBlockKind.Heading1, "7. EU declaration of conformity");
            Missing("add a copy of the EU declaration of conformity when it is drawn up (Art. 28, Annex V).");

            Add(DocumentBlockKind.Heading1, "8. Software bill of materials");
            Missing("comes with SBOM support (phase 3). Only needed when a market surveillance authority asks for it.");

            var date = _clock.Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new GeneratedDocument($"technical-documentation-{Slug(project.Name)}-{date}.docx", blocks);
        }

        // R3: e.g. "met by [US-004] Sign in with Keycloak", or why it does not apply
        private static string HowMet(RequirementCoverage requirement)
        {
            if (!requirement.IsApplicable)
            {
                return $"does not apply: {requirement.NotApplicableReason}";
            }

            var evidence = requirement.Evidence.Where(e => !e.IsExpired).Select(e => e.Evidence.Title).ToList();
            var proof = evidence.Count > 0 ? $" (evidence: {string.Join(", ", evidence)})" : "";
            return requirement.IsCovered
                ? $"met by {string.Join(", ", requirement.Links.Select(l => $"{l.WorkItem.Title} ({l.WorkItem.ExternalReference})"))}{proof}"
                : $"{ToBeCompleted}: not met by any user story yet{proof}";
        }

        // A safe file name part, e.g. "acme-portal"
        private static string Slug(string name)
        {
            var chars = name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
            var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
            return slug.Length == 0 ? "project" : slug;
        }

        private static string Or(string? text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
