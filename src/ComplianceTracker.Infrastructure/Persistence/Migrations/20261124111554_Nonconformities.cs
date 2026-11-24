using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Nonconformities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Nonconformities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Correction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Cause = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActionOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ActionDoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CheckedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckResult = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailedActions = table.Column<string>(type: "jsonb", nullable: false),
                    RaisedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nonconformities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nonconformities_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Nonconformities_OrganizationId",
                table: "Nonconformities",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Nonconformities");
        }
    }
}
