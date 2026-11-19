using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentedVersusProven : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChosenKind",
                table: "Evidence",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KindChangedAt",
                table: "Evidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KindChangedById",
                table: "Evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enough = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentationDecisions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentationDecisions_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentationDecisions_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_KindChangedById",
                table: "Evidence",
                column: "KindChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationDecisions_DecidedById",
                table: "DocumentationDecisions",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationDecisions_OrganizationId_RequirementId_Decided~",
                table: "DocumentationDecisions",
                columns: new[] { "OrganizationId", "RequirementId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationDecisions_RequirementId",
                table: "DocumentationDecisions",
                column: "RequirementId");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidence_Users_KindChangedById",
                table: "Evidence",
                column: "KindChangedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidence_Users_KindChangedById",
                table: "Evidence");

            migrationBuilder.DropTable(
                name: "DocumentationDecisions");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_KindChangedById",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "ChosenKind",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "KindChangedAt",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "KindChangedById",
                table: "Evidence");
        }
    }
}
