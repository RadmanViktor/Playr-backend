# Final Claims Fix Report

Status: DONE

Changes:
- Added `ClaimsPrincipalExtensions.TryGetUserId(out Guid userId)` for non-throwing user id claim parsing.
- Updated `AuthController.Me` and `ProfilesController.UpdateMe` to return `Unauthorized(new { error = "User id claim is missing or invalid." })` when the user id claim is missing or malformed.
- Added focused controller tests for missing and invalid user id claims without requiring PostgreSQL.

Verification:
- `dotnet test Playr.sln`: passed, 56 tests, 0 failures.
- `dotnet build Playr.sln`: succeeded, 0 warnings, 0 errors.

Concerns: None.
