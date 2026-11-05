using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCraVersion2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "FrameworkVersions",
                columns: new[] { "Id", "ActivatedAt", "ActivatedById", "ActivatedRole", "AppliesFrom", "FileName", "FrameworkId", "Number", "Sha256", "SizeInBytes", "SourceReference", "Status", "UploadedAt", "UploadedById" },
                values: new object[] { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), new DateTime(2024, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2026, 9, 11), "", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"), 2, "", 0L, "https://eur-lex.europa.eu/eli/reg/2024/2847/oj", "Active", new DateTime(2024, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.InsertData(
                table: "Requirements",
                columns: new[] { "Id", "Code", "Description", "FrameworkVersionId", "Group", "Position", "Reference", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0201"), "I.1", "Products with digital elements shall be designed, developed and produced in such a way that they ensure an appropriate level of cybersecurity based on the risks.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 1, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Appropriate level of cybersecurity based on the risks" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0202"), "I.2(a)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be made available on the market without known exploitable vulnerabilities;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 2, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "No known exploitable vulnerabilities" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0203"), "I.2(b)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be made available on the market with a secure by default configuration, unless otherwise agreed between manufacturer and business user in relation to a tailor-made product with digital elements, including the possibility to reset the product to its original state;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 3, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Secure by default configuration" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0204"), "I.2(c)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: ensure that vulnerabilities can be addressed through security updates, including, where applicable, through automatic security updates that are installed within an appropriate timeframe enabled as a default setting, with a clear and easy-to-use opt-out mechanism, through the notification of available updates to users, and the option to temporarily postpone them;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 4, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Security updates" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0205"), "I.2(d)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: ensure protection from unauthorised access by appropriate control mechanisms, including but not limited to authentication, identity or access management systems, and report on possible unauthorised access;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 5, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Protection from unauthorised access" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0206"), "I.2(e)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: protect the confidentiality of stored, transmitted or otherwise processed data, personal or other, such as by encrypting relevant data at rest or in transit by state of the art mechanisms, and by using other technical means;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 6, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Confidentiality of data" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0207"), "I.2(f)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: protect the integrity of stored, transmitted or otherwise processed data, personal or other, commands, programs and configuration against any manipulation or modification not authorised by the user, and report on corruptions;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 7, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Integrity of data, programs and configuration" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0208"), "I.2(g)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: process only data, personal or other, that are adequate, relevant and limited to what is necessary in relation to the intended purpose of the product with digital elements (data minimisation);", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 8, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Data minimisation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0209"), "I.2(h)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: protect the availability of essential and basic functions, also after an incident, including through resilience and mitigation measures against denial-of-service attacks;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 9, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Availability of essential functions" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0210"), "I.2(i)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: minimise the negative impact by the products themselves or connected devices on the availability of services provided by other devices or networks;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 10, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Limited impact on other devices and networks" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0211"), "I.2(j)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be designed, developed and produced to limit attack surfaces, including external interfaces;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 11, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Limited attack surfaces" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0212"), "I.2(k)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be designed, developed and produced to reduce the impact of an incident using appropriate exploitation mitigation mechanisms and techniques;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 12, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Reduced impact of incidents" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0213"), "I.2(l)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: provide security related information by recording and monitoring relevant internal activity, including the access to or modification of data, services or functions, with an opt-out mechanism for the user;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 13, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Security logging and monitoring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0214"), "I.2(m)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: provide the possibility for users to securely and easily remove on a permanent basis all data and settings and, where such data can be transferred to other products or systems, ensure that this is done in a secure manner.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part I – Essential cybersecurity requirements", 14, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Secure removal of data and settings" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0215"), "II.1", "Manufacturers of products with digital elements shall: identify and document vulnerabilities and components contained in products with digital elements, including by drawing up a software bill of materials in a commonly used and machine-readable format covering at the very least the top-level dependencies of the products;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 15, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Identify and document vulnerabilities and components (SBOM)" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0216"), "II.2", "Manufacturers of products with digital elements shall: in relation to the risks posed to products with digital elements, address and remediate vulnerabilities without delay, including by providing security updates; where technically feasible, new security updates shall be provided separately from functionality updates;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 16, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Remediate vulnerabilities without delay" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0217"), "II.3", "Manufacturers of products with digital elements shall: apply effective and regular tests and reviews of the security of the product with digital elements;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 17, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Regular security tests and reviews" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0218"), "II.4", "Manufacturers of products with digital elements shall: once a security update has been made available, share and publicly disclose information about fixed vulnerabilities, including a description of the vulnerabilities, information allowing users to identify the product with digital elements affected, the impacts of the vulnerabilities, their severity and clear and accessible information helping users to remediate the vulnerabilities; in duly justified cases, where manufacturers consider the security risks of publication to outweigh the security benefits, they may delay making public information regarding a fixed vulnerability until after users have been given the possibility to apply the relevant patch;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 18, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Disclose fixed vulnerabilities" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0219"), "II.5", "Manufacturers of products with digital elements shall: put in place and enforce a policy on coordinated vulnerability disclosure;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 19, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Coordinated vulnerability disclosure policy" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0220"), "II.6", "Manufacturers of products with digital elements shall: take measures to facilitate the sharing of information about potential vulnerabilities in their product with digital elements as well as in third-party components contained in that product, including by providing a contact address for the reporting of the vulnerabilities discovered in the product with digital elements;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 20, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Sharing information about vulnerabilities, with a contact address" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0221"), "II.7", "Manufacturers of products with digital elements shall: provide for mechanisms to securely distribute updates for products with digital elements to ensure that vulnerabilities are fixed or mitigated in a timely manner and, where applicable for security updates, in an automatic manner;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 21, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Secure distribution of updates" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0222"), "II.8", "Manufacturers of products with digital elements shall: ensure that, where security updates are available to address identified security issues, they are disseminated without delay and, unless otherwise agreed between a manufacturer and a business user in relation to a tailor-made product with digital elements, free of charge, accompanied by advisory messages providing users with the relevant information, including on potential action to be taken.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Part II – Vulnerability handling", 22, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Security updates without delay and free of charge" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0223"), "Art.13(2)", "For the purpose of complying with paragraph 1, manufacturers shall undertake an assessment of the cybersecurity risks associated with a product with digital elements and take the outcome of that assessment into account during the planning, design, development, production, delivery and maintenance phases of the product with digital elements with a view to minimising cybersecurity risks, preventing incidents and minimising their impact, including in relation to the health and safety of users.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 23, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Cybersecurity risk assessment, used in every phase" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0224"), "Art.13(3)", "The cybersecurity risk assessment shall be documented and updated as appropriate during a support period to be determined in accordance with paragraph 8 of this Article. That cybersecurity risk assessment shall comprise at least an analysis of cybersecurity risks based on the intended purpose and reasonably foreseeable use, as well as the conditions of use, of the product with digital elements, such as the operational environment or the assets to be protected, taking into account the length of time the product is expected to be in use. The cybersecurity risk assessment shall indicate whether and, if so in what manner, the security requirements set out in Part I, point (2), of Annex I are applicable to the relevant product with digital elements and how those requirements are implemented as informed by the cybersecurity risk assessment. It shall also indicate how the manufacturer is to apply Part I, point (1), of Annex I and the vulnerability handling requirements set out in Part II of Annex I.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 24, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "The risk assessment is documented and kept up to date" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0225"), "Art.13(4)", "When placing a product with digital elements on the market, the manufacturer shall include the cybersecurity risk assessment referred to in paragraph 3 of this Article in the technical documentation required pursuant to Article 31 and Annex VII. For products with digital elements as referred to in Article 12, which are also subject to other Union legal acts, the cybersecurity risk assessment may be part of the risk assessment required by those Union legal acts. Where certain essential cybersecurity requirements are not applicable to the product with digital elements, the manufacturer shall include a clear justification to that effect in that technical documentation.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 25, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "The risk assessment is in the technical documentation, with reasons for what does not apply" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0226"), "Art.13(5)", "For the purpose of complying with paragraph 1, manufacturers shall exercise due diligence when integrating components sourced from third parties so that those components do not compromise the cybersecurity of the product with digital elements, including when integrating components of free and open-source software that have not been made available on the market in the course of a commercial activity.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 26, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Due diligence on components from third parties" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0227"), "Art.13(6)", "Manufacturers shall, upon identifying a vulnerability in a component, including in an open source-component, which is integrated in the product with digital elements report the vulnerability to the person or entity manufacturing or maintaining the component, and address and remediate the vulnerability in accordance with the vulnerability handling requirements set out in Part II of Annex I. Where manufacturers have developed a software or hardware modification to address the vulnerability in that component, they shall share the relevant code or documentation with the person or entity manufacturing or maintaining the component, where appropriate in a machine-readable format.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 27, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Vulnerabilities in a component are reported to its maintainer" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0228"), "Art.13(7)", "The manufacturers shall systematically document, in a manner that is proportionate to the nature and the cybersecurity risks, relevant cybersecurity aspects concerning the products with digital elements, including vulnerabilities of which they become aware and any relevant information provided by third parties, and shall, where applicable, update the cybersecurity risk assessment of the products.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 28, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Cybersecurity aspects and known vulnerabilities are documented" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0229"), "Art.13(8)", "Manufacturers shall ensure, when placing a product with digital elements on the market, and for the support period, that vulnerabilities of that product, including its components, are handled effectively and in accordance with the essential cybersecurity requirements set out in Part II of Annex I. Manufacturers shall determine the support period so that it reflects the length of time during which the product is expected to be in use, taking into account, in particular, reasonable user expectations, the nature of the product, including its intended purpose, as well as relevant Union law determining the lifetime of products with digital elements. When determining the support period, manufacturers may also take into account the support periods of products with digital elements offering a similar functionality placed on the market by other manufacturers, the availability of the operating environment, the support periods of integrated components that provide core functions and are sourced from third parties as well as relevant guidance provided by the dedicated administrative cooperation group (ADCO) established pursuant to Article 52(15) and the Commission. The matters to be taken into account in order to determine the support period shall be considered in a manner that ensures proportionality. Without prejudice to the second subparagraph, the support period shall be at least five years. Where the product with digital elements is expected to be in use for less than five years, the support period shall correspond to the expected use time. Manufacturers shall include the information that was taken into account to determine the support period of a product with digital elements in the technical documentation as set out in Annex VII. Manufacturers shall have appropriate policies and procedures, including coordinated vulnerability disclosure policies, referred to in Part II, point (5), of Annex I to process and remediate potential vulnerabilities in the product with digital elements reported from internal or external sources.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 29, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "A support period of at least five years, and how it was determined" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0230"), "Art.13(9)", "Manufacturers shall ensure that each security update, as referred to in Part II, point (8), of Annex I, which has been made available to users during the support period, remains available after it has been issued for a minimum of 10 years or for the remainder of the support period, whichever is longer.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 30, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Security updates stay available for at least 10 years" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0231"), "Art.13(12)", "Before placing a product with digital elements on the market, manufacturers shall draw up the technical documentation referred to in Article 31. They shall carry out the chosen conformity assessment procedures as referred to in Article 32 or have them carried out. Where compliance of the product with digital elements with the essential cybersecurity requirements set out in Part I of Annex I and of the processes put in place by the manufacturer with the essential cybersecurity requirements set out in Part II of Annex I has been demonstrated by that conformity assessment procedure, manufacturers shall draw up the EU declaration of conformity in accordance with Article 28 and affix the CE marking in accordance with Article 30.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 31, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Technical documentation, conformity assessment, declaration and CE marking before sale" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0232"), "Art.13(13)", "Manufacturers shall keep the technical documentation and the EU declaration of conformity at the disposal of the market surveillance authorities for at least 10 years after the product with digital elements has been placed on the market or for the support period, whichever is longer.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 32, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Documentation and declaration are kept for at least 10 years" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0233"), "Art.13(14)", "Manufacturers shall ensure that procedures are in place for products with digital elements that are part of a series of production to remain in conformity with this Regulation. Manufacturers shall adequately take into account changes in the development and production process or in the design or characteristics of the product with digital elements and changes in the harmonised standards, European cybersecurity certification schemes or common specifications as referred to in Article 27 by reference to which the conformity of the product with digital elements is declared or by application of which its conformity is verified.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 33, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Products made in series stay in conformity" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0234"), "Art.13(15)", "Manufacturers shall ensure that their products with digital elements bear a type, batch or serial number or other element allowing their identification, or, where that is not possible, that that information is provided on their packaging or in a document accompanying the product with digital elements.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 34, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "A type, batch or serial number identifies the product" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0235"), "Art.13(16)", "Manufacturers shall indicate the name, registered trade name or registered trademark of the manufacturer, and the postal address, email address or other digital contact details, as well as, where applicable, the website where the manufacturer can be contacted, on the product with digital elements, on its packaging or in a document accompanying the product with digital elements. That information shall also be included in the information and instructions to the user set out in Annex II. The contact details shall be in a language which can be easily understood by users and market surveillance authorities.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 35, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "The manufacturer's name and contact details" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0236"), "Art.13(17)", "For the purposes of this Regulation, manufacturers shall designate a single point of contact to enable users to communicate directly and rapidly with them, including in order to facilitate reporting on vulnerabilities of the product with digital elements. Manufacturers shall ensure that the single point of contact is easily identifiable by the users. They shall also include the single point of contact in the information and instructions to the user set out in Annex II. The single point of contact shall allow users to choose their preferred means of communication and shall not limit such means to automated tools.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 36, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "A single point of contact for users" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0237"), "Art.13(18)", "Manufacturers shall ensure that products with digital elements are accompanied by the information and instructions to the user set out in Annex II, in paper or electronic form. Such information and instructions shall be provided in a language which can be easily understood by users and market surveillance authorities. They shall be clear, understandable, intelligible and legible. They shall allow for the secure installation, operation and use of products with digital elements. Manufacturers shall keep the information and instructions to the user set out in Annex II at the disposal of users and market surveillance authorities for at least 10 years after the product with digital elements has been placed on the market or for the support period, whichever is longer. Where such information and instructions are provided online, manufacturers shall ensure that they are accessible, user-friendly and available online for at least 10 years after the product with digital elements has been placed on the market or for the support period, whichever is longer.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 37, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Information and instructions to the user, kept for at least 10 years" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0238"), "Art.13(19)", "Manufacturers shall ensure that the end date of the support period referred to in paragraph 8, including at least the month and the year, is clearly and understandably specified at the time of purchase in an easily accessible manner and, where applicable, on the product with digital elements, its packaging or by digital means. Where technically feasible in light of the nature of the product with digital elements, manufacturers shall display a notification to users informing them that their product with digital elements has reached the end of its support period.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 38, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "The end date of the support period is shown at purchase" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0239"), "Art.13(20)", "Manufacturers shall either provide a copy of the EU declaration of conformity or a simplified EU declaration of conformity with the product with digital elements. Where a simplified EU declaration of conformity is provided, it shall contain the exact internet address at which the full EU declaration of conformity can be accessed.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 39, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "A copy of the EU declaration of conformity comes with the product" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0240"), "Art.13(21)", "From the placing on the market and for the support period, manufacturers who know or have reason to believe that the product with digital elements or the processes put in place by the manufacturer are not in conformity with the essential cybersecurity requirements set out in Annex I shall immediately take the corrective measures necessary to bring that product with digital elements or the manufacturer’s processes into conformity, or to withdraw or recall the product, as appropriate.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 40, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Corrective measures when the product is not in conformity" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0241"), "Art.13(23)", "A manufacturer that ceases its operations and, as a result, is not able to comply with this Regulation shall inform, before the cessation of operations takes effect, the relevant market surveillance authorities as well as, by any means available and to the extent possible, the users of the relevant products with digital elements placed on the market, of the impending cessation of operations.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 13 – Obligations of manufacturers", 41, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_13", "Authorities and users are told before the manufacturer ceases operations" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0242"), "Art.14(1)", "A manufacturer shall notify any actively exploited vulnerability contained in the product with digital elements that it becomes aware of simultaneously to the CSIRT designated as coordinator, in accordance with paragraph 7 of this Article, and to ENISA. The manufacturer shall notify that actively exploited vulnerability via the single reporting platform established pursuant to Article 16.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 42, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Actively exploited vulnerabilities are notified to the CSIRT and ENISA" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0243"), "Art.14(2)(a)", "For the purposes of the notification referred to in paragraph 1, the manufacturer shall submit: an early warning notification of an actively exploited vulnerability, without undue delay and in any event within 24 hours of the manufacturer becoming aware of it, indicating, where applicable, the Member States on the territory of which the manufacturer is aware that their product with digital elements has been made available;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 43, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Early warning of an actively exploited vulnerability within 24 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0244"), "Art.14(2)(b)", "For the purposes of the notification referred to in paragraph 1, the manufacturer shall submit: unless the relevant information has already been provided, a vulnerability notification, without undue delay and in any event within 72 hours of the manufacturer becoming aware of the actively exploited vulnerability, which shall provide general information, as available, about the product with digital elements concerned, the general nature of the exploit and of the vulnerability concerned as well as any corrective or mitigating measures taken, and corrective or mitigating measures that users can take, and which shall also indicate, where applicable, how sensitive the manufacturer considers the notified information to be;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 44, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Vulnerability notification within 72 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0245"), "Art.14(2)(c)", "For the purposes of the notification referred to in paragraph 1, the manufacturer shall submit: unless the relevant information has already been provided, a final report, no later than 14 days after a corrective or mitigating measure is available, including at least the following: (i) a description of the vulnerability, including its severity and impact; (ii) where available, information concerning any malicious actor that has exploited or that is exploiting the vulnerability; (iii) details about the security update or other corrective measures that have been made available to remedy the vulnerability.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 45, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Final report on the vulnerability within 14 days after a fix is available" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0246"), "Art.14(3)", "A manufacturer shall notify any severe incident having an impact on the security of the product with digital elements that it becomes aware of simultaneously to the CSIRT designated as coordinator, in accordance with paragraph 7 of this Article, and to ENISA. The manufacturer shall notify that incident via the single reporting platform established pursuant to Article 16.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 46, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Severe incidents are notified to the CSIRT and ENISA" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0247"), "Art.14(4)(a)", "For the purposes of the notification referred to in paragraph 3, the manufacturer shall submit: an early warning notification of a severe incident having an impact on the security of the product with digital elements, without undue delay and in any event within 24 hours of the manufacturer becoming aware of it, including at least whether the incident is suspected of being caused by unlawful or malicious acts, which shall also indicate, where applicable, the Member States on the territory of which the manufacturer is aware that their product with digital elements has been made available;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 47, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Early warning of a severe incident within 24 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0248"), "Art.14(4)(b)", "For the purposes of the notification referred to in paragraph 3, the manufacturer shall submit: unless the relevant information has already been provided, an incident notification, without undue delay and in any event within 72 hours of the manufacturer becoming aware of the incident, which shall provide general information, where available, about the nature of the incident, an initial assessment of the incident, as well as any corrective or mitigating measures taken, and corrective or mitigating measures that users can take, and which shall also indicate, where applicable, how sensitive the manufacturer considers the notified information to be;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 48, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Incident notification within 72 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0249"), "Art.14(4)(c)", "For the purposes of the notification referred to in paragraph 3, the manufacturer shall submit: unless the relevant information has already been provided, a final report, within one month after the submission of the incident notification under point (b), including at least the following: (i) a detailed description of the incident, including its severity and impact; (ii) the type of threat or root cause that is likely to have triggered the incident; (iii) applied and ongoing mitigation measures.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 49, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Final report on the incident within one month" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0250"), "Art.14(7)", "The notifications referred to in paragraphs 1 and 3 of this Article shall be submitted via the single reporting platform referred to in Article 16 using one of the electronic notification end-points referred to in Article 16(1). The notification shall be submitted using the electronic notification end-point of the CSIRT designated as coordinator of the Member State where the manufacturers have their main establishment in the Union and shall be simultaneously accessible to ENISA. For the purposes of this Regulation, a manufacturer shall be considered to have its main establishment in the Union in the Member State where the decisions related to the cybersecurity of its products with digital elements are predominantly taken. If such a Member State cannot be determined, the main establishment shall be considered to be in the Member State where the manufacturer concerned has the establishment with the highest number of employees in the Union. Where a manufacturer has no main establishment in the Union, it shall submit the notifications referred to in paragraphs 1 and 3 using the electronic notification end-point of the CSIRT designated as coordinator in the Member State determined pursuant to the following order and based on the information available to the manufacturer: (a) the Member State in which the authorised representative acting on behalf of the manufacturer for the highest number of products with digital elements of that manufacturer is established; (b) the Member State in which the importer placing on the market the highest number of products with digital elements of that manufacturer is established; (c) the Member State in which the distributor making available on the market the highest number of products with digital elements of that manufacturer is established; (d) the Member State in which the highest number of users of products with digital elements of that manufacturer are located.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 50, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Notifications go through the single reporting platform to the right CSIRT" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0251"), "Art.14(8)", "After becoming aware of an actively exploited vulnerability or a severe incident having an impact on the security of the product with digital elements, the manufacturer shall inform the impacted users of the product with digital elements, and where appropriate all users, of that vulnerability or incident and, where necessary, of any risk mitigation and corrective measures that the users can deploy to mitigate the impact of that vulnerability or incident, where appropriate in a structured, machine-readable format that is easily automatically processable. Where the manufacturer fails to inform the users of the product with digital elements in a timely manner, the notified CSIRTs designated as coordinators may provide such information to the users when considered to be proportionate and necessary for preventing or mitigating the impact of that vulnerability or incident.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 14 – Reporting obligations", 51, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_14", "Impacted users are informed of the vulnerability or incident" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0252"), "Art.28", "The EU declaration of conformity shall be drawn up by manufacturers in accordance with Article 13(12) and state that the fulfilment of the applicable essential cybersecurity requirements set out in Annex I has been demonstrated. The EU declaration of conformity shall have the model structure set out in Annex V and shall contain the elements specified in the relevant conformity assessment procedures set out in Annex VIII. Such a declaration shall be updated as appropriate. It shall be made available in the languages required by the Member State in which the product with digital elements is placed on the market or made available on the market. The simplified EU declaration of conformity referred to in Article 13(20) shall have the model structure set out in Annex VI. It shall be made available in the languages required by the Member State in which the product with digital elements is placed on the market or made available on the market. Where a product with digital elements is subject to more than one Union legal act requiring an EU declaration of conformity, a single EU declaration of conformity shall be drawn up in respect of all such Union legal acts. That declaration shall contain the identification of the Union legal acts concerned, including their publication references. By drawing up the EU declaration of conformity, the manufacturer shall assume responsibility for the compliance of the product with digital elements.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 28–32 – Conformity assessment and CE marking", 52, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_28", "The EU declaration of conformity (Annexes V and VI)" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0253"), "Art.30", "The CE marking shall be affixed visibly, legibly and indelibly to the product with digital elements. Where that is not possible or not warranted on account of the nature of the product with digital elements, it shall be affixed to the packaging and to the EU declaration of conformity referred to in Article 28 accompanying the product with digital elements. For products with digital elements which are in the form of software, the CE marking shall be affixed either to the EU declaration of conformity referred to in Article 28 or on the website accompanying the software product. In the latter case, the relevant section of the website shall be easily and directly accessible to consumers. The CE marking shall be affixed before the product with digital elements is placed on the market. It may be followed by a pictogram or any other mark indicating a special cybersecurity risk or use set out in the implementing acts referred to in paragraph 6. The CE marking shall be followed by the identification number of the notified body, where that body is involved in the conformity assessment procedure based on full quality assurance (based on module H) referred to in Article 32.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 28–32 – Conformity assessment and CE marking", 53, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_30", "The CE marking is affixed visibly before the product is placed on the market" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0254"), "Art.31", "The technical documentation shall contain all relevant data or details of the means used by the manufacturer to ensure that the product with digital elements and the processes put in place by the manufacturer comply with the essential cybersecurity requirements set out in Annex I. It shall at least contain the elements set out in Annex VII. The technical documentation shall be drawn up before the product with digital elements is placed on the market and shall be continuously updated, where appropriate, at least during the support period. For products with digital elements as referred to in Article 12, which are also subject to other Union legal acts which provide for technical documentation, a single set of technical documentation shall be drawn up containing the information referred to in Annex VII and the information required by those Union legal acts.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 28–32 – Conformity assessment and CE marking", 54, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_31", "Technical documentation after Annex VII, kept up to date" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0255"), "Art.32", "The manufacturer shall perform a conformity assessment of the product with digital elements and the processes put in place by the manufacturer to determine whether the essential cybersecurity requirements set out in Annex I are met. The manufacturer shall demonstrate conformity with the essential cybersecurity requirements by using any of the following procedures: (a) the internal control procedure (based on module A) set out in Annex VIII; (b) the EU-type examination procedure (based on module B) set out in Annex VIII followed by conformity to EU-type based on internal production control (based on module C) set out in Annex VIII; (c) a conformity assessment based on full quality assurance (based on module H) set out in Annex VIII; or (d) where available and applicable, a European cybersecurity certification scheme pursuant to Article 27(9). Where the product is an important product with digital elements that falls under class II as set out in Annex III, the manufacturer shall demonstrate conformity with the essential cybersecurity requirements set out in Annex I by using any of the following procedures: (a) EU-type examination procedure (based on module B) set out in Annex VIII followed by conformity to EU-type based on internal production control (based on module C) set out in Annex VIII; (b) a conformity assessment based on full quality assurance (based on module H) set out in Annex VIII; or (c) where available and applicable, a European cybersecurity certification scheme pursuant to Article 27(9) of this Regulation at assurance level at least ‘substantial’ pursuant to Regulation (EU) 2019/881. Critical products with digital elements listed in Annex IV shall demonstrate conformity with the essential cybersecurity requirements set out in Annex I by using one of the following procedures: (a) a European cybersecurity certification scheme in accordance with Article 8(1); or (b) where the conditions in Article 8(1) are not met, any of the procedures referred to in paragraph 3 of this Article.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Art. 28–32 – Conformity assessment and CE marking", 55, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#art_32", "A conformity assessment with one of the procedures (Annex VIII)" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0256"), "Annex II.1", "At minimum, the product with digital elements shall be accompanied by: the name, registered trade name or registered trademark of the manufacturer, and the postal address, the email address or other digital contact as well as, where available, the website at which the manufacturer can be contacted;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 56, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "The manufacturer's name and contact details" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0257"), "Annex II.2", "At minimum, the product with digital elements shall be accompanied by: the single point of contact where information about vulnerabilities of the product with digital elements can be reported and received, and where the manufacturer’s policy on coordinated vulnerability disclosure can be found;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 57, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "The single point of contact for vulnerabilities and the disclosure policy" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0258"), "Annex II.3", "At minimum, the product with digital elements shall be accompanied by: name and type and any additional information enabling the unique identification of the product with digital elements;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 58, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "Name and type that identify the product" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0259"), "Annex II.4", "At minimum, the product with digital elements shall be accompanied by: the intended purpose of the product with digital elements, including the security environment provided by the manufacturer, as well as the product’s essential functionalities and information about the security properties;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 59, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "Intended purpose, essential functions and security properties" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0260"), "Annex II.5", "At minimum, the product with digital elements shall be accompanied by: any known or foreseeable circumstance, related to the use of the product with digital elements in accordance with its intended purpose or under conditions of reasonably foreseeable misuse, which may lead to significant cybersecurity risks;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 60, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "Known or foreseeable circumstances that may lead to significant risks" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0261"), "Annex II.6", "At minimum, the product with digital elements shall be accompanied by: where applicable, the internet address at which the EU declaration of conformity can be accessed;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 61, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "Where the EU declaration of conformity can be found" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0262"), "Annex II.7", "At minimum, the product with digital elements shall be accompanied by: the type of technical security support offered by the manufacturer and the end-date of the support period during which users can expect vulnerabilities to be handled and to receive security updates;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 62, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "The type of technical security support and the end date of the support period" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0263"), "Annex II.8", "At minimum, the product with digital elements shall be accompanied by: detailed instructions or an internet address referring to such detailed instructions and information on: (a) the necessary measures during initial commissioning and throughout the lifetime of the product with digital elements to ensure its secure use; (b) how changes to the product with digital elements can affect the security of data; (c) how security-relevant updates can be installed; (d) the secure decommissioning of the product with digital elements, including information on how user data can be securely removed; (e) how the default setting enabling the automatic installation of security updates, as required by Part I, point (2)(c), of Annex I, can be turned off; (f) where the product with digital elements is intended for integration into other products with digital elements, the information necessary for the integrator to comply with the essential cybersecurity requirements set out in Annex I and the documentation requirements set out in Annex VII.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 63, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "Instructions for secure use, updates, decommissioning and integration" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0264"), "Annex II.9", "At minimum, the product with digital elements shall be accompanied by: If the manufacturer decides to make available the software bill of materials to the user, information on where the software bill of materials can be accessed.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex II – Information and instructions to the user", 64, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_II", "Where the SBOM can be found, if it is made available" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0265"), "Annex VII.1", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: a general description of the product with digital elements, including: (a) its intended purpose; (b) versions of software affecting compliance with essential cybersecurity requirements; (c) where the product with digital elements is a hardware product, photographs or illustrations showing external features, marking and internal layout; (d) user information and instructions as set out in Annex II;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 65, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "A general description of the product" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0266"), "Annex VII.2", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: a description of the design, development and production of the product with digital elements and vulnerability handling processes, including: (a) necessary information on the design and development of the product with digital elements, including, where applicable, drawings and schemes and a description of the system architecture explaining how software components build on or feed into each other and integrate into the overall processing; (b) necessary information and specifications of the vulnerability handling processes put in place by the manufacturer, including the software bill of materials, the coordinated vulnerability disclosure policy, evidence of the provision of a contact address for the reporting of the vulnerabilities and a description of the technical solutions chosen for the secure distribution of updates; (c) necessary information and specifications of the production and monitoring processes of the product with digital elements and the validation of those processes;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 66, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "Design, development, production and vulnerability handling" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0267"), "Annex VII.3", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: an assessment of the cybersecurity risks against which the product with digital elements is designed, developed, produced, delivered and maintained pursuant to Article 13, including how the essential cybersecurity requirements set out in Part I of Annex I are applicable;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 67, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "The cybersecurity risk assessment" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0268"), "Annex VII.4", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: relevant information that was taken into account to determine the support period pursuant to Article 13(8) of the product with digital elements;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 68, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "What was taken into account to determine the support period" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0269"), "Annex VII.5", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: a list of the harmonised standards applied in full or in part the references of which have been published in the Official Journal of the European Union, common specifications as set out in Article 27 of this Regulation or European cybersecurity certification schemes adopted pursuant to Regulation (EU) 2019/881 pursuant to Article 27(8) of this Regulation, and, where those harmonised standards, common specifications or European cybersecurity certification schemes have not been applied, descriptions of the solutions adopted to meet the essential cybersecurity requirements set out in Parts I and II of Annex I, including a list of other relevant technical specifications applied. In the event of partly applied harmonised standards, common specifications or European cybersecurity certification schemes, the technical documentation shall specify the parts which have been applied;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 69, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "Standards, specifications and certification schemes applied" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0270"), "Annex VII.6", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: reports of the tests carried out to verify the conformity of the product with digital elements and of the vulnerability handling processes with the applicable essential cybersecurity requirements as set out in Parts I and II of Annex I;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 70, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "Reports of the conformity tests" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0271"), "Annex VII.7", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: a copy of the EU declaration of conformity;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 71, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "A copy of the EU declaration of conformity" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0272"), "Annex VII.8", "The technical documentation referred to in Article 31 shall contain at least the following information, as applicable to the relevant product with digital elements: where applicable, the software bill of materials, further to a reasoned request from a market surveillance authority provided that it is necessary in order for that authority to be able to check compliance with the essential cybersecurity requirements set out in Annex I.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"), "Annex VII – Technical documentation", 72, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_VII", "The SBOM, when the market surveillance authority asks for it" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0201"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0202"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0203"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0204"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0205"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0206"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0207"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0208"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0209"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0210"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0211"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0212"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0213"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0214"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0215"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0216"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0217"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0218"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0219"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0220"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0221"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0222"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0223"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0224"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0225"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0226"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0227"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0228"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0229"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0230"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0231"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0232"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0233"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0234"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0235"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0236"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0237"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0238"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0239"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0240"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0241"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0242"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0243"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0244"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0245"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0246"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0247"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0248"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0249"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0250"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0251"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0252"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0253"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0254"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0255"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0256"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0257"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0258"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0259"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0260"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0261"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0262"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0263"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0264"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0265"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0266"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0267"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0268"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0269"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0270"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0271"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0272"));

            migrationBuilder.DeleteData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"));
        }
    }
}
