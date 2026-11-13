using ComplianceTracker.Application.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Organizations
{
    // US-096 R1: the first steps from an empty tracker to a requirement that is covered and proven
    public enum GetStartedStep
    {
        CreateOrganisation,
        ChooseIndustry, // US-080
        SwitchOnRegulation,
        CreateProject,
        AddFrameworks,
        GetWorkItems,
        LinkRequirements,
        AddEvidence
    }

    // What already exists in the organisation, read in one go (implemented in Infrastructure)
    public record GetStartedStatus(bool RegulationOn, Guid? FirstProjectId, bool HasProjectFramework, bool HasWorkItems, bool HasLinks, bool HasEvidence);

    public interface IGetStartedReadModel
    {
        Task<GetStartedStatus> StatusAsync(Guid organizationId, CancellationToken cancellationToken = default);
    }

    // One step: what it is, whether it is done, and the permission it takes to do it (R5)
    public record GetStartedItem(GetStartedStep Step, string Title, bool Done, Permission? Permission);

    // R4: shown while a step is to do and the member has not hidden it
    public record GetStartedChecklist(IReadOnlyList<GetStartedItem> Items, bool Hidden, Guid? FirstProjectId)
    {
        public bool AllDone => Items.All(i => i.Done);
        public bool Show => !Hidden && !AllDone;
    }

    // US-096: the "Get started" card. R3: each step is done by itself when the data exists; nothing is ticked by hand.
    public class GetStartedService
    {
        private readonly CurrentMember _member;
        private readonly IGetStartedReadModel _status;
        private readonly IUnitOfWork _unitOfWork;

        public GetStartedService(CurrentMember member, IGetStartedReadModel status, IUnitOfWork unitOfWork)
        {
            _member = member;
            _status = status;
            _unitOfWork = unitOfWork;
        }

        // The signed-in member's checklist, or null without an organisation
        public async Task<GetStartedChecklist?> ForMeAsync(CancellationToken cancellationToken = default)
        {
            if (await _member.FindOrganizationAsync(cancellationToken) is null)
            {
                return null;
            }

            var (organization, user) = await _member.RequireAsync(cancellationToken);
            var status = await _status.StatusAsync(organization.Id, cancellationToken);
            IReadOnlyList<GetStartedItem> items =
            [
                new(GetStartedStep.CreateOrganisation, "Create the organisation", true, null),
                new(GetStartedStep.ChooseIndustry, "Choose your industry", organization.Industry is not null, Permission.ManageMembers),
                new(GetStartedStep.SwitchOnRegulation, "Switch on a regulation", status.RegulationOn, Permission.ManageFrameworks),
                new(GetStartedStep.CreateProject, "Create a project", status.FirstProjectId is not null, Permission.EditProjects),
                new(GetStartedStep.AddFrameworks, "Add the project's frameworks", status.HasProjectFramework, Permission.EditProjects),
                new(GetStartedStep.GetWorkItems, "Get work items from GitHub or a requirement specification", status.HasWorkItems, Permission.EditProjects),
                new(GetStartedStep.LinkRequirements, "Let the AI suggest mappings, or link by hand", status.HasLinks, Permission.LinkWork),
                new(GetStartedStep.AddEvidence, "Add evidence to a requirement", status.HasEvidence, Permission.LinkWork)
            ];

            return new GetStartedChecklist(items, user.HidesGetStarted, status.FirstProjectId);
        }

        // R4: the card is hidden for this member only, and can be shown again
        public async Task SetHiddenAsync(bool hidden, CancellationToken cancellationToken = default)
        {
            var (_, user) = await _member.RequireAsync(cancellationToken);
            user.SetGetStartedHidden(hidden);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
