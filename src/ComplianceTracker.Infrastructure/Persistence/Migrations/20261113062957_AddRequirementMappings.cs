using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequirementMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Strength = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementMappings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementMappings_Requirements_FromRequirementId",
                        column: x => x.FromRequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementMappings_Requirements_ToRequirementId",
                        column: x => x.ToRequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementMappings_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementMappings_DecidedById",
                table: "RequirementMappings",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementMappings_FromRequirementId",
                table: "RequirementMappings",
                column: "FromRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementMappings_OrganizationId_FromVersionId_ToVersionId",
                table: "RequirementMappings",
                columns: new[] { "OrganizationId", "FromVersionId", "ToVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementMappings_ToRequirementId",
                table: "RequirementMappings",
                column: "ToRequirementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementMappings");
        }
    }
}
