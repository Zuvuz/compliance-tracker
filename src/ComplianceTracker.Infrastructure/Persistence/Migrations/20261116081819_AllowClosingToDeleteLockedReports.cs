using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // US-184 R3: a closed organisation's locked reports are deleted with the rest of its data. They still can never be
    // changed, and deleting is allowed only for the organisation being closed, named in the transaction's setting
    // "ct.closing_organisation" (set by OrganisationData.DeleteAsync). Nothing else in the app sets it.
    public partial class AllowClosingToDeleteLockedReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION report_snapshots_are_locked() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    closing text := current_setting('ct.closing_organisation', true);
                BEGIN
                    IF TG_OP = 'DELETE' AND closing IS NOT NULL AND closing <> '' THEN
                        -- Nested, as PL/pgSQL may read OLD's field before it knows the table
                        IF TG_TABLE_NAME = 'ReportSnapshots' THEN
                            IF OLD."OrganizationId"::text = closing THEN
                                RETURN OLD;
                            END IF;
                        ELSIF TG_TABLE_NAME = 'ReportSnapshotFiles' THEN
                            IF EXISTS (SELECT 1 FROM "ReportSnapshots" s WHERE s."Id" = OLD."SnapshotId" AND s."OrganizationId"::text = closing) THEN
                                RETURN OLD;
                            END IF;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'A locked report cannot be changed or deleted';
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION report_snapshots_are_locked() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'A locked report cannot be changed or deleted';
                END;
                $$;
                """);
        }
    }
}
