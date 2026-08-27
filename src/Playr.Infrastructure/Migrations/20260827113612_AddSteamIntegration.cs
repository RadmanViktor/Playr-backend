using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSteamIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SteamAccounts",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SteamId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAccounts", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_SteamAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SteamAchievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppId = table.Column<long>(type: "bigint", nullable: false),
                    ApiName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IconGrayUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Achieved = table.Column<bool>(type: "boolean", nullable: false),
                    UnlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAchievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SteamOwnedGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PlaytimeForeverMinutes = table.Column<int>(type: "integer", nullable: false),
                    PlaytimeRecentMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamOwnedGames", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SteamAccounts_SteamId",
                table: "SteamAccounts",
                column: "SteamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SteamAchievements_UserId_AppId_ApiName",
                table: "SteamAchievements",
                columns: new[] { "UserId", "AppId", "ApiName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SteamOwnedGames_UserId_AppId",
                table: "SteamOwnedGames",
                columns: new[] { "UserId", "AppId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SteamAccounts");

            migrationBuilder.DropTable(
                name: "SteamAchievements");

            migrationBuilder.DropTable(
                name: "SteamOwnedGames");
        }
    }
}
