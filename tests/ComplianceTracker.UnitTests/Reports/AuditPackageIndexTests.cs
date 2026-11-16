using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Mappings;

namespace ComplianceTracker.UnitTests.Reports;

// US-179: edge cases of the audit package's structure per requirement. Arrange / Act / Assert.
public class AuditPackageIndexTests
{
    private static readonly DateTime Monday = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();

    private static Framework NewFramework(string name)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.pdf", bytes), Anna, Monday);
    }

    private static FrameworkCompliance Compliance(Framework framework, params RequirementCoverage[] rows)
        => new(framework, framework.LatestVersion, rows);

    private static RequirementCoverage Row(Framework framework, string code, params EvidenceItem[] evidence)
        => new(framework.LatestVersion.AddRequirement(code, "Title " + code, null), []) { Evidence = evidence };

    private static EvidenceItem Link(string title, DateOnly? until = null)
        => new(Evidence.ForLink(Guid.NewGuid(), title, "https://example.com/p", until, Anna, null, Monday), false);

    // ---------------------------------------------------------- Folder names (R1)

    [Theory]
    [InlineData("CRA", "I.2(d)", "CRA/I.2(d)")]
    [InlineData("ISO/IEC 27001", "A.5.15", "ISO-IEC 27001/A.5.15")]
    [InlineData("NIS2", "Art. 21(2)(b)", "NIS2/Art. 21(2)(b)")]
    [InlineData("Policy", "A:1*?<>|\"", "Policy/A-1-")]
    public void A_folder_name_is_safe_on_every_system(string framework, string code, string expected)
        => Assert.Equal(expected, AuditPackageIndex.Folder(framework, code));

    [Theory]
    [InlineData("")]
    [InlineData("...")]
    [InlineData("   ")]
    public void A_code_that_leaves_nothing_gets_a_name(string code)
        => Assert.Equal("requirement", AuditPackageIndex.Part(code));

    [Fact]
    public void A_long_code_is_cut_and_does_not_end_with_a_dot()
    {
        var part = AuditPackageIndex.Part(new string('a', 59) + ".bbbbb");

        Assert.Equal(new string('a', 59), part);
    }

    [Fact]
    public void Two_codes_that_become_the_same_folder_get_a_number()
    {
        var f = NewFramework("Security policy");

        var rows = AuditPackageIndex.Rows([Compliance(f, Row(f, "A/1"), Row(f, "A:1"))], new Dictionary<Guid, (string, string)>());

        Assert.Equal(["Security policy/A-1", "Security policy/A-1-2"], rows.Select(r => r.Folder));
    }

    // ---------------------------------------------------------- Files stored once (R3)

    [Fact]
    public void The_same_file_on_two_requirements_points_to_the_same_path()
    {
        var f = NewFramework("Security policy");
        var policy = Link("Access policy");
        var files = new Dictionary<Guid, (string, string)> { [policy.Evidence.Id] = ("evidence/Access-policy-a.pdf", "abc") };

        var rows = AuditPackageIndex.Rows([Compliance(f, Row(f, "SP-1", policy), Row(f, "SP-2", policy))], files);

        Assert.All(rows, r => Assert.Equal("evidence/Access-policy-a.pdf", r.Evidence.Single().Path));
        Assert.All(rows, r => Assert.Equal("abc", r.Evidence.Single().Sha256));
    }

    [Fact]
    public void Two_files_with_the_same_name_get_different_paths()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var first = AuditPackageIndex.EvidencePath("Policy", "p.pdf", used);
        var second = AuditPackageIndex.EvidencePath("Policy", "P.PDF", used);

        Assert.Equal("evidence/Policy-p.pdf", first);
        Assert.Equal("evidence/2-Policy-P.PDF", second);
    }

    [Fact]
    public void A_file_name_with_a_slash_does_not_make_a_folder()
        => Assert.Equal("evidence/Policy-a-b.pdf", AuditPackageIndex.EvidencePath("Policy", "a/b.pdf", new HashSet<string>()));

    [Fact]
    public void Evidence_that_is_a_link_keeps_its_link_and_has_no_file()
    {
        var f = NewFramework("Security policy");

        var evidence = AuditPackageIndex.Rows([Compliance(f, Row(f, "SP-1", Link("Wiki")))], new Dictionary<Guid, (string, string)>()).Single().Evidence.Single();

        Assert.Null(evidence.Path);
        Assert.Equal("https://example.com/p", evidence.Url);
    }

    // ---------------------------------------------------------- The dossier (US-197 R1, R4; was the README of US-179 R2)

    private static string Text(GeneratedDocument dossier) => string.Join("\n", dossier.Blocks.Select(b => b.Text));

    [Fact]
    public void The_dossier_names_the_status_the_evidence_and_where_it_comes_from()
    {
        var f = NewFramework("Security policy");
        var policy = Link("Access policy", new DateOnly(2027, 3, 1));
        var files = new Dictionary<Guid, (string, string)> { [policy.Evidence.Id] = ("evidence/Access-policy-a.pdf", "abc") };
        var rows = AuditPackageIndex.Rows([Compliance(f, Row(f, "SP-1", policy))], files);

        var dossier = RequirementDossier.Build("Security policy", "Acme Portal", rows, null, _ => "Anna", "Acme ApS", Monday);

        var text = Text(dossier);
        Assert.Equal("Security policy-dossier.pdf", dossier.FileName);
        Assert.Contains("Status: Not covered", text);
        Assert.Contains("Access policy: evidence/Access-policy-a.pdf, SHA-256 abc. Added 16-11-2026, valid until 01-03-2027", text);
        Assert.Contains("User stories: none", text);
        Assert.Contains("sealed by Acme ApS", text);
    }

    [Fact]
    public void A_requirement_that_does_not_apply_says_why()
    {
        var f = NewFramework("Security policy");
        var row = new RequirementCoverage(f.LatestVersion.AddRequirement("SP-1", "T", null), [], "No hardware");

        var text = Text(RequirementDossier.Build("Security policy", "Acme Portal",
            AuditPackageIndex.Rows([Compliance(f, row)], new Dictionary<Guid, (string, string)>()), null, _ => "", null, Monday));

        Assert.Contains("Status: Not applicable (No hardware)", text);
        Assert.Contains("Evidence: none", text);
        Assert.Contains("Not sealed", text);
    }

    [Fact]
    public void A_framework_without_requirements_still_gets_a_dossier()
    {
        var text = Text(RequirementDossier.Build("Policy", "Acme Portal", [], null, _ => "", null, Monday));

        Assert.Contains("The framework has no requirements.", text);
        Assert.Contains("No lifecycle checklist items for this framework.", text);
    }

    [Fact]
    public void A_requirement_with_many_pieces_of_evidence_lists_each()
    {
        var f = NewFramework("Security policy");
        var evidence = Enumerable.Range(1, 30).Select(i => Link($"Proof {i}")).ToArray();

        var dossier = RequirementDossier.Build("Security policy", "Acme Portal",
            AuditPackageIndex.Rows([Compliance(f, Row(f, "SP-1", evidence))], new Dictionary<Guid, (string, string)>()), null, _ => "", null, Monday);

        Assert.Equal(30, dossier.Blocks.Count(b => b.Kind == DocumentBlockKind.Bullet && b.Text.StartsWith("Proof ", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_very_long_requirement_text_is_kept_whole()
    {
        var f = NewFramework("Security policy");
        var long_ = string.Join(" ", Enumerable.Repeat("word", 2000));
        var row = new RequirementCoverage(f.LatestVersion.AddRequirement("SP-1", "T", long_), []);

        var dossier = RequirementDossier.Build("Security policy", "Acme Portal",
            AuditPackageIndex.Rows([Compliance(f, row)], new Dictionary<Guid, (string, string)>()), null, _ => "", null, Monday);

        Assert.Contains(dossier.Blocks, b => b.Text == long_);
    }

    [Theory]
    [InlineData("CRA", "CRA", true)]
    [InlineData("ISO/IEC 27001", "ISO/IEC 27001:2022", true)]
    [InlineData("Customer requirements", "Customer requirements", true)]
    [InlineData("Optional test", "CRA", false)]
    [InlineData("AI Act", "CRA", false)]
    public void A_checklist_item_belongs_to_its_framework(string regulation, string framework, bool belongs)
        => Assert.Equal(belongs, RequirementDossier.Belongs(regulation, framework));

    [Fact]
    public void A_text_becomes_a_document_with_a_title_headings_and_bullets()
    {
        var document = RequirementDossier.FromText("00-check.pdf", "Audit check\n\nWhat is missing\n  [ ] SBOM\n  [x] Risks\nDone.\n");

        Assert.Equal([DocumentBlockKind.Title, DocumentBlockKind.Heading2, DocumentBlockKind.Bullet, DocumentBlockKind.Bullet, DocumentBlockKind.Paragraph],
            document.Blocks.Select(b => b.Kind));
        Assert.Equal("[ ] SBOM", document.Blocks[2].Text);
    }

    [Fact]
    public void An_empty_text_still_makes_a_document()
        => Assert.Equal("x.pdf", Assert.Single(RequirementDossier.FromText("x.pdf", "  \n").Blocks).Text);

    // ---------------------------------------------------------- Index (R4)

    [Fact]
    public void The_html_index_escapes_text_has_no_scripts_and_links_relative()
    {
        var f = NewFramework("Security policy");
        var evil = Link("<script>alert(1)</script>");
        var files = new Dictionary<Guid, (string, string)> { [evil.Evidence.Id] = ("evidence/x y.pdf", "abc") };
        var rows = AuditPackageIndex.Rows([Compliance(f, Row(f, "A(1)", evil))], files);

        var html = AuditPackageIndex.Html("Audit <package>", rows);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", html.Replace("https://example.com", ""), StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("href='evidence/x%20y.pdf'", html);
        Assert.Contains("href='Security%20policy-dossier.pdf'", html); // US-197: the code opens the framework's dossier
    }

    [Fact]
    public void The_workbook_has_a_row_per_evidence_and_one_for_a_requirement_without_any()
    {
        var f = NewFramework("Security policy");
        var rows = AuditPackageIndex.Rows([Compliance(f, Row(f, "SP-1", Link("A"), Link("B")), Row(f, "SP-2"))], new Dictionary<Guid, (string, string)>());

        var sheet = AuditPackageIndex.Sheet("Index", rows).Sheets.Single();

        Assert.Equal(3, sheet.Rows.Count);
        Assert.Equal(["SP-1", "SP-1", "SP-2"], sheet.Rows.Select(r => r.Cells[1]));
        Assert.All(sheet.Rows, r => Assert.Equal(ReportStatus.Bad, r.Status));
    }

    [Fact]
    public void No_frameworks_give_no_rows()
        => Assert.Empty(AuditPackageIndex.Rows([], new Dictionary<Guid, (string, string)>()));
}
