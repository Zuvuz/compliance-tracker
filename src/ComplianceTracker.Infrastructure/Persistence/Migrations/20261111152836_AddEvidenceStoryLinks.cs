using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceStoryLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvidenceStoryLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LinkedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceStoryLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceStoryLinks_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceStoryLinks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceStoryLinks_Users_LinkedById",
                        column: x => x.LinkedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceStoryLinks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceStorySuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MinimumScore = table.Column<double>(type: "double precision", nullable: false),
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
                    table.PrimaryKey("PK_EvidenceStorySuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceStorySuggestions_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceStorySuggestions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceStorySuggestions_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenceStorySuggestions_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStoryLinks_EvidenceId",
                table: "EvidenceStoryLinks",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStoryLinks_LinkedById",
                table: "EvidenceStoryLinks",
                column: "LinkedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStoryLinks_ProjectId_EvidenceId",
                table: "EvidenceStoryLinks",
                columns: new[] { "ProjectId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStoryLinks_WorkItemId",
                table: "EvidenceStoryLinks",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStorySuggestions_DecidedById",
                table: "EvidenceStorySuggestions",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStorySuggestions_EvidenceId",
                table: "EvidenceStorySuggestions",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStorySuggestions_ProjectId_EvidenceId",
                table: "EvidenceStorySuggestions",
                columns: new[] { "ProjectId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceStorySuggestions_WorkItemId",
                table: "EvidenceStorySuggestions",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvidenceStoryLinks");

            migrationBuilder.DropTable(
                name: "EvidenceStorySuggestions");
        }
    }
}
