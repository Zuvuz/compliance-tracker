using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Projects
{
    // Storage for classifications (US-026, implemented in Infrastructure)
    public interface IClassificationRepository
    {
        // Every classification of the project, oldest first
        Task<IReadOnlyList<ProjectClassification>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

        Task AddAsync(ProjectClassification classification, CancellationToken cancellationToken = default);
    }

    // One class of a regulation, e.g. ("ImportantClassI", "Important, class I")
    public record RegulationClass(string Code, string Title);

    // A regulation the tracker can classify under: its classes and a short guide (R2, R6)
    public record ClassifiableRegulation(IReadOnlyList<RegulationClass> Classes, string Guide)
    {
        public string TitleOf(string code) => Classes.FirstOrDefault(c => c.Code == code)?.Title ?? code;
    }

    // US-026 R2, R6: the classes of each built-in regulation, with the articles and annexes that decide them (B50).
    // Found by the regulation's short name, like the Compliance tab's address ("cra", "ai-act"). MDR comes with US-023.
    public static class RegulationClasses
    {
        private static readonly RegulationClass NotApplicable = new(ProjectClassification.NotApplicable, "Not applicable");

        private static readonly Dictionary<string, ClassifiableRegulation> BySlug = new(StringComparer.Ordinal)
        {
            ["cra"] = new(
            [
                new("Default", "Default (not important or critical)"),
                new("ImportantClassI", "Important, class I"),
                new("ImportantClassII", "Important, class II"),
                new("Critical", "Critical"),
                NotApplicable
            ], "Art. 7 and Annex III decide whether the product is important (class I or II), Art. 8 and Annex IV whether it is "
               + "critical; otherwise it is default. Art. 2 says which products are outside the CRA, e.g. Art. 2(2): devices under "
               + "the MDR or IVDR. The class decides the conformity assessment procedure (Art. 32)."),
            ["ai-act"] = new(
            [
                new("Prohibited", "Prohibited practice"),
                new("HighRisk", "High-risk"),
                new("LimitedRisk", "Limited risk (transparency)"),
                new("MinimalRisk", "Minimal risk"),
                NotApplicable
            ], "Art. 5 lists prohibited practices. Art. 6 with Annex I (safety components of products such as medical devices) "
               + "and Annex III (areas such as employment or education) decides high-risk. Art. 50 gives transparency duties "
               + "(e.g. chatbots, generated content). Otherwise minimal risk. Not applicable if it is not an AI system (Art. 3(1)).")
        };

        public static ClassifiableRegulation? For(Framework framework)
            => framework.IsBuiltIn && BySlug.TryGetValue(FrameworkSlugs.Slug(framework.Name), out var regulation) ? regulation : null;
    }

    // One regulation of the project, with its current class (null: "Not classified") and the earlier ones (R4, R5)
    public record RegulationClassification(
        Framework Framework, ClassifiableRegulation Regulation, ProjectClassification? Current, IReadOnlyList<ProjectClassification> History);

    // US-026: classify a project under each built-in regulation it follows. A compliance decision (DecideCompliance).
    public class ClassificationService
    {
        private readonly IProjectRepository _projects;
        private readonly IFrameworkRepository _frameworks;
        private readonly IClassificationRepository _classifications;
        private readonly CurrentMember _member;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public ClassificationService(
            IProjectRepository projects, IFrameworkRepository frameworks, IClassificationRepository classifications,
            CurrentMember member, IActivityLogger log, IUnitOfWork unitOfWork, TimeProvider clock)
        {
            _projects = projects;
            _frameworks = frameworks;
            _classifications = classifications;
            _member = member;
            _log = log;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        // R1–R4, R7
        public async Task ClassifyAsync(Guid projectId, Guid frameworkId, string? classCode, string? reason, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.DecideCompliance, cancellationToken);
            var project = await _projects.GetAsync(organization.Id, projectId, cancellationToken)
                ?? throw new DomainException("Project not found");
            if (project.IsClosed)
            {
                throw new DomainException("A project at end of support cannot be changed");
            }

            var framework = project.FrameworkInUse(frameworkId) is null
                ? null
                : await _frameworks.GetAvailableAsync(organization.Id, frameworkId, cancellationToken);
            var regulation = framework is null ? null : RegulationClasses.For(framework);
            if (framework is null || regulation is null)
            {
                throw new DomainException("The project can only be classified under a built-in regulation it follows");
            }

            if (regulation.Classes.All(c => c.Code != classCode))
            {
                throw new DomainException("Choose one of the regulation's classes");
            }

            var classification = ProjectClassification.Create(project.Id, framework.Id, classCode!, reason, user.Id, user.Role,
                _clock.GetUtcNow().UtcDateTime);
            await _classifications.AddAsync(classification, cancellationToken);
            await _log.LogAsync(ActivityAction.ProjectClassified, project.Id,
                $"{FrameworkSlugs.Short(framework.Name)}: {regulation.TitleOf(classification.Class)} ({classification.Reason})", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // R5: every built-in regulation the project follows, with its current class and history; null for other organisations
        public async Task<IReadOnlyList<RegulationClassification>?> ForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization
                || await _projects.GetAsync(organization.Id, projectId, cancellationToken) is not { } project)
            {
                return null;
            }

            var all = await _classifications.ListAsync(project.Id, cancellationToken);
            var result = new List<RegulationClassification>();
            foreach (var used in project.Frameworks)
            {
                if (await _frameworks.GetAvailableAsync(organization.Id, used.FrameworkId, cancellationToken) is not { } framework
                    || RegulationClasses.For(framework) is not { } regulation)
                {
                    continue;
                }

                var history = all.Where(c => c.FrameworkId == framework.Id).OrderBy(c => c.ClassifiedAt).ToList();
                result.Add(new RegulationClassification(framework, regulation, history.LastOrDefault(), history));
            }

            return result.OrderBy(r => r.Framework.Name, StringComparer.Ordinal).ToList();
        }
    }
}
