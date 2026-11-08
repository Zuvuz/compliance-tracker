using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationDocumentId",
                table: "Evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganisationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationDocuments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocuments_Users_AddedById",
                        column: x => x.AddedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocuments_Users_LastReviewedById",
                        column: x => x.LastReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocuments_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocuments_Users_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationDocumentFiles",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationDocumentFiles", x => new { x.DocumentId, x.Sha256 });
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentFiles_OrganisationDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "OrganisationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationDocumentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LinkedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationDocumentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentLinks_Frameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "Frameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentLinks_OrganisationDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "OrganisationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentLinks_Users_LinkedById",
                        column: x => x.LinkedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentLinks_Users_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentVersions_OrganisationDocuments_Document~",
                        column: x => x.DocumentId,
                        principalTable: "OrganisationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganisationDocumentVersions_Users_ReplacedById",
                        column: x => x.ReplacedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_OrganisationDocumentId",
                table: "Evidence",
                column: "OrganisationDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocumentLinks_DocumentId",
                table: "OrganisationDocumentLinks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocumentLinks_FrameworkId",
                table: "OrganisationDocumentLinks",
                column: "FrameworkId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocumentLinks_LinkedById",
                table: "OrganisationDocumentLinks",
                column: "LinkedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocumentLinks_RemovedById",
                table: "OrganisationDocumentLinks",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocuments_AddedById",
                table: "OrganisationDocuments",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocuments_LastReviewedById",
                table: "OrganisationDocuments",
                column: "LastReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocuments_OrganizationId",
                table: "OrganisationDocuments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocuments_OwnerId",
                table: "OrganisationDocuments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocuments_RemovedById",
                table: "OrganisationDocuments",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocumentVersions_DocumentId",
                table: "OrganisationDocumentVersions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationDocumentVersions_ReplacedById",
                table: "OrganisationDocumentVersions",
                column: "ReplacedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidence_OrganisationDocuments_OrganisationDocumentId",
                table: "Evidence",
                column: "OrganisationDocumentId",
                principalTable: "OrganisationDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidence_OrganisationDocuments_OrganisationDocumentId",
                table: "Evidence");

            migrationBuilder.DropTable(
                name: "OrganisationDocumentFiles");

            migrationBuilder.DropTable(
                name: "OrganisationDocumentLinks");

            migrationBuilder.DropTable(
                name: "OrganisationDocumentVersions");

            migrationBuilder.DropTable(
                name: "OrganisationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_OrganisationDocumentId",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "OrganisationDocumentId",
                table: "Evidence");
        }
    }
}
