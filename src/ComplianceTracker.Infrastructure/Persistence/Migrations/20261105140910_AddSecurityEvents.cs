using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AwareAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FixAvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EarlyWarningSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EarlyWarningReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NotificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FinalReportSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalReportReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityEvents_Users_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityEvents_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ClosedById",
                table: "SecurityEvents",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ProjectId_AwareAt",
                table: "SecurityEvents",
                columns: new[] { "ProjectId", "AwareAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_RecordedById",
                table: "SecurityEvents",
                column: "RecordedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityEvents");
        }
    }
}
