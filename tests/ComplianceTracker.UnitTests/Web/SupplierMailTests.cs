using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Web.Controllers;

namespace ComplianceTracker.UnitTests.Web;

// US-207 R4: the subject "Write" fills in. Every test follows Arrange / Act / Assert.
public class SupplierMailTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static (SupplierView View, SupplierContact Account, SupplierContact Security) Supplier(SupplierState state)
    {
        var supplier = ComplianceTracker.Domain.Organizations.Supplier.Create(Guid.NewGuid(), "CloudHost ApS", "Hosting", SupplierCriticality.High, null,
            new[] { "NIS2 Art. 21(2)(d)" }, Guid.NewGuid(), Monday);
        var account = supplier.AddContact("Jens Hansen", "Account manager", "jens@cloudhost.example", null, true, false);
        var security = supplier.AddContact("Mette Lund", "Security contact", "mette@cloudhost.example", null, false, true);
        return (new SupplierView(supplier, new SupplierStatus(state, ""), 0), account, security);
    }

    [Fact]
    public void Missing_or_ending_evidence_asks_another_person_for_updated_evidence()
    {
        var (missing, account, _) = Supplier(SupplierState.Missing);
        var (soon, accountSoon, _) = Supplier(SupplierState.ExpiresSoon);

        Assert.Equal("Updated evidence – Demo ApS", SuppliersViewModel.Subject(missing, account, "Demo ApS"));
        Assert.Equal("Updated evidence – Demo ApS", SuppliersViewModel.Subject(soon, accountSoon, "Demo ApS"));
        Assert.Equal("Updated evidence – Demo ApS", SuppliersViewModel.EvidenceSubject("Demo ApS"));
    }

    [Fact]
    public void A_security_contact_is_written_to_about_security_even_when_evidence_is_missing()
    {
        var (missing, _, security) = Supplier(SupplierState.Missing);

        Assert.Equal("Security – Demo ApS", SuppliersViewModel.Subject(missing, security, "Demo ApS"));
    }

    [Fact]
    public void A_security_contact_or_the_security_mailbox_is_about_security()
    {
        var (view, _, security) = Supplier(SupplierState.Ok);

        Assert.Equal("Security – Demo ApS", SuppliersViewModel.Subject(view, security, "Demo ApS"));
        Assert.Equal("Security – Demo ApS", SuppliersViewModel.Subject(view, null, "Demo ApS"));
    }

    [Fact]
    public void Another_person_gets_the_organisations_name()
    {
        var (view, account, _) = Supplier(SupplierState.Ok);

        Assert.Equal("Demo ApS", SuppliersViewModel.Subject(view, account, "Demo ApS"));
    }

    [Fact]
    public void Without_the_organisations_name_the_subject_has_no_dash()
    {
        var (view, account, security) = Supplier(SupplierState.Ok);

        Assert.Equal(("Security", "CloudHost ApS"), (SuppliersViewModel.Subject(view, security, " "), SuppliersViewModel.Subject(view, account, "")));
    }
}
