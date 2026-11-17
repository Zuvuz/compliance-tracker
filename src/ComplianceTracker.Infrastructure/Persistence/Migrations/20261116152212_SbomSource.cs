using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SbomSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "Sboms",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "Sboms",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Sboms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Upload");

            migrationBuilder.AddColumn<Guid>(
                name: "UploadTokenId",
                table: "Sboms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadTokenName",
                table: "Sboms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sboms_UploadTokenId_ImportedAt",
                table: "Sboms",
                columns: new[] { "UploadTokenId", "ImportedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sboms_ScanUploadTokens_UploadTokenId",
                table: "Sboms",
                column: "UploadTokenId",
                principalTable: "ScanUploadTokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sboms_ScanUploadTokens_UploadTokenId",
                table: "Sboms");

            migrationBuilder.DropIndex(
                name: "IX_Sboms_UploadTokenId_ImportedAt",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "UploadTokenId",
                table: "Sboms");

            migrationBuilder.DropColumn(
                name: "UploadTokenName",
                table: "Sboms");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedById",
                table: "Sboms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
