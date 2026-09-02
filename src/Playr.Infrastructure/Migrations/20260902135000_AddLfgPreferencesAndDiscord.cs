using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Playr.Infrastructure.Data;

#nullable disable

namespace Playr.Infrastructure.Migrations;

[DbContext(typeof(PlayrDbContext))]
[Migration("20260902135000_AddLfgPreferencesAndDiscord")]
public sealed class AddLfgPreferencesAndDiscord : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.AddColumn<string>(
                name: "DiscordUsername",
                table: "UserProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LookingForPreferredMaxAge",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LookingForPreferredMinAge",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LookingForVoiceChatEnabled",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MicrophoneRequired",
                table: "LfgGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreferredMaxAge",
                table: "LfgGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredMinAge",
                table: "LfgGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserProfiles_LookingForPreferredAge",
                table: "UserProfiles",
                sql: "(\"LookingForPreferredMinAge\" IS NULL OR \"LookingForPreferredMinAge\" BETWEEN 13 AND 99) AND (\"LookingForPreferredMaxAge\" IS NULL OR \"LookingForPreferredMaxAge\" BETWEEN 13 AND 99) AND (\"LookingForPreferredMinAge\" IS NULL OR \"LookingForPreferredMaxAge\" IS NULL OR \"LookingForPreferredMinAge\" <= \"LookingForPreferredMaxAge\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LfgGroups_PreferredAge",
                table: "LfgGroups",
                sql: "(\"PreferredMinAge\" IS NULL OR \"PreferredMinAge\" BETWEEN 13 AND 99) AND (\"PreferredMaxAge\" IS NULL OR \"PreferredMaxAge\" BETWEEN 13 AND 99) AND (\"PreferredMinAge\" IS NULL OR \"PreferredMaxAge\" IS NULL OR \"PreferredMinAge\" <= \"PreferredMaxAge\")");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserProfiles_LookingForPreferredAge",
                table: "UserProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LfgGroups_PreferredAge",
                table: "LfgGroups");

            migrationBuilder.DropColumn(
                name: "DiscordUsername",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LookingForPreferredMaxAge",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LookingForPreferredMinAge",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LookingForVoiceChatEnabled",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "MicrophoneRequired",
                table: "LfgGroups");

            migrationBuilder.DropColumn(
                name: "PreferredMaxAge",
                table: "LfgGroups");

            migrationBuilder.DropColumn(
                name: "PreferredMinAge",
                table: "LfgGroups");
    }
}
