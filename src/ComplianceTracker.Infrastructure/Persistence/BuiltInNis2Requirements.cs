namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-142: the built-in NIS2 catalogues, all at organisation level and added by a migration (seed data) with fixed ids.
    // - NIS2: Directive (EU) 2022/2555, the obligations of essential and important entities (Art. 20, 21, 23, 3(4), 27).
    // - The Danish NIS2 law (lov nr. 434 af 6. maj 2025), a national layer: each requirement names the NIS2 requirement
    //   it implements (R6). Section numbers checked on retsinformation.dk, 09-11-2026.
    // - Implementing Regulation (EU) 2024/2690: the technical measures for certain digital providers, by Annex section;
    //   each names the Art. 21 point it details. Switched on only for those providers (R7).
    // Like the AI Act (B58), titles and descriptions are summaries in our own words, in English, with a Danish text (R8).
    // EU legal texts and Danish laws may be reused freely; the binding text is behind each Reference link.
    // A change in the law becomes a new version (US-035 R7), never an edit of these rows.
    public static class BuiltInNis2Requirements
    {
        public record Item(Guid Id, int Position, string Group, string Code, string Title, string Description, string Reference,
            string? Implements, string DanishTitle, string DanishDescription, Guid DanishTextId);

        // ---------------------------------------------------------- NIS2

        public const string Governance = "Governance (Art. 20)";
        public const string Measures = "Cybersecurity risk-management measures (Art. 21)";
        public const string Reporting = "Reporting obligations (Art. 23)";
        public const string Registration = "Registration (Art. 3 and 27)";

        private const string Nis2Document = "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555";

        // R5: every obligation the catalogue must hold; a unit test checks the list against Nis2 (no requirement missing)
        public static readonly IReadOnlyList<string> Nis2Codes =
        [
            "Art. 20(1)", "Art. 20(2)", "Art. 21(1)",
            "Art. 21(2)(a)", "Art. 21(2)(b)", "Art. 21(2)(c)", "Art. 21(2)(d)", "Art. 21(2)(e)",
            "Art. 21(2)(f)", "Art. 21(2)(g)", "Art. 21(2)(h)", "Art. 21(2)(i)", "Art. 21(2)(j)",
            "Art. 21(3)", "Art. 21(4)",
            "Art. 23(1)", "Art. 23(2)", "Art. 23(4)(a)", "Art. 23(4)(b)", "Art. 23(4)(c)", "Art. 23(4)(d)",
            "Art. 3(4)", "Art. 27(2)"
        ];

        public static readonly IReadOnlyList<Item> Nis2 =
        [
            N(1, Governance, "Art. 20(1)", 20, "Management approves and oversees the measures",
                "The management body approves the cybersecurity risk-management measures taken under Art. 21, oversees how they are carried out, and can be held liable when the entity does not comply.",
                "Ledelsen godkender og fører tilsyn med foranstaltningerne",
                "Ledelsesorganet godkender de foranstaltninger til styring af cybersikkerhedsrisici, der træffes efter art. 21, fører tilsyn med gennemførelsen og kan holdes ansvarlig, hvis enheden ikke overholder dem."),
            N(2, Governance, "Art. 20(2)", 20, "Management takes cybersecurity training",
                "Members of the management body follow training to gain enough knowledge and skills to identify risks and assess cybersecurity practices, and the entity encourages similar training for its employees regularly.",
                "Ledelsen deltager i cybersikkerhedskurser",
                "Ledelsens medlemmer deltager i kurser, så de kan identificere risici og vurdere praksis for cybersikkerhed, og enheden tilskynder til, at de øvrige ansatte jævnligt får tilsvarende kurser."),

            N(3, Measures, "Art. 21(1)", 21, "Appropriate and proportionate measures",
                "The entity takes appropriate and proportionate technical, operational and organisational measures to manage the risks to its network and information systems and to prevent or minimise the impact of incidents, based on an all-hazards approach and in proportion to its exposure, size and the likely impact.",
                "Passende og forholdsmæssige foranstaltninger",
                "Enheden træffer passende og forholdsmæssige tekniske, operationelle og organisatoriske foranstaltninger for at styre risiciene for sine net- og informationssystemer og forhindre eller begrænse virkningen af hændelser, ud fra en tilgang med alle farer og i forhold til eksponering, størrelse og sandsynlig virkning."),
            N(4, Measures, "Art. 21(2)(a)", 21, "Risk analysis and information system security policies",
                "Policies on risk analysis and on the security of information systems.",
                "Politikker for risikoanalyse og informationssystemsikkerhed",
                "Politikker for risikoanalyse og for sikkerheden i informationssystemer."),
            N(5, Measures, "Art. 21(2)(b)", 21, "Incident handling",
                "How incidents are prevented, detected, analysed, contained, responded to and recovered from.",
                "Håndtering af hændelser",
                "Hvordan hændelser forebygges, opdages, analyseres, inddæmmes, håndteres og genoprettes."),
            N(6, Measures, "Art. 21(2)(c)", 21, "Business continuity and crisis management",
                "Business continuity, such as backup management and disaster recovery, and crisis management.",
                "Driftskontinuitet og krisestyring",
                "Driftskontinuitet, f.eks. backupstyring og reetablering efter en katastrofe, og krisestyring."),
            N(7, Measures, "Art. 21(2)(d)", 21, "Supply chain security",
                "Security of the supply chain, including the security of the relationships with direct suppliers and service providers.",
                "Forsyningskædesikkerhed",
                "Sikkerhed i forsyningskæden, herunder sikkerheden i forholdet til direkte leverandører og tjenesteudbydere."),
            N(8, Measures, "Art. 21(2)(e)", 21, "Security in acquisition, development and maintenance",
                "Security when network and information systems are acquired, developed and maintained, including vulnerability handling and disclosure.",
                "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse",
                "Sikkerhed når net- og informationssystemer erhverves, udvikles og vedligeholdes, herunder håndtering og offentliggørelse af sårbarheder."),
            N(9, Measures, "Art. 21(2)(f)", 21, "Assessing the effectiveness of the measures",
                "Policies and procedures to assess whether the cybersecurity risk-management measures are effective.",
                "Vurdering af foranstaltningernes effektivitet",
                "Politikker og procedurer til at vurdere, om foranstaltningerne til styring af cybersikkerhedsrisici virker."),
            N(10, Measures, "Art. 21(2)(g)", 21, "Basic cyber hygiene and cybersecurity training",
                "Basic cyber hygiene practices and cybersecurity training.",
                "Grundlæggende cyberhygiejne og cybersikkerhedsuddannelse",
                "Grundlæggende cyberhygiejnepraksis og uddannelse i cybersikkerhed."),
            N(11, Measures, "Art. 21(2)(h)", 21, "Cryptography and encryption",
                "Policies and procedures on the use of cryptography and, where relevant, encryption.",
                "Kryptografi og kryptering",
                "Politikker og procedurer for brug af kryptografi og, hvor det er relevant, kryptering."),
            N(12, Measures, "Art. 21(2)(i)", 21, "Human resources security, access control and asset management",
                "Human resources security, access control policies and asset management.",
                "Personalesikkerhed, adgangskontrol og forvaltning af aktiver",
                "Personalesikkerhed, politikker for adgangskontrol og forvaltning af aktiver."),
            N(13, Measures, "Art. 21(2)(j)", 21, "Multi-factor authentication and secured communications",
                "Multi-factor or continuous authentication, secured voice, video and text communications, and secured emergency communication systems within the entity, where appropriate.",
                "Multifaktorautentificering og sikret kommunikation",
                "Multifaktor- eller kontinuerlig autentificering, sikret tale-, video- og tekstkommunikation og sikrede nødkommunikationssystemer internt, hvor det er relevant."),
            N(14, Measures, "Art. 21(3)", 21, "Suppliers' vulnerabilities and practices",
                "When deciding the supply chain measures, the entity takes into account the vulnerabilities of each direct supplier and service provider, the overall quality of their products and cybersecurity practices, and their secure development procedures.",
                "Leverandørers sårbarheder og praksis",
                "Når enheden fastlægger foranstaltninger for forsyningskæden, tager den højde for den enkelte direkte leverandørs sårbarheder, den samlede kvalitet af produkter og cybersikkerhedspraksis og deres procedurer for sikker udvikling."),
            N(15, Measures, "Art. 21(4)", 21, "Corrective measures without undue delay",
                "An entity that finds it does not comply with the measures takes all necessary, appropriate and proportionate corrective measures without undue delay.",
                "Korrigerende foranstaltninger uden unødigt ophold",
                "En enhed, der ikke overholder foranstaltningerne, træffer uden unødigt ophold alle nødvendige, passende og forholdsmæssige korrigerende foranstaltninger."),

            N(16, Reporting, "Art. 23(1)", 23, "Notify significant incidents",
                "The entity notifies its CSIRT or competent authority without undue delay of any significant incident, and informs the recipients of its services of significant incidents likely to affect the services.",
                "Underret om væsentlige hændelser",
                "Enheden underretter uden unødigt ophold sin CSIRT eller kompetente myndighed om enhver væsentlig hændelse og orienterer modtagerne af sine tjenester om væsentlige hændelser, der sandsynligvis påvirker tjenesterne."),
            N(17, Reporting, "Art. 23(2)", 23, "Inform recipients about significant cyber threats",
                "The entity tells the recipients of its services who may be affected by a significant cyber threat which measures or remedies they can take, and where appropriate about the threat itself.",
                "Orienter modtagere om væsentlige cybertrusler",
                "Enheden fortæller de modtagere af sine tjenester, der kan blive berørt af en væsentlig cybertrussel, hvilke foranstaltninger de kan træffe, og hvor det er relevant om selve truslen."),
            N(18, Reporting, "Art. 23(4)(a)", 23, "Early warning within 24 hours",
                "An early warning within 24 hours of becoming aware of the significant incident, saying whether it is suspected to be caused by unlawful or malicious acts and whether it could have a cross-border impact.",
                "Tidlig varsling inden for 24 timer",
                "En tidlig varsling inden for 24 timer efter kendskab til den væsentlige hændelse, der angiver, om den mistænkes at skyldes ulovlige eller ondsindede handlinger, og om den kan få grænseoverskridende virkning."),
            N(19, Reporting, "Art. 23(4)(b)", 23, "Incident notification within 72 hours",
                "An incident notification within 72 hours of becoming aware of the significant incident, updating the early warning with an initial assessment of its severity and impact and the indicators of compromise, where available.",
                "Hændelsesunderretning inden for 72 timer",
                "En hændelsesunderretning inden for 72 timer efter kendskab til hændelsen, som opdaterer den tidlige varsling med en første vurdering af alvor og virkning og kompromitteringsindikatorer, hvor de findes."),
            N(20, Reporting, "Art. 23(4)(c)", 23, "Intermediate report on request",
                "An intermediate report on relevant status updates when the CSIRT or the competent authority asks for one.",
                "Foreløbig rapport efter anmodning",
                "En foreløbig rapport med relevante statusopdateringer, når CSIRT'en eller den kompetente myndighed beder om det."),
            N(21, Reporting, "Art. 23(4)(d)", 23, "Final report within one month",
                "A final report no later than one month after the incident notification, with a detailed description of the incident, the likely root cause, the mitigation applied and ongoing, and any cross-border impact; a progress report instead if the incident is still ongoing.",
                "Endelig rapport inden for en måned",
                "En endelig rapport senest en måned efter hændelsesunderretningen med en detaljeret beskrivelse, den sandsynlige grundlæggende årsag, de afbødende foranstaltninger og eventuel grænseoverskridende virkning; en statusrapport i stedet, hvis hændelsen stadig pågår."),

            N(22, Registration, "Art. 3(4)", 3, "Register with the authority",
                "The entity submits at least its name, address and up-to-date contact details, its sector and sub-sector, and the other member states where it provides services, so the authority can keep its list of essential and important entities, and notifies changes.",
                "Registrer jer hos myndigheden",
                "Enheden indsender mindst navn, adresse og ajourførte kontaktoplysninger, sektor og delsektor og de øvrige medlemsstater, hvor den leverer tjenester, så myndigheden kan føre sin liste over væsentlige og vigtige enheder, og melder ændringer."),
            N(23, Registration, "Art. 27(2)", 27, "Registration of digital providers",
                "Only for DNS, top-level domain, domain registration, cloud, data centre, content delivery network, managed (security) service providers, online marketplaces, search engines and social networking platforms: register with the authority, including the main establishment, IP ranges and the member states served, and notify changes within three months.",
                "Registrering af digitale udbydere",
                "Kun for udbydere af DNS, topdomæner, domænenavnsregistrering, cloud, datacentre, indholdsleveringsnetværk, administrerede (sikkerheds)tjenester, onlinemarkedspladser, søgemaskiner og sociale netværk: registrering hos myndigheden med hovedforretningssted, ip-intervaller og de medlemsstater, der betjenes, og ændringer inden for tre måneder.")
        ];

        private static Item N(int number, string group, string code, int article, string title, string description, string danishTitle, string danishDescription)
            => new(Id("05", number), number, group, code, title, description, $"{Nis2Document}#art_{article}", null, danishTitle, danishDescription, TextId("05", number));

        // ---------------------------------------------------------- The Danish NIS2 law

        public const string DanishMeasures = "Foranstaltninger (§ 6-7)";
        public const string DanishRegistration = "Registrering (§ 9-10)";
        public const string DanishReporting = "Underretning (§ 12-15)";

        private const string DanishDocument = "https://www.retsinformation.dk/eli/lta/2025/434";

        public static readonly IReadOnlyList<Item> Danish =
        [
            D(1, DanishMeasures, "§ 6, stk. 1", "Art. 21(1)", "Appropriate and proportionate measures",
                "Essential and important entities take appropriate and proportionate technical, operational and organisational measures to manage the risks to their network and information systems, as a minimum those in points 1 to 10.",
                "Passende og forholdsmæssige foranstaltninger",
                "Væsentlige og vigtige enheder træffer passende og forholdsmæssige tekniske, operationelle og organisatoriske foranstaltninger for at styre risiciene for deres net- og informationssystemer, som minimum dem i nr. 1-10."),
            D(2, DanishMeasures, "§ 6, stk. 1, nr. 1", "Art. 21(2)(a)", "Risk analysis and information system security policies",
                "Policies on risk analysis and information system security.", "Politikker for risikoanalyse og informationssystemsikkerhed",
                "Politikker for risikoanalyse og informationssystemsikkerhed."),
            D(3, DanishMeasures, "§ 6, stk. 1, nr. 2", "Art. 21(2)(b)", "Incident handling",
                "Handling of incidents.", "Håndtering af hændelser", "Håndtering af hændelser."),
            D(4, DanishMeasures, "§ 6, stk. 1, nr. 3", "Art. 21(2)(c)", "Business continuity and crisis management",
                "Business continuity, including backup management, disaster recovery and crisis management.", "Driftskontinuitet og krisestyring",
                "Driftskontinuitet, herunder backupstyring, reetablering efter en katastrofe og krisestyring."),
            D(5, DanishMeasures, "§ 6, stk. 1, nr. 4", "Art. 21(2)(d)", "Supply chain security",
                "Supply chain security, including the relationships with direct suppliers and service providers.", "Forsyningskædesikkerhed",
                "Forsyningskædesikkerhed, herunder forholdet til direkte leverandører og tjenesteudbydere."),
            D(6, DanishMeasures, "§ 6, stk. 1, nr. 5", "Art. 21(2)(e)", "Security in acquisition, development and maintenance",
                "Security in acquiring, developing and maintaining network and information systems, including vulnerability handling and disclosure.",
                "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse",
                "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse af net- og informationssystemer, herunder håndtering og offentliggørelse af sårbarheder."),
            D(7, DanishMeasures, "§ 6, stk. 1, nr. 6", "Art. 21(2)(f)", "Assessing the effectiveness of the measures",
                "Policies and procedures to assess the effectiveness of the measures.", "Vurdering af foranstaltningernes effektivitet",
                "Politikker og procedurer til at vurdere effektiviteten af foranstaltningerne."),
            D(8, DanishMeasures, "§ 6, stk. 1, nr. 7", "Art. 21(2)(g)", "Basic cyber hygiene and cybersecurity training",
                "Basic cyber hygiene practices and cybersecurity training.", "Grundlæggende cyberhygiejne og cybersikkerhedsuddannelse",
                "Grundlæggende cyberhygiejnepraksis og cybersikkerhedsuddannelse."),
            D(9, DanishMeasures, "§ 6, stk. 1, nr. 8", "Art. 21(2)(h)", "Cryptography and encryption",
                "Policies and procedures on cryptography and, where relevant, encryption.", "Kryptografi og kryptering",
                "Politikker og procedurer for brug af kryptografi og, hvor det er relevant, kryptering."),
            D(10, DanishMeasures, "§ 6, stk. 1, nr. 9", "Art. 21(2)(i)", "Human resources security, access control and asset management",
                "Human resources security, access control policies and asset management.", "Personalesikkerhed, adgangskontrol og forvaltning af aktiver",
                "Personalesikkerhed, politikker for adgangskontrol og forvaltning af aktiver."),
            D(11, DanishMeasures, "§ 6, stk. 1, nr. 10", "Art. 21(2)(j)", "Multi-factor authentication and secured communications",
                "Multi-factor or continuous authentication, secured voice, video and text communication and secured internal emergency communication, where relevant.",
                "Multifaktorautentificering og sikret kommunikation",
                "Multifaktor- eller kontinuerlig autentificering, sikret tale-, video- og tekstkommunikation og sikrede interne nødkommunikationssystemer, hvor det er relevant."),
            D(12, DanishMeasures, "§ 6, stk. 2", "Art. 21(4)", "Corrective measures without undue delay",
                "An entity that does not meet the requirements for the measures takes the necessary corrective measures without undue delay.",
                "Korrigerende foranstaltninger uden unødigt ophold",
                "En enhed, der ikke overholder kravene til foranstaltningerne, træffer uden unødigt ophold de nødvendige korrigerende foranstaltninger."),
            D(13, DanishMeasures, "§ 7, stk. 1", "Art. 20(1)", "Management approves and oversees the measures",
                "The entity's management body approves the measures and oversees that they are carried out.",
                "Ledelsen godkender og fører tilsyn med foranstaltningerne",
                "Enhedens ledelsesorgan godkender foranstaltningerne og fører tilsyn med, at de gennemføres."),
            D(14, DanishMeasures, "§ 7, stk. 2", "Art. 20(2)", "Management takes cybersecurity courses",
                "Members of the management body take relevant courses in managing cybersecurity risks and encourage similar courses for the other employees.",
                "Ledelsen deltager i cybersikkerhedskurser",
                "Ledelsens medlemmer deltager i relevante kurser om styring af cybersikkerhedsrisici og tilskynder til, at de øvrige ansatte tilbydes tilsvarende kurser."),

            D(15, DanishRegistration, "§ 9", "Art. 27(2)", "Registration of digital providers within 3 months",
                "DNS, top-level domain and domain registration services, cloud, data centre, content delivery network, managed (security) service providers, online marketplaces, search engines and social networking platforms register with the competent authority within 3 months of being covered, and report changes within 3 months.",
                "Registrering af digitale udbydere inden for 3 måneder",
                "Udbydere af DNS, topdomæner, domænenavnsregistrering, cloud, datacentre, indholdsleveringsnetværk, administrerede (sikkerheds)tjenester, onlinemarkedspladser, søgemaskiner og sociale netværk registrerer sig hos den kompetente myndighed senest 3 måneder efter, at de er omfattet, og melder ændringer inden for 3 måneder."),
            D(16, DanishRegistration, "§ 10", "Art. 3(4)", "Registration within 2 weeks",
                "Essential and important entities register with the competent authority (on virk.dk) within 2 weeks of being covered, with name, address and contact details including IP ranges, sector and sub-sector, and the other EU countries served, and report changes within 2 weeks.",
                "Registrering inden for 2 uger",
                "Væsentlige og vigtige enheder registrerer sig hos den kompetente myndighed (på virk.dk) senest 2 uger efter, at de er omfattet, med navn, adresse og kontaktoplysninger inkl. ip-intervaller, sektor og delsektor og de øvrige EU-lande, der betjenes, og melder ændringer inden for 2 uger."),

            D(17, DanishReporting, "§ 12", "Art. 23(1)", "Notify significant incidents",
                "Essential and important entities notify the competent authority and the CSIRT of every significant incident: one that has caused or can cause serious disruption or financial loss, or considerable damage to others.",
                "Underret om væsentlige hændelser",
                "Væsentlige og vigtige enheder underretter den kompetente myndighed og CSIRT'en om enhver væsentlig hændelse: en hændelse, der har forårsaget eller kan forårsage alvorlige driftsforstyrrelser eller økonomiske tab, eller betydelig skade for andre."),
            D(18, DanishReporting, "§ 13, stk. 1, nr. 1", "Art. 23(4)(a)", "Early warning within 24 hours",
                "An early warning without undue delay and within 24 hours of becoming aware of the significant incident.",
                "Tidlig varsling inden for 24 timer",
                "En tidlig varsling uden unødigt ophold og senest 24 timer efter, at enheden har fået kendskab til den væsentlige hændelse."),
            D(19, DanishReporting, "§ 13, stk. 1, nr. 2", "Art. 23(4)(b)", "Incident notification within 72 hours",
                "An incident notification within 72 hours of becoming aware of the incident, with an initial assessment (24 hours for trust service providers, stk. 2).",
                "Hændelsesunderretning inden for 72 timer",
                "En hændelsesunderretning senest 72 timer efter kendskab til hændelsen med en indledende vurdering (24 timer for tillidstjenesteudbydere, stk. 2)."),
            D(20, DanishReporting, "§ 13, stk. 1, nr. 3", "Art. 23(4)(c)", "Intermediate report on request",
                "An intermediate report with relevant status updates when the CSIRT asks for one.",
                "Foreløbig rapport efter anmodning",
                "En foreløbig rapport med relevante statusopdateringer efter anmodning fra CSIRT'en."),
            D(21, DanishReporting, "§ 13, stk. 1, nr. 4", "Art. 23(4)(d)", "Final report within one month",
                "A final report no later than one month after the incident notification, with a detailed description, the likely root cause, the mitigation and any cross-border impact.",
                "Endelig rapport inden for en måned",
                "En endelig rapport senest en måned efter hændelsesunderretningen med en detaljeret beskrivelse, den sandsynlige årsag, de afbødende foranstaltninger og eventuel grænseoverskridende virkning."),
            D(22, DanishReporting, "§ 15, stk. 1", "Art. 23(1)", "Inform recipients of significant incidents",
                "Essential and important entities inform the recipients of their services without undue delay of significant incidents likely to affect the services.",
                "Orienter modtagere om væsentlige hændelser",
                "Væsentlige og vigtige enheder underretter uden unødigt ophold modtagerne af deres tjenester om væsentlige hændelser, der sandsynligvis påvirker leveringen negativt."),
            D(23, DanishReporting, "§ 15, stk. 2", "Art. 23(2)", "Inform recipients of significant cyber threats",
                "Essential and important entities tell the recipients potentially affected by a significant cyber threat which measures they can take, and about the threat where relevant.",
                "Orienter modtagere om væsentlige cybertrusler",
                "Væsentlige og vigtige enheder oplyser modtagere, der kan blive berørt af en væsentlig cybertrussel, om de foranstaltninger, de kan træffe, og om truslen, hvor det er relevant.")
        ];

        private static Item D(int number, string group, string code, string implements, string title, string description, string danishTitle, string danishDescription)
            => new(Id("06", number), number, group, code, title, description, DanishDocument, implements, danishTitle, danishDescription, TextId("06", number));

        // ---------------------------------------------------------- Implementing Regulation (EU) 2024/2690

        public const string TechnicalMeasures = "Technical and methodological requirements (Annex)";

        private const string TechnicalDocument = "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690";

        public static readonly IReadOnlyList<Item> Technical =
        [
            T(1, "Annex 1", "Art. 21(2)(a)", "Policy on the security of network and information systems",
                "A security policy approved by management, with roles and responsibilities, reviewed at least yearly and after significant incidents or changes.",
                "Politik for sikkerhed i net- og informationssystemer",
                "En sikkerhedspolitik godkendt af ledelsen med roller og ansvar, som gennemgås mindst årligt og efter væsentlige hændelser eller ændringer."),
            T(2, "Annex 2", "Art. 21(2)(a)", "Risk management policy",
                "A risk management framework: identify, analyse, evaluate and treat risks, a risk treatment plan with owners, compliance monitoring and independent review.",
                "Politik for risikostyring",
                "En ramme for risikostyring: identificér, analysér, vurdér og behandl risici, en risikobehandlingsplan med ejere, overvågning af overholdelse og uafhængig gennemgang."),
            T(3, "Annex 3", "Art. 21(2)(b)", "Incident handling",
                "An incident handling policy, monitoring and logging, event reporting, assessment and classification of events, response, and post-incident reviews.",
                "Håndtering af hændelser",
                "En politik for hændelseshåndtering, overvågning og logning, rapportering, vurdering og klassificering af hændelser, respons og evaluering efter hændelser."),
            T(4, "Annex 4", "Art. 21(2)(c)", "Business continuity and crisis management",
                "A business continuity and disaster recovery plan, backup and redundancy management, and a crisis management process.",
                "Driftskontinuitet og krisestyring",
                "En plan for driftskontinuitet og reetablering, styring af backup og redundans og en proces for krisestyring."),
            T(5, "Annex 5", "Art. 21(2)(d)", "Supply chain security",
                "A supply chain security policy and a directory of suppliers and service providers, with security requirements in contracts.",
                "Forsyningskædesikkerhed",
                "En politik for forsyningskædesikkerhed og en fortegnelse over leverandører og tjenesteudbydere med sikkerhedskrav i kontrakterne."),
            T(6, "Annex 6", "Art. 21(2)(e)", "Security in acquisition, development and maintenance",
                "Security in acquiring ICT services and products, a secure development life cycle, configuration and change management, security testing, patch management, network security and segmentation, protection against malicious software, and vulnerability handling and disclosure.",
                "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse",
                "Sikkerhed ved indkøb af ikt-tjenester og -produkter, en sikker udviklingslivscyklus, konfigurations- og ændringsstyring, sikkerhedstest, patchstyring, netværkssikkerhed og segmentering, beskyttelse mod skadelig software og håndtering og offentliggørelse af sårbarheder."),
            T(7, "Annex 7", "Art. 21(2)(f)", "Assessing the effectiveness of the measures",
                "Policies and procedures to assess whether the cybersecurity risk-management measures are effective, with what is monitored and measured, how and when.",
                "Vurdering af foranstaltningernes effektivitet",
                "Politikker og procedurer til at vurdere, om foranstaltningerne virker, med hvad der overvåges og måles, hvordan og hvornår."),
            T(8, "Annex 8", "Art. 21(2)(g)", "Basic cyber hygiene and security training",
                "Awareness raising and basic cyber hygiene for all employees, and security training for those whose roles need it, reviewed regularly.",
                "Grundlæggende cyberhygiejne og sikkerhedsuddannelse",
                "Bevidstgørelse og grundlæggende cyberhygiejne for alle ansatte og sikkerhedsuddannelse for dem, hvis roller kræver det, gennemgået jævnligt."),
            T(9, "Annex 9", "Art. 21(2)(h)", "Cryptography",
                "A policy on cryptography: when and how encryption is used, the algorithms and protocols, and key management.",
                "Kryptografi",
                "En politik for kryptografi: hvornår og hvordan der krypteres, algoritmer og protokoller og nøglehåndtering."),
            T(10, "Annex 10", "Art. 21(2)(i)", "Human resources security",
                "Employees understand their security responsibilities, background checks where relevant, procedures for leaving or changing roles, and a disciplinary process.",
                "Personalesikkerhed",
                "Ansatte forstår deres sikkerhedsansvar, baggrundstjek hvor det er relevant, procedurer ved fratræden eller nye roller og en disciplinær proces."),
            T(11, "Annex 11", "Art. 21(2)(i)", "Access control",
                "An access control policy, management of access rights and privileged accounts, administration systems, identification, authentication (including multi-factor) and regular review of access.",
                "Adgangskontrol",
                "En politik for adgangskontrol, styring af adgangsrettigheder og privilegerede konti, administrationssystemer, identifikation, autentificering (også multifaktor) og jævnlig gennemgang af adgange."),
            T(12, "Annex 12", "Art. 21(2)(i)", "Asset management",
                "Classification of assets, handling of assets and removable media, an inventory of assets, and the return or deletion of assets when employment ends.",
                "Forvaltning af aktiver",
                "Klassificering af aktiver, håndtering af aktiver og flytbare medier, en fortegnelse over aktiver og aflevering eller sletning, når ansættelsen ophører."),
            T(13, "Annex 13", "Art. 21(1)", "Environmental and physical security",
                "Protection against the loss of supporting utilities, physical and environmental threats, and physical access control to the premises.",
                "Fysisk og miljømæssig sikkerhed",
                "Beskyttelse mod svigt i forsyninger, fysiske og miljømæssige trusler og fysisk adgangskontrol til lokalerne.")
        ];

        private static Item T(int number, string code, string implements, string title, string description, string danishTitle, string danishDescription)
            => new(Id("07", number), number, TechnicalMeasures, code, title, description, $"{TechnicalDocument}#anx_1", implements,
                danishTitle, danishDescription, TextId("07", number));

        private static Guid Id(string catalogue, int number) => Guid.Parse($"6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d{catalogue}{number:D2}");

        private static Guid TextId(string catalogue, int number) => Guid.Parse($"6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e{catalogue}{number:D2}");
    }
}
