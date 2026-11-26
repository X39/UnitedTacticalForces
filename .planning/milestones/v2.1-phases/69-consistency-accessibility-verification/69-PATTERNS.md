# Phase 69: Consistency, Accessibility & Verification - Pattern Map

**Mapped:** 2026-11-22
**Files analyzed:** 40 new/modified file groups (plus about 70 family conversion files grouped by family)
**Analogs found:** 36 / 40

All paths are relative to `sources/ReactApp/` unless they start with `.planning/` or `scripts/` at the repo root.
Every named analog was checked as git-tracked source. Phase 68 automation components (`AutomationPanel`,
`AutomationStatusList`, `ScheduleRuleFields`, `ScheduledStartConsentDialog`, `AutoStopNotice`,
`tests/e2e/support/automationMocks.ts`, `tests/e2e/hosting-automation.spec.ts`, `tests/uat/automation.spec.ts`) are
NOT on disk yet; reference them by these expected paths and resolve them by role and accessible name (F11).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/components/StateViews.tsx` (NEW) | component | request-response (render state) | `src/features/hosting/components.tsx` (HostingLoading/Error/AccessBoundary, lines 24-71) + `StatePanel` in `src/components/primitives.tsx` 66-125 | exact |
| `src/components/statusTones.ts` (NEW) | utility | transform (Map lookup) | `src/features/hosting/serverState.ts` `stateTones` lines 110-119 | exact |
| `src/components/PageStack.tsx` (NEW) | component | render | `StatusBadge` in `primitives.tsx` 127-138 | role-match |
| `src/components/shared-primitives.css` (NEW) | config (CSS) | — | `src/components/server/*.css` (style-guard clean) | exact |
| `src/components/primitives.tsx` (MOD: PageHeader, StatePanel, StatusBadge, ConfirmDialog shell) | component | render | itself | self |
| `src/components/Disclosure.tsx` (MOD: button disclosure) | component | event-driven | itself (keep `DisclosureProps`, lines 5-23) | self |
| `src/components/FormFields.tsx` / `server/form-fields.css` (MOD: select chevron) | component/CSS | — | `SelectField` lines 272-284 | self |
| `src/styles/tokens.css` (MOD: `--utf-measure`, `--quiet` value) | config | — | existing `--utf-focus-ring` line 121 | self |
| `src/features/hosting/copy.ts` (DELETE) + `components.tsx` (MOD) | — | — | ownership test `tests/locales/ownership.test.ts` 30-33 | exact (deletion guard) |
| `src/app/urlParameters.ts` (NEW) | config | static contract | `src/app/routes.ts` (`routeManifest`, `matchRoute` line 45) + `workspaceTabIds` in `features/hosting/workspaceSections.ts:4` | role-match |
| `eslint.config.js` (MOD: control + literal rules) | config | — | itself (flat config, 48 lines) | self |
| `tests/ratchets/*.json` (NEW, one per family) | config | — | none | no analog (use RESEARCH Pattern 3) |
| `tests/ratchets/controls.test.ts`, `tests/styles/legacy-ratchet.test.ts` (NEW) | test | batch scan | `tests/styles/server-style-guard.test.ts` + `tests/helpers/cssGuard.ts` | exact |
| `tests/locales/hard-coded-text.test.ts` (NEW) | test | batch scan | `tests/hosting/change-source-scan.test.ts` | role-match |
| `tests/app/url-parameters.test.ts`, `tests/app/route-links.test.ts` (NEW) | test | batch scan | `tests/routes.test.ts` (imports `matchRoute`) + `change-source-scan.test.ts` | role-match |
| `tests/hosting/screen-registry.test.ts` (NEW) | test | guard | `tests/hosting/parity-matrix.test.ts` (imports `workspaceTabIds`) + `tests/hosting/routes.test.ts` | exact |
| `scripts/check-locales.mjs` (MOD: identical -> failure, `--unused`) | utility (Node CLI) | batch | itself lines 56-100 | self |
| `tests/locales/check-locales.test.ts`, `server-keys.test.ts`, `ownership.test.ts` (MOD) | test | — | themselves | self |
| `tests/hosting/parity-matrix.test.ts` (MOD) + `61-PARITY-MATRIX.md` (MOD) | test/doc | — | itself (`phase66CapabilityIds` guard pattern) | self |
| `tests/styles/tokens-contrast.test.ts` (MOD) | test | — | itself + `tests/helpers/cssTokens.ts` | self |
| `tests/disclosure.test.tsx` + e2e `summary`/`details` lines (MOD) | test | — | themselves | self |
| `playwright.config.ts` (MOD: projects, snapshots) | config | — | itself + `playwright.uat.config.ts` doc-comment style | self |
| `package.json` (MOD: scripts) | config | — | itself `scripts` block | self |
| `tests/e2e/support/mockApi.ts` (NEW) | test utility | request-response mock | `tests/e2e/hosting-a11y.spec.ts` `mockApi` lines 117-187 | exact |
| `tests/e2e/support/a11y.ts` (NEW: scanAxe, walkKeyboard, checkReflow, checkTextSpacing) | test utility | — | `hosting-a11y.spec.ts` `scan` 189-199 and 320px test 263-288 | exact |
| `tests/e2e/support/germanLeak.ts` (NEW) + unit test `tests/e2e-support/german-leak.test.ts` | test utility | transform | RESEARCH code example; locale reading in `scripts/check-locales.mjs` | partial |
| `tests/e2e/support/visual.ts` (NEW: stabilize) | test utility | — | `settleLabelFont` in `tests/e2e/hosting-workspace.spec.ts:305-312` | role-match |
| `tests/e2e/support/serverScreens.ts` (NEW registry) + fixture/mocks (files tree, finance, profiles) | test utility | — | `tests/e2e/support/costMocks.ts`, `metricsMocks.ts`; fixtures in `tests/helpers/*Fixtures.ts` | role-match |
| `tests/e2e/audit/{a11y-audit,german-walk,route-walk}.spec.ts` (NEW) | test (e2e) | — | `tests/e2e/hosting-a11y.spec.ts` 201-290 (culture loop) | exact |
| `tests/e2e/journeys/j1..j4*.spec.ts` (NEW) | test (e2e) | — | `tests/e2e/hosting-create.spec.ts`, `hosting-change.spec.ts` | exact |
| `tests/e2e/mobile/visual.spec.ts` (NEW, `@visual`) | test (e2e) | — | none (`toHaveScreenshot` unused today) | no analog |
| `tests/e2e/fonts/*` + `MANIFEST.json` + manifest test (NEW) | asset/test | file-I/O | none | no analog |
| `scripts/Update-VisualBaselines.ps1` (NEW, repo root `scripts/`) | utility | batch | `scripts/Start-Uat.ps1` | role-match |
| `tests/uat/journeys.spec.ts` (NEW) + `tests/uat/support/uat.ts` (MOD: localhost guard) | test (seeded) | — | `tests/uat/cost-alerts.spec.ts`, `tests/uat/support/uat.ts` | exact |
| Shell/nav: `CommandCenterLayout.tsx`, `CommunityLayout.tsx`, `ProfileMenu.tsx`, `commandNavigation.tsx`, `en.json`/`de.json` values | component | render | `ActionControl.tsx` usages; `SelectField` | role-match |
| Hosting Tier A family (`FleetPages`, `CreateServerPage`, `ServerWorkspacePage`, `ServerFilesPanel`, `ConfigurationProfile*`, finance, `CostAlertsPage`, `ServerRuntimePanel`, `CloudMachinePanel`, `ServerLogsPanel`, `HostingAuditFeed`, `CostConfirmDialog`) | component/page | request-response | `components.tsx` `ServerActions` (ActionButton usage) | exact |
| Admin Tier B families (authority, members, events, content/media, field reports, mod packs/terrains, Verein, settings) | page | CRUD | `RoleDetailPage.tsx` (PageHeader + early-return to fix) | role-match |
| Public Tier C (`pages/*`, `WikiPage`, `PublicHero`) | page | render | `components/PublicHero.tsx` | self |

## Pattern Assignments

### `src/components/StateViews.tsx` (component, NEW)

**Analog:** `src/features/hosting/components.tsx` lines 1-71 (to be deleted after migration) over `StatePanel` (`primitives.tsx` 90-125).

Imports pattern (components.tsx 1-4):
```typescript
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
```

AccessBoundary pattern (components.tsx 24-39) - copy, swap keys to `Common_State_ForbiddenTitle/Body` with optional override props:
```typescript
export function HostingAccessBoundary({ allowed, children }: { allowed: boolean; children: ReactNode }) {
  const { t } = useTranslation();
  return allowed ? (
    children
  ) : (
    <StatePanel kind="forbidden" title={t("Server_Access_ForbiddenTitle")}>
      {t("Server_Access_ForbiddenBody")}
    </StatePanel>
  );
}
```

Error-with-retry pattern (components.tsx 52-71) - becomes `ErrorState` with `Common_State_LoadErrorTitle`, `Common_State_LoadErrorBody`, `Common_State_Retry`:
```typescript
export function HostingError({ onRetry, message }: { onRetry(): void; message?: string }) {
  const { t } = useTranslation();
  return (
    <StatePanel
      kind="error"
      title={t("Server_Access_LoadError")}
      action={<ActionButton variant="ghost" onClick={onRetry}>{t("Server_Access_Retry")}</ActionButton>}
    >
      {message ?? null}
    </StatePanel>
  );
}
```
Doc-comment convention: JSDoc with `@param props.x` lines (as at components.tsx 15-23, primitives.tsx 78-89).
Optional props must be typed `?: T | undefined` (exactOptionalPropertyTypes, F16; see `StatusBadge.className` primitives.tsx 133).

Callers to migrate (18 usages): `FleetPages.tsx` 17,118-134,283,424-435; `ServerWorkspacePage.tsx` 16,189-264; `CreateServerPage.tsx` 26,572. Fleet keeps its specific `Server_Access_*` keys via override props (UI-SPEC copy table).

---

### `src/components/statusTones.ts` (utility, NEW)

**Analog:** `src/features/hosting/serverState.ts` lines 110-119 (typed closed map) and line 161 (lookup with fallback):
```typescript
const stateTones = {
  off: "neutral",
  starting: "info",
  running: "success",
  ...
  unknown: "neutral",
} as const satisfies Record<ServerState, ServerStateTone>;
```
Use `Map<string, StatusTone>` per domain plus `toneOf(map, key)` returning `"neutral"` for unknown (UI-SPEC D-05).
Tone type must equal `StatusBadge` `tone` union (`primitives.tsx` 133: `"neutral" | "success" | "warning" | "danger" | "info"`) - export it from one place.

Page-local functions to replace (F7), example `features/admin/MembersPage.tsx` 52-55:
```typescript
function statusTone(member: CommunityMember): "danger" | "success" | "warning" {
  const status = memberStatus(member);
  return status === "verified" ? "success" : status === "unverified" ? "warning" : "danger";
}
```
Others: `EventSlotPlanner.statusTone`, `FieldReportsPage.stateTone`, `MemberDetailPage.statusTone/.membershipTone`, `SettingsPage.settingsFeatureTone`, `VereinMembershipRequestsPage.statusTones`, `HomePage.operationTone/.attendanceTone`, `UpcomingEventsPage.attendanceTone`.

---

### `src/components/primitives.tsx` (MOD) and `PageStack.tsx` (NEW)

PageHeader today (primitives.tsx 36-64) - keep props, only CSS moves to Display 28/700 in `shared-primitives.css`:
```typescript
<header className="page-header">
  <div>
    {kicker && <span className="kicker">{kicker}</span>}
    <Heading tabIndex={-1}>{title}</Heading>
    {description && <p>{description}</p>}
    {meta && <div className="page-header__meta">{meta}</div>}
  </div>
  {actions && <div className="page-header__actions">{actions}</div>}
</header>
```
StatePanel role/live-region contract (primitives.tsx 103-124) stays: `role={kind === "error" ? "alert" : "status"}`, icon `aria-hidden`.
ConfirmDialog (primitives.tsx 513-568): native `<dialog>` + `showModal()`, `initialFocus === "dismiss"` focuses `[data-dialog-dismiss]`; actions row at 459-505 (`dialog__actions`, dismiss then confirm). D-10 geometry changes are CSS only; the challenge `<input>` at ~443 should become `TextField`. `PageStack` copies the tiny-wrapper shape of `StatusBadge` (127-138: className join, `children: ReactNode`).

---

### `src/components/Disclosure.tsx` (MOD)

Keep `DisclosureProps` (lines 5-23) and ids (`useId` + `-title/-summary/-content`, lines 42-45). Replace lines 46-70 (`<details>/<summary>`) with `<button type="button" aria-expanded={open} aria-controls={contentId} onClick={() => onOpenChange(!open)}>` and `<div role="region" id={contentId} aria-labelledby={titleId} hidden={!open}>`. Keep `ChevronDown20Regular aria-hidden` and class names `disclosure__summary|__title|__chevron|__summary-line|__content` so existing CSS keeps working. Update in same plan: `tests/disclosure.test.tsx`, `tests/e2e/hosting-change.spec.ts:732`, `hosting-create.spec.ts:383-386,455`, `event-authoring.spec.ts:258-259`.

---

### `src/features/hosting/ServerFilesPanel.tsx` row menu (D-AUTO-2)

Current (lines 872-915): `<details className="next-hosting-file-menu"><summary aria-label=...>` + `div[role=menu]` with raw `<button role="menuitem">` and `<a role="menuitem" download>`. Replace with inline compact controls in the `<td>`: `ActionButton` (Open, Delete) and `ActionAnchor` (Download, keeps `href={hostingService.fileDownloadUrl(...)}` and `download`). Drop `role="menu"`/`menuitem`. Delete label/handler (`setDeleteTarget(entry)`, `disabled={deleteMutation.isPending}`) unchanged. `ActionButton`/`ActionAnchor` signatures: `ActionControl.tsx` 104, 158 (`variant`, `size`, `iconOnly` needs `aria-label`).

---

### Routed pages with early-return states (D-AUTO-3)

**Anti-pattern to fix, example** `features/admin/RoleDetailPage.tsx` 174-185:
```typescript
if (!authorized)
  return (
    <StatePanel kind="forbidden" title={t("Pages_Command_Roles_AccessRequiredTitle")}>
      {t("Pages_Command_Roles_AccessRequiredDescription")}
    </StatePanel>
  );
```
Target: render `<PageHeader kicker=... title={routeLabel} />` then `<ForbiddenState />` / `<EmptyState>` (not-found) inside the same `<section>` used at line 188 (`authority-page role-detail-page`). Same rule for loading states.

---

### `src/app/urlParameters.ts` (NEW) + `tests/app/url-parameters.test.ts`, `tests/app/route-links.test.ts`

Analog: `src/app/routes.ts` exports (`routeManifest` 19, `matchRoute(pathname)` 45). Typed constant keyed by route id, values reuse existing constants (`workspaceTabIds` from `features/hosting/workspaceSections.ts:4`, fleet filters in `fleetModel.ts`, action ids as listed in `parity-matrix.test.ts` `requiredActionIds`).
Test import pattern (`tests/routes.test.ts` 1-12):
```typescript
import { readdirSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { matchRoute, /* ... */ } from "@/app/routes";
```
Scan only URL-state readers (`useSearchParams`, `window.location.search`, `filesLocation.ts`, `fleetModel.ts`, `CostConfirmationProvider.tsx`, `workspaceSections.ts`); exclude API `new URLSearchParams({take})` (F9).

---

### Static source-scan tests (`hard-coded-text`, ratchets, legacy CSS ratchet)

**Analog A:** `tests/hosting/change-source-scan.test.ts` 1-50 - `sourceRoot = resolve(import.meta.dirname, "..", "..", "src")`, list of target files, `stripComments`, regex `findViolations` returning `name: match` strings, `expect(found).toEqual([])`.
**Analog B (CSS):** `tests/styles/server-style-guard.test.ts` + `tests/helpers/cssGuard.ts`:
```typescript
const serverDirectory = resolve(import.meta.dirname, "..", "..", "src", "components", "server");
function serverStylesheets(): string[] {
  return readdirSync(serverDirectory).filter((name) => name.endsWith(".css"));
}
it("covers at least one real stylesheet", () => { expect(serverStylesheets().length).toBeGreaterThan(0); });
it.each(serverStylesheets())("keeps %s free of raw colours and pixel literals", (name) => {
  const css = readFileSync(join(serverDirectory, name), "utf8");
  expect(findStyleViolations(css)).toEqual([]);
});
```
Extend: add `src/components/shared-primitives.css` to the scanned set; add an `outline: none|0` check to `findStyleViolations` (or a sibling function) with unit cases like the existing `it("flags a raw hex colour")` style. Legacy ratchet counts per file for every other `src/**/*.css` (F3, F15); compute baseline at implementation time.
Ratchet JSON: one file per family under `tests/ratchets/` (RESEARCH Pattern 3; no analog exists). The control count may be computed by running ESLint's Node API with the selectors from RESEARCH "ESLint ratchet selectors".

---

### `eslint.config.js` (MOD)

Current flat config (48 lines): global ignores, `js.configs.recommended`, node globals block, the `**/*.{ts,tsx}` block with `tsparser` + `projectService: true`, and a tests globals block. Append one block `{ files: ["src/**/*.tsx"], ignores: [primitive files, ...ratchet files read from tests/ratchets/*.json], rules: { "no-restricted-syntax": ["error", ...] } }`. Merge all `no-restricted-syntax` selectors for one file group into one block (later blocks replace the rule). Selectors verbatim in RESEARCH Code Examples. Note `tests/**/*.spec.ts` is not covered by the tests globals block today (only `*.test.*`).

---

### `scripts/check-locales.mjs` (MOD)

Change line 76-78 from `warnings.push("[identical-warning] ...")` to `fail("identical", key, ...)`. Add `--unused` to `readOptions` (lines 9-23 parse flags in pairs; a boolean flag needs a separate branch). Keep output format `[category] key - detail` and the final summary line (93-99). Test analog: `tests/locales/check-locales.test.ts` (add failing sample); script wiring asserted by `tests/locales/ownership.test.ts` 43-50 (`scripts["locales:check"]`, `build`, `build:assets`, `ci`) - extend for `locales:unused` in `ci` only.

---

### `tests/hosting/screen-registry.test.ts` (NEW) and `parity-matrix.test.ts` (MOD)

Analog `tests/hosting/parity-matrix.test.ts` 1-60: imports app constants (`workspaceTabIds`, `hostingService`) into Vitest and asserts completeness against a fixed list; reads `.planning/...61-PARITY-MATRIX.md` via `resolve(dirname(fileURLToPath(import.meta.url)), "../../../..", path)` (comment explains why not `new URL`). For D-19 add a rule "no row has status `pending`" and a Phase 69 evidence guard modeled on `phase66CapabilityIds`. Registry guard imports `hostingRouteDefinitions` (see `tests/hosting/routes.test.ts`) and `workspaceTabIds`; it may import `tests/e2e/support/serverScreens.ts` only if that file has no `@playwright/test` runtime import at module top (keep the registry metadata in a pure module, setup functions referencing `Page` by type only).

---

### `tests/e2e/support/mockApi.ts` (NEW)

**Analog:** `tests/e2e/hosting-a11y.spec.ts` 112-187:
```typescript
async function mockApi(page: Page, culture: Culture, server: object, options = {}): Promise<string[]> {
  const unexpected: string[] = [];
  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), culture);
  await page.route("**/appsettings.json", (route) =>
    route.fulfill({ json: { Api: { BaseUrl: "/api/" }, Urls: { TeamSpeak3: "", Discord: "" },
      Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" } } }));
  await page.route("**/Insignia-48px.png", (route) => route.fulfill({ status: 200, contentType: "image/png", body: "" }));
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === "/api/users/me") return route.fulfill({ json: { /* claims */ } });
    ...
    if (await fulfillMetricsRoute(route, request.method(), path, serverId)) return;
    if (await fulfillCostRoute(route, request.method(), path, serverId)) return;
    unexpected.push(`${request.method()} ${path}`);
    return route.abort();
  });
  return unexpected;
}
```
Generalize to `(page, culture, handlers: Handler[])` where a handler has the `fulfillCostRoute` signature (`tests/e2e/support/costMocks.ts` 17-30: `(route, method, path, ...) => Promise<boolean>`). Fixture builders importable from `tests/helpers/fleetFixtures.ts`, `workspaceFixtures.ts`, `createFixtures.ts`, `costAlertFixtures.ts`, `configurationFixtures.ts`, `metricsHistoryFixtures.ts` (type-only imports, safe in Playwright). Never import one `*.spec.ts` from another. Phase 68 `automationMocks.ts` joins as a handler when merged. Do not edit existing specs.

---

### `tests/e2e/support/a11y.ts` (NEW)

Axe scan with non-vacuity (hosting-a11y.spec.ts 189-199):
```typescript
async function scan(page: Page, selector: string) {
  const results = await new AxeBuilder({ page }).include(selector).withTags(wcagTags).analyze();
  // A scan that evaluated no rule at all would pass vacuously, so require evidence that rules ran.
  expect(results.passes.length).toBeGreaterThan(0);
  return results.violations.map((violation) => ({
    id: violation.id,
    nodes: violation.nodes.map((node) => node.target.join(" ")),
  }));
}
```
Tags become `["wcag2a","wcag2aa","wcag21a","wcag21aa","wcag22aa"]`. Reflow metric (263-287): `document.documentElement.scrollWidth <= innerWidth`, `boundingBox()` inside viewport. Dialog focus assertion: `dialog.locator("[data-dialog-dismiss]")` toBeFocused (213).

---

### `tests/e2e/support/visual.ts` (NEW)

Analog `tests/e2e/hosting-workspace.spec.ts` 305-312 (font settle) and its annotation fallback 328-334:
```typescript
async function settleLabelFont(page: Page): Promise<boolean> {
  return page.evaluate(async () => {
    await document.fonts.ready;
    return [...document.fonts].some((face) => face.family.includes("Barlow Condensed") && face.status === "loaded");
  });
}
```
`stabilize` adds `page.route` for `fonts.googleapis.com` / vendored woff2, `emulateMedia({ reducedMotion: "reduce", colorScheme: "dark" })`, `page.clock.setFixedTime(...)` before `goto` (RESEARCH shape). Pixel tests: `test.skip(process.env.UTF_VISUAL_BASELINES !== "1", ...)` (D-AUTO-1).

---

### Audit, journey and mobile specs (`tests/e2e/audit|journeys|mobile/*.spec.ts`)

Analog structure `hosting-a11y.spec.ts` 201-290: `for (const culture of ["en","de"] as const) { test.describe(\`... (${culture})\`, () => { test(..., async ({ page }) => { const unexpected = await mockApi(...); await page.goto("command/servers/..."); ...; expect(unexpected).toEqual([]); }) }) }`. Relative `goto` against `baseURL` `http://127.0.0.1:4173/next/`. Registry-driven: iterate `serverScreens` instead of hand-written tests; group states per (screen, culture, project) in one test. Journeys copy flows from `hosting-create.spec.ts` (J-1) and `hosting-change.spec.ts` (J-2); fleet push frames from `tests/e2e/support/fleetStream.ts`.

---

### `playwright.config.ts` (MOD)

Current: single project `{ name: "chromium", use: { ...devices["Desktop Chrome"] } }`, two `webServer` entries (classic stub 4174, `build:assets && preview` 4173), `retries: CI ? 2 : 0`. Add `snapshotPathTemplate`, `expect.toHaveScreenshot` defaults, `chromium` with `testIgnore` for `audit|journeys|mobile`, a desktop audit project (`testMatch`), `mobile-390`, `mobile-320` (RESEARCH Code Examples). Do not change CI timeouts/workers (D-AUTO-1). Keep webServer block untouched.

---

### `tests/uat/journeys.spec.ts` (NEW) + `tests/uat/support/uat.ts` (MOD)

Helpers to reuse from `tests/uat/support/uat.ts`: `Culture`, `useCulture(page, culture)` (init script `UtfCulture`), `uatTitles` (seeded rows: `running: "UAT 2 Running"` etc.), `writeHandOver/readHandOver` for `@fresh` -> `@restarted` state. Tag tests `@fresh`/`@restarted` (config `grep` in `playwright.uat.config.ts`). Add a module-level guard that throws unless `new URL(process.env.UAT_BASE_URL ?? default).hostname` is `localhost` or `127.0.0.1` (F17). Create-then-start asserts the waiting-for-worker stage only (D-AUTO-6); Running is `Deferred follow-up:`. Extend (do not duplicate) Phase 68 `tests/uat/automation.spec.ts` with the German pass.

---

### Hosting/admin/public family conversions (Wave 1)

Control target pattern: `components.tsx` `ServerActions` 103-136 - `ActionButton variant={...} unavailableReason={...} onClick={...}`; labels via `t(...)`; API prose in `<ApiText>` (`lang="en"`, the German walk exclusion). Form controls: `TextField`/`SelectField`/`TextAreaField`/`CheckboxField` from `@/components/FormFields` (props: `label`, `hint`, `error`, `required`, `options` for select; React 19 `ref` as prop keeps `register(...)` working). Dialogs: `ConfirmDialog` with `cancelLabel` from a new `{Area}_{Dialog}_Dismiss` key. Run the matching `tests/admin-*.test.ts(x)` / `tests/hosting/*.test.tsx` in the same plan (Pitfall 4). New locale keys inserted in sorted, plan-prefixed positions.

## Shared Patterns

### Localization
**Source:** `useTranslation()` + `t("Key")` everywhere (e.g. `components.tsx` 33-36). Interpolation `t("React_ServerFiles_ActionsFor", { name })` (ServerFilesPanel 874). API text via `ApiText`. No literals in JSX (enforced by new ESLint selectors).
**Apply to:** every converted `.tsx`.

### Strict mock + unexpected-request assertion
**Source:** `hosting-a11y.spec.ts` 117-187; handlers `costMocks.ts`, `metricsMocks.ts`.
**Apply to:** all new e2e audit/journey/mobile specs (via `support/mockApi.ts`).

### Source-scan Vitest guard
**Source:** `tests/hosting/change-source-scan.test.ts`, `tests/styles/server-style-guard.test.ts`, `tests/locales/ownership.test.ts` (file-must-not-exist + no-importer check, lines 19-33).
**Apply to:** ratchets, hard-coded text, url-parameter/link scans, `copy.ts` deletion guard, tone guard ("no StatusBadge tone outside statusTones").

### Doc comments and typing
**Source:** JSDoc on every exported symbol (`Disclosure.tsx` 5-34, `costMocks.ts` 3-16, `playwright.uat.config.ts` 4-13); `?: T | undefined` for optional props; English only.
**Apply to:** all new TS files incl. test support (tests are type-checked, F16).

### Focus ring
**Source:** `--utf-focus-ring: 2px solid var(--gold-bright)` (`src/styles/tokens.css:121`), global `:focus-visible` (`styles.css:1578`).
**Apply to:** every `outline: none|0` fix (F3 list).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `tests/ratchets/*.json` + update script | config | batch | No ratchet mechanism exists; use RESEARCH Pattern 3 |
| `tests/e2e/mobile/visual.spec.ts` + `__screenshots__/` | test | — | No `toHaveScreenshot` usage in repo |
| `tests/e2e/fonts/` + `MANIFEST.json` + hash test | asset | file-I/O | No vendored assets with manifest exist |
| `tests/e2e/support/germanLeak.ts` | test utility | transform | New algorithm; use RESEARCH shape (drop `an`, `will`, F5) and unit-test it under `tests/` (Vitest excludes `tests/e2e/**`, so put the unit test outside e2e and import the pure module) |

## Metadata

**Analog search scope:** `sources/ReactApp/src/components`, `src/features/hosting`, `src/features/admin`, `src/app`, `tests/**`, `scripts/`, `eslint.config.js`, `playwright*.config.ts`, `package.json`
**Files scanned:** ~35
**Pattern extraction date:** 2026-11-22
