using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRerankScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RankingModel",
                table: "MappingSuggestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SearchScore",
                table: "MappingSuggestions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RankingModel",
                table: "EvidenceLinkSuggestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SearchScore",
                table: "EvidenceLinkSuggestions",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RankingModel",
                table: "MappingSuggestions");

            migrationBuilder.DropColumn(
                name: "SearchScore",
                table: "MappingSuggestions");

            migrationBuilder.DropColumn(
                name: "RankingModel",
                table: "EvidenceLinkSuggestions");

            migrationBuilder.DropColumn(
                name: "SearchScore",
                table: "EvidenceLinkSuggestions");
        }
    }
}
