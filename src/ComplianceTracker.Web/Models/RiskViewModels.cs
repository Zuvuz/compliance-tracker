using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Web.Filters;
using System.ComponentModel.DataAnnotations;

namespace ComplianceTracker.Web.Models
{
    // The fields of the add/edit risk form (US-042 R1, R3). The domain checks the values.
    // IFormModel: on a broken rule, DomainExceptionFilter shows the form again with what was typed.
    public class RiskFormModel : IFormModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = "";

        // Empty when adding
        public Guid? RiskId { get; set; }

        // US-092: the version the edit form was opened with
        public uint? Version { get; set; }

        [Display(Name = "Title")]
        public string? Title { get; set; }

        [Display(Name = "Threat and what it affects")]
        public string? Description { get; set; }

        [Display(Name = "Likelihood (1–5)")]
        public int Likelihood { get; set; } = 3;

        [Display(Name = "Impact (1–5)")]
        public int Impact { get; set; } = 3;

        [Display(Name = "Owner")]
        public Guid OwnerId { get; set; }

        [Display(Name = "Treatment")]
        public RiskTreatment Treatment { get; set; } = RiskTreatment.Reduce;

        [Display(Name = "Residual likelihood (optional)")]
        public int? ResidualLikelihood { get; set; }

        [Display(Name = "Residual impact (optional)")]
        public int? ResidualImpact { get; set; }

        public static RiskFormModel From(Risk risk, string projectName) => new()
        {
            ProjectId = risk.ProjectId,
            ProjectName = projectName,
            RiskId = risk.Id,
            Version = risk.Version,
            Title = risk.Title,
            Description = risk.Description,
            Likelihood = risk.Likelihood,
            Impact = risk.Impact,
            OwnerId = risk.OwnerId,
            Treatment = risk.Treatment,
            ResidualLikelihood = risk.ResidualLikelihood,
            ResidualImpact = risk.ResidualImpact
        };
    }

    // The risk's own page: the risk, its project, names, and what can be linked as measures (US-042 R5)
    public record RiskPageViewModel(
        Risk Risk,
        Domain.Projects.Project Project,
        Domain.Organizations.Organization Organization,
        Application.Compliance.ProjectCompliance? Compliance,
        DateTime Now)
    {
        public string NameOf(Guid? userId)
            => Organization.Users.FirstOrDefault(u => u.Id == userId)?.Name ?? "Unknown";

        // US-090 R5: a removed owner is shown as such until a new owner is chosen
        public string OwnerOf(Guid userId)
            => OwnerName(Organization, userId);

        public static string OwnerName(Domain.Organizations.Organization? organization, Guid userId)
            => organization?.Users.FirstOrDefault(u => u.Id == userId) is { } owner
                ? owner.IsActive ? owner.Name : $"Owner removed ({owner.Name})"
                : "Unknown";

        // US-111 R2: the project's evidence, to link to an action
        public IReadOnlyList<Domain.Mappings.Evidence> ProjectEvidence { get; init; } = [];

        public DateOnly Today => DateOnly.FromDateTime(Now);

        // US-124 R1: the thread from this risk to requirements and evidence
        public Application.Risks.RiskTrace? Trace => Compliance is null ? null : Application.Risks.RiskTraces.For(Risk, Compliance);

        public Domain.WorkItems.WorkItem? WorkItemOf(Guid? workItemId)
            => Compliance?.WorkItems.FirstOrDefault(w => w.Id == workItemId);

        public string EvidenceTitle(Guid? evidenceId)
            => ProjectEvidence.FirstOrDefault(e => e.Id == evidenceId)?.Title ?? "(evidence removed)";

        public static string StatusText(RiskActionStatus status) => status switch
        {
            RiskActionStatus.InProgress => "In progress",
            _ => status.ToString()
        };

        public string MeasureName(RiskMeasure measure)
            => measure.RequirementId is { } requirementId
                ? Compliance?.Frameworks.SelectMany(f => f.Requirements).FirstOrDefault(r => r.Requirement.Id == requirementId) is { } row
                    ? $"{row.Requirement.Code} {row.Requirement.Title}"
                    : "(requirement no longer followed)"
                : Compliance?.WorkItems.FirstOrDefault(w => w.Id == measure.WorkItemId)?.Title ?? "(user story removed)";
    }
}
