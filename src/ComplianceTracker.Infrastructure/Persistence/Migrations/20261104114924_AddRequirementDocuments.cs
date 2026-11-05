using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ProjectId_Source_ExternalReference",
                table: "WorkItems");

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "WorkItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "WorkItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentVersion",
                table: "WorkItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemovedInVersion",
                table: "WorkItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequirementDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementDocuments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequirementDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Added = table.Column<int>(type: "integer", nullable: false),
                    Changed = table.Column<int>(type: "integer", nullable: false),
                    Removed = table.Column<int>(type: "integer", nullable: false),
                    SkippedRows = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementDocumentVersions_RequirementDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "RequirementDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementDocumentVersions_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequirementDocumentFiles",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementDocumentFiles", x => x.VersionId);
                    table.ForeignKey(
                        name: "FK_RequirementDocumentFiles_RequirementDocumentVersions_Versio~",
                        column: x => x.VersionId,
                        principalTable: "RequirementDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_DocumentId",
                table: "WorkItems",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId_Source_DocumentId_ExternalReference",
                table: "WorkItems",
                columns: new[] { "ProjectId", "Source", "DocumentId", "ExternalReference" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_RequirementDocuments_ProjectId",
                table: "RequirementDocuments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementDocumentVersions_DocumentId_Number",
                table: "RequirementDocumentVersions",
                columns: new[] { "DocumentId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequirementDocumentVersions_UploadedById",
                table: "RequirementDocumentVersions",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_RequirementDocuments_DocumentId",
                table: "WorkItems",
                column: "DocumentId",
                principalTable: "RequirementDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_RequirementDocuments_DocumentId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "RequirementDocumentFiles");

            migrationBuilder.DropTable(
                name: "RequirementDocumentVersions");

            migrationBuilder.DropTable(
                name: "RequirementDocuments");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_DocumentId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ProjectId_Source_DocumentId_ExternalReference",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DocumentVersion",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RemovedInVersion",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId_Source_ExternalReference",
                table: "WorkItems",
                columns: new[] { "ProjectId", "Source", "ExternalReference" },
                unique: true);
        }
    }
}
