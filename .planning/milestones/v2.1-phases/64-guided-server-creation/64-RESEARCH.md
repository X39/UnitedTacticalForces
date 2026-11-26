# Phase 64: Guided Server Creation - Research

**Researched:** 2026-11-20
**Domain:** React 19 + react-hook-form/zod capability-driven form, TanStack Query, shared cost confirmation, existing hosting API (no API change)
**Confidence:** HIGH (everything below was read in this repo this session; the two risky behaviors were reproduced in a scratch test and jsdom run)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** A create preset is built from what already exists: **configuration profiles** (admin-managed, per game, with a default) and **mod packs**. They are not named "presets" in the product today; this phase makes them **proper presets** in the creation flow: a first-class, friendly-named preset picker, with no new API, table, or admin UI unless research proves a minimal projection is unavoidable. The user said "we can make them proper presets"; the exact naming and metadata shown per preset is Claude's discretion.
- **D-02:** **Presets default to keep off:** choosing or defaulting a preset pre-selects "Create and keep off". Starting is always an explicit choice (CRT-04, CRT-05).
- **D-03:** Up-front decisions (at most three, CRT-01): recommended **Name, game, and size** (title, game, compute profile with price). Preset, provider, storage, mod pack, terrain/scenario and cost owner default sensibly and live under an **Advanced** disclosure. Cost owner shows as a read-only "Billed to ..." line up front so billing is never hidden; changing it is in Advanced. The preset picker is an entry shortcut, not a fourth decision.
- **D-04:** Layout: **one page, capability-driven**, with a sticky cost summary and create bar. Fields for a game's declared capabilities (terrain/scenario, mod packs, configuration profiles, storage strategies) render generically from `hostingService.capabilities` and related option queries, only when the capability exists, so no game-specific code is in the flow (CRT-06). Must work at 320px and 390px.
- **D-05:** Live cost estimate (CRT-02) reuses `combinedHourlyCost` and `estimateStorage` and updates as choices change: hourly and monthly figures, never an invented number; show "not available" when a price is unknown.
- **D-06:** Early block (CRT-03): the create action is disabled with a visible reason (`UnavailableReason`) when the selected cost owner is frozen or otherwise cannot pay, before submit. Current data exposes only `isFrozen` on `GameServerCostOwnerOptionDto`; the researcher confirms whether balance or affordability data exists and, if a cannot-pay signal beyond frozen is required, adds the minimal projection in the owning layer (per AGENTS.md, regenerate the OpenAPI snapshot and clients).
- **D-07:** "Create and start" (CRT-04) creates the server, then starts through the shared `useCostConfirmation().request({ serverId, actionId: "start", source: "create" })`. If the member dismisses the confirmation or start fails, the server remains created and off, and the member lands in the workspace with the reason shown. "Create and keep off" never starts anything.
- **D-08:** Existing form capabilities must not be lost: every `CRT-*` row in `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` is marked `kept` or `relocated` (never deleted), and the parity completeness tests keep passing.

### Claude's Discretion
- All mechanics of D-03..D-08, copy, component split under `src/components/server/` and `src/features/hosting/`, preset naming and display, and German copy in `src/locales/en.json` and `de.json`.

### Deferred Ideas (OUT OF SCOPE)
- Admin-defined or per-user saved presets with new persistence, only if later needed.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CRT-01 | Guided flow, at most three decisions, advanced behind disclosure | Page anatomy in 64-UI-SPEC; `resolveCreateDefaults` (Pattern 2); native `<details>` Disclosure verified to toggle and fire `toggle` in jsdom (Pitfall 3); required-field/closed-details hazards (Pitfalls 2, 3) |
| CRT-02 | Live cost estimate while creating | `combinedHourlyCost` + `estimateStorage` already exist; money rule and "Calculating" gating (Pattern 3, Pitfall 7); monthly source verified in `RuntimeProfileDto` |
| CRT-03 | Block early, with reason, when cost owner cannot pay | "Cannot pay" = frozen, verified server enforcement points; affordability is enforced later at start, not create (Findings F1); `deriveCreateReadiness` priority list (Pattern 4) |
| CRT-04 | Keep off or start now | Create returns the projected server incl. `actions`; navigate-then-request ordering hazard reproduced (Pitfall 1); notice/consume pattern (Pattern 5) |
| CRT-05 | Presets, default keep off | Client-side projection of `profileOptions` + `creationModPacks`; options endpoint already filters archived (Findings F2); select-value model incl. "Customized" (Pattern 6) |
| CRT-06 | Steps generated from capabilities, not game code | Verified capability flags per game; `requiresModPack` conflict (Findings F3, Open Question 1); source-scan guard test |
</phase_requirements>

## Summary

Phase 64 is a **frontend-only** rewrite of one route (`/command/servers/create`) inside `sources/ReactApp`. Every data source the approved 64-UI-SPEC needs already exists in `hostingService` and the generated OpenAPI types; no API, DTO, database, migration, OpenAPI snapshot or Kiota change is required, and `sources/WebApp` and `routes.json` stay untouched. [VERIFIED: sources/ReactApp/src/features/hosting/services.ts:473-560 and :567-596, all eight methods named in D-04 exist.] The UI-SPEC's research table is correct on the D-06 question: the API enforces only `IsFrozen` when a server is created, and balance affordability is enforced later, when a start job reserves a cost slice (Findings F1). So "cannot pay" means "frozen" in this phase and no projection is added.

The work is mostly new presentation components (`FormFields`, `Disclosure`, six `components/server/*` pieces, `create.css`), a pure model extension (`resolveCreateDefaults`, `deriveCreateReadiness`, preset projection, money rule), a rewrite of `CreateServerPage` as composition plus form state, and one integration seam in the workspace page that consumes router state to run the start confirmation. The three real risks are not visual: (1) calling `request()` in the same commit as the navigation silently drops the dialog (reproduced), (2) native constraint validation and RHF focus handling break on fields inside a collapsed `<details>`, and (3) the UI-SPEC makes a mod pack "required when `capability.requiresModPack`", which is `true` for Arma 3 although neither the old form nor the API requires it (Open Question 1).

**Primary recommendation:** Build the pure model first (defaults, readiness, presets, money), then the generic `FormFields`/`Disclosure`, then the page composition with `noValidate` and manual DOM-order focus-on-invalid, and implement "Create and start" as router state consumed once by `ServerWorkspacePage` with a deferred `request()` call, covered by an integration test that uses the real `CostConfirmationProvider` across two routes.

## Project Constraints (from AGENTS.md; no CLAUDE.md exists)

- Identifiers, comments and technical docs in English; user text only in `src/locales/en.json` and `de.json` (flat keys, EN/DE parity gate `locales:check`). C# rules (E-prefixed enums, XML docs, one type per file) do not apply because no C# changes are planned. If one becomes necessary, the OpenAPI snapshot and generated clients must be regenerated and EF migrations scaffolded with `dotnet ef migrations add`.
- Do not touch the legacy Blazor `sources/WebApp`; generated code (`src/generated/api.ts`) is never hand-edited.
- UI component reuse: no raw `<input>` posing as a button; use `ActionButton`/`ActionLink`; create reusable components instead of page-local markup for standard controls (this is why `FormFields` and `Disclosure` are new shared components).
- Architecture boundaries: do not broaden API, shared contracts or core infrastructure beyond what the feature needs (none needed here).
- Commits: `type(scope): summary` under 80 chars, one cohesive unit per commit, no Markdown in the summary; breaking changes need `!` and a `BREAKING CHANGE:` body (none expected).
- Repo style carried by tooling: Prettier (`format:check`), ESLint `--max-warnings 0` with `eslint-plugin-react-hooks` 7.0.1 recommended rules, TypeScript strict with `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes`. [VERIFIED: sources/ReactApp/tsconfig.app.json: `"noUncheckedIndexedAccess": true,` and `"exactOptionalPropertyTypes": true,`]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Capability catalog (games, providers, sizes, storage, flags) | API / Backend (`/game-servers/capabilities`) | Browser (renders generically) | API projects it from game definitions; client must not know game ids (CRT-06) |
| Option lists (profiles, mod packs, terrains, scenarios, cost owners) | API / Backend | Browser (React Query cache) | Authorization-scoped lists already exist |
| Defaults, preset projection, readiness, cost math | Browser (pure functions in `createServerModel.ts`) | — | No server data needed beyond the queries; keeps one source of truth, unit-testable |
| Frozen-owner check | API / Backend (real guard: `HostingValidationException`) | Browser (early block with reason) | UI block is advisory; server rejects again |
| Storage fit and demand estimate | API / Backend (`POST /game-servers/storage-demand-estimate`) | Browser (gates submit) | Server computes; client only gates and explains |
| Create record | API / Backend (`POST /game-servers`, returns projected `GameServerDto`) | Browser | Creates a durable record only; starts nothing |
| Start after create | API / Backend (action endpoint, job executor) | Browser (shared `CostConfirmationProvider` dialog) | Dialog is a UX guard; API revalidates permission and state |
| Post-create notice / start orchestration | Browser (workspace page + router state) | — | Pure UI state, no persistence |

## Standard Stack

### Core (all already installed; no new dependency)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | existing |
| react-hook-form | 7.85.0 | form state | existing create page already uses it with `zodResolver` |
| @hookform/resolvers | 5.2.2 | zod resolver | existing |
| zod | 4.4.3 | schema; `z.uuid()` already used | existing |
| @tanstack/react-query | 5.101.4 | all option queries, invalidation | existing |
| react-router | 7.18.2 | `useNavigate`, `useLocation().state`, `useBlocker` | existing |
| i18next / react-i18next | 26.3.6 / 17.0.11 | `t()` with `{{name}}` interpolation and `_one/_other` plurals | existing |
| @fluentui/react-components | 9.74.6 | `Spinner`, `FluentProvider` only | existing; tests alias it to `tests/mocks/react-components.tsx` |
| @fluentui/react-icons | 2.0.320 | `ChevronDown20Regular`, `Warning20Regular`, `Info16Regular` | existing; also aliased in tests |

[VERIFIED: sources/ReactApp/package.json dependencies block, all versions above read in this session]

### Supporting (dev, existing)

| Library | Version | Purpose |
|---------|---------|---------|
| vitest | 4.1.10 | unit/component tests (jsdom 28.0.0) |
| @testing-library/react / user-event / jest-dom | 16.3.2 / 14.6.1 / 6.9.1 | component tests |
| @playwright/test + @axe-core/playwright | 1.62.1 / 4.13.0 | e2e at 320 and 390 plus axe |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Native `<details>` Disclosure | Fluent Accordion | Spec locks project primitives and native details; no new dependency |
| Server-defined presets | new endpoint/table | Deferred (FUT-03); D-01 forbids unless unavoidable (it is avoidable) |

**Installation:** none. **Version verification:** versions are the pinned values in `package.json` read this session; nothing is added, so no registry lookup applies.

## Package Legitimacy Audit

No external package is installed by this phase. `@axe-core/playwright` is already a dev dependency. The audit is therefore empty.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | - | - | - | - | - | - |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Fleet header / empty-state link ──► /command/servers/create  (CreateServerPage, claim-gated)
                                          │
        ┌─────────────────────────────────┼───────────────────────────────────────────┐
        ▼                                 ▼                                           ▼
 capabilities query              per-game option queries                     cost-owner query
 (games, providers,              creationModPacks, profileOptions,           (frozen flags only)
  sizes, storage, flags)         terrains*, scenarios*  (*only if required)          │
        │                                 │                                           │
        └──────────────┬──────────────────┘                                           │
                       ▼                                                              │
        storage estimate query (game + provider + modPack)                            │
                       │                                                              │
                       ▼                                                              ▼
   createServerModel (pure):  resolveCreateDefaults → form values (RHF, touched-aware)
                              presets(profileOptions, modPacks)  ◄── Preset select edits the SAME two form values
                              deriveCreateReadiness(owners, options, provider, estimate) → first blocking reason | null
                              hourly = combinedHourlyCost(...) ; monthly = money rule
                       │
                       ▼
   Page composition: Your server (Name, Game, Preset, Size, Billed to) ─ Advanced (Disclosure) ─ After creating
                     CreateSummary (sticky aside / bottom bar): estimate, reason, submit, request error
                       │ submit (blocked ⇒ nothing; invalid ⇒ focus first invalid in DOM order, open Advanced)
                       ▼
              hostingService.create(payload)  ──► POST /game-servers ──► GameServerDto (actions projected)
                       │
        ┌──────────────┴───────────────┐
        ▼ keep off                     ▼ start now
 reset baseline, invalidate,    reset baseline, invalidate, toast,
 toast, navigate(/{id})         navigate(/{id}, { state: { createdStartPending: true } })
                                         │
                                         ▼ ServerWorkspacePage (consumes state once, clears it via replace)
                       start action openable? ── yes ─► deferred request({serverId, actionId:"start", source:"create"})
                                         │                        │ confirm → job accepted (onSubmitted clears notice)
                                         └─ no / dismissed ──────► info notice "created and still off" (+ mapped reason)
```

### Recommended Project Structure

```
sources/ReactApp/src/
├── components/
│   ├── FormFields.tsx            # NEW: TextField, SelectField, CheckboxField, RadioGroupField (generic)
│   ├── Disclosure.tsx            # NEW: controlled native <details>
│   └── server/
│       ├── create.css            # NEW: tokens only; covered by tests/styles/server-style-guard.test.ts
│       ├── PresetField.tsx  SizeField.tsx  CostOwnerLine.tsx  AdvancedOptions.tsx
│       ├── StorageOptions.tsx  StorageDemandDetails.tsx  AfterCreatingChoice.tsx  CreateSummary.tsx
│       └── useCostConfirmation.ts   # EDIT: add "create" to CostConfirmationSource
├── features/hosting/
│   ├── CreateServerPage.tsx      # REWRITE: composition + form state only
│   ├── createServerModel.ts      # EXTEND (additive): defaults, readiness, presets, money
│   └── ServerWorkspacePage.tsx   # EDIT: consume createdStartPending, notice, deferred request
├── styles/tokens.css             # EDIT: --utf-create-summary-width, --utf-create-bar-reserve
└── locales/{en,de}.json          # ADD Server_Create_* keys
```

### Pattern 1: Keep `ServerCreationFormValues` as the payload shape; UI-only state lives beside it

`ServerCreationFormValues` maps 1:1 onto the create payload and the existing zod schema. "After creating" (keep off / start), the Advanced open state, the "touched" set and the announcement text are UI state, not form values. Do not add them to the schema.

### Pattern 2: `resolveCreateDefaults` is pure and touched-aware

```typescript
// Source: signature proposed from 64-UI-SPEC "Defaults and generated values"; identifiers verified in
// createServerModel.ts (ServerCreationFormValues, orderProviders, isProviderAvailable) and types.ts.
export function resolveCreateDefaults(input: {
  capabilities: ServerCapability[];
  current: ServerCreationFormValues;
  touched: ReadonlySet<keyof ServerCreationFormValues>;
  terrains: GameContentOption[] | undefined;
  scenarios: GameContentOption[] | undefined;
  storageDemand: StorageDemand | undefined;
  owners: ServerCreationCostOwnerOption[] | undefined;
}): ServerCreationFormValues;
```

Rules to encode (all from the UI-SPEC table, re-checked against the current page's effects, `CreateServerPage.tsx:349-386` and `:500-520`): game = first capability; provider = `orderProviders(...filter(isProviderAvailable))[0]`; size = cheapest with a known hourly price whose fit is not `isSufficient === false`, else first; strategy = `AutomaticExternal` when `provider.storage?.supportsExternalBlockStorage` else `IncludedDisk`; terrain/scenario = item whose version `sourceIdentifier` equals `defaultTerrainSourceIdentifier` / `defaultScenarioSourceIdentifier`, else first; a chosen value is kept only while it is still valid; a selected cost owner id that is no longer in the owner list resets to `null`.

Track "touched" as React state (or a `Set` in state) set from member-change handlers. `react-hooks/refs` (enabled, see Pitfall 9) forbids reading a ref during render, and RHF `dirtyFields` loses the "member set it back to the default" case.

### Pattern 3: One money rule, one source of truth

Hourly = `combinedHourlyCost(profile, selectedStorageBytes(...), strategy, provider.storage)` (existing). Monthly = `profile.estimatedMonthlyCostEur` when strategy is `IncludedDisk`, else hourly x 730 labelled "about" (the convention the current form uses, `CreateServerPage.tsx:985`: `euro(hourlyCost * 730, 2)`). Format with `formatHourlyRate` / `formatMoney` from `@/features/hosting/model` (verified exports, `CostConfirmDialog.tsx:18`). While `storageEstimate.isFetching` the hourly line shows "Calculating…" and never a stale amount.

### Pattern 4: `deriveCreateReadiness` returns the first blocking reason in the UI-SPEC priority order

Return a discriminated reason code (not a string) so the page maps it to a `Server_Create_Blocked_*` key. The eight reasons are in 64-UI-SPEC "Blocked-submit reasons". Pass loading/error flags per query explicitly: `isCostOwnerSelectionValid` returns `false` for "owners undefined", "selected owner missing" and "selected owner frozen" alike, so it cannot distinguish loading from frozen. Keep it as the frozen/unknown rule and compute loading/failed from the query state before calling it. [VERIFIED: createServerModel.ts:63-73]

### Pattern 5: "Create and start" via router state consumed by the workspace page

1. `CreateServerPage` on success: `form.reset(values)`, await the three invalidations (existing order), toast, `navigate(\`/command/servers/${server.primaryKey}\`, { state: { createdStartPending: true } })` for start-now (no state for keep-off).
2. `ServerWorkspacePage` reads `useLocation().state` (accept only `=== true` on that one key), copies it into local state once, then clears the history entry with `navigate(location.pathname + location.search, { replace: true, state: null })` (router state survives a browser reload, so it must be cleared to meet "never re-appears on reload").
3. When `server.data` is loaded: if the `start` action exists and is not `actionIsUnavailable(...)`, call `request({ serverId, actionId: "start", source: "create", onSubmitted })` **deferred** (Pitfall 1); otherwise show the notice with `ActionUnavailableReason` appended.
4. Notice (`StatePanel kind="info"`, dismissible, pass through the existing `notices` slot of `WorkspaceShell`) renders while the local flag is set and `deriveServerState(server).state === "off"`; `onSubmitted` clears the flag at once. Use `deriveServerState`, never `deployment.status` (single-owner guard test).

### Pattern 6: One source of truth for presets

A preset is a projection `{ kind: "default" | "profile" | "modPack", id, title, revision? }` over the two option queries. The select value is derived from `(configurationProfileId, modPackDefinitionId)`: both null => default; profile only => that profile preset; mod pack only => that mod-pack preset; both set => `"custom"` and a disabled `Customized` option is rendered so the controlled `<select>` never has a value without an option. Choosing a preset sets both fields, sets "After creating" to keep off (D-02), and announces. Preset and Advanced edit the same two RHF values.

### Anti-Patterns to Avoid

- **Game literals in create files.** No `arma3`, `aska` or `counterstrike2` string in `CreateServerPage`, `createServerModel` or `components/server/*` create files (source-scan test).
- **`form.watch()`.** Flagged by `react-hooks/incompatible-library`; use `useWatch` as the current page does.
- **Reading `deployment.status`** in the workspace notice (use `deriveServerState`).
- **Printing `Error.message`** (the old `errorMessage()` helper); use fixed keys.
- **Reusing the global `.field` class** (`styles.css:3917`) or relying on the global `input, select, textarea` rule (`styles.css:1555`); give `FormFields` its own class names so the two cascades do not fight.
- **Seeding the server cache before navigating** and calling `request()` in the first workspace effect (Pitfall 1).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Start confirmation | a new cost dialog | `useCostConfirmation().request(...)` | One dialog, API revalidates; Phase 61 contract |
| Disabled-with-reason submit | `disabled` + tooltip | `ActionButton unavailableReason` | Keeps focus, `aria-disabled`, `aria-describedby`; click is `preventDefault`ed so Enter in a text field cannot submit [VERIFIED: ActionControl.tsx:121-128] |
| Page states | custom loading/error panels | `StatePanel`, `PageHeader`, `Breadcrumbs` | Shared, already tested |
| Unsaved-changes leave guard | custom `beforeunload` | `useUnsavedChanges(form.formState.isDirty)` | Existing hook, unchanged this phase |
| Money/number formatting | page-local `euro()`/`number()` | `formatHourlyRate`, `formatMoney`, `formatBytes` | Locale-aware, shared |
| Storage fit/validity math | new math | `isStorageSelectionValid`, `selectedStorageBytes`, `automaticExternalStorageGiB` | Existing, tested rules |
| Owner frozen rule | new rule | `isCostOwnerSelectionValid` | Existing, tested |
| API-text language marking | raw strings | `ApiText` | `lang="en"` for API-originated English |
| Server state decision | status comparisons | `deriveServerState` | Single-owner guard |

**Key insight:** this phase adds almost no new logic; it recomposes existing, tested rules. The new logic worth testing is defaults resolution, readiness priority, preset projection and the post-create start seam.

## Runtime State Inventory

Not a rename/refactor/migration phase. Omitted.

## Findings (verified facts that shape the plan)

**F1. "Cannot pay" is "frozen" at create; affordability is a start-time check.**
`GameServerLifecycleService.CreateAsync` rejects only a frozen owner: `if (costOwner.IsFrozen)` followed by `throw new HostingValidationException("The selected cost owner is frozen.");` [VERIFIED: sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs:208-209]. Balance is used only in the start path: `var availableAfter = owner.Balance + (owner.Allowance ?? 0m) - otherReservations - amount;` [VERIFIED: sources/Api/Services/GameServerLifecycle/HostingCostSliceService.cs:71], and `HostingAffordabilityException` is caught in the job executor, not at create [VERIFIED: LifecycleJobExecutor.cs:334]. The DTO exposes `costOwnerId`, `displayName`, `isFrozen`, `isCurrentUser` only [VERIFIED: sources/ReactApp/src/generated/api.ts:7225-7237]. Consequence: no API projection (matches the UI-SPEC); a "Create and start" can still fail later on affordability, which is exactly the Phase 63 failure panel plus the D-07 notice path.

**F2. Preset data is already filtered server-side.** `/configuration-profiles/options` returns `.Where(q => q.GameIdentifier == normalized && !q.IsArchived && q.Revisions.Any())` ordered by title, with `LatestRevisionNumber = q.Revisions.Max(r => r.RevisionNumber)` [VERIFIED: sources/Api/Controllers/ConfigurationProfilesController.cs:43-62]. The client does not need to filter archived. There is no default flag and no description. The create endpoint re-checks `!q.IsArchived` and game match [VERIFIED: GameServerLifecycleService.cs ~150-170].

**F3. `requiresModPack` is true for Arma 3 but not enforced anywhere on server creation.** The capability record is `(RequiresTerrain, TerrainLabel, RequiresScenario, ScenarioLabel, SupportsModPacks, RequiresModPack, ...)` [VERIFIED: GameContentCapabilityDefinition.cs:3-11]; Arma 3 passes `false, "Terrain", false, "Mission", true, true,` [VERIFIED: Arma3GameServerDefinitionRegistration.cs:131-137] and Counter-Strike 2 passes `true, "Map", true, "Game mode", true, false,` with defaults `"de_dust2", "competitive"` [VERIFIED: CounterStrike2GameServerDefinitionRegistration.cs:50-60]. The service defaults a missing flag to true: `requiresModPack: capability.requiresModPack ?? true,` [VERIFIED: services.ts:84]. `CreateAsync` validates terrain and scenario requirements (`RequiresTerrain && selectedTerrainId is null` -> exception) but only validates a mod pack **if one was sent** [VERIFIED: GameServerLifecycleService.cs:~94-100 and :~119-148]. The current form treats the mod pack as optional. See Open Question 1.

**F4. The server fills terrain/scenario defaults itself** when the payload omits them and the game declares a default source (`DefaultTerrainSourceIdentifier` / `DefaultScenarioSourceIdentifier`) [VERIFIED: GameServerLifecycleService.cs:~72-92]. The client default rule is a UX nicety plus a required-field guard, not a correctness dependency.

**F5. First-time creators have no `isCurrentUser` owner row.** If no personal owner exists the server creates one named after the actor: `DisplayName     = actor.Nickname,` [VERIFIED: GameServerLifecycleService.cs, `costOwner ??= new CostOwner { ... }` block]. The old form handled it with a generic "My cost owner" option. For the read-only "Billed to {name}" line use the signed-in user's `nickname` when no `isCurrentUser` owner is returned, instead of the spec's "Cost owner unknown" fallback (that fallback stays for an unresolvable selected id).

**F6. Create returns the projected server including actions**: `CreatedAtRoute(..., dto)` with `GetServerAsync` [VERIFIED: GameServersController.cs:45-52]. The client can inspect `server.actions` for the `start` action right after create without another fetch.

**F7. `CostConfirmationSource` has no `create` member.** [VERIFIED: sources/ReactApp/src/components/server/useCostConfirmation.ts:4-12]

```typescript
export type CostConfirmationSource =
  | "workspace-primary"
  | "workspace-action"
  | "workspace-header"
  | "workspace-failure"
  | "workspace-machine"
  | "fleet-card"
  | "notification"
  | "deep-link";
```
Add `| "create"`. `tests/hosting/cost-confirm-model.test.ts:160-162` enumerates the union and must be extended.

**F8. Locale and key rules.** No `Server_Create_*` key exists yet (0 matches in `en.json`). All "carried" `Hosting_*` keys named in the UI-SPEC exist in both locale files (checked programmatically). Carried `Hosting_*` values use positional `{0}`/`{1}` placeholders resolved by `formatResource`, new `Server_*` keys use `{{name}}`; the locale gate treats both styles as placeholders and requires equal sets in EN/DE. `Server_*` keys must match `^[A-Z][A-Za-z0-9]*(_[A-Z][A-Za-z0-9]*)*(_one|_other)?$` [VERIFIED: tests/locales/server-keys.test.ts]. Button-label guard `isButtonLabelKey` currently matches only `Server_Action_*_(Label|Dismiss|Pending)` and `Server_Access_Retry`; extend it for `Server_Create_Submit_*`, `Server_Create_Leave`, `Server_Create_Retry`, `Server_Create_BilledTo_Change`, `Server_Create_Owner_Retry` (UI-SPEC copy rules).

## Common Pitfalls

### Pitfall 1: `request()` right after `navigate()` drops the dialog (REPRODUCED)
**What goes wrong:** `CostConfirmationProvider` records the pathname at request time from `pathnameRef`, and closes the request when the location pathname differs: `const pathChanged = active !== null && active.pathname !== location.pathname;` [VERIFIED: CostConfirmationProvider.tsx:127]. `pathnameRef` is updated in an effect of the provider (`CostConfirmationProvider.tsx:141-146`), which runs **after** child effects of the same commit.
**Why it happens:** a workspace effect that calls `request()` in the first commit after navigation (cached server available) captures the old `/command/servers/create` path, then `pathChanged` is true and `setActive(null)` runs.
**Evidence:** scratch Vitest with the real provider across two routes and a cached server: effect calling `request()` immediately => dialog `open=false`; same call deferred with `setTimeout(..., 0)` => `open=true` (test file removed after the run).
**How to avoid:** call `request()` from `ServerWorkspacePage` deferred (`setTimeout` inside the effect, cleared on cleanup). Do **not** use `useState` + `setArmed(true)` in an effect; `react-hooks/set-state-in-effect` is enabled. Alternatively do not seed the server cache before navigating so the effect fires only after the server query resolves. Lock it with an integration test (Validation Architecture).
**Warning signs:** dialog never opens in the create-then-start test; toast "Confirm the start next" with no dialog.

### Pitfall 2: Native constraint validation blocks submit on hidden Advanced fields
**What goes wrong:** `required`, `min`, `max`, `step` on a control inside a closed `<details>` makes the browser refuse to submit ("invalid form control is not focusable") with no UI feedback.
**How to avoid:** `<form noValidate>`; keep `required` as a semantic attribute only; validation is zod via RHF. Auto-open Advanced when a required capability field has no value (UI-SPEC) as the second safeguard.

### Pitfall 3: RHF `shouldFocusError` cannot focus fields in a closed `<details>`, and ignores DOM order
**What goes wrong:** RHF focuses the first error in registration order; content of a closed `<details>` is not focusable.
**How to avoid:** `useForm({ shouldFocusError: false })`, use `handleSubmit(onValid, onInvalid)`; in `onInvalid` compute the first invalid field from an explicit DOM-order field list (pure helper, unit-tested), open Advanced first if that field lives there, then focus after the next render (effect or `requestAnimationFrame`). Counts for the `Server_Create_Invalid_one/_other` message come from the same list.
**Evidence for test setup:** jsdom 28 toggles `details.open` on `summary.click()` and fires `toggle` [VERIFIED: ran jsdom 28 this session: "open after click: true", "toggle events: 1"]. Controlled usage: `onToggle={(e) => setOpen(e.currentTarget.open)}` with `open={open}`.

### Pitfall 4: Default size depends on the storage estimate, which depends on provider and mod pack
**What goes wrong:** the old estimate query is enabled only when a `profile` is resolved (`CreateServerPage.tsx:427-429`); the new default size needs the estimate's `runtimeProfiles[].isSufficient`.
**How to avoid:** enable the estimate on `gameIdentifier && provider` only; resolve an initial price-only default immediately and refine it when the estimate settles, but only for fields the member has not touched. Debounce the cost announcement (600 ms) so the refinement does not spam the live region.

### Pitfall 5: The size "fit" line contradicts a valid default strategy
**What goes wrong:** the default strategy is `AutomaticExternal` when supported, where included-disk fit is irrelevant to validity (`isStorageSelectionValid` checks the external capacity, not `isSufficient`, for external strategies [VERIFIED: model.ts:41-70]). Showing "Too small for this setup" next to a valid external selection is wrong.
**How to avoid:** show the fit line only when strategy is `IncludedDisk` (where `isSufficient === true` is required); for external strategies omit it or show the external capacity fact. Record as a UI-SPEC clarification in the plan.

### Pitfall 6: Estimate failure has no blocking reason in the spec
**What goes wrong:** if `estimateStorage` errors, `isStorageSelectionValid` is false (no demand) and reason 8 ("does not fit") would be shown, which is untrue.
**How to avoid:** add a ninth reason or map to reason 7's neighbor, e.g. `Server_Create_Blocked_EstimateFailed` ("The storage check failed. Try again.") with the existing retry in Advanced > Storage. Needs a copy decision (Open Question 3).

### Pitfall 7: Never show a stale or invented amount
`combinedHourlyCost` returns `undefined` when the profile has no `estimatedHourlyCostEur`, and for external strategies treats a missing capacity as 0 storage cost. While the estimate is fetching, force the "Calculating…" state rather than showing compute-only as final. `estimatedMonthlyCostEur` can be null even when hourly is known; the money rule then omits the monthly line (do not invent).

### Pitfall 8: Leave guard and replace-navigation
`useUnsavedChanges` uses `useBlocker(isDirty)`. The old page called `form.reset(values)` then awaited invalidations before `navigate`, giving React a render to clear `isDirty`. Keep that order; the workspace's `navigate(..., { replace: true, state: null })` is on a different route and unaffected.

### Pitfall 9: Lint and typing traps
`eslint-plugin-react-hooks` 7.0.1 recommended enables `react-hooks/refs`, `set-state-in-effect`, `set-state-in-render`, `purity`, `incompatible-library`, `immutability` [VERIFIED: printed `configs.flat.recommended.rules` this session]. Practical effects: no ref reads during render, no synchronous setState in effects, no `form.watch()`. `beforeEach` is **not** a lint global for tests (only `describe, it, expect, vi, beforeAll, afterEach`), so import it from `vitest` as `create-server-page.test.tsx` does. `exactOptionalPropertyTypes` means optional props must be declared `?: T | undefined` when callers pass `undefined`.

### Pitfall 10: Style guard and tokens
`tests/styles/server-style-guard.test.ts` scans every `.css` in `components/server`: no hex, no colour functions, no pixel literals except a 1px border width, media breakpoints only `520px` and `800px`. `--command-header-height: 64px;` exists [VERIFIED: src/styles.css:1480]; use it (do not redefine). All other tokens named by the UI-SPEC exist (`--utf-control-height`, `--utf-control-height-touch`, `--utf-motion-base`, `--utf-elevation-1`, `--utf-border-control`, `--utf-focus-ring`, `--utf-surface-raised`, `--utf-border-divider`, `--utf-radius`, `--utf-space-xl`, `--utf-text-muted`, `--utf-state-danger-fg`, `--utf-state-warning-*`) [VERIFIED: grep of src/styles/tokens.css]; only the two new layout tokens are missing.

### Pitfall 11: Inert pending form
"Every field and the leave link are non-interactive while pending": a `<fieldset disabled>` covers controls but not links. Prefer `inert` on the main column (React 19 supports the boolean attribute [ASSUMED]) or `aria-disabled` plus `preventDefault` on the leave link; assert the attribute in tests (jsdom/user-event do not implement inertness).

### Pitfall 12: Existing tests will break and must be rewritten
`tests/hosting/create-server-page.test.tsx` uses old labels ("Create server record", `/^Mod pack \(optional\)/`, "Server creation access required" kept) and `tests/hosting/create-server.test.ts` covers the model/service (keep, extend). `tests/hosting/member-access.test.tsx` and `fleet-page.test.tsx` assert the fleet link text "Create server" (unchanged).

## Code Examples

### Add the source value (additive)
```typescript
// Source: sources/ReactApp/src/components/server/useCostConfirmation.ts:4-12 (quoted in F7)
export type CostConfirmationSource =
  | "workspace-primary"
  /* ...existing members unchanged... */
  | "deep-link"
  | "create";
```

### Workspace consumption of the create-then-start intent (shape only)
```typescript
// Source: pattern composed from ServerWorkspacePage.tsx deep-link effect (lines 78-93, `handledDeepLink` ref guard)
// and the Pitfall 1 reproduction. Identifiers `request`, `actionIsUnavailable`, `deriveServerState` verified.
useEffect(() => {
  if (!pendingStart || !server.data || startHandled.current === server.data.primaryKey) return;
  startHandled.current = server.data.primaryKey;
  const start = server.data.actions.find((a) => a.id.toLowerCase() === "start");
  if (!start || actionIsUnavailable(start)) return; // notice shows the mapped reason
  const timer = window.setTimeout(
    () => request({ serverId: server.data.primaryKey, actionId: start.id, source: "create", onSubmitted: clearNotice }),
    0,
  );
  return () => window.clearTimeout(timer);
}, [pendingStart, server.data, request]);
```
(`startHandled` is a ref written only inside the effect, which `react-hooks/refs` allows; `clearNotice` is a state setter wrapper.) Note: with a cleanup that clears the timer, a re-run caused by `server.data` changing must not skip the request, so guard by "request was actually issued", not by "effect ran". Implement and test this explicitly (StrictMode double effect).

### Early-block readiness (shape only)
```typescript
// Source: createServerModel.ts:63-73 rule kept; query flags supplied by the page.
export type CreateBlockReason =
  | "owner-frozen" | "owner-loading" | "owner-failed" | "options-loading" | "options-failed"
  | "no-provider" | "estimate-running" | "storage-misfit";
export function deriveCreateReadiness(input: CreateReadinessInput): CreateBlockReason | null;
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| 12-field form, everything visible | Name + size decisions, rest behind Advanced | this phase | Fewer decisions; hidden-field hazards (Pitfalls 2-3) |
| Page-local `euro()`/`number()`, `useHostingCopy` | shared money helpers, `useTranslation` with `Server_Create_*` | Phases 61-63 | Legacy `Hosting_Create_*` keys stay until Phase 69 |
| `HostingError` with raw message | `StatePanel` + fixed copy + retry | Phase 61 | No raw `Error.message` |

**Deprecated within the page:** `RuntimeProfileSummary`, `StorageDemandSummary` (page-local), `errorMessage()`, `.next-hosting-form-grid` / `-capability-card` / `-create-boundary` rules in `hosting.css` once unreferenced (`hosting.css:38`, `:643`, `:2159`).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | React 19 renders the boolean `inert` attribute on a `div` | Pitfall 11 | Pending state not inert; fall back to `aria-disabled` + `fieldset disabled` |
| A2 | The mismatch between `requiresModPack` (Arma 3 true) and the optional-mod-pack API/old form should be resolved by NOT requiring a mod pack at create | Open Question 1 | If the user wants a required mod pack for Arma 3, the default "Game defaults" preset must also preselect a mod pack and the default flow changes |
| A3 | Showing the size fit line only for `IncludedDisk` is the intended reading of the UI-SPEC | Pitfall 5 | Minor copy/behavior difference |
| A4 | Using the user's nickname as the "Billed to" name for first-time creators is acceptable | Findings F5 | Minor copy difference |
| A5 | Playwright's cached chromium revisions include the one pinned by @playwright/test 1.62.1 | Environment Availability | e2e needs `npx playwright install chromium` |

## Open Questions

1. **Is a mod pack required to create a server for a game with `requiresModPack: true` (Arma 3)?**
   - What we know: UI-SPEC "Capability-Driven Field Rules" says mod pack is "required only when `capability.requiresModPack` is true" and auto-opens Advanced when missing; Arma 3 is `true` (F3); the API and the current form accept no mod pack; the preset "Game defaults" and every profile preset set mod pack to none; CRT-01 limits decisions.
   - What's unclear: the intent of the flag for servers (it drives event authoring `modPackRequired` in `EventCreatePage.tsx:364`).
   - Recommendation: do not enforce it at server create (preserves CRT-mod-pack parity and the three-decision goal); keep "No mod pack" available. Record as a deliberate UI-SPEC deviation in the plan and get a one-line confirmation. Also decide whether `normalizeCapability`'s `?? true` default should stay (it only matters if enforcement is added).
2. **Monthly figure when `estimatedMonthlyCostEur` is null but hourly is known (IncludedDisk).** Recommendation: omit the monthly sentence, keep hourly; do not compute.
3. **Blocking copy for a failed storage estimate (Pitfall 6).** Recommendation: add `Server_Create_Blocked_EstimateFailed` EN/DE and show it before reason 8.
4. **Optional: invalidate the `me` query after create** so cached claims include the new server's `sv` claim. Server-side claims refresh per request (`OnValidatePrincipal`), and `canOperateServer` has no screen caller today, so this is likely unnecessary. [LOW confidence; no change planned unless a screen starts using cached claims.]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node | build, vitest | yes | v25.5.0 (package.json engines `>=24 <25`) | works in practice; vitest ran this session |
| npm | scripts | yes | 11.8.0 | none needed |
| vitest + jsdom | unit/component tests | yes | 4.1.10 / 28.0.0 | none needed |
| Playwright | e2e | yes (CLI) | 1.62.1 | browsers cached: chromium-1217..1255 and headless shells; pinned revision not confirmed (A5) -> `npx playwright install chromium` |
| .NET SDK | only if an API change appeared | yes | 10.0.201 | not needed (no API change planned) |
| API/DB at runtime | none (all tests mock `hostingService` or `**/api/**`) | n/a | n/a | n/a |

**Missing dependencies with no fallback:** none.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 (jsdom, Testing Library, user-event) + Playwright 1.62.1 with @axe-core/playwright 4.13.0 |
| Config file | `sources/ReactApp/vitest.config.ts` (aliases `@fluentui/*` to `tests/mocks`), `sources/ReactApp/playwright.config.ts` |
| Quick run command | `npm --prefix sources/ReactApp test -- tests/hosting/create-server.test.ts` |
| Full suite command | `npm --prefix sources/ReactApp run ci` (+ `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-create.spec.ts tests/e2e/hosting-a11y.spec.ts`) |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CRT-01 | Default view shows Name, (Game), Size, Billed to; Advanced collapsed with always-visible summary line; one-game renders read-only; Advanced auto-opens on required-missing / invalid / storage misfit / Change cost owner | component | `npm --prefix sources/ReactApp test -- tests/hosting/create-server-page.test.tsx` | rewrite (exists, old content) |
| CRT-01 | `resolveCreateDefaults`: game/provider/size/strategy/terrain/scenario rules, touched values kept, invalid chosen value reset | unit | `... test -- tests/hosting/create-server.test.ts` | extend |
| CRT-01 | Disclosure controlled open, `aria-expanded`, toggle via keyboard; FormFields label/hint/error wiring | component | `... test -- tests/form-fields.test.tsx tests/disclosure.test.tsx` | Wave 0 |
| CRT-02 | Hourly/monthly money rule; "Calculating" while estimate fetching; unknown price shows "not available", never 0; live region debounced | unit + component (fake timers) | `... test -- tests/hosting/create-server.test.ts tests/hosting/create-summary.test.tsx` | Wave 0 |
| CRT-03 | `deriveCreateReadiness` priority order (all 8 + estimate-failed); frozen owner blocks with reason before any request; Enter in text field does not submit while blocked; `create` not called | unit + component | `... test -- tests/hosting/create-server.test.ts tests/hosting/create-server-page.test.tsx` | extend / rewrite |
| CRT-04 | Keep off: no `request()` call, navigate to `/command/servers/{id}`. Start now: create, navigate with state, dialog opens (real provider, cached server), confirm calls `executeAction`; dismiss leaves notice; unavailable start shows mapped reason and no `request()`; failed create calls no `request()`; reload-safe (state cleared) | integration | `... test -- tests/hosting/create-start-flow.test.tsx` | Wave 0 |
| CRT-05 | Preset projection (default/profile/mod pack), select value derived from two fields incl. "Customized", choosing a preset forces keep off and announces; omitted when no profiles and no mod packs; options error panel with retry | unit + component | `... test -- tests/hosting/create-presets.test.ts tests/hosting/create-server-page.test.tsx` | Wave 0 |
| CRT-06 | Terrain/scenario/mod pack/storage options render from capability flags; source scan finds no `arma3`/`aska`/`counterstrike2` in create files; CS2-shaped and Arma-shaped fixtures render different fields with the same code | unit (scan) + component | `... test -- tests/hosting/create-capability-driven.test.tsx` | Wave 0 |
| D-08 | Every `CRT-*` row `kept`/`relocated` with New location and Evidence; `SVC-*` rows for the eight methods `kept`; completeness guard still green | unit | `... test -- tests/hosting/parity-matrix.test.ts` | exists (data update) |
| Locales | `Server_Create_*` keys present EN/DE with equal placeholders, plural `Invalid_one/_other` render in both languages; no banned generic button label | unit | `... test -- tests/locales` and `npm --prefix sources/ReactApp run locales:check` | extend `server-keys.test.ts` |
| Style | `create.css` token-only | unit | `... test -- tests/styles` | auto-covered |
| Layout/a11y | 320x568 and 390x844 in EN and DE: default, blocked-owner, Advanced open, invalid submit; no horizontal overflow; bottom bar <= one third of viewport; last Advanced field not obscured; axe WCAG 2.1 AA zero violations with non-vacuous `passes.length` | e2e | `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/hosting-create.spec.ts` | Wave 0 |
| Source union | `"create"` accepted by `CostConfirmationSource` | unit | `... test -- tests/hosting/cost-confirm-model.test.ts` | extend |

### Sampling Rate
- **Per task commit:** touched test files plus `npm --prefix sources/ReactApp run typecheck` and `lint`.
- **Per wave merge:** `npm --prefix sources/ReactApp test`, `locales:check`, `tests/styles`, `tests/hosting/server-state-single-owner.test.ts`.
- **Phase gate:** `npm --prefix sources/ReactApp run ci`, the Playwright specs above, `git diff --exit-code -- sources/Common/Ui/routes.json`, `contracts:check` clean (no API change expected; `dotnet test tests/Api.Tests/Api.Tests.csproj` only if an API change is introduced).

### Wave 0 Gaps
- [ ] `tests/form-fields.test.tsx`, `tests/disclosure.test.tsx` - shared controls (CRT-01)
- [ ] `tests/hosting/create-summary.test.tsx` - estimate, reasons, bar structure (CRT-02, CRT-03)
- [ ] `tests/hosting/create-presets.test.ts` - preset projection and select value model (CRT-05)
- [ ] `tests/hosting/create-capability-driven.test.tsx` - capability fixtures + source-scan guard (CRT-06)
- [ ] `tests/hosting/create-start-flow.test.tsx` - two-route integration with the real `CostConfirmationProvider`, StrictMode case (CRT-04, Pitfall 1)
- [ ] `tests/e2e/hosting-create.spec.ts` - model on `tests/e2e/hosting-a11y.spec.ts` (`mockApi`, strict unmocked-request recording, `scan()` with `passes.length > 0`)
- [ ] Rewrite `tests/hosting/create-server-page.test.tsx` for the new labels and flow; extend `create-server.test.ts`, `server-keys.test.ts`, `cost-confirm-model.test.ts`
- No framework install needed.

## Security Domain

`security_enforcement` is enabled (absent = enabled), ASVS level 1, block on high.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (unchanged) | existing cookie auth |
| V3 Session Management | no (unchanged) | existing |
| V4 Access Control | yes | Page gated by `getHostingPermissions(user).createServers`; API `[Authorize(Claims.Creation.Server)]` on create, cost-owner options, mod-pack options and storage estimate [VERIFIED: GameServersController.cs:36-37 `[HttpPost]` then `[Authorize(Claims.Creation.Server)]`; the same attribute precedes `creation/mod-pack-options`, `creation/cost-owner-options` and `storage-demand-estimate`]; server re-checks `CanUseCostOwnerAsync` and frozen owner; start still re-authorized per action |
| V5 Input Validation | yes | zod schema client-side (trim, 256 max, uuid/number checks), server validates again (`HostingValidationException`); `externalStorageSizeGiB` sent only for `FixedExternal` |
| V6 Cryptography | no | no secrets handled on this page |
| V7 Error handling / logging | yes | fixed error copy, never print `Error.message` or API detail; no secrets in toasts/notices |
| V13 API | yes | no new endpoint; unchanged contract |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored/DOM XSS via API strings (capability description, labels, profile titles, owner names, server title in toast/notice) | Tampering | Render only as React text children; API English text inside `ApiText`; long names wrap with `overflow-wrap: anywhere`; never `dangerouslySetInnerHTML` |
| Forged router state (`createdStartPending`) | Spoofing/Tampering | Accept only the literal boolean `true`; server id always from the route param, never from state; the dialog still requires an explicit confirm and the API revalidates |
| Client-side bypass of the frozen-owner block | Elevation | UI block is advisory; API rejects frozen owner; on API rejection show the request error and refetch owners |
| Double submit / duplicate servers | Tampering | Submit guarded by `create.isPending`, `aria-busy`, blocked reasons; Enter-key submission blocked via `ActionButton` click prevention plus a handler guard |
| Information disclosure through hidden owner names | Information disclosure | Owner list is API-scoped to owners the caller may use; "Billed to" shows only returned names |

## Sources

### Primary (HIGH confidence, read this session)
- `sources/ReactApp/src/features/hosting/CreateServerPage.tsx` (full), `createServerModel.ts` (full), `services.ts`, `types.ts`, `model.ts` (formatters, storage rules), `ServerWorkspacePage.tsx`, `WorkspaceShell.tsx`, `serverState.ts`
- `sources/ReactApp/src/components/ActionControl.tsx`, `primitives.tsx` (PageHeader, StatePanel, `useUnsavedChanges`), `server/CostConfirmationProvider.tsx`, `server/useCostConfirmation.ts`, `server/costConfirmationModel.ts`, `Breadcrumbs.tsx`, `server/ApiText.tsx`, `server/DeepLinkActionNotice.tsx`
- `sources/ReactApp/src/generated/api.ts` (creation payload, cost-owner DTO, runtime profile DTO), `locales/en.json` and `de.json` (key existence), `styles/tokens.css`, `styles.css`
- `sources/ReactApp/tests/*` (create tests, parity matrix guard, style guard, locale guards, helpers, e2e a11y pattern), `vitest.config.ts`, `playwright.config.ts`, `eslint.config.js`, `tsconfig.app.json`, `package.json`
- API: `GameServersController.cs`, `ConfigurationProfilesController.cs`, `GameServerLifecycleService.cs` (create path), `GameServerProjectionService.cs` (cost owners, capability projection), `HostingCostSliceService.cs`, `GameContentCapabilityDefinition.cs`, `Arma3`/`CounterStrike2` registrations
- `.planning`: `64-CONTEXT.md`, `64-UI-SPEC.md`, `REQUIREMENTS.md`, `ROADMAP.md` Phase 64, `61-PARITY-MATRIX.md`, `63-RESEARCH.md` (validation pattern), `63-12-SUMMARY.md`
- Experiments: scratch Vitest (navigate-then-request ordering) and jsdom 28 `<details>` toggle run, both executed and the scratch test removed

### Secondary / Tertiary
- None. No web search was needed; no claim depends on external documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, nothing new, versions read from `package.json`
- Architecture: HIGH, composed from existing, tested seams and the approved UI-SPEC
- Pitfalls: HIGH for 1, 2 (standard browser behavior), 3 (jsdom run), 9, 10; MEDIUM for 11 (A1)
- Open Question 1: HIGH on the facts, decision needs the user

**Research date:** 2026-11-20
**Valid until:** 2026-12-20 (stable; revisit if Phase 63 shell or `CostConfirmationProvider` changes)
