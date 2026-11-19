using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DefinitionOfDone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DoneAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DoneById",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DefinitionsOfDone",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UserStoryCriteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UseCaseCriteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequirementCriteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefinitionsOfDone", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefinitionsOfDone_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DefinitionsOfDone_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoneTicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Criterion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TickedById = table.Column<Guid>(type: "uuid", nullable: false),
                    TickedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UntickedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UntickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoneTicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoneTicks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoneTicks_Users_TickedById",
                        column: x => x.TickedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoneTicks_Users_UntickedById",
                        column: x => x.UntickedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoneTicks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefinitionsOfDone_ChangedById",
                table: "DefinitionsOfDone",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_DefinitionsOfDone_ProjectId",
                table: "DefinitionsOfDone",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoneTicks_ProjectId",
                table: "DoneTicks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DoneTicks_TickedById",
                table: "DoneTicks",
                column: "TickedById");

            migrationBuilder.CreateIndex(
                name: "IX_DoneTicks_UntickedById",
                table: "DoneTicks",
                column: "UntickedById");

            migrationBuilder.CreateIndex(
                name: "IX_DoneTicks_WorkItemId",
                table: "DoneTicks",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefinitionsOfDone");

            migrationBuilder.DropTable(
                name: "DoneTicks");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DoneAt",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DoneById",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                table: "WorkItems");
        }
    }
}
