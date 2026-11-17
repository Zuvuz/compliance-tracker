using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Search : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Search",
                table: "WorkItems",
                columns: new[] { "ExternalReference", "Title", "Rules", "Description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Search",
                table: "Risks",
                columns: new[] { "Title", "Description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_Search",
                table: "Requirements",
                columns: new[] { "Code", "Title", "Description" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Search",
                table: "Projects",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_Search",
                table: "Evidence",
                column: "Title")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Search",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_Risks_Search",
                table: "Risks");

            migrationBuilder.DropIndex(
                name: "IX_Requirements_Search",
                table: "Requirements");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Search",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_Search",
                table: "Evidence");
        }
    }
}
