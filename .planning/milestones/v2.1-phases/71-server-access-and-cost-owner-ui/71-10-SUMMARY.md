---
phase: 71-server-access-and-cost-owner-ui
plan: 10
subsystem: ui
tags: [react, access-panel, tanstack-query, confirm-dialog, focus-management, filter]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-05 accessMutationScope, invalidateAfterAccessChange, useGrantAccess"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-08 useToggleAccess, AccessSubjectRow switches, pairKey state, the self-removal dialog"
provides:
  - "useRemoveAllAccess: sequential revokes of the stored permissions, stop at the first failure, always refetch, resolves after the refresh"
  - "settleAccessChange: the shared hook-level settle step that refreshes server, fleet, access list and signed-in user and reports a lost View or a lost Manage access"
  - "Remove all access button in AccessSubjectRow and the danger dialog (own-row variant) in ServerAccessPanel"
  - "D-15 lost-access flow: toast, Settings h2 focus through the onAccessLost prop, fleet redirect with replace on 404 or 403"
  - "Filter for lists of eight or more subjects (D-16)"
affects: [71-11, 71-12, access-panel]

actuals:
  tokens: 18115
  tasks: 3
  commits: 3
plan_head_before: 7d4c467a1d946694b13c840e56a360c6763ce628
plan_head_after: 957aefec31babef5599b010626f4263f3875d044

tech-stack:
  added: []
  patterns:
    - "All access mutations end in one hook-level settleAccessChange, because the panel unmounts when canManageAccess flips"
    - "Remove all access awaits the settle step in onSettled, so mutateAsync resolves only after the list shows what is left"
    - "A 403 on a switch is tracked as a forbidden subset of the failed pairs and shown with the forbidden message"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/useAccessMutations.ts
    - sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx
    - sources/ReactApp/src/components/server/AccessSubjectRow.tsx
    - sources/ReactApp/src/components/server/access.css
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/server-access-panel.test.tsx
    - sources/ReactApp/tests/hosting/settings-section.test.tsx

key-decisions:
  - "Revocation order is catalog order with ids the catalog does not know first and Manage access then Full control last, so a member removing their own access keeps the right to revoke until the final call"
  - "Remove all access reads the stored permissions again from the current list at confirm time, not from the row that opened the dialog"
  - "The 404/403 test is done on the settle step by reading getQueryState of the server key: null data is a 404, an ApiError 403 next to stale data is a refusal"
  - "Toasts use the info intent (polite region)"
  - "The filter is ignored below eight subjects even if text was typed earlier, so removals can never leave a hidden list behind a missing field"

patterns-established:
  - "Hooks take AccessSettleCallbacks ({ onViewLost, onManageLost }) as a second argument; the panel builds them from useNavigate, useAppToast and onAccessLost"

requirements-completed: [D-09, D-15, D-16]

coverage:
  - id: S1
    description: "Remove all access opens a danger dialog (own row: self variant) with Keep access focused; confirming revokes each stored permission once in order and the dialog is busy meanwhile"
    requirement: "D-09"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#opens a danger dialog with Keep access focused"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#revokes each stored permission once, in order, and is busy meanwhile"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#uses the self variant of the dialog on the own row"
        status: pass
    human_judgment: false
  - id: S2
    description: "A partial failure keeps the dialog open with the alert, makes no further call and refetches the list; success closes the dialog, announces and focuses the list heading"
    requirement: "D-09"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#stops at the first failure, keeps the dialog open with the alert and fetches the list again"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#closes the dialog, announces the result and focuses the list heading on success"
        status: pass
    human_judgment: false
  - id: S3
    description: "Remove all access is unavailable with a visible reason when a stored permission is outside the assignable set"
    requirement: "orchestrator decision 2"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#makes Remove all access unavailable with the reason when a stored permission is not assignable"
        status: pass
    human_judgment: false
  - id: S4
    description: "Lost Manage access removes the panel, shows the toast and moves focus to the Settings h2; the settle step completes after the panel unmounted"
    requirement: "D-15"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#tells the member, removes the panel and calls onAccessLost when Manage access is gone"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#completes the settle step after the panel unmounted, for Remove all access on the own row"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#moves focus to the Settings heading when the Access panel goes after a change of access"
        status: pass
    human_judgment: false
  - id: S5
    description: "A refreshed server read that answers 403 or resolves to null sends the member to the fleet with replace and the ViewLost toast"
    requirement: "D-15"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#leaves for the fleet with replace and a toast when the refreshed server read answers 403"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#leaves for the fleet when the refreshed server read resolves to nothing"
        status: pass
    human_judgment: false
  - id: S6
    description: "A 403 on a switch shows the forbidden message in place and still refreshes server, fleet and user; other failures keep the generic message"
    requirement: "UI Considerations: conflict, lost permission"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#shows the forbidden message under the switch on a 403 and still refreshes the queries"
        status: pass
    human_judgment: false
  - id: S7
    description: "From eight subjects the filter narrows by name and translated kind with a polite count, never hides a subject that cannot see the server, shows Nobody matches with a clear action, and stays out of the URL"
    requirement: "D-16"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access list filter (9 tests)"
        status: pass
    human_judgment: false
---

# Phase 71 Plan 10: Remove All Access, Lost Access and List Filter Summary

Remove all access with a danger dialog and sequential revokes, a shared hook-level settle step that handles the member losing their own access (toast, Settings heading focus, fleet redirect), and a name-and-kind filter from eight subjects.

## What was built

**Task 1 (tracer, commit edee01f4).** `useRemoveAllAccess` (serial scope, mutation key `["server-access", serverId, "remove-all"]`) revokes the stored permissions one at a time through `changeServerUserAccess` or `changeServerRoleAccess` with `grant false`, stops at the first rejection and rethrows it. Its hook-level `onSettled` returns the refresh promise, so `mutateAsync` resolves only after the lists were refetched. `AccessSubjectRow` ends its content with the ghost button Remove all access (hidden for a subject with no stored permission, unavailable with `Server_Access_Remove_NotAssignable` when a stored permission fails `canAssign`). `ServerAccessPanel` owns the dialog (danger confirm, primary Keep access focused, busy, no backdrop close, error alert). On success it closes, announces and focuses the list `h3` (`tabIndex -1`, ref). The tracer feedback gate was satisfied by re-running the automated verify (panel tests, both locale gates, typecheck) before Task 2; it passed.

**Task 2 (commit d2d6186b).** `settleAccessChange(queryClient, serverId, { onViewLost, onManageLost })` awaits the exact invalidations, then reads `getQueryState` of the server key: null data or an `ApiError` 403 calls `onViewLost`, a loaded server with `canManageAccess === false` calls `onManageLost`. `useGrantAccess`, `useToggleAccess` and `useRemoveAllAccess` take the callbacks and run it from their hook-level `onSettled`. The panel builds the callbacks from `useNavigate`, `useAppToast` and the new `onAccessLost` prop; `SettingsSection` passes `onAccessLost` that bumps its heading focus counter. A 403 on a switch is kept in a `forbiddenPairs` subset and shown with `Server_Access_Error_Forbidden` (new optional row prop `forbiddenPermissions`).

**Task 3 (commit 957aefec).** `accessFilterThreshold = 8`; from that many subjects a `SearchField` and a polite `Server_Access_Filter_Result` line appear above the list. The filter matches name or translated kind case-insensitively for the locale and never hides a subject with the View warning. With no visible row an `EmptyState` offers a ghost Clear filter that empties the field and returns focus to it. The filter is component state only.

## XPF findings

- **XPF-71-05-4 (D-15 is this plan):** done. Toast, Settings h2 focus via `onAccessLost`, redirect with `replace` on a 404 or 403 server read.
- **XPF-71-08-4 (hook-level onSettled):** done once as `settleAccessChange`, reused by the grant, toggle and remove-all hooks.
- **XPF-71-05-1 (scope and exact invalidations):** every access hook uses `accessMutationScope(serverId)`; `settleAccessChange` delegates to `invalidateAfterAccessChange` (all `exact: true`).
- **XPF-71-08-1 (sequential remove all):** `mutateAsync`, revokes in a loop inside one `mutationFn`, stop at the first failure, refetch always (`onSettled`).
- **XPF-71-08-2/3 (row props, pairKey):** `onRemoveAll` and `forbiddenPermissions` were added as optional props; the forbidden state reuses `pairKey` and `permissionsOfSubject`.
- **XPF-71-01-1 (assignable decides unavailability):** `AccessSubjectRow` blocks Remove all access when any stored permission is outside `assignablePermissions`, with the visible reason.
- **XPF-71-07-3 (dynamic keys):** no dynamic translation key was needed; the forbidden or generic switch message is chosen with two literal keys, the kind label with two literal keys.
- **XPF-71-05-5 (Server_Change_* keys):** carried, not applicable: this plan adds only `Server_Access_*` keys, so `tests/locales/server-keys.test.ts` needed no change.
- **XPF-71-04-1 (placement):** the dialog, hooks and filter live in `ServerAccessPanelContent`.
- **XPF-71-04-4 (EmptyState heading level):** carried. The "Nobody matches" state uses the shared `EmptyState`, which renders an `h2` like the existing "No one has been given access" state. 71-12's accessibility audit still owns the decision whether `StatePanel` needs a heading-level prop.
- **XPF-71-08-5 (metrics-performance flake):** did not occur; the whole `tests/hosting` folder passed (2896 tests with styles, locales, ratchets and app).

## Verification

- `tests/hosting`, `tests/styles`, `tests/locales`, `tests/ratchets`, `tests/app`: 139 files, 2896 passed, 2 skipped (pre-existing skips).
- `locales:check`: 4035 keys each, 0 failures. `locales:unused`: no `Server_Access_*` key reported (46 pre-existing unrelated warnings).
- `lint` (max-warnings 0), `typecheck` and prettier (`--end-of-line auto`) on all plan files: clean.
- CI step substitution: `npm run ci` was not run because `format:check` fails on every file under CRLF checkouts (environmental). The steps were run individually as listed above. `contracts:check`, `build:assets` and Playwright were not run: this plan changes no API contract, route or built asset, and e2e coverage of the Access panel belongs to the later plans.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Catalog-order revocation would fail on the member's own row**
- **Found during:** Task 1 design
- **Issue:** The plan says to revoke "in catalog order". Manage access sits before Delete data, Manage credentials and Reveal credentials in the catalog. A member removing their own access with Manage access stored would lose the right to revoke after that call, and every later revoke would be refused with 403, ending in a partial removal that is not the user's intent.
- **Fix:** `revocationOrder` keeps catalog order but moves Manage access, then Full control, to the end and puts ids the catalog does not know first. For rows that do not store those two permissions it is plain catalog order.
- **Files modified:** `useAccessMutations.ts`, `server-access-panel.test.tsx`
- **Commit:** edee01f4

**2. [Rule 1 - Bug] The permissions to revoke are read at confirm time**
- **Found during:** Task 1 design
- **Issue:** Using the permissions of the row that opened the dialog could revoke ids that a refetch had since removed (or miss ones another manager added).
- **Fix:** `confirmRemoval` looks the subject up again in the current list and passes its stored permissions to the mutation.
- **Files modified:** `ServerAccessPanel.tsx`
- **Commit:** edee01f4

**3. [Rule 2 - Missing critical] Filter state could hide a list behind a missing field**
- **Found during:** Task 3
- **Issue:** After removals shorten the list below eight, the filter field disappears while its text would still hide rows.
- **Fix:** The filter text only applies while the list has at least `accessFilterThreshold` subjects.
- **Files modified:** `ServerAccessPanel.tsx`
- **Commit:** 957aefec

### Plan wording interpreted

- The grant hook previously refreshed on success and on a 403 only. Its refresh now goes through `settleAccessChange` under the same condition (success or 403), so a 500 on a grant still triggers no refresh. The toggle and remove-all hooks settle after every outcome, as the toggle already did.
- The toggle hook keeps its "settle once the last pending toggle has settled" rule and does not await the settle step, so a switch is released as soon as its own request settles. Remove all access does await it, so its dialog closes only once the list shows what is left.
- The commit trailer names Claude Sonnet 5.5 (the system attribution reminder), not the Opus 5.5 line of the orchestrator's environment notes, matching the model that wrote the commits.
- The plan's ledger file for commit counting could not be written inside the shared git directory from this isolated worktree; `commits` and `plan_head_before` were measured from the expected base commit instead.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. No new network endpoint, auth path or schema; the remove-all action uses the existing grant and revoke calls. Mitigations T-71-24 (view-lost detection and redirect with replace), T-71-25 (serial, stop-at-first-failure, always refetch, partial-failure alert) and T-71-26 (settle step after every access mutation, also after unmount) are implemented and tested.

## Cross-plan findings

- **XPF-71-10-1:** every access mutation hook now takes `AccessSettleCallbacks` as its second argument. A later plan that adds another access mutation (for example a cost owner transfer that can change `canManageAccess`) should reuse `settleAccessChange` instead of calling `invalidateAfterAccessChange` directly.
- **XPF-71-10-2:** the access list heading `h3` is now focusable (`tabIndex -1`) and is the focus target after Remove all access; e2e flows (71-11, 71-12) should expect focus there, and the Settings `h2` after a lost Manage access.
- **XPF-71-10-3:** the settle step reads the server from the query cache. An e2e mock that always answers the server with `canManageAccess: true` will never show the lost-access flow; a mock must flip the flag, answer 404 or answer 403 after the change.
- **XPF-71-10-4:** the filter result line (`aria-live polite`) is mounted whenever the filter is shown, including with an empty field ("8 entries shown"), so the region exists before its text changes.

## Self-Check: PASSED

- `sources/ReactApp/src/features/hosting/useAccessMutations.ts`: FOUND
- `sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx`: FOUND
- `sources/ReactApp/src/components/server/AccessSubjectRow.tsx`: FOUND
- `sources/ReactApp/tests/hosting/settings-section.test.tsx`: FOUND
- Commits edee01f4, d2d6186b, 957aefec: FOUND
