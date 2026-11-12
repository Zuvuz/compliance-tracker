using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSboms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sboms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sboms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sboms_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sboms_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SbomComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SbomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    License = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Supplier = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SbomComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SbomComponents_Sboms_SbomId",
                        column: x => x.SbomId,
                        principalTable: "Sboms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SbomFiles",
                columns: table => new
                {
                    SbomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SbomFiles", x => x.SbomId);
                    table.ForeignKey(
                        name: "FK_SbomFiles_Sboms_SbomId",
                        column: x => x.SbomId,
                        principalTable: "Sboms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SbomComponents_SbomId",
                table: "SbomComponents",
                column: "SbomId");

            migrationBuilder.CreateIndex(
                name: "IX_Sboms_ProjectId_ImportedAt",
                table: "Sboms",
                columns: new[] { "ProjectId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sboms_UploadedById",
                table: "Sboms",
                column: "UploadedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SbomComponents");

            migrationBuilder.DropTable(
                name: "SbomFiles");

            migrationBuilder.DropTable(
                name: "Sboms");
        }
    }
}
