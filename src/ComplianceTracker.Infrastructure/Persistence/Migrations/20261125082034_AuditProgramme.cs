using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditProgramme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditProgrammes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Years = table.Column<int>(type: "integer", nullable: false),
                    ControlOwners = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditProgrammes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditProgrammes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannedAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgrammeId = table.Column<Guid>(type: "uuid", nullable: false),
                    On = table.Column<DateOnly>(type: "date", nullable: false),
                    Clauses = table.Column<List<string>>(type: "text[]", nullable: false),
                    Controls = table.Column<List<string>>(type: "text[]", nullable: false),
                    ProjectIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    AuditorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImpartialityReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAuditId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedAudits_AuditProgrammes_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalTable: "AuditProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditProgrammes_OrganizationId",
                table: "AuditProgrammes",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAudits_ProgrammeId",
                table: "PlannedAudits",
                column: "ProgrammeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannedAudits");

            migrationBuilder.DropTable(
                name: "AuditProgrammes");
        }
    }
}
