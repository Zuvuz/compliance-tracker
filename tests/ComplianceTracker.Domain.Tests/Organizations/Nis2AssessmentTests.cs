using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.Domain.Tests.Organizations;

// Edge-case unit tests of US-142 R3, R7: the NIS2 scope check. Arrange / Act / Assert.
public class Nis2AssessmentTests
{
    private static readonly DateTime Monday = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private static Nis2Assessment Check(Sector sector, int employees, decimal turnover = 0, decimal balance = 0,
        Nis2Service service = Nis2Service.None, bool designated = false)
        => Nis2Assessment.Assess(Guid.NewGuid(), sector, service, employees, turnover, balance, designated, Guid.NewGuid(), Monday);

    [Fact]
    public void An_annex_II_medium_sized_company_is_important()
    {
        // Act
        var check = Check(Sector.Manufacturing, 60, 12, 5);

        // Assert
        Assert.Equal(Nis2Result.Important, check.Result);
        Assert.Equal("Annex II, medium-sized", check.Reason);
        Assert.False(check.Uses2024_2690);
    }

    [Fact]
    public void An_annex_I_large_company_is_essential()
    {
        // Act / Assert
        Assert.Equal(Nis2Result.Essential, Check(Sector.Energy, 300).Result);
    }

    [Fact]
    public void An_annex_I_medium_sized_company_is_important()
    {
        // Act
        var check = Check(Sector.Health, 80);

        // Assert
        Assert.Equal(Nis2Result.Important, check.Result);
        Assert.Equal("Annex I, medium-sized", check.Reason);
    }

    [Fact]
    public void An_annex_II_large_company_is_only_important()
    {
        // Act / Assert
        Assert.Equal(Nis2Result.Important, Check(Sector.Food, 1000).Result);
    }

    [Theory]
    [InlineData(49, Nis2Result.NotCovered)]
    [InlineData(50, Nis2Result.Important)]
    public void Fifty_employees_is_the_medium_limit(int employees, Nis2Result expected)
    {
        // Act / Assert
        Assert.Equal(expected, Check(Sector.Manufacturing, employees).Result);
    }

    [Theory]
    [InlineData(249, Nis2Result.Important)]
    [InlineData(250, Nis2Result.Essential)]
    public void Two_hundred_and_fifty_employees_is_the_large_limit(int employees, Nis2Result expected)
    {
        // Act / Assert
        Assert.Equal(expected, Check(Sector.Transport, employees).Result);
    }

    [Theory]
    [InlineData(10, 11, Nis2Result.NotCovered)] // exactly 10 million turnover is not over the limit
    [InlineData(10.01, 10.01, Nis2Result.Important)]
    [InlineData(20, 5, Nis2Result.NotCovered)] // both turnover and balance sheet must be over the limit
    public void Money_counts_only_when_both_turnover_and_balance_sheet_are_over_the_limit(decimal turnover, decimal balance, Nis2Result expected)
    {
        // Act / Assert
        Assert.Equal(expected, Check(Sector.Chemicals, 5, turnover, balance).Result);
    }

    [Fact]
    public void A_small_company_in_an_annex_is_not_covered_and_says_why()
    {
        // Act
        var check = Check(Sector.Manufacturing, 20);

        // Assert
        Assert.Equal(Nis2Result.NotCovered, check.Result);
        Assert.Contains("under the size limit", check.Reason);
        Assert.False(check.IsCovered);
    }

    [Fact]
    public void A_sector_outside_the_annexes_is_not_covered()
    {
        // Act
        var check = Check(Sector.Other, 5000);

        // Assert
        Assert.Equal(Nis2Result.NotCovered, check.Result);
        Assert.Contains("not in Annex I or II", check.Reason);
    }

    [Theory]
    [InlineData(Nis2Service.DnsService)]
    [InlineData(Nis2Service.TopLevelDomainRegistry)]
    [InlineData(Nis2Service.QualifiedTrustService)]
    public void Some_digital_services_are_essential_whatever_the_size(Nis2Service service)
    {
        // Act / Assert
        Assert.Equal(Nis2Result.Essential, Check(Sector.DigitalInfrastructure, 2, service: service).Result);
    }

    [Theory]
    [InlineData(5, Nis2Result.Important)]
    [InlineData(60, Nis2Result.Essential)]
    public void Public_electronic_communications_is_covered_whatever_the_size(int employees, Nis2Result expected)
    {
        // Act / Assert
        Assert.Equal(expected, Check(Sector.DigitalInfrastructure, employees, service: Nis2Service.PublicElectronicCommunications).Result);
    }

    [Fact]
    public void A_small_trust_service_is_important()
    {
        // Act / Assert
        Assert.Equal(Nis2Result.Important, Check(Sector.DigitalInfrastructure, 3, service: Nis2Service.TrustService).Result);
    }

    [Fact]
    public void Public_administration_is_essential()
    {
        // Act / Assert
        Assert.Equal(Nis2Result.Essential, Check(Sector.PublicAdministration, 3).Result);
    }

    [Fact]
    public void Designation_by_the_authority_wins_even_outside_the_annexes()
    {
        // Act
        var check = Check(Sector.Other, 3, designated: true);

        // Assert
        Assert.Equal(Nis2Result.Essential, check.Result);
        Assert.Contains("Art. 3(1)(f)", check.Reason);
    }

    [Theory]
    [InlineData(Sector.DigitalInfrastructure, Nis2Service.CloudComputing, 120, true)]
    [InlineData(Sector.IctServiceManagement, Nis2Service.ManagedService, 60, true)]
    [InlineData(Sector.DigitalProviders, Nis2Service.OnlineMarketplace, 60, true)]
    [InlineData(Sector.DigitalInfrastructure, Nis2Service.PublicElectronicCommunications, 60, false)]
    [InlineData(Sector.DigitalInfrastructure, Nis2Service.CloudComputing, 10, false)] // not covered, so not either
    public void The_technical_measures_regulation_applies_only_to_covered_digital_providers(Sector sector, Nis2Service service, int employees, bool expected)
    {
        // Act / Assert
        Assert.Equal(expected, Check(sector, employees, service: service).Uses2024_2690);
    }

    [Fact]
    public void A_service_must_belong_to_the_sector()
    {
        // Act / Assert
        var error = Assert.Throws<DomainException>(() => Check(Sector.Manufacturing, 60, service: Nis2Service.CloudComputing));
        Assert.Equal("That service belongs to the sector DigitalInfrastructure", error.Message);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(10_000_001, 0, 0)]
    [InlineData(0, -0.01, 0)]
    [InlineData(0, 0, -5)]
    public void Numbers_out_of_range_are_refused(int employees, decimal turnover, decimal balance)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Check(Sector.Manufacturing, employees, turnover, balance));
    }

    [Fact]
    public void An_unknown_sector_is_refused()
    {
        // Act / Assert
        Assert.Equal("Choose a sector", Assert.Throws<DomainException>(() => Check((Sector)999, 60)).Message);
    }

    [Fact]
    public void Every_sector_but_Other_is_in_exactly_one_annex()
    {
        // Act
        var sectors = Enum.GetValues<Sector>().Where(s => s != Sector.Other).ToList();

        // Assert
        Assert.All(sectors, s => Assert.True(Nis2Assessment.AnnexI.Contains(s) ^ Nis2Assessment.AnnexII.Contains(s), s.ToString()));
        Assert.DoesNotContain(Sector.Other, Nis2Assessment.AnnexI.Concat(Nis2Assessment.AnnexII));
    }

    [Fact]
    public void Every_special_service_has_a_sector()
    {
        // Act / Assert
        Assert.All(Enum.GetValues<Nis2Service>().Where(s => s != Nis2Service.None), s => Assert.True(Nis2Assessment.SectorOf.ContainsKey(s), s.ToString()));
    }
}
