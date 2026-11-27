using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccessLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessLogRetentionMonths",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AccessLogStartsOn",
                table: "Organizations",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PageVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tab = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SecondsSpent = table.Column<int>(type: "integer", nullable: false),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    Ended = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageVisits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_OrganizationId_Kind_ItemId",
                table: "PageVisits",
                columns: new[] { "OrganizationId", "Kind", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_OrganizationId_ProjectId_LastSeenAt",
                table: "PageVisits",
                columns: new[] { "OrganizationId", "ProjectId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_OrganizationId_StartedAt",
                table: "PageVisits",
                columns: new[] { "OrganizationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_OrganizationId_UserId_StartedAt",
                table: "PageVisits",
                columns: new[] { "OrganizationId", "UserId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageVisits");

            migrationBuilder.DropColumn(
                name: "AccessLogRetentionMonths",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AccessLogStartsOn",
                table: "Organizations");
        }
    }
}
