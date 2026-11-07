using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SignsForManagement",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OrganisationSeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Thumbprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProtectedCertificate = table.Column<string>(type: "text", nullable: false),
                    ProtectedPassword = table.Column<string>(type: "text", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationSeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationSeals_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationSeals_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportSignings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SealedSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SealSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SealThumbprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSignings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSignings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportSignings_ReportSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "ReportSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportSignings_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SigningId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SignerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignerRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSignatures_ReportSignings_SigningId",
                        column: x => x.SigningId,
                        principalTable: "ReportSignings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportSignatures_Users_SignerId",
                        column: x => x.SignerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SealedReportFiles",
                columns: table => new
                {
                    SigningId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SealedReportFiles", x => x.SigningId);
                    table.ForeignKey(
                        name: "FK_SealedReportFiles_ReportSignings_SigningId",
                        column: x => x.SigningId,
                        principalTable: "ReportSignings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationSeals_OrganizationId",
                table: "OrganisationSeals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationSeals_UploadedById",
                table: "OrganisationSeals",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSignatures_SignerId",
                table: "ReportSignatures",
                column: "SignerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSignatures_SigningId_Role",
                table: "ReportSignatures",
                columns: new[] { "SigningId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSignings_OrganizationId",
                table: "ReportSignings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSignings_RequestedById",
                table: "ReportSignings",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSignings_SnapshotId",
                table: "ReportSignings",
                column: "SnapshotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganisationSeals");

            migrationBuilder.DropTable(
                name: "ReportSignatures");

            migrationBuilder.DropTable(
                name: "SealedReportFiles");

            migrationBuilder.DropTable(
                name: "ReportSignings");

            migrationBuilder.DropColumn(
                name: "SignsForManagement",
                table: "Users");
        }
    }
}
