using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "ActivityLog",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousHash",
                table: "ActivityLog",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // US-091 R1: the database refuses to change or delete log entries, also from SQL outside the tracker,
            // unless an owner of the table turns the triggers off first (which is itself visible in the chain)
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION activity_log_is_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'The activity log cannot be changed';
                END;
                $$;

                CREATE TRIGGER activity_log_no_update_or_delete
                    BEFORE UPDATE OR DELETE ON "ActivityLog"
                    FOR EACH ROW EXECUTE FUNCTION activity_log_is_append_only();

                CREATE TRIGGER activity_log_no_truncate
                    BEFORE TRUNCATE ON "ActivityLog"
                    FOR EACH STATEMENT EXECUTE FUNCTION activity_log_is_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS activity_log_no_truncate ON "ActivityLog";
                DROP TRIGGER IF EXISTS activity_log_no_update_or_delete ON "ActivityLog";
                DROP FUNCTION IF EXISTS activity_log_is_append_only();
                """);

            migrationBuilder.DropColumn(
                name: "Hash",
                table: "ActivityLog");

            migrationBuilder.DropColumn(
                name: "PreviousHash",
                table: "ActivityLog");
        }
    }
}
