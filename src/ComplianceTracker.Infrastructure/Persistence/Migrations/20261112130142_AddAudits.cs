using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Scope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeldOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AuditorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuditorCompany = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReportFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ReportSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificateIssuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificateValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificateValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CorrectsAuditId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audits_Audits_CorrectsAuditId",
                        column: x => x.CorrectsAuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Audits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Audits_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Users_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditReportFiles",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditReportFiles", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_AuditReportFiles_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_AuditId",
                table: "AuditFindings",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_ClosedById",
                table: "AuditFindings",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_OwnerId",
                table: "AuditFindings",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_ProjectId",
                table: "AuditFindings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CorrectsAuditId",
                table: "Audits",
                column: "CorrectsAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_ProjectId",
                table: "Audits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_RecordedById",
                table: "Audits",
                column: "RecordedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditFindings");

            migrationBuilder.DropTable(
                name: "AuditReportFiles");

            migrationBuilder.DropTable(
                name: "Audits");
        }
    }
}
