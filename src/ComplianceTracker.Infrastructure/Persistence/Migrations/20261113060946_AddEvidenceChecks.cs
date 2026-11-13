using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvidenceChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiVerdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Missing = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Quotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CheckedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedVerdict = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceChecks_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceChecks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceChecks_Users_CheckedById",
                        column: x => x.CheckedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceChecks_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceChecks_CheckedById",
                table: "EvidenceChecks",
                column: "CheckedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceChecks_DecidedById",
                table: "EvidenceChecks",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceChecks_EvidenceId",
                table: "EvidenceChecks",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceChecks_ProjectId",
                table: "EvidenceChecks",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvidenceChecks");
        }
    }
}
