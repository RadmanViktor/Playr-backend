# Design: Bild-/videouppladdning för inlägg

## Syfte

Användare ska kunna bifoga en bild eller video till ett inlägg (post), utöver text och humör. Media är valfritt.

## Arkitektur

- Ny lagringsabstraktion `IFileStorageService` i `Playr.Application` med en lokal disk-implementation `LocalFileStorageService` i `Playr.Infrastructure`. Detta gör det möjligt att byta ut mot molnlagring (S3/Azure Blob) senare utan att ändra Application/Api-lagren.
- Filer sparas lokalt på servern (t.ex. `wwwroot/uploads/posts/`) och serveras statiskt.
- `PostsController` ändras från JSON-body till `multipart/form-data` för create och update.

## Datamodell

`Post`-entiteten (`src/Playr.Domain/Posts/Post.cs`) utökas med:

- `MediaUrl` (`string?`, nullable) — relativ URL till filen, t.ex. `/uploads/posts/{guid}.jpg`
- `MediaType` (`PostMediaType?`, nullable enum: `Image`, `Video`)

Ingen separat media-tabell behövs eftersom det bara stöds **en fil per post**.

En EF Core-migration läggs till för dessa två kolumner, i linje med befintliga migrationer i `src/Playr.Infrastructure/Migrations/`.

### Begränsningar (defaults)

- Bilder: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif` — max 10 MB
- Video: `.mp4`, `.webm`, `.mov` — max 100 MB
- Validering av filändelse/content-type och storlek sker både i frontend (snabb feedback) och backend (säkerhet, auktoritativ).
- Filnamn genereras servern-sidan som `{Guid}.{ext}`; originalfilnamn används aldrig som filnamn (undviker path traversal/kollisioner).

## Backend API

- `POST /api/posts` — accepterar `multipart/form-data` med fälten `GameId`, `TextContent`, `Mood`, valfri `Media` (`IFormFile`). Om fil finns: validera typ/storlek → spara via `IFileStorageService` → sätt `MediaUrl`/`MediaType` på den skapade posten.
- `PUT /api/posts/{id}` — samma multipart-hantering, samt nytt fält `RemoveMedia: bool`:
  - `RemoveMedia = true` och ingen ny fil → befintlig media tas bort (fil raderas från disk, fälten nollställs).
  - Ny fil skickas → ersätter befintlig media (gammal fil raderas från disk).
  - Ingen fil och `RemoveMedia = false` → media lämnas oförändrad.
- `PostDto` och `PostResponse` utökas med `MediaUrl` och `MediaType`.
- Statiska filer serveras via `app.UseStaticFiles()` från uploads-mappen.
- Felhantering: ogiltig filtyp eller för stor fil → `400 Bad Request` med tydligt felmeddelande (samma valideringslogik återanvänds av create och update).

## Frontend

- Ny delad komponent `src/components/MediaUploadInput.tsx`: filväljare (`<input type="file" accept="image/*,video/*">`), förhandsvisning (bild-thumbnail eller `<video>`-preview), knapp för att ta bort vald/befintlig fil, samt klientvalidering av typ/storlek med felmeddelande.
- `CreatePostPage.tsx` använder `MediaUploadInput` för att låta användaren bifoga en fil vid skapande av inlägg.
- `PostCard.tsx` i redigeringsläge använder samma komponent, förifylld med befintlig media, för att byta ut eller ta bort den.
- `PostCard.tsx` i visningsläge renderar `<img>` om `MediaType === 'Image'` respektive `<video controls>` om `MediaType === 'Video'`.
- `postsApi.ts`: `createPost` och `updatePost` skickar alltid `FormData` (istället för JSON) för enhetlighet, med `Authorization`-header men utan explicit `Content-Type` (låter webbläsaren sätta multipart-boundary).

## Tester

Följ befintliga testmönster:

**Backend**
- `Playr.Application.Tests/Posts/PostServiceTests.cs` — utökas med tester för media (spara med/utan fil, validering av typ/storlek, ersättning, borttagning via `RemoveMedia`)
- `Playr.IntegrationTests/HttpPostsFlowTests.cs` — utökas med multipart-flöde end-to-end

**Frontend**
- `src/components/MediaUploadInput.test.tsx` — nytt, testar val/förhandsvisning/validering/borttagning
- `CreatePostPage.test.tsx` — utökas med scenario där fil bifogas
- `PostCard.test.tsx` — utökas med rendering av bild/video samt redigering av media
- `postsApi.test.ts` — utökas för FormData-anrop
