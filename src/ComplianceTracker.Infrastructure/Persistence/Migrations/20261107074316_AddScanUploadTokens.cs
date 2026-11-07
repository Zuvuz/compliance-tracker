using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScanUploadTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                table: "Scans",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadTokenId",
                table: "Scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Scans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScanUploadTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanUploadTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanUploadTokens_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanUploadTokens_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanUploadTokens_Users_RevokedById",
                        column: x => x.RevokedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scans_UploadTokenId_ScannedAt",
                table: "Scans",
                columns: new[] { "UploadTokenId", "ScannedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanUploadTokens_CreatedById",
                table: "ScanUploadTokens",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ScanUploadTokens_Hash",
                table: "ScanUploadTokens",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanUploadTokens_ProjectId",
                table: "ScanUploadTokens",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanUploadTokens_RevokedById",
                table: "ScanUploadTokens",
                column: "RevokedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Scans_ScanUploadTokens_UploadTokenId",
                table: "Scans",
                column: "UploadTokenId",
                principalTable: "ScanUploadTokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Scans_ScanUploadTokens_UploadTokenId",
                table: "Scans");

            migrationBuilder.DropTable(
                name: "ScanUploadTokens");

            migrationBuilder.DropIndex(
                name: "IX_Scans_UploadTokenId_ScannedAt",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "CommitSha",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "UploadTokenId",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Scans");
        }
    }
}
