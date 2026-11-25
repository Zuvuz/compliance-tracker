using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArgumentConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArgumentConfirmedAt",
                table: "RiskMeasureSuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArgumentConfirmedAt",
                table: "MappingSuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArgumentConfirmedAt",
                table: "EvidenceStorySuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArgumentConfirmedAt",
                table: "EvidenceLinkSuggestions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArgumentConfirmedAt",
                table: "RiskMeasureSuggestions");

            migrationBuilder.DropColumn(
                name: "ArgumentConfirmedAt",
                table: "MappingSuggestions");

            migrationBuilder.DropColumn(
                name: "ArgumentConfirmedAt",
                table: "EvidenceStorySuggestions");

            migrationBuilder.DropColumn(
                name: "ArgumentConfirmedAt",
                table: "EvidenceLinkSuggestions");
        }
    }
}
