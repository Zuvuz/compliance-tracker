using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEventUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "SecurityEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisclosedAt",
                table: "SecurityEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstAwareAt",
                table: "SecurityEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpgradeReason",
                table: "SecurityEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpgradedAt",
                table: "SecurityEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpgradedById",
                table: "SecurityEvents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "DisclosedAt",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "FirstAwareAt",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "UpgradeReason",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "UpgradedAt",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "UpgradedById",
                table: "SecurityEvents");
        }
    }
}
