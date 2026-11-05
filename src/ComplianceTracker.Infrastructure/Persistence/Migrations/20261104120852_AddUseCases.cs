using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUseCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UseCase",
                table: "WorkItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "RequirementDocuments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "RequirementSpecification"); // existing specifications hold requirements
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseCase",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "RequirementDocuments");
        }
    }
}
