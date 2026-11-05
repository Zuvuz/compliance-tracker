using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequirementMethods",
                table: "Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "All");

            // US-104 R6: a project gets the methods it already uses; a project that uses none keeps all three
            migrationBuilder.Sql("""
                UPDATE "Projects" p SET "RequirementMethods" = used.methods
                FROM (
                    SELECT p2."Id",
                           concat_ws(', ',
                               CASE WHEN p2."GitHub_Repository" IS NOT NULL THEN 'GitHubUserStories' END,
                               CASE WHEN EXISTS (SELECT 1 FROM "RequirementDocuments" d WHERE d."ProjectId" = p2."Id" AND d."Kind" = 'RequirementSpecification') THEN 'RequirementSpecification' END,
                               CASE WHEN EXISTS (SELECT 1 FROM "RequirementDocuments" d WHERE d."ProjectId" = p2."Id" AND d."Kind" = 'UseCases') THEN 'UseCases' END) AS methods
                    FROM "Projects" p2
                ) used
                WHERE used."Id" = p."Id" AND used.methods <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequirementMethods",
                table: "Projects");
        }
    }
}
