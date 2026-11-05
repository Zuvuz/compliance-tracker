using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubProjectLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GitHub_ProjectNumber",
                table: "Projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHub_ProjectOwner",
                table: "Projects",
                type: "character varying(39)",
                maxLength: 39,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHub_ProjectTitle",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHub_ProjectNumber",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "GitHub_ProjectOwner",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "GitHub_ProjectTitle",
                table: "Projects");
        }
    }
}
