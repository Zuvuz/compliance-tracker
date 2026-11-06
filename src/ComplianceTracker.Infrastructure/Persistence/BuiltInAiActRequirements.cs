namespace ComplianceTracker.Infrastructure.Persistence
{
    // The requirements of the built-in AI Act (US-062): Regulation (EU) 2024/1689, for teams that build or deploy AI
    // systems. Added to version 1 by a migration (seed data), with fixed ids so they are the same in every database.
    //
    // Unlike the CRA (US-043), the title and the description are a summary in our own words, not the official text (B58).
    // Each requirement links to its article on EUR-Lex, where the binding text is; the "Official text" link on the
    // Compliance tab opens it. Where an article applies later than the regulation's start, the description says when.
    // The obligations for general-purpose AI models (Chapter V) are left out: they are for model providers (R7).
    // A change in the regulation becomes a new version (US-035 R7), never an edit of these rows.
    public static class BuiltInAiActRequirements
    {
        public const string Literacy = "AI literacy";
        public const string Prohibited = "Prohibited AI practices";
        public const string HighRiskRequirements = "High-risk AI systems – requirements";
        public const string HighRiskProviders = "High-risk AI systems – provider obligations";
        public const string Transparency = "Transparency";

        private const string Document = "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689";

        private const string From2025 = "Applies from 2 February 2025. ";
        private const string HighRisk = "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). ";
        private const string From2026 = "Applies from 2 August 2026. ";
        private const string Prohibits = "It is prohibited to place on the market, put into service or use an AI system that ";

        public record Item(Guid Id, int Position, string Group, string Code, string Title, string Description, string Reference);

        public static readonly IReadOnlyList<Item> All =
        [
            Of(1, Literacy, "Art. 4", 4, "AI literacy of staff",
                From2025 + "Providers and deployers take measures to ensure, to their best extent, a sufficient level of AI literacy of their staff and others operating or using AI systems on their behalf, taking into account their knowledge, experience, education and training, the context of use and the persons the systems are used on."),

            Of(2, Prohibited, "Art. 5(1)(a)", 5, "No manipulative or deceptive techniques",
                From2025 + Prohibits + "deploys subliminal, purposefully manipulative or deceptive techniques that materially distort a person's behaviour by impairing their ability to make an informed decision, causing or likely causing significant harm."),
            Of(3, Prohibited, "Art. 5(1)(b)", 5, "No exploitation of vulnerabilities",
                From2025 + Prohibits + "exploits the vulnerabilities of a person or group due to their age, disability or social or economic situation, to materially distort their behaviour in a way that causes or is likely to cause significant harm."),
            Of(4, Prohibited, "Art. 5(1)(c)", 5, "No social scoring",
                From2025 + Prohibits + "evaluates or classifies persons over time based on their social behaviour or personal characteristics, where the score leads to detrimental or unfavourable treatment in unrelated contexts or that is unjustified or disproportionate."),
            Of(5, Prohibited, "Art. 5(1)(d)", 5, "No crime prediction from profiling alone",
                From2025 + Prohibits + "assesses or predicts the risk of a person committing a criminal offence based solely on profiling or on their personality traits and characteristics (not when it supports a human assessment based on objective, verifiable facts)."),
            Of(6, Prohibited, "Art. 5(1)(e)", 5, "No untargeted scraping of facial images",
                From2025 + Prohibits + "creates or expands facial recognition databases through untargeted scraping of facial images from the internet or CCTV footage."),
            Of(7, Prohibited, "Art. 5(1)(f)", 5, "No emotion recognition at work or in education",
                From2025 + Prohibits + "infers the emotions of a person in the workplace or in education institutions, except for medical or safety reasons."),
            Of(8, Prohibited, "Art. 5(1)(g)", 5, "No biometric categorisation of sensitive traits",
                From2025 + Prohibits + "categorises persons based on their biometric data to deduce or infer their race, political opinions, trade union membership, religious or philosophical beliefs, sex life or sexual orientation (labelling or filtering of lawfully acquired biometric datasets excepted)."),
            Of(9, Prohibited, "Art. 5(1)(h)", 5, "No real-time remote biometric identification for law enforcement",
                From2025 + "It is prohibited to use real-time remote biometric identification systems in publicly accessible spaces for law enforcement, unless strictly necessary for one of the objectives the Article lists (such as searching for victims, preventing an imminent threat or locating suspects of serious crimes) and under its safeguards."),

            Of(10, HighRiskRequirements, "Art. 9", 9, "Risk management system",
                HighRisk + "A risk management system is established, implemented, documented and maintained throughout the lifecycle of the high-risk AI system: known and foreseeable risks to health, safety and fundamental rights are identified, estimated and evaluated, appropriate risk management measures are taken, and the system is tested against defined metrics."),
            Of(11, HighRiskRequirements, "Art. 10", 10, "Data and data governance",
                HighRisk + "Training, validation and testing data sets are subject to appropriate data governance and management practices, are relevant, sufficiently representative and, to the best extent possible, free of errors and complete for the intended purpose, and are examined for possible biases."),
            Of(12, HighRiskRequirements, "Art. 11", 11, "Technical documentation",
                HighRisk + "Technical documentation (Annex IV) is drawn up before the system is placed on the market or put into service, kept up to date, and shows how the system meets the requirements."),
            Of(13, HighRiskRequirements, "Art. 12", 12, "Record-keeping (logs)",
                HighRisk + "The system technically allows the automatic recording of events (logs) over its lifetime, so that its functioning can be traced, risks identified and post-market monitoring carried out."),
            Of(14, HighRiskRequirements, "Art. 13", 13, "Transparency and instructions for use",
                HighRisk + "The system is designed so its operation is sufficiently transparent for deployers to interpret its output and use it appropriately, and it comes with instructions for use that state its purpose, accuracy, known risks, human oversight measures and maintenance."),
            Of(15, HighRiskRequirements, "Art. 14", 14, "Human oversight",
                HighRisk + "The system is designed so it can be effectively overseen by natural persons while in use, with measures that let them understand its capacities and limitations, avoid over-reliance on its output, and decide not to use it, override it or stop it."),
            Of(16, HighRiskRequirements, "Art. 15", 15, "Accuracy, robustness and cybersecurity",
                HighRisk + "The system achieves an appropriate level of accuracy, robustness and cybersecurity and performs consistently throughout its lifecycle; it is resilient against errors, faults and attempts by third parties to alter its use or performance (such as data poisoning or adversarial examples)."),

            Of(17, HighRiskProviders, "Art. 16", 16, "Obligations of providers",
                HighRisk + "Providers of high-risk AI systems ensure the system meets the requirements of Articles 8 to 15, give their name and contact address, have a quality management system, keep documentation and logs, carry out the conformity assessment, draw up the EU declaration of conformity, affix the CE marking, register the system, take corrective actions and meet accessibility requirements."),
            Of(18, HighRiskProviders, "Art. 17", 17, "Quality management system",
                HighRisk + "Providers put a documented quality management system in place, with policies and procedures for regulatory compliance, design, development, testing, data management, risk management, post-market monitoring, incident reporting, record-keeping and accountability."),
            Of(19, HighRiskProviders, "Art. 18", 18, "Keeping the documentation",
                HighRisk + "Providers keep the technical documentation, the quality management system documentation, approved changes, notified body decisions and the EU declaration of conformity available to the authorities for 10 years after the system is placed on the market."),
            Of(20, HighRiskProviders, "Art. 19", 19, "Keeping the logs",
                HighRisk + "Providers keep the logs automatically generated by the system, to the extent they control them, for a period appropriate to the intended purpose and at least six months."),
            Of(21, HighRiskProviders, "Art. 20", 20, "Corrective actions and duty of information",
                HighRisk + "Providers who consider that a system they placed on the market does not conform immediately take corrective action (bring it into conformity, withdraw, disable or recall it), inform distributors and deployers, and inform the authorities when it presents a risk."),
            Of(22, HighRiskProviders, "Art. 43", 43, "Conformity assessment",
                HighRisk + "Before the system is placed on the market, the provider follows the conformity assessment procedure that applies: internal control (Annex VI) or assessment with a notified body (Annex VII), or the procedure of the Union harmonisation legislation in Annex I; substantial modifications need a new assessment."),
            Of(23, HighRiskProviders, "Art. 47", 47, "EU declaration of conformity",
                HighRisk + "The provider draws up a written, machine-readable EU declaration of conformity (Annex V) for each high-risk AI system, keeps it for 10 years and keeps it up to date."),
            Of(24, HighRiskProviders, "Art. 48", 48, "CE marking",
                HighRisk + "The CE marking is affixed visibly, legibly and indelibly to the high-risk AI system, or digitally for systems provided digitally, with the identification number of the notified body where one was involved."),
            Of(25, HighRiskProviders, "Art. 49", 49, "Registration in the EU database",
                HighRisk + "Before placing a high-risk AI system listed in Annex III on the market or putting it into service, the provider registers itself and the system in the EU database (Article 71)."),
            Of(26, HighRiskProviders, "Art. 72", 72, "Post-market monitoring",
                HighRisk + "Providers establish and document a post-market monitoring system, proportionate to the risks, that actively collects and analyses data on the system's performance throughout its lifetime, based on a post-market monitoring plan."),
            Of(27, HighRiskProviders, "Art. 73", 73, "Reporting serious incidents",
                HighRisk + "Providers report any serious incident to the market surveillance authorities immediately after establishing a causal link, and no later than 15 days after becoming aware of it (shorter for widespread infringements and deaths), and investigate it."),

            Of(28, Transparency, "Art. 50(1)", 50, "Tell people they interact with an AI system",
                From2026 + "Providers design AI systems that interact directly with persons so that the persons are informed they are interacting with an AI system, unless this is obvious from the context."),
            Of(29, Transparency, "Art. 50(2)", 50, "Mark AI-generated content",
                From2026 + "Providers of AI systems that generate synthetic audio, image, video or text ensure the outputs are marked in a machine-readable format and detectable as artificially generated or manipulated."),
            Of(30, Transparency, "Art. 50(3)", 50, "Inform people exposed to emotion recognition or biometric categorisation",
                From2026 + "Deployers of an emotion recognition system or a biometric categorisation system inform the persons exposed to it of its operation and process personal data in line with the data protection rules."),
            Of(31, Transparency, "Art. 50(4)", 50, "Disclose deep fakes and AI-generated public texts",
                From2026 + "Deployers of an AI system that generates or manipulates deep fake image, audio or video content disclose that it has been artificially generated or manipulated; the same applies to AI-generated text published to inform the public on matters of public interest, unless a person has reviewed it and holds editorial responsibility.")
        ];

        private static Item Of(int number, string group, string code, int article, string title, string description)
            => new(Id(number), number, group, code, title, description, $"{Document}#art_{article}");

        private static Guid Id(int number) => Guid.Parse($"6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d04{number:D2}");
    }
}
