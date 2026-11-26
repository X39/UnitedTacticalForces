---
phase: 64-guided-server-creation
reviewed: 2026-11-20T00:00:00Z
depth: standard
files_reviewed: 24
files_reviewed_list:
  - sources/ReactApp/src/features/hosting/CreateServerPage.tsx
  - sources/ReactApp/src/features/hosting/createServerModel.ts
  - sources/ReactApp/src/features/hosting/createStartIntent.ts
  - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
  - sources/ReactApp/src/features/hosting/hosting.css
  - sources/ReactApp/src/components/FormFields.tsx
  - sources/ReactApp/src/components/Disclosure.tsx
  - sources/ReactApp/src/components/server/AdvancedOptions.tsx
  - sources/ReactApp/src/components/server/AfterCreatingChoice.tsx
  - sources/ReactApp/src/components/server/CostOwnerLine.tsx
  - sources/ReactApp/src/components/server/CreateStartNotice.tsx
  - sources/ReactApp/src/components/server/CreateSummary.tsx
  - sources/ReactApp/src/components/server/PresetField.tsx
  - sources/ReactApp/src/components/server/SizeField.tsx
  - sources/ReactApp/src/components/server/StorageDemandDetails.tsx
  - sources/ReactApp/src/components/server/StorageOptions.tsx
  - sources/ReactApp/src/components/server/useCostConfirmation.ts
  - sources/ReactApp/src/components/server/create.css
  - sources/ReactApp/src/components/server/create-fields.css
  - sources/ReactApp/src/components/server/create-options.css
  - sources/ReactApp/src/components/server/form-fields.css
  - sources/ReactApp/src/styles/tokens.css
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/identical-allowed.json
findings:
  critical: 0
  warning: 4
  info: 4
  total: 8
  necessity: 0
status: issues_found
---

# Phase 64: Code Review Report

**Reviewed:** 2026-11-20
**Depth:** standard
**Files Reviewed:** 24 source files (tests skimmed for gaps only)
**Status:** issues_found

## Summary

Reviewed the guided create page, its model, the new `src/components/server` components, the shared `FormFields` and `Disclosure`, the create-then-start handoff in the workspace, the CSS, and the locale files.

The overall design holds up:

- Defaults resolve without dirtying the form. The double-submit guard (`creating` ref plus `isPending`) is sound.
- The `flushSync` reset before navigation is correct.
- `invalidateQueries` does not reject, so the reset-then-navigate sequence is safe in the normal path.
- The router-state intent is validated strictly and cleared from history.
- No secret or raw provider error reaches the UI. The failure copy is fixed and API text is rendered as React text only.
- `en.json` and `de.json` have identical key sets and matching `{{placeholder}}` sets, and nothing identical between the two is unlisted in `identical-allowed.json`.
- The CSS classes removed from `hosting.css` have no remaining references.

I found no blockers. Four warnings need attention:

1. A transient render forces Advanced options open for every game that requires a terrain or scenario, which contradicts the UI-SPEC "collapsed by default" rule.
2. The create-start intent is not scoped to the created server.
3. The cost summary can show a zero price that the Size field refuses to show.
4. The create `catch` is wider than the create request.

None of the four has a page-level test (see "Test gaps").

## Warnings

### WR-01: Advanced options is forced open by a transient pre-default render for games that require a terrain or scenario

**File:** `sources/ReactApp/src/features/hosting/CreateServerPage.tsx:353-363` (interacts with the resolve effect at 287-329)
**Issue:** `terrainMissing` is computed from `terrains.data !== undefined && terrainId === null`. When the terrain list arrives, the render that first sees `terrains.data` still has `terrainId === null`, because the default is only written by the resolve effect after that commit. In that render `forceAdvanced` is true, so the adjust-state-while-rendering block runs `setAdvancedOpen(true)`.

The effect then writes the default terrain. The next render has `forceAdvanced === false` and only resets `advancedForced`. `advancedOpen` is never closed again. Every game with `requiresTerrain` or `requiresScenario` therefore opens Advanced on first load, although a valid default is resolved a moment later.

UI-SPEC line 389 says the disclosure opens automatically only when a required field "has no valid value after defaults", and line 102 says "Collapsed by default".

The same shape applies to `storageMisfit`. When the estimate arrives, the size that was picked without the demand is evaluated first, and the effect re-picks a fitting size afterwards.

The only page tests that assert a closed disclosure use the default fixture, where `requiresTerrain` and `requiresScenario` are false (`createFixtures.ts:84,86`). The terrain and scenario fixture at lines 112-114 is exercised only on `AdvancedOptions` in isolation.
**Fix:** Derive the "missing" facts from the values the defaults would produce, not from the pre-effect form value. For example, compute `resolveCreateDefaults(...)` in render (it is pure) and base `terrainMissing`, `scenarioMissing` and the misfit input on `resolved.terrainId`, `resolved.scenarioId` and `resolved.runtimeProfileIdentifier`. Alternatively, apply the forced open only after the resolve effect has settled, for example `terrainMissing && !hasPendingDefault`. Add a page-level test with a terrain-requiring capability that asserts the disclosure stays closed after the lists load.

### WR-02: The create-start intent is not scoped to the created server and can fire for another server or resurface later

**File:** `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx:112-148`
**Issue:** `createStart` is a bare boolean. The route element has no `key`, so navigating between `/command/servers/:serverId` routes reuses the component and keeps the state. `startIssued` only prevents a repeat for the same server id. It does not stop the effect from issuing a request for a different one.

Scenario: the member creates a server with "Start it now", cancels the confirmation, and `createStart` stays true. They then follow a link or notification to another server's workspace. The effect sees `createStart && server.data && createStartAction` with a different `primaryKey` and opens a start confirmation for the other server. The confirmation dialog still requires an explicit confirm, so no cost is incurred silently. The prompt is unrequested and refers to a server the member did not just create.

A related stale case: the member starts the server through the normal workspace button instead of the dialog, so `onSubmitted` never clears `createStart`. When the server later stops and `deriveServerState` returns "off", the "was created and is still off" notice reappears.
**Fix:** Store the intent against the server it was created for and ignore it otherwise.

```tsx
const [createStartFor, setCreateStartFor] = useState<string | null>(createIntent ? serverId : null);
const createStart = createStartFor === serverId;
// adopt: if (createIntent) setCreateStartFor(serverId);
// clear: setCreateStartFor(null)
```

Also clear it when the server's state leaves "off" for any reason, or when `serverId` changes.

### WR-03: The cost summary can show a zero price that the Size field deliberately hides, and a zero-priced size can win the default

**File:** `sources/ReactApp/src/features/hosting/createServerModel.ts:140-146` and `394-399` (versus `sources/ReactApp/src/components/server/SizeField.tsx:50-53`)
**Issue:** `SizeField.hourlyPrice` treats a price as real only when it is finite and greater than 0. Its doc comment says "An unknown price is never shown as zero". `combinedHourlyCost` only checks `== null`, and `defaultSizeIdentifier` skips only `== null`.

A size reported with `estimatedHourlyCostEur: 0` is therefore handled three ways:

- The select shows "price not available".
- The sticky summary shows a known "0.00 per hour while it runs" figure and a monthly figure.
- `defaultSizeIdentifier` treats it as the cheapest priced size and preselects it.

For the billing display this contradicts the stated invariant of never inventing an amount. The API's `null` is the documented unknown, but a misconfigured or placeholder 0 reaches the summary unchecked.
**Fix:** Use one predicate for "has a real price", for example exported from the model and used by `SizeField`, `combinedHourlyCost` and `defaultSizeIdentifier`:

```ts
export const hasRealPrice = (p: RuntimeProfile) =>
  typeof p.estimatedHourlyCostEur === "number" &&
  Number.isFinite(p.estimatedHourlyCostEur) &&
  p.estimatedHourlyCostEur > 0;
```

Return `undefined` or "unavailable" when it is false, and add a model test for a price of 0.

### WR-04: The create `catch` also covers the post-create steps and can report "nothing was created" after the server exists

**File:** `sources/ReactApp/src/features/hosting/CreateServerPage.tsx:415-450`
**Issue:** The single `try` wraps `mutateAsync` and also `flushSync(form.reset)`, the invalidations, `showToast` and `navigate`. If any step after a successful create throws, the `catch` shows "The server could not be created. Nothing was started and nothing costs", re-enables the form, and invites a second submit that creates a duplicate server. `invalidateQueries` does not reject by default, so this needs a throwing toast, a render error inside `flushSync`, or a navigate error. That is unlikely, but the failure mode is a duplicated cost-bearing resource and an incorrect statement to the member.
**Fix:** Narrow the `try` to the request and handle the follow-up separately.

```tsx
let server;
try { server = await create.mutateAsync(...); }
catch { /* request-failed handling */ return; }
finally-equivalent: reset creating.current only after the follow-up
// then run reset, invalidations, toast and navigate outside the request catch
```

## Info

### IN-01: Duplicated helpers across the new components

**File:** `sources/ReactApp/src/components/server/AdvancedOptions.tsx:113-115` and `StorageOptions.tsx:55-57`; `SizeField.tsx:38-41` and `StorageDemandDetails.tsx:18-22`
**Issue:** `toNumberOrNull` is defined twice, and `formatNumber` twice with slightly different defaults.
**Fix:** Move both to a shared helper (for example next to `FormFields` or in `features/hosting/model`).

### IN-02: Unnormalized provider status comparison and key mismatch in cache invalidation

**File:** `sources/ReactApp/src/components/server/AdvancedOptions.tsx:389` and `sources/ReactApp/src/features/hosting/CreateServerPage.tsx:429`
**Issue:**
- `item.availabilityStatus === "paused"` is an exact compare. `isProviderAvailable` and `providerStatusKey` trim and lowercase the status. A status with different case or whitespace falls through to the generic description.
- The invalidation uses `costOwnerQueryKey(user?.primaryKey ?? "")`, while the query uses `?? "anonymous"`, so the keys differ when no user is present. This is only reachable if the user disappears mid-create.
**Fix:** Normalize `availabilityStatus` once. Share one constant for the fallback user key.

### IN-03: Redundant alias and a slightly misleading double warning

**File:** `sources/ReactApp/src/features/hosting/CreateServerPage.tsx:331` and `sources/ReactApp/src/components/server/StorageDemandDetails.tsx:123-128`
**Issue:**
- `const strategy = storageStrategy;` is a pure alias.
- `StorageDemandDetails` renders the "selected profile fit unknown" warning for `selectedFit?.isSufficient == null`. That is also true when `selectedFit` is undefined, where the "capacity unavailable" paragraph already explains it, so two overlapping messages show.
**Fix:** Use `storageStrategy` directly. Guard the unknown warning with `selectedFit !== undefined`.

### IN-04: The zero-misfit-reason copy can mislead when the fit is merely unknown

**File:** `sources/ReactApp/src/features/hosting/createServerModel.ts:318-339` (rule from `model.ts:49-55`)
**Issue:** For the included disk, `isStorageSelectionValid` requires `isSufficient === true`, so an unknown fit (`null`) counts as a misfit. It blocks create with "The selected storage does not fit this setup", while the Size field simultaneously says "The fit could not be checked". The two statements conflict on the same screen. This is existing selection logic, now surfaced as a hard block.
**Fix:** Either distinguish "unknown fit" from "known insufficient" in `deriveCreateReadiness` with its own reason and copy, or confirm that blocking on an unknown fit is intended and align the Size-field copy with it.

## Test gaps (tests skimmed only)

- No page-level test asserts that Advanced stays closed for a terrain-requiring or scenario-requiring game (WR-01).
- No test covers the start intent crossing server ids, or the notice resurfacing after a manual start and a later stop (WR-02).
- No model test covers `estimatedHourlyCostEur: 0` (WR-03).
- No test makes a post-create step throw to assert that the failure copy is not shown (WR-04).

---

_Reviewed: 2026-11-20_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
