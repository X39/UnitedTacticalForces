# Phase 64: Guided Server Creation - Pattern Map

**Mapped:** 2026-11-20
**Files analyzed:** 22 new or modified (all under `sources/ReactApp`, plus planning docs)
**Analogs found:** 21 / 22
**Scope:** frontend only. No API, DTO, database, OpenAPI, Kiota or `sources/WebApp` change. All analog paths below were confirmed git-tracked (`git ls-files`) or live under tracked `sources/ReactApp/src` and `tests`.

Paths are relative to `sources/ReactApp/` unless stated.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/features/hosting/createServerModel.ts` (EXTEND) | utility (pure model) | transform | itself (lines 50-88) | exact |
| `src/features/hosting/CreateServerPage.tsx` (REWRITE) | component (page, form) | request-response + CRUD | itself (old, lines 270-575) | exact (replace) |
| `src/features/hosting/ServerWorkspacePage.tsx` (EDIT) | component (page) | event-driven (router state to dialog) | itself, deep-link effect lines 68-103 | exact |
| `src/components/server/useCostConfirmation.ts` (EDIT) | hook / type | request-response | itself lines 4-12 | exact |
| `src/components/FormFields.tsx` (NEW) | component (generic control) | request-response | `src/components/SearchField.tsx` | role-match |
| `src/components/Disclosure.tsx` (NEW) | component (generic control) | event-driven | `src/components/SearchField.tsx` | partial |
| `src/components/server/PresetField.tsx` (NEW) | component | transform | `FormFields` plus presets in model | partial |
| `src/components/server/SizeField.tsx` (NEW) | component | transform | old `RuntimeProfileSummary` (`CreateServerPage.tsx:94-133`) | role-match |
| `src/components/server/CostOwnerLine.tsx` (NEW) | component | transform | `src/components/server/DeepLinkActionNotice.tsx` | role-match |
| `src/components/server/AdvancedOptions.tsx` (NEW) | component | transform | old form fields (`CreateServerPage.tsx:639-700+`) | role-match |
| `src/components/server/StorageOptions.tsx` (NEW) | component | transform | old storage section of `CreateServerPage.tsx` | role-match |
| `src/components/server/StorageDemandDetails.tsx` (NEW) | component | transform | old `StorageDemandSummary` (`CreateServerPage.tsx:135-268`) | exact (restyle) |
| `src/components/server/AfterCreatingChoice.tsx` (NEW) | component | event-driven | `FormFields` radio plus `ActionControl` | partial |
| `src/components/server/CreateSummary.tsx` (NEW) | component | request-response | `ActionButton` with `unavailableReason` (`ActionControl.tsx:99-135`) | role-match |
| `src/components/server/create.css` (NEW) | config (style) | n/a | `src/components/server/fleet-toolbar.css` | exact |
| `src/styles/tokens.css` (EDIT) | config | n/a | existing `--utf-fleet-card-min` tokens (Phase 62 precedent) | exact |
| `src/locales/en.json`, `de.json` (EDIT) | config | n/a | existing `Server_*` keys | exact |
| `tests/hosting/create-server-page.test.tsx` (REWRITE) | test | request-response | itself (header lines 1-60) | exact |
| `tests/hosting/create-server.test.ts` (EXTEND) | test | transform | itself | exact |
| `tests/hosting/create-start-flow.test.tsx` (NEW) | test (integration) | event-driven | `tests/hosting/cost-confirmation-provider.test.tsx` | role-match |
| `tests/hosting/create-presets.test.ts`, `create-capability-driven.test.tsx`, `create-summary.test.tsx`, `tests/form-fields.test.tsx`, `tests/disclosure.test.tsx` (NEW) | test | transform | `tests/hosting/create-server-page.test.tsx` / `tests/hosting/create-server.test.ts` | role-match |
| `tests/e2e/hosting-create.spec.ts` (NEW) | test (e2e, axe) | request-response | `tests/e2e/hosting-a11y.spec.ts` | exact |
| `tests/locales/server-keys.test.ts` (EXTEND `isButtonLabelKey`), `tests/hosting/cost-confirm-model.test.ts` (EXTEND), `tests/hosting/parity-matrix.test.ts` + `61-PARITY-MATRIX.md` CRT rows (DATA) | test / data | transform | themselves | exact |

## Pattern Assignments

### `src/features/hosting/createServerModel.ts` (utility, transform) - add `resolveCreateDefaults`, `deriveCreateReadiness`, preset projection, money rule

**Analog:** the file itself. Add exports additively next to the existing pure functions. Keep the `import type { ... } from "@/features/hosting/types"` style and no React imports.

**Existing rules to reuse, not rewrite** (lines 50-88):
```typescript
export function isProviderAvailable(provider: RuntimeProvider): boolean {
  return (
    provider.availabilityStatus.trim().toLocaleLowerCase() === "available" &&
    provider.profiles.length > 0
  );
}
export function orderProviders(providers: RuntimeProvider[]): RuntimeProvider[] {
  return [...providers].sort((left, right) =>
    left.identifier.localeCompare(right.identifier, undefined, { sensitivity: "base" }),
  );
}
export function isCostOwnerSelectionValid(costOwnerId: string | null, owners: ServerCreationCostOwnerOption[] | undefined): boolean {
  if (!owners) return false;
  if (costOwnerId) {
    const selected = owners.find((owner) => owner.costOwnerId === costOwnerId);
    return selected !== undefined && !selected.isFrozen;
  }
  return owners.find((owner) => owner.isCurrentUser)?.isFrozen !== true;
}
export function combinedHourlyCost(profile, selectedStorageCapacityBytes, strategy, storage): number | undefined { /* lines 75-88 */ }
```
`isCostOwnerSelectionValid` returns `false` for loading, missing and frozen alike; `deriveCreateReadiness` must take query flags explicitly and call it only for the frozen/unknown decision.

**Default-resolution source to port into the pure function** (old effect, `CreateServerPage.tsx:349-386`, `:474-498`, `:500-520`): game = `capabilities[0]`, provider = `orderProviders(filter(isProviderAvailable))[0]`, strategy = `provider?.storage?.supportsExternalBlockStorage ? "AutomaticExternal" : "IncludedDisk"`, terrain/scenario chosen by `version.sourceIdentifier === capability.defaultTerrainSourceIdentifier`, else first:
```typescript
const selected =
  terrains.data.find((item) =>
    item.versions.some((version) => version.sourceIdentifier === capability.defaultTerrainSourceIdentifier),
  ) ?? terrains.data[0];
if (selected) form.setValue("terrainId", Number(selected.primaryKey), { shouldValidate: true });
```
Case-insensitive id matching uses `.toLocaleLowerCase()` on both sides everywhere; keep it.

**Money rule:** `formatHourlyRate(value)` and `formatMoney(value)` live in `src/features/hosting/model.ts` (lines 266-277). Monthly = `profile.estimatedMonthlyCostEur` for `IncludedDisk`, else hourly x 730 (old page used `euro(hourlyCost * 730, 2)`, `CreateServerPage.tsx:985`). Never return 0 for unknown.

---

### `src/features/hosting/CreateServerPage.tsx` (page, form + request-response) - REWRITE as composition

**Analog:** the current file. Keep these blocks verbatim in intent, move presentation out.

**Imports/form setup to keep** (lines 1-36, 270-320): `zodResolver`, `useForm`, `useWatch` (never `form.watch()`), `useMutation`, `useQuery`, `useQueryClient`, `useNavigate`, `useAuth`, `getHostingPermissions`, `useUnsavedChanges`, `hostingQueryKeys`, `hostingService`. Replace `useHostingCopy` with `useTranslation` and `Link` back-link with `Breadcrumbs`. Add `shouldFocusError: false` to `useForm` options (RESEARCH Pitfall 3) and `noValidate` on `<form>` (Pitfall 2).
```typescript
const form = useForm<ServerCreationFormValues>({ resolver: zodResolver(schema), defaultValues: defaults });
const [gameIdentifier, ...] = useWatch({ control: form.control, name: ["gameIdentifier", ...] });
useUnsavedChanges(form.formState.isDirty);
```

**Queries to keep unchanged** (lines 322-435): `capabilities` (`staleTime: 60_000`, `enabled: permissions.createServers`), `creationModPacks`, `terrains` (enabled only when `capability?.requiresTerrain === true`), `scenarios`, `profileOptions`, `storageEstimate`, `costOwners` keyed by `costOwnerQueryKey(user?.primaryKey ?? "anonymous")`. Change per RESEARCH Pitfall 4: enable `storageEstimate` on `gameIdentifier && provider` only (not on `profile`).

**Storage math to reuse** (lines 437-467): `isStorageSelectionValid`, `selectedStorageBytes`, `combinedHourlyCost`, `isCostOwnerSelectionValid`.

**Page-state ladder to keep** (lines 578-597): `authLoading` -> loading, `!permissions.createServers` -> `StatePanel kind="forbidden"`, `capabilities.isLoading` -> `StatePanel kind="loading"`, `isError` -> error with retry, empty -> `StatePanel kind="empty"`. Swap `HostingError message={errorMessage(...)}` for fixed keys (no `Error.message`; delete `errorMessage()`).

**Submit and post-create pattern to keep** (lines 541-561), extending for start-now:
```typescript
const submit = form.handleSubmit(async (values) => {
  if (!canSubmit || create.isPending) return;
  try {
    const server = await create.mutateAsync({
      ...values,
      title: values.title.trim(),
      externalStorageSizeGiB: values.runtimeStorageStrategy === "FixedExternal" ? values.externalStorageSizeGiB : null,
      configurationProfileRevisionId: null,
    });
    form.reset(values);
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet }),
      queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications }),
      queryClient.invalidateQueries({ queryKey: costOwnerQueryKey(user?.primaryKey ?? "") }),
    ]);
    navigate(`/command/servers/${server.primaryKey}`); // start-now: add { state: { createdStartPending: true } }
  } catch { /* render Server_Create_Error, form stays editable */ }
});
```
Keep the `reset` then awaited invalidations order before `navigate` (Pitfall 8). Pass `handleSubmit(onValid, onInvalid)` and focus the first invalid field in DOM order from an explicit field list, opening Advanced first (Pitfall 3).

**Deleted from page:** `euro`, `number`, `providerStatusKey` (move provider status mapping into `AdvancedOptions`), `RuntimeProfileSummary`, `StorageDemandSummary`.

---

### `src/features/hosting/ServerWorkspacePage.tsx` (page, event-driven) - consume `createdStartPending`

**Analog:** the deep-link effect and notice in the same file (lines 68-103, 177-195).

**Guarded one-shot request pattern** (lines 78-93):
```typescript
const handledDeepLink = useRef<string | null>(null);
useEffect(() => {
  if (requestedActionId === null) { handledDeepLink.current = null; return; }
  if (!server.data || !requestedAction || !requestedActionOpenable) return;
  const key = `${server.data.primaryKey}|${requestedAction.id.toLowerCase()}`;
  if (handledDeepLink.current === key) return;
  handledDeepLink.current = key;
  request({ serverId: server.data.primaryKey, actionId: requestedAction.id, source: "deep-link" });
}, [requestedActionId, requestedAction, requestedActionOpenable, server.data, request]);
```
Differences required for create: read `useLocation().state` (accept only `=== true` on `createdStartPending`), clear it with `navigate(pathname + search, { replace: true, state: null })`, wrap `request(...)` in `window.setTimeout(..., 0)` with cleanup (RESEARCH Pitfall 1: the provider captures `pathnameRef` in an effect that runs after children, so an immediate call closes the dialog; see `CostConfirmationProvider.tsx:127`, `:141-146`). Guard by "request was actually issued" so the StrictMode double effect still fires it. Pass `source: "create"` and `onSubmitted: clearNotice`. Use `actionIsUnavailable(action)` (already imported line 8) for the openable decision.

**Notice slot pattern** (lines 177-195): the `notices` prop of `WorkspaceShell` renders a node. Compose the new notice with `StatePanel kind="info"` plus a dismiss `ActionButton`, and `ActionUnavailableReason` for the mapped reason, copying `DeepLinkActionNotice`. Gate on `deriveServerState(server).state === "off"` from `serverState.ts`, never `deployment.status`.

---

### `src/components/server/useCostConfirmation.ts` (type, EDIT)

**Analog:** itself, lines 4-12. Add `| "create"` as the last union member:
```typescript
export type CostConfirmationSource =
  | "workspace-primary" | "workspace-action" | "workspace-header" | "workspace-failure"
  | "workspace-machine" | "fleet-card" | "notification" | "deep-link";
```
Extend `tests/hosting/cost-confirm-model.test.ts` (union enumeration near lines 160-162).

---

### `src/components/FormFields.tsx` and `src/components/Disclosure.tsx` (generic components)

**Analog:** `src/components/SearchField.tsx` (generic shared control, imports its CSS from `components/server`, `useId` for label wiring, documented props interface).

**Imports and structure pattern** (SearchField lines 1-5, 42-75):
```typescript
import { Dismiss20Regular, Search20Regular } from "@fluentui/react-icons";
import { useId, useRef, type KeyboardEvent, type RefObject } from "react";
import { ActionButton } from "@/components/ActionControl";
import "@/components/server/fleet-toolbar.css";   // new files import "@/components/server/create.css"

const inputId = useId();
<label className="sr-only" htmlFor={inputId}>{label}</label>
<input id={inputId} ... />
```
Rules to apply: export a documented `XxxProps` interface and a JSDoc block; wire `label htmlFor`, `aria-describedby` (hint then error ids), `aria-invalid`, error with `role="alert"`; own class names (`form-field__*`), do NOT reuse global `.field` (`styles.css:3917`). `Disclosure` is a controlled native `<details open={open} onToggle={(e) => setOpen(e.currentTarget.open)}>` with `ChevronDown20Regular` (jsdom toggle verified in RESEARCH). Any button inside uses `ActionButton`, never a raw `<button>` posing as a control (AGENTS.md UI reuse).

---

### `src/components/server/CreateSummary.tsx` (component, request-response)

**Analog:** `ActionButton` blocked pattern in `src/components/ActionControl.tsx` (lines 92-135). Submit is `<ActionButton type="submit" variant="gold" unavailableReason={reasonNode}>`.
```typescript
// aria-disabled, focusable, aria-describedby linked to the visible reason, onClick preventDefault (Enter cannot submit)
<button {...rest} type={type} className={classNames} aria-disabled="true"
  aria-describedby={describedBy ? `${describedBy} ${reasonId}` : reasonId}
  onClick={(event) => event.preventDefault()} />
<span id={reasonId} className="action-reason"><Info16Regular aria-hidden="true" />{unavailableReason}</span>
```
Pass `unavailableReason={undefined}` when ready (the `hasReason` check treats `undefined`/`""`/`false` as none). Pending state: `Spinner` from `@fluentui/react-components` plus `Server_Create_Submit_Pending`. Use `formatHourlyRate`/`formatMoney`; one polite hidden live region lives in the page, not here (no nested live regions). Reason keys map from the `CreateBlockReason` code returned by `deriveCreateReadiness`.

---

### `src/components/server/CostOwnerLine.tsx` (component)

**Analog:** `src/components/server/DeepLinkActionNotice.tsx` (lines 46-75): `useTranslation`, `StatePanel kind="warning"` with an `action` slot holding an `ActionButton variant="ghost"`, interpolated `t("Key", { name })`.
```typescript
<StatePanel kind="warning" action={dismiss}>
  {t("Server_DeepLink_Unavailable", { action: name })}{" "}
  <ActionUnavailableReason server={props.server} action={props.action} />
</StatePanel>
```
Frozen notice = `StatePanel kind="warning"` with `Server_Create_Owner_Frozen` and a compact ghost "Change cost owner" `ActionButton`. Per RESEARCH F5, when no `isCurrentUser` owner is returned, show the signed-in user's nickname as "Billed to".

---

### `src/components/server/StorageDemandDetails.tsx` (component) and `SizeField.tsx`, `StorageOptions.tsx`, `AdvancedOptions.tsx`

**Analog:** old page-local components and fields in `CreateServerPage.tsx`.
- `StorageDemandSummary` (lines 135-268): move as-is into `StorageDemandDetails.tsx`, keep `formatBytes`, `formatResource(t("Hosting_Create_SafetyMargin"), ...)` carried keys, the `selectedFit?.isSufficient` ladder, and the `<details>` breakdown (convert to nested `Disclosure`). Replace `next-hosting-*` classes with `create.css` classes and local `number()` with shared formatters.
- `RuntimeProfileSummary` (lines 94-133): facts `dl` (`Hosting_Profile_VirtualCpu`, `_Memory`, `_Disk`, `_HourlyEstimate`) becomes the size facts in `SizeField`. Show fit line only when strategy is `IncludedDisk` (RESEARCH Pitfall 5).
- Capability-driven fields (lines 639-700): terrain/scenario `select` with `capability.terrainLabel`/`scenarioLabel` and `required`; mod pack disabled while `isLoading || isError`; keep `capability?.supportsModPacks !== false` gating. Move labels rendered from API into `ApiText` (`src/components/server/ApiText.tsx`). Replace hard-coded "Select ..." with `Server_Create_Content_Choose`.
- No `arma3`, `aska`, `counterstrike2` literals anywhere in these files (source-scan test).

---

### `src/components/server/create.css` (style) and `src/styles/tokens.css`

**Analog:** `src/components/server/fleet-toolbar.css` (header comment lines 1-8, rules 11-59).
```css
.search-control {
  display: flex; gap: var(--utf-space-sm); min-height: var(--utf-control-height);
  border: 1px solid var(--utf-border-control); border-radius: var(--utf-radius);
  background: var(--utf-surface-raised); color: var(--utf-text);
}
.search-control__input:focus-visible { outline: var(--utf-focus-ring); outline-offset: var(--utf-focus-offset); }
```
Rules enforced by `tests/styles/server-style-guard.test.ts`: tokens only, no hex or colour functions, no pixel literals except 1px borders, media breakpoints only `520px` and `800px`. Use existing `--command-header-height` (`styles.css:1480`). Add only `--utf-create-summary-width: 22rem` and `--utf-create-bar-reserve: 12rem` to `tokens.css` (Phase 62 added `--utf-fleet-card-min` the same way). Use `overflow-wrap: anywhere`; no `text-overflow: ellipsis`.

---

### `tests/hosting/create-start-flow.test.tsx` (integration test)

**Analog:** `tests/hosting/cost-confirmation-provider.test.tsx` (header lines 1-80: `renderWithProviders` from `../helpers/renderWithProviders`, `hostingService`/`hostingQueryKeys` mocks, `makeServer`, `startAction` fixture). Use the real `CostConfirmationProvider` across two memory-router routes with a cached server, plus a StrictMode case, asserting the dialog opens only after the deferred call (RESEARCH Pitfall 1).

### `tests/hosting/create-server-page.test.tsx` (rewrite) and the other component tests

**Analog:** the existing file, header lines 1-60: `createMemoryRouter` + `RouterProvider`, `I18nextProvider` with `await i18nReady`, `vi.hoisted` `authState` and `vi.mock("@/auth/auth", ...)`, a `creator` `FullUser` with `claims: [{ identifier: "crt:sv", value: null }]`, and a `capability` fixture. `import { beforeEach } from "vitest"` explicitly (not a lint global). Build a CS2-shaped fixture (`requiresTerrain`, `requiresScenario`, `requiresModPack: false`) next to the Arma-shaped one for `create-capability-driven.test.tsx`.

### `tests/e2e/hosting-create.spec.ts` (e2e + axe)

**Analog:** `tests/e2e/hosting-a11y.spec.ts` (lines 1-70): `AxeBuilder` from `@axe-core/playwright`, `wcagTags = ["wcag2a","wcag2aa","wcag21a","wcag21aa"]`, `type Culture = "en" | "de"`, API fixture objects, `mockApi` with strict unmocked-request recording, and `scan()` asserting `passes.length > 0`. Cover 320x568 and 390x844 in EN and DE.

### `tests/locales/server-keys.test.ts` (extend)

**Analog:** itself, lines 307-309. Extend `isButtonLabelKey`:
```typescript
function isButtonLabelKey(key: string): boolean {
  return /^Server_Action_.+_(Label|Dismiss|Pending)$/.test(key) || key === "Server_Access_Retry";
}
```
to also match `Server_Create_Submit_*`, `Server_Create_Leave`, `Server_Create_Retry`, `Server_Create_BilledTo_Change`, `Server_Create_Owner_Retry`. New keys are flat `Server_Create_*`, pass `^[A-Z][A-Za-z0-9]*(_[A-Z][A-Za-z0-9]*)*(_one|_other)?$`, use `{{name}}` placeholders, and need equal placeholder sets in `en.json` and `de.json`. Carried `Hosting_*` keys use positional `{0}` through `formatResource`.

## Shared Patterns

### Disabled-with-reason actions
**Source:** `src/components/ActionControl.tsx:99-135` (`ActionButton unavailableReason`)
**Apply to:** the submit in `CreateSummary`, the "Start it now" radio reason, any blocked control. Reason is visible text, never `title`-only.

### Server state and unavailable reasons
**Source:** `src/components/server/ActionUnavailableReason.tsx` (`actionIsUnavailable`, `ActionUnavailableReason`) and `src/features/hosting/serverState.ts` (`deriveServerState`)
**Apply to:** workspace notice, start-now decision. Never read `deployment.status`; guarded by `tests/hosting/server-state-single-owner.test.ts`.

### Cost confirmation
**Source:** `src/components/server/useCostConfirmation.ts:15-55` (`request({ serverId, actionId, source, onSubmitted })`)
**Apply to:** workspace page only; never call from the create page (navigation race, Pitfall 1). `onSubmitted` clears the notice.

### Page states and headers
**Source:** `src/components/primitives.tsx` (`PageHeader`, `StatePanel`, `useUnsavedChanges`), `src/components/Breadcrumbs.tsx`, `src/features/hosting/components.tsx` (`HostingLoading`, `HostingError`)
**Apply to:** `CreateServerPage` state ladder (see page excerpt above).

### i18n
**Source:** `useTranslation` + `t("Server_Create_*", { name })`; `formatResource` from `@/app/i18n` for carried `Hosting_*` positional keys; `ApiText` for API-originated English text.
**Apply to:** every new component. No raw `Error.message`, no hard-coded copy, no `useHostingCopy` in migrated files.

### Query cache and invalidation
**Source:** `src/features/hosting/services.ts` (`hostingQueryKeys`, `hostingService`) and `CreateServerPage.tsx:552-556`
**Apply to:** create success path (fleet, notifications, cost-owner keys); workspace uses exact-key invalidation (`ServerWorkspacePage.tsx:107-124`).

### Lint and typing traps
**Apply to:** all new TSX. `react-hooks/refs`, `set-state-in-effect`, `incompatible-library` are on; use `useWatch`, no ref reads in render, no sync setState in effects. `exactOptionalPropertyTypes`: declare optional props `?: T | undefined`. `noUncheckedIndexedAccess`: guard `arr[0]`.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/components/server/AfterCreatingChoice.tsx` / `RadioGroupField` | component | event-driven | No radio-group `fieldset` control exists in `src/components`; build it with `FormFields` conventions (label wiring from `SearchField`, 44px row targets from `create.css`), and the disabled-with-reason pattern from `ActionControl.tsx`. |

(`Disclosure.tsx`, `PresetField.tsx` have only partial analogs and follow `SearchField.tsx` structure; use RESEARCH Patterns 5 and 6 for behavior.)

## Metadata

**Analog search scope:** `src/components`, `src/components/server`, `src/features/hosting`, `tests/hosting`, `tests/e2e`, `tests/locales`, `tests/styles`
**Files read:** CreateServerPage (to line 700), createServerModel, ServerWorkspacePage, ActionControl, SearchField, DeepLinkActionNotice, useCostConfirmation, fleet-toolbar.css (head), plus test headers
**Pattern extraction date:** 2026-11-20
**Planner notes:** RESEARCH Open Question 1 (do not enforce `requiresModPack` at create) and Open Question 3 (add `Server_Create_Blocked_EstimateFailed` EN/DE) need a decision before the plan fixes the readiness reason list.
