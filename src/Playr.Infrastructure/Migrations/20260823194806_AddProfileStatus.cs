using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LookingForPlayers",
                table: "UserProfiles");

            migrationBuilder.AddColumn<Guid>(
                name: "LookingForGameId",
                table: "UserProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LookingForPlayStyle",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_LookingForGameId",
                table: "UserProfiles",
                column: "LookingForGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Games_LookingForGameId",
                table: "UserProfiles",
                column: "LookingForGameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Games_LookingForGameId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_LookingForGameId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LookingForGameId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LookingForPlayStyle",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserProfiles");

            migrationBuilder.AddColumn<bool>(
                name: "LookingForPlayers",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
