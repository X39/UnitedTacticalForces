---
phase: 71-server-access-and-cost-owner-ui
plan: 08
subsystem: ui
tags: [react, access-panel, switches, optimistic-update, tanstack-query, confirm-dialog]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-04 serverAccessModel catalog, effectivePermissions, canAssign and AccessSubjectRow with a children slot"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-05 accessMutationScope, invalidateAfterAccessChange, the panel-local polite region"
provides:
  - "useToggleAccess: one grant or revoke per call, optimistic, single-pair rollback, settle-time invalidation once the last pending toggle settles"
  - "applyOptimistic and requiresSelfConfirm in serverAccessModel"
  - "Eight SwitchField permission switches in each opened AccessSubjectRow, with Full control inclusion, assignability, busy state and a per-switch alert"
  - "Self-removal danger ConfirmDialog in ServerAccessPanel"
  - "Two-column (above 800px) and one-column (800px and below) switch layout"
affects: [71-09, 71-10, 71-11, 71-12, access-panel]

actuals:
  tokens: 13100
  tasks: 2
  commits: 2
plan_head_before: 1738d16ac387869324e4f5f108abc1e1afaf3da5
plan_head_after: 577ec9e93387fd9a8900346eecb6ee64b5fad3f6

tech-stack:
  added: []
  patterns:
    - "Concurrent optimistic toggles use mutateAsync per call; mutate per-call callbacks fire only for the latest call"
    - "Rollback writes the inverse of the one failed pair, never a whole-list snapshot"
    - "Pending and failed state is kept per subject-permission pair key in the panel, a ref guards synchronous double input"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/serverAccessModel.ts
    - sources/ReactApp/src/features/hosting/useAccessMutations.ts
    - sources/ReactApp/src/components/server/AccessSubjectRow.tsx
    - sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx
    - sources/ReactApp/src/components/server/access.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/server-access-model.test.ts
    - sources/ReactApp/tests/hosting/server-access-panel.test.tsx

key-decisions:
  - "Toggles go through mutateAsync, not mutate, because TanStack Query v5 fires the per-call callbacks of mutate only for the latest call, and toggles overlap"
  - "The failure alert is the SwitchField error prop (FieldError, role alert, aria-describedby wired), so it sits directly under the switch with no extra markup"
  - "aria-busy is set only while pending (omitted otherwise), the input is not disabled so focus stays on the switch"
  - "canAssign now accepts a readonly assignable list, so rows can pass the list without copying it"
  - "The hook-level onSettled does not await the invalidation, so a switch is released as soon as its own request settles"

patterns-established:
  - "Switch region rendered by the row from optional onToggle, failedPermissions, pendingPermissions and assignablePermissions props; children still render after it for later plans"

requirements-completed: [D-04, D-06, D-07, D-08, D-09]

coverage:
  - id: S1
    description: "An opened row holds eight switches in the fixed order, named by label, described by description, on exactly for held permissions, with no authored aria-checked"
    requirement: "D-06, D-07"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#shows eight switches in the fixed order, on exactly for the held permissions"
        status: pass
    human_judgment: false
  - id: S2
    description: "A toggle moves the switch at once and sends one grant or revoke call, for members and for roles, with no staging"
    requirement: "D-04"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#moves the switch at once and sends one grant call without staging"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#sends role toggles to the role endpoint with the numeric id"
        status: pass
    human_judgment: false
  - id: S3
    description: "A failed toggle rolls back only its own pair, a View toggle that succeeded meanwhile keeps its state, and the alert sits under the failed switch"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#rolls back only the failed pair and shows the alert directly under its switch"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-model.test.ts#applyOptimistic is restored by the inverse change"
        status: pass
    human_judgment: false
  - id: S4
    description: "Access list, server (exact), fleet (exact) and the signed-in user are invalidated once, after the last pending toggle settled; a subject without permissions stays listed until the refetch"
    requirement: "D-08, D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#refreshes the exact queries once, after the last pending toggle settled"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#keeps a member whose last permission was switched off in the list until the refetch"
        status: pass
    human_judgment: false
  - id: S5
    description: "With Full control the seven other switches are on, disabled and say they are included; switching Full control off revokes sv only and the stored grants reappear"
    requirement: "D-07"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#shows the seven other switches on and disabled while Full control is held"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#revokes only Full control and shows the stored grants again"
        status: pass
    human_judgment: false
  - id: S6
    description: "A permission outside assignablePermissions is disabled with the visible reason; a pending switch is aria-busy and ignores a second input"
    requirement: "D-07, orchestrator decision 2"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#disables a switch the member may not assign and says why"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#marks a switch busy while its request is pending and sends no second request"
        status: pass
    human_judgment: false
  - id: S7
    description: "Removing Full control, View or Manage access on the own row asks first in a danger dialog with Keep permission focused; Operate, other rows and grants never ask"
    requirement: "D-09"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-model.test.ts#requiresSelfConfirm is true only for removing Full control, View or Manage access on the own row"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#asks before the signed-in member removes their own View, with Keep permission focused"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#revokes Operate on the own row and grants on the own row without a dialog"
        status: pass
    human_judgment: false
  - id: S8
    description: "Success is announced politely as Name: Permission granted or removed and focus stays on the switch"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#announces the result politely and keeps focus on the switch"
        status: pass
    human_judgment: false
  - id: S9
    description: "Switches lay out in two equal columns above 800px and one column at 800px and below with touch-height rows, using only the allowed breakpoints and tokens"
    requirement: "UI Considerations: switch columns at 320"
    verification:
      - kind: unit
        ref: "tests/styles (server style guard) passes with the new access.css rules"
        status: pass
    human_judgment: true
    note: "The rendered 320, 390 and desktop layout is not covered by a jsdom test; plan 71-09 and the later reflow plans own the browser check"
---

# Phase 71 Plan 08: Access Permission Switches Summary

Eight optimistic permission switches per access row, with a single-pair rollback, Full control and assignability rules, a confirmed self-removal and polite announcements.

## What was built

**Task 1 (tracer, commit c9df304d).** `applyOptimistic` (pure) and `useToggleAccess` (mutation key `["server-access", serverId, "toggle"]`, the shared serial scope, `onMutate` that cancels the exact access key and writes the change, `onError` that writes the inverse pair only, hook-level `onSettled` that invalidates once `isMutating <= 1`). `AccessSubjectRow` renders the eight `SwitchField`s from optional props; the panel keeps pending and failed pair sets and routes switches to the hook. Locale keys State_On, State_Off, Error_Change.

**Task 2 (commit 577ec9e9).** `requiresSelfConfirm`; included, unassignable and busy switch states; the self-removal `ConfirmDialog` (danger confirm, primary dismiss focused, no backdrop close, permission label in `bdi`); the announcements; the `.access-switches` grid; eight more locale keys in en and de.

The tracer feedback gate was satisfied by re-running the automated verify (model and panel tests, both locale gates, typecheck) end to end before Task 2; it passed.

## Verification

- `tests/hosting/server-access-model.test.ts`, `tests/hosting/server-access-panel.test.tsx`, `tests/styles`, `tests/locales`, `tests/ratchets`: all pass (49 panel tests).
- `locales:check` 0 failures; `locales:unused` reports no `Server_Access_*` key (the 46 warnings are pre-existing unrelated keys).
- `lint`, `typecheck` and prettier on the plan files: clean.
- Whole `tests/hosting` run: 2470 pass, 1 fail. The failure is `metrics-performance.test.tsx` (a 200 ms wall-clock budget, 335 ms measured while another executor ran in parallel). It touches none of this plan's files and is not caused by it.
- Not run: `contracts:check`, `build:assets` and Playwright; no API contract, route or built asset changed in this plan, and the e2e coverage for access belongs to plan 71-09.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Per-call `mutate` callbacks cannot drive concurrent toggles**
- **Found during:** Task 1 design
- **Issue:** The plan says to route to `useToggleAccess().mutate` with `mutate` callbacks for the alert and the announcement. In TanStack Query v5 those per-call callbacks fire only for the latest `mutate` call of an observer, so with two overlapping toggles the earlier pair would never get its alert, its announcement or its pending release.
- **Fix:** The panel calls `mutateAsync` per toggle and tracks the result of each call itself (try, catch, finally). The hook-level `onMutate`, `onError` and `onSettled` are unchanged and still run for every toggle.
- **Files modified:** `ServerAccessPanel.tsx`, `useAccessMutations.ts` (doc comment)
- **Commit:** c9df304d

**2. [Rule 1 - Type error] `canAssign` rejected a readonly assignable list**
- **Found during:** Task 2 typecheck
- **Issue:** `canAssign` took `Pick<ServerAccess, "assignablePermissions">` (a mutable array), but the row receives a `readonly string[]`.
- **Fix:** The parameter type is now `{ readonly assignablePermissions: readonly string[] }`, which every existing caller still satisfies.
- **Files modified:** `serverAccessModel.ts`
- **Commit:** 577ec9e9

**3. [Rule 3 - Blocker] Test fixtures made the assignable-only switches inert**
- **Found during:** Task 2 tests
- **Issue:** The shared `accessList` fixture assigns only `sv`, `sv:view` and `sv:operate`, so once unassignable switches were disabled the Manage access switch could not be clicked.
- **Fix:** The switch tests use fixtures (`accessWithBob`, `ownRowOnly`) whose assignable list is the whole catalog.
- **Files modified:** `server-access-panel.test.tsx`
- **Commit:** 577ec9e9

### Plan wording interpreted

- The plan asks for a "reserved alert slot (`role="alert"`, one line only when an error exists)". I used the `SwitchField` `error` prop instead of an always-present empty alert. It renders `role="alert"` only on failure, directly under the switch, with `aria-invalid` and `aria-describedby` wired. An empty always-mounted alert per switch would add 8 empty alert nodes per open row and break `getByRole("alert")` queries for no gain, since an inserted `role="alert"` is announced.
- The commit trailer names Claude Sonnet 5.5 (the system attribution reminder) rather than the Opus 5.5 line in the orchestrator's environment notes, so it matches the model that wrote the commit.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path or schema; the toggles use the existing grant and revoke calls. Mitigations T-71-20 (self-removal confirm), T-71-21 (serial scope, inverse-only rollback, one invalidation after the last toggle) and T-71-22 (switches outside `assignablePermissions` disabled with the reason) are implemented and tested.

## Cross-plan findings

- **XPF-71-08-1:** concurrent optimistic toggles must use `mutateAsync`; `mutate` per-call callbacks fire only for the latest call. Plan 71-10 (Remove all access, runs several revokes) should use the same pattern or a sequential `await` loop, and must not rely on `mutate` callbacks.
- **XPF-71-08-2:** `AccessSubjectRow` now takes `onToggle`, `failedPermissions`, `pendingPermissions` and `assignablePermissions`. Without `onToggle` the row shows no switches. Plan 71-10 should add its Remove all access action through the `children` slot, which still renders after the switches.
- **XPF-71-08-3:** the pair key is `kind:id|permission` (`pairKey` in `ServerAccessPanel.tsx`); a plan that adds its own per-pair state should reuse it so pending and failed sets stay consistent.
- **XPF-71-08-4:** the hook-level `onSettled` of `useToggleAccess` is the place 71-10 attaches the D-15 (lost own access) handling; it already runs after unmount and does not await the refetch. Keep that shape.
- **XPF-71-08-5:** `tests/hosting/metrics-performance.test.tsx` is wall-clock sensitive and fails under parallel executor load (335 ms against a 200 ms budget). It is unrelated to this phase; re-run it alone before treating a failure as real.
- **XPF-71-08-6:** any e2e mock for access (plan 71-09) must let the grant and revoke calls resolve, and the refetch after the last toggle returns server truth, so a mock that always returns the original list will flip the switch back after settle.

## Self-Check: PASSED

- `sources/ReactApp/src/features/hosting/useAccessMutations.ts`: FOUND
- `sources/ReactApp/src/features/hosting/serverAccessModel.ts`: FOUND
- `sources/ReactApp/src/components/server/AccessSubjectRow.tsx`: FOUND
- Commit c9df304d: FOUND; commit 577ec9e9: FOUND
