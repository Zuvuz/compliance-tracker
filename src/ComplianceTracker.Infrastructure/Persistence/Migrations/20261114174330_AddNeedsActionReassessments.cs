using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNeedsActionReassessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContextChangedAt",
                table: "NeedsActionSuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NeedsActionReassessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reasons = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaitingForAi = table.Column<bool>(type: "boolean", nullable: false),
                    Result = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeedsActionReassessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NeedsActionReassessments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NeedsActionReassessments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NeedsActionReassessments_DoneAt_DueAt",
                table: "NeedsActionReassessments",
                columns: new[] { "DoneAt", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NeedsActionReassessments_OrganizationId",
                table: "NeedsActionReassessments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_NeedsActionReassessments_ProjectId_DoneAt",
                table: "NeedsActionReassessments",
                columns: new[] { "ProjectId", "DoneAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NeedsActionReassessments");

            migrationBuilder.DropColumn(
                name: "ContextChangedAt",
                table: "NeedsActionSuggestions");
        }
    }
}
