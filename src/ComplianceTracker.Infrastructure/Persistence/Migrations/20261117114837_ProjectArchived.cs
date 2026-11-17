using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedById",
                table: "Projects",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ArchivedById",
                table: "Projects");
        }
    }
}
