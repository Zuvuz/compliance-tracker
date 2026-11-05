using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityLog_OrganizationId",
                table: "ActivityLog");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_OrganizationId_OccurredAt",
                table: "ActivityLog",
                columns: new[] { "OrganizationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_OrganizationId_SubjectId_OccurredAt",
                table: "ActivityLog",
                columns: new[] { "OrganizationId", "SubjectId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityLog_OrganizationId_OccurredAt",
                table: "ActivityLog");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLog_OrganizationId_SubjectId_OccurredAt",
                table: "ActivityLog");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_OrganizationId",
                table: "ActivityLog",
                column: "OrganizationId");
        }
    }
}
