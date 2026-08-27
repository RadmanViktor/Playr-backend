using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChatBrowserNotificationsEnabled",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChatSoundEnabled",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatBrowserNotificationsEnabled",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ChatSoundEnabled",
                table: "UserProfiles");
        }
    }
}
