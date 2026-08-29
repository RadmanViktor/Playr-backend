using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "CoverImageUrl", "Genre", "Name" },
                values: new object[,]
                {
                    { new Guid("00000001-0000-0000-0000-000000000009"), null, null, "Doom" },
                    { new Guid("00000001-0000-0000-0000-00000000000a"), null, null, "Dota 2" },
                    { new Guid("00000001-0000-0000-0000-00000000000b"), null, null, "League of Legends" },
                    { new Guid("00000001-0000-0000-0000-00000000000c"), null, null, "EA Sports FC 25" },
                    { new Guid("00000001-0000-0000-0000-00000000000d"), null, null, "UFC 6" },
                    { new Guid("00000001-0000-0000-0000-00000000000e"), null, null, "Hogwarts Legacy" },
                    { new Guid("00000001-0000-0000-0000-00000000000f"), null, null, "Unravel Two" },
                    { new Guid("00000001-0000-0000-0000-000000000010"), null, null, "Marvel's Spider-Man 2" },
                    { new Guid("00000001-0000-0000-0000-000000000011"), null, null, "God of War Ragnarök" },
                    { new Guid("00000001-0000-0000-0000-000000000012"), null, null, "The Last of Us Part II" },
                    { new Guid("00000001-0000-0000-0000-000000000013"), null, null, "Horizon Forbidden West" },
                    { new Guid("00000001-0000-0000-0000-000000000014"), null, null, "Grand Theft Auto V" },
                    { new Guid("00000001-0000-0000-0000-000000000015"), null, null, "Minecraft" },
                    { new Guid("00000001-0000-0000-0000-000000000016"), null, null, "Fortnite" },
                    { new Guid("00000001-0000-0000-0000-000000000017"), null, null, "Overwatch 2" },
                    { new Guid("00000001-0000-0000-0000-000000000018"), null, null, "Rocket League" },
                    { new Guid("00000001-0000-0000-0000-000000000019"), null, null, "Rainbow Six Siege" },
                    { new Guid("00000001-0000-0000-0000-00000000001a"), null, null, "Pokémon Scarlet/Violet" },
                    { new Guid("00000001-0000-0000-0000-00000000001b"), null, null, "Mario Kart 8 Deluxe" },
                    { new Guid("00000001-0000-0000-0000-00000000001c"), null, null, "The Legend of Zelda: Tears of the Kingdom" },
                    { new Guid("00000001-0000-0000-0000-00000000001d"), null, null, "Baldur's Gate 3" },
                    { new Guid("00000001-0000-0000-0000-00000000001e"), null, null, "Stardew Valley" },
                    { new Guid("00000001-0000-0000-0000-00000000001f"), null, null, "It Takes Two" },
                    { new Guid("00000001-0000-0000-0000-000000000020"), null, null, "Cyberpunk 2077" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000001a"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000001b"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000001c"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000001d"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000001e"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-00000000001f"));

            migrationBuilder.DeleteData(
                table: "Games",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000020"));
        }
    }
}
