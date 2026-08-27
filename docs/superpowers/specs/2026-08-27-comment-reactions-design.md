# Design: Reaktioner på kommentarer

## Bakgrund

PLAYR har idag kommentarer på inlägg (`PostComment`) och gillningar på inlägg
(`PostLike`, ett toggle-mönster med en gilla-markering per användare och
inlägg). Detta dokument beskriver hur vi lägger till **emoji-reaktioner på
kommentarer** — en fristående funktion som låter användare reagera på en
kommentar med en av fem fördefinierade reaktionstyper.

## Krav

- En användare kan reagera på en kommentar med **en** av fem typer:
  Like, Haha, Wow, Sad, Angry.
- En användare kan bara ha **en aktiv reaktion per kommentar** åt gången.
  - Väljer man en ny typ ersätts den gamla.
  - Väljer man samma typ igen tas reaktionen bort (toggle av).
- Kommentardata (vid listning och vid enskild hämtning) ska innehålla:
  - Antal reaktioner **per typ** (inte bara totalsumma).
  - Vilken reaktionstyp (om någon) den inloggade användaren själv har satt.
- Funktionen ska följa samma arkitekturmönster och konventioner som
  befintlig kommentar- och gilla-funktionalitet (Domain → Application →
  Infrastructure → Api).

## Datamodell

Ny entitet i `src/Playr.Domain/Comments/CommentReaction.cs`:

```csharp
public enum ReactionType
{
    Like,
    Haha,
    Wow,
    Sad,
    Angry
}

public sealed class CommentReaction
{
    public Guid CommentId { get; set; }
    public PostComment Comment { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public ReactionType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- **Primärnyckel**: composite key `(CommentId, UserId)` — garanterar att en
  användare bara kan ha en rad (=en aktiv reaktion) per kommentar, precis som
  `PostLike` garanterar en gillning per användare/inlägg.
- **FK till `PostComment`**: `OnDelete(DeleteBehavior.Cascade)` — reaktioner
  försvinner när kommentaren tas bort.
- **FK till `ApplicationUser`**: `OnDelete(DeleteBehavior.Cascade)`.
- **Index**: på `CommentId` för snabb gruppering vid aggregering
  (räkna per typ över många kommentarer samtidigt).
- **`CreatedAt`**: samma unix-ms-konvertering för icke-Postgres providers som
  används för övriga tidsstämplar i `PlayrDbContext` (testkompatibilitet).

EF Core-konfiguration läggs i `PlayrDbContext.cs`, i samma stil som
`PostLike`-konfigurationen (rad ~124–138), samt en `DbSet<CommentReaction>
CommentReactions`.

## Applikationslager

### DTO:er (`src/Playr.Application/Comments/`)

```csharp
public sealed record ReactionCounts(int Like, int Haha, int Wow, int Sad, int Angry);

public sealed record CommentReactionSummary(
    ReactionCounts Counts,
    ReactionType? CurrentUserReaction);
```

`CommentDto` utökas med ett nytt fält:

```csharp
public sealed record CommentDto(
    Guid Id,
    Guid PostId,
    Guid AuthorId,
    string AuthorUsername,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    string TextContent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    CommentReactionSummary Reactions);
```

### `ICommentService` utökas med:

```csharp
Task<CommentReactionSummary> SetReactionAsync(
    Guid commentId, Guid userId, ReactionType type, CancellationToken cancellationToken);

Task<CommentReactionSummary> RemoveReactionAsync(
    Guid commentId, Guid userId, CancellationToken cancellationToken);
```

Ingen ny service-abstraktion behövs — reaktioner hör naturligt hemma i
`ICommentService`/`CommentService`, precis som gillningar hör hemma i
`IPostService`/`PostService`.

## Infrastrukturlager (`CommentService.cs`)

**`SetReactionAsync`**:
1. Verifiera att kommentaren finns (annars `InvalidOperationException("Comment was not found.")`, samma konvention som övriga metoder).
2. Hämta ev. befintlig `CommentReaction` för `(commentId, userId)`.
3. Om ingen finns → skapa ny rad med angiven `type`.
4. Om en finns med **samma** `type` → ta bort raden (toggle av).
5. Om en finns med **annan** `type` → uppdatera `Type` (och `CreatedAt`) på raden.
6. Spara, räkna om aggregering, returnera `CommentReactionSummary`.

**`RemoveReactionAsync`**:
1. Verifiera att kommentaren finns.
2. Ta bort ev. befintlig rad för `(commentId, userId)` (no-op om ingen finns).
3. Spara, räkna om aggregering, returnera `CommentReactionSummary`.

**Aggregering vid listning (`GetPagedAsync`)**:
För att undvika N+1-frågor batch-hämtas reaktionsdata för alla kommentarer i
sidan i en enda fråga, grupperat på `(CommentId, Type)` för räkning samt en
separat uppslagning av den anropande användarens egna reaktioner för samma
kommentar-ID:n — samma mönster som `commentCountMap` byggs i
`PostService.MapToPostDtoAsync` idag.

## API-lager

Nya endpoints i `CommentsController` (route-prefix
`api/posts/{postId}/comments/{commentId}`):

- **`PUT /reactions`** — body: `{ "type": "Like" }` (`SetReactionRequest`).
  Sätter/byter reaktion. Om samma typ redan är satt av användaren tas den bort
  (toggle-beteende), annars sätts/byts den. Kräver `[Authorize]`.
  Returnerar `200 OK` med `CommentReactionResponse` (motsvarar
  `CommentReactionSummary`).
- **`DELETE /reactions`** — tar bort den anropande användarens egen reaktion
  explicit, om någon finns. Kräver `[Authorize]`. Returnerar `200 OK` med
  uppdaterad `CommentReactionResponse`.

Felhantering följer befintlig konvention i `CommentsController`:
`InvalidOperationException` med meddelande som innehåller "was not found" →
404, "You are not allowed to" → 403, annars → 400.

Nya API-modeller i `src/Playr.Api/Models/Comments/`:
```csharp
public sealed record SetReactionRequest(string Type);

public sealed record ReactionCountsResponse(int Like, int Haha, int Wow, int Sad, int Angry);

public sealed record CommentReactionResponse(
    ReactionCountsResponse Counts,
    string? CurrentUserReaction);
```

`CommentResponse` och `PagedCommentResponse` utökas med ett `Reactions`-fält
av typen `CommentReactionResponse`.

`Type` i `SetReactionRequest` valideras mot `ReactionType`-enumet
(case-insensitive sträng → enum); ogiltigt värde ger `400 Bad Request` med
felmeddelande om giltiga värden.

## Testning

- Enhetstester för `CommentService.SetReactionAsync`/`RemoveReactionAsync`:
  skapa ny reaktion, byta typ, toggla av samma typ, ta bort explicit,
  kommentar hittas inte.
- Enhetstester för att `GetPagedAsync` korrekt aggregerar räkning per typ och
  användarens egen reaktion över flera kommentarer.
- Integrationstest för `PUT`/`DELETE /reactions`-endpoints: happy path,
  ej autentiserad (401), kommentar finns inte (404), ogiltig reaktionstyp
  (400).

Detta fyller även den tidigare identifierade luckan att `CommentService`
saknar tester överhuvudtaget — nya tester bör läggas i
`Playr.Application.Tests` för servicelogiken respektive
`Playr.IntegrationTests` för endpoints, i linje med befintlig teststruktur.

## Ej i scope

- Reaktioner på inlägg (endast `PostLike` finns och ändras inte).
- Notifieringar vid ny reaktion.
- Historik/logg över reaktionsändringar (endast senaste reaktionen per
  användare lagras).
