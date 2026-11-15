using ComplianceTracker.Application.Organizations;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.UnitTests.Organizations;

// US-191: edge cases of the suppliers and their status. Arrange / Act / Assert.
public class SupplierTests
{
    private static readonly DateOnly Today = new(2026, 11, 15);
    private static readonly DateTime Monday = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    private static Supplier New(SupplierCriticality criticality, params string[] rules)
        => Supplier.Create(Guid.NewGuid(), "Hosting ApS", "Hosting", criticality, null, rules, Guid.NewGuid(), Monday);

    private static void Add(Supplier supplier, string title, DateOnly? until)
        => supplier.AddEvidence(SupplierEvidenceKind.Certificate, title, until, "c.pdf", "application/pdf", 10, "abc", Guid.NewGuid(), Monday);

    // ---------------------------------------------------------- Status (R1, R3)

    [Fact]
    public void A_critical_supplier_without_evidence_is_missing()
        => Assert.Equal(new SupplierStatus(SupplierState.Missing, "No evidence"), SupplierStatus.Of(New(SupplierCriticality.High), Today));

    [Fact]
    public void Expired_evidence_is_named_and_counts_as_missing()
    {
        var supplier = New(SupplierCriticality.Medium);
        Add(supplier, "ISO/IEC 27001 certificate", Today.AddDays(-1));

        Assert.Equal(new SupplierStatus(SupplierState.Missing, "ISO/IEC 27001 certificate expired"), SupplierStatus.Of(supplier, Today));
    }

    [Fact]
    public void Evidence_that_ends_today_is_still_valid()
    {
        var supplier = New(SupplierCriticality.High);
        Add(supplier, "Certificate", Today);

        Assert.Equal(SupplierState.ExpiresSoon, SupplierStatus.Of(supplier, Today).State);
    }

    [Fact]
    public void Evidence_without_an_end_date_never_expires()
    {
        var supplier = New(SupplierCriticality.High);
        Add(supplier, "Contract", null);

        Assert.Equal(SupplierState.Ok, SupplierStatus.Of(supplier, Today.AddYears(10)).State);
    }

    [Fact]
    public void One_piece_that_lasts_longer_keeps_the_supplier_ok()
    {
        var supplier = New(SupplierCriticality.High);
        Add(supplier, "Old", Today.AddDays(5));
        Add(supplier, "New", Today.AddYears(1));

        Assert.Equal(new SupplierStatus(SupplierState.Ok, "2 valid"), SupplierStatus.Of(supplier, Today));
    }

    [Fact]
    public void The_thirtieth_day_is_soon_and_the_thirty_first_is_not()
    {
        var soon = New(SupplierCriticality.High);
        Add(soon, "A", Today.AddDays(SupplierStatus.SoonDays));
        var later = New(SupplierCriticality.High);
        Add(later, "A", Today.AddDays(SupplierStatus.SoonDays + 1));

        Assert.Equal(SupplierState.ExpiresSoon, SupplierStatus.Of(soon, Today).State);
        Assert.Equal(SupplierState.Ok, SupplierStatus.Of(later, Today).State);
    }

    [Fact]
    public void A_supplier_with_low_criticality_needs_no_evidence()
        => Assert.Equal(SupplierState.Ok, SupplierStatus.Of(New(SupplierCriticality.Low), Today).State);

    // ---------------------------------------------------------- The supplier (R1, R2)

    [Fact]
    public void Rules_outside_the_list_are_refused_and_duplicates_kept_once()
    {
        Assert.Throws<DomainException>(() => New(SupplierCriticality.High, "Made up rule"));
        Assert.Equal(["GDPR Art. 28"], New(SupplierCriticality.High, "GDPR Art. 28", " GDPR Art. 28 ").RuleList);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_supplier_needs_a_name(string name)
        => Assert.Throws<DomainException>(() => Supplier.Create(Guid.NewGuid(), name, "", SupplierCriticality.High, null, [], Guid.NewGuid(), Monday));

    [Fact]
    public void A_supplier_needs_a_criticality()
        => Assert.Throws<DomainException>(() => Supplier.Create(Guid.NewGuid(), "A", "", null, null, [], Guid.NewGuid(), Monday));

    [Fact]
    public void A_removed_supplier_takes_no_new_evidence_and_cannot_be_removed_twice()
    {
        var supplier = New(SupplierCriticality.High);
        supplier.Remove(Monday);

        Assert.Throws<DomainException>(() => Add(supplier, "A", null));
        Assert.Throws<DomainException>(() => supplier.Remove(Monday));
    }

    [Fact]
    public void Evidence_needs_a_title_and_a_kind()
    {
        var supplier = New(SupplierCriticality.High);

        Assert.Throws<DomainException>(() => supplier.AddEvidence(SupplierEvidenceKind.Certificate, " ", null, "c.pdf", "application/pdf", 1, "a", Guid.NewGuid(), Monday));
        Assert.Throws<DomainException>(() => supplier.AddEvidence(null, "A", null, "c.pdf", "application/pdf", 1, "a", Guid.NewGuid(), Monday));
    }

    // ---------------------------------------------------------- SBOM components (R4)

    [Fact]
    public void Components_are_matched_on_the_supplier_field_whatever_the_case()
    {
        var components = new[] { Component("Acme Libraries Inc."), Component("acme libraries"), Component(null), Component("Other") };

        Assert.Equal(2, SupplierService.Delivered("Acme Libraries", components));
    }

    private static Domain.Scanning.SbomComponent Component(string? supplier)
    {
        var component = (Domain.Scanning.SbomComponent)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Domain.Scanning.SbomComponent));
        typeof(Domain.Scanning.SbomComponent).GetProperty(nameof(Domain.Scanning.SbomComponent.Supplier))!.SetValue(component, supplier);
        return component;
    }
}
