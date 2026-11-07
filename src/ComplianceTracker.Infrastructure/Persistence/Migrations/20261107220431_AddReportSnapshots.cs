using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LockedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LockedRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_Users_LockedById",
                        column: x => x.LockedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportSnapshotFiles",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSnapshotFiles", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_ReportSnapshotFiles_ReportSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "ReportSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_LockedById",
                table: "ReportSnapshots",
                column: "LockedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_OrganizationId_Sha256",
                table: "ReportSnapshots",
                columns: new[] { "OrganizationId", "Sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_ProjectId",
                table: "ReportSnapshots",
                column: "ProjectId");

            // US-050 R2: a locked report can never be changed or deleted, like the activity log (US-091)
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION report_snapshots_are_locked() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'A locked report cannot be changed or deleted';
                END;
                $$;

                CREATE TRIGGER report_snapshots_no_update_or_delete
                    BEFORE UPDATE OR DELETE ON "ReportSnapshots"
                    FOR EACH ROW EXECUTE FUNCTION report_snapshots_are_locked();
                CREATE TRIGGER report_snapshots_no_truncate
                    BEFORE TRUNCATE ON "ReportSnapshots"
                    FOR EACH STATEMENT EXECUTE FUNCTION report_snapshots_are_locked();
                CREATE TRIGGER report_snapshot_files_no_update_or_delete
                    BEFORE UPDATE OR DELETE ON "ReportSnapshotFiles"
                    FOR EACH ROW EXECUTE FUNCTION report_snapshots_are_locked();
                CREATE TRIGGER report_snapshot_files_no_truncate
                    BEFORE TRUNCATE ON "ReportSnapshotFiles"
                    FOR EACH STATEMENT EXECUTE FUNCTION report_snapshots_are_locked();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS report_snapshot_files_no_truncate ON "ReportSnapshotFiles";
                DROP TRIGGER IF EXISTS report_snapshot_files_no_update_or_delete ON "ReportSnapshotFiles";
                DROP TRIGGER IF EXISTS report_snapshots_no_truncate ON "ReportSnapshots";
                DROP TRIGGER IF EXISTS report_snapshots_no_update_or_delete ON "ReportSnapshots";
                DROP FUNCTION IF EXISTS report_snapshots_are_locked();
                """);

            migrationBuilder.DropTable(
                name: "ReportSnapshotFiles");

            migrationBuilder.DropTable(
                name: "ReportSnapshots");
        }
    }
}
