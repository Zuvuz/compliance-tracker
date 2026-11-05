using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ClassifiedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassifiedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectClassifications_Frameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "Frameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectClassifications_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectClassifications_Users_ClassifiedById",
                        column: x => x.ClassifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClassifications_ClassifiedById",
                table: "ProjectClassifications",
                column: "ClassifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClassifications_FrameworkId",
                table: "ProjectClassifications",
                column: "FrameworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectClassifications_ProjectId_FrameworkId_ClassifiedAt",
                table: "ProjectClassifications",
                columns: new[] { "ProjectId", "FrameworkId", "ClassifiedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectClassifications");
        }
    }
}
