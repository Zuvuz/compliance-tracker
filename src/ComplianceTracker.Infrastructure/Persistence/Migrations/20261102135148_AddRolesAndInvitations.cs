using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedRole",
                table: "Risks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedRole",
                table: "RequirementLinks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedRole",
                table: "RequirementApplicability",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedRole",
                table: "MappingSuggestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivatedRole",
                table: "FrameworkVersions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ActivityLog",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedById = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_AcceptedById",
                        column: x => x.AcceptedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c02"),
                column: "ActivatedRole",
                value: null);

            migrationBuilder.UpdateData(
                table: "FrameworkVersions",
                keyColumn: "Id",
                keyValue: new Guid("6a1f8c2e-0c4b-4d4e-9a51-5f1c2a3b4c04"),
                column: "ActivatedRole",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedById",
                table: "Invitations",
                column: "AcceptedById");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_CreatedById",
                table: "Invitations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OrganizationId",
                table: "Invitations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);
        
            // US-010: decisions made before roles existed were all made by organisation admins (every member was one),
            // so the role they had then is their role now. From here on, the role is stored when the decision is made.
            migrationBuilder.Sql(@"
                UPDATE ""ActivityLog"" a SET ""Role"" = u.""Role"" FROM ""Users"" u WHERE a.""UserId"" = u.""Id"";
                UPDATE ""MappingSuggestions"" s SET ""DecidedRole"" = u.""Role"" FROM ""Users"" u WHERE s.""DecidedById"" = u.""Id"";
                UPDATE ""RequirementLinks"" l SET ""LinkedRole"" = u.""Role"" FROM ""Users"" u WHERE l.""LinkedById"" = u.""Id"";
                UPDATE ""RequirementApplicability"" a SET ""DecidedRole"" = u.""Role"" FROM ""Users"" u WHERE a.""DecidedById"" = u.""Id"";
                UPDATE ""Risks"" r SET ""AcceptedRole"" = u.""Role"" FROM ""Users"" u WHERE r.""AcceptedById"" = u.""Id"";
                UPDATE ""FrameworkVersions"" v SET ""ActivatedRole"" = u.""Role"" FROM ""Users"" u WHERE v.""ActivatedById"" = u.""Id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropColumn(
                name: "AcceptedRole",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "LinkedRole",
                table: "RequirementLinks");

            migrationBuilder.DropColumn(
                name: "DecidedRole",
                table: "RequirementApplicability");

            migrationBuilder.DropColumn(
                name: "DecidedRole",
                table: "MappingSuggestions");

            migrationBuilder.DropColumn(
                name: "ActivatedRole",
                table: "FrameworkVersions");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ActivityLog");
        }
    }
}
