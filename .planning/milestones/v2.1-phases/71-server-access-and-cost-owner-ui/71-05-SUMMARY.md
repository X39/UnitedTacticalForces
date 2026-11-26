---
phase: 71-server-access-and-cost-owner-ui
plan: 05
subsystem: ui
tags: [react, react-query, combobox, access-control, i18n, settings-panel]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-02 SearchPicker and useDebouncedValue; 71-04 Access panel, access data layer and canAssign; 71-03 canManageAccess in the generated contract"
provides:
  - "AccessGrantForm: segmented Member / Role choice, SearchPicker and the Give access button with reason, pending and error states"
  - "useAccessMutations.ts: accessMutationScope, invalidateAfterAccessChange, useGrantAccess, grantFailureKey"
  - "Grant wiring in ServerAccessPanel: candidate search (250ms, one character, previous results kept, stale requests aborted), focus return and a polite announcement"
  - "Settings read-only banner that no longer claims the whole tab is read-only while the Access panel is operable"
affects: [71-06, 71-07, 71-08, 71-09, 71-12, server-access-panel, settings-section]

actuals:
  tokens: 11000
  tasks: 2
  commits: 2
plan_head_before: 7ffc03082038a17ef2f5be1e08e81307bc426d9d
plan_head_after: b797daa8c986945cf476eb493f85bf0a6ea00854

tech-stack:
  added: []
  patterns:
    - "One serial mutation scope per server (`server-access-<id>`) shared by every access mutation"
    - "Invalidate exact keys only after an access change (access, server, fleet, me) plus the candidates prefix of the granted kind"
    - "Candidate query result tagged with its kind so a placeholder kept from the other flavor is never mapped"
    - "Search enabled only for a rested text (debounced equals trimmed), status derived from the immediate text"

key-files:
  created:
    - sources/ReactApp/src/components/server/AccessGrantForm.tsx
    - sources/ReactApp/src/features/hosting/useAccessMutations.ts
  modified:
    - sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/components/server/access.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/tests/hosting/server-access-panel.test.tsx
    - sources/ReactApp/tests/hosting/settings-section.test.tsx
    - sources/ReactApp/tests/locales/server-keys.test.ts

key-decisions:
  - "Give access is blocked only when the loaded access list lacks sv:view in assignablePermissions; while the list is loading or failed the button stays usable and the API's 403 is mapped to the forbidden message"
  - "A failed grant also refreshes access, server, fleet and the signed-in member when the status is 403, because the member lost the right elsewhere"
  - "The search runs only for a text that has rested (debounced copy equals the immediate text), so right after a Member/Role switch no request is made for the old text"
  - "Clearing the chosen token keeps the typed query so the member can refine it; a successful grant clears both"

patterns-established:
  - "useAccessMutations is the home of every later access mutation; they pass accessMutationScope(serverId) as `scope` and end with invalidateAfterAccessChange"

requirements-completed: [D-04, D-05, D-10]

coverage:
  - id: D1
    description: "Member / Role segmented choice with the shared SearchPicker: 250ms debounce, at least one character, nickname and detail in the options, kind switch clears query and choice and keeps focus on the segmented control"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form searches members once, 250ms after typing, and lists nickname and detail"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form offers roles behind the segmented choice and grants View to the chosen role"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form never maps the member candidates through the role flavor after a switch"
        status: pass
    human_judgment: false
  - id: D2
    description: "Give access grants exactly sv:view with one call, no staging, no dialog; a second submit while pending is impossible"
    requirement: "D-04"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form grants View with exactly one call and ignores a second submit while pending"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form stages nothing: no review bar and no dialog"
        status: pass
    human_judgment: false
  - id: D3
    description: "After a grant the picker clears, focus returns to the picker input, the polite region announces, the new row appears closed, and the exact access, server, fleet and me keys plus the candidates prefix are invalidated"
    requirement: "D-05"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form clears the picker, returns focus to it, announces and shows the new row closed"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form refreshes the exact queries of the access change and the candidates of that kind"
        status: pass
    human_judgment: false
  - id: D4
    description: "Failure and pending states: 403, 404 and other statuses map to their messages without the server text, the choice stays, the picker, the segmented choice and the token clear are disabled while pending with aria-busy and the pending label"
    requirement: "D-05"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form maps a failed grant to a message by status, never the server text, and keeps the choice"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form disables the picker and the segmented choice while the grant is pending"
        status: pass
    human_judgment: false
  - id: D5
    description: "Give access is unavailable with a visible reason when sv:view is not in assignablePermissions (orchestrator decision 2)"
    requirement: "D-05"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#Access grant form keeps Give access unavailable with its reason when View cannot be assigned"
        status: pass
    human_judgment: false
  - id: D6
    description: "Settings read-only banner reads the access-only sentence without canModify but with canManageAccess, the existing sentence without both, and nothing with canModify (orchestrator decision 4)"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#tells a member who can only manage access that the Access panel stays operable"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#keeps the read-only sentence without canModify and without canManageAccess"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#shows no read-only banner when the member may change settings"
        status: pass
    human_judgment: false
  - id: D7
    description: "How the grant form looks and lays out at 320, 390 and 1280px in English and German (picker and button on one row from 521px, stacked with a full-width touch-height button below), and the spoken quality of the combobox"
    verification: []
    human_judgment: true
    rationale: "Layout and assistive-technology speech are not asserted by jsdom unit tests; the screen registry entry S-09 and the milestone verification (Deferred follow-up) cover them"

# Metrics
duration: 40min
completed: 2026-11-26
---

# Phase 71 Plan 05: Give access form and grant mutation Summary

**A manager gives a member or a role access with one Give access button on the shared SearchPicker (View only, no staging), the grant mutation sets the serial-scope and exact-invalidation pattern for every later access mutation, and the Settings read-only banner now tells the truth when the Access panel is still operable.**

## Performance

- **Duration:** about 40 min
- **Tasks:** 2 (1 tracer, 1 auto/TDD)
- **Files changed:** 11 (2 created, 9 modified)
- **Commits:** 2 (measured from the plan head ledger)

## Accomplishments

- **Grant form (D-05, D-10):** `AccessGrantForm` is a presentational `form` labelled by its `h3`, with the description, the segmented `RadioGroupField` (legend hidden), the `SearchPicker`, the primary submit `ActionButton` and an error `StatePanel` (an alert) that exists only while an error exists. Both flavors resolve their keys through a `Map` lookup. The button keeps focus and shows its reason (`Choose a member first.`, `Choose a role first.`, or `You can only give access while you hold View on this server yourself.`).
- **Grant mutation (D-04):** `useGrantAccess` runs in `accessMutationScope(serverId)` and calls `changeServerUserAccess` or `changeServerRoleAccess` with `sv:view` and `true`, one call, nothing staged. On success it refreshes the candidates prefix of that kind and calls `invalidateAfterAccessChange` (access, server and fleet by exact key, plus `queryKeys.me`). A 403 refreshes the same queries.
- **Panel wiring:** `ServerAccessPanelContent` owns kind, query and choice; the candidates query (`staleTime` 20s, `keepPreviousData`, query `signal`) runs 250ms after the last keystroke for at least one character; `status` follows the immediate text. After a success the picker clears, focus returns to the input once the grant has settled, the panel-local polite region says `Access given to {{name}}.`, and the refetched list shows the new row closed.
- **Banner (orchestrator decision 4):** without `canModify` the Settings banner reads `Server_Change_ReadOnly_AccessOnly` when `canManageAccess` is true, otherwise the existing `Server_Change_ReadOnly`; no banner with `canModify`; the archived sentence is unchanged.
- **Layout:** `access.css` lays the picker and the button on one grid row from 521px (button offset to the input, not the label) and stacks them at `max-width: 520px` with a full-width button at `--utf-control-height-touch`; touch height at 800px and below. Tokens only; the style guard passes.
- **Locale:** all grant, search, picker, error and banner keys in English and German.

## Task Commits

1. **Task 1: tracer, give a member access from the picker to the refreshed list** - `47e7e8ef` (feat)
2. **Task 2: role flavor, assignability, failure and pending states, layout and the banner** - `b797daa8` (feat)

## Verification

- `tests/hosting/server-access-panel.test.tsx` (30 tests) and `tests/hosting/settings-section.test.tsx` pass, as do `tests/styles`, `tests/locales`, `tests/ratchets` and `tests/search-picker.test.tsx` (404 passed in the targeted run).
- `locales:check` 0 failures; `locales:unused` 0 failures (47 warnings: the 46 pre-existing ones plus `Picker_NoMatches`, see Known Stubs); `lint` (max-warnings 0), `typecheck` (`tsc -b`) and prettier (`--end-of-line auto`, whole `src` and `tests`) clean.
- Acceptance greps: `<SearchPicker` 1, `exact: true` 4, `scope:` 1, `Server_Access_Grant_Submit` (en) 1, `Person20Regular` in the mock 1, `Server_Change_ReadOnly_AccessOnly` in `SettingsSection.tsx` 1, `Server_Access_Grant_CannotGiveView` (de) 1, `max-width: 520px` in `access.css` 1.
- Tracer gate: verify is automated only (`human_verify_mode` end-of-phase, auto advance on), re-run end to end before expansion and passed; tracer verified end-to-end, expanded.
- `npx vitest run --maxWorkers=4` (whole project): 219 files, 3669 passed, 2 skipped, 0 failed.

- `contracts:check` clean and `build:assets` built (both rewrite `src/generated/api.ts` with line-ending-only changes, restored with `git checkout`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A kind switch queried the stale search text**
- **Found during:** Task 2 (the new role-flavor test)
- **Issue:** `enabled` was `debounced length >= 1`. Right after switching Member to Role the debounced copy still held the old text, so typing one character fired a role search for the old text.
- **Fix:** the query is enabled only while the debounced text equals the immediate trimmed text; the status shows loading in between.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx`, test `offers roles behind the segmented choice` asserts exactly one role request
- **Committed in:** `b797daa8`

**2. [Rule 2 - Missing critical functionality] Candidate results tagged with their kind**
- **Found during:** Task 1 design
- **Issue:** `placeholderData: keepPreviousData` keeps the previous key's data across a kind switch, so member candidates could be mapped as roles (a `roleId` that does not exist).
- **Fix:** the query function returns `{ kind, items }` and the options are mapped only when that kind equals the current kind; covered by `never maps the member candidates through the role flavor after a switch`.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx`
- **Committed in:** `47e7e8ef`

**3. [Rule 3 - Blocking] Phase 65 locale vocabulary list pins every `Server_Change_*` key**
- **Found during:** Task 2
- **Issue:** `tests/locales/server-keys.test.ts` requires each `Server_Change_*` key to be in `phase65RequiredKeys` and pins the count at 163, so `Server_Change_ReadOnly_AccessOnly` failed it.
- **Fix:** listed the key and moved the pinned count to 164 (the same pattern earlier phases used).
- **Files modified:** `sources/ReactApp/tests/locales/server-keys.test.ts`
- **Committed in:** `b797daa8`

**4. [Plan clarification] Locale keys of Task 2 landed with Task 1**
- `Server_Access_Grant_Submitting`, `Server_Access_Grant_CannotGiveView` and the three `Server_Access_Error_*` keys are used by the form file that Task 1 creates (typed `TranslationKey`), so they were added in the Task 1 commit; `Server_Change_ReadOnly_AccessOnly` stayed in Task 2. `grantFailureKey` therefore also landed in `useAccessMutations.ts` with Task 1, and Task 2 tests cover it.

---

**Total deviations:** 3 auto-fixed (1 Rule 1, 1 Rule 2, 1 Rule 3) plus 1 clarification. **Impact:** none on scope; the search fix and the kind tag prevent wrong requests and wrong mappings.

## Authentication Gates

None.

## Issues Encountered

- The shell wrapper refused heredocs and compound `git` commands in the worktree; they were split into plain commands or done through the edit tools.
- The orchestrator note named the commit attribution as Claude Opus 5.5 while the session attribution reminder names Claude Sonnet 5.5; the session reminder was followed and the first commit amended to match.

## Known Stubs

- `Picker_NoMatches` (the UI-SPEC "default no-match text") is added to both locale files as the plan lists, but nothing references it yet because the picker takes its no-match text from the caller (`Server_Access_Search_MemberEmpty` and `_RoleEmpty` here, `Server_CostOwner_NoOptions` in 71-07). It shows as one extra `unused-warn` (a warning, not a failure). Remove it or give it a consumer in 71-07.

## Threat Flags

None. T-71-12 is mitigated (the button is unavailable without View in `assignablePermissions`, and a 403 maps to the forbidden message); T-71-13 (250ms debounce, one character, abortable query signal, client asks for 20); T-71-14 (exact refetch of server, fleet and the signed-in member after every grant). Names render as React text inside `bdi` in the picker. No package was installed (T-71-SC).

## Cross-plan findings

- **XPF-71-05-1:** `useAccessMutations.ts` is the home for the remaining access mutations (71-08 switches, remove all). Each must pass `scope: accessMutationScope(serverId)` and finish with `invalidateAfterAccessChange(queryClient, serverId)`; the grant already refreshes the candidates of its kind, and a revoke should do the same for both kinds (a removed subject becomes a candidate again).
- **XPF-71-05-2 (applies XPF-71-02-1):** the 42px high clear control of the chosen token was accepted unchanged. It meets the 24px AA target size; the grant button and the radios carry the 44px touch height at 800px and below. 71-12's audit can revisit it.
- **XPF-71-05-3 (applies XPF-71-02-2):** `status` is computed from the immediate trimmed text, with the debounced copy only deciding whether the request may run. 71-07 should reuse this shape (and the rule that the search runs only for a rested text).
- **XPF-71-05-4:** D-15 (panel unmounts after the member loses `canManageAccess`, focus to the Settings heading, `Server_Access_Toast_SelfLost`, redirect with `Server_Access_Toast_ViewLost`) is not part of this plan. The grant cannot remove the caller's own right, so nothing here needs it; the plan that adds revoke (71-08 or later) must implement it on top of `invalidateAfterAccessChange`.
- **XPF-71-05-5:** `tests/locales/server-keys.test.ts` pins every `Server_Change_*` key and the count (now 164); later plans that add a `Server_Change_*` key must list it there. Keys of other prefixes are unaffected.
- **XPF-71-05-6:** `AccessGrantForm` lays out with a three-to-one grid column split (`minmax(0, 3fr) minmax(min-content, 1fr)`) so a long unavailable reason wraps in the button column instead of squeezing the picker; unverified in a real browser (no jsdom layout), so the S-09 screenshots at 521px to 800px, English and German, should confirm it.

## Next Phase Readiness

The grant is complete and tested; 71-08 can add the switches and revoke on `useAccessMutations.ts` with the same scope and invalidation helpers. No blockers.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/components/server/AccessGrantForm.tsx
- FOUND: sources/ReactApp/src/features/hosting/useAccessMutations.ts
- FOUND commit 47e7e8ef, FOUND commit b797daa8 (ancestors of HEAD)
