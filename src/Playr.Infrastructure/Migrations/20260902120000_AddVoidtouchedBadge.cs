using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Playr.Infrastructure.Data;

#nullable disable

namespace Playr.Infrastructure.Migrations;

[DbContext(typeof(PlayrDbContext))]
[Migration("20260902120000_AddVoidtouchedBadge")]
public sealed class AddVoidtouchedBadge : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH qualified_users AS (
                SELECT library."UserId", MAX(library."UpdatedAt") AS "UpdatedAt"
                FROM "UserGameLibraryEntries" AS library
                INNER JOIN "Games" AS game ON game."Id" = library."GameId"
                WHERE library."Rating" = 5
                  AND (
                    game."Id" = '00000001-0000-0000-0000-000000000007'
                    OR game."RawgId" = 9767
                  )
                GROUP BY library."UserId"
            )
            INSERT INTO "UserBadges" ("Id", "UserId", "Type", "Level", "UnlockedAt")
            SELECT
                md5(qualified."UserId"::text || ':Voidtouched:badge')::uuid,
                qualified."UserId",
                'Voidtouched',
                'Gold',
                qualified."UpdatedAt"
            FROM qualified_users AS qualified
            ON CONFLICT ("UserId", "Type") DO NOTHING;

            UPDATE "UserProfiles" AS profile
            SET "ActiveBadgeType" = 'Voidtouched', "ActiveBadgeLevel" = 'Gold'
            WHERE profile."ActiveBadgeType" IS NULL
              AND EXISTS (
                SELECT 1
                FROM "UserBadges" AS badge
                WHERE badge."UserId" = profile."UserId"
                  AND badge."Type" = 'Voidtouched'
              );

            INSERT INTO "Notifications" (
                "Id", "RecipientUserId", "ActorUserId", "Type", "BadgeType", "BadgeLevel", "IsRead", "CreatedAt")
            SELECT
                md5(badge."UserId"::text || ':Voidtouched:notification')::uuid,
                badge."UserId",
                badge."UserId",
                'BadgeUnlocked',
                'Voidtouched',
                'Gold',
                false,
                CURRENT_TIMESTAMP
            FROM "UserBadges" AS badge
            WHERE badge."Type" = 'Voidtouched'
              AND NOT EXISTS (
                SELECT 1
                FROM "Notifications" AS notification
                WHERE notification."RecipientUserId" = badge."UserId"
                  AND notification."Type" = 'BadgeUnlocked'
                  AND notification."BadgeType" = 'Voidtouched'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Notifications"
            WHERE "Type" = 'BadgeUnlocked' AND "BadgeType" = 'Voidtouched';

            UPDATE "UserProfiles"
            SET "ActiveBadgeType" = NULL, "ActiveBadgeLevel" = NULL
            WHERE "ActiveBadgeType" = 'Voidtouched';

            DELETE FROM "UserBadges" WHERE "Type" = 'Voidtouched';
            """);
    }
}
