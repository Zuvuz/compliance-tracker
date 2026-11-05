using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LinkedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_Users_LinkedById",
                        column: x => x.LinkedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinks_Users_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceLinkSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RationaleModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceLinkSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceLinkSuggestions_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinkSuggestions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinkSuggestions_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceLinkSuggestions_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_ProjectId",
                table: "Evidence",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_EvidenceId_RequirementId",
                table: "EvidenceLinks",
                columns: new[] { "EvidenceId", "RequirementId" },
                unique: true,
                filter: "\"RemovedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_LinkedById",
                table: "EvidenceLinks",
                column: "LinkedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_ProjectId_RequirementId",
                table: "EvidenceLinks",
                columns: new[] { "ProjectId", "RequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_RemovedById",
                table: "EvidenceLinks",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinks_RequirementId",
                table: "EvidenceLinks",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinkSuggestions_DecidedById",
                table: "EvidenceLinkSuggestions",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinkSuggestions_EvidenceId",
                table: "EvidenceLinkSuggestions",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinkSuggestions_ProjectId_EvidenceId",
                table: "EvidenceLinkSuggestions",
                columns: new[] { "ProjectId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLinkSuggestions_RequirementId",
                table: "EvidenceLinkSuggestions",
                column: "RequirementId");

            // US-059 R10: every evidence from US-057 becomes one link to its requirement, with the same who and when.
            // Removed evidence gets a link too, so the history says what it proved.
            migrationBuilder.Sql("""
                INSERT INTO "EvidenceLinks" ("Id", "ProjectId", "EvidenceId", "RequirementId", "Source", "LinkedById", "LinkedRole", "LinkedAt", "RemovedById", "RemovedAt")
                SELECT gen_random_uuid(), "ProjectId", "Id", "RequirementId", 'Manual', "AddedById", "AddedRole", "AddedAt", "RemovedById", "RemovedAt"
                FROM "Evidence";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Evidence_Requirements_RequirementId",
                table: "Evidence");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_ProjectId_RequirementId",
                table: "Evidence");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_RequirementId",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "RequirementId",
                table: "Evidence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Evidence linked to several requirements keeps only its first link
            migrationBuilder.AddColumn<Guid>(
                name: "RequirementId",
                table: "Evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Evidence" e SET "RequirementId" = (
                    SELECT l."RequirementId" FROM "EvidenceLinks" l WHERE l."EvidenceId" = e."Id" ORDER BY l."LinkedAt" LIMIT 1);
                DELETE FROM "EvidenceDocuments" d USING "Evidence" e WHERE d."EvidenceId" = e."Id" AND e."RequirementId" IS NULL;
                DELETE FROM "Evidence" WHERE "RequirementId" IS NULL;
                ALTER TABLE "Evidence" ALTER COLUMN "RequirementId" SET NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "EvidenceLinks");

            migrationBuilder.DropTable(
                name: "EvidenceLinkSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_ProjectId",
                table: "Evidence");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_ProjectId_RequirementId",
                table: "Evidence",
                columns: new[] { "ProjectId", "RequirementId" });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_RequirementId",
                table: "Evidence",
                column: "RequirementId");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidence_Requirements_RequirementId",
                table: "Evidence",
                column: "RequirementId",
                principalTable: "Requirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
