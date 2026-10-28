using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Organizations
{
    // The organisation's sector. The values follow the sectors in the NIS2 directive,
    // so the tracker can later tell whether NIS2 applies. "Other" covers everything else.
    public enum Sector
    {
        Energy,
        Transport,
        Banking,
        FinancialMarketInfrastructure,
        Health,
        DrinkingWater,
        WasteWater,
        DigitalInfrastructure,
        IctServiceManagement,
        PublicAdministration,
        Space,
        PostalAndCourier,
        WasteManagement,
        Chemicals,
        Food,
        Manufacturing,
        DigitalProviders,
        Research,
        Other
    }
}
