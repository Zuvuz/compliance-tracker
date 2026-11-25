using ComplianceTracker.Application.Isms;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Mappings;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.UnitTests.Isms;

// US-213 R6: the share of policies read, a measure for the objectives (US-209). Every test follows Arrange / Act / Assert.
public class PolicyShareTests
{
    private static readonly DateTime Monday = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

    private static (Organization Acme, IReadOnlyList<User> Members) Acme()
    {
        var acme = Organization.Create("Acme ApS", "12345678", Sector.DigitalInfrastructure, OrganizationSize.Small,
            new UserIdentity("anna-sub", "Anna", "anna@acme.test"));
        acme.AddMember(new UserIdentity("bo-sub", "Bo", "bo@acme.test"), UserRole.Developer);
        return (acme, acme.Members);
    }

    private static OrganisationDocument Policy(Organization acme, string title, string version)
        => OrganisationDocument.Create(acme.Id, OrganisationDocumentKind.Policy, title, acme.Members[0].Id, version,
            EvidenceFile.Create("policy.pdf", TestPdf()), null, acme.Members[0].Id, Monday);

    private static byte[] TestPdf() => System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");

    [Fact]
    public void Each_member_and_policy_counts_once_and_only_the_current_version()
    {
        var (acme, members) = Acme();
        var access = Policy(acme, "Access control policy", "4");
        var backup = Policy(acme, "Backup policy", "1");
        var anna = members[0].Id;
        var bo = members[1].Id;

        var share = CompetenceService.ShareRead([access, backup], members,
        [
            PolicyAcknowledgement.Confirm(acme.Id, access.Id, anna, "4", Monday),
            PolicyAcknowledgement.Confirm(acme.Id, access.Id, bo, "3", Monday),
            PolicyAcknowledgement.Confirm(acme.Id, backup.Id, bo, "1", Monday)
        ]);

        Assert.Equal(50, share);
    }

    [Fact]
    public void Without_policies_there_is_nothing_to_measure()
    {
        var (_, members) = Acme();

        Assert.Null(CompetenceService.ShareRead([], members, []));
    }

    [Fact]
    public void Policies_read_is_a_tracker_measure_in_per_cent()
        => Assert.Equal("%", SecurityObjective.UnitOf(TrackerMeasure.PoliciesRead, null));
}
