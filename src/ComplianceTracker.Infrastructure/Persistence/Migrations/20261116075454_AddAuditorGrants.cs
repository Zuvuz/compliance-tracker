using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditorGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AccessFrom",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AccessUntil",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OnlyProjectId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditorGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedById = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditorGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditorGrants_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditorGrants_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditorGrants_InvitationId",
                table: "AuditorGrants",
                column: "InvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditorGrants_OrganizationId",
                table: "AuditorGrants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditorGrants_ProjectId",
                table: "AuditorGrants",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditorGrants");

            migrationBuilder.DropColumn(
                name: "AccessFrom",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OnlyProjectId",
                table: "Users");
        }
    }
}
