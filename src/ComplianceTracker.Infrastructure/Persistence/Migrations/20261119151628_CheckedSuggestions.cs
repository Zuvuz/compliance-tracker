using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckedSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckModel",
                table: "RequirementMappings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Differs",
                table: "RequirementMappings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckMissing",
                table: "EvidenceLinkSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckModel",
                table: "EvidenceLinkSuggestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckPassage",
                table: "EvidenceLinkSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckVerdict",
                table: "EvidenceLinkSuggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckMissing",
                table: "DocumentLinkSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckModel",
                table: "DocumentLinkSuggestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckPassage",
                table: "DocumentLinkSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckVerdict",
                table: "DocumentLinkSuggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckedRight",
                table: "AiEvaluations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Found",
                table: "AiEvaluations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NotCheckedRight",
                table: "AiEvaluations",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckModel",
                table: "RequirementMappings");

            migrationBuilder.DropColumn(
                name: "Differs",
                table: "RequirementMappings");

            migrationBuilder.DropColumn(
                name: "CheckMissing",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckModel",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckPassage",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckVerdict",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckMissing",
                table: "DocumentLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckModel",
                table: "DocumentLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckPassage",
                table: "DocumentLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckVerdict",
                table: "DocumentLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "CheckedRight",
                table: "AiEvaluations");

            migrationBuilder.DropColumn(
                name: "Found",
                table: "AiEvaluations");

            migrationBuilder.DropColumn(
                name: "NotCheckedRight",
                table: "AiEvaluations");
        }
    }
}
