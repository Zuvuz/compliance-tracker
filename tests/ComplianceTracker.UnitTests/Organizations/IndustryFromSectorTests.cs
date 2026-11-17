using ComplianceTracker.Domain.Organizations;

namespace ComplianceTracker.UnitTests.Organizations;

// US-163 R2: the industry a NIS2 sector suggests. Edge cases. Arrange / Act / Assert.
public class IndustryFromSectorTests
{
    [Theory]
    [InlineData(Sector.Health, Industry.Medtech)]
    [InlineData(Sector.Banking, Industry.Fintech)]
    [InlineData(Sector.FinancialMarketInfrastructure, Industry.Fintech)]
    [InlineData(Sector.IctServiceManagement, Industry.Software)]
    [InlineData(Sector.DigitalInfrastructure, Industry.Software)]
    [InlineData(Sector.DigitalProviders, Industry.Software)]
    [InlineData(Sector.Energy, Industry.Other)]
    [InlineData(Sector.Manufacturing, Industry.Other)]
    public void A_sector_suggests_the_industry_that_usually_fits(Sector sector, Industry industry)
        => Assert.Equal(industry, IndustryFromSector.Suggest(sector));

    [Fact]
    public void The_sector_other_gives_nothing_to_go_by()
        => Assert.Null(IndustryFromSector.Suggest(Sector.Other));

    [Fact]
    public void An_unknown_sector_suggests_nothing()
        => Assert.Null(IndustryFromSector.Suggest((Sector)999));

    [Fact]
    public void Every_known_sector_but_other_suggests_an_industry()
        => Assert.All(Enum.GetValues<Sector>().Where(s => s != Sector.Other), s => Assert.NotNull(IndustryFromSector.Suggest(s)));
}
