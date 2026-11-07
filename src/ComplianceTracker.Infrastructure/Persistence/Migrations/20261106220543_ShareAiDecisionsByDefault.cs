using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShareAiDecisionsByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AiSharingChangedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiSharingChangedById",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromAiSharing",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShareAiFeedbackSince",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            // US-130 R7: existing organisations share from now on, except those whose admin switched sharing off on
            // purpose. ShareAiFeedbackSince = now, so their decisions from before are never used (they did not agree to it).
            // The activity log is only read here; it is never written outside the app, so its fingerprints stay intact.
            migrationBuilder.Sql("""
                UPDATE "Organizations" AS o
                SET "ShareAiFeedback" = TRUE, "ShareAiFeedbackSince" = now()
                WHERE o."ShareAiFeedback" = FALSE
                  AND NOT EXISTS (
                    SELECT 1 FROM "ActivityLog" AS l
                    WHERE l."SubjectId" = o."Id" AND l."Action" = 'AiFeedbackSharingChanged' AND l."Details" = 'Sharing off');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSharingChangedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AiSharingChangedById",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ExcludedFromAiSharing",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ShareAiFeedbackSince",
                table: "Organizations");
        }
    }
}
