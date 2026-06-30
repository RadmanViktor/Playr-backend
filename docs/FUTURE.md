# Future Work

This file tracks important ideas and technical decisions that are intentionally deferred from the MVP.

## Games and Discovery

- When PLAYR needs discovery, search, or filtering, replace simple profile game-name arrays with real `Game` entities or integrate with a game database.
- Add searchable/filterable fields for games, platforms, region, languages, and playstyle.
- Consider a richer model for currently playing games, including status, progress, favorite roles, and preferred play times.

## Posts and Media

- Add profile posts/logs after the auth and profile foundation is stable.
- Support text posts with mood/status values such as Enjoying, Frustrated, Completed, and NeedHelp.
- Add image upload after storage strategy is decided.
- Add short video upload only after image upload and moderation concerns are understood.

## Social Features

- Add friends after profiles and discovery are useful.
- Add private chat later, because it increases complexity around privacy, notifications, and moderation.
- Add looking-for-players flows based on games, platforms, region, language, and playstyle.

## Discussions

- Add thread/discussion sections for game-specific conversations.
- Support categories such as help requests, recommendations, opinions, and finding players.

## Authentication

- Add email confirmation.
- Add password reset.
- Add refresh tokens.
- Add roles/admin permissions if moderation tools are needed.
- Add external login providers such as Steam, Discord, Twitch, YouTube, or Google if they become product requirements.

## Frontend

- Build Angular after the backend MVP endpoints are stable.
- Use a dark theme with gaming energy that still feels fresh and not overly nerdy.
- Design public profile pages around gaming identity, currently playing games, and looking-for-players status.
