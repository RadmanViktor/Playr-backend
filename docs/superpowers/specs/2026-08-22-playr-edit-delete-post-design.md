# PLAYR — Edit + Delete Post Design

## Context

PLAYR has a working post feed: users can create posts (game + text + optional mood)
and see them in a global feed via `PostCard` components. Posts have no edit or delete
capability yet. The `PostCard` currently has no action menu.

## Decisions (locked via brainstorm)

- **Who:** author-only. The `...` menu is only visible when the logged-in user is
  the post's author (`currentUserId === post.authorId`).
- **Edit UX:** inline — the card flips into edit mode in-place (no page navigation).
- **Delete UX:** inline confirm — "Delete this post? Delete / Cancel" appears in the
  card before removal.

## Non-goals

- Edit history / audit log.
- Soft delete (hard delete only).
- Admin/moderator override.
- Editing the game field (only text and mood are editable).

## Backend

### New application contracts

`UpdatePostCommand` record: `(string TextContent, string? Mood)`.

`IPostService` gains two new methods:
- `Task<PostDto> UpdateAsync(Guid postId, Guid requesterId, UpdatePostCommand command, CancellationToken)`
- `Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken)`

### Service behaviour (`PostService`)

**`UpdateAsync`:**
1. Trim and validate `TextContent` (required, ≤ 1000) — same rules as `CreateAsync`.
2. Parse/validate `Mood` (null allowed, or a defined `PostMood` name) — same rules.
3. Load the `Post` by `postId`; throw `InvalidOperationException("Post was not found.")` if missing.
4. If `post.AuthorId != requesterId` throw `InvalidOperationException("You are not allowed to edit this post.")`.
5. Update `TextContent` and `Mood`, save, return a fresh `PostDto` (same join approach as `CreateAsync`).

**`DeleteAsync`:**
1. Load the `Post` by `postId`; throw `InvalidOperationException("Post was not found.")` if missing.
2. If `post.AuthorId != requesterId` throw `InvalidOperationException("You are not allowed to delete this post.")`.
3. Remove and save.

### Error mapping in controller

| Condition | HTTP |
|---|---|
| Service throws `"Post was not found."` | `404 NotFound { error }` |
| Service throws `"You are not allowed to..."` | `403 Forbidden { error }` |
| Other `InvalidOperationException` | `400 BadRequest { error }` |
| Missing/invalid user-id claim | `401 Unauthorized { error }` |

### New API models

`UpdatePostRequest`: `[Required][StringLength(1000, MinimumLength=1)] string TextContent`,
`string? Mood`.

### New controller actions (`PostsController`)

- `PUT /api/posts/{id}` `[Authorize]` → `200 PostResponse` or 400/401/403/404.
- `DELETE /api/posts/{id}` `[Authorize]` → `204 NoContent` or 401/403/404.

### CORS

`Program.cs` CORS policy already allows `GET/POST/PUT/OPTIONS`.  
**`DELETE` must be added** to the `WithMethods(...)` list.

## Frontend

### `postsApi.ts` additions

```ts
updatePost(token: string, postId: string, data: { textContent: string; mood?: string | null }): Promise<PostFeedItem>
deletePost(token: string, postId: string): Promise<void>
```

Both follow the existing `ApiError` + `parseErrorMessage` pattern.

### `PostCard` changes

New optional props:
- `currentUserId?: string` — the logged-in user's id; determines whether `...` is shown.
- `onDelete?: (postId: string) => void` — called by the card after a successful delete.
- `onUpdate?: (post: PostFeedItem) => void` — called by the card after a successful save.

Internal state machine (string union, no external lib):
`'read' | 'menu-open' | 'editing' | 'confirming-delete'`

**`read`:** Normal card. `...` `IconButton` visible only when
`currentUserId === post.authorId`. Badge + text displayed normally.

**`menu-open`:** Small dropdown below `...` with two items:
- **Edit** — transitions to `'editing'`
- **Delete** — transitions to `'confirming-delete'`
Clicking outside or pressing Escape closes to `'read'`.

**`editing`:** The `textContent` paragraph is replaced by a `<textarea>` pre-filled
with current text (char counter, max 1000). The mood `Badge` is replaced by the
same mood button-picker as `CreatePostPage` (None/Enjoying/Frustrated/Completed/
Need Help), pre-selected to the current mood. **Save** and **Cancel** buttons appear.
- Save: calls `updatePost`, on success calls `onUpdate(updatedPost)` and returns to
  `'read'` showing updated data; on error shows error text in the card.
- Cancel: discards changes, returns to `'read'`.

**`confirming-delete`:** A row reads "Delete this post?" with a red **Delete** button
and a **Cancel** button.
- Delete: calls `deletePost`, on success calls `onDelete(post.id)`; on error shows
  error text in the card.
- Cancel: returns to `'read'`.

### `FeedPage` changes

- Reads `user` from `useAuth()`.
- Passes `currentUserId={user?.id}` to every `PostCard`.
- Passes `onDelete` callback: filters the post out of `posts` state.
- Passes `onUpdate` callback: replaces the matching post in `posts` state.

### Dropdown close-on-outside-click

Use a `useEffect` that adds a `mousedown` listener on `document` while in
`'menu-open'` state, removing it on cleanup. No external library.

## Testing

### Backend (xUnit, SQLite fixture — mirrors PostServiceTests)

- `UpdateAsync`: success updates text+mood; empty text rejected; text too long
  rejected; invalid mood rejected; post not found; requester not author → 403 error.
- `DeleteAsync`: success removes post; post not found; requester not author → 403 error.
- Endpoint config: `PUT /api/posts/{id}` requires `[Authorize]`; `DELETE /api/posts/{id}`
  requires `[Authorize]`; both are present on `PostsController`.
- CORS: `DELETE` method allowed.

### Frontend (Vitest + RTL)

- `postsApi`: `updatePost` sends PUT with bearer token; `deletePost` sends DELETE;
  both throw `ApiError` on non-OK.
- `PostCard`:
  - `...` button not rendered when `currentUserId` differs from `post.authorId`.
  - `...` button rendered when `currentUserId === post.authorId`.
  - Edit flow: opens editing state, textarea pre-filled, Save calls `updatePost`,
    `onUpdate` called with result.
  - Delete flow: opens confirm state, Delete calls `deletePost`, `onDelete` called.
  - Cancel in edit mode returns to read.
  - Cancel in confirm mode returns to read.

## Open questions

None.
