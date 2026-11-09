using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationAreaAndNis2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Implements",
                table: "Requirements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Projects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Product");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Organizations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "DK");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Frameworks",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImplementsFrameworkId",
                table: "Frameworks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Frameworks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Product");

            migrationBuilder.CreateTable(
                name: "Nis2Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sector = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Service = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Employees = table.Column<int>(type: "integer", nullable: false),
                    TurnoverMillions = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    BalanceSheetMillions = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DesignatedByAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Uses2024_2690 = table.Column<bool>(type: "boolean", nullable: false),
                    AssessedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nis2Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nis2Assessments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementTexts_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"),
                columns: new[] { "Country", "ImplementsFrameworkId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03"),
                columns: new[] { "Country", "ImplementsFrameworkId" },
                values: new object[] { null, null });

            migrationBuilder.InsertData(
                table: "Frameworks",
                columns: new[] { "Id", "Country", "ImplementsFrameworkId", "Level", "Name", "OwnerOrganizationId" },
                values: new object[] { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"), null, null, "Organisation", "NIS2 – Directive (EU) 2022/2555", null });

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0101"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0102"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0103"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0104"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0105"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0106"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0107"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0108"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0109"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0110"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0111"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0112"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0113"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0114"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0115"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0116"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0117"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0118"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0119"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0120"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0121"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0122"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0201"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0202"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0203"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0204"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0205"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0206"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0207"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0208"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0209"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0210"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0211"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0212"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0213"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0214"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0215"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0216"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0217"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0218"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0219"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0220"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0221"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0222"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0223"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0224"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0225"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0226"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0227"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0228"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0229"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0230"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0231"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0232"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0233"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0234"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0235"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0236"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0237"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0238"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0239"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0240"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0241"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0242"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0243"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0244"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0245"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0246"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0247"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0248"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0249"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0250"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0251"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0252"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0253"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0254"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0255"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0256"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0257"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0258"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0259"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0260"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0261"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0262"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0263"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0264"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0265"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0266"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0267"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0268"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0269"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0270"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0271"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0272"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0401"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0402"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0403"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0404"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0405"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0406"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0407"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0408"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0409"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0410"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0411"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0412"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0413"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0414"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0415"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0416"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0417"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0418"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0419"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0420"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0421"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0422"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0423"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0424"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0425"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0426"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0427"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0428"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0429"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0430"),
                column: "Implements",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0431"),
                column: "Implements",
                value: null);

            migrationBuilder.InsertData(
                table: "FrameworkVersions",
                columns: new[] { "Id", "ActivatedAt", "ActivatedById", "ActivatedRole", "AppliesFrom", "Extraction", "FileName", "FrameworkId", "Number", "Sha256", "SizeInBytes", "SourceReference", "Status", "UploadedAt", "UploadedById" },
                values: new object[] { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), new DateTime(2022, 12, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2024, 10, 18), null, "", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"), 1, "", 0L, "https://eur-lex.europa.eu/eli/dir/2022/2555/oj", "Active", new DateTime(2022, 12, 27, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.InsertData(
                table: "Frameworks",
                columns: new[] { "Id", "Country", "ImplementsFrameworkId", "Level", "Name", "OwnerOrganizationId" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c08"), "DK", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"), "Organisation", "NIS 2-loven – Lov nr. 434 af 6. maj 2025 (Denmark)", null },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0a"), null, new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"), "Organisation", "NIS2 technical measures – Implementing Regulation (EU) 2024/2690", null }
                });

            migrationBuilder.InsertData(
                table: "FrameworkVersions",
                columns: new[] { "Id", "ActivatedAt", "ActivatedById", "ActivatedRole", "AppliesFrom", "Extraction", "FileName", "FrameworkId", "Number", "Sha256", "SizeInBytes", "SourceReference", "Status", "UploadedAt", "UploadedById" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), new DateTime(2025, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2025, 7, 1), null, "", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c08"), 1, "", 0L, "https://www.retsinformation.dk/eli/lta/2025/434", "Active", new DateTime(2025, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), new DateTime(2024, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateOnly(2024, 10, 18), null, "", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0a"), 1, "", 0L, "https://eur-lex.europa.eu/eli/reg_impl/2024/2690/oj", "Active", new DateTime(2024, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.InsertData(
                table: "Requirements",
                columns: new[] { "Id", "Code", "Description", "FrameworkVersionId", "Group", "Implements", "Position", "Reference", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0501"), "Art. 20(1)", "The management body approves the cybersecurity risk-management measures taken under Art. 21, oversees how they are carried out, and can be held liable when the entity does not comply.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Governance (Art. 20)", null, 1, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_20", "Management approves and oversees the measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0502"), "Art. 20(2)", "Members of the management body follow training to gain enough knowledge and skills to identify risks and assess cybersecurity practices, and the entity encourages similar training for its employees regularly.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Governance (Art. 20)", null, 2, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_20", "Management takes cybersecurity training" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0503"), "Art. 21(1)", "The entity takes appropriate and proportionate technical, operational and organisational measures to manage the risks to its network and information systems and to prevent or minimise the impact of incidents, based on an all-hazards approach and in proportion to its exposure, size and the likely impact.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 3, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Appropriate and proportionate measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0504"), "Art. 21(2)(a)", "Policies on risk analysis and on the security of information systems.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 4, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Risk analysis and information system security policies" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0505"), "Art. 21(2)(b)", "How incidents are prevented, detected, analysed, contained, responded to and recovered from.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 5, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Incident handling" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0506"), "Art. 21(2)(c)", "Business continuity, such as backup management and disaster recovery, and crisis management.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 6, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Business continuity and crisis management" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0507"), "Art. 21(2)(d)", "Security of the supply chain, including the security of the relationships with direct suppliers and service providers.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 7, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Supply chain security" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0508"), "Art. 21(2)(e)", "Security when network and information systems are acquired, developed and maintained, including vulnerability handling and disclosure.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 8, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Security in acquisition, development and maintenance" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0509"), "Art. 21(2)(f)", "Policies and procedures to assess whether the cybersecurity risk-management measures are effective.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 9, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Assessing the effectiveness of the measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0510"), "Art. 21(2)(g)", "Basic cyber hygiene practices and cybersecurity training.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 10, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Basic cyber hygiene and cybersecurity training" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0511"), "Art. 21(2)(h)", "Policies and procedures on the use of cryptography and, where relevant, encryption.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 11, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Cryptography and encryption" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0512"), "Art. 21(2)(i)", "Human resources security, access control policies and asset management.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 12, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Human resources security, access control and asset management" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0513"), "Art. 21(2)(j)", "Multi-factor or continuous authentication, secured voice, video and text communications, and secured emergency communication systems within the entity, where appropriate.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 13, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Multi-factor authentication and secured communications" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0514"), "Art. 21(3)", "When deciding the supply chain measures, the entity takes into account the vulnerabilities of each direct supplier and service provider, the overall quality of their products and cybersecurity practices, and their secure development procedures.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 14, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Suppliers' vulnerabilities and practices" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0515"), "Art. 21(4)", "An entity that finds it does not comply with the measures takes all necessary, appropriate and proportionate corrective measures without undue delay.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Cybersecurity risk-management measures (Art. 21)", null, 15, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_21", "Corrective measures without undue delay" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0516"), "Art. 23(1)", "The entity notifies its CSIRT or competent authority without undue delay of any significant incident, and informs the recipients of its services of significant incidents likely to affect the services.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Reporting obligations (Art. 23)", null, 16, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_23", "Notify significant incidents" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0517"), "Art. 23(2)", "The entity tells the recipients of its services who may be affected by a significant cyber threat which measures or remedies they can take, and where appropriate about the threat itself.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Reporting obligations (Art. 23)", null, 17, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_23", "Inform recipients about significant cyber threats" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0518"), "Art. 23(4)(a)", "An early warning within 24 hours of becoming aware of the significant incident, saying whether it is suspected to be caused by unlawful or malicious acts and whether it could have a cross-border impact.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Reporting obligations (Art. 23)", null, 18, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_23", "Early warning within 24 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0519"), "Art. 23(4)(b)", "An incident notification within 72 hours of becoming aware of the significant incident, updating the early warning with an initial assessment of its severity and impact and the indicators of compromise, where available.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Reporting obligations (Art. 23)", null, 19, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_23", "Incident notification within 72 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0520"), "Art. 23(4)(c)", "An intermediate report on relevant status updates when the CSIRT or the competent authority asks for one.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Reporting obligations (Art. 23)", null, 20, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_23", "Intermediate report on request" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0521"), "Art. 23(4)(d)", "A final report no later than one month after the incident notification, with a detailed description of the incident, the likely root cause, the mitigation applied and ongoing, and any cross-border impact; a progress report instead if the incident is still ongoing.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Reporting obligations (Art. 23)", null, 21, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_23", "Final report within one month" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0522"), "Art. 3(4)", "The entity submits at least its name, address and up-to-date contact details, its sector and sub-sector, and the other member states where it provides services, so the authority can keep its list of essential and important entities, and notifies changes.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Registration (Art. 3 and 27)", null, 22, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_3", "Register with the authority" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0523"), "Art. 27(2)", "Only for DNS, top-level domain, domain registration, cloud, data centre, content delivery network, managed (security) service providers, online marketplaces, search engines and social networking platforms: register with the authority, including the main establishment, IP ranges and the member states served, and notify changes within three months.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"), "Registration (Art. 3 and 27)", null, 23, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32022L2555#art_27", "Registration of digital providers" }
                });

            migrationBuilder.InsertData(
                table: "RequirementTexts",
                columns: new[] { "Id", "Description", "Language", "RequirementId", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0501"), "Ledelsesorganet godkender de foranstaltninger til styring af cybersikkerhedsrisici, der træffes efter art. 21, fører tilsyn med gennemførelsen og kan holdes ansvarlig, hvis enheden ikke overholder dem.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0501"), "Ledelsen godkender og fører tilsyn med foranstaltningerne" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0502"), "Ledelsens medlemmer deltager i kurser, så de kan identificere risici og vurdere praksis for cybersikkerhed, og enheden tilskynder til, at de øvrige ansatte jævnligt får tilsvarende kurser.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0502"), "Ledelsen deltager i cybersikkerhedskurser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0503"), "Enheden træffer passende og forholdsmæssige tekniske, operationelle og organisatoriske foranstaltninger for at styre risiciene for sine net- og informationssystemer og forhindre eller begrænse virkningen af hændelser, ud fra en tilgang med alle farer og i forhold til eksponering, størrelse og sandsynlig virkning.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0503"), "Passende og forholdsmæssige foranstaltninger" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0504"), "Politikker for risikoanalyse og for sikkerheden i informationssystemer.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0504"), "Politikker for risikoanalyse og informationssystemsikkerhed" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0505"), "Hvordan hændelser forebygges, opdages, analyseres, inddæmmes, håndteres og genoprettes.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0505"), "Håndtering af hændelser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0506"), "Driftskontinuitet, f.eks. backupstyring og reetablering efter en katastrofe, og krisestyring.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0506"), "Driftskontinuitet og krisestyring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0507"), "Sikkerhed i forsyningskæden, herunder sikkerheden i forholdet til direkte leverandører og tjenesteudbydere.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0507"), "Forsyningskædesikkerhed" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0508"), "Sikkerhed når net- og informationssystemer erhverves, udvikles og vedligeholdes, herunder håndtering og offentliggørelse af sårbarheder.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0508"), "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0509"), "Politikker og procedurer til at vurdere, om foranstaltningerne til styring af cybersikkerhedsrisici virker.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0509"), "Vurdering af foranstaltningernes effektivitet" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0510"), "Grundlæggende cyberhygiejnepraksis og uddannelse i cybersikkerhed.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0510"), "Grundlæggende cyberhygiejne og cybersikkerhedsuddannelse" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0511"), "Politikker og procedurer for brug af kryptografi og, hvor det er relevant, kryptering.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0511"), "Kryptografi og kryptering" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0512"), "Personalesikkerhed, politikker for adgangskontrol og forvaltning af aktiver.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0512"), "Personalesikkerhed, adgangskontrol og forvaltning af aktiver" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0513"), "Multifaktor- eller kontinuerlig autentificering, sikret tale-, video- og tekstkommunikation og sikrede nødkommunikationssystemer internt, hvor det er relevant.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0513"), "Multifaktorautentificering og sikret kommunikation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0514"), "Når enheden fastlægger foranstaltninger for forsyningskæden, tager den højde for den enkelte direkte leverandørs sårbarheder, den samlede kvalitet af produkter og cybersikkerhedspraksis og deres procedurer for sikker udvikling.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0514"), "Leverandørers sårbarheder og praksis" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0515"), "En enhed, der ikke overholder foranstaltningerne, træffer uden unødigt ophold alle nødvendige, passende og forholdsmæssige korrigerende foranstaltninger.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0515"), "Korrigerende foranstaltninger uden unødigt ophold" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0516"), "Enheden underretter uden unødigt ophold sin CSIRT eller kompetente myndighed om enhver væsentlig hændelse og orienterer modtagerne af sine tjenester om væsentlige hændelser, der sandsynligvis påvirker tjenesterne.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0516"), "Underret om væsentlige hændelser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0517"), "Enheden fortæller de modtagere af sine tjenester, der kan blive berørt af en væsentlig cybertrussel, hvilke foranstaltninger de kan træffe, og hvor det er relevant om selve truslen.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0517"), "Orienter modtagere om væsentlige cybertrusler" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0518"), "En tidlig varsling inden for 24 timer efter kendskab til den væsentlige hændelse, der angiver, om den mistænkes at skyldes ulovlige eller ondsindede handlinger, og om den kan få grænseoverskridende virkning.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0518"), "Tidlig varsling inden for 24 timer" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0519"), "En hændelsesunderretning inden for 72 timer efter kendskab til hændelsen, som opdaterer den tidlige varsling med en første vurdering af alvor og virkning og kompromitteringsindikatorer, hvor de findes.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0519"), "Hændelsesunderretning inden for 72 timer" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0520"), "En foreløbig rapport med relevante statusopdateringer, når CSIRT'en eller den kompetente myndighed beder om det.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0520"), "Foreløbig rapport efter anmodning" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0521"), "En endelig rapport senest en måned efter hændelsesunderretningen med en detaljeret beskrivelse, den sandsynlige grundlæggende årsag, de afbødende foranstaltninger og eventuel grænseoverskridende virkning; en statusrapport i stedet, hvis hændelsen stadig pågår.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0521"), "Endelig rapport inden for en måned" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0522"), "Enheden indsender mindst navn, adresse og ajourførte kontaktoplysninger, sektor og delsektor og de øvrige medlemsstater, hvor den leverer tjenester, så myndigheden kan føre sin liste over væsentlige og vigtige enheder, og melder ændringer.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0522"), "Registrer jer hos myndigheden" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0523"), "Kun for udbydere af DNS, topdomæner, domænenavnsregistrering, cloud, datacentre, indholdsleveringsnetværk, administrerede (sikkerheds)tjenester, onlinemarkedspladser, søgemaskiner og sociale netværk: registrering hos myndigheden med hovedforretningssted, ip-intervaller og de medlemsstater, der betjenes, og ændringer inden for tre måneder.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0523"), "Registrering af digitale udbydere" }
                });

            migrationBuilder.InsertData(
                table: "Requirements",
                columns: new[] { "Id", "Code", "Description", "FrameworkVersionId", "Group", "Implements", "Position", "Reference", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0601"), "§ 6, stk. 1", "Essential and important entities take appropriate and proportionate technical, operational and organisational measures to manage the risks to their network and information systems, as a minimum those in points 1 to 10.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(1)", 1, "https://www.retsinformation.dk/eli/lta/2025/434", "Appropriate and proportionate measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0602"), "§ 6, stk. 1, nr. 1", "Policies on risk analysis and information system security.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(a)", 2, "https://www.retsinformation.dk/eli/lta/2025/434", "Risk analysis and information system security policies" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0603"), "§ 6, stk. 1, nr. 2", "Handling of incidents.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(b)", 3, "https://www.retsinformation.dk/eli/lta/2025/434", "Incident handling" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0604"), "§ 6, stk. 1, nr. 3", "Business continuity, including backup management, disaster recovery and crisis management.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(c)", 4, "https://www.retsinformation.dk/eli/lta/2025/434", "Business continuity and crisis management" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0605"), "§ 6, stk. 1, nr. 4", "Supply chain security, including the relationships with direct suppliers and service providers.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(d)", 5, "https://www.retsinformation.dk/eli/lta/2025/434", "Supply chain security" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0606"), "§ 6, stk. 1, nr. 5", "Security in acquiring, developing and maintaining network and information systems, including vulnerability handling and disclosure.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(e)", 6, "https://www.retsinformation.dk/eli/lta/2025/434", "Security in acquisition, development and maintenance" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0607"), "§ 6, stk. 1, nr. 6", "Policies and procedures to assess the effectiveness of the measures.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(f)", 7, "https://www.retsinformation.dk/eli/lta/2025/434", "Assessing the effectiveness of the measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0608"), "§ 6, stk. 1, nr. 7", "Basic cyber hygiene practices and cybersecurity training.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(g)", 8, "https://www.retsinformation.dk/eli/lta/2025/434", "Basic cyber hygiene and cybersecurity training" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0609"), "§ 6, stk. 1, nr. 8", "Policies and procedures on cryptography and, where relevant, encryption.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(h)", 9, "https://www.retsinformation.dk/eli/lta/2025/434", "Cryptography and encryption" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0610"), "§ 6, stk. 1, nr. 9", "Human resources security, access control policies and asset management.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(i)", 10, "https://www.retsinformation.dk/eli/lta/2025/434", "Human resources security, access control and asset management" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0611"), "§ 6, stk. 1, nr. 10", "Multi-factor or continuous authentication, secured voice, video and text communication and secured internal emergency communication, where relevant.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(2)(j)", 11, "https://www.retsinformation.dk/eli/lta/2025/434", "Multi-factor authentication and secured communications" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0612"), "§ 6, stk. 2", "An entity that does not meet the requirements for the measures takes the necessary corrective measures without undue delay.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 21(4)", 12, "https://www.retsinformation.dk/eli/lta/2025/434", "Corrective measures without undue delay" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0613"), "§ 7, stk. 1", "The entity's management body approves the measures and oversees that they are carried out.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 20(1)", 13, "https://www.retsinformation.dk/eli/lta/2025/434", "Management approves and oversees the measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0614"), "§ 7, stk. 2", "Members of the management body take relevant courses in managing cybersecurity risks and encourage similar courses for the other employees.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Foranstaltninger (§ 6-7)", "Art. 20(2)", 14, "https://www.retsinformation.dk/eli/lta/2025/434", "Management takes cybersecurity courses" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0615"), "§ 9", "DNS, top-level domain and domain registration services, cloud, data centre, content delivery network, managed (security) service providers, online marketplaces, search engines and social networking platforms register with the competent authority within 3 months of being covered, and report changes within 3 months.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Registrering (§ 9-10)", "Art. 27(2)", 15, "https://www.retsinformation.dk/eli/lta/2025/434", "Registration of digital providers within 3 months" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0616"), "§ 10", "Essential and important entities register with the competent authority (on virk.dk) within 2 weeks of being covered, with name, address and contact details including IP ranges, sector and sub-sector, and the other EU countries served, and report changes within 2 weeks.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Registrering (§ 9-10)", "Art. 3(4)", 16, "https://www.retsinformation.dk/eli/lta/2025/434", "Registration within 2 weeks" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0617"), "§ 12", "Essential and important entities notify the competent authority and the CSIRT of every significant incident: one that has caused or can cause serious disruption or financial loss, or considerable damage to others.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(1)", 17, "https://www.retsinformation.dk/eli/lta/2025/434", "Notify significant incidents" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0618"), "§ 13, stk. 1, nr. 1", "An early warning without undue delay and within 24 hours of becoming aware of the significant incident.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(4)(a)", 18, "https://www.retsinformation.dk/eli/lta/2025/434", "Early warning within 24 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0619"), "§ 13, stk. 1, nr. 2", "An incident notification within 72 hours of becoming aware of the incident, with an initial assessment (24 hours for trust service providers, stk. 2).", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(4)(b)", 19, "https://www.retsinformation.dk/eli/lta/2025/434", "Incident notification within 72 hours" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0620"), "§ 13, stk. 1, nr. 3", "An intermediate report with relevant status updates when the CSIRT asks for one.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(4)(c)", 20, "https://www.retsinformation.dk/eli/lta/2025/434", "Intermediate report on request" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0621"), "§ 13, stk. 1, nr. 4", "A final report no later than one month after the incident notification, with a detailed description, the likely root cause, the mitigation and any cross-border impact.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(4)(d)", 21, "https://www.retsinformation.dk/eli/lta/2025/434", "Final report within one month" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0622"), "§ 15, stk. 1", "Essential and important entities inform the recipients of their services without undue delay of significant incidents likely to affect the services.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(1)", 22, "https://www.retsinformation.dk/eli/lta/2025/434", "Inform recipients of significant incidents" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0623"), "§ 15, stk. 2", "Essential and important entities tell the recipients potentially affected by a significant cyber threat which measures they can take, and about the threat where relevant.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"), "Underretning (§ 12-15)", "Art. 23(2)", 23, "https://www.retsinformation.dk/eli/lta/2025/434", "Inform recipients of significant cyber threats" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0701"), "Annex 1", "A security policy approved by management, with roles and responsibilities, reviewed at least yearly and after significant incidents or changes.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(a)", 1, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Policy on the security of network and information systems" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0702"), "Annex 2", "A risk management framework: identify, analyse, evaluate and treat risks, a risk treatment plan with owners, compliance monitoring and independent review.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(a)", 2, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Risk management policy" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0703"), "Annex 3", "An incident handling policy, monitoring and logging, event reporting, assessment and classification of events, response, and post-incident reviews.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(b)", 3, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Incident handling" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0704"), "Annex 4", "A business continuity and disaster recovery plan, backup and redundancy management, and a crisis management process.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(c)", 4, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Business continuity and crisis management" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0705"), "Annex 5", "A supply chain security policy and a directory of suppliers and service providers, with security requirements in contracts.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(d)", 5, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Supply chain security" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0706"), "Annex 6", "Security in acquiring ICT services and products, a secure development life cycle, configuration and change management, security testing, patch management, network security and segmentation, protection against malicious software, and vulnerability handling and disclosure.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(e)", 6, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Security in acquisition, development and maintenance" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0707"), "Annex 7", "Policies and procedures to assess whether the cybersecurity risk-management measures are effective, with what is monitored and measured, how and when.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(f)", 7, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Assessing the effectiveness of the measures" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0708"), "Annex 8", "Awareness raising and basic cyber hygiene for all employees, and security training for those whose roles need it, reviewed regularly.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(g)", 8, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Basic cyber hygiene and security training" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0709"), "Annex 9", "A policy on cryptography: when and how encryption is used, the algorithms and protocols, and key management.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(h)", 9, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Cryptography" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0710"), "Annex 10", "Employees understand their security responsibilities, background checks where relevant, procedures for leaving or changing roles, and a disciplinary process.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(i)", 10, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Human resources security" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0711"), "Annex 11", "An access control policy, management of access rights and privileged accounts, administration systems, identification, authentication (including multi-factor) and regular review of access.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(i)", 11, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Access control" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0712"), "Annex 12", "Classification of assets, handling of assets and removable media, an inventory of assets, and the return or deletion of assets when employment ends.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(2)(i)", 12, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Asset management" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0713"), "Annex 13", "Protection against the loss of supporting utilities, physical and environmental threats, and physical access control to the premises.", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"), "Technical and methodological requirements (Annex)", "Art. 21(1)", 13, "https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32024R2690#anx_1", "Environmental and physical security" }
                });

            migrationBuilder.InsertData(
                table: "RequirementTexts",
                columns: new[] { "Id", "Description", "Language", "RequirementId", "Title" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0601"), "Væsentlige og vigtige enheder træffer passende og forholdsmæssige tekniske, operationelle og organisatoriske foranstaltninger for at styre risiciene for deres net- og informationssystemer, som minimum dem i nr. 1-10.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0601"), "Passende og forholdsmæssige foranstaltninger" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0602"), "Politikker for risikoanalyse og informationssystemsikkerhed.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0602"), "Politikker for risikoanalyse og informationssystemsikkerhed" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0603"), "Håndtering af hændelser.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0603"), "Håndtering af hændelser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0604"), "Driftskontinuitet, herunder backupstyring, reetablering efter en katastrofe og krisestyring.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0604"), "Driftskontinuitet og krisestyring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0605"), "Forsyningskædesikkerhed, herunder forholdet til direkte leverandører og tjenesteudbydere.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0605"), "Forsyningskædesikkerhed" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0606"), "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse af net- og informationssystemer, herunder håndtering og offentliggørelse af sårbarheder.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0606"), "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0607"), "Politikker og procedurer til at vurdere effektiviteten af foranstaltningerne.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0607"), "Vurdering af foranstaltningernes effektivitet" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0608"), "Grundlæggende cyberhygiejnepraksis og cybersikkerhedsuddannelse.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0608"), "Grundlæggende cyberhygiejne og cybersikkerhedsuddannelse" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0609"), "Politikker og procedurer for brug af kryptografi og, hvor det er relevant, kryptering.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0609"), "Kryptografi og kryptering" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0610"), "Personalesikkerhed, politikker for adgangskontrol og forvaltning af aktiver.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0610"), "Personalesikkerhed, adgangskontrol og forvaltning af aktiver" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0611"), "Multifaktor- eller kontinuerlig autentificering, sikret tale-, video- og tekstkommunikation og sikrede interne nødkommunikationssystemer, hvor det er relevant.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0611"), "Multifaktorautentificering og sikret kommunikation" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0612"), "En enhed, der ikke overholder kravene til foranstaltningerne, træffer uden unødigt ophold de nødvendige korrigerende foranstaltninger.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0612"), "Korrigerende foranstaltninger uden unødigt ophold" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0613"), "Enhedens ledelsesorgan godkender foranstaltningerne og fører tilsyn med, at de gennemføres.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0613"), "Ledelsen godkender og fører tilsyn med foranstaltningerne" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0614"), "Ledelsens medlemmer deltager i relevante kurser om styring af cybersikkerhedsrisici og tilskynder til, at de øvrige ansatte tilbydes tilsvarende kurser.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0614"), "Ledelsen deltager i cybersikkerhedskurser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0615"), "Udbydere af DNS, topdomæner, domænenavnsregistrering, cloud, datacentre, indholdsleveringsnetværk, administrerede (sikkerheds)tjenester, onlinemarkedspladser, søgemaskiner og sociale netværk registrerer sig hos den kompetente myndighed senest 3 måneder efter, at de er omfattet, og melder ændringer inden for 3 måneder.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0615"), "Registrering af digitale udbydere inden for 3 måneder" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0616"), "Væsentlige og vigtige enheder registrerer sig hos den kompetente myndighed (på virk.dk) senest 2 uger efter, at de er omfattet, med navn, adresse og kontaktoplysninger inkl. ip-intervaller, sektor og delsektor og de øvrige EU-lande, der betjenes, og melder ændringer inden for 2 uger.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0616"), "Registrering inden for 2 uger" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0617"), "Væsentlige og vigtige enheder underretter den kompetente myndighed og CSIRT'en om enhver væsentlig hændelse: en hændelse, der har forårsaget eller kan forårsage alvorlige driftsforstyrrelser eller økonomiske tab, eller betydelig skade for andre.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0617"), "Underret om væsentlige hændelser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0618"), "En tidlig varsling uden unødigt ophold og senest 24 timer efter, at enheden har fået kendskab til den væsentlige hændelse.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0618"), "Tidlig varsling inden for 24 timer" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0619"), "En hændelsesunderretning senest 72 timer efter kendskab til hændelsen med en indledende vurdering (24 timer for tillidstjenesteudbydere, stk. 2).", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0619"), "Hændelsesunderretning inden for 72 timer" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0620"), "En foreløbig rapport med relevante statusopdateringer efter anmodning fra CSIRT'en.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0620"), "Foreløbig rapport efter anmodning" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0621"), "En endelig rapport senest en måned efter hændelsesunderretningen med en detaljeret beskrivelse, den sandsynlige årsag, de afbødende foranstaltninger og eventuel grænseoverskridende virkning.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0621"), "Endelig rapport inden for en måned" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0622"), "Væsentlige og vigtige enheder underretter uden unødigt ophold modtagerne af deres tjenester om væsentlige hændelser, der sandsynligvis påvirker leveringen negativt.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0622"), "Orienter modtagere om væsentlige hændelser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0623"), "Væsentlige og vigtige enheder oplyser modtagere, der kan blive berørt af en væsentlig cybertrussel, om de foranstaltninger, de kan træffe, og om truslen, hvor det er relevant.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0623"), "Orienter modtagere om væsentlige cybertrusler" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0701"), "En sikkerhedspolitik godkendt af ledelsen med roller og ansvar, som gennemgås mindst årligt og efter væsentlige hændelser eller ændringer.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0701"), "Politik for sikkerhed i net- og informationssystemer" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0702"), "En ramme for risikostyring: identificér, analysér, vurdér og behandl risici, en risikobehandlingsplan med ejere, overvågning af overholdelse og uafhængig gennemgang.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0702"), "Politik for risikostyring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0703"), "En politik for hændelseshåndtering, overvågning og logning, rapportering, vurdering og klassificering af hændelser, respons og evaluering efter hændelser.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0703"), "Håndtering af hændelser" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0704"), "En plan for driftskontinuitet og reetablering, styring af backup og redundans og en proces for krisestyring.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0704"), "Driftskontinuitet og krisestyring" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0705"), "En politik for forsyningskædesikkerhed og en fortegnelse over leverandører og tjenesteudbydere med sikkerhedskrav i kontrakterne.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0705"), "Forsyningskædesikkerhed" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0706"), "Sikkerhed ved indkøb af ikt-tjenester og -produkter, en sikker udviklingslivscyklus, konfigurations- og ændringsstyring, sikkerhedstest, patchstyring, netværkssikkerhed og segmentering, beskyttelse mod skadelig software og håndtering og offentliggørelse af sårbarheder.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0706"), "Sikkerhed ved erhvervelse, udvikling og vedligeholdelse" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0707"), "Politikker og procedurer til at vurdere, om foranstaltningerne virker, med hvad der overvåges og måles, hvordan og hvornår.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0707"), "Vurdering af foranstaltningernes effektivitet" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0708"), "Bevidstgørelse og grundlæggende cyberhygiejne for alle ansatte og sikkerhedsuddannelse for dem, hvis roller kræver det, gennemgået jævnligt.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0708"), "Grundlæggende cyberhygiejne og sikkerhedsuddannelse" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0709"), "En politik for kryptografi: hvornår og hvordan der krypteres, algoritmer og protokoller og nøglehåndtering.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0709"), "Kryptografi" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0710"), "Ansatte forstår deres sikkerhedsansvar, baggrundstjek hvor det er relevant, procedurer ved fratræden eller nye roller og en disciplinær proces.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0710"), "Personalesikkerhed" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0711"), "En politik for adgangskontrol, styring af adgangsrettigheder og privilegerede konti, administrationssystemer, identifikation, autentificering (også multifaktor) og jævnlig gennemgang af adgange.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0711"), "Adgangskontrol" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0712"), "Klassificering af aktiver, håndtering af aktiver og flytbare medier, en fortegnelse over aktiver og aflevering eller sletning, når ansættelsen ophører.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0712"), "Forvaltning af aktiver" },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3e0713"), "Beskyttelse mod svigt i forsyninger, fysiske og miljømæssige trusler og fysisk adgangskontrol til lokalerne.", "da", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0713"), "Fysisk og miljømæssig sikkerhed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganisationArea",
                table: "Projects",
                column: "OrganizationId",
                unique: true,
                filter: "\"Kind\" = 'Organisation'");

            migrationBuilder.CreateIndex(
                name: "IX_Frameworks_ImplementsFrameworkId",
                table: "Frameworks",
                column: "ImplementsFrameworkId");

            migrationBuilder.CreateIndex(
                name: "IX_Nis2Assessments_OrganizationId_AssessedAt",
                table: "Nis2Assessments",
                columns: new[] { "OrganizationId", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementTexts_RequirementId_Language",
                table: "RequirementTexts",
                columns: new[] { "RequirementId", "Language" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Frameworks_Frameworks_ImplementsFrameworkId",
                table: "Frameworks",
                column: "ImplementsFrameworkId",
                principalTable: "Frameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Frameworks_Frameworks_ImplementsFrameworkId",
                table: "Frameworks");

            migrationBuilder.DropTable(
                name: "Nis2Assessments");

            migrationBuilder.DropTable(
                name: "RequirementTexts");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganisationArea",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Frameworks_ImplementsFrameworkId",
                table: "Frameworks");

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0501"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0502"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0503"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0504"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0505"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0506"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0507"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0508"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0509"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0510"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0511"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0512"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0513"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0514"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0515"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0516"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0517"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0518"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0519"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0520"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0521"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0522"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0523"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0601"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0602"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0603"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0604"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0605"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0606"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0607"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0608"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0609"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0610"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0611"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0612"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0613"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0614"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0615"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0616"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0617"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0618"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0619"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0620"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0621"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0622"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0623"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0701"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0702"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0703"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0704"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0705"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0706"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0707"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0708"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0709"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0710"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0711"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0712"));

            migrationBuilder.DeleteData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0713"));

            migrationBuilder.DeleteData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"));

            migrationBuilder.DeleteData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"));

            migrationBuilder.DeleteData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"));

            migrationBuilder.DeleteData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c08"));

            migrationBuilder.DeleteData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0a"));

            migrationBuilder.DeleteData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"));

            migrationBuilder.DropColumn(
                name: "Implements",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Frameworks");

            migrationBuilder.DropColumn(
                name: "ImplementsFrameworkId",
                table: "Frameworks");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Frameworks");
        }
    }
}
