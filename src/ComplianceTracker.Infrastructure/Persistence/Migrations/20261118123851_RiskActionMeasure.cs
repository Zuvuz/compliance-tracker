using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiskActionMeasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MeasureId",
                table: "RiskActions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskActions_MeasureId",
                table: "RiskActions",
                column: "MeasureId");

            migrationBuilder.AddForeignKey(
                name: "FK_RiskActions_RiskMeasures_MeasureId",
                table: "RiskActions",
                column: "MeasureId",
                principalTable: "RiskMeasures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiskActions_RiskMeasures_MeasureId",
                table: "RiskActions");

            migrationBuilder.DropIndex(
                name: "IX_RiskActions_MeasureId",
                table: "RiskActions");

            migrationBuilder.DropColumn(
                name: "MeasureId",
                table: "RiskActions");
        }
    }
}
