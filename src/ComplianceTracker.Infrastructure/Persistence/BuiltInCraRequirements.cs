namespace ComplianceTracker.Infrastructure.Persistence
{
    // The requirements of the built-in CRA (US-043): Annex I of Regulation (EU) 2024/2847.
    // Added to version 1 by a migration (seed data), with fixed ids so they are the same in every database.
    //
    // The description is the official English text from EUR-Lex (OJ L, 2024/2847, 20.11.2024).
    // EU legislation may be reused when the source is given (Commission Decision 2011/833/EU); Reference is that source.
    // The points of Part I (2) and Part II share a lead-in ("... shall:"), which is put in front of each point,
    // so every requirement can be read on its own. The title is a short summary in our own words.
    // A change in the regulation becomes a new version (US-035 R7), never an edit of these rows.
    public static class BuiltInCraRequirements
    {
        public const string PartI = "Part I – Essential cybersecurity requirements";
        public const string PartII = "Part II – Vulnerability handling";

        public const string Reference = "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I";

        private const string PartIPoint2 = "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: ";
        private const string PartIILeadIn = "Manufacturers of products with digital elements shall: ";

        public record Item(Guid Id, int Position, string Group, string Code, string Title, string Description);

        public static readonly IReadOnlyList<Item> All =
        [
            new(Id(1), 1, PartI, "I.1", "Appropriate level of cybersecurity based on the risks",
                "Products with digital elements shall be designed, developed and produced in such a way that they ensure an appropriate level of cybersecurity based on the risks."),
            new(Id(2), 2, PartI, "I.2(a)", "No known exploitable vulnerabilities",
                PartIPoint2 + "be made available on the market without known exploitable vulnerabilities;"),
            new(Id(3), 3, PartI, "I.2(b)", "Secure by default configuration",
                PartIPoint2 + "be made available on the market with a secure by default configuration, unless otherwise agreed between manufacturer and business user in relation to a tailor-made product with digital elements, including the possibility to reset the product to its original state;"),
            new(Id(4), 4, PartI, "I.2(c)", "Security updates",
                PartIPoint2 + "ensure that vulnerabilities can be addressed through security updates, including, where applicable, through automatic security updates that are installed within an appropriate timeframe enabled as a default setting, with a clear and easy-to-use opt-out mechanism, through the notification of available updates to users, and the option to temporarily postpone them;"),
            new(Id(5), 5, PartI, "I.2(d)", "Protection from unauthorised access",
                PartIPoint2 + "ensure protection from unauthorised access by appropriate control mechanisms, including but not limited to authentication, identity or access management systems, and report on possible unauthorised access;"),
            new(Id(6), 6, PartI, "I.2(e)", "Confidentiality of data",
                PartIPoint2 + "protect the confidentiality of stored, transmitted or otherwise processed data, personal or other, such as by encrypting relevant data at rest or in transit by state of the art mechanisms, and by using other technical means;"),
            new(Id(7), 7, PartI, "I.2(f)", "Integrity of data, programs and configuration",
                PartIPoint2 + "protect the integrity of stored, transmitted or otherwise processed data, personal or other, commands, programs and configuration against any manipulation or modification not authorised by the user, and report on corruptions;"),
            new(Id(8), 8, PartI, "I.2(g)", "Data minimisation",
                PartIPoint2 + "process only data, personal or other, that are adequate, relevant and limited to what is necessary in relation to the intended purpose of the product with digital elements (data minimisation);"),
            new(Id(9), 9, PartI, "I.2(h)", "Availability of essential functions",
                PartIPoint2 + "protect the availability of essential and basic functions, also after an incident, including through resilience and mitigation measures against denial-of-service attacks;"),
            new(Id(10), 10, PartI, "I.2(i)", "Limited impact on other devices and networks",
                PartIPoint2 + "minimise the negative impact by the products themselves or connected devices on the availability of services provided by other devices or networks;"),
            new(Id(11), 11, PartI, "I.2(j)", "Limited attack surfaces",
                PartIPoint2 + "be designed, developed and produced to limit attack surfaces, including external interfaces;"),
            new(Id(12), 12, PartI, "I.2(k)", "Reduced impact of incidents",
                PartIPoint2 + "be designed, developed and produced to reduce the impact of an incident using appropriate exploitation mitigation mechanisms and techniques;"),
            new(Id(13), 13, PartI, "I.2(l)", "Security logging and monitoring",
                PartIPoint2 + "provide security related information by recording and monitoring relevant internal activity, including the access to or modification of data, services or functions, with an opt-out mechanism for the user;"),
            new(Id(14), 14, PartI, "I.2(m)", "Secure removal of data and settings",
                PartIPoint2 + "provide the possibility for users to securely and easily remove on a permanent basis all data and settings and, where such data can be transferred to other products or systems, ensure that this is done in a secure manner."),
            new(Id(15), 15, PartII, "II.1", "Identify and document vulnerabilities and components (SBOM)",
                PartIILeadIn + "identify and document vulnerabilities and components contained in products with digital elements, including by drawing up a software bill of materials in a commonly used and machine-readable format covering at the very least the top-level dependencies of the products;"),
            new(Id(16), 16, PartII, "II.2", "Remediate vulnerabilities without delay",
                PartIILeadIn + "in relation to the risks posed to products with digital elements, address and remediate vulnerabilities without delay, including by providing security updates; where technically feasible, new security updates shall be provided separately from functionality updates;"),
            new(Id(17), 17, PartII, "II.3", "Regular security tests and reviews",
                PartIILeadIn + "apply effective and regular tests and reviews of the security of the product with digital elements;"),
            new(Id(18), 18, PartII, "II.4", "Disclose fixed vulnerabilities",
                PartIILeadIn + "once a security update has been made available, share and publicly disclose information about fixed vulnerabilities, including a description of the vulnerabilities, information allowing users to identify the product with digital elements affected, the impacts of the vulnerabilities, their severity and clear and accessible information helping users to remediate the vulnerabilities; in duly justified cases, where manufacturers consider the security risks of publication to outweigh the security benefits, they may delay making public information regarding a fixed vulnerability until after users have been given the possibility to apply the relevant patch;"),
            new(Id(19), 19, PartII, "II.5", "Coordinated vulnerability disclosure policy",
                PartIILeadIn + "put in place and enforce a policy on coordinated vulnerability disclosure;"),
            new(Id(20), 20, PartII, "II.6", "Sharing information about vulnerabilities, with a contact address",
                PartIILeadIn + "take measures to facilitate the sharing of information about potential vulnerabilities in their product with digital elements as well as in third-party components contained in that product, including by providing a contact address for the reporting of the vulnerabilities discovered in the product with digital elements;"),
            new(Id(21), 21, PartII, "II.7", "Secure distribution of updates",
                PartIILeadIn + "provide for mechanisms to securely distribute updates for products with digital elements to ensure that vulnerabilities are fixed or mitigated in a timely manner and, where applicable for security updates, in an automatic manner;"),
            new(Id(22), 22, PartII, "II.8", "Security updates without delay and free of charge",
                PartIILeadIn + "ensure that, where security updates are available to address identified security issues, they are disseminated without delay and, unless otherwise agreed between a manufacturer and a business user in relation to a tailor-made product with digital elements, free of charge, accompanied by advisory messages providing users with the relevant information, including on potential action to be taken.")
        ];

        // Fixed ids: 6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c01NN
        private static Guid Id(int number) => Guid.Parse($"6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c01{number:D2}");
    }
}
