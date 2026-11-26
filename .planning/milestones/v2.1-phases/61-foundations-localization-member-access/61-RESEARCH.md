# Phase 61: Foundations, Localization & Member Access - Research

**Researched:** 2026-11-18
**Domain:** React 19 / TypeScript front-end foundations (shared state model, confirmation dialog, i18next JSON ownership, CSS design tokens, a11y) plus one ASP.NET authorization proof test
**Confidence:** HIGH for codebase facts and API behaviour (all read this session); MEDIUM for tooling choices (axe runner verified by probe, i18next plural typing not exercised)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

The approved `61-UI-SPEC.md` is the locked design contract. The user ran discuss after the UI-SPEC step and chose to
accept the UI-SPEC plus the defaults below without further questioning.

#### Locked by UI-SPEC (do not re-ask; read the spec for detail)
- **D-01:** State and cost come from one pure function `deriveServerState` in `@/features/hosting/serverState`; no
  component compares `deployment.status` itself. Property-tested invariant: `possibleCost` true never yields `off` or
  cost `none`; `off` always yields cost `none`.
- **D-02:** Notifications join to the fleet query by `serverId` (same React Query key, no new endpoint) and render
  `ServerStateBadge` + `CostIndicator`; `statusLabel` is never rendered. No API changes.
- **D-03:** Every `StartsCost`/`StopsCost` action (start, stop, terminate) opens `CostConfirmDialog` via
  `useCostConfirmation().request({ serverId, actionId, source })`. Entry points converted: profile-menu cost-stop,
  fleet card terminate link (still navigates to `?action=terminate`), workspace primary action and `ServerActions`,
  `?action=` deep link (now opens for every found, enabled, non-pending action, including `start`).
- **D-04:** Dialog reuses and additively extends `ConfirmDialog`; initial focus on dismiss; outcome-specific dismiss
  labels per action (`Server_Action_{ActionId}_Dismiss`), never a generic Cancel.
- **D-05:** Disabled actions use `aria-disabled` plus a visible, `aria-describedby`-linked reason via the additive
  `unavailableReason` capability of `ActionControl` and `UnavailableReason` (owns the reason-to-copy mapping; unknown
  API text is quoted in a `lang="en"` span).
- **D-06:** Locale source of truth is hand-edited `src/locales/en.json` and `de.json` (flat keys, `Server_{Area}_{Item}`
  convention for new keys). RESX parsing and `locales:generate` are removed. `scripts/check-locales.mjs` runs in
  `build`, `build:assets`, and `ci`, failing on missing/empty German, orphan German, and placeholder mismatch.
- **D-07:** Tokens live in `src/styles/tokens.css` (additive `--utf-*` layer, imported before `styles.css`). A contrast
  unit test and a style-guard test (no raw hex or pixel spacing literals in `components/server/*`) are required.
- **D-08:** Parity matrix lives at `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md`
  with columns `ID, Capability, Today, Status, New location, Phase, Evidence`; statuses `pending|kept|relocated|removed`.
- **D-09:** Fleet page gains `HostingAccessBoundary`; a user with no server claim does not issue the fleet query. A bare
  `sv`/`sv:view` member gets read experience with operate actions disabled-with-reason and the Create control disabled
  with a reason.

#### Locale seeding (default chosen)
- **D-10:** Seed `en.json`/`de.json` once from the current generated output (6,055 keys). Drop unreferenced keys.
  Every still-referenced key whose German value equals English (~628 today) receives a real German value, unless it is
  listed in `src/locales/identical-allowed.json` with a reason (proper nouns, "Server"). Identical-value check stays a
  warning in this phase and becomes a failure in Phase 69. — **Reversibility:** costly — key names are referenced by
  every component, so renaming later touches many call sites.

#### ACC-01 proof (default chosen)
- **D-11:** Prove a bare `sv` and a bare `sv:view` holder end to end with an `Api.Tests` integration test asserting the
  real list-endpoint response (servers returned match view permission), plus a React-side check that the navigation,
  fleet, and boundary behave for both claims. UI-only mocks do not satisfy ACC-01. If the API list endpoint is found to
  filter wrongly for these claims, a minimal API fix is in scope for this phase because ACC-01 cannot be met without it;
  keep it inside the existing `HasServerPermission` boundary and do not broaden contracts.

#### Parity matrix (default chosen)
- **D-12:** One row per capability (per workspace tab, `hostingService` method, projected action id, fleet entry point,
  create-form field group), seeded from the UI-SPEC inventory and completed by reading the code. Rows are never
  deleted; `removed` requires a written reason and explicit user sign-off recorded in `Evidence`. The completeness
  unit test (every `WorkspaceTab` id and `hostingService` method has a row, no empty status) runs in CI from this phase.

#### Rollout and cleanup (default chosen)
- **D-13:** `ActionConfirmation` and `ServerStatus` are replaced in one cut by `CostConfirmDialog` and
  `ServerStateBadge`, with no long-lived compatibility shim. Test fixtures modelling Start as `dangerous: true` are
  corrected to `dangerous: false` (the dialog must still open). Copy entries used by shared server components move
  from `copy.ts` into the JSON; `copy.ts` otherwise shrinks only as screens are redesigned and must be gone for
  redesigned screens by Phase 69.

### Claude's Discretion
- Exact Fluent icon glyph per state (fixed per state and tested), file/module layout under
  `src/components/server/`, test-runner choice for the axe scan (jest-axe or `@axe-core/playwright`), and the
  mechanics of the check scripts.

### Deferred Ideas (OUT OF SCOPE)
- Fleet card grid overflow at many servers: Phase 62 owns fleet layout.
- Stable API reason code replacing exact-string reason matching: Phase 63.
- Global `--quiet` contrast fix and identical-EN/DE warning promoted to failure: Phase 69.
- Full Playwright mobile projects and German 320/390 visual baselines: Phase 69.
- Create-and-start confirmation (`source: create`): Phase 64.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FND-01 | Server screens use shared semantic design tokens | `tokens.css` layer is additive over the `:root` block at `styles.css:1457`; contrast numbers in UI-SPEC re-computed and reproduce (danger on soft is 4.58, margin 0.08); test must alias-resolve across both CSS files and alpha-composite the 12% tints |
| FND-02 | Same status on fleet, workspace header, notifications | Five current status renderings enumerated (Standard Stack / Pitfalls); workspace header has **no** status today; Overview dashboard also renders `statusLabel` |
| FND-03 | Possible-cost server never labelled off/stopped | Exhaustive table test (12 statuses x 2 x 2 x 2); compile-time `satisfies Record<EDeploymentStatus, ...>` makes a new API status fail tsc |
| FND-04 | One cost confirmation for every cost-bearing action | Provider-in-layout pattern; API facts: `stop` has a typed-title challenge, `terminate` always has one, `start` has none |
| FND-05 | `?action=` deep link opens same confirmation or explains | Current gate `Boolean(candidate.confirmationChallenge)` at `ServerWorkspacePage.tsx:637` is why `?action=start` does nothing |
| FND-07 | Parity matrix, nothing lost | `.planning/` is entirely git-ignored (0 tracked files): a CI test cannot read the locked D-08 path; see Open Question 1 |
| L10N-01 | React owns EN/DE JSON, no RESX | Only `i18n.ts`, `localizationTypes.ts`, `i18next.d.ts` import the generated JSON; `keys.ts` has zero importers |
| L10N-02 | Build fails when English key lacks German | Pure-node `check-locales.mjs`, spawn-tested; must normalise `{0:N0}` format specs; 3 existing placeholder defects must be fixed in the seed |
| ACC-01 | `sv` / `sv:view` member sees servers they may view, incl. API response | API filtering verified correct by reading; `sv` implies Operate, so only `sv:view` is read-only; controller-level test recommended |
| ACC-03 | Disabled-with-reason | `aria-disabled` pattern; API reason literals verified verbatim |
</phase_requirements>

## Summary

Phase 61 is almost entirely a front-end refactor inside `sources/ReactApp`, plus one C# test file. The code reading
changes the plan in five material ways. First, `.planning/` is wholly git-ignored, so the locked parity-matrix location
(D-08) cannot be read by the CI completeness test (D-12); the matrix needs a tracked home. Second, an axe scan of an
open `<dialog>` under jsdom does not report violations, it reports every rule as `incomplete`, so a jsdom axe test
would pass while a real violation exists (probe reproduced below). The dialog scan therefore belongs in Playwright
Chromium with `@axe-core/playwright`. Third, the API already filters the list endpoint correctly for `sv` and
`sv:view`; ACC-01 needs a proof test, not an API fix, and `sv` is the "all permissions" claim, so only an `sv:view`
holder gets the read-only experience the UI-SPEC describes. Fourth, the real API sends `costOwnerName` as the literal
`"Assigned owner"` to viewers who cannot see the owner, so the "Cost owner" fact row will often show an English
placeholder that must be localised. Fifth, only about 3,021 of the 6,055 generated keys are referenced by the React
source, and only 208 of those have an English-identical German value (not ~628), which shrinks the translation work
a lot.

The recommended build order is: locale ownership first (typed `t()` makes every new `Server_*` key a compile-time
dependency), then tokens, the pure state model and vocabulary tables, then the shared components, then the
`CostConfirmationProvider` and entry-point conversion, then tests, e2e axe, docs. The ACC-01 API test, the parity
matrix and the locale seed are independent and can run in parallel.

**Primary recommendation:** Mount one `CostConfirmationProvider` inside both route layouts (`CommunityLayout`,
`CommandCenterLayout`) so the profile menu, fleet card, workspace and `?action=` deep link all call
`useCostConfirmation().request()` against one dialog; derive every badge from one pure `deriveServerState`; make
`@axe-core/playwright@4.13.0` the axe runner; keep the parity matrix in a tracked path under `sources/ReactApp`.

## Project Constraints (from AGENTS.md)

- English-only identifiers, comments and technical docs; localized end-user text only in resource files (here: the
  JSON locale files). [VERIFIED: AGENTS.md "Handwritten source standards"]
- Enum types need an `E` prefix. The React app uses string-literal unions and has no TS `enum` declarations
  [VERIFIED: grep for `enum ` in `sources/ReactApp/src` and `tests` returned nothing]. Keep unions; if a TS `enum` is
  ever introduced it must be `E`-prefixed.
- C# XML documentation on every type and member of the new `Api.Tests` file(s); one top-level type per file named
  after the type. Existing tests use `/// <summary>` + `<param>` + `<returns>` (see `GameServerProjectionServiceTests.cs`).
- Generated code is exempt and must not be hand-edited: `sources/ReactApp/src/generated/api.ts` stays generator-owned.
  (`src/generated/locales/*` is deleted by this phase, not edited.)
- Architecture boundaries: do not broaden the API, shared contracts or core infrastructure. Phase 61 adds no API
  surface; the only C# change is a test file.
- Commit cadence: commit each cohesive verified unit. Subject format `type(scope): summary`, under 80 chars, no
  Markdown; recent React work uses scope `webapp` (`fix(webapp): ...`) and hosting work `hosting`. `!` only for
  breaking public-contract changes (none expected). Attribution line comes from the session reminder.
- UI: reuse shared controls; never a raw `<input>` or `<button>` posing as a control; build from reusable components.
  Note the current hosting components violate this in places (`components.tsx` uses raw `<button className="button ...">`);
  new code must not copy that.
- Do not read or commit secrets (`appsettings.secret.json` etc.). Not touched by this phase.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| View permission (which servers a member may see) | API / Backend | Browser (navigation visibility only) | `GetServersAsync` filters per server id with `HasServerPermission`; the browser's `hasClaim` ignores the claim value and is UX gating only |
| Operate permission (is an action enabled) | API / Backend | Browser (renders `enabled` + `unavailableReason`) | `ProjectAction` computes `enabled`; UI never recomputes it |
| Server state and cost label | Browser (pure model) | API supplies `deployment.status`, `possibleCost`, `pendingJobCount` | UI-SPEC D-01; no API change allowed |
| Cost estimate shown in confirmation | API supplies numbers | Browser formats | UI never computes a monthly figure |
| Confirmation before cost-bearing action | Browser | API (`confirmation` payload validated server-side) | Client dialog is UX guard; API remains authority |
| Locale strings (EN/DE) | Browser (JSON bundled in app) | -- | L10N-01 removes the RESX dependency |
| Text that originates in the API (action label, reasons, fault summary) | API (English only) | Browser maps known ids/strings, wraps rest in `lang="en"` | No silent English in German view |
| Design tokens | CDN / Static (CSS) | -- | Pure CSS custom properties |
| Parity matrix | Documentation / repo | CI (completeness test) | Must live where CI can read it |
| a11y scan | Browser test tier (Playwright Chromium) | jsdom structural assertions | jsdom cannot evaluate modal `<dialog>` for axe |

## Standard Stack

### Core (all already in the repo; no new runtime dependency)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | existing [VERIFIED: sources/ReactApp/package.json] |
| react-router | 7.18.2 | `useSearchParams` for `?action=` | existing |
| @tanstack/react-query | 5.101.4 | fleet / server / notifications cache shared by key | existing |
| i18next / react-i18next | 26.3.6 / 17.0.11 | typed flat-key translations | existing; `t()` is typed from `en.json` via `src/i18next.d.ts` |
| @fluentui/react-components / react-icons | 9.74.6 / 2.0.320 | `Spinner`, `Select`, `FluentProvider`; `*16Regular` / `*20Regular` glyphs | existing; every glyph the UI-SPEC needs exists (checked: `CircleOff16Regular`, `Money16Regular`, `ArrowSync16Regular`, `CheckmarkCircle16Regular`, `DismissCircle16Regular`, `Warning16Regular`, `Info16Regular`, `Dismiss20Regular`) [VERIFIED: grep of `node_modules/@fluentui/react-icons/lib`] |
| vitest + jsdom + @testing-library/* | 4.1.10 / 28.0.0 / 16.3.2 | unit/component tests | existing; baseline green: 22 files, 126 tests in ~20 s for hosting + profile + primitives |
| @playwright/test | 1.62.1 | e2e | existing; Chromium already provisioned in CI job `react-browser` |
| zod, prettier, eslint, typescript | 4.4.3, 3.9.6, 9.39.2, 6.0.3 | existing toolchain | `tsconfig.app.json` has `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes`, and includes `tests/` |

### Supporting (one new dev dependency)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| @axe-core/playwright | 4.13.0 (exact pin) | WCAG 2.1 AA scan of the open confirmation dialog and deep-link notice in real Chromium | Phase gate for UI-SPEC "zero violations at WCAG 2.1 AA tags"; depends on `axe-core ~4.13.0` [VERIFIED: npm registry] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `@axe-core/playwright` | `jest-axe` 11.0.0 (pins axe-core 4.12.1) or `axe-core` direct in jsdom | **Rejected.** Probe (below) shows axe in jsdom with `<dialog open>` returns all rules as `incomplete`, zero violations, even with an empty `<button>` inside. A jsdom dialog scan gives false assurance. |
| `@axe-core/playwright@4.14.0` | -- | 4.14.0 pulls `axe-core ~4.14.0` which resolves to 4.14.1, published 2026-11-16 (two days old; seam flags `too-new`). 4.13.0 resolves to the 3.5-month-old 4.13.0. |
| fast-check for the state invariant | Exhaustive nested loops | The domain is 12 statuses x 2 x 2 x 2 = 96 cases; exhaustive enumeration is stronger than random sampling and needs no dependency |
| Native Node type-stripping to share a TS checker | `.mjs` script spawned by tests | `allowJs:false` plus strict makes importing an untyped `.mjs` from a `.ts` test a TS7016 error; spawn the real CLI and assert exit code instead |

**Installation:**
```bash
cd sources/ReactApp
npm install --save-dev --save-exact @axe-core/playwright@4.13.0
```

**Version verification:**
```text
npm view @axe-core/playwright@4.13.0 dependencies  -> { "axe-core": "~4.13.0" }
npm view axe-core versions                         -> only stable 4.13.x is 4.13.0
@axe-core/playwright publish dates: 4.13.0 2026-08-11, 4.14.0 2026-10-15
axe-core publish dates: 4.13.0 2026-08-05, 4.14.0 2026-10-05, 4.14.1 2026-11-16
```
[VERIFIED: npm registry, queried this session]

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| @axe-core/playwright | npm | years (4.11.0 on 2025-10-21; latest 4.14.0 2026-10-15) | ~25.9M/wk | github.com/dequelabs/axe-core-npm | OK | Approved, pin 4.13.0 |
| axe-core (transitive of the above) | npm | years | ~119M/wk | github.com/dequelabs/axe-core | SUS (`too-new`, only because latest 4.14.1 is 2 days old) | Approved at 4.13.0 via the tilde range; `axe-core` `[WARNING: flagged as suspicious by the seam for the latest tag only — pinned version is 3.5 months old; planner adds a one-line checkpoint:human-verify that the lockfile resolves axe-core to 4.13.0]` |
| jest-axe, vitest-axe | npm | -- | -- | -- | OK | Not used (rejected above); listed because they were evaluated |

**Packages removed due to SLOP verdict:** none
**Packages flagged as suspicious [SUS]:** `axe-core` (latest tag only); no postinstall script on any of them (`postinstall: null` in the seam signals).
Provenance: `@axe-core/playwright` is the package named by the official Playwright accessibility guide
[CITED: playwright.dev/docs/accessibility-testing], and the seam returned `OK`.

## Architecture Patterns

### System Architecture Diagram

```
                         GET /game-servers (view-filtered by API)         GET /game-servers/notifications
                                    |                                                 |
                     hostingService.servers() --normalizeServer--+         hostingService.notifications()
                                    |                            |                    |
                       React Query ["hosting","servers"] <-------+----join by serverId-+
                                    |
                    +---------------+------------------+----------------------+
                    v                                  v                      v
             ServerCard (fleet)             ServerWorkspacePage        ProfileMenu + Overview attention list
                    |                       (header + record panel          |
                    |                        + CloudPanel + actions)        |
                    +---------------+------------------+--------------------+
                                    v
               deriveServerState(server | notification fallback)  -- pure, no React
                                    |
                    { state, cost, tone, labelKey, detailKey? }
                                    |
                +-------------------+--------------------+
                v                                        v
         ServerStateBadge (icon+text)             CostIndicator (icon+text)

 Entry points (all call the SAME hook, never execute directly):
   profile-menu cost-stop --+
   fleet card terminate ----+--> navigate ?action=terminate --> workspace deep-link effect --+
   workspace primary/action-+                                                                |
                            v                                                                v
                 useCostConfirmation().request({serverId, actionId, source})  <--------------+
                            |
        CostConfirmationProvider (inside CommunityLayout / CommandCenterLayout, router child)
                            |  resolves server via React Query cache or fetchQuery(server(id))
                            |  stays live with useQuery(server(id)) while open
                            v
       CostConfirmDialog (extended ConfirmDialog, native <dialog> showModal)
         idle -> pending -> error(role=alert, stays open) | success
         success: hostingService.executeAction -> toast -> invalidate fleet/server/jobs/operations/notifications
                  -> strip ?action= (replace) -> return focus (invoker, else h1)

 Disabled action path:  ActionButton unavailableReason -> aria-disabled + UnavailableReason(aria-describedby)
 Deep link miss path:   DeepLinkActionNotice (StatePanel warning|info, role=status), only after server data exists
```

### Recommended Project Structure
```
sources/ReactApp/
├── scripts/
│   ├── check-locales.mjs            # new; pure node, no git, no network
│   └── export-locales.mjs           # DELETE
├── src/
│   ├── locales/
│   │   ├── en.json                  # hand-edited, flat keys, prettier-formatted
│   │   ├── de.json
│   │   └── identical-allowed.json   # { "<key>": "<reason>" }
│   ├── generated/locales/           # DELETE (en.json, de.json, keys.ts all tracked today)
│   ├── styles/tokens.css            # new, imported in main.tsx BEFORE "@/styles.css"
│   ├── components/server/           # one component per file
│   │   ├── ServerStateBadge.tsx
│   │   ├── CostIndicator.tsx
│   │   ├── CostConfirmDialog.tsx
│   │   ├── CostConfirmationProvider.tsx
│   │   ├── useCostConfirmation.ts
│   │   ├── UnavailableReason.tsx
│   │   ├── DeepLinkActionNotice.tsx
│   │   └── server.css               # token-only; style-guard test scans this
│   └── features/hosting/
│       ├── serverState.ts           # deriveServerState + tables (pure)
│       └── actionVocabulary.ts      # action id -> keys (Map), costImpact helper, reason table
├── docs/server-parity-matrix.md     # recommended tracked home of the matrix (Open Question 1)
└── tests/ ... (see Validation Architecture)
```

### Pattern 1: One pure state function with compile-time exhaustiveness
**What:** a table keyed by the API status union checked with `satisfies Record<...>`; unknown strings fall to `unknown`.
**When to use:** every surface that shows server state.
**Example:**
```typescript
// Source: UI-SPEC "State and Cost Model"; EDeploymentStatus members verified in "Verified in-repo values" below.
import type { components } from "@/generated/api";
type EDeploymentStatus = components["schemas"]["EDeploymentStatus"];

export type ServerState =
  "off" | "starting" | "running" | "stopping" | "working" | "attention" | "unconfirmed" | "unknown";
export type ServerCost = "live" | "maybe" | "none";

const statusClass = {
  Provisioning: "starting", WaitingForWorker: "starting", Preparing: "starting",
  Running: "running",
  Stopping: "stopping", Terminating: "stopping", TerminatingDueToFault: "stopping",
  Failed: "attention", PreservationFailed: "attention", TerminatedDueToFault: "attention",
  Stopped: "settled", Terminated: "settled",
} as const satisfies Record<EDeploymentStatus, "starting" | "running" | "stopping" | "attention" | "settled">;
// A new API status makes tsc fail here; at runtime an unmapped string takes UI-SPEC rule 8 (`unknown`).

function lookupStatus(status: string | null | undefined) {
  return status !== null && status !== undefined && Object.hasOwn(statusClass, status)
    ? statusClass[status as EDeploymentStatus]   // hasOwn guards "constructor" / "__proto__"
    : undefined;
}
```
The cost rule 3 set (`Provisioning, WaitingForWorker, Preparing, Running, Stopping, Terminating, TerminatingDueToFault`
=> `live` regardless of `possibleCost`) is a second `satisfies`-checked table, not a re-derivation from `statusClass`
(`Failed`/`PreservationFailed` are deliberately not in it).

### Pattern 2: Provider-in-layout confirmation entry point
**What:** `CostConfirmationProvider` rendered by `CommunityLayout` and `CommandCenterLayout` (both are router children
that already host `ProfileMenu`), exposing `request({ serverId, actionId, source, returnFocus? })`.
**Why not App-level:** `AppToastProvider` sits outside `RouterProvider` (`App.tsx:669-679`), but the provider needs
`useSearchParams` to strip `?action=` and needs to sit under `QueryClientProvider`, `AuthProvider`, `AppToastProvider`.
Both layouts satisfy that; the root `App` does not satisfy the router requirement.
**Behaviour contract:**
- Resolve the server from `hostingQueryKeys.server(id)` cache, else the fleet cache, else `fetchQuery`. Then keep a live
  `useQuery(server(id))` while open, so `enabled`/`pending` changes while the dialog is open disable Confirm and show
  the mapped reason (never submit against stale availability).
- `request()` for the same `(serverId, actionId)` while already open is a no-op (React StrictMode double effects fire the
  deep-link effect twice).
- Re-validate `action.enabled && !action.pending` at confirm time.
- Hook throws outside the provider (same convention as `useAppToast`), so tests wrap with a shared helper.
- `source === "deep-link"` strips `action` from the query string (`replace`, keep other params) on close, confirm or dismiss.

### Pattern 3: Additive `unavailableReason` on the shared action control
`ActionButton` gains `unavailableReason?: string | null` (under `exactOptionalPropertyTypes`, type it as
`string | null | undefined` explicitly). When present it renders `aria-disabled="true"` (not `disabled`), a generated
`useId()` linked through `aria-describedby`, swallows `onClick` and form submission (`event.preventDefault()`; a
`type="submit"` button with `aria-disabled` still submits), and renders `<UnavailableReason>` directly below inside a
wrapper. `ActionLink` cannot be disabled (a `Link` navigates), so disabled-with-reason call sites render
`ActionButton`, never `ActionLink`. Add `.button[aria-disabled="true"]` to the existing `.button:disabled` rule at
`styles.css:1989`, otherwise the control looks enabled.

### Pattern 4: Typed vocabulary tables, never string-built keys
`t()` is typed against `en.json`, so build lookups as literal-key tables checked by the compiler:
```typescript
const actionKeys = {
  start: { label: "Server_Action_Start_Label", dismiss: "Server_Action_Start_Dismiss" },
  "collect-logs": { label: "Server_Action_CollectLogs_Label", dismiss: "Server_Action_CollectLogs_Dismiss" },
  // ... closed list from UI-SPEC
} as const satisfies Record<string, { label: TranslationKey; dismiss: TranslationKey }>;
const actionVocabulary = new Map(Object.entries(actionKeys));   // Map, not property lookup
```
Use a `Map` (or `Object.hasOwn`) for every id-to-copy lookup. `?action=constructor` and `?action=__proto__` are
user-controlled URL input, and `({a:1})["constructor"]` is a function (verified by running node). The existing
`deploymentStatusKey` and `serverMessageKey` in `model.ts` have this flaw; the new code must not.

### Anti-Patterns to Avoid
- **Deciding status in a component** (`deployment.status === ...`). Every site listed under Pitfall 8 must go through `deriveServerState`.
- **Building keys with template strings** for the new vocabulary (`` `Server_Action_${id}_Label` ``). Defeats typed `t()` and the orphan check.
- **Reusing `Common_Cancel` / `Hosting_Common_Cancel`** for any dialog this phase touches.
- **Rendering `notification.statusLabel`.** It is a raw stage or the literals `"Stopped"` / `"Lifecycle work pending"`.
- **Putting the parity matrix only under `.planning/`** (untracked).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Modal focus trap / inert background | a custom trap | native `<dialog>` + `showModal()` (already how `ConfirmDialog` works) | browser does trap and inert; `ProfileMenu`'s hand-written `trapFocus` is the cautionary example |
| Currency / number / plural formatting | string concatenation | `Intl.NumberFormat` via `currentLocale()` and i18next `_one/_other` + `{{count}}` | German `0,42 €` uses a non-breaking space |
| Contrast math | an approximation | WCAG 2.x relative-luminance formula in a ~25-line test helper | UI-SPEC numbers reproduce exactly with it (checked) |
| a11y rule engine | hand-written ARIA assertions as the gate | `@axe-core/playwright` | rule set is maintained upstream |
| Placeholder comparison in the locale check | regex-free string compare | one normaliser that maps `{0:N0}` to `{0}` and extracts `{{name}}` | legacy format specs legitimately differ between languages |
| Reason-string translation | per-call-site `if (reason === ...)` chains | one `UnavailableReason` owning a `Map` of the six known strings | seven call sites would otherwise drift |
| Dialog confirm-until-typed field | uncontrolled `<input>` read at submit | a controlled field with state, confirm `aria-disabled` until equal | UI-SPEC requires Confirm disabled until exact match |

**Key insight:** the risk in this phase is drift between surfaces (five status renderings, three dialogs, two copy
systems), not algorithmic difficulty. Every item above exists to give each concern exactly one owner.

## Runtime State Inventory

This is a source-of-truth migration for translations (RESX-derived to React-owned JSON). Categories answered explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | Browser `localStorage` key `UtfCulture` (language preference) is read by the detector and written by `setCulture` in `src/app/i18n.ts`. No server-side store references translation keys. | None. Key name and values (`en`, `de`) unchanged [VERIFIED: `i18n.ts` read]. |
| Live service config | None. No external service stores translation keys or the `Hosting_*` strings. | None. |
| OS-registered state | None. | None. |
| Secrets / env vars | None. `UTF_REACT_BASE_PATH`, `UTF_API_TARGET` are unrelated. | None. |
| Build artifacts / tracked generated files | `src/generated/locales/en.json`, `de.json`, `keys.ts` are **tracked** in git (`git ls-files src/generated` lists all three plus `api.ts`). `.prettierignore` line `src/generated/locales/*.json` becomes dead. `dist/` and `node_modules/.tmp/*.tsbuildinfo` carry stale types until rebuilt. `docs/features/react-ui-beta.md:73` still states "The existing English and German RESX files remain the localization source of truth." | **Code edit + deletion**: `git rm` the three generated files, drop the `.prettierignore` line, rewrite the doc paragraph (and the "fails when either localization files or TypeScript declarations drift" sentence). `.mozaic/check-ordinary.sh` keeps working unchanged: it stages only `src/generated` as a baseline for `contracts:check`, and `locales:check` no longer uses git. |

## Common Pitfalls

### Pitfall 1: `.planning/` is not in git, so the parity-matrix test cannot read it in CI
**What goes wrong:** D-08 locks the matrix at `.planning/phases/61-.../61-PARITY-MATRIX.md` and D-12 requires a
completeness unit test "in CI". `.planning/.gitignore` is `*` and `git ls-files .planning | wc -l` is `0`
[VERIFIED: ran both this session]. The Mozaic `react-tests` job says "Source transfer excludes .git"
(`check-ordinary.sh:14`) and runs `cd sources/ReactApp`. A Vitest test reading that path fails on any fresh clone and
in CI, and the matrix itself is not durable.
**How to avoid:** put the canonical matrix at a tracked path the test can reach from `sources/ReactApp` (it already
relies on `../Common` and `../WebApp` siblings, so `sources/` is transferred; `docs/` and `.planning/` are not proven).
Recommended: `sources/ReactApp/docs/server-parity-matrix.md`, add it to `.prettierignore` (prettier would re-align the
7-column table), keep a one-line pointer file at the D-08 path. See Open Question 1; this needs user sign-off because
D-08 is locked.
**Warning signs:** the test uses an absolute or `../../.planning` path.

### Pitfall 2: axe on an open `<dialog>` in jsdom silently passes
**What goes wrong:** reproduced in a scratch project with `axe-core@4.12.1` and `jsdom@28.0.0`:
```text
plain empty button       violations: [ 'button-name' ]  incomplete: (none)
dialog empty button      violations: []                 incomplete: aria-allowed-attr, ..., button-name, nested-interactive, scrollable-region-focusable
div role=dialog          violations: [ 'button-name' ]  incomplete: 0
```
[VERIFIED: probe run this session] With `<dialog open>` every rule moves to `incomplete`, so `toHaveNoViolations()` passes
on a broken dialog.
**How to avoid:** run the dialog and notice scans in Playwright Chromium (`@axe-core/playwright`, `withTags(["wcag2a","wcag2aa","wcag21a","wcag21aa"])`,
`waitFor()` the dialog first, per [CITED: playwright.dev/docs/accessibility-testing]). Cover the fast loop in jsdom
with structural assertions (every `aria-labelledby`/`aria-describedby` id resolves, dialog has a name, icon is
`aria-hidden`, disabled control has a described reason). If a jsdom axe helper is ever added, it must also fail on a
non-empty `incomplete`.

### Pitfall 3: pressing Escape twice closes a "pending" native dialog anyway
**What goes wrong:** UI-SPEC says Escape is disabled while pending. `ConfirmDialog` already calls
`event.preventDefault()` in `onCancel`, but browsers only fire a cancelable `cancel` once per user activation; the
second Escape closes the dialog [CITED: developer.mozilla.org/en-US/docs/Web/API/CloseWatcher/cancel_event]. React
state still says `open`, the request keeps running, and a failure would be invisible.
**How to avoid:** also handle the dialog's `close` event: when state is still open (pending or error), call `showModal()`
again. Success path closes through state. Test with `fireEvent(dialog, new Event("close"))` while pending.

### Pitfall 4: the existing `ConfirmDialog` is not shaped for this phase
**What goes wrong (all verified in `primitives.tsx:301-392`):** confirm is hard-coded `variant="danger"`
(Start would be red); the challenge field is an uncontrolled `<input ref>` compared only at submit, so Confirm is never
disabled; there is no initial-focus control (the first focusable wins, today the dismiss button only by DOM order);
there is no busy state; and `.dialog h2` is 2rem uppercase Barlow Condensed (`styles.css:4188-4194`), contradicting
the UI-SPEC 20px/700 heading. The `<dialog>` stays mounted while closed, so state must be reset on `open` change.
**How to avoid:** extend additively (`confirmVariant`, `children`, `initialFocus`, `busy`, `error`), call `.focus()` on
the dismiss button after `showModal()` (React `autoFocus` runs at mount while the dialog is closed, so it does nothing),
add a `.dialog--cost` modifier for the typography/spacing overrides instead of editing `.dialog`, and keep existing
callers (archive record, finance) working. Existing callers pass an explicit `cancelLabel`.

### Pitfall 5: the profile popover and the native dialog fight over Escape and focus
**What goes wrong:** `ProfileMenu` registers a document-level Escape listener that closes the popover and focuses its
trigger (`ProfileMenu.tsx`, effect on `open`), has its own `role="dialog"` popover with a hand-written Tab trap, and the
notification button that invoked the action unmounts when the popover closes.
**How to avoid:** close the popover before calling `request()`; pass `returnFocus` (the profile trigger ref) so the
provider has a live target; the provider falls back to the page `h1` when the stored invoker is no longer
`isConnected`.

### Pitfall 6: `sv` is the all-permissions claim; only `sv:view` is read-only
**What goes wrong:** the UI-SPEC and D-09 treat "bare `sv`/`sv:view`" as read-only. The API disagrees for `sv`:
```csharp
// sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs:13-19
public bool HasServerPermission(ClaimsPrincipal principal, Guid gameServerId, string permission)
{
    var value = gameServerId.ToString();
    return IsHostingAdministrator(principal)
           || principal.HasClaim(Claims.ResourceBased.Server.All, value)
           || principal.HasClaim(permission, value);
}
```
`Claims.ResourceBased.Server.All = "sv"`, so an `sv` holder passes View, Operate, Modify, secrets. Their actions are
enabled by the API. Only `sv:view` produces `enabled=false` with the reason `"Server Operate permission is required."`
(`GameServerProjectionService.cs:1006-1012`).
**Also:** `HasClaim(permission, value)` needs `value == server id`. A claim with an empty value matches nothing, and
the browser's `hasClaim` ignores the value (`claims.ts`, `authorization.ts:26`). So a "bare" claim shows the section
but returns an empty list. That is the realistic zero-server case the access empty-state copy is for.
**How to avoid:** write the ACC-01 test with resource-scoped claims (value = server id) and assert three outcomes
(see Validation Architecture). Do not assert "operate disabled" for an `sv` holder.

### Pitfall 7: the API hides the cost owner from non-viewers with an English literal
**What goes wrong:** `CostOwnerName = canSeeOwner ? server.CurrentCostOwner?.DisplayName ?? "Unknown owner" : "Assigned owner"`
(`GameServerProjectionService.cs:855-858`, repeated at `:895-898`). A `sv:view` member without `co:view` always sees
`"Assigned owner"` in the confirmation's required "Cost owner" row, in English, even in a German view.
**How to avoid:** map the two literals to `Server_Confirm_CostOwnerHidden` / `..._Unknown` keys in the dialog; show any
other value verbatim. Add the mapping to the `UnavailableReason`-style table module, not inline.

### Pitfall 8: five places show status today, and the workspace header shows none
Verified in code: `ServerStatus` (`components.tsx:55-63`) used by `ServerCard` and the overview record panel
(`ServerWorkspacePage.tsx:426`); `CloudPanel` renders `humanizeToken(deployment.status)` with a tone from
`possibleCost` (`:198-200`); the second overview panel `<h2>` renders `humanizeToken(server.deployment.status)` or
`Hosting_Detail_StateOffline` (`:495-498`); `ProfileMenu.tsx:404` and `FleetPages.tsx:64` both render
`notification.statusLabel`. The page `PageHeader` has no status at all, so success criterion 2 ("workspace header")
requires adding the badge there (additive `meta` slot on `PageHeader`; Phase 63 replaces the header).
**How to avoid:** the plan must convert all five plus add the header; grep gate: no `deployment.status` /
`deployment?.status` / `statusLabel` outside `serverState.ts` and the generated types.

### Pitfall 9: notification `costStopAction` is always `terminate`
`CostStopAction = dto.Actions.SingleOrDefault(q => q.Id == GameServerActions.Terminate)` (`:543`), and `terminate`
always carries `ConfirmationChallenge = server.Title` (`:972-981`). So in production the profile menu never took its
"execute immediately" branch; it always navigated to the workspace. The existing test that asserts navigation
(`profile-menu.test.tsx`, "routes challenge actions to the full server workspace") and the fixture action id
`"stop-cost"` model a world the API does not produce. Convert to an in-place dialog (Open Question 2) and rewrite
that test with `id: "terminate"`.

### Pitfall 10: `primaryServerAction` prefers the destructive action
`workspaceModel.ts:220-238` picks the first `dangerous` + `StopsCost` candidate when `possibleCost`. In real data that
is `terminate` ("Shut down without saving"), not the graceful `stop`, because `stop` is `IsDangerous=false`
(`Arma3GameServerDefinitionRegistration.cs`, third ctor arg `false`) while `terminate` is `Dangerous = true`
(`:979`). That contradicts "Shut down and stop billing is the one cost-stop phrase" and "gold primary is never
destructive". See Open Question 3.

### Pitfall 11: hourly cost rounds to a wrong number
`formatMoney` (`model.ts`) uses default EUR digits. Verified: `new Intl.NumberFormat("en-US",{style:"currency",currency:"EUR"}).format(0.0063)`
gives `€0.01`; with `minimumFractionDigits:2, maximumFractionDigits:4` it gives `€0.0063` (German `0,0063 €`)
[VERIFIED: node run]. A cloud server costing under a cent per hour would be shown as one cent. Add a rate formatter
for the hourly row; keep `formatMoney` for amounts. German output contains a non-breaking space, so assert with
` ` in tests.

### Pitfall 12: locale seeding traps
- Only **3,021** of 6,055 keys are referenced (2,984 as quoted literals, 37 more via template families such as
  `HostingFinance_LedgerKind_*`, `React_ServerFiles_Status_*`, `Pages_Public_Join_GuestStep*`,
  `Pages_Unit_Index_Value*`) [VERIFIED: scan over `src` + `tests` this session]. Of those, **208** are English-identical in
  German (186 are single short words or cognates such as Discord, Steam, Audit, Status, Name), not ~628.
- Three existing placeholder defects would fail the new check on day one: `ModPacks_ImportExport` (DE lacks `{0}`),
  `Pages_Command_Index_EventAttentionNoActionDetail` and `Pages_Command_Settings_RoutingSummary` (DE uses `{0:N0}`, EN `{0}`).
  The checker must normalise `{n:Nk}` to `{n}` (so the two N0 cases pass) and the seed must fix `ModPacks_ImportExport`.
- i18next defaults `keySeparator="."` and `nsSeparator=":"`; `i18n.ts` overrides neither. 228 generated keys contain a
  dot and some a colon. None of the referenced keys do (checked), so keep them dropped and make `check-locales` fail on
  any key containing `.` or `:`.
- `tsc -b` is the safety net for pruning: `t()` is typed from `en.json`, so deleting a still-used literal key is a
  compile error. Dynamic template keys are checked against the key union too. Prune with a one-off script, then run
  `tsc -b` and the full Vitest suite before committing the JSON. Keep the pruning script out of the repo.
- 784 keys are not identifier-shaped (English-sentence keys such as `"Cost owner"`), a handful are referenced; leave them.
- Key order: the old exporter sorted with `localeCompare`. Keep both files sorted the same way to keep merges and diffs sane.
- Seeded files are Prettier-clean when written with `JSON.stringify(obj, null, 2) + "\n"` (verified:
  `prettier --check --ignore-path /dev/null` passes on the current generated JSON), so `format:check` can cover them.

### Pitfall 13: tests that will break by design, and test scaffolding
Assertions on the old copy/behaviour that must be rewritten with the change:
`tests/hosting/actions.test.tsx` (label "Terminate now", `toBeDisabled()`), `tests/hosting/fleet.test.tsx`
("Jetzt deprovisionieren", `title` attribute reason, `toBeDisabled()`), `tests/hosting/workspace-page.test.tsx`
(Start fixture `dangerous: true`, `confirmationChallenge: "START"`, `costImpact: "None"` which is not a valid
`ECostImpact`, "Cancel" at lines ~293), `tests/profile-menu.test.tsx` (`stop-cost` id, navigation test, `toBeDisabled()`),
`tests/e2e/public-shell.spec.ts` (fixture `status: 3` numeric, `dangerous: true`).
Scaffolding: `vitest.config.ts` aliases `@fluentui/react-icons` to `tests/mocks/react-icons.tsx`, an explicit list of 56
named exports. Any new icon import (`CircleOff16Regular`, `Money16Regular`, ...) is `undefined` in tests unless added
there. `workspaceTabIds` is module-private in `ServerWorkspacePage.tsx:49`; export it for the matrix test.
`ProfileMenu` tests render it alone, so they need the new provider wrapper.

### Pitfall 14: contrast guard details
`--utf-*` tokens alias legacy variables (`--utf-text: var(--ink)`), and the targets live in the `:root` block of
`styles.css` (`:1457-1504`), a different file. The test must parse both files and resolve `var()` chains. The 12%
state tints are alpha colours: composite them over each of the three surfaces before measuring. Reproduced margins:
danger 5.49 / 5.04 / **4.58**, info 6.31 / 5.75 / 5.21, success 5.94 minimum, warning 6.68 minimum, neutral 5.87
minimum; `--utf-text-subtle` 5.34 on soft; `--utf-border-control` 3.86 on soft [VERIFIED: computed this session].
Danger on soft passes 4.5 by 0.08, so do not lighten the tint above 12%.

### Pitfall 15: `?action=` effect correctness
The deep-link effect must run once per distinct param value, be idempotent under StrictMode, wait for `server.data`
(UI-SPEC: nothing action-related renders while loading), never render action hints for a forbidden or missing server,
match ids case-insensitively but look up copy with `Map`/`hasOwn`, and render an unknown id as text truncated to
40 characters (never in an `href`, never HTML).

### Pitfall 16: React Query key prefixes
`hostingQueryKeys.fleet = ["hosting","servers"]` is a prefix of `server(id)`, `jobs(id)`, `operations(id)` and every
file/config key. `invalidateQueries({queryKey: fleet})` already invalidates all of them. The UI-SPEC list (fleet, server,
jobs, operations, notifications) is correct but partly redundant; notifications is the only independent key. Share one
`useFleetQuery(enabled)` hook (same `queryKey`, `queryFn`, and 10 s `refetchInterval`) between `FleetPages`,
`ProfileMenu` and the overview, so observers do not diverge in options.

## Code Examples

### `deriveServerState` shape (pure core)
```typescript
// Source: UI-SPEC "State rules, first match wins" and "Cost rules"; invariant per FND-03.
export interface ServerStateInput {
  hasDeployment: boolean;
  status: string | null | undefined;
  possibleCost: boolean;
  pendingJobCount: number;
}
export interface DerivedServerState {
  state: ServerState;
  cost: ServerCost;
  tone: "neutral" | "info" | "success" | "warning" | "danger";
  labelKey: TranslationKey;
  detailKey?: TranslationKey;      // Hosting_DeploymentStatus_* as secondary text only
}
// deriveServerState(server | undefined, options?: { notification?: { possibleCost: boolean } })
//   - server present      -> core(fromServer(server))
//   - server undefined    -> core({ hasDeployment: false, status: undefined,
//                                    possibleCost: notification?.possibleCost ?? false, pendingJobCount: 0 })
//                            and state "unknown" (never "off") when only a notification exists
```
Exhaustive invariant test (no fast-check):
```typescript
const statuses = Object.keys(statusClass) as EDeploymentStatus[];
for (const status of [...statuses, "SomethingNew"])
  for (const possibleCost of [true, false])
    for (const pendingJobCount of [0, 1])
      for (const hasDeployment of [true, false]) {
        const result = core({ hasDeployment, status: hasDeployment ? status : undefined, possibleCost, pendingJobCount });
        if (possibleCost) { expect(result.state).not.toBe("off"); expect(result.cost).not.toBe("none"); }
        if (result.state === "off") expect(result.cost).toBe("none");
      }
```

### `check-locales.mjs` contract
```javascript
// Pure node. CLI: node scripts/check-locales.mjs [--en <path>] [--de <path>] [--allow <path>]
// Defaults: src/locales/en.json, de.json, identical-allowed.json (relative to the script).
// Exit 1 and print the key lists on: German key missing, German value empty/whitespace,
// German key without English key, placeholder-set mismatch, key containing "." or ":".
// Exit 0 with warnings on identical EN/DE values that are not allow-listed (D-10; fails in Phase 69).
const placeholders = (text) =>
  [...text.matchAll(/\{\{\s*([A-Za-z0-9_]+)\s*\}\}|\{(\d+)(?::[A-Za-z0-9]+)?\}/g)]
    .map((m) => m[1] ?? m[2]).sort().join(",");   // {0:N0} -> 0, {{name}} -> name
```
Test it by `spawnSync(process.execPath, [script, "--en", f1, "--de", f2])` over temp fixtures. Wire as:
`"locales:check": "node scripts/check-locales.mjs"`, `"generate": "npm run contracts:generate"`,
`"build": "npm run locales:check && npm run generate && tsc -b && vite build"`,
`"build:assets": "npm run locales:check && npm run generate && vite build"`,
`"ci": "npm run locales:check && npm run contracts:check && ..."` (existing `ci` already starts with `locales:check`).
Playwright's `webServer` command runs `npm run build:assets`, so e2e fails on a locale gap too.

### Provider consumption
```tsx
// Entry points never execute; they request.
const { request } = useCostConfirmation();
<ActionButton variant="gold" onClick={() => request({ serverId, actionId: "start", source: "workspace-primary" })}>
```
```tsx
// Deep link (workspace page), once per distinct param, after server.data exists
useEffect(() => {
  if (!server.data || !requestedActionId) return;
  const action = findActionIgnoreCase(server.data.actions, requestedActionId);
  if (action && action.enabled && !action.pending)
    request({ serverId, actionId: action.id, source: "deep-link" });
}, [server.data, requestedActionId]);   // else DeepLinkActionNotice renders
```

### Axe e2e (real Chromium)
```typescript
// tests/e2e/hosting-a11y.spec.ts; mock users/me, game-servers, notifications, server, jobs, operations
// like tests/e2e/hosting-placement.spec.ts (page.route("**/api/**") throws on anything unmocked).
import AxeBuilder from "@axe-core/playwright";
await page.goto(`command/servers/${serverId}?action=start`);
await page.locator("dialog[open]").waitFor();
const scan = await new AxeBuilder({ page })
  .include("dialog[open]")
  .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
  .analyze();
expect(scan.violations).toEqual([]);
```
Run per dialog state (idle, error via a failing `POST .../actions/start`, challenge), in `en` and `de`
(`localStorage.UtfCulture`), and for the deep-link notice. Chromium evaluates colour contrast with real alpha blending,
which jsdom cannot.

### ACC-01 controller-level test (C#, follows AGENTS.md documentation rules)
```csharp
/// <summary>Verifies that the game-server list endpoint returns exactly the servers a member may view.</summary>
public sealed class GameServerListAccessTests
{
    /// <summary>A holder of <c>sv</c> or <c>sv:view</c> bound to one server receives that server only.</summary>
    /// <param name="permission">The resource-scoped claim type granted for the first server.</param>
    /// <returns>A task completing after the endpoint response is verified.</returns>
    [Theory]
    [InlineData(Claims.ResourceBased.Server.All)]
    [InlineData(Claims.ResourceBased.Server.View)]
    public async Task GetAllAsync_ReturnsOnlyServersTheClaimIsBoundTo(string permission)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var first = await HostingTestData.SeedServerAsync(store.DbContext);
        var second = await HostingTestData.SeedServerAsync(store.DbContext);
        var controller = new GameServersController(
            HostingTestData.CreateLifecycleService(store.DbContext),
            HostingTestData.CreateProjectionService(store.DbContext),
            new NoOpGameServerAccessService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = HostingTestData.ServerPrincipal(first.User, first.Server, permission),
                },
            },
        };

        var ok = Assert.IsType<OkObjectResult>(await controller.GetAllAsync(CancellationToken.None));
        var servers = Assert.IsAssignableFrom<IReadOnlyCollection<GameServerDto>>(ok.Value);

        Assert.Equal([first.Server.PrimaryKey], servers.Select(q => q.PrimaryKey).ToArray());
    }
}
```
Add the cases from Validation Architecture (excluded `sv:operate`-only, other-server claim, no claims,
`sv:view` actions disabled with reason, `sv` actions enabled, notifications `CanOperate`). Verify `SeedServerAsync` can be
called twice against one store; if a unique constraint objects, add the second `GameServer` to the first owner.

## Verified in-repo values (quoted verbatim; examples above use only these or new names)

```csharp
// sources/HostingContracts/EDeploymentStatus.cs:8-19
Provisioning = 0, WaitingForWorker = 1, Preparing = 2, Running = 3, Stopping = 4, PreservationFailed = 5,
Terminating = 6, Stopped = 7, TerminatingDueToFault = 8, TerminatedDueToFault = 9, Failed = 10, Terminated = 11,
// sources/HostingContracts/ECostImpact.cs:8-12
NoCostImpact = 0, StartsCost = 1, StopsCost = 2, KeepsCostRunning = 3, Unknown = 4,
// sources/Api/Services/GameServerLifecycle/GameServerActions.cs:10-16
Start = "start"; Stop = "stop"; Terminate = "terminate"; Restart = "restart"; Install = "install"; Update = "update"; CollectLogs = "collect-logs";
// sources/Api/Services/GameServerLifecycle/HostingControlPlaneState.cs:13
PausedReason = "Hosting lifecycle execution is paused by the operator."
// sources/Claims/Claims.cs:91-92 (ResourceBased.Server)
All = "sv"; View = "sv:view"; Operate = "sv:operate";
```
```text
sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs:1006-1017 reason strings, verbatim:
  "Server Operate permission is required."
  "Another lifecycle operation is pending."
  "Server launch is disabled."   (custom: server.LaunchDisabledReason ?? "Server launch is disabled.")
  "This action is unavailable in the current deployment state."
sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs:972-973 (Terminate)
  UnavailableReason = !canOperate ? "Server Operate permission is required." : terminating ? "Termination is already in progress." : null
sources/ReactApp/src/generated/api.ts:6814  ECostImpact: "NoCostImpact" | "StartsCost" | "StopsCost" | "KeepsCostRunning" | "Unknown";
sources/ReactApp/src/generated/api.ts:6820  EDeploymentStatus: "Provisioning" | "WaitingForWorker" | "Preparing" | "Running" | "Stopping" | "PreservationFailed" | "Terminating" | "Stopped" | "TerminatingDueToFault" | "TerminatedDueToFault" | "Failed" | "Terminated";
sources/ReactApp/src/features/hosting/services.ts:109-111  fleet: ["hosting", "servers"] as const,  notifications: ["hosting", "notifications"] as const,  capabilities: ...
sources/ReactApp/src/features/hosting/authorization.ts:26  accessServers: createServers || hasClaim(user, "sv", "sv:view"),
```
The key-name list `Server_State_*` etc. in this document are proposals (new names), not in-repo values; no `Server_*` key
exists today [VERIFIED: key scan returned `[]`].

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| RESX exported to JSON at build (`locales:generate` + git-diff drift check) | Hand-edited JSON + pure-node parity check | this phase | CI no longer needs a git baseline for locales; the Blazor RESX is decoupled |
| Per-component `status` comparison and `statusLabel` rendering | One pure `deriveServerState` | this phase | removes the "Stopped next to a costing server" class of bug |
| Uncontrolled confirm dialog with generic Cancel | Outcome-specific dismiss, controlled challenge, initial focus on dismiss | this phase | accidental Enter can no longer start or stop cost |
| `disabled` buttons with `title` reasons | `aria-disabled` + visible `aria-describedby` reason | this phase | reasons reach keyboard and touch users |

**Deprecated/outdated:** `ActionConfirmation`, `ServerStatus`, `serverTone`, `Hosting_Servers_TerminateNow` ("Deprovision now"),
`src/generated/locales/*`, `scripts/export-locales.mjs`, the RESX paragraph in `docs/features/react-ui-beta.md`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The Mozaic `react-tests` source transfer does not include git-ignored `.planning/` content | Pitfall 1 | If it did, the locked D-08 path would work in CI; the recommended tracked copy is still harmless |
| A2 | Hetzner hourly prices are on the order of a cent or less per hour, so the 2-digit rounding bug is user-visible | Pitfall 11 | If prices were higher the bug would be cosmetic; the formatter fix is cheap either way |
| A3 | i18next 26 plural keys (`_one`/`_other`) type-check through `t("Key", { count })` with the `typeof en` resource type | Pattern 4, Locale | A Wave 0 `tsc` spike with one plural key proves or disproves it; fallback is two literal keys chosen in code |
| A4 | `@axe-core/playwright@4.13.0` runs cleanly against `@playwright/test@1.62.1` Chromium (peer range is `playwright-core >= 1.0.0`) | Standard Stack | If incompatible, bump to 4.14.0 and pin `axe-core` through npm `overrides` to 4.14.0 |
| A5 | Chrome's single-cancel-per-activation behaviour also applies to Playwright's Chromium build used in e2e | Pitfall 3 | Only affects whether the re-open handler is observable in e2e; the unit test covers the handler |
| A6 | `HostingTestData.SeedServerAsync` can be called twice in one store without a uniqueness failure | ACC-01 example | Minor: add the second server to the first owner instead |

## Open Questions

1. **Parity matrix location (conflicts with locked D-08).**
   - What we know: `.planning/` has zero tracked files; CI can only be relied on to have `sources/`.
   - What's unclear: whether the user wants the matrix tracked in the repo.
   - Recommendation: keep D-08's file as a pointer and make `sources/ReactApp/docs/server-parity-matrix.md` canonical,
     added to `.prettierignore`. Needs one confirmation from the user before execution; do not silently change a locked decision.
2. **Profile-menu cost-stop: in-place dialog or navigate-then-deep-link?**
   - What we know: D-03 says "convert"; API data means the current code always navigated (Pitfall 9).
   - Recommendation: in-place dialog via the layout provider (consistent with "never executes directly", works from the
     community layout). Rewrite the navigation test.
3. **`primaryServerAction` prefers `terminate` over graceful `stop` while cost may run.**
   - Recommendation: prefer an enabled non-dangerous `StopsCost` action, fall back to the dangerous one only when `stop`
     is unavailable (for example a failed deployment). Update `workspace-model.test.ts` ("prioritizes a dangerous
     cost-stop action when provider cost remains"). In scope because the workspace primary action is converted here.
4. **`ECostImpact.Unknown` is not covered by the UI-SPEC.** Recommendation: treat as uncertain and show the cost row
   with a `Server_Confirm_CostUnknown` fallback rather than hiding it; do not treat it as `NoCostImpact`.
5. **Workspace "header" status.** Today there is none. Recommendation: add an additive `meta` slot to `PageHeader` and
   render `ServerStateBadge` + `CostIndicator` there; Phase 63 supersedes it.
6. **Overview dashboard attention list** also renders `statusLabel` (`FleetPages.tsx:64`). Recommendation: convert it in
   this phase (it has the fleet data) and record it in the parity matrix under "Fleet and entry points".

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node / npm | all React work | yes | v25.5.0 / 11.8.0 local; `engines` `>=24 <25`, `.node-version` 24, `.npmrc` `engine-strict=false` | CI pins Node 24; local run emits harmless `--localstorage-file` warnings |
| .NET SDK | `Api.Tests` | yes | 10.0.201 (matches `global.json`) | -- |
| dotnet-ef | not needed (no migration this phase) | yes | 10.0.5 | -- |
| Playwright Chromium | e2e axe | yes | several `chromium-12xx` builds under `~/AppData/Local/ms-playwright` | CI installs via `npx --no-install playwright install chromium` |
| npm registry access | installing `@axe-core/playwright` | yes | queried this session | CI allow-list already includes `registry.npmjs.org` |
| Real Hetzner / Steam | -- | n/a | tests must not call them (docs) | fake providers |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** none.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 (jsdom 28.0.0, `globals: true`, `css: true`); Playwright 1.62.1; xUnit 2.9.3 for `Api.Tests` |
| Config file | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts`, `tests/Api.Tests/Api.Tests.csproj` |
| Quick run command | `cd sources/ReactApp && npx vitest run <files>` (about 20 s for the hosting + profile + primitives set) |
| Full suite command | `cd sources/ReactApp && npm run ci`; `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerHosting` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FND-01 | every `--utf-*` fg/bg pair >= 4.5 (text) or 3.0 (control border, focus ring); alias + alpha resolution | unit | `npx vitest run tests/styles/tokens-contrast.test.ts` | Wave 0 |
| FND-01 | `components/server/*.css` has no raw hex or px spacing outside declared exceptions | unit (fs read) | `npx vitest run tests/styles/server-style-guard.test.ts` | Wave 0 |
| FND-02 | same server renders the same state text on card, workspace header, notification row | component | `npx vitest run tests/hosting/server-state-surfaces.test.tsx` | Wave 0 |
| FND-02 | notification joined by `serverId` to the fleet cache; missing server gives `unknown`; `statusLabel` never in DOM | component | `npx vitest run tests/profile-menu.test.tsx` | rewrite |
| FND-03 | exhaustive status x cost x pending x deployment invariant | unit | `npx vitest run tests/hosting/server-state.test.ts` | Wave 0 |
| FND-04 | dialog states (idle focus on dismiss, pending, error, success), variants, challenge gating, estimate/owner fallbacks, `Assigned owner` mapping, DE copy | component | `npx vitest run tests/hosting/cost-confirm-dialog.test.tsx` | Wave 0 |
| FND-04 | every entry point opens the dialog and never calls `executeAction` first | component | `npx vitest run tests/hosting/actions.test.tsx tests/hosting/fleet.test.tsx tests/hosting/workspace-page.test.tsx tests/profile-menu.test.tsx` | rewrite |
| FND-05 | `?action=start|stop|terminate` opens; disabled/pending/unknown id gives notice; forbidden/missing server gives no hint; param stripped on close | component | `npx vitest run tests/hosting/deep-link.test.tsx` | Wave 0 |
| FND-07 | every `WorkspaceTab` id and every `Object.keys(hostingService)` entry has a matrix row; no empty status; `relocated` has a location | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | Wave 0 |
| L10N-01 | no import of `generated/locales`; `export-locales.mjs` gone; `i18n.ts` uses `@/locales` | unit (fs) | `npx vitest run tests/locales/ownership.test.ts` | Wave 0 |
| L10N-02 | checker exits 1 on missing/empty/orphan/placeholder/dotted key, 0 on clean; warns on identical | unit (spawn CLI) | `npx vitest run tests/locales/check-locales.test.ts` | Wave 0 |
| L10N-02 | real `en.json` / `de.json` pass | CLI | `npm run locales:check` | Wave 0 |
| ACC-01 | list endpoint returns exactly the bound servers for `sv` and `sv:view`; excludes `sv:operate`-only, other-server and no-claim principals | xUnit | `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerListAccessTests` | Wave 0 |
| ACC-01 | nav entry, fleet boundary and query gating for `sv`-only and `sv:view`-only users | component | `npx vitest run tests/hosting/member-access.test.tsx` | Wave 0 |
| ACC-03 | `ActionButton unavailableReason`: `aria-disabled`, focusable, described, click is a no-op, submit blocked | component | `npx vitest run tests/shared-controls.test.tsx tests/hosting/unavailable-reason.test.tsx` | extend + Wave 0 |
| ACC-03 | six known reasons + fallback, EN and DE, `lang="en"` on unknown text | component | same file | Wave 0 |
| a11y gate | zero axe violations (WCAG 2.1 AA) for dialog states and notice, EN and DE, 320x568 no horizontal scroll | e2e | `npm run test:e2e -- tests/e2e/hosting-a11y.spec.ts` | Wave 0 |

### Sampling Rate
- **Per task commit:** `npx vitest run` on the touched test files plus `npx tsc -b` (typed `t()` makes tsc the fastest key check).
- **Per wave merge:** `npm test` (full Vitest) and `npm run lint`.
- **Phase gate:** `npm run ci` green, `dotnet test tests/Api.Tests --filter FullyQualifiedName~GameServerHosting` green, the axe e2e spec green, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `tests/styles/tokens-contrast.test.ts`, `tests/styles/server-style-guard.test.ts`, shared `tests/helpers/` (contrast math, CSS var resolver)
- [ ] `tests/hosting/{server-state,server-state-surfaces,cost-confirm-dialog,deep-link,unavailable-reason,member-access,parity-matrix}.test.ts(x)`
- [ ] `tests/locales/{check-locales,ownership}.test.ts` with fixture JSON
- [ ] `tests/e2e/hosting-a11y.spec.ts`
- [ ] `tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs`
- [ ] Add new icons to `tests/mocks/react-icons.tsx`
- [ ] Export `workspaceTabIds` from `ServerWorkspacePage.tsx`
- [ ] Shared test render helper wrapping `QueryClientProvider`, `MemoryRouter`/`createMemoryRouter`, `I18nextProvider`, `AppToastProvider`, `CostConfirmationProvider`
- [ ] A3 spike: one plural key through typed `t()`
- [ ] Framework install: `npm install --save-dev --save-exact @axe-core/playwright@4.13.0`

## Security Domain

`security_enforcement` is enabled (absent or true in `.planning/config.json`; ASVS level 1, block on high).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (unchanged) | existing cookie session via `AuthProvider` |
| V3 Session Management | no (unchanged) | existing |
| V4 Access Control | yes | API is the authority (`HasServerPermission`, `[Authorize(...View)]`); UI gating is cosmetic; ACC-01 test asserts the API response |
| V5 Input Validation | yes | `?action=` is untrusted: `Map`/`Object.hasOwn` lookups, text-node rendering, 40-char truncation, `encodeURIComponent` in generated links; no `dangerouslySetInnerHTML` |
| V6 Cryptography | no | none introduced |
| V7 Error handling / logging | yes | request errors show localized copy, never raw API prose or stack; no secrets rendered or logged |
| V14 Configuration / supply chain | yes | one pinned dev dependency (`@axe-core/playwright@4.13.0`), lockfile committed, `.npmrc` has `audit=false` so review the lockfile diff by hand |

### Known Threat Patterns for React + i18next + API-projected UI

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Prototype-key lookup via `?action=constructor` | Tampering | `Map` / `Object.hasOwn` tables (verified that `({a:1})["constructor"]` is a function) |
| Information disclosure via deep link (action hints for forbidden server) | Information disclosure | render action UI only after `server.data` exists; forbidden/missing shows the existing panel only |
| Interpolated API text into translated strings (`escapeValue:false` in `i18n.ts`) | Tampering (XSS) | only ever render `t()` output as React text children; never into `dangerouslySetInnerHTML`; API text goes in a `lang="en"` span |
| Client-only confirmation treated as authorization | Elevation of privilege | keep sending `confirmation` to the API; the dialog is a UX guard, API validates |
| Cost-owner identity leakage | Information disclosure | show the API's `"Assigned owner"` placeholder as a localized phrase; never try to infer the owner |
| Stale action availability submitted from a dialog | Tampering / integrity | live `useQuery` while open and re-check `enabled && !pending` at confirm |
| Supply-chain on new dev dependency | Tampering | exact pin, seam verdict recorded, lockfile review |

## Sources

### Primary (HIGH confidence)
- Repository files read this session: `AGENTS.md`; `61-CONTEXT.md`, `61-UI-SPEC.md`, `REQUIREMENTS.md`, `STATE.md`;
  `sources/ReactApp/{package.json,vitest.config.ts,vite.config.ts,playwright.config.ts,eslint.config.js,tsconfig.app.json,.npmrc,.prettierignore}`,
  `scripts/export-locales.mjs`, `src/app/{i18n,localizationTypes,App,format}.ts(x)`, `src/i18next.d.ts`,
  `src/components/{ActionControl,primitives,ProfileMenu,AppToasts,CommandCenterLayout}.tsx`, `src/auth/claims.ts`,
  `src/features/hosting/{components,model,services,authorization,workspaceModel,copy,FleetPages,ServerWorkspacePage,types,routes}.ts(x)`,
  `src/styles.css` (`:root` block and `.dialog`), tests listed under Pitfall 13;
  `sources/Api/Services/GameServerLifecycle/{HostingAuthorizationService,GameServerProjectionService,GameServerActions,HostingControlPlaneState}.cs`,
  `sources/Api/Controllers/GameServersController.cs`, `sources/Api/Authorization/Requirements/ServerIdRequirement.cs`,
  `sources/Api/Services/GameServerDefinition/*Registration.cs`, `sources/HostingContracts/{EDeploymentStatus,ECostImpact}.cs`,
  `sources/Claims/Claims.cs`, `tests/Api.Tests/GameServerHosting/{HostingTestData,HostingTestStore,GameServerProjectionServiceTests,GameServerAccessControllerAuthorizationTests}.cs`,
  `.mozaic/{pipeline.yml,check-ordinary.sh,build.sh}`, `.github/workflows/dotnet.yml`, `docs/features/react-ui-beta.md`.
- npm registry (`npm view`) for `@axe-core/playwright`, `axe-core`, `jest-axe`, `vitest-axe`; gsd `package-legitimacy check` seam.
- Local probes: axe-core 4.12.1 + jsdom 28.0.0 dialog probe; contrast computation; `Intl` currency and plural probes; key-reference scan.
- [Playwright accessibility testing](https://playwright.dev/docs/accessibility-testing) - package name, `include()`, `withTags()`, `waitFor()` guidance.

### Secondary (MEDIUM confidence)
- [MDN CloseWatcher cancel event](https://developer.mozilla.org/en-US/docs/Web/API/CloseWatcher/cancel_event) - single cancel per user activation; limited availability.
- [Chrome dialog guidance](https://developer.chrome.com/docs/modern-web-guidance/guides/ui-components/dialog.md.txt) - do not rely on `preventDefault()` for repeated Escape (surfaced via web search, not fetched in full).

### Tertiary (LOW confidence)
- None relied on; Hetzner price magnitude (A2) and i18next plural typing (A3) are in the Assumptions Log.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new runtime libraries; the one dev dependency was registry-verified and probed in the relevant failure mode.
- Architecture: HIGH for tier ownership and provider placement (router/provider order read from `App.tsx`); MEDIUM for the exact provider API until the Wave 0 spike.
- Pitfalls: HIGH, nearly all reproduced by running code or quoted from the files.
- ACC-01: HIGH for API behaviour by reading; the proof test itself is not yet written or run.

**Research date:** 2026-11-18
**Valid until:** 2026-12-18 (stable repo; re-check axe-core pin and the Mozaic transfer assumption if the pipeline changes)
