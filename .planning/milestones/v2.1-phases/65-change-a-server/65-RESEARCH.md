# Phase 65: Change a Server - Research

**Researched:** 2026-11-20
**Domain:** Staged-draft editing of an existing cloud game server (React 19 + react-router 7 frontend, ASP.NET Core 10 / EF Core API) with a durable "applies on next start" signal
**Confidence:** HIGH (every finding below was read from live code this session; the three external behaviours are verified against `node_modules` types or are tagged ASSUMED)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Project-wide policy changes made in this discussion (user decisions)**
- **D-00a:** The current API is **not a frozen contract**. Docs that told agents to keep API changes minimal or avoid them were amended (AGENTS.md, PROJECT.md, ROADMAP.md, Phase 62/64 CONTEXT, research docs). Add or change API projections, endpoints, and behavior whenever the feature is better for it; keep architecture boundaries and regenerate the OpenAPI snapshot and generated clients.
- **D-00b:** **URLs are not a stable contract.** Routes, pathnames, and query parameters may change; the route manifest need not stay byte-identical. When routes change, update `sources/Common/Ui/routes.json` and the route tests together and keep them green. VER-01 and roadmap criterion 69.4 were reworded accordingly.

**Staging model (CHG-03)**
- **D-01:** One staged draft covers **mod pack, configuration profile, and configuration values** (and secret changes, see D-08). **File operations (upload, delete, create folder) stay immediate**; they act on files, not config, and uploads are slow.
- **D-02:** Draft is **in-memory only** (no browser-storage persistence), so there is no stale-draft risk.
- **D-03:** Review UI placement is Claude's discretion; default intent is a sticky pending-changes bar plus a before/after review step with Apply and Discard, usable at 320px and 390px.
- **D-04:** Partial apply failure behavior is Claude's discretion; default intent: applied items leave the draft, failed items stay staged with the reason, nothing is rolled back silently.

**Mod picker (CHG-01)**
- **D-05:** Capability-driven, game-agnostic **mod pack chooser** (current pack marked) using the server-scoped `GetModPackOptions` endpoint and `ChangeModPack`, with unavailable packs shown with a reason. The review shows a **contents diff** (current vs new pack). The API may gain a read-only pack-contents projection if the options DTO lacks it (D-00a). Validate with Arma 3 mod packs; Minecraft is not defined in the API.

**Settings and secrets (CHG-02, CHG-07)**
- **D-06:** **The API declares Common vs Advanced**: add a group/priority field to each configuration option in the game definition so every game decides its own Common set; the UI renders generically.
- **D-07:** Secret values are **masked with an explicit Reveal**, offered only to members allowed to manage secrets (`canManageSecrets`). Revealed values are fetched on demand, auto-hidden, and never stored in staged drafts, URLs, or logs.
- **D-08:** Setting or rotating a secret is a **staged change** in the review showing only "changed", never the value.

**Take effect and leave guard (CHG-04, CHG-05)**
- **D-09:** Add an **API "applies on next start" / pending-restart signal** so the UI shows a persistent banner only when applied changes truly wait for a start, plus a notice in the review step and after apply. This brings the restart-required drift badge (FUT-02) into scope for the signal needed here.
- **D-10:** The unsaved-changes guard covers **everything**: section switches (`?tab=`), router navigation (`useBlocker`), and browser tab close/reload (`beforeunload`). The prompt offers Review, Discard, or Stay.

**Files (CHG-06)**
- **D-11:** Current folder lives in `?path=` combined with the Files section; folder changes **push history** so Back returns to the previous folder. Invalid or missing paths fall back to root with a notice. Final mechanics are Claude's discretion.

### Claude's Discretion
Review UI placement (D-03), partial-failure semantics (D-04), exact pack-contents projection shape (D-05), the group field naming and fallback for options without a group (D-06), banner design (D-09), all copy and German strings, and component split under `src/components/server/` and `src/features/hosting/`.

### Deferred Ideas (OUT OF SCOPE)
- Mod catalog search/install (FUT-01), undo/revert (FUT-04): remain future.
- Server-defined presets (FUT-03) stay out (stated in the domain section of CONTEXT.md).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CHG-01 | Change a server's mods through a capability-driven picker using existing mod pack capabilities | Options DTO has only `PrimaryKey` and `Title` today (no availability, no contents); `ChangeModPackAsync` validates active + same game only and does not clear the pinned revision. See "API Changes" A1-A3 and Pitfalls 3, 4. |
| CHG-02 | Change configuration through settings grouped into Common and Advanced | `GameConfigurationDefinition` has no tier; `GameConfigurationDto` is **not in the OpenAPI snapshot**, so the new field needs `ProducesResponseType` annotations first. See A4 and Pitfall 1. |
| CHG-03 | Stage several changes, review, apply explicitly | Pure draft model + `useReducer`; apply = existing endpoints in fixed order with per-item results; no batch endpoint. See Architecture Patterns 1 and 4. |
| CHG-04 | UI states when changes take effect (next start) | New durable pending-start state on `GameServer`; consumption points are Start (deployment creation) and command creation. See A5 and Pitfall 5. |
| CHG-05 | Warn before leaving a section with unsaved changes | `useBlocker(fn)` + `beforeunload` in a new `useLeaveGuard`; data router already in use. See Pattern 5 and Pitfall 7. |
| CHG-06 | File manager with current folder in `?path=` | State lives in `useState` in `ServerFilesPanel`; 404 semantics and an infinite-fallback trap at root. See Pattern 6 and Pitfall 8. |
| CHG-07 | Secrets hidden unless allowed; never logged | Secrets controller gates on `ManageSecrets`/`RevealSecrets`; configuration API never returns sensitive entries; secrets service does not check archived. See Security Domain and Pitfalls 2 and 6. |
</phase_requirements>

## Summary

Phase 65 is mostly a frontend phase (draft model, review dialog, leave guard, files URL state) sitting on top of **five small but real API/DB changes**: a durable pending-start signal, mod pack availability and contents, a Common/Advanced tier on configuration options, OpenAPI annotation of the configuration endpoints, and a handful of correctness fixes in code the phase now depends on. The UI-SPEC (`65-UI-SPEC.md`) is already highly prescriptive for the UI; this research verifies it against live code and corrects or extends it where the code disagrees. Phase 64's primitives (`FormFields`, `Disclosure`, `ConfirmDialog`, `UnavailableReason`, `ActionButton`) are in place and the test harness (`renderWithProviders` with a memory data router, jsdom dialog polyfill) supports every test the UI-SPEC asks for.

The most important findings the planner would otherwise miss: (1) `GameConfigurationDto` is absent from `sources/ReactApp/openapi/utf-api.json` (grep count 0), so the tier field cannot reach the generated types until the configuration controller gets `ProducesResponseType`. (2) The configuration API **never returns and never accepts sensitive entries** (`if (definition.Sensitive) continue;` and `"Secrets must be managed through the server secret API."`), so the UI-SPEC's "sensitive configuration entry staged as changed" path cannot succeed and should be a defensive guard only. (3) Configuration and secrets are re-read at **every** worker command creation, but the mod pack is frozen into `GameServerDeployment.ContentSnapshot` at Start, so "applies on next start" is exactly true for mod packs and "next start or restart" for configuration and credentials; the pending signal must clear per area. (4) `ChangeModPackAsync` leaves `PinnedModPackRevisionFk` untouched, which makes a server created with a pinned revision unstartable after a pack change. (5) A newly chosen configuration profile has no revision id available to pin; the profile options DTO needs one.

**Primary recommendation:** Build backend first (one EF migration scaffolded with `dotnet ef`: `PendingStartChangeAreas` flags + `PendingStartSince` on `GameServer`; tier enum on the definition; pack options/contents projection; controller annotations; fixes), regenerate the OpenAPI snapshot and `api.ts`, then build the pure draft model with unit tests, then panels, dialog, leave guard, Files location. Apply the draft through the existing endpoints sequentially with per-item results; do **not** add a batch endpoint.

## Project Constraints (from AGENTS.md)

`./CLAUDE.md` does not exist; the binding file is `A:\dev\git\unitedTacticalForces\AGENTS.md` (read this session). Treat these as locked:

- All identifiers, comments, XML docs in English; localized text only in resource files (`en.json`/`de.json`).
- **Every enum type is named `E` + PascalCase** (new C# enums: `EGameConfigurationTier`, `EGameServerChangeArea`; TS union types are not enums but keep the vocabulary).
- Meaningful XML documentation on every handwritten C# type and member (`<summary>`, `<param>`, `<returns>`, `<value>`, `<exception>`); one top-level type per file named after the type.
- Generated code (`sources/WebApp/UtfApi`, EF migration designer files, `sources/ReactApp/src/generated/api.ts`) is exempt and must be regenerated, never hand-edited.
- API/DTO/DB changes needed by the feature are in scope with no separate approval; put each change in the layer that owns the data; regenerate OpenAPI snapshot and generated clients.
- **Legacy Blazor WebApp is not updated** for frontend work; touch `sources/WebApp` only if an API change would break its compatibility (additive fields do not).
- **EF migrations only via `dotnet ef migrations add`** (command in AGENTS.md); never hand-create the migration, designer, or snapshot; custom SQL may be added to the generated `Up`/`Down`.
- Architecture: API control plane never executes game processes; provider/worker/game boundaries stay separate. Nothing in this phase touches the worker.
- UI: no raw `<input>`/`<button>` as controls; reuse shared components; page files compose, shared components own presentation.
- Commits: cohesive, verified units; subject `type(scope): summary` under 80 chars; breaking changes need `!` and a `BREAKING CHANGE:` body.
- Secrets (Steam, provider, bootstrap, worker credentials) must not reach the frontend or unredacted logs.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Staged draft (state, normalization, apply plan) | Browser / Client | - | D-02: in-memory only; pure TS model, no storage. |
| Validation of staged values | Browser / Client (UX) | API / Backend (authoritative) | `validateConfigurationEntry` gives instant feedback; `ValidateAndClone` in the API is the real gate. |
| Apply of mod pack / profile / values / secrets | API / Backend | Browser (sequencing, results) | Existing audited endpoints; client orders calls and reports per item. |
| "Applies on next start" truth | API / Backend (database) | Browser (render only) | D-09: computed by the control plane, never inferred in the browser; must survive reload and be visible to other members. |
| Mod pack availability and contents | API / Backend | - | Resolution of nested packs, versions, and game scope already lives in `GameServerContentSnapshotResolver`. |
| Common/Advanced tier | API / Backend (game definition) | Browser (generic render) | D-06: each game declares its own Common set in `GameServerImplementations`. |
| Leave guard | Browser / Client | - | `useBlocker` + `beforeunload`; nothing server-side. |
| Files current folder | Browser / Client (URL `?path=`) | API (404 semantics) | Folder is view state; the API only lists. |
| Secret reveal | API / Backend (audited) | Browser (transient state, 30 s hide) | `RevealAsync` writes the audit record; the browser must never persist the value. |
| Live propagation of the banner to other sessions | API / Backend (`FleetChangeInterceptor`) | Browser (query invalidation) | The workspace already refetches on fleet signals for its own server. |

## Standard Stack

No new dependency is needed (confirmed against `sources/ReactApp/package.json`; the UI-SPEC also states none). All versions below are what is already pinned.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | already pinned [VERIFIED: sources/ReactApp/package.json] |
| react-router | 7.18.2 | Data router, `useBlocker`, `useSearchParams` | `createBrowserRouter` already used in `app/App.tsx:665`; `useBlocker(shouldBlock: boolean \| BlockerFunction)` verified in `node_modules/react-router/dist/development/index.d.ts:876` |
| @tanstack/react-query | 5.101.4 | Server state, `useQueries` for pack contents, mutations with `gcTime: 0` for secrets | already the project standard |
| i18next / react-i18next | pinned | `Server_{Area}_{Item}` flat keys, `_one/_other` plurals | existing locale tests enforce it |
| vitest 4.1.10 + @testing-library/react 16.3.2 + jsdom 28 | pinned | Unit/component tests | `tests/setup.ts` polyfills `HTMLDialogElement.showModal/close` |
| @playwright/test 1.62.1 + @axe-core/playwright 4.13.0 | pinned | e2e + axe | Chromium builds present in `%LOCALAPPDATA%/ms-playwright` |
| ASP.NET Core / EF Core / Npgsql | 10.0.9 / 10.0.9 / 10.0.2 | API + migration | per AGENTS.md stack |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Sequential existing endpoints | New batch `PUT /game-servers/{id}/changes` | Batch would need per-item results anyway (UI-SPEC "unresolved" row) and duplicates four audited code paths; sequential keeps audit/authorization identical and needs no new contract. **Use sequential.** |
| Timestamp-only pending signal computed from `TimeStampUpdated` | Flags + since-timestamp columns | `TimeStampUpdated` is bumped by many unrelated changes, so it cannot distinguish "config changed" from "title changed". **Use explicit flags.** |

**Installation:** none. Backend migration: `dotnet ef migrations add AddPendingStartChangeTracking --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext --output-dir Migrations` (AGENTS.md command, name is the planner's choice).

## Package Legitimacy Audit

No external package is installed or added by this phase. `npm install` is not part of any plan task.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | - | - | - | - | n/a | No new packages |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
 Browser (SettingsSection owns draft)                       API control plane                     DB (PostgreSQL)
 ┌────────────────────────────────────────────┐
 │ Panels (Mods | Configuration | Credentials)│  GET server, mod-pack-options, configuration,
 │   read saved values (react-query)          │  secrets, profile-options   ────────────────►  GameServer, ModPack*, ConfigValue,
 │   merge with draft (useServerChangeDraft)  │                                                GameServerSecret
 │   stage/unstage ─► serverChangeDraft.ts    │
 │        │  (pure: normalize, count, plan)   │
 │        ▼                                   │
 │ PendingChangesBar ─ Review ─► ReviewDialog │  GET pack contents (per pack, cached) ───────► ModPackRevision(+Mods,+Deps)
 │        │ Apply (sequential, per-item)      │
 │        ▼                                   │
 │ 1 PUT mod-pack ────────────────────────────►  GameServerLifecycleService.ChangeModPack ──► set PendingStart[ModPack]
 │ 2 PUT configuration/profile ───────────────►  GameServerConfigurationController ─────────► set PendingStart[Configuration]
 │ 3 PUT configuration (values+resetKeys) ────►  (same controller, one call)  ──────────────► set PendingStart[Configuration]
 │ 4 PUT secrets / POST secrets/{k}/rotate ───►  GameServerSecretService ───────────────────► set PendingStart[Credentials]
 │        ▼ per-item result → keep failed in draft, invalidate queries
 │ PendingStartNotice (shell notices slot) ◄── GameServerDto.changesPendingStartAreas ◄─────  cleared at Start (all) / Restart
 │ useLeaveGuard: useBlocker(fn) + beforeunload                                              command creation (config+credentials)
 └────────────────────────────────────────────┘
 Files section: ?tab=files&path=…&area=live ─► filesLocation.ts (strict parse) ─► useQuery files ─► 404? ─► replace to root
```

### Recommended Project Structure
```
sources/ReactApp/src/
├── components/
│   ├── FormFields.tsx                 # + TextAreaField (new)
│   ├── useLeaveGuard.ts               # new (wraps useBlocker + beforeunload)
│   └── server/
│       ├── ModPackPanel.tsx, ModPackDiff.tsx, ChangeDiffItem.tsx
│       ├── PendingChangesBar.tsx, ReviewChangesDialog.tsx, PendingStartNotice.tsx
│       ├── ConfigurationEntryField.tsx
│       └── change.css                 # tokens only (style guard scans every css here)
└── features/hosting/
    ├── serverChangeDraft.ts           # pure model + unit tests
    ├── useServerChangeDraft.ts        # useReducer + small context, owned by SettingsSection
    ├── filesLocation.ts               # pure ?path=/?area= parse/build + unit tests
    ├── ServerConfigurationPanel.tsx   # rewritten
    ├── ServerSecretsPanel.tsx         # rewritten (Credentials)
    └── sections/SettingsSection.tsx   # hosts draft, bar, dialog, guard
sources/Api/…  GameServerImplementations/…  Database/…  (see "API and Database Changes")
```

### Pattern 1: Pure draft model (CHG-03, D-01, D-02, D-08)
**What:** `serverChangeDraft.ts` exports plain functions over an immutable draft value: `stageValue`, `unstage`, `stageReset`, `stageSecret`, `stageProfile`, `stageModPack`, `normalize(draft, saved)`, `count`, `buildApplyPlan`. `useServerChangeDraft` is a thin `useReducer` over them. A value equal to the saved value is removed by `normalize` (compare with `configurationInputValue(entry)` from `workspaceModel.ts`, which already exists).
**When to use:** all staging. The reducer must not touch storage or `console`; a source-scan test (UI-SPEC) enforces it.
**Key API facts the model must encode:** `values` and `resetKeys` must be disjoint and unique or the API returns 400 (`"Configuration keys must occur in exactly one update operation."`, `GameServerConfigurationController.cs` UpdateAsync). Staging a value for a reset key must remove the reset, and vice versa.

### Pattern 2: Sequential apply with per-item results (D-04)
Order is fixed by dependency: mod pack, profile, values+resets (one `updateConfiguration` call, skipped as "Not tried" when the profile step failed), then each credential in `key` order. `updateConfiguration` is atomic per call (single `SaveChangesAsync`; a mid-loop `ArgumentException` returns 400 before save), so "values" is one item group, not N independent items. Failed and not-tried items stay staged; applied items are removed by id, never by recomputing from stale state. Secret mutations use `gcTime: 0` so TanStack's mutation cache does not retain the variables.

### Pattern 3: Pack options and contents (CHG-01, D-05)
Chooser: native select through `SelectField` with `optgroup`-free flat options (UI-SPEC). Contents come from a per-pack read-only endpoint, loaded with `useQueries` keyed by pack id, only for the saved pack and the staged pack. Diff by `key` in a pure function (`modPackDiff(current, next) -> {added, removed, unchanged}`), unit-tested, with the loading/failed/unknown degradations from the UI-SPEC.

### Pattern 4: Pending-start signal (CHG-04, D-09)
`GameServerDto` gains `ChangesPendingStartAreas` (list of `EGameServerChangeArea`: `ModPack`, `Configuration`, `Credentials`) and a derived `HasChangesPendingStart`. Persisted as two columns on `GameServer` (see A5). The banner reads only this; wording variants use `isDeploymentActive` / `deriveServerState` (the `server-state-single-owner` test forbids reading `deployment.status` outside `serverState.ts`/`services.ts`).

### Pattern 5: Leave guard (CHG-05, D-10)
```tsx
// Source: react-router 7.18.2 types, node_modules/react-router/dist/development/index.d.ts:876
// useBlocker(shouldBlock: boolean | BlockerFunction): Blocker
// BlockerFunction = (args: { currentLocation; nextLocation; historyAction }) => boolean
const blocker = useBlocker(({ currentLocation, nextLocation }) =>
  dirty && leavesGuardedArea(currentLocation, nextLocation));
// leavesGuardedArea: pathname differs OR workspaceSectionFromSearch(new URLSearchParams(next.search).get("tab")) !== "settings"
useEffect(() => {
  if (!dirty) return;
  const onBeforeUnload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
  window.addEventListener("beforeunload", onBeforeUnload);
  return () => window.removeEventListener("beforeunload", onBeforeUnload);
}, [dirty]);
// blocker.state === "blocked" -> open the three-action dialog; Stay = blocker.reset(), Discard = clear draft + blocker.proceed()
```
The existing `useUnsavedChanges` in `components/primitives.tsx` (`useBlocker(isDirty)` + `beforeunload` + `window.confirm`) stays for admin pages; the workspace stops calling it.

### Pattern 6: Files location (CHG-06, D-11)
`filesLocation.ts` is strict and pure: reject empty segments, `.`/`..`, backslashes, control characters, length over 1024 (UI-SPEC). Do **not** feed untrusted input straight to the existing `normalizeServerRelativePath` (`workspaceModel.ts:27-36`): it throws on `..`, silently accepts leading/double slashes and trims. Navigation uses `navigate({ search }, { replace: false })`; fallback uses `replace: true`.

### Anti-Patterns to Avoid
- **Computing pending-start in the browser** (compare timestamps, guess from draft): violates D-09 and breaks for other members' sessions.
- **A batch apply endpoint**: re-implements four audited paths and forces an all-or-nothing vs partial decision the UI-SPEC marks unresolved.
- **Printing `Error.message`** (current panels do): API 400 bodies can name keys; the UI-SPEC bans it.
- **Persisting anything secret-adjacent**: no `localStorage`/`sessionStorage`/URL/router state/query cache/`console` in draft, panel, dialog files.
- **Re-using `window.confirm`** in the workspace leave guard.
- **Hard-coding `arma3`/`aska`/`counterstrike2`** in mod or generic configuration components (UI-SPEC source-scan).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Flattening nested mod packs into a mod list | A second resolver | `IGameServerContentSnapshotResolver.ResolveAsync(string gameIdentifier, long? modPackDefinitionId, ct)` | Already handles nesting, cycles, pinned revisions, per-game scope, dedupe by mod; same code path the Start snapshot uses, so the diff matches what will actually run. |
| Config value coercion/validation | New client validators | `parseConfigurationEntryValue`, `validateConfigurationEntry`, `configurationInputValue` in `workspaceModel.ts` | Existing, tested, matches API `GameConfigurationValueValidator`. |
| Disabled-with-reason | Custom tooltips | `ActionButton unavailableReason` / `UnavailableReason` | Phase 61/64 pattern with `aria-describedby`. |
| Dialogs | Custom overlay | `ConfirmDialog` (native `<dialog>`, busy, error, initial focus) extended additively with `extraAction` and `bodyLabel` | Existing focus/escape/busy behavior and tests. |
| Server state wording | Status string compares | `deriveServerState`, `isDeploymentActive` | Single-owner test. |
| Test router | Custom history mock | `renderWithProviders` (`createMemoryRouter` data router) | Required for `useBlocker` to function in tests. |
| Fleet/workspace live refresh | New socket code | `FleetChangeInterceptor` + `useWorkspaceLiveUpdates` | Add the new GameServer column to `IsRelevant`; the client path already invalidates `server(id)` on signal. |

**Key insight:** every backend capability this phase needs (nested pack resolution, audited mutations, authorization, live signals) already exists; the work is exposing it in a staged-draft shape and adding one durable fact (pending start).

## API and Database Changes (D-00a: in scope, no approval needed)

Order matters: backend and snapshot first, then UI.

**A1. Mod pack option availability and reason.** `GameServerModPackOptionDto` today is exactly `PrimaryKey` and `Title` [VERIFIED: sources/Api/DTO/GameServerModPackOptionDto.cs:6-11 — `public long PrimaryKey { get; init; }`, `public string Title { get; init; } = string.Empty;`]. Add `IsAvailable` and `UnavailableReason`. The server-scoped query (`GameServerProjectionService.GetModPackOptionsAsync`, lines 217-248) filters `q.IsActive && q.GameIdentifier == gameIdentifier`, so inactive packs and a **currently selected pack that has since been deactivated are not listed at all**. Recommended: include the server's selected pack even when inactive, marked unavailable ("no longer active"), plus a cheap structural availability test (active latest revision exists) in the list query; reserve full resolution for the contents endpoint and for `ChangeModPackAsync`. Do not run the full resolver per pack in the list (it issues several queries per mod).

**A2. Pack contents projection.** New read-only endpoint, e.g. `GET /game-servers/{gameServerId:guid}/mod-pack-options/{modPackDefinitionId:long}/contents`, `Server.View`, returning `{ total, items: [{ key, title }] }` built from the resolver above (`WorkerContentReference` carries `Source`, `Identifier`, `DisplayName`; key = stable source+identifier or add the mod id). Catch `PermanentLifecycleExecutionException` and return a safe reason instead of 500. The existing `GET game-content/mod-packs/revisions/{id}/members?effective=true` is untyped (anonymous object), revision-scoped, and not game/server-scoped, so it is not the right contract. Contents must be capped or paged on the server if real packs reach thousands of mods (UI-SPEC "unresolved" row; treat as an ASSUMED risk, A2 in the Assumptions Log).

**A3. `ChangeModPackAsync` fixes.** [VERIFIED: sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs:884-929] It sets only `SelectedModPackDefinitionFk`; `PinnedModPackRevisionFk` (set at creation, line 231) is never cleared. The resolver then looks up `ModPackRevisions.SingleOrDefault(q => q.PrimaryKey == pinned && q.DefinitionFk == definitionId)` (`GameServerContentSnapshotResolver.cs` lines ~88-92), which returns null for a different pack and throws `"A selected mod pack has no resolvable revision."` at Start. Clear the pin whenever the definition changes. Also validate resolvability (reuse the resolver) so an unavailable pack is rejected with `HostingValidationException` carrying the same reason the options DTO shows. The audit string already says `AppliesTo=next-preparation-or-update`.

**A4. Common/Advanced tier + OpenAPI exposure.** `GameConfigurationDefinition` is a positional record whose only post-construction property is `IsControlPlaneOnly { get; init; }` [VERIFIED: sources/GameServerImplementations/GameConfigurationDefinition.cs:19-40]. Add `EGameConfigurationTier Tier { get; init; }` with `Advanced = 0` first so "no value = Advanced" holds by default, plus `Common = 1`; thread an optional `tier` parameter through the private `Definition(...)` helpers (Arma3 helper at `Arma3ConfigurationImplementation.cs:270`, similar in Aska/CS2). Project it in `GameServerConfigurationController.Project` and `GameConfigurationEntryDto`. The same record is serialized in `ConfigurationProfileDefinitionCatalogDto.Definitions`, so profile screens receive it additively. `JsonStringEnumConverter` is registered (`Program.cs:270`), so the TS side sees `"Common" | "Advanced"`.
**Critical:** `grep -c "GameConfigurationDto\|GameConfigurationEntryDto" sources/ReactApp/openapi/utf-api.json` returns **0** and `types.ts` hand-maintains `GameConfigurationEntry` with the comment "The configuration controller predates the generated OpenAPI response annotations". Add `[ProducesResponseType<GameConfigurationDto>(200)]` (+404/400 as applicable) to `GetAsync`/`UpdateAsync` and `ProducesResponseType<IReadOnlyCollection<ConfigurationProfileSummaryDto>>` to `GetProfileOptionsAsync`, regenerate, then replace the hand-written interfaces with generated aliases (or keep them and add `tier`, but generated is the AGENTS.md direction).
Arma 3 Common set proposal (planner/user to confirm, game-owned): `server.cfg@hostname`, `server.cfg@maxPlayers`, `headlessClientPlacement`; keep the Common set small and decided in the game definition, not in the UI.

**A5. Pending-start signal (durable).** Add to `GameServer` (`sources/Database/Hosting/GameServer.cs`): `PendingStartChangeAreas` (flags enum `EGameServerChangeArea`, int, default 0) and `PendingStartSince` (`Instant?`). Expose on `GameServerDto` as `ChangesPendingStartAreas` + `HasChangesPendingStart` (the projection is built in `GameServerProjectionService.ProjectServerAsync`, DTO at `GameServerDto.cs:6-47`). Migration via `dotnet ef migrations add` only.
- **Set** (same `SaveChangesAsync` as the change, so it is atomic with the audit record): `ChangeModPackAsync` (ModPack), config `UpdateAsync` and `SelectProfileAsync` (Configuration), `GameServerSecretService.SetAsync`/`RotateAsync` (Credentials). Only set when something actually changed (`ChangeModPackAsync` already returns early on equal selection).
- **Clear:** in `GameServerLifecycleService` Start path, where the `GameServerDeployment` is created with `ContentSnapshot`/`PlacementSnapshot` [VERIFIED: GameServerLifecycleService.cs:444-480, `ContentSnapshot = JsonSerializer.Serialize(storageDemand.EffectiveContent, SnapshotJsonOptions)`] clear all areas in the same transaction. For `ELifecycleJobKind.Restart` clear only `Configuration | Credentials`, because `EnsureCommandPersistedAsync` re-resolves configuration and `CreateProtectedSnapshotAsync` for every command while content comes from the persisted `deployment.ContentSnapshot` [VERIFIED: LifecycleJobExecutor.cs:1920-1926 and the `ProtectedSecrets = await secretService.CreateProtectedSnapshotAsync` line at ~2012]. A change landing between enqueue and command creation is consumed but leaves the flag set: a conservative false positive, no false negative. Document it; do not try to fix it with locks (change endpoints do not use `BeginServerMutationAsync`).
- **Live propagation:** add `PendingStartChangeAreas` to `FleetChangeInterceptor.IsRelevant(EntityEntry<GameServer>)` [VERIFIED: FleetChangeInterceptor.cs:279-286 currently lists `Title`, `IsArchived`, `IsLaunchDisabled`, `CurrentCostOwnerFk`] so other members' open workspaces refetch. Add a case to `FleetChangeInterceptorTests`.
- **Known limitation to note in the plan:** a new revision of a profile that a server *follows* changes the effective configuration of that server without any server-side write; this phase does not flag those servers (FUT-02 territory).

**A6. Profile revision id for pinning.** `GetProfileOptionsAsync` returns `LatestRevisionNumber` but no revision id [VERIFIED: GameServerConfigurationController.cs `ConfigurationProfileSummaryDto { ... LatestRevisionNumber = q.Revisions.Max(r => (int?) r.RevisionNumber) ?? 0 ...}`], and `SelectProfileAsync` pins by `PinnedRevisionId` (Guid). The UI-SPEC's "Stay on revision {n}" for a *newly chosen* profile is therefore not implementable. Add `LatestRevisionId` (`Guid?`) to `ConfigurationProfileSummaryDto` (additive, shared with `ConfigurationProfilesController`), or restrict the revision radio to the saved profile as today. Recommend the DTO field.

**A7. Secrets service hardening.** `GameServerSecretService.LoadAsync` loads by primary key without `IsArchived` [VERIFIED: GameServerSecretService.cs:221-231] and the controller gates only on `ManageSecrets`, whereas `ChangeModPackAsync`, config `UpdateAsync` and `SelectProfileAsync` reject archived servers. The UI-SPEC makes an archived server read-only, so reject `SetAsync`/`RotateAsync` for archived servers (HostingConflict/Validation) to keep API and UI consistent. Reveal on an archived server is a policy call; keep it allowed (read-only, audited).

**Regeneration sequence** (all verified in repo): `dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi sources/ReactApp/openapi/utf-api.json` (CI step in `.github/workflows/dotnet.yml:73-76` does `cmp --silent` against it), then `npm --prefix sources/ReactApp run generate` (`openapi-typescript`), then `python sources/WebApp/generate-client.py` for the Kiota client (reads the same snapshot, `sources/WebApp/generate-client.py`; Kiota 1.32.2 is installed globally). `OpenApiRouteCompatibilityTests` guards ambiguous route templates, so keep the new pack-contents route unambiguous.

## Runtime State Inventory

Not a rename/refactor/migration phase. One additive DB migration; no stored string is renamed. Existing `GameServer` rows get `PendingStartChangeAreas = 0` and `PendingStartSince = null` (no backfill: servers with changes applied before this release are simply not flagged).

## Common Pitfalls

### Pitfall 1: The tier field never reaches the client
**What goes wrong:** Adding `Tier` to the DTO and `api.ts` regeneration produces nothing, because `GameConfigurationDto` is not in the OpenAPI document.
**Why:** `GameServerConfigurationController` actions have no `ProducesResponseType`; `types.ts` papers over it with a hand-written interface.
**Avoid:** annotate first, regenerate, assert in a test that `components["schemas"]["GameConfigurationEntryDto"]` has `tier`. **Warning sign:** `contracts:check` passes while `api.ts` lacks the type.

### Pitfall 2: Building a sensitive-configuration staging path that can never succeed
**What goes wrong:** The UI-SPEC stages sensitive entries as "changed" via `updateConfiguration`.
**Why:** `ResolveAsync` skips them [VERIFIED: GameServerConfigurationService.cs:58 `if (definition.Sensitive)` followed by `continue;`] and `ValidateAndClone` throws for them [VERIFIED: lines 124-125 `if (definition.Sensitive)` / `throw new ArgumentException("Secrets must be managed through the server secret API.", nameof(key));`]. Entries therefore never arrive, and a staged one would 400.
**Avoid:** keep `ConfigurationEntryField` defensive: if `sensitive || isRedacted` ever appears, render read-only with a pointer to Credentials and never stage it. Remove it from the apply plan. Secrets are staged only through the Credentials panel.

### Pitfall 3: Stale pinned mod pack revision after a pack change
See A3. A test must cover: server created with `ModPackRevisionId`, then `ChangeModPackAsync` to another pack, then the content resolver resolves. **Warning sign:** Start fails with "A selected mod pack has no resolvable revision."

### Pitfall 4: Current pack missing from the chooser
The options query lists only `IsActive` packs. If the saved pack was deactivated, the select has no matching option and shows the wrong value. Always include the saved pack (marked unavailable) and select it by id.

### Pitfall 5: "Next start" is not one moment for all areas
Mod pack is frozen at Start (`ContentSnapshot`); configuration and secrets are re-read at every command creation including Restart. A single boolean cleared at Start only would keep the banner up after a restart that already consumed config, and a boolean cleared at any command would lie about the mod pack. Hence per-area flags with area-specific clearing (A5). The review/toast copy "the next time the server starts" stays correct for all areas as a conservative statement.

### Pitfall 6: Secret values leaking through client plumbing
Values can leak via: TanStack mutation cache (use `gcTime: 0`), the draft map surviving unmount, error messages, `aria-live` announcements, the revealed value in a `useQuery` cache (reveal must be a `useMutation`), `visibilitychange`/timer cleanup missing on unmount. The server already audits reveal/set/rotate and logs only `Key=` and `Version=` [VERIFIED: GameServerSecretService.cs:90,106,124]. The UI must add nothing that prints.

### Pitfall 7: `useBlocker` behaviors
Needs a data router (present: `createBrowserRouter`). The function form receives `{currentLocation, nextLocation, historyAction}`. A `?action=` deep-link cleanup in `ServerWorkspacePage` (`setSearch(..., {replace:true})`) and the create-intent `navigate(..., {replace:true})` stay on the same tab and must not trigger the dialog: the predicate must compare the **resolved section**, not the raw search string. A blocked Back leaves history unchanged after `reset()`. Only one guard may be active per mounted section: the Settings and Files guards never coexist because sections unmount (the draft is lost on unmount, which is exactly why the guard exists). Test with `renderWithProviders` (memory data router) and `router.navigate(-1)`.

### Pitfall 8: Files fallback loop and live context
`BrowseAsync` maps `DirectoryNotFoundException` to HTTP 404 [VERIFIED: GameServerFilesController.cs `FileNotFoundException or DirectoryNotFoundException => new NotFoundObjectResult(...)`], and the **live** context throws `DirectoryNotFoundException("The server has no active live runtime.")` when no deployment is active [VERIFIED: GameServerFilesController.cs `ResolveDeploymentAsync`]. A naive "404 means fall back to root" loops at root and for `?area=live` without a runtime. Fall back only when the requested path is non-root; a 404 at root keeps the existing error panel and never rewrites the URL. `ApiError.code === "not-found"` is the client signal (`api/client.ts:43`). Persistent-context generated directories return `[]` instead of 404 (`HasGeneratedDescendant`), so a "missing" generated folder is not detected as missing; accept that.

### Pitfall 9: `routes.json` does not list query parameters
[VERIFIED: sources/Common/Ui/routes.json:349-355] the workspace is `"canonical": "/command/servers/{ServerId:guid}"` with no query data, so `?tab=`/`?path=`/`?area=` need **no manifest change**; the verify step `git diff --exit-code -- sources/Common/Ui/routes.json` should stay clean. Do not edit it unless a pathname actually changes.

### Pitfall 10: Draft re-normalization on refetch
The server query refetches during polling and on live signals. Normalization must run against the latest saved values on every render (derive, do not copy), and "Before" in the review reads the latest saved values (UI-SPEC). Never mirror props into draft state.

### Pitfall 11: Parity matrix and locale gates
`tests/hosting/parity-matrix.test.ts` requires an `SVC-<method>` row for **every** key of `hostingService` (line ~151); a new `modPackContents` method fails the suite until the row is added to `61-PARITY-MATRIX.md`. Rows `CHG-*`/`FIL-*` (lines 148-172, currently `pending`) must be updated to `kept`/`relocated` with evidence; do not delete rows. New locale keys need both `en.json` and `de.json`, `_one/_other` plural pairs, banned-label compliance, and `identical-allowed.json` entries with reasons for identical words (`Mods`, `Revision`).

## Code Examples

### Existing endpoints the apply plan calls (all verified in controllers)
```text
PUT  /game-servers/{id}/mod-pack                 body { modPackDefinitionId: number|null }  -> 204
PUT  /game-servers/{id}/configuration/profile    body { profileId, pinnedRevisionId }       -> 204
PUT  /game-servers/{id}/configuration            body { values:[{key,value}], resetKeys }   -> 200 GameConfigurationDto
PUT  /game-servers/{id}/secrets                  body { key, value, origin }                -> 204
POST /game-servers/{id}/secrets/{key}/rotate                                                -> 204
POST /game-servers/{id}/secrets/{key}/reveal                                                -> 200 { key, value, version }
```
`hostingService` already wraps each (`changeModPack`, `updateConfiguration`, `selectConfigurationProfile`, `setSecret`, `rotateSecret`, `revealSecret`); no new client method is needed except pack contents.

### Per-item apply result shape (recommended)
```ts
type ChangeItemId = "modPack" | "profile" | "settings" | `secret:${string}`;
type ItemResult =
  | { id: ChangeItemId; state: "applied" }
  | { id: ChangeItemId; state: "failed"; reason: "forbidden" | "conflict" | "other" }
  | { id: ChangeItemId; state: "notTried"; reason: "dependency" };
// Map ApiError.code: "forbidden" -> forbidden, "conflict" -> conflict, everything else -> other.
// Never surface ApiError.message (UI-SPEC copy rule).
```

### Backend pending-start set/clear (shape only)
```csharp
// Set inside the same unit of work as the change, e.g. in ChangeModPackAsync before SaveChangesAsync:
server.PendingStartChangeAreas |= EGameServerChangeArea.ModPack;
server.PendingStartSince ??= now;
// Clear in the Start branch of the lifecycle service where the deployment (ContentSnapshot) is created:
server.PendingStartChangeAreas = EGameServerChangeArea.None;
server.PendingStartSince = null;
// Restart: server.PendingStartChangeAreas &= ~(EGameServerChangeArea.Configuration | EGameServerChangeArea.Credentials);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Four immediate-apply panels + `window.confirm` guard | One in-memory draft, review dialog, `useBlocker` function form | this phase | Retires `next-hosting-configuration-savebar`, raw controls, and per-panel guards. |
| Hand-written `GameConfigurationEntry` TS type | Generated from OpenAPI | this phase (A4) | Contract drift becomes a CI failure (`contracts:check`). |
| Mod pack under Machine (`ServerRuntimePanel` lines ~374-420) | Settings > Mods, staged | this phase | Remove the mod pack state/mutation from `ServerRuntimePanel`; runtime profile, storage, terrain/scenario stay immediate. |

**Deprecated/outdated:** `ServerWorkspacePage` comment-level assumption that only job churn changes `server`; the banner adds a second reason to refetch (covered by the interceptor change).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The Arma 3 Common set should be small (hostname, max players, Headless Client placement, a few lifecycle/security toggles); exact membership is a product choice | API changes A4 | Wrong Common set means members must open Advanced for everyday settings; user/planner should confirm the list. |
| A2 | Real mod packs are small enough (tens to low hundreds of mods) that returning full contents is fine | A2, UI-SPEC "unresolved" | Thousands of entries would need server paging/windowing. |
| A3 | Not flagging servers that follow a profile whose latest revision changed is acceptable for this phase | A5 limitation | Banner is silent for an effective-config change not made on the server (FUT-02). |
| A4 | A conservative false positive (flag set for a change consumed between Restart enqueue and command creation) is acceptable | A5 | Banner could show once more than strictly needed after a restart; no false negatives. |
| A5 | Allowing Reveal on archived servers is acceptable policy | A7 | If product wants archived servers fully sealed, `RevealAsync` also needs the archived check. |

All other claims in this document were verified by reading the cited source files this session or by reading `node_modules` type definitions.

## Open Questions

1. **Exact Common membership per game** (A1 above)
   - Known: tier is declared on the game definition; default is Advanced.
   - Unclear: which Arma 3 / Aska / CS2 settings count as Common.
   - Recommendation: planner proposes a short list in the task; keep it data in `GameServerImplementations`, covered by one test per game asserting at least one Common entry or an intentionally flat list.
2. **Pack contents key**
   - `WorkerContentReference` has `Source`/`Identifier`/`DisplayName` but no mod id; a stable diff key is needed.
   - Recommendation: key = `{source}:{identifier}`; add the mod id only if two mods can share that pair (the resolver dedupes by `Mod.PrimaryKey`).
3. **Reveal on archived servers** (A5): policy confirmation.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | API, migration, tests | yes | 10.0.201 (`dotnet --version`) | - |
| dotnet-ef | migration scaffold | yes | 10.0.5 (global tool; EF packages are 10.0.9, a version warning is expected, AGENTS.md requires a compatible version) | - |
| Kiota | Kiota client regeneration | yes | 1.32.2 (global tool) | - |
| Node | ReactApp | yes | v25.5.0 | - |
| Playwright Chromium | e2e + axe | yes | several chromium builds in `%LOCALAPPDATA%/ms-playwright` | skip e2e locally, CI runs them |
| PostgreSQL | scaffolding does not need it; API tests use in-memory SQLite (`HostingTestStore`) | not probed | - | not required for this phase's tests |

**Missing dependencies with no fallback:** none found.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Frontend | vitest 4.1.10 + @testing-library/react 16.3.2, jsdom 28; config `sources/ReactApp/vitest.config.ts`, setup `tests/setup.ts` |
| e2e | Playwright 1.62.1 + `@axe-core/playwright` 4.13.0; `playwright.config.ts` builds assets and previews on 4173 with a classic stub |
| Backend | xUnit via `tests/Api.Tests` (SQLite in-memory `HostingTestStore`, `NodaTimeSqliteApiDbContext`) |
| Quick run | `npm --prefix sources/ReactApp test -- tests/hosting/<file>` ; `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServer"` |
| Full suite | `npm --prefix sources/ReactApp run ci` (+ `test:e2e` for the hosting specs) ; `dotnet test X39.UnitedTacticalForces.slnx` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CHG-01 | Pack options carry availability/reason; saved inactive pack still listed; contents projection; pin cleared on change; unavailable pack rejected | API unit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ModPack"` | Wave 0 (no `ChangeModPack` test exists) |
| CHG-01 | Chooser, current marker, unavailable disclosure, contents diff | component | `npm --prefix sources/ReactApp test -- tests/hosting/mod-pack-panel.test.tsx` | Wave 0 |
| CHG-02 | Tier projected; Common/Advanced split, flat fallback, Advanced auto-open | API + component | `... --filter "FullyQualifiedName~GameServerConfiguration"`; `tests/hosting/configuration-panel.test.tsx` | Wave 0 |
| CHG-03 | Draft normalize/count/plan order/dependency skip; apply partial failure | unit + component | `tests/hosting/server-change-draft.test.ts`, `review-changes-dialog.test.tsx` | Wave 0 |
| CHG-04 | Flag set on each change kind, cleared at Start, partial clear at Restart; DTO exposes it; banner variants | API + component | `... --filter "FullyQualifiedName~PendingStart"`; `tests/hosting/pending-start-notice.test.tsx` | Wave 0 |
| CHG-04 | Interceptor signals on the new column | API | `--filter "FullyQualifiedName~FleetChangeInterceptor"` | extend existing |
| CHG-05 | Predicate, blocked Back/tab, `beforeunload` registered only while dirty, three dialog outcomes | component | `tests/leave-guard.test.tsx` (memory data router) | Wave 0 |
| CHG-06 | `filesLocation` parse/build; push vs replace; root-404 does not loop; live without runtime | unit + component | `tests/hosting/files-location.test.ts`, `files-panel-location.test.tsx` | Wave 0 |
| CHG-07 | Reveal gating, auto-hide 30 s (fake timers), no value in DOM/storage/URL/console, `gcTime: 0` | component + e2e | `tests/hosting/credentials-panel.test.tsx`; Playwright (d) | Wave 0 |
| CHG-07 | Archived server rejects secret set/rotate | API | `--filter "FullyQualifiedName~GameServerSecretService"` | extend existing |
| all | Source-scan: no storage/`console.`/game literals in draft, panel, dialog files; style guard; locale parity; parity matrix | scan | `tests/styles`, `tests/locales`, `tests/hosting/parity-matrix.test.ts` | extend |
| all | OpenAPI snapshot matches code; `api.ts` matches snapshot | CI | `dotnet run ... --export-openapi` + `cmp`; `npm run contracts:check` | exists (CI) |

### Sampling Rate
- **Per task commit:** touched test files + `npm --prefix sources/ReactApp run typecheck` and `lint`; touched .NET test class.
- **Per wave merge:** `npm --prefix sources/ReactApp test`, `locales:check`, `tests/styles`, `tests/hosting/server-state-single-owner.test.ts`; `dotnet test tests/Api.Tests`.
- **Phase gate:** `npm --prefix sources/ReactApp run ci`, Playwright (hosting-a11y plus the new change spec at 320 and 390, en and de), `git diff --exit-code -- sources/Common/Ui/routes.json` clean, `contracts:check` clean, OpenAPI `cmp` clean.

### Wave 0 Gaps
- [ ] API tests: `ChangeModPackAsync` (none exist), pending-start set/clear, pack options/contents, tier projection, secrets archived rejection, interceptor case.
- [ ] Frontend test files listed above (all new); extend `workspace-section-panels.test.tsx` (it covers the panels being rewritten) and `workspace-sections.test.ts` (`sectionSearch` stays tab-only).
- [ ] Update `61-PARITY-MATRIX.md` rows and add `SVC-modPackContents`.
- [ ] Playwright spec for the change flow (existing specs: `hosting-create`, `hosting-a11y`, `hosting-fleet`, `hosting-placement`).

## Security Domain

`security_enforcement` is enabled in `.planning/config.json` (ASVS level 1, block on high).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (existing auth) | - |
| V3 Session Management | no | - |
| V4 Access Control | yes | Existing `Claims.ResourceBased.Server.View/Modify/ManageSecrets/RevealSecrets` via `IHostingAuthorizationService`; new pack-contents endpoint `Server.View`; UI mirrors `canModify`/`canManageSecrets`/`canRevealSecrets` but never replaces server checks. |
| V5 Input Validation | yes | Server: `ValidateAndClone`, `GameConfigurationValueValidator`, pack game-scope checks, `SafeServerPath` for files. Client: strict `filesLocation` parser for untrusted `?path=`/`?area=`/`?tab=`. |
| V6 Cryptography | yes | Existing data-protection of secret values (`_protector`); no new crypto; never hand-roll. |
| V7 Error Handling and Logging | yes | Secret/audit logs carry key and version only; UI prints no `Error.message`, no values. |
| V8 Data Protection | yes | No secret in draft persistence, URL, router state, query cache, storage, announcements. |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secret exposure via client caches/logs | Information disclosure | `useMutation` with `gcTime: 0` for set/reveal, component-state-only reveal with 30 s + visibility + unmount hide, source-scan test banning `console.`/storage in these files. |
| Path traversal via `?path=` | Tampering | Strict parser (reject `..`, `.`, backslash, control chars, length cap) and the API's `SafeServerPath.Normalize`; never build a file URL from raw `path`. |
| Prototype-key / unknown `?tab=` | Tampering | Existing exact `includes` match in `workspaceSectionFromSearch`; keep for the new params (never index objects with them). |
| Privilege confusion (Modify vs ManageSecrets vs Reveal) | Elevation of privilege | Keep three distinct gates; Credentials panel renders only with `canManageSecrets`; Reveal needs `canRevealSecrets && revealable && isConfigured`. |
| Editing an archived server | Tampering | Server rejects (config, mod pack already do; add secrets, A7). |
| Cross-game mod pack assignment | Tampering | Existing game-identifier check in `ChangeModPackAsync`; keep, and enforce on the contents endpoint. |
| Lost update when two members edit | Tampering | Last write wins today; UI re-normalizes against fresh saved data and shows fresh "Before"; no optimistic concurrency token exists (conflict copy exists for 409 only). Accepted for this phase. |

## Sources

### Primary (HIGH confidence, read this session)
- `A:/dev/git/unitedTacticalForces/AGENTS.md`, `.planning/phases/65-change-a-server/65-CONTEXT.md`, `65-UI-SPEC.md`, `.planning/REQUIREMENTS.md`, `.planning/config.json`
- Backend: `GameServersController.cs`, `GameServerConfigurationController.cs`, `GameServerSecretsController.cs`, `GameServerFilesController.cs` (browse/resolve/error mapping), `GameServerLifecycleService.cs` (Start path 340-520, `ChangeModPackAsync` 884-929), `GameServerProjectionService.cs`, `GameServerContentSnapshotResolver.cs`, `LifecycleJobExecutor.cs` (`EnsureCommandPersistedAsync`, `RevalidateStartAsync`), `GameServerSecretService.cs`, `GameServerConfigurationService.cs`, `FleetChangeInterceptor.cs`, `GameServer.cs`, `GameServerDto.cs`, `GameConfigurationDto.cs`, `GameConfigurationDefinition.cs`, `EGameConfigurationValueKind.cs`, `ELifecycleJobKind.cs`, `GameServerActions.cs`, `NormalizedModPacksController.cs`, `.github/workflows/dotnet.yml`, `sources/WebApp/generate-client.py`
- Frontend: `ServerConfigurationPanel.tsx`, `ServerSecretsPanel.tsx`, `ServerFilesPanel.tsx` (state, navigateTo), `ServerWorkspacePage.tsx`, `SettingsSection.tsx`, `WorkspaceShell.tsx`, `workspaceSections.ts`, `workspaceModel.ts`, `services.ts`, `types.ts`, `serverState.ts`, `components/primitives.tsx` (`ConfirmDialog`, `useUnsavedChanges`), `FormFields.tsx`, `Disclosure.tsx`, `useWorkspaceLiveUpdates.ts`, `app/App.tsx`, `api/client.ts`, `tests/setup.ts`, `tests/helpers/renderWithProviders.tsx`, `tests/hosting/parity-matrix.test.ts`, `tests/hosting/server-state-single-owner.test.ts`, `tests/styles/server-style-guard.test.ts`, `sources/Common/Ui/routes.json`, `sources/ReactApp/openapi/utf-api.json` (grep counts), `61-PARITY-MATRIX.md` rows
- `node_modules/react-router/dist/development/index.d.ts` and `instrumentation-*.d.ts` (`useBlocker`, `BlockerFunction`), react-router 7.18.2

### Secondary (MEDIUM confidence)
- none

### Tertiary (LOW confidence)
- none (no web search was needed; the external API surface used is local type definitions). The research-plan/Context7 seam was not invoked because no unfamiliar library is involved.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no change from pinned dependencies.
- Architecture: HIGH - every integration point was read in live code; the pending-start clearing points are derived from the actual Start and command-creation code.
- Pitfalls: HIGH - each is tied to a specific verified line or behavior; Common-set membership and pack-size assumptions are tagged ASSUMED.

**Research date:** 2026-11-20
**Valid until:** 2026-12-20 (stable stack; invalidate if Phase 66+ lands changes to `GameServer`, the executor, or `WorkspaceShell` first)
