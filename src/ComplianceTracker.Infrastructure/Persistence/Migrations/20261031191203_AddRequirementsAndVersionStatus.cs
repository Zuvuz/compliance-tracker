using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementsAndVersionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "FrameworkVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivatedById",
                table: "FrameworkVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FrameworkVersions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // US-007 decision: versions uploaded before drafts existed are already used by projects,
            // so they become active (locked) as they are. New versions are always written with their status.
            migrationBuilder.Sql(
                "UPDATE \"FrameworkVersions\" SET \"Status\" = 'Active', " +
                "\"ActivatedAt\" = \"UploadedAt\", \"ActivatedById\" = \"UploadedById\";");

            migrationBuilder.CreateTable(
                name: "Requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requirements_FrameworkVersions_FrameworkVersionId",
                        column: x => x.FrameworkVersionId,
                        principalTable: "FrameworkVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"),
                columns: new[] { "ActivatedAt", "ActivatedById", "Status" },
                values: new object[] { new DateTime(2024, 11, 20, 0, 0, 0, 0, DateTimeKind.Utc), null, "Active" });

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"),
                columns: new[] { "ActivatedAt", "ActivatedById", "Status" },
                values: new object[] { new DateTime(2024, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), null, "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkVersions_ActivatedById",
                table: "FrameworkVersions",
                column: "ActivatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_FrameworkVersionId_Code",
                table: "Requirements",
                columns: new[] { "FrameworkVersionId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FrameworkVersions_Users_ActivatedById",
                table: "FrameworkVersions",
                column: "ActivatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrameworkVersions_Users_ActivatedById",
                table: "FrameworkVersions");

            migrationBuilder.DropTable(
                name: "Requirements");

            migrationBuilder.DropIndex(
                name: "IX_FrameworkVersions_ActivatedById",
                table: "FrameworkVersions");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "FrameworkVersions");

            migrationBuilder.DropColumn(
                name: "ActivatedById",
                table: "FrameworkVersions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FrameworkVersions");
        }
    }
}
