using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Domain.Tests.Organizations;

// US-207: a supplier's company details and contact persons. Every test follows Arrange / Act / Assert.
public class SupplierContactTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Supplier Supplier(SupplierCriticality criticality = SupplierCriticality.High, string? contact = null)
        => ComplianceTracker.Domain.Organizations.Supplier.Create(Guid.NewGuid(), "CloudHost ApS", "Hosting", criticality, contact, new[] { "NIS2 Art. 21(2)(d)" }, Anna, Monday);

    // ---------------------------------------------------------- Contact persons (R2)

    [Fact]
    public void The_first_contact_person_is_primary()
    {
        var supplier = Supplier();

        var jens = supplier.AddContact("Jens Hansen", "Account manager", "jens@cloudhost.example", null, primary: false, securityContact: false);

        Assert.True(jens.IsPrimary);
        Assert.Same(jens, supplier.PrimaryContact);
    }

    [Fact]
    public void Two_primary_contacts_leave_only_the_newest_primary()
    {
        var supplier = Supplier();
        var jens = supplier.AddContact("Jens Hansen", null, null, null, primary: true, securityContact: false);

        var mette = supplier.AddContact("Mette Lund", null, null, null, primary: true, securityContact: true);

        Assert.Equal((false, true), (jens.IsPrimary, mette.IsPrimary));
    }

    [Fact]
    public void A_second_person_that_is_not_primary_keeps_the_first_as_primary()
    {
        var supplier = Supplier();
        var jens = supplier.AddContact("Jens Hansen", null, null, null, primary: false, securityContact: false);

        supplier.AddContact("Mette Lund", null, null, null, primary: false, securityContact: false);

        Assert.Same(jens, supplier.PrimaryContact);
    }

    [Fact]
    public void Removing_the_primary_makes_the_next_person_primary()
    {
        var supplier = Supplier();
        var jens = supplier.AddContact("Jens Hansen", null, null, null, primary: true, securityContact: false);
        var mette = supplier.AddContact("Mette Lund", null, null, null, primary: false, securityContact: false);

        supplier.RemoveContact(jens.Id);

        Assert.True(mette.IsPrimary);
        Assert.Single(supplier.Contacts);
    }

    [Fact]
    public void Removing_the_last_contact_person_shows_the_old_note_again()
    {
        var supplier = Supplier(contact: "jens@cloudhost.example, phone 12345678");
        var jens = supplier.AddContact("Jens Hansen", null, null, null, primary: true, securityContact: false);
        Assert.Null(supplier.ContactNote);

        supplier.RemoveContact(jens.Id);

        Assert.Equal(("jens@cloudhost.example, phone 12345678", null), (supplier.ContactNote, supplier.PrimaryContact));
    }

    [Fact]
    public void A_contact_person_needs_a_name()
        => Assert.Equal("Give the contact person's name",
            Assert.Throws<DomainException>(() => Supplier().AddContact("  ", null, null, null, false, false)).Message);

    [Fact]
    public void A_contact_person_that_is_not_the_suppliers_cannot_be_changed_or_removed()
    {
        var supplier = Supplier();

        Assert.Equal("Contact person not found", Assert.Throws<DomainException>(() => supplier.RemoveContact(Guid.NewGuid())).Message);
        Assert.Throws<DomainException>(() => supplier.ChangeContact(Guid.NewGuid(), "x", null, null, null, false, false));
    }

    [Fact]
    public void A_removed_supplier_gets_no_contact_persons()
    {
        var supplier = Supplier();
        supplier.Remove(Monday);

        Assert.Equal("The supplier has been removed", Assert.Throws<DomainException>(() => supplier.AddContact("Jens", null, null, null, false, false)).Message);
    }

    [Fact]
    public void Changing_a_person_to_primary_takes_over()
    {
        var supplier = Supplier();
        var jens = supplier.AddContact("Jens Hansen", null, null, null, primary: true, securityContact: false);
        var mette = supplier.AddContact("Mette Lund", null, null, null, primary: false, securityContact: false);

        supplier.ChangeContact(mette.Id, "Mette Lund", "Security contact", "mette@cloudhost.example", null, primary: true, securityContact: true);

        Assert.Equal((false, true, true), (jens.IsPrimary, mette.IsPrimary, mette.IsSecurityContact));
    }

    // ---------------------------------------------------------- E-mail and phone (R3)

    [Theory]
    [InlineData("jens at cloudhost")]
    [InlineData("jens@cloudhost")]
    [InlineData("@cloudhost.example")]
    [InlineData("jens@@cloudhost.example")]
    [InlineData("jens hansen@cloudhost.example")]
    public void An_e_mail_address_that_is_not_valid_is_refused(string email)
        => Assert.StartsWith("Not a valid e-mail address: ", Assert.Throws<DomainException>(() => SupplierContacts.CleanEmail(email)).Message);

    [Fact]
    public void An_e_mail_address_is_trimmed_and_an_empty_one_is_none()
    {
        Assert.Equal("jens@cloudhost.example", SupplierContacts.CleanEmail("  jens@cloudhost.example "));
        Assert.Null(SupplierContacts.CleanEmail("   "));
    }

    [Theory]
    [InlineData("+45 12 34 56 78", "+45 12 34 56 78")]
    [InlineData("12345678", "12345678")]
    [InlineData("  +45   12  34 56 78 ", "+45 12 34 56 78")]
    [InlineData("+1 (555) 123-4567", "+1 (555) 123-4567")]
    public void A_phone_number_may_have_spaces_a_plus_and_a_country_code(string phone, string expected)
        => Assert.Equal(expected, SupplierContacts.CleanPhone(phone));

    [Theory]
    [InlineData("12 34")]
    [InlineData("phone 12345678")]
    [InlineData("45+12345678")]
    [InlineData("++4512345678")]
    public void A_phone_number_that_is_not_valid_is_refused(string phone)
        => Assert.Throws<DomainException>(() => SupplierContacts.CleanPhone(phone));

    // ---------------------------------------------------------- Company details (R1)

    [Fact]
    public void Company_details_say_what_changed_without_the_values()
    {
        var supplier = Supplier();

        var changed = supplier.ChangeCompany("12345678", null, "Denmark", "cloudhost.example", "security@cloudhost.example");

        Assert.Equal(["registration number", "country", "website", "security e-mail"], changed);
        Assert.Equal("https://cloudhost.example/", supplier.Website);
    }

    [Fact]
    public void The_same_company_details_again_change_nothing()
    {
        var supplier = Supplier();
        supplier.ChangeCompany("12345678", "Vej 1", "Denmark", null, null);

        Assert.Empty(supplier.ChangeCompany(" 12345678 ", "Vej 1", "Denmark", "", ""));
    }

    [Theory]
    [InlineData("ftp://cloudhost.example")]
    [InlineData("not a website")]
    [InlineData("https://localhost")]
    public void A_website_that_is_not_valid_is_refused(string website)
        => Assert.Throws<DomainException>(() => Supplier().ChangeCompany(null, null, null, website, null));

    [Fact]
    public void A_supplier_outside_the_EU_with_a_VAT_number_is_fine()
    {
        var supplier = Supplier();

        supplier.ChangeCompany("US 12-3456789", "1 Main Street, Austin, TX", "United States", null, null);

        Assert.Equal(("US 12-3456789", "United States"), (supplier.RegistrationNumber, supplier.Country));
    }

    [Fact]
    public void A_registration_number_that_is_too_long_is_refused()
        => Assert.Throws<DomainException>(() => Supplier().ChangeCompany(new string('1', ComplianceTracker.Domain.Organizations.Supplier.MaxRegistrationLength + 1), null, null, null, null));

    // ---------------------------------------------------------- No security contact (R5)

    [Fact]
    public void A_critical_supplier_without_a_security_contact_is_marked()
    {
        var supplier = Supplier();
        supplier.AddContact("Jens Hansen", "Account manager", "jens@cloudhost.example", null, true, false);

        Assert.True(supplier.LacksSecurityContact);
    }

    [Fact]
    public void A_security_contact_with_an_e_mail_or_a_security_e_mail_is_enough()
    {
        var withPerson = Supplier();
        withPerson.AddContact("Mette Lund", null, "mette@cloudhost.example", null, true, true);
        var withMailbox = Supplier();
        withMailbox.ChangeCompany(null, null, null, null, "security@cloudhost.example");

        Assert.Equal((false, false), (withPerson.LacksSecurityContact, withMailbox.LacksSecurityContact));
    }

    [Fact]
    public void A_security_contact_without_an_e_mail_cannot_be_written_to()
    {
        var supplier = Supplier();
        supplier.AddContact("Mette Lund", null, null, "+45 12 34 56 78", true, true);

        Assert.True(supplier.LacksSecurityContact);
    }

    [Theory]
    [InlineData(SupplierCriticality.Medium)]
    [InlineData(SupplierCriticality.Low)]
    public void Only_a_critical_supplier_is_marked(SupplierCriticality criticality)
        => Assert.False(Supplier(criticality).LacksSecurityContact);

    // ---------------------------------------------------------- Write (R4) and the log (R6)

    [Fact]
    public void Write_opens_the_e_mail_program_with_the_subject()
        => Assert.Equal("mailto:mette@cloudhost.example?subject=Security%20%E2%80%93%20Demo%20ApS",
            SupplierContacts.MailTo("mette@cloudhost.example", "Security – Demo ApS"));

    [Fact]
    public void The_log_name_has_no_e_mail_or_phone()
    {
        var mette = Supplier().AddContact("Mette Lund", "Security contact", "mette@cloudhost.example", "+45 12 34 56 78", true, true);

        Assert.Equal("Mette Lund (Security contact)", mette.LogName);
    }
}
