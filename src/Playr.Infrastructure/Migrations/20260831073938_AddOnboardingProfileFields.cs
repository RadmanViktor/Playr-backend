using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CurrentlyPlayingGames",
                table: "UserProfiles",
                newName: "Genres");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "UserProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedOnboarding",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlaystylePreference",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypicalPlayTime",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsuallyPlayingWith",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserPlayingNows",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlayingNows", x => new { x.UserId, x.GameId });
                    table.ForeignKey(
                        name: "FK_UserPlayingNows_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlayingNows_GameId",
                table: "UserPlayingNows",
                column: "GameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPlayingNows");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "HasCompletedOnboarding",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PlaystylePreference",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TypicalPlayTime",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UsuallyPlayingWith",
                table: "UserProfiles");

            migrationBuilder.RenameColumn(
                name: "Genres",
                table: "UserProfiles",
                newName: "CurrentlyPlayingGames");
        }
    }
}
