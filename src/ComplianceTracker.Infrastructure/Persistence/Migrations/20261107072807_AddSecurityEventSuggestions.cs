using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEventSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExploitedLists",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cves = table.Column<List<string>>(type: "text[]", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExploitedLists", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "SecurityEventSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpgradeEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    FindingIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    LearnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DismissedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEventSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityEventSuggestions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityEventSuggestions_SecurityEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "SecurityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityEventSuggestions_SecurityEvents_UpgradeEventId",
                        column: x => x.UpgradeEventId,
                        principalTable: "SecurityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityEventSuggestions_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventSuggestions_DecidedById",
                table: "SecurityEventSuggestions",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventSuggestions_EventId",
                table: "SecurityEventSuggestions",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventSuggestions_ProjectId_Key",
                table: "SecurityEventSuggestions",
                columns: new[] { "ProjectId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventSuggestions_UpgradeEventId",
                table: "SecurityEventSuggestions",
                column: "UpgradeEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExploitedLists");

            migrationBuilder.DropTable(
                name: "SecurityEventSuggestions");
        }
    }
}
