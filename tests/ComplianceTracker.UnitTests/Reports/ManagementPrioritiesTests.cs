using System.Text;
using ComplianceTracker.Application.Compliance;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Application.Risks;
using ComplianceTracker.Domain.Frameworks;
using ComplianceTracker.Domain.Reports;
using ComplianceTracker.Domain.Risks;

namespace ComplianceTracker.UnitTests.Reports;

// Edge-case unit tests of US-141: the traffic light, "since last", the points, the ranking of the gaps and the risks.
// Arrange / Act / Assert.
public class ManagementPrioritiesTests
{
    private const string Cra = "CRA – Regulation (EU) 2024/2847";
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ManagementFacts NoFacts = new(0, 0, 0, new DateOnly(2031, 1, 1));

    // A framework with the codes in this order; covered ones are covered, risks reach the requirement by code
    private static FrameworkCompliance Framework(
        string name, IEnumerable<string> codes, IEnumerable<string>? covered = null, Dictionary<string, RiskLevel>? risks = null)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        var framework = Domain.Frameworks.Framework.Create(Org, name, CatalogFile.Create("f.pdf", bytes), Anna, Monday, FrameworkLevel.Product);
        var coveredSet = (covered ?? []).ToHashSet();
        var requirements = codes.Select(code =>
        {
            var requirement = framework.LatestVersion.AddRequirement(code, "Title " + code, null);
            return new RequirementCoverage(requirement, [])
            {
                CoveredThrough = coveredSet.Contains(code) ? "elsewhere" : null,
                Risks = risks is not null && risks.TryGetValue(code, out var level) ? [new RiskReference(Guid.NewGuid(), "Risk " + code, level)] : []
            };
        }).ToList();
        return new FrameworkCompliance(framework, framework.LatestVersion, requirements);
    }

    private static IEnumerable<string> Codes(int count, string prefix = "R") => Enumerable.Range(1, count).Select(i => $"{prefix}{i}");

    // ---------------------------------------------------------- R2: the traffic light

    [Theory]
    [InlineData(0, 10, "Red")]
    [InlineData(49, 100, "Red")]
    [InlineData(50, 100, "Yellow")]
    [InlineData(79, 100, "Yellow")]
    [InlineData(80, 100, "Green")]
    [InlineData(100, 100, "Green")]
    public void The_light_is_red_under_50_yellow_to_79_and_green_from_80(int covered, int total, string expected)
    {
        // Act / Assert
        Assert.Equal(expected, ManagementPriorities.Light(covered, total).Text);
    }

    [Fact]
    public void The_light_follows_the_rounded_per_cent_that_is_shown()
    {
        // Act: 199 of 400 is 49.75 %, shown as 50 %
        var light = ManagementPriorities.Light(199, 400);

        // Assert
        Assert.Equal(("Yellow", ReportStatus.Warning), light);
    }

    [Fact]
    public void No_requirements_has_no_colour()
    {
        // Act / Assert
        Assert.Equal(("No requirements yet", ReportStatus.Muted), ManagementPriorities.Light(0, 0));
    }

    // ---------------------------------------------------------- R3: since last

    [Fact]
    public void Without_a_locked_report_there_is_nothing_to_compare_with()
    {
        // Act / Assert
        Assert.Equal("No locked report to compare with", ManagementPriorities.SinceLast(Cra, 7, null));
    }

    [Fact]
    public void A_regulation_the_locked_report_did_not_have_is_not_compared()
    {
        // Arrange
        var last = new LastManagementReport(Monday, new Dictionary<string, int> { ["GDPR"] = 40 });

        // Act / Assert
        Assert.Equal("No locked report to compare with", ManagementPriorities.SinceLast(Cra, 7, last));
    }

    [Theory]
    [InlineData(4, 7, "↑ +3 since")]
    [InlineData(9, 7, "↓ -2 since")]
    [InlineData(7, 7, "= unchanged since")]
    public void The_change_since_the_locked_report_is_shown_with_its_direction(int before, int now, string expected)
    {
        // Arrange
        var last = new LastManagementReport(Monday, new Dictionary<string, int> { [Cra] = before });

        // Act / Assert
        Assert.StartsWith(expected, ManagementPriorities.SinceLast(Cra, now, last));
    }

    [Fact]
    public void The_figures_survive_a_round_trip_through_json()
    {
        // Arrange
        var figures = ManagementPriorities.Figures([Framework(Cra, Codes(4), covered: ["R1"])]);

        // Act
        var back = ManagementPriorities.ParseFigures(ManagementPriorities.FiguresJson(figures));

        // Assert
        Assert.Equal(25, back![Cra]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Unreadable_figures_count_as_none(string? json)
    {
        // Act / Assert
        Assert.Null(ManagementPriorities.ParseFigures(json));
    }

    [Fact]
    public void The_same_regulations_ignore_their_order_but_not_an_extra_one()
    {
        // Arrange
        var figures = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2 };

        // Act / Assert
        Assert.True(ManagementPriorities.SameRegulations(figures, ["B", "A"]));
        Assert.False(ManagementPriorities.SameRegulations(figures, ["A"]));
        Assert.False(ManagementPriorities.SameRegulations(figures, ["A", "B", "C"]));
    }

    [Fact]
    public void A_locked_snapshot_keeps_its_figures_and_a_blank_is_none()
    {
        // Act
        var with = ReportSnapshot.Lock(Org, null, "traceability:management:all", "T", "t.pdf", "application/pdf", [1], Anna, null, Monday, " {\"A\":1} ");
        var blank = ReportSnapshot.Lock(Org, null, "traceability:management:all", "T", "t.pdf", "application/pdf", [1], Anna, null, Monday, "  ");

        // Assert
        Assert.Equal("{\"A\":1}", with.Figures);
        Assert.Null(blank.Figures);
    }

    // ---------------------------------------------------------- R4: what management must know now

    [Fact]
    public void Nothing_to_report_says_so()
    {
        // Act
        var points = ManagementPriorities.Points([Framework(Cra, Codes(2), covered: Codes(2))], NoFacts);

        // Assert
        Assert.Equal(["Nothing needs a decision right now"], points);
    }

    [Fact]
    public void At_most_five_points_most_serious_first()
    {
        // Arrange: seven things to report
        var facts = new ManagementFacts(2, 3, 1, null)
        {
            HighRisks = [new ManagementRisk("Orphan", RiskLevel.High, 12, "", false, null)]
        };
        List<FrameworkCompliance> frameworks = [Framework(Cra, Codes(10)), Framework("GDPR", Codes(10)), Framework("AI Act", Codes(10), covered: Codes(6))];

        // Act
        var points = ManagementPriorities.Points(frameworks, facts);

        // Assert
        Assert.Equal(5, points.Count);
        Assert.StartsWith("1 open security event to handle", points[0]);
        Assert.Equal("3 open high or critical scan findings", points[1]);
        Assert.Equal("1 High or Critical risk without an owner", points[2]);
        Assert.Equal("Support period not set; CRA requires one (Art. 13(8))", points[3]);
        Assert.Equal("CRA: 10 of 10 requirements not covered (0 %)", points[4]);
    }

    [Fact]
    public void The_support_period_only_matters_for_the_cra()
    {
        // Act
        var points = ManagementPriorities.Points([Framework("GDPR", Codes(1), covered: Codes(1))], new ManagementFacts(0, 0, 0, null));

        // Assert
        Assert.Equal(["Nothing needs a decision right now"], points);
    }

    [Fact]
    public void A_regulation_without_requirements_is_not_a_point()
    {
        // Act
        var points = ManagementPriorities.Points([Framework("GDPR", [])], NoFacts);

        // Assert
        Assert.Equal(["Nothing needs a decision right now"], points);
    }

    // ---------------------------------------------------------- R5: the biggest gaps

    [Fact]
    public void A_gap_linked_to_a_risk_comes_first_then_a_deadline_then_the_catalogues_order()
    {
        // Arrange
        var framework = Framework(Cra, ["Art. 13(1)", "Art. 14(1)", "I.2(a)", "I.2(k)", "I.2(m)"],
            risks: new() { ["I.2(k)"] = RiskLevel.High, ["I.2(m)"] = RiskLevel.Critical, ["I.2(a)"] = RiskLevel.Medium });

        // Act
        var gaps = ManagementPriorities.Gaps(framework);

        // Assert
        Assert.Equal(["I.2(m)", "I.2(k)", "Art. 14(1)", "Art. 13(1)", "I.2(a)"], gaps.Select(g => g.Requirement.Requirement.Code));
        Assert.Equal(["Linked to a Critical risk", "Linked to a High risk", "Has a deadline (CRA Art. 14)", "Not covered", "Not covered"],
            gaps.Select(g => g.Why));
    }

    [Fact]
    public void Art_14_has_no_deadline_outside_the_cra()
    {
        // Act
        var gaps = ManagementPriorities.Gaps(Framework("AI Act – Regulation (EU) 2024/1689", ["Art. 13", "Art. 14"]));

        // Assert
        Assert.Equal(["Art. 13", "Art. 14"], gaps.Select(g => g.Requirement.Requirement.Code));
        Assert.All(gaps, g => Assert.Equal("Not covered", g.Why));
    }

    [Fact]
    public void At_most_ten_gaps_and_covered_requirements_are_not_gaps()
    {
        // Act
        var gaps = ManagementPriorities.Gaps(Framework(Cra, Codes(15), covered: ["R1", "R2"]));

        // Assert
        Assert.Equal(10, gaps.Count);
        Assert.Equal("R3", gaps[0].Requirement.Requirement.Code);
    }

    [Fact]
    public void A_covered_requirement_with_a_high_risk_is_not_a_gap()
    {
        // Act
        var gaps = ManagementPriorities.Gaps(Framework(Cra, ["A", "B"], covered: ["A"], risks: new() { ["A"] = RiskLevel.Critical }));

        // Assert
        Assert.Equal(["B"], gaps.Select(g => g.Requirement.Requirement.Code));
    }

    // ---------------------------------------------------------- R6: the open high risks

    [Fact]
    public void Risks_are_listed_highest_first_and_low_ones_left_out()
    {
        // Arrange
        ManagementRisk[] risks =
        [
            new("B", RiskLevel.High, 10, "Bo", true, null),
            new("Low", RiskLevel.Low, 2, "Bo", true, null),
            new("A", RiskLevel.High, 10, "Bo", true, null),
            new("C", RiskLevel.Critical, 20, "", false, null),
            new("D", RiskLevel.High, 16, "Bo", true, null)
        ];

        // Act
        var ordered = ManagementPriorities.Risks(risks);

        // Assert
        Assert.Equal(["C", "D", "A", "B"], ordered.Select(r => r.Title));
    }

    [Fact]
    public void A_risk_without_an_owner_says_so()
    {
        // Arrange
        var facts = NoFacts with { HighRisks = [new ManagementRisk("Orphan", RiskLevel.High, 12, "", false, new DateOnly(2026, 12, 1))] };

        // Act
        var sheet = ManagementPriorities.Sheets([Framework(Cra, Codes(1))], facts).Single(s => s.Name == "Open high risks");

        // Assert
        Assert.Equal(["Orphan", "High", "(no owner)", "01-12-2026"], sheet.Rows[0].Cells);
    }

    // ---------------------------------------------------------- R1, R8: the sections

    [Fact]
    public void The_sections_come_in_their_fixed_order()
    {
        // Act
        var sheets = ManagementPriorities.Sheets([Framework(Cra, Codes(3))], NoFacts);

        // Assert
        Assert.Equal(["Status", "What management must know now", "Key figures", "Biggest gaps", "Open high risks", "Details"], sheets.Select(s => s.Name));
        Assert.Equal(ReportSheetKind.Tiles, sheets[2].Kind);
        Assert.Equal(ReportSheetKind.Points, sheets[1].Kind);
        Assert.All(sheets, s => Assert.True(s.Name.Length <= 31));
    }

    [Fact]
    public void A_regulation_without_requirements_shows_no_per_cent()
    {
        // Act
        var status = ManagementPriorities.Sheets([Framework("GDPR", [])], NoFacts)[0];

        // Assert
        Assert.Equal(["GDPR", "No requirements yet", "No requirements yet", "No locked report to compare with"], status.Rows[0].Cells);
        Assert.Equal(ReportStatus.Muted, status.Rows[0].Status);
    }
}
