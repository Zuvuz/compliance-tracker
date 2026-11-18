using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FrameworkRemoved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "Frameworks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemovedById",
                table: "Frameworks",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c01"),
                columns: new[] { "RemovedAt", "RemovedById" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c03"),
                columns: new[] { "RemovedAt", "RemovedById" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c06"),
                columns: new[] { "RemovedAt", "RemovedById" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c08"),
                columns: new[] { "RemovedAt", "RemovedById" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Frameworks",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0a"),
                columns: new[] { "RemovedAt", "RemovedById" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "Frameworks");

            migrationBuilder.DropColumn(
                name: "RemovedById",
                table: "Frameworks");
        }
    }
}
