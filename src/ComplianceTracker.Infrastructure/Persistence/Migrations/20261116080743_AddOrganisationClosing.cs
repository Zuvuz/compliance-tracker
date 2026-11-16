using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClosesOn",
                table: "Organizations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosingStartedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosingStartedById",
                table: "Organizations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ClosesOn",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ClosingStartedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ClosingStartedById",
                table: "Organizations");
        }
    }
}
