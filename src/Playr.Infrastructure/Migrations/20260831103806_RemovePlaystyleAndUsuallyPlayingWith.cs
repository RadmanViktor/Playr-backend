using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaystyleAndUsuallyPlayingWith : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaystylePreference",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UsuallyPlayingWith",
                table: "UserProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaystylePreference",
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
        }
    }
}
