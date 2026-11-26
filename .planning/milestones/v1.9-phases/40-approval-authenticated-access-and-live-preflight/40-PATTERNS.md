# Phase 40 Patterns

## Backend Patterns

- Put persisted hosting entities under `sources/Api/Data/Hosting` and expose them through `ApiDbContext`.
- Use explicit EF relationships in `ApiDbContext.OnModelCreating` when delete behavior must be restricted.
- Put public response records under `sources/Api/DTO` and payload records under `sources/Api/DTO/Payloads`.
- Map persisted entities to DTOs through `sources/Api/Transform.cs`.
- Register services with `X39.Util.DependencyInjection` attributes and inject abstractions into controllers.
- Append hosting audit through `IHostingAuditService`, which applies sanitizer behavior to summaries/details.
- Use server-scoped `GameServersController` routes for operator/server-owner cloud workflow actions and admin-only `CloudInventoryController` for global cloud inventory surfaces.
- Return `Unauthorized`, `NotFound`, `Forbid`, `BadRequest`, `Conflict`, and `Ok` consistently with existing controller methods.

## Testing Patterns

- Use SQLite in-memory `ApiDbContext` test fixtures and `EnsureCreatedAsync` for EF-backed API/service tests.
- Seed minimal users, server, owner assignment, provider configuration, metadata sync state, dry-run plan, cost estimate, approval request, and reconciliation run directly or through existing services.
- Use fake policy/provisioning/teardown services when verifying controller boundaries.
- Add source-boundary tests under `tests/Api.Tests/WebApp` for WebApp repository/UI wiring when generated Kiota client code is not hand-edited.
- Verify secret-safety with both source assertions and DTO/audit content assertions.

## UI Patterns

- Use existing Blazor WebAssembly and Microsoft Fluent UI components.
- Keep preflight UI in dense operational panels under `/servers` and `/servers/cloud`.
- Use the Phase 40 UI-SPEC copy exactly for primary actions:
  - `Record preflight approval`
  - `Refresh preflight evidence`
- Use compact status rows and existing cloud workflow card/list styling; do not add a landing page or marketing hero.
- Do not display feature instructions, shortcuts, raw credentials, provider tokens, cookies, bearer values, or raw audit JSON.

## Migration Patterns

- Prefer `dotnet ef migrations add` for EF model changes when available.
- Treat generated migration files as generated output. Do not hand-edit generated designer/snapshot files unless tool execution is blocked and the change is mechanical and reviewed.

