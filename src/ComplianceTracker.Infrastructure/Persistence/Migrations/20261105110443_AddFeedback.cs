using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Page = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AsA = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IWant = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SoThat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Rules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Examples = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ScreenshotFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScreenshotContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScreenshotSizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    ScreenshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IssueNumber = table.Column<int>(type: "integer", nullable: true),
                    IssueUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DismissReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedback_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Feedback_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackScreenshots",
                columns: table => new
                {
                    FeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackScreenshots", x => x.FeedbackId);
                    table.ForeignKey(
                        name: "FK_FeedbackScreenshots_Feedback_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "Feedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_OrganizationId",
                table: "Feedback",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_UserId_SentAt",
                table: "Feedback",
                columns: new[] { "UserId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackScreenshots");

            migrationBuilder.DropTable(
                name: "Feedback");
        }
    }
}
