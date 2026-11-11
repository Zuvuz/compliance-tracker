using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionFingerprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EarlierRejection",
                table: "RiskMeasureSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedFingerprint",
                table: "RiskMeasureSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlierRejection",
                table: "MappingSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedFingerprint",
                table: "MappingSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlierRejection",
                table: "EvidenceStorySuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedFingerprint",
                table: "EvidenceStorySuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlierRejection",
                table: "EvidenceLinkSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedFingerprint",
                table: "EvidenceLinkSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlierRejection",
                table: "DocumentLinkSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedFingerprint",
                table: "DocumentLinkSuggestions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlierRejection",
                table: "RiskMeasureSuggestions");

            migrationBuilder.DropColumn(
                name: "RejectedFingerprint",
                table: "RiskMeasureSuggestions");

            migrationBuilder.DropColumn(
                name: "EarlierRejection",
                table: "MappingSuggestions");

            migrationBuilder.DropColumn(
                name: "RejectedFingerprint",
                table: "MappingSuggestions");

            migrationBuilder.DropColumn(
                name: "EarlierRejection",
                table: "EvidenceStorySuggestions");

            migrationBuilder.DropColumn(
                name: "RejectedFingerprint",
                table: "EvidenceStorySuggestions");

            migrationBuilder.DropColumn(
                name: "EarlierRejection",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "RejectedFingerprint",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "EarlierRejection",
                table: "DocumentLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "RejectedFingerprint",
                table: "DocumentLinkSuggestions");
        }
    }
}
