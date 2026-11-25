using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Competence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RenewalMonths = table.Column<int>(type: "integer", nullable: true),
                    RequiredRoles = table.Column<string[]>(type: "text[]", nullable: false),
                    ForPeopleWithoutUser = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Competences_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffPeople",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPeople", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffPeople_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrainedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRecords_Competences_CompetenceId",
                        column: x => x.CompetenceId,
                        principalTable: "Competences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competences_OrganizationId",
                table: "Competences",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_DocumentId_UserId_Version",
                table: "PolicyAcknowledgements",
                columns: new[] { "DocumentId", "UserId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_OrganizationId",
                table: "PolicyAcknowledgements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPeople_OrganizationId",
                table: "StaffPeople",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_CompetenceId",
                table: "TrainingRecords",
                column: "CompetenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_OrganizationId",
                table: "TrainingRecords",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyAcknowledgements");

            migrationBuilder.DropTable(
                name: "StaffPeople");

            migrationBuilder.DropTable(
                name: "TrainingRecords");

            migrationBuilder.DropTable(
                name: "Competences");
        }
    }
}
