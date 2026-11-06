using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiActRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Requirements",
                columns: new[] { "Id", "Code", "Description", "FrameworkVersionId", "Group", "Position", "Reference", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0401"), "Art. 4", "Applies from 2 February 2025. Providers and deployers take measures to ensure, to their best extent, a sufficient level of AI literacy of their staff and others operating or using AI systems on their behalf, taking into account their knowledge, experience, education and training, the context of use and the persons the systems are used on.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "AI literacy", 1, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_4", "AI literacy of staff" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0402"), "Art. 5(1)(a)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that deploys subliminal, purposefully manipulative or deceptive techniques that materially distort a person's behaviour by impairing their ability to make an informed decision, causing or likely causing significant harm.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 2, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No manipulative or deceptive techniques" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0403"), "Art. 5(1)(b)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that exploits the vulnerabilities of a person or group due to their age, disability or social or economic situation, to materially distort their behaviour in a way that causes or is likely to cause significant harm.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 3, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No exploitation of vulnerabilities" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0404"), "Art. 5(1)(c)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that evaluates or classifies persons over time based on their social behaviour or personal characteristics, where the score leads to detrimental or unfavourable treatment in unrelated contexts or that is unjustified or disproportionate.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 4, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No social scoring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0405"), "Art. 5(1)(d)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that assesses or predicts the risk of a person committing a criminal offence based solely on profiling or on their personality traits and characteristics (not when it supports a human assessment based on objective, verifiable facts).", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 5, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No crime prediction from profiling alone" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0406"), "Art. 5(1)(e)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that creates or expands facial recognition databases through untargeted scraping of facial images from the internet or CCTV footage.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 6, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No untargeted scraping of facial images" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0407"), "Art. 5(1)(f)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that infers the emotions of a person in the workplace or in education institutions, except for medical or safety reasons.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 7, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No emotion recognition at work or in education" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0408"), "Art. 5(1)(g)", "Applies from 2 February 2025. It is prohibited to place on the market, put into service or use an AI system that categorises persons based on their biometric data to deduce or infer their race, political opinions, trade union membership, religious or philosophical beliefs, sex life or sexual orientation (labelling or filtering of lawfully acquired biometric datasets excepted).", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 8, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No biometric categorisation of sensitive traits" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0409"), "Art. 5(1)(h)", "Applies from 2 February 2025. It is prohibited to use real-time remote biometric identification systems in publicly accessible spaces for law enforcement, unless strictly necessary for one of the objectives the Article lists (such as searching for victims, preventing an imminent threat or locating suspects of serious crimes) and under its safeguards.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Prohibited AI practices", 9, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_5", "No real-time remote biometric identification for law enforcement" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0410"), "Art. 9", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). A risk management system is established, implemented, documented and maintained throughout the lifecycle of the high-risk AI system: known and foreseeable risks to health, safety and fundamental rights are identified, estimated and evaluated, appropriate risk management measures are taken, and the system is tested against defined metrics.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 10, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_9", "Risk management system" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0411"), "Art. 10", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Training, validation and testing data sets are subject to appropriate data governance and management practices, are relevant, sufficiently representative and, to the best extent possible, free of errors and complete for the intended purpose, and are examined for possible biases.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 11, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_10", "Data and data governance" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0412"), "Art. 11", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Technical documentation (Annex IV) is drawn up before the system is placed on the market or put into service, kept up to date, and shows how the system meets the requirements.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 12, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_11", "Technical documentation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0413"), "Art. 12", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). The system technically allows the automatic recording of events (logs) over its lifetime, so that its functioning can be traced, risks identified and post-market monitoring carried out.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 13, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_12", "Record-keeping (logs)" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0414"), "Art. 13", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). The system is designed so its operation is sufficiently transparent for deployers to interpret its output and use it appropriately, and it comes with instructions for use that state its purpose, accuracy, known risks, human oversight measures and maintenance.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 14, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_13", "Transparency and instructions for use" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0415"), "Art. 14", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). The system is designed so it can be effectively overseen by natural persons while in use, with measures that let them understand its capacities and limitations, avoid over-reliance on its output, and decide not to use it, override it or stop it.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 15, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_14", "Human oversight" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0416"), "Art. 15", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). The system achieves an appropriate level of accuracy, robustness and cybersecurity and performs consistently throughout its lifecycle; it is resilient against errors, faults and attempts by third parties to alter its use or performance (such as data poisoning or adversarial examples).", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – requirements", 16, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_15", "Accuracy, robustness and cybersecurity" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0417"), "Art. 16", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers of high-risk AI systems ensure the system meets the requirements of Articles 8 to 15, give their name and contact address, have a quality management system, keep documentation and logs, carry out the conformity assessment, draw up the EU declaration of conformity, affix the CE marking, register the system, take corrective actions and meet accessibility requirements.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 17, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_16", "Obligations of providers" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0418"), "Art. 17", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers put a documented quality management system in place, with policies and procedures for regulatory compliance, design, development, testing, data management, risk management, post-market monitoring, incident reporting, record-keeping and accountability.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 18, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_17", "Quality management system" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0419"), "Art. 18", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers keep the technical documentation, the quality management system documentation, approved changes, notified body decisions and the EU declaration of conformity available to the authorities for 10 years after the system is placed on the market.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 19, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_18", "Keeping the documentation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0420"), "Art. 19", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers keep the logs automatically generated by the system, to the extent they control them, for a period appropriate to the intended purpose and at least six months.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 20, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_19", "Keeping the logs" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0421"), "Art. 20", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers who consider that a system they placed on the market does not conform immediately take corrective action (bring it into conformity, withdraw, disable or recall it), inform distributors and deployers, and inform the authorities when it presents a risk.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 21, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_20", "Corrective actions and duty of information" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0422"), "Art. 43", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Before the system is placed on the market, the provider follows the conformity assessment procedure that applies: internal control (Annex VI) or assessment with a notified body (Annex VII), or the procedure of the Union harmonisation legislation in Annex I; substantial modifications need a new assessment.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 22, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_43", "Conformity assessment" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0423"), "Art. 47", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). The provider draws up a written, machine-readable EU declaration of conformity (Annex V) for each high-risk AI system, keeps it for 10 years and keeps it up to date.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 23, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_47", "EU declaration of conformity" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0424"), "Art. 48", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). The CE marking is affixed visibly, legibly and indelibly to the high-risk AI system, or digitally for systems provided digitally, with the identification number of the notified body where one was involved.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 24, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_48", "CE marking" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0425"), "Art. 49", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Before placing a high-risk AI system listed in Annex III on the market or putting it into service, the provider registers itself and the system in the EU database (Article 71).", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 25, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_49", "Registration in the EU database" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0426"), "Art. 72", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers establish and document a post-market monitoring system, proportionate to the risks, that actively collects and analyses data on the system's performance throughout its lifetime, based on a post-market monitoring plan.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 26, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_72", "Post-market monitoring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0427"), "Art. 73", "Applies from 2 August 2026 (2 August 2027 for high-risk AI systems under Article 6(1) and Annex I). Providers report any serious incident to the market surveillance authorities immediately after establishing a causal link, and no later than 15 days after becoming aware of it (shorter for widespread infringements and deaths), and investigate it.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "High-risk AI systems – provider obligations", 27, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_73", "Reporting serious incidents" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0428"), "Art. 50(1)", "Applies from 2 August 2026. Providers design AI systems that interact directly with persons so that the persons are informed they are interacting with an AI system, unless this is obvious from the context.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Transparency", 28, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_50", "Tell people they interact with an AI system" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0429"), "Art. 50(2)", "Applies from 2 August 2026. Providers of AI systems that generate synthetic audio, image, video or text ensure the outputs are marked in a machine-readable format and detectable as artificially generated or manipulated.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Transparency", 29, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_50", "Mark AI-generated content" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0430"), "Art. 50(3)", "Applies from 2 August 2026. Deployers of an emotion recognition system or a biometric categorisation system inform the persons exposed to it of its operation and process personal data in line with the data protection rules.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Transparency", 30, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_50", "Inform people exposed to emotion recognition or biometric categorisation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0431"), "Art. 50(4)", "Applies from 2 August 2026. Deployers of an AI system that generates or manipulates deep fake image, audio or video content disclose that it has been artificially generated or manipulated; the same applies to AI-generated text published to inform the public on matters of public interest, unless a person has reviewed it and holds editorial responsibility.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "Transparency", 31, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202401689#art_50", "Disclose deep fakes and AI-generated public texts" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0401"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0402"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0403"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0404"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0405"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0406"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0407"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0408"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0409"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0410"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0411"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0412"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0413"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0414"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0415"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0416"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0417"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0418"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0419"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0420"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0421"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0422"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0423"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0424"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0425"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0426"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0427"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0428"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0429"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0430"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0431"));
        }
    }
}
