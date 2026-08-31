using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTypicalPlayTimeToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TypicalPlayTime",
                table: "UserProfiles");

            migrationBuilder.AddColumn<string>(
                name: "TypicalPlayTimes",
                table: "UserProfiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TypicalPlayTimes",
                table: "UserProfiles");

            migrationBuilder.AddColumn<string>(
                name: "TypicalPlayTime",
                table: "UserProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }
    }
}
