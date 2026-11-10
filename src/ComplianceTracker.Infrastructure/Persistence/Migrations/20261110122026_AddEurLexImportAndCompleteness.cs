using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEurLexImportAndCompleteness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Completeness",
                table: "FrameworkVersions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "ApprovalRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"),
                column: "Completeness",
                value: null);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"),
                column: "Completeness",
                value: null);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c05"),
                column: "Completeness",
                value: null);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c07"),
                column: "Completeness",
                value: null);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c09"),
                column: "Completeness",
                value: null);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c0b"),
                column: "Completeness",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completeness",
                table: "FrameworkVersions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "ApprovalRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
