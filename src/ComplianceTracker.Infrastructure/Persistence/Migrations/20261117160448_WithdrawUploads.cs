using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WithdrawUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAt",
                table: "Scans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawnById",
                table: "Scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawnReason",
                table: "Scans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAt",
                table: "Sboms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawnById",
                table: "Sboms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawnReason",
                table: "Sboms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAt",
                table: "RequirementDocumentVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WithdrawnById",
                table: "RequirementDocumentVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawnReason",
                table: "RequirementDocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousFixedAt",
                table: "Findings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousLastScanId",
                table: "Findings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousState",
                table: "Findings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "WithdrawnById",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "WithdrawnReason",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "WithdrawnById",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "WithdrawnReason",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "RequirementDocumentVersions");

            migrationBuilder.DropColumn(
                name: "WithdrawnById",
                table: "RequirementDocumentVersions");

            migrationBuilder.DropColumn(
                name: "WithdrawnReason",
                table: "RequirementDocumentVersions");

            migrationBuilder.DropColumn(
                name: "PreviousFixedAt",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "PreviousLastScanId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "PreviousState",
                table: "Findings");
        }
    }
}
