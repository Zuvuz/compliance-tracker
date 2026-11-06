using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoneById = table.Column<Guid>(type: "uuid", nullable: true),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskActions_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskActions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskActions_Users_DoneById",
                        column: x => x.DoneById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskActions_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskActionLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskActionLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskActionLinks_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskActionLinks_RiskActions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "RiskActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskActionLinks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskActionLinks_ActionId",
                table: "RiskActionLinks",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActionLinks_EvidenceId",
                table: "RiskActionLinks",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActionLinks_WorkItemId",
                table: "RiskActionLinks",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_CreatedById",
                table: "RiskActions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_DoneById",
                table: "RiskActions",
                column: "DoneById");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_OwnerId",
                table: "RiskActions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_RiskId",
                table: "RiskActions",
                column: "RiskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskActionLinks");

            migrationBuilder.DropTable(
                name: "RiskActions");
        }
    }
}
