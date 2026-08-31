using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLfgGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_DirectUserAId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_DirectUserAId_DirectUserBId",
                table: "Conversations");

            migrationBuilder.AlterColumn<Guid>(
                name: "DirectUserBId",
                table: "Conversations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "DirectUserAId",
                table: "Conversations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "LfgGroupId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Conversations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Conversations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Direct");

            migrationBuilder.CreateTable(
                name: "LfgGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayStyle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PlayersWanted = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Open"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LfgGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LfgGroups_AspNetUsers_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LfgGroups_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LfgGroupApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LfgGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LfgGroupApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LfgGroupApplications_AspNetUsers_ApplicantUserId",
                        column: x => x.ApplicantUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LfgGroupApplications_LfgGroups_LfgGroupId",
                        column: x => x.LfgGroupId,
                        principalTable: "LfgGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LfgGroupInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LfgGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LfgGroupInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LfgGroupInvites_AspNetUsers_InviteeUserId",
                        column: x => x.InviteeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LfgGroupInvites_AspNetUsers_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LfgGroupInvites_LfgGroups_LfgGroupId",
                        column: x => x.LfgGroupId,
                        principalTable: "LfgGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LfgGroupMembers",
                columns: table => new
                {
                    LfgGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsCreator = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LfgGroupMembers", x => new { x.LfgGroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_LfgGroupMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LfgGroupMembers_LfgGroups_LfgGroupId",
                        column: x => x.LfgGroupId,
                        principalTable: "LfgGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DirectUserAId_DirectUserBId",
                table: "Conversations",
                columns: new[] { "DirectUserAId", "DirectUserBId" },
                unique: true,
                filter: "\"DirectUserAId\" IS NOT NULL AND \"DirectUserBId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LfgGroupId",
                table: "Conversations",
                column: "LfgGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroupApplications_ApplicantUserId",
                table: "LfgGroupApplications",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroupApplications_LfgGroupId_ApplicantUserId",
                table: "LfgGroupApplications",
                columns: new[] { "LfgGroupId", "ApplicantUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroupInvites_InviteeUserId",
                table: "LfgGroupInvites",
                column: "InviteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroupInvites_InviterUserId",
                table: "LfgGroupInvites",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroupInvites_LfgGroupId_InviteeUserId",
                table: "LfgGroupInvites",
                columns: new[] { "LfgGroupId", "InviteeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroupMembers_UserId",
                table: "LfgGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroups_CreatorUserId_Status",
                table: "LfgGroups",
                columns: new[] { "CreatorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LfgGroups_GameId",
                table: "LfgGroups",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_DirectUserAId",
                table: "Conversations",
                column: "DirectUserAId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AspNetUsers_DirectUserAId",
                table: "Conversations");

            migrationBuilder.DropTable(
                name: "LfgGroupApplications");

            migrationBuilder.DropTable(
                name: "LfgGroupInvites");

            migrationBuilder.DropTable(
                name: "LfgGroupMembers");

            migrationBuilder.DropTable(
                name: "LfgGroups");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_DirectUserAId_DirectUserBId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_LfgGroupId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LfgGroupId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Conversations");

            migrationBuilder.AlterColumn<Guid>(
                name: "DirectUserBId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DirectUserAId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DirectUserAId_DirectUserBId",
                table: "Conversations",
                columns: new[] { "DirectUserAId", "DirectUserBId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AspNetUsers_DirectUserAId",
                table: "Conversations",
                column: "DirectUserAId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
