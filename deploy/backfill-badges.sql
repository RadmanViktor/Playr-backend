-- One-off manual data fix (2026-09-01):
-- 1. Users registered before the badge feature shipped (2026-08-31) never had
--    CheckFirstHundredUsersAsync run for them, so the first 100 accounts by
--    CreatedAt are missing the Gold "FirstHundredUsers" badge. Backfill it.
-- 2. Grant RadmanViktor the Gold "Admin" badge.
--
-- Safe to re-run: uses ON CONFLICT on the (UserId, Type) unique index and only
-- touches ActiveBadgeType/Level when it is currently null, mirroring the
-- auto-activation behavior in BadgeService.UnlockIfHigherAsync.

BEGIN;

-- 1) Backfill Gold FirstHundredUsers for the first 100 accounts by CreatedAt.
WITH first_hundred AS (
    SELECT "Id"
    FROM "AspNetUsers"
    ORDER BY "CreatedAt" ASC
    LIMIT 100
)
INSERT INTO "UserBadges" ("Id", "UserId", "Type", "Level", "UnlockedAt")
SELECT gen_random_uuid(), fh."Id", 'FirstHundredUsers', 'Gold', now()
FROM first_hundred fh
ON CONFLICT ("UserId", "Type")
DO UPDATE SET "Level" = 'Gold', "UnlockedAt" = "UserBadges"."UnlockedAt";

WITH first_hundred AS (
    SELECT "Id"
    FROM "AspNetUsers"
    ORDER BY "CreatedAt" ASC
    LIMIT 100
)
UPDATE "UserProfiles" up
SET "ActiveBadgeType" = 'FirstHundredUsers', "ActiveBadgeLevel" = 'Gold'
FROM first_hundred fh
WHERE up."UserId" = fh."Id" AND up."ActiveBadgeType" IS NULL;

-- 2) Grant RadmanViktor the Gold Admin badge.
INSERT INTO "UserBadges" ("Id", "UserId", "Type", "Level", "UnlockedAt")
SELECT gen_random_uuid(), u."Id", 'Admin', 'Gold', now()
FROM "AspNetUsers" u
WHERE u."NormalizedUserName" = 'RADMANVIKTOR'
ON CONFLICT ("UserId", "Type")
DO UPDATE SET "Level" = 'Gold', "UnlockedAt" = now();

UPDATE "UserProfiles" up
SET "ActiveBadgeType" = 'Admin', "ActiveBadgeLevel" = 'Gold'
FROM "AspNetUsers" u
WHERE up."UserId" = u."Id" AND u."NormalizedUserName" = 'RADMANVIKTOR' AND up."ActiveBadgeType" IS NULL;

COMMIT;

-- Verification:
-- SELECT count(*) FROM "UserBadges" WHERE "Type" = 'FirstHundredUsers';
-- SELECT u."UserName", ub."Type", ub."Level" FROM "UserBadges" ub JOIN "AspNetUsers" u ON u."Id" = ub."UserId" WHERE ub."Type" = 'Admin';
