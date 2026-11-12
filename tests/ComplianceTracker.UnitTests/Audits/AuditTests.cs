using ComplianceTracker.Application.Audits;
using ComplianceTracker.Domain.Audits;
using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.UnitTests.Audits;

// Unit tests of audits, findings and certificates (US-134). Every test follows Arrange / Act / Assert.
public class AuditTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Portal = Guid.NewGuid();

    private static Audit NewAudit(DateOnly? heldOn = null, string? scope = "ISO/IEC 27001:2022", string? auditor = "Lead auditor",
        string? company = "Bureau Veritas", Guid? project = null, Guid? corrects = null)
        => Audit.Record(project ?? Portal, AuditKind.External, scope, heldOn ?? new DateOnly(2026, 10, 12), auditor, company, AuditResult.Passed,
            "report.pdf", new string('a', 64), Anna, Now, Today, corrects);

    // ---------------------------------------------------------- Recording (R1, R4)

    [Fact]
    public void An_audit_in_the_future_is_refused_but_today_is_fine()
    {
        Assert.Throws<DomainException>(() => NewAudit(Today.AddDays(1)));
        Assert.Equal(Today, NewAudit(Today).HeldOn);
    }

    [Theory]
    [InlineData(null, "Lead auditor", "Give the scope of the audit, e.g. ISO/IEC 27001:2022")]
    [InlineData("ISO/IEC 27001:2022", "  ", "Give the auditor's name")]
    public void Scope_and_auditor_are_needed(string? scope, string? auditor, string message)
        => Assert.Equal(message, Assert.Throws<DomainException>(() => NewAudit(scope: scope, auditor: auditor)).Message);

    [Fact]
    public void An_unknown_kind_or_result_is_refused()
        => Assert.Throws<DomainException>(() => Audit.Record(Portal, (AuditKind)9, "ISO", Today, "A", null, AuditResult.Passed, "r.pdf", "x", Anna, Now, Today));

    [Fact]
    public void Long_texts_are_cut_and_blanks_are_left_out()
    {
        var audit = NewAudit(auditor: new string('x', 300), company: " ");

        Assert.Equal(Audit.MaxTextLength, audit.AuditorName.Length);
        Assert.Null(audit.AuditorCompany);
    }

    // ---------------------------------------------------------- The certificate (R3)

    [Fact]
    public void A_certificate_must_end_after_it_starts_and_is_given_once()
    {
        var audit = NewAudit();
        Assert.Throws<DomainException>(() => audit.GiveCertificate("DK-1234", null, Today, Today));

        audit.GiveCertificate("DK-1234", null, Today, Today.AddYears(3));

        Assert.Equal("Bureau Veritas", audit.CertificateIssuer); // the company when no issuer is given
        Assert.Throws<DomainException>(() => audit.GiveCertificate("DK-9", null, Today, Today.AddYears(1)));
    }

    [Theory]
    [InlineData(91, CertificateState.Valid)]
    [InlineData(90, CertificateState.Valid)]
    [InlineData(89, CertificateState.ExpiringSoon)]
    [InlineData(0, CertificateState.ExpiringSoon)]
    [InlineData(-1, CertificateState.Expired)]
    public void The_certificate_needs_attention_within_90_days_and_expires_after_its_last_day(int daysLeft, CertificateState expected)
    {
        var audit = NewAudit();
        audit.GiveCertificate("DK-1234", null, new DateOnly(2026, 1, 1), Today.AddDays(daysLeft));

        Assert.Equal(expected, audit.CertificateOn(Today));
    }

    [Fact]
    public void The_badge_names_the_standard_without_its_year()
    {
        var audit = NewAudit();
        audit.GiveCertificate("DK-1234", null, Today.AddYears(-1), new DateOnly(2027, 6, 30));

        Assert.Equal("ISO/IEC 27001 certified until 30-06-2027", audit.CertificateBadge(Today));
        Assert.Equal("ISO/IEC 27001 certificate expired 30-06-2027", audit.CertificateBadge(new DateOnly(2027, 7, 1)));
        Assert.Null(NewAudit().CertificateBadge(Today));
    }

    // ---------------------------------------------------------- Findings (R2)

    [Fact]
    public void A_finding_needs_a_title_and_a_deadline_after_the_audit()
    {
        var audit = NewAudit();

        Assert.Throws<DomainException>(() => audit.AddFinding(" ", Anna, Today));
        Assert.Equal("A finding's deadline cannot be before the audit",
            Assert.Throws<DomainException>(() => audit.AddFinding("Access reviews", Anna, audit.HeldOn.AddDays(-1))).Message);
    }

    [Fact]
    public void A_finding_is_closed_once_with_a_reason()
    {
        var finding = NewAudit().AddFinding("Access reviews", Anna, Today.AddDays(30));

        Assert.Equal("Give a reason for closing the finding", Assert.Throws<DomainException>(() => finding.Close("  ", null, Anna, Now)).Message);
        finding.Close(" Reviews are monthly now ", null, Anna, Now);

        Assert.Equal(("Reviews are monthly now", false), (finding.CloseReason, finding.IsOpen));
        Assert.Throws<DomainException>(() => finding.Close("again", null, Anna, Now));
    }

    [Fact]
    public void An_open_finding_past_its_deadline_is_overdue_and_a_closed_one_never_is()
    {
        var finding = NewAudit().AddFinding("Access reviews", Anna, Today);

        Assert.False(finding.IsOverdue(Today));
        Assert.True(finding.IsOverdue(Today.AddDays(1)));
        finding.Close("Done", null, Anna, Now);
        Assert.False(finding.IsOverdue(Today.AddDays(1)));
    }

    // ---------------------------------------------------------- What the project shows

    [Fact]
    public void A_corrected_audit_gives_no_badge_and_no_open_findings()
    {
        var first = NewAudit();
        first.GiveCertificate("DK-1234", null, Today.AddYears(-1), Today.AddYears(1));
        first.AddFinding("Access reviews", Anna, Today.AddDays(30));
        var correction = NewAudit(corrects: first.Id);
        correction.GiveCertificate("DK-1235", null, Today.AddYears(-1), Today.AddYears(2));

        var overview = new AuditOverview([correction, first], [], Today);

        Assert.Same(correction, overview.CorrectedBy(first));
        Assert.Equal(["DK-1235"], overview.Badges.Select(b => b.Number));
        Assert.Empty(overview.OpenFindings);
    }

    [Fact]
    public void The_organisations_certificate_shows_on_the_project_but_not_its_findings()
    {
        var organisation = NewAudit(project: Guid.NewGuid());
        organisation.GiveCertificate("DK-1234", null, Today.AddYears(-1), Today.AddYears(1));
        organisation.AddFinding("Policy review", Anna, Today.AddDays(10));

        var overview = new AuditOverview([], [organisation], Today);

        Assert.True(overview.Badges.Single().FromOrganisation);
        Assert.Empty(overview.OpenFindings);
    }

    [Fact]
    public void The_newest_certificate_per_standard_is_shown()
    {
        var old = NewAudit(new DateOnly(2024, 1, 1));
        old.GiveCertificate("OLD", null, new DateOnly(2024, 1, 1), new DateOnly(2027, 1, 1));
        var newer = NewAudit();
        newer.GiveCertificate("NEW", null, Today.AddYears(-1), new DateOnly(2029, 1, 1));
        var other = NewAudit(scope: "ISO 9001:2015");
        other.GiveCertificate("Q-1", null, Today.AddYears(-1), new DateOnly(2028, 1, 1));

        Assert.Equal(["NEW", "Q-1"], new AuditOverview([old, newer, other], [], Today).Badges.Select(b => b.Number).Order());
    }

    [Fact]
    public void An_expiring_certificate_counts_the_days_left()
    {
        var audit = NewAudit();
        audit.GiveCertificate("DK-1234", null, Today.AddYears(-1), new DateOnly(2026, 12, 30));

        Assert.Equal(["Certificate DK-1234 expires in 48 days"], new AuditOverview([audit], [], Today).Expiring);
    }

    [Fact]
    public void The_log_text_names_kind_scope_auditor_result_certificate_and_findings()
    {
        var audit = NewAudit();
        audit.GiveCertificate("DK-1234", null, Today.AddYears(-1), new DateOnly(2027, 6, 30));
        audit.AddFinding("Access reviews", Anna, Today.AddDays(30));

        Assert.Equal("External audit of ISO/IEC 27001:2022 by Bureau Veritas on 12-10-2026: Passed; certificate DK-1234 to 30-06-2027; 1 finding",
            AuditService.Describe(audit));
    }
}
