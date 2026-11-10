using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementKindsAndTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Requirements",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Requirement");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Requirements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableColumns",
                table: "Frameworks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"),
                column: "TableColumns",
                value: null);

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03"),
                column: "TableColumns",
                value: null);

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"),
                column: "TableColumns",
                value: null);

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c08"),
                column: "TableColumns",
                value: null);

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0a"),
                column: "TableColumns",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0101"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0102"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0103"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0104"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0105"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0106"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0107"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0108"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0109"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0110"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0111"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0112"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0113"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0114"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0115"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0116"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0117"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0118"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0119"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0120"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0121"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0122"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0201"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0202"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0203"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0204"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0205"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0206"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0207"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0208"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0209"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0210"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0211"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0212"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0213"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0214"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0215"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0216"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0217"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0218"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0219"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0220"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0221"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0222"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0223"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0224"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0225"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0226"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0227"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0228"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0229"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0230"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0231"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0232"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0233"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0234"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0235"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0236"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0237"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0238"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0239"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0240"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0241"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0242"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0243"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0244"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0245"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0246"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0247"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0248"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0249"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0250"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0251"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0252"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0253"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0254"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0255"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0256"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0257"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0258"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0259"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0260"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0261"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0262"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0263"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0264"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0265"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0266"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0267"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0268"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0269"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0270"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0271"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3c0272"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0401"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0402"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0403"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0404"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0405"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0406"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0407"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0408"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0409"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0410"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0411"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0412"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0413"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0414"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0415"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0416"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0417"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0418"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0419"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0420"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0421"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0422"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0423"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0424"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0425"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0426"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0427"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0428"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0429"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0430"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0431"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0501"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0502"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0503"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0504"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0505"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0506"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0507"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0508"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0509"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0510"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0511"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0512"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0513"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0514"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0515"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0516"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0517"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0518"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0519"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0520"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0521"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0522"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0523"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0601"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0602"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0603"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0604"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0605"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0606"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0607"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0608"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0609"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0610"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0611"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0612"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0613"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0614"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0615"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0616"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0617"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0618"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0619"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0620"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0621"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0622"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0623"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0701"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0702"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0703"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0704"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0705"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0706"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0707"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0708"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0709"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0710"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0711"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0712"),
                column: "ParentId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Requirements",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3d0713"),
                column: "ParentId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_ParentId",
                table: "Requirements",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requirements_ParentId",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Requirements");

            migrationBuilder.DropColumn(
                name: "TableColumns",
                table: "Frameworks");
        }
    }
}
