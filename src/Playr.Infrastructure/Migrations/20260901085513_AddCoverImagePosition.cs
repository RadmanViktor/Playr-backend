using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverImagePosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CoverImagePositionX",
                table: "UserProfiles",
                type: "double precision",
                nullable: false,
                defaultValue: 50.0);

            migrationBuilder.AddColumn<double>(
                name: "CoverImagePositionY",
                table: "UserProfiles",
                type: "double precision",
                nullable: false,
                defaultValue: 50.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImagePositionX",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "CoverImagePositionY",
                table: "UserProfiles");
        }
    }
}
