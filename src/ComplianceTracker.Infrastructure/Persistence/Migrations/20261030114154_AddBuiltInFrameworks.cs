using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuiltInFrameworks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "FrameworkVersions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                table: "FrameworkVersions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(64)",
                oldFixedLength: true,
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "FrameworkVersions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerOrganizationId",
                table: "Frameworks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "OrganizationFrameworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnabledById = table.Column<Guid>(type: "uuid", nullable: false),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisabledById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationFrameworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationFrameworks_FrameworkVersions_FrameworkVersionId",
                        column: x => x.FrameworkVersionId,
                        principalTable: "FrameworkVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationFrameworks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationFrameworks_Users_DisabledById",
                        column: x => x.DisabledById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationFrameworks_Users_EnabledById",
                        column: x => x.EnabledById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Frameworks",
                columns: new[] { "Id", "Name", "OwnerOrganizationId" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"), "CRA – Regulation (EU) 2024/2847", null },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03"), "AI Act – Regulation (EU) 2024/1689", null }
                });

            migrationBuilder.InsertData(
                table: "FrameworkVersions",
                columns: new[] { "Id", "FileName", "FrameworkId", "Number", "Sha256", "SizeInBytes", "SourceReference", "UploadedAt", "UploadedById" },
                values: new object[,]
                {
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"), "", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"), 1, "", 0L, "https://eur-lex.europa.eu/eli/reg/2024/2847/oj", new DateTime(2024, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"), "", new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03"), 1, "", 0L, "https://eur-lex.europa.eu/eli/reg/2024/1689/oj", new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationFrameworks_DisabledById",
                table: "OrganizationFrameworks",
                column: "DisabledById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationFrameworks_EnabledById",
                table: "OrganizationFrameworks",
                column: "EnabledById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationFrameworks_FrameworkVersionId",
                table: "OrganizationFrameworks",
                column: "FrameworkVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationFrameworks_OrganizationId_FrameworkVersionId",
                table: "OrganizationFrameworks",
                columns: new[] { "OrganizationId", "FrameworkVersionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationFrameworks");

            migrationBuilder.DeleteData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"));

            migrationBuilder.DeleteData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"));

            migrationBuilder.DeleteData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"));

            migrationBuilder.DeleteData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03"));

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "FrameworkVersions");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "FrameworkVersions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                table: "FrameworkVersions",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerOrganizationId",
                table: "Frameworks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
