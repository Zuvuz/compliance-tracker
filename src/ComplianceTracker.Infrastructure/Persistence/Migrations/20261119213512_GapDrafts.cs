using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GapDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "AlsoCovers",
                table: "StoryDrafts",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<string>(
                name: "Fits",
                table: "StoryDrafts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form",
                table: "StoryDrafts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UserStory");

            migrationBuilder.AddColumn<bool>(
                name: "FromGaps",
                table: "StoryDrafts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Postconditions",
                table: "StoryDrafts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlsoCovers",
                table: "StoryDrafts");

            migrationBuilder.DropColumn(
                name: "Fits",
                table: "StoryDrafts");

            migrationBuilder.DropColumn(
                name: "Form",
                table: "StoryDrafts");

            migrationBuilder.DropColumn(
                name: "FromGaps",
                table: "StoryDrafts");

            migrationBuilder.DropColumn(
                name: "Postconditions",
                table: "StoryDrafts");
        }
    }
}
