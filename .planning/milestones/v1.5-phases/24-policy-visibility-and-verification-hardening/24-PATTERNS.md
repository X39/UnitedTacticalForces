# Phase 24 Patterns

## Backend

- Put new read-composition logic under `sources/Api/Services/CloudPolicies`.
- Put API response shapes under `sources/Api/DTO` and map entities through `Transform.cs` when the shape is a direct entity DTO.
- Use `[Scoped<Implementation, Interface>]` for new services.
- Controllers should return `Ok(...)`, `NotFound()`, `Unauthorized()`, or `BadRequest()` in the style of existing cloud controllers.
- Admin endpoints use `[Authorize(Claims.Administrative.Server)]`.
- Server-scoped endpoints on `GameServersController` use resource authorization such as `[Authorize($"{Claims.ResourceBased.Server.All}:*")]`.
- Use `AsNoTracking()` for read-only visibility queries.
- Order recent operational rows by most recent timestamps, then primary key, and cap row counts to keep DTOs bounded.

## WebApp

- Keep generated Kiota code under `sources/WebApp/UtfApi` generated only.
- Add hand-written facade models to `CloudAdminModels`.
- Reuse `CloudAdminRepositoryImpl` mapping helpers for generated DTO conversion.
- Reuse the existing `/servers/cloud` layout: summary strip, tabbed data table, and sticky detail panel.
- Reuse existing status badge classes and neutral/success/warning/danger semantics.
- Use the selected-server detail panel on `/servers` for server-owner policy visibility rather than adding a new page.

## Verification

- Prefer focused `dotnet test tests\Api.Tests\Api.Tests.csproj --filter ...` runs for policy changes.
- Run API build and WebApp build after generated client and UI changes.
- Run GSD schema/codebase drift checks and document legacy warnings instead of hiding them.
- Record OPS-02 authenticated/live/provider UAT as operator-gated unless the operator explicitly supplies credentials, sessions, approval, and cleanup window.
