using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AzureDevOpsLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AzureDevOps_AreaPath",
                table: "Projects",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AzureDevOps_ConnectedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AzureDevOps_ConnectedById",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOps_EncryptedToken",
                table: "Projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOps_LastSyncError",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AzureDevOps_LastSyncedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOps_Organization",
                table: "Projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureDevOps_Project",
                table: "Projects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AzureDevOps_TokenChangedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AzureSkippedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AzureDevOpsLinkProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzureSkippedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AzureSkippedItems_Projects_AzureDevOpsLinkProjectId",
                        column: x => x.AzureDevOpsLinkProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AzureSkippedItems_AzureDevOpsLinkProjectId",
                table: "AzureSkippedItems",
                column: "AzureDevOpsLinkProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AzureSkippedItems");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_AreaPath",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_ConnectedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_ConnectedById",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_EncryptedToken",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_LastSyncError",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_LastSyncedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_Organization",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_Project",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AzureDevOps_TokenChangedAt",
                table: "Projects");
        }
    }
}
