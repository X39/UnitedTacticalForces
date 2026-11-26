# Phase 61: Foundations, Localization & Member Access - Pattern Map

**Mapped:** 2026-11-18
**Files analyzed:** 36 (new or modified)
**Analogs found:** 33 / 36

All analog paths are git-tracked (`git ls-files` verified for the C# and React hosting files; `.planning/` is wholly
untracked, see Metadata). Paths are relative to `sources/ReactApp/` unless prefixed `tests/Api.Tests`.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/features/hosting/serverState.ts` | utility (pure model) | transform | `src/features/hosting/model.ts` (`deploymentStatusKey`, `serverTone`) + `workspaceModel.ts` | role-match |
| `src/features/hosting/actionVocabulary.ts` | utility (lookup tables) | transform | `src/features/hosting/model.ts` (`serverMessageKey`) | role-match (fix proto flaw) |
| `src/components/server/ServerStateBadge.tsx` | component | request-response (props) | `src/features/hosting/components.tsx` `ServerStatus` | exact (replaces) |
| `src/components/server/CostIndicator.tsx` | component | props | `ServerStatus` + `ToastIcon` in `AppToasts.tsx` | role-match |
| `src/components/server/CostConfirmDialog.tsx` | component | request-response | `ConfirmDialog` in `primitives.tsx:306-373` + `ActionConfirmation` | exact |
| `src/components/server/CostConfirmationProvider.tsx` | provider | event-driven | `AppToastProvider` in `AppToasts.tsx` | role-match |
| `src/components/server/useCostConfirmation.ts` | hook | event-driven | `useAppToast` in `AppToasts.tsx:145-149` | exact |
| `src/components/server/UnavailableReason.tsx` | component | transform | `ServerCard` reason mapping (`components.tsx:67-73`) | partial |
| `src/components/server/DeepLinkActionNotice.tsx` | component | props | `StatePanel` in `primitives.tsx` | role-match |
| `src/components/server/server.css` | config (style) | n/a | `src/features/hosting/hosting.css`, `src/components/ProfileMenu.css` | role-match |
| `src/styles/tokens.css` | config (style) | n/a | `:root` block `src/styles.css:1457-1504` | exact |
| `src/main.tsx` (modify) | config | n/a | itself (import order line 8-9) | exact |
| `src/components/ActionControl.tsx` (modify) | component | props | itself | exact |
| `src/components/primitives.tsx` (modify: `ConfirmDialog`, `PageHeader` meta slot) | component | props | itself | exact |
| `src/styles.css` (modify: `.button[aria-disabled]`, `.dialog--cost`) | config | n/a | `.button:disabled` at `:1989` | exact |
| `src/features/hosting/components.tsx` (modify/replace) | component | request-response | itself | exact |
| `src/features/hosting/FleetPages.tsx` (modify) | component | CRUD (React Query) | itself (`useFleet`, line 18-24) | exact |
| `src/features/hosting/ServerWorkspacePage.tsx` (modify) | component | request-response | itself | exact |
| `src/components/ProfileMenu.tsx` (modify) | component | event-driven | itself | exact |
| `src/components/CommunityLayout.tsx`, `CommandCenterLayout.tsx` (modify: mount provider) | layout | n/a | themselves | exact |
| `src/app/i18n.ts` (modify) | config | n/a | itself | exact |
| `src/app/localizationTypes.ts`, `src/i18next.d.ts` (modify) | config (types) | n/a | themselves | exact |
| `src/locales/en.json`, `de.json` (new) | locale data | n/a | `src/generated/locales/en.json` (seed) | exact |
| `src/locales/identical-allowed.json` (new) | config | n/a | none | no analog |
| `scripts/check-locales.mjs` (new) | utility script | batch / file-I/O | `scripts/export-locales.mjs` | role-match |
| `scripts/export-locales.mjs`, `src/generated/locales/*` (delete) | n/a | n/a | n/a | n/a |
| `package.json`, `.prettierignore` (modify) | config | n/a | themselves | exact |
| `docs/server-parity-matrix.md` (new, canonical; see Open Question 1) | doc | n/a | none | no analog |
| `tests/hosting/server-state.test.ts` | test | transform | `tests/hosting/authorization.test.ts`, `workspace-model.test.ts` | role-match |
| `tests/hosting/cost-confirm-dialog.test.tsx`, `deep-link.test.tsx`, `unavailable-reason.test.tsx`, `server-state-surfaces.test.tsx`, `member-access.test.tsx` | test (component) | request-response | `tests/hosting/actions.test.tsx`, `tests/profile-menu.test.tsx` | exact |
| `tests/hosting/parity-matrix.test.ts`, `tests/locales/*.test.ts`, `tests/styles/*.test.ts` | test (fs / spawn) | file-I/O | none in repo (use `node:fs`, `spawnSync`) | partial |
| `tests/e2e/hosting-a11y.spec.ts` | test (e2e) | request-response | `tests/e2e/hosting-placement.spec.ts` | exact |
| `tests/mocks/react-icons.tsx` (modify) | test mock | n/a | itself | exact |
| `tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs` | test (C#) | request-response | `GameServerAccessControllerAuthorizationTests.cs` + `HostingTestData.cs` | exact |

## Pattern Assignments

### `src/components/server/CostConfirmDialog.tsx` (component, request-response)

**Analog:** `src/components/primitives.tsx` `ConfirmDialog` (lines 306-373) and `src/features/hosting/components.tsx`
`ActionConfirmation` (lines 143-181).

**Native dialog pattern to keep** (primitives.tsx 325-341):
```tsx
const dialogRef = useRef<HTMLDialogElement>(null);
const challengeRef = useRef<HTMLInputElement>(null);
useEffect(() => {
  if (challengeRef.current) challengeRef.current.value = "";
  if (open && !dialogRef.current?.open) dialogRef.current?.showModal();
  if (!open && dialogRef.current?.open) dialogRef.current.close();
}, [open]);
return (
  <dialog ref={dialogRef} className="dialog"
    onCancel={(event) => { event.preventDefault(); onCancel(); }}>
```
**Gaps the phase must close (additive props `confirmVariant`, `children`, `initialFocus`, `busy`, `error`):**
confirm button is hard-coded `variant="danger"` (line 366), challenge is an uncontrolled `<input ref>` compared only on
submit (line 346, 359), no initial focus (React `autoFocus` does nothing on a closed dialog: call `.focus()` on the
dismiss button after `showModal()`). Add a `close` event handler that re-calls `showModal()` while pending/error
(Research Pitfall 3). Use a `.dialog--cost` modifier class, do not edit `.dialog`.

**Description assembly to replace** (components.tsx 157-161):
```tsx
const description = action
  ? [action.confirmationText, action.description, action.costDescription].filter(Boolean).join(" ")
  : "";
```
**Inline error pattern** (components.tsx 174-178): `<p className="next-hosting-inline-error" role="alert">{error}</p>`
(keep `role="alert"`; render inside the dialog, stay open on error).

**Do not copy:** `cancelLabel={c.cancel}` (generic Cancel; D-04 requires `Server_Action_{ActionId}_Dismiss`), raw
`<button className="button ...">` (components.tsx 38; use `ActionButton`).

---

### `src/components/server/CostConfirmationProvider.tsx` + `useCostConfirmation.ts` (provider/hook, event-driven)

**Analog:** `src/components/AppToasts.tsx`.

**Imports pattern** (lines 7-18):
```tsx
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate } from "react-router";
```
**Context + throwing hook** (lines 40, 145-149):
```tsx
const AppToastContext = createContext<AppToastContextValue | null>(null);
export function useAppToast(): AppToastContextValue {
  const context = useContext(AppToastContext);
  if (!context) throw new Error("useAppToast must be used inside AppToastProvider.");
  return context;
}
```
**Memoized value** (line 96): `const value = useMemo(() => ({ showToast }), [showToast]);` then
`<Ctx.Provider value={value}>{children}<Dialog/></Ctx.Provider>`.
**Success feedback:** `useAppToast().showToast(message, "success")`.
**Route param strip with replace** (`useAppRouteToast`, lines 169-199): `navigate({pathname, search, hash}, { replace: true })`.
For `?action=` use `useSearchParams` and `setSearchParams(next, { replace: true })` keeping other params.
**Query keys for invalidation / live server** (`src/features/hosting/services.ts:108-117`):
```ts
export const hostingQueryKeys = {
  fleet: ["hosting", "servers"] as const,
  notifications: ["hosting", "notifications"] as const,
  server: (serverId: string) => ["hosting", "servers", serverId] as const,
  jobs: (serverId: string) => ["hosting", "servers", serverId, "jobs"] as const,
  operations: (serverId: string) => ["hosting", "servers", serverId, "operations"] as const,
```
Mount inside `CommunityLayout` and `CommandCenterLayout` (router children; `AppToastProvider` is outside the router).

---

### `src/components/server/ServerStateBadge.tsx` / `CostIndicator.tsx` (component, props)

**Analog:** `ServerStatus` (components.tsx 48-63) and `ToastIcon` (AppToasts.tsx 42-47).
```tsx
export function ServerStatus({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const status = server.deployment?.status;
  const key = deploymentStatusKey(status);
  return (
    <StatusBadge tone={serverTone(server)}>
      {status ? (key ? t(key) : humanizeToken(status)) : t("Hosting_Servers_NoDeployment")}
    </StatusBadge>
  );
}
```
```tsx
function ToastIcon({ intent }) {
  if (intent === "success") return <CheckmarkCircle20Regular aria-hidden="true" />;
  ...
```
Replace the status comparison with `deriveServerState(server)`; icons always `aria-hidden="true"` next to visible text.
Every new icon import must also be exported from `tests/mocks/react-icons.tsx` (vitest alias), e.g.
`export const CircleOff16Regular = Icon;`.

---

### `src/features/hosting/serverState.ts` and `actionVocabulary.ts` (utility, transform)

**Analog:** `src/features/hosting/model.ts` (`deploymentStatusKey`, `serverMessageKey`) and `components.tsx:48-52`
(`serverTone`, the logic being replaced):
```ts
export function serverTone(server: GameServer): "neutral" | "warning" | "info" {
  if (server.deployment?.possibleCost) return "warning";
  if (server.pendingJobCount > 0) return "info";
  return "neutral";
}
```
Type source for statuses: `import type { components } from "@/generated/api";` and
`components["schemas"]["EDeploymentStatus"]` (generated, `api.ts:6820`). Use RESEARCH Pattern 1 (`satisfies Record<...>`,
`Object.hasOwn`) and Pattern 4 (literal-key tables + `Map`). Do NOT copy the plain-object property lookup in
`deploymentStatusKey` / `serverMessageKey` (prototype-key flaw, RESEARCH Pattern 4).
Enum rule: React uses string-literal unions; no TS `enum` (AGENTS.md `E` prefix would apply if introduced).

---

### `src/components/ActionControl.tsx` (modify: `unavailableReason`)

**Analog:** itself. Insert into `ActionButton` (lines 81-97):
```tsx
export function ActionButton({ variant = "default", size = "default", fullWidth = false,
  iconOnly = false, type = "button", className, ...buttonProps }: ActionButtonProps) {
  return (<button {...buttonProps} type={type}
    className={actionControlClassName({ variant, size, fullWidth, iconOnly, className })} />);
}
```
Props type is `Omit<ComponentPropsWithoutRef<"button">, ...> & ActionControlVisualProps & ActionControlContentProps`
(lines 27-32); add `unavailableReason?: string | null | undefined` (tsconfig has `exactOptionalPropertyTypes`).
When set: `aria-disabled="true"`, `aria-describedby={useId()}`, `event.preventDefault()` in onClick (submit would otherwise
fire). `ActionLink` (lines 99-113) cannot be disabled; disabled call sites use `ActionButton`.
CSS: extend `src/styles.css:1989` `.button:disabled { opacity: 0.55; cursor: not-allowed; }` with
`.button[aria-disabled="true"]`.

---

### `src/features/hosting/components.tsx` / `FleetPages.tsx` (modify)

**Analog:** `FleetPages.tsx:18-24` shared fleet query (share as `useFleetQuery(enabled)` with ProfileMenu/overview):
```tsx
function useFleet(enabled: boolean) {
  return useQuery({
    queryKey: hostingQueryKeys.fleet,
    queryFn: ({ signal }) => hostingService.servers(signal),
    refetchInterval: 10_000,
    enabled,
  });
}
```
**Access gate** (components.tsx 15-24; D-09 wraps the fleet page in it, passing `getHostingPermissions(user).accessServers`):
```tsx
export function HostingAccessBoundary({ allowed, children }: { allowed: boolean; children: ReactNode }) {
  const c = useHostingCopy();
  return allowed ? children : <StatePanel kind="forbidden">{c.forbidden}</StatePanel>;
}
```
Pass `enabled: permissions.accessServers` so a claimless user issues no query.
**Terminate link on card** (components.tsx 114-133) is the fork to convert: enabled branch is a `<Link ...?action=terminate>`
(keep navigation per D-03), disabled branch is `<button disabled title=...>` and becomes
`<ActionButton unavailableReason=...>`.
Status render sites to convert (grep gate): `components.tsx:54-63`, `ServerWorkspacePage.tsx` ~198-200, ~426, ~495-498,
`ProfileMenu.tsx:404`, `FleetPages.tsx:64` (`statusLabel`).
**Deep-link gate to change:** `ServerWorkspacePage.tsx:637` `Boolean(candidate.confirmationChallenge)` (why `?action=start`
never opens).

---

### `src/features/hosting/authorization.ts` (claims, read-only analog for ACC)

```ts
accessServers: createServers || hasClaim(user, "sv", "sv:view"),
```
Browser `hasClaim` ignores the claim value; API is authority. Tests: `tests/hosting/authorization.test.ts:1-40` builds
users with a local `userWithClaims(...claims)` helper (copy for `member-access.test.tsx`).

---

### `src/app/i18n.ts` (modify) and locale files

**Analog:** itself, lines 4-5 are the only imports to change:
```ts
import de from "@/generated/locales/de.json";
import en from "@/generated/locales/en.json";
```
becomes `@/locales/de.json` / `@/locales/en.json`. Keep `formatResource` (lines 38-54), `UtfCulture` key, `setCulture`.
Also update `src/app/localizationTypes.ts` and `src/i18next.d.ts` (same three importers). Seed per D-10 by a one-off script
kept out of the repo; write with `JSON.stringify(obj, null, 2) + "\n"` and `localeCompare` key sort (matches the
current exporter, export-locales.mjs 29-31 and 45-46). Flat keys, `_` separators, no `.` or `:`. New keys:
`Server_{Area}_{Item}`.

---

### `scripts/check-locales.mjs` (utility, batch / file-I/O)

**Analog:** `scripts/export-locales.mjs` (pure node ESM, no deps beyond prettier).
```js
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
```
End with `console.log(...)` summary; use `process.exitCode = 1` on failure. Placeholder normaliser regex from RESEARCH
("`check-locales.mjs` contract"). CLI flags `--en --de --allow` so tests can `spawnSync(process.execPath, [script, ...])`.
`package.json` current scripts to rewrite (lines 11-22): `generate` -> `npm run contracts:generate`; `locales:check` ->
`node scripts/check-locales.mjs`; `build`/`build:assets` prepend `npm run locales:check &&`; `ci` already starts with it.
`.prettierignore` drop `src/generated/locales/*.json`; add `docs/server-parity-matrix.md`.

---

### `src/styles/tokens.css` (config)

**Analog:** `src/styles.css:1457-1475` `:root` block (alias targets):
```css
:root {
  color-scheme: dark;
  --ink: #f4f1e8;  --muted: #aab2bf;  --quiet: #778190;
  --gold: #baa05a; --gold-bright: #dec982;
  --canvas: #090c11; --surface: #0e131a; --surface-raised: #141b25; --surface-soft: #1a2330;
  --stroke: #293442; --stroke-strong: #3b4858;
  --success: #66c49a; --warning: #e1b968; --danger: #e47777; --info: #79a9df;
```
Define `--utf-*` as `var(--ink)` style aliases plus 12% alpha tints. Import in `src/main.tsx` before styles:
```ts
import "highlight.js/styles/github-dark.css";
import "@/styles.css";
```
add `import "@/styles/tokens.css";` before `@/styles.css`. `server.css` uses tokens only (style-guard test scans it).

---

### `tests/hosting/*.test.tsx` (component tests)

**Analog 1:** `tests/hosting/actions.test.tsx:1-41`:
```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
...
describe("...", () => {
  beforeAll(async () => i18nReady);
  it("...", async () => {
    render(<I18nextProvider i18n={i18n}>...</I18nextProvider>);
    const user = userEvent.setup();
```
Fixture style: `const terminate = { id: "terminate", ..., dangerous: true, ... } as ServerAction;`. Correct Start fixtures to
`dangerous: false` (D-13). Rewrites required: labels "Terminate now" and `toBeDisabled()` (lines 32, 53) become
aria-disabled assertions (`toHaveAttribute("aria-disabled","true")`).

**Analog 2:** `tests/profile-menu.test.tsx:1-33` for auth mocking and routing:
```tsx
const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined, logout: vi.fn() }));
vi.mock("@/auth/auth", () => ({ useAuth: () => ({ user: authState.user, isAuthenticated: Boolean(authState.user),
  isLoading: false, providers: { steam: true, discord: true }, providersLoading: false,
  login: vi.fn(), logout: authState.logout }) }));
```
plus `QueryClient`, `createMemoryRouter`/`RouterProvider`. Create one shared render helper (tests/helpers/) wrapping
`QueryClientProvider`, router, `I18nextProvider`, `AppToastProvider`, `CostConfirmationProvider`. Use fixture claim
`id: "terminate"` (not `"stop-cost"`).
**Claim fixture shape** (profile-menu.test.tsx 50-71): `{ primaryKey, identifier: "sv:view", value: null, title, category, description, isPrefix, valueType }`.
**Pure model tests:** `tests/hosting/authorization.test.ts` (`userWithClaims` helper, `describe/it` with global `vi`, no imports for
vitest globals).

---

### `tests/e2e/hosting-a11y.spec.ts` (e2e)

**Analog:** `tests/e2e/hosting-placement.spec.ts:1-30`:
```ts
import { expect, test } from "@playwright/test";
const serverId = "10000000-0000-4000-8000-000000000001";
const server = { primaryKey: serverId, title: "Placement test server", gameIdentifier: "arma3", ... actions: [] };
```
`page.route("**/api/**")` throws on anything unmocked: mock users/me, game-servers, notifications, server, jobs,
operations. Add `import AxeBuilder from "@axe-core/playwright"` (exact pin 4.13.0). Do not copy `status: 3` numeric or
`dangerous: true` Start fixtures from `public-shell.spec.ts`.

---

### `tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs` (test, request-response)

**Analog:** `GameServerAccessControllerAuthorizationTests.cs` (header + style) and `HostingTestData.cs`.
**Namespace/usings** (lines 1-17): file-scoped `namespace Api.Tests.GameServerHosting;`, `public sealed class ...Tests`,
`using SecurityClaim = System.Security.Claims.Claim;`, `using Xunit;`, `using X39.UnitedTacticalForces;` (for `Claims`),
`using X39.UnitedTacticalForces.Api.Controllers;`, `...Api.DTO;`.
**Test shape** ([Theory] with `[InlineData(Claims.ResourceBased.Server.View)]`, lines 44-57); `Claims.ResourceBased.Server.All`
= `"sv"`, `.View` = `"sv:view"`, `.Operate` = `"sv:operate"`.
**Test helpers** (HostingTestData.cs, `internal static`):
```csharp
public static async Task<HostingTestSeed> SeedServerAsync(DatabaseContext dbContext,
    bool withActiveDeployment = false, EDeploymentStatus deploymentStatus = EDeploymentStatus.Running)
public static ClaimsPrincipal ServerPrincipal(User user, GameServer server, params string[] permissions)
    => Principal(user, permissions.Select(q => (q, server.PrimaryKey.ToString())).ToArray());
public static GameServerLifecycleService CreateLifecycleService(DatabaseContext dbContext)
public static GameServerProjectionService CreateProjectionService(DatabaseContext dbContext,
    WorkerProvisioningOptions? workerProvisioningOptions = null)
```
Store: `await using var store = await HostingTestStore.CreateAsync();` then `store.DbContext` (see
`GameServerProjectionServiceTests.cs`). Controller: `new GameServersController(lifecycle, projection, new NoOpGameServerAccessService()) { ControllerContext = ... }`
(constructor shape per RESEARCH example; verify against `GameServersController.cs`, `GetAllAsync(CancellationToken)` at line 28).
Principal built with `ServerPrincipal` (value = server id; an empty claim value matches nothing).
**AGENTS.md:** every type/member needs real XML docs (`<summary>`, `<param>`, `<returns>`), one type per file named after it,
`E` prefix for any enum. Assert: `sv`/`sv:view` return only the bound server; `sv:operate`-only, other-server and no-claim
principals do not; `sv:view` actions `Enabled == false` with reason "Server Operate permission is required."; `sv`
actions enabled (it is the all-permissions claim).

---

## Shared Patterns

### i18n usage in components
**Source:** `components.tsx`, `AppToasts.tsx`
```tsx
const { t } = useTranslation();
t("Hosting_Common_Runtime");
formatResource(t("Hosting_Servers_JobCount"), server.pendingJobCount)
```
`t()` is typed from `en.json`; build keys as literal-key tables, never template strings. Apply to all new components and tables.

### Import conventions
Path alias `@/` -> `src/`; type-only imports via `import type`; imports ordered third-party, then `@/...`. Named exports,
one component per file in `components/server/`. Prettier + `eslint --max-warnings 0`; `noUncheckedIndexedAccess` and
`exactOptionalPropertyTypes` are on (`...[1]!` non-null in tests, conditional-spread for optional props:
`{...(cond ? { challenge: x } : {})}`).

### Accessible status markup
Icons `aria-hidden="true"`; errors `role="alert"`; non-error toasts `role="status"` (AppToasts 117).

### Shared controls
Use `ActionButton`/`ActionLink` from `@/components/ActionControl`; `StatePanel`, `StatusBadge`, `PageHeader`, `ConfirmDialog`
from `@/components/primitives`. Never raw `<button className="button">` in new code (AGENTS.md UI Component Reuse).

### Authorization
API is authority (`HasServerPermission`); UI shows `enabled` / `unavailableReason` from the API and never recomputes.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/locales/identical-allowed.json` | config | n/a | New concept; shape `{ "<key>": "<reason>" }` from RESEARCH |
| `docs/server-parity-matrix.md` | doc | n/a | No existing parity doc; columns fixed by D-08 (`ID, Capability, Today, Status, New location, Phase, Evidence`) |
| `tests/styles/tokens-contrast.test.ts`, `server-style-guard.test.ts`, `tests/locales/*`, `tests/hosting/parity-matrix.test.ts` | test (fs) | file-I/O | No existing test reads CSS/JSON files or spawns a CLI; use `node:fs` and `spawnSync(process.execPath, ...)`; WCAG luminance math per RESEARCH Pitfall 14 |

## Metadata

**Analog search scope:** `sources/ReactApp/{src,tests,scripts}`, `tests/Api.Tests/GameServerHosting`, `sources/Api/Controllers`.
**Files scanned:** ~30 read or grep-targeted; ~150 tracked files listed.
**Notes for planner:**
- `.planning/` is entirely git-ignored (gitStatus `?? .planning/`); the D-08 matrix path is not readable by CI. Keep canonical
  matrix at `sources/ReactApp/docs/server-parity-matrix.md` (needs user confirmation, RESEARCH Open Question 1).
- `tests/mocks/react-icons.tsx` is an explicit export list; vitest aliases `@fluentui/react-icons` to it.
- `workspaceTabIds` is module-private in `ServerWorkspacePage.tsx`; export for the matrix test.
**Pattern extraction date:** 2026-11-18
