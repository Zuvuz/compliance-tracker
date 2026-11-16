using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuditCertificateMonths",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 36);

            migrationBuilder.AddColumn<int>(
                name: "DocumentReviewMonths",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceValidityMonths",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<int>(
                name: "SupplierEvidenceMonths",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuditCertificateMonths",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DocumentReviewMonths",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EvidenceValidityMonths",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SupplierEvidenceMonths",
                table: "Organizations");
        }
    }
}
