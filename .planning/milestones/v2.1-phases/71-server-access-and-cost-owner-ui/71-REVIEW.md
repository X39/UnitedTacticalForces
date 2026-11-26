---
phase: 71-server-access-and-cost-owner-ui
reviewed: 2026-11-26T00:00:00Z
depth: standard
files_reviewed: 52
files_reviewed_list:
  - sources/Api/Controllers/GameServersController.cs
  - sources/Api/Controllers/CommandCenterController.cs
  - sources/Api/DTO/GameServerAccessDto.cs
  - sources/Api/DTO/GameServerCostOwnerOptionDto.cs
  - sources/Api/DTO/GameServerDto.cs
  - sources/Api/DTO/GameServerRoleAccessCandidateDto.cs
  - sources/Api/DTO/GameServerRoleAccessGrantDto.cs
  - sources/Api/DTO/GameServerUserAccessCandidateDto.cs
  - sources/Api/DTO/GameServerUserAccessGrantDto.cs
  - sources/Api/Helpers/LikePattern.cs
  - sources/Api/Services/GameServerLifecycle/GameServerAccessService.cs
  - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
  - sources/Api/Services/GameServerLifecycle/HostingSearchLimits.cs
  - sources/Api/Services/GameServerLifecycle/IGameServerAccessService.cs
  - sources/Api/Services/GameServerLifecycle/IGameServerProjectionService.cs
  - sources/ReactApp/src/app/urlParameters.ts
  - sources/ReactApp/src/app/useDebouncedValue.ts
  - sources/ReactApp/src/components/SearchPicker.tsx
  - sources/ReactApp/src/components/search-picker.css
  - sources/ReactApp/src/components/server/AccessGrantForm.tsx
  - sources/ReactApp/src/components/server/AccessSubjectRow.tsx
  - sources/ReactApp/src/components/server/CostStrip.tsx
  - sources/ReactApp/src/components/server/ReasonReserve.tsx
  - sources/ReactApp/src/components/server/access.css
  - sources/ReactApp/src/components/server/cost-owner.css
  - sources/ReactApp/src/components/server/reason-reserve.css
  - sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx
  - sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx
  - sources/ReactApp/src/features/hosting/pickerLimits.ts
  - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
  - sources/ReactApp/src/features/hosting/serverAccessModel.ts
  - sources/ReactApp/src/features/hosting/services.ts
  - sources/ReactApp/src/features/hosting/types.ts
  - sources/ReactApp/src/features/hosting/useAccessMutations.ts
  - sources/ReactApp/src/features/hosting/useCostOwnerTransfer.ts
  - sources/ReactApp/src/styles/tokens.css
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/tests/hosting/server-access-panel.test.tsx
  - sources/ReactApp/tests/hosting/server-cost-owner-panel.test.tsx
  - sources/ReactApp/tests/hosting/picker-limits.test.ts
  - sources/ReactApp/tests/search-picker.test.tsx
  - sources/ReactApp/tests/hosting/server-access-model.test.ts
  - sources/ReactApp/tests/hosting/cost-owner-transfer.test.tsx
  - sources/ReactApp/tests/e2e/hosting-access.spec.ts
  - sources/ReactApp/tests/e2e/hosting-cost-owner.spec.ts
  - sources/ReactApp/tests/e2e/support/screens/workspaceMocks.ts
  - sources/ReactApp/tests/e2e/support/germanLeak.ts
  - tests/Api.Tests/GameServerHosting/GameServerAccessServiceTests.cs
  - tests/Api.Tests/GameServerHosting/GameServerAccessControllerAuthorizationTests.cs
  - tests/Api.Tests/GameServerHosting/GameServerCreationCostOwnerOptionsTests.cs
  - tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs
findings:
  critical: 0
  warning: 1
  info: 0
  total: 1
  necessity: 0
status: issues_found
---

# Phase 71: Code Review Report

**Reviewed:** 2026-11-26
**Depth:** standard
**Files Reviewed:** 52
**Status:** issues_found

## Summary

Re-review after the iteration 2 fix pass (commits c53c5ea2, efde7d2c, 3d11434a, d2f9ea88). Four of the five iteration 2
items are resolved in the source. The fix for IN-01 (clearing `toggledSubject`) introduced one regression: it defeats
the WR-05 focus move whenever the post-toggle refetch is not instant, which is the normal case against a real server.

**Iteration 2 items, verified against source**

| Item | Result |
|---|---|
| IN-01 `toggledSubject` never cleared | Not resolved as intended; the fix regresses WR-05 (see WR-01 below). |
| IN-02 awaited candidate invalidation | Resolved. `invalidateAfterAccessChange` (`useAccessMutations.ts:52-60`) marks the candidate searches stale with `void` and awaits only the access, server, fleet and `me` refetches. `invalidateQueries` does not reject on a failed refetch (`throwOnError` is off), so the discarded promise cannot raise an unhandled rejection. The flag is set synchronously, so the stale-marking tests still hold. |
| IN-03 Escape from Retry | Resolved. The wrapper `onKeyDown` (`SearchPicker.tsx:338-345`) ignores events from the input, ignores Escape when the list is not expanded, and ignores every other key. The Retry focus move stays inside the wrapper, so the `onBlur` containment check does not close early. The input's `onFocus` calls `setOpen(true)`, but `close()` runs after it in the same event, so the batched result is closed. The consumer `onFocus` runs once more (`ServerCostOwnerPanel.tsx:231` sets `engaged` to true, which is idempotent; `AccessGrantForm` passes none). No new keyboard trap. |
| IN-04 internal type in public docs | Resolved. `IGameServerAccessService.cs:36` and `:56` read "from 1 to 50". No remaining `cref` to `HostingSearchLimits` in any public contract. `HostingSearchLimits.MaximumTake` is pinned by `GameServerAccessServiceTests.HostingSearchLimits_MaximumTake_MatchesTheDocumentedBound` (the test assembly reaches the internal type through the `InternalsVisibleTo` in `Api.csproj:35`). The literal in the controller docs is accepted. |
| IN-05 `pointerType === "mouse"` guard | Resolved, no regression. A mouse still chooses on pointer down and `choose` closes the list, which unmounts the `ul` before the click. A disabled option makes `choose` return at once. Touch and pen still skip pointer down and choose on the tap click. Clicks without a `pointerType`, and assistive technology activation, still choose. |

**Not run.** I did not execute the test suites, the e2e tests or the app. WR-01 comes from reading the call order of
`runToggle`, `useToggleAccess` and `applyOptimistic`.

## Warnings

### WR-01: Clearing `toggledSubject` in `runToggle`'s `finally` reads the optimistic cache, so the focus move to the list heading never fires against a real server

**File:** `sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx:329-340`, with `sources/ReactApp/src/features/hosting/useAccessMutations.ts:228-232` and `sources/ReactApp/src/features/hosting/serverAccessModel.ts:184-223`
**Issue:** The `finally` added in efde7d2c reads the cached access list and clears `toggledSubject.current` when the subject
is still listed and no toggle is in flight. The comment says this happens for a "settled toggle". It does not wait for
the refetch:

1. `useToggleAccess`'s `onSettled` (`useAccessMutations.ts:228-232`) is `void settleAccessChange(...)`. It returns
   nothing, so TanStack resolves `mutateAsync` as soon as `onSettled` returns. It does not wait for the access refetch.
   (`useRemoveAllAccess` returns the promise, which is why that hook behaves differently.)
2. `applyOptimistic` never deletes a subject, even when its last permission is removed (its own doc says so). While the
   refetch is in flight, the cache therefore still lists the subject with an empty permission array.
3. `runToggle`'s `finally` runs right after `mutateAsync` resolves, reads that optimistic cache, finds the subject listed,
   and sets `toggledSubject.current = null`.
4. The refetch then answers without the subject. The focus effect (`:282-289`) sees `key === null` and returns. The
   focused switch is unmounted with its row, focus falls to `document.body`, and the heading is never focused.

Failure scenario: a keyboard member opens Bob's row, turns off Bob's only permission (View). The server needs about
100 ms to answer the list read. The switch turns off, the row disappears when the list lands, and focus is lost to the
document. This is the exact WR-05 problem that effect was written to fix. The WR-05 test passes only because the
mock resolves the refetch within microtasks, before the `finally` runs: `serverAccess` is mocked with
`.mockResolvedValue(withoutBob)` (`server-access-panel.test.tsx:1148-1150`). The delayed-landing test (`:1165`) moves
focus to another control before the data lands, so it cannot show the loss either. The new test (`:1193`) covers
only the opposite direction.

**Fix:** Clear the ref only after the refetch of the access list has landed. The smallest change is to make the toggle
hook wait for the settle step when it is the last toggle, as `useRemoveAllAccess` already does:
```ts
onSettled: () => {
  // The settling toggle still counts as pending here, so one means it was the last.
  if (queryClient.isMutating({ mutationKey }) <= 1)
    return settleAccessChange(queryClient, serverId, callbacks);
  return undefined;
},
```
`mutateAsync` then resolves after the refetch, and the `finally` reads the refetched list. If delaying the mutation is
not wanted, skip the clear in the `finally` while the access query is refetching or invalidated, for example only
clear when `queryClient.getQueryState(key)?.fetchStatus === "idle" && !getQueryState(key)?.isInvalidated`. Add a
regression test whose `serverAccess` mock answers after a real delay (the manual-promise pattern of the test at
`:1165`, but without moving focus away) and assert that the heading has focus after the row is gone.

---

_Reviewed: 2026-11-26_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
