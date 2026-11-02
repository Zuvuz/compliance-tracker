using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCraRequirementsAndApplicability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Requirements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Requirements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequirementApplicability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementApplicability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementApplicability_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementApplicability_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementApplicability_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementApplicability_Users_RevokedById",
                        column: x => x.RevokedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Requirements",
                columns: new[] { "Id", "Code", "Description", "FrameworkVersionId", "Group", "Position", "Reference", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0101"), "I.1", "Products with digital elements shall be designed, developed and produced in such a way that they ensure an appropriate level of cybersecurity based on the risks.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 1, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Appropriate level of cybersecurity based on the risks" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0102"), "I.2(a)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be made available on the market without known exploitable vulnerabilities;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 2, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "No known exploitable vulnerabilities" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0103"), "I.2(b)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be made available on the market with a secure by default configuration, unless otherwise agreed between manufacturer and business user in relation to a tailor-made product with digital elements, including the possibility to reset the product to its original state;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 3, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Secure by default configuration" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0104"), "I.2(c)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: ensure that vulnerabilities can be addressed through security updates, including, where applicable, through automatic security updates that are installed within an appropriate timeframe enabled as a default setting, with a clear and easy-to-use opt-out mechanism, through the notification of available updates to users, and the option to temporarily postpone them;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 4, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Security updates" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0105"), "I.2(d)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: ensure protection from unauthorised access by appropriate control mechanisms, including but not limited to authentication, identity or access management systems, and report on possible unauthorised access;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 5, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Protection from unauthorised access" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0106"), "I.2(e)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: protect the confidentiality of stored, transmitted or otherwise processed data, personal or other, such as by encrypting relevant data at rest or in transit by state of the art mechanisms, and by using other technical means;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 6, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Confidentiality of data" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0107"), "I.2(f)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: protect the integrity of stored, transmitted or otherwise processed data, personal or other, commands, programs and configuration against any manipulation or modification not authorised by the user, and report on corruptions;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 7, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Integrity of data, programs and configuration" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0108"), "I.2(g)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: process only data, personal or other, that are adequate, relevant and limited to what is necessary in relation to the intended purpose of the product with digital elements (data minimisation);", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 8, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Data minimisation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0109"), "I.2(h)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: protect the availability of essential and basic functions, also after an incident, including through resilience and mitigation measures against denial-of-service attacks;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 9, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Availability of essential functions" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0110"), "I.2(i)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: minimise the negative impact by the products themselves or connected devices on the availability of services provided by other devices or networks;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 10, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Limited impact on other devices and networks" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0111"), "I.2(j)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be designed, developed and produced to limit attack surfaces, including external interfaces;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 11, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Limited attack surfaces" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0112"), "I.2(k)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: be designed, developed and produced to reduce the impact of an incident using appropriate exploitation mitigation mechanisms and techniques;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 12, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Reduced impact of incidents" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0113"), "I.2(l)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: provide security related information by recording and monitoring relevant internal activity, including the access to or modification of data, services or functions, with an opt-out mechanism for the user;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 13, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Security logging and monitoring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0114"), "I.2(m)", "On the basis of the cybersecurity risk assessment referred to in Article 13(2) and where applicable, products with digital elements shall: provide the possibility for users to securely and easily remove on a permanent basis all data and settings and, where such data can be transferred to other products or systems, ensure that this is done in a secure manner.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part I – Essential cybersecurity requirements", 14, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Secure removal of data and settings" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0115"), "II.1", "Manufacturers of products with digital elements shall: identify and document vulnerabilities and components contained in products with digital elements, including by drawing up a software bill of materials in a commonly used and machine-readable format covering at the very least the top-level dependencies of the products;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 15, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Identify and document vulnerabilities and components (SBOM)" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0116"), "II.2", "Manufacturers of products with digital elements shall: in relation to the risks posed to products with digital elements, address and remediate vulnerabilities without delay, including by providing security updates; where technically feasible, new security updates shall be provided separately from functionality updates;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 16, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Remediate vulnerabilities without delay" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0117"), "II.3", "Manufacturers of products with digital elements shall: apply effective and regular tests and reviews of the security of the product with digital elements;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 17, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Regular security tests and reviews" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0118"), "II.4", "Manufacturers of products with digital elements shall: once a security update has been made available, share and publicly disclose information about fixed vulnerabilities, including a description of the vulnerabilities, information allowing users to identify the product with digital elements affected, the impacts of the vulnerabilities, their severity and clear and accessible information helping users to remediate the vulnerabilities; in duly justified cases, where manufacturers consider the security risks of publication to outweigh the security benefits, they may delay making public information regarding a fixed vulnerability until after users have been given the possibility to apply the relevant patch;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 18, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Disclose fixed vulnerabilities" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0119"), "II.5", "Manufacturers of products with digital elements shall: put in place and enforce a policy on coordinated vulnerability disclosure;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 19, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Coordinated vulnerability disclosure policy" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0120"), "II.6", "Manufacturers of products with digital elements shall: take measures to facilitate the sharing of information about potential vulnerabilities in their product with digital elements as well as in third-party components contained in that product, including by providing a contact address for the reporting of the vulnerabilities discovered in the product with digital elements;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 20, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Sharing information about vulnerabilities, with a contact address" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0121"), "II.7", "Manufacturers of products with digital elements shall: provide for mechanisms to securely distribute updates for products with digital elements to ensure that vulnerabilities are fixed or mitigated in a timely manner and, where applicable for security updates, in an automatic manner;", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 21, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Secure distribution of updates" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0122"), "II.8", "Manufacturers of products with digital elements shall: ensure that, where security updates are available to address identified security issues, they are disseminated without delay and, unless otherwise agreed between a manufacturer and a business user in relation to a tailor-made product with digital elements, free of charge, accompanied by advisory messages providing users with the relevant information, including on potential action to be taken.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "Part II – Vulnerability handling", 22, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=OJ:L_202402847#anx_I", "Security updates without delay and free of charge" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementApplicability_DecidedById",
                table: "RequirementApplicability",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementApplicability_ProjectId_RequirementId",
                table: "RequirementApplicability",
                columns: new[] { "ProjectId", "RequirementId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementApplicability_RequirementId",
                table: "RequirementApplicability",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementApplicability_RevokedById",
                table: "RequirementApplicability",
                column: "RevokedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementApplicability");

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0101"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0102"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0103"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0104"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0105"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0106"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0107"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0108"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0109"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0110"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0111"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0112"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0113"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0114"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0115"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0116"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0117"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0118"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0119"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0120"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0121"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0122"));

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Requirements");
        }
    }
}
