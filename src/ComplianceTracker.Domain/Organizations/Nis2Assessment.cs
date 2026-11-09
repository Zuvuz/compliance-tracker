using ComplianceTracker.Domain.Common;

namespace ComplianceTracker.Domain.Organizations
{
    // US-142 R3: the result of the NIS2 scope check
    public enum Nis2Result
    {
        NotCovered,
        Important,
        Essential
    }

    // US-142 R3: the services that NIS2 treats in a special way, or that decide whether Implementing Regulation
    // (EU) 2024/2690 applies (R7). "None" for everything else in the sector.
    public enum Nis2Service
    {
        None,
        PublicElectronicCommunications,
        QualifiedTrustService,
        TrustService,
        TopLevelDomainRegistry,
        DnsService,
        CloudComputing,
        DataCentre,
        ContentDeliveryNetwork,
        ManagedService,
        ManagedSecurityService,
        OnlineMarketplace,
        OnlineSearchEngine,
        SocialNetworkingPlatform
    }

    // US-142 R3: one NIS2 scope check of an organisation. Never changed: a new check is a new row, so earlier results
    // are kept (the newest applies). The rules are the directive's Art. 2 and 3 with Annex I and II; the size limits are
    // those of Commission Recommendation 2003/361/EC, which Art. 2(1) refers to (B75).
    public class Nis2Assessment
    {
        // A sanity limit on the numbers, so a typo such as 10000000 employees is caught
        public const int MaxEmployees = 10_000_000;
        public const decimal MaxMillions = 10_000_000m;

        // Annex I (sectors of high criticality) and Annex II (other critical sectors)
        public static readonly IReadOnlySet<Sector> AnnexI = new HashSet<Sector>
        {
            Sector.Energy, Sector.Transport, Sector.Banking, Sector.FinancialMarketInfrastructure, Sector.Health,
            Sector.DrinkingWater, Sector.WasteWater, Sector.DigitalInfrastructure, Sector.IctServiceManagement,
            Sector.PublicAdministration, Sector.Space
        };

        public static readonly IReadOnlySet<Sector> AnnexII = new HashSet<Sector>
        {
            Sector.PostalAndCourier, Sector.WasteManagement, Sector.Chemicals, Sector.Food, Sector.Manufacturing,
            Sector.DigitalProviders, Sector.Research
        };

        // The sector each special service belongs to (Annex I point 8 and 9, Annex II point 6)
        public static readonly IReadOnlyDictionary<Nis2Service, Sector> SectorOf = new Dictionary<Nis2Service, Sector>
        {
            [Nis2Service.PublicElectronicCommunications] = Sector.DigitalInfrastructure,
            [Nis2Service.QualifiedTrustService] = Sector.DigitalInfrastructure,
            [Nis2Service.TrustService] = Sector.DigitalInfrastructure,
            [Nis2Service.TopLevelDomainRegistry] = Sector.DigitalInfrastructure,
            [Nis2Service.DnsService] = Sector.DigitalInfrastructure,
            [Nis2Service.CloudComputing] = Sector.DigitalInfrastructure,
            [Nis2Service.DataCentre] = Sector.DigitalInfrastructure,
            [Nis2Service.ContentDeliveryNetwork] = Sector.DigitalInfrastructure,
            [Nis2Service.ManagedService] = Sector.IctServiceManagement,
            [Nis2Service.ManagedSecurityService] = Sector.IctServiceManagement,
            [Nis2Service.OnlineMarketplace] = Sector.DigitalProviders,
            [Nis2Service.OnlineSearchEngine] = Sector.DigitalProviders,
            [Nis2Service.SocialNetworkingPlatform] = Sector.DigitalProviders
        };

        // R7: the providers Implementing Regulation (EU) 2024/2690 is for (its Art. 1)
        public static readonly IReadOnlySet<Nis2Service> Under2024_2690 = new HashSet<Nis2Service>
        {
            Nis2Service.DnsService, Nis2Service.TopLevelDomainRegistry, Nis2Service.CloudComputing, Nis2Service.DataCentre,
            Nis2Service.ContentDeliveryNetwork, Nis2Service.ManagedService, Nis2Service.ManagedSecurityService,
            Nis2Service.OnlineMarketplace, Nis2Service.OnlineSearchEngine, Nis2Service.SocialNetworkingPlatform,
            Nis2Service.TrustService, Nis2Service.QualifiedTrustService
        };

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }

        // What was answered
        public Sector Sector { get; private set; }
        public Nis2Service Service { get; private set; }
        public int Employees { get; private set; }
        public decimal TurnoverMillions { get; private set; }
        public decimal BalanceSheetMillions { get; private set; }
        public bool DesignatedByAuthority { get; private set; }

        // What it gave
        public Nis2Result Result { get; private set; }
        public string Reason { get; private set; } = "";
        public bool Uses2024_2690 { get; private set; }

        public Guid AssessedById { get; private set; }
        public DateTime AssessedAt { get; private set; }

        public bool IsCovered => Result != Nis2Result.NotCovered;

        // For EF Core only
        private Nis2Assessment() { }

        public static Nis2Assessment Assess(
            Guid organizationId, Sector sector, Nis2Service service, int employees, decimal turnoverMillions,
            decimal balanceSheetMillions, bool designatedByAuthority, Guid assessedById, DateTime assessedAt)
        {
            if (!Enum.IsDefined(sector))
            {
                throw new DomainException("Choose a sector");
            }

            if (!Enum.IsDefined(service))
            {
                throw new DomainException("Choose a service");
            }

            if (employees is < 0 or > MaxEmployees)
            {
                throw new DomainException("The number of employees must be between 0 and 10,000,000");
            }

            if (turnoverMillions is < 0 or > MaxMillions || balanceSheetMillions is < 0 or > MaxMillions)
            {
                throw new DomainException("Turnover and balance sheet must be 0 or more, in million euro");
            }

            if (service != Nis2Service.None && SectorOf[service] != sector)
            {
                throw new DomainException($"That service belongs to the sector {SectorOf[service]}");
            }

            var (result, reason) = Decide(sector, service, SizeOf(employees, turnoverMillions, balanceSheetMillions), designatedByAuthority);
            return new Nis2Assessment
            {
                OrganizationId = organizationId,
                Sector = sector,
                Service = service,
                Employees = employees,
                TurnoverMillions = turnoverMillions,
                BalanceSheetMillions = balanceSheetMillions,
                DesignatedByAuthority = designatedByAuthority,
                Result = result,
                Reason = reason,
                Uses2024_2690 = result != Nis2Result.NotCovered && Under2024_2690.Contains(service),
                AssessedById = assessedById,
                AssessedAt = assessedAt
            };
        }

        // Recommendation 2003/361/EC: medium from 50 employees, large from 250; or by money, where turnover AND
        // balance sheet must both be over the limit (10/10 million for medium, 50/43 million for large)
        public static OrganizationSize SizeOf(int employees, decimal turnoverMillions, decimal balanceSheetMillions)
        {
            if (employees >= 250 || (turnoverMillions > 50 && balanceSheetMillions > 43))
            {
                return OrganizationSize.Large;
            }

            if (employees >= 50 || (turnoverMillions > 10 && balanceSheetMillions > 10))
            {
                return OrganizationSize.Medium;
            }

            return employees >= 10 || (turnoverMillions > 2 && balanceSheetMillions > 2) ? OrganizationSize.Small : OrganizationSize.Micro;
        }

        private static (Nis2Result, string) Decide(Sector sector, Nis2Service service, OrganizationSize size, bool designated)
        {
            var annexI = AnnexI.Contains(sector);
            var annex = annexI ? "Annex I" : "Annex II";
            var large = size == OrganizationSize.Large;
            var atLeastMedium = size is OrganizationSize.Medium or OrganizationSize.Large;

            // Art. 3(1)(f): designated by the authority, whatever the size or sector
            if (designated)
            {
                return (Nis2Result.Essential, "Designated as essential by the authority (Art. 3(1)(f))");
            }

            // Art. 3(1)(b): always essential, whatever the size
            if (service is Nis2Service.QualifiedTrustService or Nis2Service.TopLevelDomainRegistry or Nis2Service.DnsService)
            {
                return (Nis2Result.Essential, "Qualified trust service, top-level domain registry or DNS service, whatever the size (Art. 3(1)(b))");
            }

            // Art. 3(1)(d): public administration of central government
            if (sector == Sector.PublicAdministration)
            {
                return (Nis2Result.Essential, "Public administration of central government (Art. 3(1)(d))");
            }

            // Art. 2(2)(a): covered whatever the size; Art. 3(1)(c): public electronic communications from medium size
            if (service == Nis2Service.PublicElectronicCommunications)
            {
                return atLeastMedium
                    ? (Nis2Result.Essential, $"Public electronic communications, {SizeText(size)} (Art. 3(1)(c))")
                    : (Nis2Result.Important, "Public electronic communications, covered whatever the size (Art. 2(2)(a))");
            }

            if (service == Nis2Service.TrustService)
            {
                return large
                    ? (Nis2Result.Essential, "Annex I, large (Art. 3(1)(a))")
                    : (Nis2Result.Important, "Trust service, covered whatever the size (Art. 2(2)(a))");
            }

            if (!annexI && !AnnexII.Contains(sector))
            {
                return (Nis2Result.NotCovered, "The sector is not in Annex I or II (Art. 2(1))");
            }

            if (!atLeastMedium)
            {
                return (Nis2Result.NotCovered, $"{annex}, but {SizeText(size)}: under the size limit (Art. 2(1))");
            }

            // Art. 3(1)(a): Annex I and large; everything else covered is important (Art. 3(2))
            return annexI && large
                ? (Nis2Result.Essential, "Annex I, large (Art. 3(1)(a))")
                : (Nis2Result.Important, $"{annex}, {SizeText(size)}");
        }

        private static string SizeText(OrganizationSize size) => size switch
        {
            OrganizationSize.Large => "large",
            OrganizationSize.Medium => "medium-sized",
            OrganizationSize.Small => "small",
            _ => "micro"
        };
    }
}
