using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsmsScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterestedParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Requirements = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterestedParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterestedParties_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IsmsScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Locations = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Departments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InternalIssues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExternalIssues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextReviewOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedSinceApproval = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsmsScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsmsScopes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IsmsScopeProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsInside = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsmsScopeProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsmsScopeProjects_IsmsScopes_ScopeId",
                        column: x => x.ScopeId,
                        principalTable: "IsmsScopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IsmsScopeProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterestedParties_OrganizationId",
                table: "InterestedParties",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_IsmsScopeProjects_ProjectId",
                table: "IsmsScopeProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IsmsScopeProjects_ScopeId_ProjectId",
                table: "IsmsScopeProjects",
                columns: new[] { "ScopeId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsmsScopes_OrganizationId",
                table: "IsmsScopes",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterestedParties");

            migrationBuilder.DropTable(
                name: "IsmsScopeProjects");

            migrationBuilder.DropTable(
                name: "IsmsScopes");
        }
    }
}
