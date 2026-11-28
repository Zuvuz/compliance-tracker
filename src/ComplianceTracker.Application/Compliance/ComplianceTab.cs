using ComplianceTracker.Application.Ai;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Application.Projects.Tabs;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.Application.Compliance
{
    // US-232: the Compliance tab's sub-tab (US-041: a framework's slug; US-144 R13: "all"), already read from the address
    public record ComplianceTabRequest(string? Sub = null);

    // US-232: what the Compliance tab's list shows. Available: every framework, for "Not used by this project" (US-144 R15).
    public record ComplianceTabDto(
        ProjectCompliance Compliance,
        IReadOnlyList<Framework> Available,
        NeedsActionReassessment? Reassessment,
        DateOnly Today);

    // US-232: a requirement's own page (US-153) under the Compliance tab, e.g. .../compliance/cra/I.2(d)
    public record RequirementPageRequest(string? Sub, string Requirement);

    // US-232: the requirement with its framework, the project's evidence to pick from (US-153) and its open risks (US-124)
    public record RequirementPageDto(
        ProjectCompliance Compliance,
        FrameworkCompliance Framework,
        RequirementCoverage Row,
        IReadOnlyList<EvidenceRow> Evidence,
        IReadOnlyList<Risk> OpenRisks);

    // US-232: the sub-tabs and the requirement an address points at. Pure, so the edge cases are tested on their own.
    public static class ComplianceAddresses
    {
        // US-144 R13: the sub-tab with every framework's requirements
        public const string AllSub = "all";

        // US-153: a requirement code is short; anything longer is not one
        public const int MaxCodeLength = 100;

        // No sub-tab, "all" when there is more than one framework, or a framework of the project's
        public static bool IsKnownSub(ProjectCompliance compliance, string? sub)
            => sub is null
               || (sub == AllSub && compliance.Frameworks.Count > 1)
               || compliance.Frameworks.Any(f => FrameworkSlugs.Slug(f.Framework.Name) == sub);

        // US-041 R3: the sub-tab's framework, or the first one
        public static FrameworkCompliance? Active(ProjectCompliance compliance, string? sub)
            => compliance.Frameworks.FirstOrDefault(f => FrameworkSlugs.Slug(f.Framework.Name) == sub)
               ?? compliance.Frameworks.FirstOrDefault();

        // US-153 R4: the code a form sent back ("req"). Blank or too long: none.
        public static string? Clean(string? code)
        {
            var trimmed = code?.Trim();
            return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxCodeLength ? null : trimmed;
        }

        // US-153 R3: the requirement the address points at, in the sub-tab's framework (any framework on "all"), whatever
        // the case of the code. Null when the code or the framework is unknown, so the page is "not found".
        public static (FrameworkCompliance Framework, RequirementCoverage Row)? Find(ProjectCompliance? compliance, string? sub, string? code)
        {
            var wanted = Clean(code);
            if (compliance is null || wanted is null)
            {
                return null;
            }

            var frameworks = sub is null || sub == AllSub
                ? compliance.Frameworks
                : compliance.Frameworks.Where(f => FrameworkSlugs.Slug(f.Framework.Name) == sub).ToList();
            foreach (var framework in frameworks)
            {
                var row = framework.Requirements.FirstOrDefault(r => string.Equals(r.Requirement.Code, wanted, StringComparison.OrdinalIgnoreCase));
                if (row is not null)
                {
                    return (framework, row);
                }
            }

            return null;
        }
    }

    public class ComplianceTabLoader : IProjectTabLoader<ComplianceTabRequest, ComplianceTabDto>
    {
        private readonly ComplianceQueries _compliance;
        private readonly FrameworkQueries _frameworks;
        private readonly INeedsActionReassessmentRepository _reassessments;
        private readonly TimeProvider _clock;

        public ComplianceTabLoader(
            ComplianceQueries compliance, FrameworkQueries frameworks, INeedsActionReassessmentRepository reassessments, TimeProvider clock)
        {
            _compliance = compliance;
            _frameworks = frameworks;
            _reassessments = reassessments;
            _clock = clock;
        }

        // Null for another organisation's project, and for a sub-tab the project does not have
        public async Task<ComplianceTabDto?> LoadAsync(Guid projectId, ComplianceTabRequest request, CancellationToken cancellationToken = default)
        {
            if (await _compliance.ForProjectAsync(projectId, cancellationToken) is not { } compliance
                || !ComplianceAddresses.IsKnownSub(compliance, request.Sub))
            {
                return null;
            }

            return new ComplianceTabDto(
                compliance,
                await _frameworks.ListAsync(cancellationToken),
                await _reassessments.LatestAsync(projectId, cancellationToken), // US-156
                _clock.Today());
        }
    }

    public class RequirementPageLoader : IProjectTabLoader<RequirementPageRequest, RequirementPageDto>
    {
        private readonly ComplianceQueries _compliance;
        private readonly EvidenceQueries _evidence;
        private readonly Risks.RiskQueries _risks;

        public RequirementPageLoader(ComplianceQueries compliance, EvidenceQueries evidence, Risks.RiskQueries risks)
        {
            _compliance = compliance;
            _evidence = evidence;
            _risks = risks;
        }

        // Null for another organisation's project, an unknown sub-tab and a requirement the sub-tab does not have
        public async Task<RequirementPageDto?> LoadAsync(Guid projectId, RequirementPageRequest request, CancellationToken cancellationToken = default)
        {
            if (await _compliance.ForProjectAsync(projectId, cancellationToken) is not { } compliance
                || !ComplianceAddresses.IsKnownSub(compliance, request.Sub)
                || ComplianceAddresses.Find(compliance, request.Sub, request.Requirement) is not { } found)
            {
                return null;
            }

            var (framework, row) = found;
            var evidence = await _evidence.ForProjectAsync(projectId, compliance, cancellationToken);
            var risks = await _risks.ForProjectAsync(projectId, cancellationToken) ?? [];
            return new RequirementPageDto(
                compliance,
                framework,
                row,
                evidence?.Evidence ?? [],
                risks.Where(r => r.Status != RiskStatus.Closed).ToList());
        }
    }
}
