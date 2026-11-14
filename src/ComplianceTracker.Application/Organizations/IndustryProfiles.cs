using ComplianceTracker.Application.ActivityLog;
using ComplianceTracker.Application.Common;
using ComplianceTracker.Application.Frameworks;
using ComplianceTracker.Domain.ActivityLog;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // The ids of the built-in frameworks a profile can suggest (from Infrastructure, like Nis2Catalogues)
    public record IndustryFrameworks(Guid Cra, Guid AiAct);

    // How a suggestion is acted on: a built-in framework is switched on (US-006); NIS2 needs the scope check (US-142);
    // a standard is the organisation's own copy (US-143)
    public enum SuggestionAction
    {
        SwitchOn,
        ScopeCheck,
        OwnCopy
    }

    // R3: what is suggested and why
    public record FrameworkSuggestion(string Name, string Reason, SuggestionAction Action, Guid? FrameworkId = null);

    // R5: what an industry is expected to have. ComingSoon (R8) names what its own suggestions will add.
    public record IndustryProfile(Industry Industry, IReadOnlyList<FrameworkSuggestion> Frameworks, IReadOnlyList<string> ScanKinds,
        IReadOnlyList<string> Reports, string? ComingSoon = null);

    // One industry; a new industry is one new class (B105)
    public interface IIndustryProfile
    {
        Industry Industry { get; }

        IndustryProfile For(bool productsUseAi, IndustryFrameworks frameworks);
    }

    // R2: software products with digital elements
    public sealed class SoftwareProfile : IIndustryProfile
    {
        public Industry Industry => Industry.Software;

        public IndustryProfile For(bool productsUseAi, IndustryFrameworks frameworks)
            => new(Industry, Suggestions(productsUseAi, frameworks), ScanKinds, Reports);

        // Shared with the industries whose own suggestions are still coming (R8)
        public static IReadOnlyList<FrameworkSuggestion> Suggestions(bool productsUseAi, IndustryFrameworks frameworks)
        {
            var list = new List<FrameworkSuggestion>
            {
                new("CRA – Regulation (EU) 2024/2847", "You make products with digital elements that are sold in the EU", SuggestionAction.SwitchOn, frameworks.Cra)
            };
            if (productsUseAi)
            {
                list.Add(new("AI Act – Regulation (EU) 2024/1689", "Your products use AI, so the AI Act may apply (high-risk, transparency)", SuggestionAction.SwitchOn, frameworks.AiAct));
            }

            list.Add(new("NIS2 – Directive (EU) 2022/2555", "Do the scope check to see whether NIS2 covers the organisation", SuggestionAction.ScopeCheck));
            list.Add(new("ISO/IEC 27001:2022", "The usual standard for information security; add your own copy", SuggestionAction.OwnCopy));
            return list;
        }

        public static readonly IReadOnlyList<string> ScanKinds =
            ["SAST (code scanning)", "Dependency scan with an SBOM", "Secret scanning", "DAST for web applications"];

        public static readonly IReadOnlyList<string> Reports =
            ["CRA technical documentation", "Statement of Applicability (ISO/IEC 27001)", "Compliance status report"];
    }

    // R8: medical devices with software and hardware; MDR, ISO 14971 and IEC 62304 come with their own stories
    public sealed class MedtechProfile : IIndustryProfile
    {
        public Industry Industry => Industry.Medtech;

        public IndustryProfile For(bool productsUseAi, IndustryFrameworks frameworks)
            => new(Industry, SoftwareProfile.Suggestions(productsUseAi, frameworks), SoftwareProfile.ScanKinds, SoftwareProfile.Reports,
                "MDR, ISO 13485, ISO 14971, IEC 62304, IEC 62366-1, IEC 60601-1 and IEC 81001-5-1, for software and hardware");
    }

    // R8: financial services; DORA comes with US-157
    public sealed class FintechProfile : IIndustryProfile
    {
        public Industry Industry => Industry.Fintech;

        public IndustryProfile For(bool productsUseAi, IndustryFrameworks frameworks)
            => new(Industry, SoftwareProfile.Suggestions(productsUseAi, frameworks), SoftwareProfile.ScanKinds, SoftwareProfile.Reports,
                "DORA, the ICT third-party register, incident reporting, PSD2 and PCI DSS");
    }

    // R1: "Other" gives no suggestions
    public sealed class OtherProfile : IIndustryProfile
    {
        public Industry Industry => Industry.Other;

        public IndustryProfile For(bool productsUseAi, IndustryFrameworks frameworks) => new(Industry, [], [], []);
    }

    // The factory: picks the industry's profile (US-083's idea of one class per industry)
    public class IndustryProfiles
    {
        private readonly IReadOnlyDictionary<Industry, IIndustryProfile> _profiles;
        private readonly IndustryFrameworks _frameworks;

        public IndustryProfiles(IEnumerable<IIndustryProfile> profiles, IndustryFrameworks frameworks)
        {
            _profiles = profiles.ToDictionary(p => p.Industry);
            _frameworks = frameworks;
        }

        public IndustryProfile For(Industry industry, bool productsUseAi)
            => _profiles.TryGetValue(industry, out var profile)
                ? profile.For(productsUseAi, _frameworks)
                : throw new DomainException("Choose an industry");

        public static IReadOnlyList<IIndustryProfile> All => [new SoftwareProfile(), new MedtechProfile(), new FintechProfile(), new OtherProfile()];
    }

    // The industry section on Settings: the choice, what it suggests and which built-ins are on already
    public record IndustryView(Industry? Industry, bool ProductsUseAi, IndustryProfile? Profile, IReadOnlySet<Guid> SwitchedOn);

    // US-080: an Admin chooses the industry; the profile only suggests (R4)
    public class IndustryProfileService
    {
        private readonly CurrentMember _member;
        private readonly IndustryProfiles _profiles;
        private readonly IFrameworkRepository _frameworks;
        private readonly IActivityLogger _log;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Projects.IProjectRepository _projects;
        private readonly Ai.NeedsActionReassessments _reassessments; // US-156

        public IndustryProfileService(CurrentMember member, IndustryProfiles profiles, IFrameworkRepository frameworks, IActivityLogger log, IUnitOfWork unitOfWork,
            Projects.IProjectRepository projects, Ai.NeedsActionReassessments reassessments)
        {
            _projects = projects;
            _reassessments = reassessments;
            _member = member;
            _profiles = profiles;
            _frameworks = frameworks;
            _log = log;
            _unitOfWork = unitOfWork;
        }

        public async Task<IndustryView?> ViewAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is not { } organization)
            {
                return null;
            }

            var profile = organization.Industry is { } industry ? _profiles.For(industry, organization.ProductsUseAi) : null;
            var switchedOn = new HashSet<Guid>();
            foreach (var suggestion in profile?.Frameworks.Where(f => f.FrameworkId is not null) ?? [])
            {
                var framework = await _frameworks.GetBuiltInAsync(suggestion.FrameworkId!.Value, cancellationToken);
                foreach (var version in framework?.Versions ?? [])
                {
                    if (await _frameworks.GetActiveSelectionAsync(organization.Id, version.Id, cancellationToken) is not null)
                    {
                        switchedOn.Add(suggestion.FrameworkId.Value);
                    }
                }
            }

            return new IndustryView(organization.Industry, organization.ProductsUseAi, profile, switchedOn);
        }

        // R1, R6: nothing is switched on or off here
        public async Task ChooseAsync(Industry? industry, bool productsUseAi, CancellationToken cancellationToken = default)
        {
            var (organization, user) = await _member.RequireAsync(Permission.ManageMembers, cancellationToken);
            if (organization.SetIndustry(industry, productsUseAi, user.Id))
            {
                await _log.LogAsync(ActivityAction.IndustryProfileChosen, organization.Id,
                    $"{organization.Industry}{(productsUseAi ? ", products use AI" : "")}", cancellationToken);

                // US-156 R1: the industry is part of what the AI reads about every product
                foreach (var project in await _projects.ListAsync(organization.Id, cancellationToken))
                {
                    await _reassessments.RequestAsync(project, "industry profile changed", cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
