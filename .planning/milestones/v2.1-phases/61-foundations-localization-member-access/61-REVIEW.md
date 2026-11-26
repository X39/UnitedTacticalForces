---
phase: 61-foundations-localization-member-access
reviewed: 2026-11-18T00:00:00Z
depth: standard
files_reviewed: 54
files_reviewed_list:
  - docs/features/react-ui-beta.md
  - sources/ReactApp/package.json
  - sources/ReactApp/.prettierignore
  - sources/ReactApp/scripts/check-locales.mjs
  - sources/ReactApp/src/app/i18n.ts
  - sources/ReactApp/src/app/localizationTypes.ts
  - sources/ReactApp/src/i18next.d.ts
  - sources/ReactApp/src/main.tsx
  - sources/ReactApp/src/styles.css
  - sources/ReactApp/src/styles/tokens.css
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/identical-allowed.json
  - sources/ReactApp/src/components/ActionControl.tsx
  - sources/ReactApp/src/components/primitives.tsx
  - sources/ReactApp/src/components/CommandCenterLayout.tsx
  - sources/ReactApp/src/components/CommunityLayout.tsx
  - sources/ReactApp/src/components/ProfileMenu.tsx
  - sources/ReactApp/src/components/ProfileMenu.css
  - sources/ReactApp/src/components/server/ActionUnavailableReason.tsx
  - sources/ReactApp/src/components/server/ApiText.tsx
  - sources/ReactApp/src/components/server/CostConfirmDialog.tsx
  - sources/ReactApp/src/components/server/CostConfirmationProvider.tsx
  - sources/ReactApp/src/components/server/CostIndicator.tsx
  - sources/ReactApp/src/components/server/DeepLinkActionNotice.tsx
  - sources/ReactApp/src/components/server/ServerStateBadge.tsx
  - sources/ReactApp/src/components/server/ServerStateSummary.tsx
  - sources/ReactApp/src/components/server/UnavailableReason.tsx
  - sources/ReactApp/src/components/server/costConfirmationModel.ts
  - sources/ReactApp/src/components/server/server.css
  - sources/ReactApp/src/components/server/unavailableReasonMapping.ts
  - sources/ReactApp/src/components/server/useCostConfirmation.ts
  - sources/ReactApp/src/features/hosting/FleetPages.tsx
  - sources/ReactApp/src/features/hosting/ServerLogsPanel.tsx
  - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
  - sources/ReactApp/src/features/hosting/actionVocabulary.ts
  - sources/ReactApp/src/features/hosting/components.tsx
  - sources/ReactApp/src/features/hosting/model.ts
  - sources/ReactApp/src/features/hosting/serverState.ts
  - sources/ReactApp/src/features/hosting/useFleetQuery.ts
  - sources/ReactApp/src/features/hosting/workspaceModel.ts
  - sources/ReactApp/src/pages/CommandOverviewPage.tsx
  - tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs
  - tests/Api.Tests/GameServerHosting/GameServerNotificationAccessTests.cs
  - sources/ReactApp/tests/e2e/hosting-a11y.spec.ts
  - sources/ReactApp/tests/hosting/server-state.test.ts
  - sources/ReactApp/tests/hosting/cost-confirm-model.test.ts
  - sources/ReactApp/tests/locales/check-locales.test.ts
  - sources/ReactApp/tests/locales/ownership.test.ts
  - sources/ReactApp/tests/styles/server-style-guard.test.ts
  - sources/ReactApp/tests/styles/tokens-contrast.test.ts
  - sources/ReactApp/tests/helpers/cssGuard.ts
  - sources/ReactApp/tests/helpers/cssTokens.ts
  - sources/ReactApp/tests/helpers/renderWithProviders.tsx
  - sources/ReactApp/tests/mocks/react-icons.tsx
findings:
  critical: 0
  warning: 7
  info: 8
  total: 15
  necessity: 0
status: issues_found
---

# Phase 61: Code Review Report

**Reviewed:** 2026-11-18
**Depth:** standard
**Files Reviewed:** 54 (production sources read in full or by diff; test files skimmed for reliability only)
**Status:** issues_found

## Summary

Phase 61 moves locale ownership into the React app, adds a single server-state derivation, a shared cost-confirmation dialog and provider, disabled-with-reason action buttons, token styles, and API-side access tests for `sv` / `sv:view`. No security vulnerabilities or data-loss defects were found. API reason strings are rendered as React text (no HTML sinks), the untrusted `?action=` value is only ever used through `Array.find` or a `Map`, and the locale gate (`check-locales.mjs`) runs clean (3105 keys each, 0 failures, 0 warnings). No source files were modified.

The defects are correctness and robustness issues in the new shared components. The most important are:
- A query key shared by two observers whose query functions disagree about a missing server.
- Cost UI that says "Not costing" for a status the client does not recognise.
- Misleading "how to stop billing" copy on actions that have no cost impact.
- Silent dismissal of the confirmation dialog.
- A delete dialog that hides its own failure.

Structural findings (fallow) were not provided for this review.

## Warnings

### WR-01: Provider and workspace page share one query key with incompatible query functions

**File:** `sources/ReactApp/src/components/server/CostConfirmationProvider.tsx:89-103`, `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx:600-606`
**Issue:** Both observers use `hostingQueryKeys.server(serverId)`. The workspace `queryFn` resolves a missing server (404) to `null`, which selects the not-found panel. The provider `queryFn` throws `new Error("The server is no longer available.")` on the same condition. TanStack Query runs the `queryFn` of the observer that triggers each fetch, so while a dialog is open both functions write to one cache entry:
- When the server disappears (archived, deleted, access revoked), the shared entry flips to the error state.
- The workspace then renders `HostingError` with that English message (`server.isError` is checked before `!server.data`) instead of the not-found panel.
- The provider also writes `GameServer` data into an entry whose declared type elsewhere is `GameServer | null`. The two different generics (`useQuery<GameServer, Error>` and the inferred `GameServer | null`) hide this mismatch from the compiler.
**Fix:** Give the provider the same contract as the workspace page and treat `null` as "gone" locally:
```ts
queryFn: async ({ signal }) => (await hostingService.server(serverId, signal)) ?? null,
// ...
const server = live.data ?? undefined;
const serverGone = active !== null && (live.isError || live.data === null) && !server;
```
Alternatively, give the provider its own key such as `[...hostingQueryKeys.server(id), "confirmation"]`. Note that `invalidateQueries({ queryKey: hostingQueryKeys.server(id) })` still prefix-matches that key.

### WR-02: Unknown deployment status is reported as "Not costing"

**File:** `sources/ReactApp/src/features/hosting/serverState.ts:125-127, 145-148`
**Issue:** For a status string the client does not know (a status the API adds later), `deriveState` returns `unknown`. `deriveCost` then falls through to `input.possibleCost ? "maybe" : "none"`. When the API reports `possibleCost: false`, the badge reads "Status unknown" next to a cost chip that reads "Not costing" (`Server_Cost_None`).
- This is a fail-open cost claim, in a phase whose core invariant is that a server that may cost money is never shown as free.
- `tests/hosting/server-state.test.ts:113-120` and `:156-158` assert `cost: "none"` for this case, so the behaviour is deliberate but unsafe.
- A new provisioning-style status that bills while `possibleCost` is momentarily false is the realistic trigger.
**Fix:** Treat an unrecognised status as cost-uncertain:
```ts
function deriveCost(status: string | undefined, input: ServerStateInput): ServerCost {
  if (status && !isKnownStatus(status)) return "maybe";
  if (status && liveCostStatuses[status]) return "live";
  return input.possibleCost ? "maybe" : "none";
}
```
Update the two test expectations accordingly.

### WR-03: "How to stop billing" row is shown for actions with no cost impact

**File:** `sources/ReactApp/src/components/server/costConfirmationModel.ts:83`, `sources/ReactApp/src/components/server/CostConfirmDialog.tsx:98-111, 186`
**Issue:** `howToStop` is `"stop-text"` only for category `stops`; every other category, including `none`, maps to `"start-text"`. `StopRow` is rendered unconditionally.
- Every confirmation for an action with `NoCostImpact` (collect-logs, install and update where they report no impact) shows a "Stop" fact reading `Use "Shut down and stop billing" on the server page at any time.`
- The cost row correctly hides for `none`, but the stop row does not, so the dialog advertises billing for an action that has none.
- Phase 61 newly routes collect-logs through this dialog, so this is a regression in copy for that flow.
**Fix:** Make `howToStop` nullable and omit the row:
```ts
howToStop: category === "stops" ? "stop-text" : category === "starts" ? "start-text" : null,
// CostConfirmDialog: {facts.howToStop && <StopRow facts={facts} />}
```
Add a test for the `none` category in `cost-confirm-model.test.ts`.

### WR-04: Confirmation dialog vanishes silently when the server cannot be loaded or the route changes

**File:** `sources/ReactApp/src/components/server/CostConfirmationProvider.tsx:112-114, 124`
**Issue:** During render, `if (active !== null && (pathChanged || serverGone)) setActive(null)` discards the request.
- No toast is shown, so the user receives no explanation.
- Focus is not restored (`restoreFocus` is only called from `close`).
- A deep-link request keeps `?action=...` in the URL because `stripAction` is never called.
- The mutation state is not reset either.
- A transient failure of the first live fetch, such as a 403 or network error after the initial `placeholderData` render, makes an opened dialog disappear.
**Fix:** Route both cases through the same `close`-style path from an effect, not from render. For `serverGone`, call `showToast(t("Server_Access_LoadError"), "error")`, `stripAction()` for deep links, `resetExecution()` and `restoreFocus(active)`.

### WR-05: Delete confirmation hides its own failure behind the modal

**File:** `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx:735-739, 766-781`
**Issue:** `deleteOpen` is never reset when `deletion.mutate()` fails.
- The failure text is rendered in the page-level `<p role="alert">` outside the dialog, which is inert while the modal `<dialog>` is open. The dialog stays open with no feedback, and assistive technology cannot reach the alert.
- This phase added an `error` prop and a busy state to `ConfirmDialog` precisely for this shape, but this call site does not use them. Confirm and cancel also remain enabled while the delete is pending.
**Fix:**
```tsx
<ConfirmDialog
  ...
  busy={deletion.isPending}
  {...(deletion.isError ? { error: deletion.error.message } : {})}
  onCancel={() => { deletion.reset(); setDeleteOpen(false); }}
/>
```

### WR-06: Finance-only viewers see an overview of zero servers and zero cost

**File:** `sources/ReactApp/src/features/hosting/FleetPages.tsx:92-117`
**Issue:** `HostingAccessBoundary allowed={permissions.accessServers || permissions.viewFinance}` admits a viewer who has `fin` but no server access. The fleet and notification queries are both `enabled: permissions.accessServers`, so for that viewer `fleet.isLoading` is false and `fleet.data` is `undefined`. `HostingOverviewView` is then rendered with `servers=[]` and `notifications=[]`, showing "Total servers 0", "Possible cost 0" and "Attention 0".
- That presents a confident "nothing is costing money" signal that is wrong, because the data was never loaded.
- The code is pre-existing, but this phase rewired the same component and the fleet gating (`useFleetQuery(permissions.accessServers)`).
**Fix:** When `!permissions.accessServers`, render only the finance shortcuts without the fleet metrics and attention panel, or pass a `hasFleet` flag to `HostingOverviewView` that suppresses the metric tiles.

### WR-07: API placeholder owner names are localized in the dialog but shown raw everywhere else

**File:** `sources/ReactApp/src/components/server/costConfirmationModel.ts:4-8, 52-56`, `sources/ReactApp/src/features/hosting/components.tsx:93`, `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx:447-448`
**Issue:** The confirmation dialog maps the English API placeholders `"Assigned owner"` and `"Unknown owner"` (`GameServerProjectionService.cs:857-858`) to localized copy. `ServerCard` and the workspace record panel print `server.costOwnerName` unchanged.
- A German viewer sees "Assigned owner" or "Unknown owner" in English on the card and workspace, but German copy in the dialog. This is the cross-surface inconsistency the phase set out to remove.
- The mapping couples the client to English strings in the API. A user or Verein whose display name is literally `Assigned owner` or `Unknown owner` is mis-presented as a placeholder.
**Fix:** Export `ownerFor` (or a small `<CostOwnerName>` component) from the shared model and use it on all three surfaces. Prefer an explicit API discriminator (a `costOwnerVisible` boolean) over matching display text.

## Info

### IN-01: `isCostBearingAction` is exported and tested but never used

**File:** `sources/ReactApp/src/features/hosting/actionVocabulary.ts:156-160`
**Issue:** The only references are in `tests/hosting/action-vocabulary.test.ts`. The provider and dialog open for every action regardless of cost category, so no production code consults this predicate. Its doc comment says cost-bearing actions "must go through the cost dialog", which the code does not enforce.
**Fix:** Remove it and its test, or use it where the decision is made. Do not leave a documented policy that nothing implements.

### IN-02: Orphaned locale keys remain after the orphan-cleanup commit

**File:** `sources/ReactApp/src/locales/en.json:503, 585` (and the matching de.json lines)
**Issue:** `Hosting_Detail_ActionPending` and `Hosting_Detail_StateOffline` have no reference in `src/` or `tests/`. `check-locales.mjs` only verifies en/de parity, not use, so nothing catches this.
**Fix:** Delete the two keys from both locale files. Consider a used-key check in the locale gate.

### IN-03: Test constants lack the XML documentation the repo rules require, and are duplicated

**File:** `tests/Api.Tests/GameServerHosting/GameServerListAccessTests.cs:19`, `tests/Api.Tests/GameServerHosting/GameServerNotificationAccessTests.cs:19`
**Issue:** The private const `OperatePermissionRequiredReason` is copy-pasted into both files and has no `<summary>`. AGENTS.md requires documentation on every C# member. It also silently duplicates the production string in `GameServerProjectionService.cs:974, 1008`.
**Fix:** Add a `<summary>`. Move the constant to `HostingTestData` or reference a shared production constant so the three copies cannot drift.

### IN-04: Translation string embeds another translation's label text

**File:** `sources/ReactApp/src/locales/en.json:2959` (and de.json:2959)
**Issue:** `Server_Confirm_StopHowStart` hard-codes `"Shut down and stop billing"`, which is the value of `Server_Action_Stop_Label` (en.json:2932). Renaming the action label leaves the sentence pointing at a button that no longer exists, and `check-locales` cannot detect it.
**Fix:** Interpolate the label: `"Use \"{{label}}\" ..."`, passing `t("Server_Action_Stop_Label")`.

### IN-05: Deep-link "unavailable" notice prints the raw action id outside `ApiText`

**File:** `sources/ReactApp/src/components/server/DeepLinkActionNotice.tsx:67-68`
**Issue:** For an action with no vocabulary entry, `name` is `props.action.id`, inserted into a translated English/German sentence with no `lang="en"` wrapper. The `ActionLabel` component already handles this case correctly and is used on the other surfaces.
**Fix:** Use `ActionLabel`-style resolution (vocabulary label, otherwise `<ApiText>` with the label or humanized id), or accept a pre-rendered node.

### IN-06: Inconsistent fallback reason between the dialog and the action buttons

**File:** `sources/ReactApp/src/components/server/CostConfirmDialog.tsx:162-163`, `sources/ReactApp/src/components/server/ActionUnavailableReason.tsx:38-39`
**Issue:** For a disabled action with no `unavailableReason` and not pending, the buttons fall back to `deploymentStateReason`, which renders "unavailable while the server is {{state}}". The stale dialog passes `undefined`, which resolves to the generic "This action isn't available right now." The same action therefore explains itself differently depending on the surface.
**Fix:** Have the dialog reuse `ActionUnavailableReason` (it has `server` and `action`) instead of rebuilding the reason.

### IN-07: `isDeploymentActive` re-implements status comparison beside the status table

**File:** `sources/ReactApp/src/features/hosting/serverState.ts:180-183`
**Issue:** The doc comment says the helper keeps "the status comparison here", but it compares lowercase literals (`"terminated"`, `"terminatedduetofault"`) instead of using `statusClasses`/`liveCostStatuses`. A new terminal status added to those typed tables would not update it. It also counts `Stopped` and `Failed` as active, which does not match the `settled` classification used by `deriveState`.
**Fix:** Derive it from the typed tables, for example via a `terminal` flag per status, and decide explicitly whether `Stopped` and `Failed` count as active.

### IN-08: Attention items can pair `possibleCost: true` with a derived cost of "none"

**File:** `sources/ReactApp/src/pages/CommandOverviewPage.tsx:57-66`
**Issue:** The notification and fleet queries poll at different intervals (15s and 10s). If the notification still says `possibleCost` while the freshly fetched server says `deployment.possibleCost: false` and the status is settled, the item gets `possibleCost: true` but `derived.cost === "none"` and state "off". The same row is then listed under attention yet labelled "Not costing", which breaks the single-derivation guarantee the phase advertises.
**Fix:** When both a server and a notification exist, derive from the server and set `possibleCost` from `derived.cost !== "none"` (or drop the item once the server no longer reports possible cost).

---

_Reviewed: 2026-11-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
