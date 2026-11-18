using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvidenceSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Evidence",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Uploaded");

            // US-201 R6: the evidence made before this from test results, an SBOM, a scan or a locked report, in the same project.
            // Test results by the accepted suggestion; the others by the file's SHA-256, or by the title the app gave them.
            migrationBuilder.Sql("""
                UPDATE "Evidence" e SET "Source" = 'TestResults'
                WHERE e."OrganisationDocumentId" IS NULL AND e."FileName" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "EvidenceSuggestions" s WHERE s."EvidenceId" = e."Id" AND s."Source" = 'TestRun');

                UPDATE "Evidence" e SET "Source" = 'Sbom'
                WHERE e."Source" = 'Uploaded' AND e."OrganisationDocumentId" IS NULL AND e."FileName" IS NOT NULL
                  AND (EXISTS (SELECT 1 FROM "Sboms" b WHERE b."ProjectId" = e."ProjectId" AND lower(b."Sha256") = lower(e."Sha256"))
                       OR e."Title" ~ '^SBOM .+ \(.+\)$');

                UPDATE "Evidence" e SET "Source" = 'Scan'
                WHERE e."Source" = 'Uploaded' AND e."OrganisationDocumentId" IS NULL AND e."FileName" IS NOT NULL
                  AND (EXISTS (SELECT 1 FROM "Scans" c WHERE c."ProjectId" = e."ProjectId" AND lower(c."Sha256") = lower(e."Sha256"))
                       OR e."Title" ~ '^.+ scan [0-9]{2}-[0-9]{2}-[0-9]{4}$');

                UPDATE "Evidence" e SET "Source" = 'Report'
                WHERE e."Source" = 'Uploaded' AND e."OrganisationDocumentId" IS NULL AND e."FileName" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "ReportSnapshots" r WHERE r."ProjectId" = e."ProjectId" AND lower(r."Sha256") = lower(e."Sha256"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Evidence");
        }
    }
}
