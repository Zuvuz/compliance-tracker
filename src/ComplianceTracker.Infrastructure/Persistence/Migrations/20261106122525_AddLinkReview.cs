using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CarriedFromRequirementId",
                table: "RequirementLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "RequirementLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "RequirementLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "RequirementLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarriedFromRequirementId",
                table: "RequirementApplicability",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "RequirementApplicability",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "RequirementApplicability",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "RequirementApplicability",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarriedFromRequirementId",
                table: "EvidenceLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "EvidenceLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "EvidenceLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "EvidenceLinks",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarriedFromRequirementId",
                table: "RequirementLinks");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "RequirementLinks");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "RequirementLinks");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "RequirementLinks");

            migrationBuilder.DropColumn(
                name: "CarriedFromRequirementId",
                table: "RequirementApplicability");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "RequirementApplicability");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "RequirementApplicability");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "RequirementApplicability");

            migrationBuilder.DropColumn(
                name: "CarriedFromRequirementId",
                table: "EvidenceLinks");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "EvidenceLinks");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "EvidenceLinks");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "EvidenceLinks");
        }
    }
}
