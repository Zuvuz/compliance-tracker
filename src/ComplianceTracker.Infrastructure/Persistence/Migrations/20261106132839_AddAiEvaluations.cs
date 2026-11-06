using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Decisions = table.Column<int>(type: "integer", nullable: false),
                    DecisionsFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionsTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Top1 = table.Column<double>(type: "double precision", nullable: true),
                    Top3 = table.Column<double>(type: "double precision", nullable: true),
                    Top10 = table.Column<double>(type: "double precision", nullable: true),
                    RejectedAboveLimit = table.Column<double>(type: "double precision", nullable: true),
                    Graded = table.Column<int>(type: "integer", nullable: false),
                    AverageGrade = table.Column<double>(type: "double precision", nullable: true),
                    RunBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiEvaluations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluations_Step_RunAt",
                table: "AiEvaluations",
                columns: new[] { "Step", "RunAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiEvaluations");
        }
    }
}
