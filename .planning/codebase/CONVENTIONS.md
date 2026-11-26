---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
# Coding Conventions

**Analysis Date:** 2026-11-18

Scope: active handwritten code under `sources/` and `tests/`. The `source/` tree is legacy and unreferenced; do not use it as a style source. Generated code is exempt from every rule here and must never be hand-edited: `sources/WebApp/UtfApi/**` (Kiota), `sources/Database/Migrations/**` (EF), `sources/ReactApp/src/generated/**` (openapi-typescript and locale export), `**/obj/**`, `*.Designer.cs`. To change generated output, change the generator input and regenerate (`npm run generate` in `sources/ReactApp`, Kiota for `UtfApi`, `dotnet ef migrations add` for migrations).

## Mandatory Rules (from `AGENTS.md`)

- **English only** for identifiers, comments, XML docs, and technical docs. Localized user-facing text goes into resource files (`Properties/Language*.resx` for Blazor, the locale sources exported by `sources/ReactApp/scripts/export-locales.mjs` for React).
- **Enums take an `E` prefix**, any visibility: `EDeploymentStatus` (`sources/HostingContracts/EDeploymentStatus.cs`), `ELifecycleJobStatus`, `EWorkerWorkloadState`, `EWorkerCommandJournalState`, `EEventAcceptance` (`sources/Common/Event/EEventAcceptance.cs`). Members are plain PascalCase.
- **XML documentation on every C# type and member**, with `<summary>` plus every applicable `<typeparam>`, `<param>`, `<returns>`, `<value>`, `<exception>`. Use `<inheritdoc />` on interface implementations/overrides. Placeholder docs that restate the name do not count. Existing code is inconsistent (e.g. `GameServerAccessService` in `sources/Api/Services/GameServerLifecycle/GameServerAccessService.cs` has no type summary, and implementations in `AutomaticCostStopService.cs` lack `<inheritdoc />`); new and touched code must comply.
- **One top-level type per file**, file named after the type. Only a same-name generic-arity family (`Type`, `Type<T>`) may share a file. Private nested helper types inside a test class are fine (they are not top-level).
- **EF migrations** are scaffolded only via `dotnet ef migrations add <Name> --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext --output-dir Migrations`; only add custom SQL to the generated `Up`/`Down`.
- **Commit subjects**: `type(scope)!,type(scope): summary`, under 80 chars, no Markdown, `version-bump` for version changes, `!` plus a `BREAKING CHANGE:` body for public API breaks. Example: `fix(api): reject redelivered worker commands with changed action`.

## Naming Patterns

**Files:**
- C#: `TypeName.cs`. Blazor components: `Name.razor` + `Name.razor.cs` partial (`sources/WebApp/Components/FluentMarkdown.razor.cs`).
- WebApp repositories: feature folder with `IXRepository.cs` + `XRepositoryImpl.cs` (`sources/WebApp/Services/EventRepository/`).
- Exceptions: `<Domain><Outcome>Exception.cs`, e.g. `sources/Api/Services/GameServerLifecycle/HostingConflictException.cs`, `HostingNotFoundException.cs`, `HostingForbiddenException.cs`, `sources/Api/Services/GameServerDefinition/ModSourceUnavailableException.cs`.
- React: components `PascalCase.tsx` (`src/features/admin/ModPackContentEditor.tsx`), modules camelCase (`src/features/hosting/model.ts`), tests `*.test.ts(x)` and kebab-case files under `sources/ReactApp/tests/` (`admin-event-page.test.tsx`).

**Functions:**
- PascalCase methods; async methods end in `Async` and take `CancellationToken cancellationToken = default` last (`TriggerAsync`, `ReconcileAsync`, `ClaimNextAsync`).
- TypeScript: camelCase functions (`formatBytes`).

**Variables:**
- camelCase locals/parameters; private fields `_camelCase` (`_options` in `DeploymentHealthReconciler.cs`); private static readonly fields PascalCase (`PermissionAllowlist` in `GameServerAccessService.cs`).
- LINQ lambda parameter is conventionally `q` (`.Where(q => q.PrimaryKey == deploymentId)`); use descriptive names for nested lambdas (`worker`, `command`).

**Types:**
- Interfaces `I*`; implementations `internal sealed class` registered via DI attributes.
- DTOs `*Dto` (`Plain*Dto`, `Full*Dto`), creation payloads `*CreationPayload`, partial updates `*Update` under `sources/Api/DTO/`.
- Options classes `*Options` (`LifecycleWorkerOptions`, `HetznerRuntimeOptions`).
- Entities: key `PrimaryKey`, foreign keys `*Fk` (`GameServerFk`), timestamps `TimeStamp*` (`TimeStampResolved`, `TimeStampStopped`).

## Code Style

**Formatting:**
- C#: `.editorconfig` (ReSharper + .NET rules) is authoritative. File-scoped namespaces, Allman braces (`csharp_new_line_before_open_brace = all`), max line 120, trailing commas in multi-line initializers, `[]` collection expressions, `var` for obvious types.
- Braceless single-line guard clauses are accepted:
  ```csharp
  if (_options.StaleThreshold <= TimeSpan.Zero)
      throw new InvalidOperationException("Hosting:Worker:StaleThreshold must be greater than zero.");
  ```
- Prefer primary constructors for DI services (`internal sealed class DeploymentHealthReconciler(DatabaseContext dbContext, ...)`); copy `IOptions<T>.Value` into a `_field` when used repeatedly.
- Use `.ConfigureAwait(false)` on awaits in API/worker service code (`sources/Api/Services/**`).
- Multi-line method signatures put one parameter per line.
- TypeScript: Prettier (`sources/ReactApp/.prettierrc`: `printWidth 100`, semicolons, double quotes, `trailingComma: all`).

**Linting:**
- C#: no analyzers/StyleCop; `dotnet build X39.UnitedTacticalForces.slnx` with nullable enabled is the static gate. Nullable and implicit usings are on in every project.
- TypeScript: `sources/ReactApp/eslint.config.js` (typescript-eslint recommended, react-hooks, `no-explicit-any: error`), run as `npm run lint` with `--max-warnings 0`; also `npm run typecheck`, `format:check`, `contracts:check`, `locales:check` (all chained in `npm run ci`).

## Import Organization

**Order (C#):**
1. `System.*` / aliases (`using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;`)
2. `Microsoft.*`
3. `X39.UnitedTacticalForces.*` and `X39.Util.*`
Ordering is loose in existing files; keep usings at file top, outside the namespace, and remove unused ones.

**Path Aliases:**
- C#: none; namespaces follow root namespace + folder (`X39.UnitedTacticalForces.Api.Services.GameServerLifecycle`). Test projects use short namespaces (`Api.Tests.GameServerHosting`, `CloudWorker.Tests`).
- TypeScript: `@/` maps to `sources/ReactApp/src` (`import { formatBytes } from "@/features/hosting/model";`).

## Error Handling

**Patterns:**
- Controllers return `Ok`, `NoContent`, `NotFound`, `BadRequest`, `Unauthorized`, `Forbid` and annotate with `ProducesResponseType`.
- Services throw domain exceptions (`HostingConflictException`, `HostingNotFoundException`, `HostingForbiddenException`, `HostingAffordabilityException`) that controllers map to HTTP results; never throw bare `Exception`.
- Configuration invariants throw `InvalidOperationException` with the config key in the message; enum switches default to `ArgumentOutOfRangeException`.
- Sanitize any external/provider failure text before persisting or logging: `HostingFailureSanitizer.Sanitize(faultMessage, 2048)`.
- Concurrency-sensitive mutations run in explicit transactions with row locks on PostgreSQL (`FOR UPDATE`) and `Serializable` fallback elsewhere (`AutomaticCostStopService.cs`).
- Worker command results use result objects (`WorkerCommandExecutionResult.Success(...)` / `.Failure(code, reason, message)`) rather than exceptions across the protocol boundary.
- Long-lived loops may swallow expected `OperationCanceledException`/`WebSocketException`/`ObjectDisposedException` only.
- Parser: `Parse` returns `ParseResult`; `ParseOrThrow` throws `FormatException` (`sources/X39.BI.Config.Parsing/ConfigParser.cs`).
- WebApp repositories translate Kiota `ApiException` 204 to `null`.

## Logging

**Framework:** `Microsoft.Extensions.Logging` (`ILogger<T>`) with OpenTelemetry export via `sources/ServiceDefaults/Extensions.cs`.

**Patterns:**
- Structured templates with named placeholders: `logger.LogInformation("Deployment {DeploymentId} stopped", id)`; no string interpolation in templates.
- Log infrastructure and state changes (provider allocation/cleanup, worker health, role changes); persist auditable hosting actions through `IHostingAuditWriter`.
- Never log Steam credentials, provider tokens, bootstrap/worker credentials, or command payloads (tests assert journals do not contain `payload`).

## Comments

**When to Comment:**
- Short inline comments only for non-obvious rationale or external references.

**JSDoc/TSDoc:**
- C# XML docs are mandatory (see top). Summaries state behavior and guarantees, e.g. "Converts a confirmed unattended deployment fault into the same durable Terminate path used for verified cost-stop."
- Test methods may carry `<summary>`/`<returns>` describing the verified guarantee (`FlatFileWorkerCommandJournalTests.cs`).
- No TSDoc convention in `sources/ReactApp`.

## Function Design

**Size:** Large orchestrating methods exist in lifecycle services; extract private helpers (`EnsureServerExistsAsync`, `Authorize`, `ResourceValue`) for reusable steps.

**Parameters:** Inject dependencies via constructor; pass `CancellationToken` last with `= default`; use named arguments for ambiguous literals (`CreateJob(seed, sequence: 1, priority: 0)`).

**Return Values:** `Task<T>`; `bool` for "did something" outcomes (`TriggerAsync`); nullable for not-found lookups; DTOs (never EF entities) from API endpoints.

## Module Design

**Exports:** Service implementations are `internal sealed`; register with `X39.Util.DependencyInjection` attributes, e.g. `[Scoped<AutomaticCostStopService, IAutomaticCostStopService>]` or `[Singleton<...>]`, discovered by `AddAttributedServicesFromAssemblyOf`. Use `InternalsVisibleTo` for test access rather than making types public.

**Barrel Files:** Not used in C#. React imports modules directly via `@/...` paths.

**Boundaries:** API owns control plane and providers (`sources/Api/Services/HostingRuntime`), game process execution lives only in `sources/CloudWorker` and `sources/GameServerImplementations`, shared wire types in `sources/HostingContracts` and `sources/WebSocketContracts`. Do not broaden shared contracts for convenience.

---

*Convention analysis: 2026-11-18*
