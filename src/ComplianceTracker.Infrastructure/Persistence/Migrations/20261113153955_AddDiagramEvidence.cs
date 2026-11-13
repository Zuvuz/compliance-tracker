using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagramEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiagramElementCount",
                table: "Evidence",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagramElements",
                table: "Evidence",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagramError",
                table: "Evidence",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiagramLinkCount",
                table: "Evidence",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagramLinks",
                table: "Evidence",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiagramTotalElements",
                table: "Evidence",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagramElementCount",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "DiagramElements",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "DiagramError",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "DiagramLinkCount",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "DiagramLinks",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "DiagramTotalElements",
                table: "Evidence");
        }
    }
}
