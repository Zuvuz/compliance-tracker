using System.Text;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Tests.Reports;

// Edge-case unit tests of US-132: signing a locked report in two roles, and the organisation's seal.
// Arrange / Act / Assert.
public class ReportSigningTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 report");

    private static (Organization Organization, User Anna, User Bo, User Dan) Members()
    {
        var organization = Organization.Create("Acme ApS", "12345678", Sector.Energy, OrganizationSize.Small, new UserIdentity("anna", "Anna", "anna@acme.test"));
        var anna = organization.Users.Single();
        var bo = organization.AddMember(new UserIdentity("bo", "Bo", "bo@acme.test"), UserRole.Developer);
        var dan = organization.AddMember(new UserIdentity("dan", "Dan", "dan@acme.test"), UserRole.Developer);
        organization.SetSignsForManagement(bo.Id, true, anna.Id);
        return (organization, anna, bo, dan);
    }

    private static (ReportSigning Signing, string Sha) Signing(Organization organization, User requestedBy)
    {
        var snapshot = ReportSnapshot.Lock(organization.Id, Guid.NewGuid(), "traceability:management:all", "Traceability", "t.pdf", "application/pdf", Pdf, requestedBy.Id, requestedBy.Role, Monday);
        return (ReportSigning.Request(snapshot, requestedBy.Id, Monday), snapshot.Sha256);
    }

    [Fact]
    public void Both_roles_by_two_people_make_the_report_signed()
    {
        // Arrange
        var (organization, anna, bo, _) = Members();
        var (signing, sha) = Signing(organization, anna);

        // Act
        signing.Sign(SignatureRole.ComplianceOfficer, anna, sha, Monday);
        signing.Sign(SignatureRole.Manager, bo, sha, Monday.AddHours(1));

        // Assert
        Assert.True(signing.IsSigned);
        Assert.Empty(signing.Missing);
        Assert.Equal(UserRole.Developer, signing.Signatures.Single(s => s.Role == SignatureRole.Manager).SignerRole);
    }

    [Fact]
    public void One_person_cannot_sign_in_two_roles()
    {
        // Arrange
        var (organization, anna, _, _) = Members();
        organization.SetSignsForManagement(anna.Id, true, anna.Id);
        var (signing, sha) = Signing(organization, anna);
        signing.Sign(SignatureRole.ComplianceOfficer, anna, sha, Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.Manager, anna, sha, Monday));
    }

    [Fact]
    public void A_role_is_signed_once()
    {
        // Arrange
        var (organization, anna, _, _) = Members();
        var carl = organization.AddMember(new UserIdentity("carl", "Carl", "carl@acme.test"), UserRole.ComplianceManager);
        var (signing, sha) = Signing(organization, anna);
        signing.Sign(SignatureRole.ComplianceOfficer, anna, sha, Monday);

        // Act / Assert
        Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.ComplianceOfficer, carl, sha, Monday));
    }

    [Fact]
    public void A_developer_is_not_a_compliance_officer_and_needs_the_flag_to_sign_for_the_management()
    {
        // Arrange
        var (organization, anna, _, dan) = Members();
        var (signing, sha) = Signing(organization, anna);

        // Act / Assert
        Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.ComplianceOfficer, dan, sha, Monday));
        Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.Manager, dan, sha, Monday));
    }

    [Fact]
    public void A_removed_member_cannot_sign()
    {
        // Arrange
        var (organization, anna, bo, _) = Members();
        var (signing, sha) = Signing(organization, anna);
        organization.RemoveMember(bo.Id, anna.Id, Monday);

        // Act / Assert
        Assert.False(ReportSigning.MaySign(bo, SignatureRole.Manager));
        Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.Manager, bo, sha, Monday));
    }

    [Fact]
    public void A_signature_is_only_for_the_locked_file()
    {
        // Arrange
        var (organization, anna, _, _) = Members();
        var (signing, _) = Signing(organization, anna);

        // Act / Assert
        Assert.Equal("The file is not the locked report", Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.ComplianceOfficer, anna, new string('0', 64), Monday)).Message);
    }

    [Fact]
    public void A_report_is_sealed_only_when_both_have_signed_and_only_once()
    {
        // Arrange
        var (organization, anna, bo, _) = Members();
        var (signing, sha) = Signing(organization, anna);
        signing.Sign(SignatureRole.ComplianceOfficer, anna, sha, Monday);

        // Act / Assert
        Assert.Contains("Manager", Assert.Throws<DomainException>(() => signing.Seal("x", "CN=Acme", "AB", Monday)).Message, StringComparison.Ordinal);
        signing.Sign(SignatureRole.Manager, bo, sha, Monday);
        signing.Seal("x", "CN=Acme", "AB", Monday);
        Assert.True(signing.IsSealed);
        Assert.Throws<DomainException>(() => signing.Seal("y", "CN=Acme", "AB", Monday));
        Assert.Throws<DomainException>(() => signing.Sign(SignatureRole.Manager, bo, sha, Monday));
    }

    [Fact]
    public void Only_an_admin_chooses_who_signs_for_the_management_and_the_same_choice_twice_changes_nothing()
    {
        // Arrange
        var (organization, anna, bo, dan) = Members();

        // Act / Assert
        Assert.Throws<DomainException>(() => organization.SetSignsForManagement(dan.Id, true, bo.Id));
        Assert.Null(organization.SetSignsForManagement(bo.Id, true, anna.Id));
        Assert.NotNull(organization.SetSignsForManagement(bo.Id, false, anna.Id));
        Assert.False(bo.SignsForManagement);
    }

    // ---------------------------------------------------------- The seal (R6)

    [Fact]
    public void An_expired_certificate_cannot_be_the_seal()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => OrganisationSeal.Create(Guid.NewGuid(), "CN=Acme", "AB", Monday.AddYears(-2), Monday, "c", "p", Guid.NewGuid(), Monday));
    }

    [Fact]
    public void A_seal_expires_at_its_end_date_and_is_not_valid_before_its_start()
    {
        // Arrange
        var seal = OrganisationSeal.Create(Guid.NewGuid(), "CN=Acme", "AB", Monday.AddDays(1), Monday.AddYears(1), "c", "p", Guid.NewGuid(), Monday);

        // Act / Assert
        Assert.NotNull(seal.WhyNotUsable(Monday));
        Assert.Null(seal.WhyNotUsable(Monday.AddDays(2)));
        Assert.NotNull(seal.WhyNotUsable(Monday.AddYears(1)));
    }

    [Fact]
    public void A_removed_seal_cannot_seal_or_be_removed_again()
    {
        // Arrange
        var seal = OrganisationSeal.Create(Guid.NewGuid(), "CN=Acme", "AB", Monday.AddDays(-1), Monday.AddYears(1), "c", "p", Guid.NewGuid(), Monday);

        // Act
        seal.Remove(Monday);

        // Assert
        Assert.Equal("The seal is removed", seal.WhyNotUsable(Monday));
        Assert.Throws<DomainException>(() => seal.Remove(Monday));
    }
}
