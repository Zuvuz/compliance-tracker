namespace ComplianceTracker.Domain.Organizations
{
    // US-080 R1: the industry the organisation makes products for. It only decides what is suggested (B105).
    public enum Industry
    {
        Software,
        Medtech,
        Fintech,
        Other
    }

    // US-163 R2: the industry that usually fits a NIS2 sector, suggested on the organisation's profile. Nothing is changed
    // until the admin saves. The sector decides whether NIS2 applies; the industry decides what the tracker suggests.
    public static class IndustryFromSector
    {
        public static Industry? Suggest(Sector sector) => sector switch
        {
            Sector.Health => Industry.Medtech,
            Sector.Banking or Sector.FinancialMarketInfrastructure => Industry.Fintech,
            Sector.DigitalInfrastructure or Sector.IctServiceManagement or Sector.DigitalProviders => Industry.Software,
            Sector.Other => null, // nothing to go by
            _ when Enum.IsDefined(sector) => Industry.Other,
            _ => null
        };
    }
}
