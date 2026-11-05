using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionAppliesFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AppliesFrom",
                table: "FrameworkVersions",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"),
                column: "AppliesFrom",
                value: new DateOnly(2027, 12, 11));

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"),
                column: "AppliesFrom",
                value: new DateOnly(2026, 8, 2));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesFrom",
                table: "FrameworkVersions");
        }
    }
}
