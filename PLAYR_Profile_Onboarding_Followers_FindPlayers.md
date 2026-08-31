# PLAYR – Profile, Onboarding, Followers & Find Players

Implement the following functionality for PLAYR.

The goal is to make the user profile independent of Steam and instead build a first-class gaming identity based on data the user adds directly in PLAYR.

Steam and other external services should only be optional integrations that enrich the profile.

---

## Task 1 – Build onboarding flow for new users

Create an onboarding flow shown after account creation, or the first time a profile is missing core gaming data.

The onboarding should be simple, visual, and split into multiple steps.

All onboarding data must be editable later from profile settings.

### Step 1 – Welcome

Show a heading similar to:

**Build your gaming profile**

Short description:

> Tell us what you play and what kind of player you are. You can change everything later.

CTA:

**Get started**

---

### Step 2 – Platforms

Allow the user to select one or more platforms.

Examples:

- PC
- PlayStation
- Xbox
- Nintendo

Use visually selectable cards or chips rather than standard checkboxes.

---

### Step 3 – Genres

Allow the user to select the genres they enjoy.

Examples:

- FPS
- RPG
- Survival
- MMO
- Strategy
- Horror
- Racing
- Sports
- Co-op
- Indie

Use multi-select chips.

Selected chips should use the existing PLAYR purple visual language.

---

### Step 4 – Games

Allow the user to search for games from PLAYR's shared game registry.

Example:

```text
Search for games...

Counter-Strike 2
[ Add ]

The Witcher 3
[ Add ]
```

Selected games should be displayed visually with cover art.

Avoid free-text game names.

Games should reference actual entries in PLAYR's game database so the data can later be used for discovery, matching, profile pages, and activity.

---

### Step 5 – Playing now

Allow the user to mark one or more games from their library as:

**Playing now**

Allow an optional short status.

Examples:

```text
Counter-Strike 2
Grinding Premier with friends
```

```text
Elden Ring
First playthrough
```

This data should appear directly on the user's profile.

---

### Step 6 – Playstyle

Allow the user to optionally define gaming preferences.

#### Playstyle

- Casual
- Competitive
- Both

#### Usually playing

- Solo
- With friends
- Looking for players

#### Typical play time

- Evenings
- Weekends
- Daytime
- Varies

---

### Step 7 – Profile customization

Allow the user to configure:

- Avatar
- Cover image
- Bio

Finish with:

**Finish profile**

The user should always be able to edit these values later.

---

## Task 2 – Rebuild Profile Overview

The profile overview must not depend on Steam data.

Remove Steam-specific dashboard cards such as:

- Steam playtime
- Most played
- Steam achievements

The profile should instead primarily show PLAYR-owned data.

### Profile header

The header should contain:

- Cover image
- Avatar
- Display name
- Username
- Bio
- Platforms
- Optional genres
- Followers
- Following
- Friends

Example:

```text
Olzzz
@olzzz

FPS · RPG · PC · PS5

Games, coffee and questionable Counter-Strike decisions.

128 Followers · 74 Following · 16 Friends
```

---

## Task 3 – Rebuild Profile Navigation

Change the profile tabs to:

```text
Overview
Posts
Games
About
```

Do not use Steam as a primary profile tab.

Steam should instead live under integrations/connections.

---

## Task 4 – Add Playing Now section

Add a section to the profile overview called:

**Playing now**

Each item may contain:

- Game cover
- Game title
- Short status
- Platform
- Playstyle

Example:

```text
Counter-Strike 2

Grinding Premier with friends

PC · Competitive
```

The owner of the profile should be able to edit this directly from their own profile.

---

## Task 5 – Add Favorite Games

Allow users to select favorite games.

Show favorites visually on the Overview page.

Example:

```text
Favorite games

[ CS2 ]
[ Witcher 3 ]
[ Elden Ring ]
[ Minecraft ]
```

Keep the Overview concise, for example 4–6 favorites.

All games can be shown in the full **Games** tab.

---

## Task 6 – Build Games tab

Create a dedicated Games tab for the profile.

Suggested sections:

```text
Playing now
Favorites
Played
Want to play
```

All data should come from PLAYR's own game library.

Steam imports may help users populate this data later, but Steam must never be required.

---

## Task 7 – Add Recent Activity

Add a recent activity section to the Overview page.

Examples:

```text
Posted a new Counter-Strike clip
Started playing Elden Ring
Added The Witcher 3 to favorites
Updated gaming profile
```

Activity should be based on PLAYR-owned events.

---

## Task 8 – Add Latest Posts

Show a limited number of recent posts on the profile Overview.

For example 2–4 posts.

Each post preview may contain:

- Image/video thumbnail
- Game
- Text preview
- Date
- Likes
- Comments

Add a CTA:

**View all posts**

---

## Task 9 – Build About tab

The About tab should describe the user's gaming identity.

Example:

```text
Gaming

Platforms
PC · PlayStation 5

Genres
FPS · RPG · Survival

Playstyle
Competitive + Casual

Usually plays
Evenings · Weekends
```

Also show connected external accounts.

Example:

```text
Connected accounts

Steam       Connected
Twitch      Connected
YouTube     Not connected
Discord     Connected
```

External services are optional profile enrichments, not requirements.

---

## Task 10 – Implement Followers

Implement a follower system.

A follow relationship is **one-way**.

If User A follows User B, it means:

> User A wants to see more of User B's content and activity.

User B does not need to approve the relationship.

Example:

```text
A → follows → B
```

B does not need to follow A back.

Followers should primarily be used for:

- Feed
- Posts
- Gaming journey
- Clips
- Discovery
- Creator/social functionality

### Profile actions

On another user's profile, the primary CTA should be:

**Follow**

After following:

**Following**

Allow unfollowing.

Show:

```text
128 Followers
74 Following
```

### Suggested data model

```text
UserFollow

FollowerUserId
FollowingUserId
CreatedAt
```

The relationship should be unique per user pair.

---

## Task 11 – Relationship between Friends and Followers

Friends already exist in PLAYR and should remain a separate concept from followers.

### Follow

Follow means:

> I am interested in this person's content.

It does not require approval.

### Friend

Friend means:

> We have a mutual social relationship and may play or communicate together.

Friends are mutual.

Followers are directional.

A user should therefore be able to:

```text
Follow someone
WITHOUT
being their friend
```

It should also be possible to be friends while following each other.

When two users become friends, PLAYR may automatically create follow relationships in both directions.

If the friendship is later removed, do not automatically remove the follow relationships.

---

## Task 12 – Profile actions

When visiting another user's profile, actions should follow this pattern.

Default:

```text
[ Follow ] [ Add friend ] [ ••• ]
```

Already following:

```text
[ Following ] [ Add friend ] [ ••• ]
```

Friend request already sent:

```text
[ Following ] [ Request sent ] [ ••• ]
```

Already friends:

```text
[ Following ] [ Friends ] [ ••• ]
```

The overflow menu can later contain actions such as:

```text
Message
Unfollow
Remove friend
Block
Report
```

Use the already implemented friend system for all friend-related actions and states.

---

## Task 13 – Find Players integration

Keep the existing Find Players functionality, but update player result cards to use a clearer, more polished visual structure.

Each result should make it immediately obvious:

- Who the player is
- Which game they are looking to play
- Their platform
- Their playstyle
- Their current intent/status
- What actions are available

### Suggested card content

```text
@Olzzz

Counter-Strike 2
PC · Competitive

Looking for teammates

[ View profile ] [ Add friend ]
```

### Suggested visual hierarchy

Use a modern PLAYR-styled card with:

- Avatar on the left
- Username/display name as the strongest text
- Game cover or small game artwork/icon
- Game title clearly visible
- Platform and playstyle as muted metadata
- Status such as **Looking for teammates** displayed as a highlighted pill/badge
- Clear primary and secondary actions
- Existing dark UI
- Purple accents and subtle glow/border states
- Good spacing and strong hierarchy
- Avoid making the result look like a dense dashboard card

Example layout:

```text
┌────────────────────────────────────────────────────┐
│  [Avatar]  @Olzzz                                  │
│                                                    │
│            Counter-Strike 2                        │
│            PC · Competitive                        │
│                                                    │
│            ● Looking for teammates                 │
│                                                    │
│            [ View profile ]   [ Add friend ]       │
└────────────────────────────────────────────────────┘
```

The card should feel social and personal rather than purely statistical.

The result structure should be reusable for future discovery surfaces.

Future matching may use:

- Same games
- Platform
- Genres
- Playstyle
- Looking for players
- Typical playtime

This is one of the main reasons onboarding data should be stored in a structured way.

---

## Task 14 – Profile completeness

Add a profile completeness indicator visible only to the profile owner.

Example:

```text
Your profile

████████░░ 80%

+ Add favorite games
+ Add a bio
+ Connect an account
```

Do not show profile completeness publicly.

Use it only to help users finish setting up their profile.

---

# Product Principle

PLAYR must own the core gaming identity.

The following must work completely without Steam:

```text
Profile
Games
Playing now
Favorite games
Platforms
Genres
Playstyle
Posts
Followers
Friends
Activity
Find Players
```

Steam, Discord, Twitch, YouTube, and future services should be treated as:

**Connections / Integrations**

They may provide additional data, automation, or convenience, but PLAYR must never feel empty, incomplete, or broken when the user has not connected an external service.
