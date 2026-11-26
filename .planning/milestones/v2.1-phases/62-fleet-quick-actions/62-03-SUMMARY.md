---
phase: 62-fleet-quick-actions
plan: 03
subsystem: ui
tags: [react, i18next, locales, vitest, playwright, cost-confirmation, navigation]

requires:
  - phase: 61-foundations-localization-member-access
    provides: locale gate (locales:check), shared CostConfirmationProvider, member-access claim helpers, tokens.css layer
provides:
  - All Phase 62 fleet strings in EN and DE (Server_Fleet_*), typed for t()
  - Server section renamed to Servers (EN) and Server (DE) through Hosting_Nav_Servers and Hosting_Servers_Title
  - --utf-fleet-card-min 22rem layout token
  - D-02 proof test for bare sv and sv:view members through CommunityLayout and CommandCenterLayout
  - Exported ownerFor cost owner placeholder mapping
  - Optional onSubmitted callback on CostConfirmationRequest, run before invalidations and contained on throw
affects: [62-fleet-card, 62-fleet-page, 62-fleet-search, 62-live-updates, phase-69-cleanup]

actuals:
  tokens: 9234
  tasks: 3
  commits: 3

plan_head_before: f5a02059b3712fe3c459b4bdc6cc099e662ca41b
plan_head_after: 92d6465963e200de54f181e53c1a32197c57eb38

tech-stack:
  added: []
  patterns:
    - "Locale additions via a one-off Node assign, localeCompare sort, JSON.stringify(_, null, 2) plus newline"
    - "Requester callbacks inside the shared confirmation run through a try/catch guard before toast and invalidations"

key-files:
  created:
    - sources/ReactApp/tests/hosting/server-section-entry.test.tsx
  modified:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/styles/tokens.css
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/e2e/public-shell.spec.ts
    - sources/ReactApp/src/components/server/costConfirmationModel.ts
    - sources/ReactApp/src/components/server/useCostConfirmation.ts
    - sources/ReactApp/src/components/server/CostConfirmationProvider.tsx
    - sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx
    - sources/ReactApp/tests/hosting/cost-confirm-model.test.ts

key-decisions:
  - "62-03: a throwing onSubmitted is swallowed silently inside notifySubmitted (no console output, src has no console usage); the toast, the five invalidations and the close always run"
  - "62-03: e2e assertions on the renamed labels use exact matching because Servers and Server substring-match card titles and the new German description contains was läuft"
  - "62-03: identical-allowed.json needed no entry; every new German value differs from English"

patterns-established:
  - "Fleet button label keys are checked against a bare-label ban list in both languages in server-keys.test.ts"
  - "D-02 entry proofs query links inside the translated navigation landmarks, never the whole page"

requirements-completed: [ACC-02, FLT-01, FLT-04]

coverage:
  - id: D1
    description: "Every fleet string from the UI-SPEC copy table exists in EN and DE with matching placeholders and plural pairs; the nav and title read Servers and Server; the description has no admin wording"
    requirement: "FLT-01"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/locales/server-keys.test.ts#fleet locale vocabulary"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false
  - id: D2
    description: "Bare sv and sv:view members see the Command Center header link and the Servers command navigation item in EN and DE; the community header has no Servers link; mp members do not see Servers"
    requirement: "ACC-02"
    verification:
      - kind: integration
        ref: "sources/ReactApp/tests/hosting/server-section-entry.test.tsx"
        status: pass
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/public-shell.spec.ts#renders the permission-filtered command shell for an administrator"
        status: pass
    human_judgment: false
  - id: D3
    description: "useCostConfirmation().request accepts onSubmitted, run once after a successful action and before the invalidations, never on dismiss, rejection or a refused confirm, and contained on throw"
    requirement: "FLT-04"
    verification:
      - kind: integration
        ref: "sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx#onSubmitted"
        status: pass
    human_judgment: false
  - id: D4
    description: "ownerFor is exported and maps Assigned owner to hidden, Unknown owner and empty or missing names to unknown, and any other value to name"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/cost-confirm-model.test.ts#ownerFor"
        status: pass
    human_judgment: false
  - id: D5
    description: "--utf-fleet-card-min: 22rem token added with no new colour pair"
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/styles/tokens-contrast.test.ts"
        status: pass
    human_judgment: false

duration: 10min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 03: Fleet Vocabulary, Entry Proof and Confirmation Hook Summary

**This plan adds all 34 fleet strings in English and German and renames the server section to Servers / Server. It also adds a test proving that members with only a bare sv or sv:view claim reach the section, exports `ownerFor`, and adds an optional `onSubmitted` callback to the shared cost confirmation. The callback runs before the invalidations, and an exception it throws cannot stop the toast, the invalidations or the close.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-11-19T08:40:47Z
- **Completed:** 2026-11-19T08:51:19Z
- **Tasks:** 3
- **Files modified:** 11 (1 created, 10 modified)

## Accomplishments

- EN and DE locale files carry every key from the UI-SPEC "New keys" table. That includes the `_one`/`_other` pairs for ResultCount, CostLine and ChangedMore, plus the three changed values (nav, title, description). `locales:check` reports 3137 keys, 0 failures and 0 warnings.
- `server-section-entry.test.tsx` covers the entry path for bare `sv` and `sv:view` members through the real `CommunityLayout` and `CommandCenterLayout`, in both languages. A temporary mutation that dropped `sv:view` from `canAccessGameServers` made the test fail, so it can catch a regression.
- `CostConfirmationRequest.onSubmitted` is stored on the active request and runs first in the confirm mutation's `onSuccess`. A repeated request for the same server and action keeps the first callback.
- `ownerFor` is exported with TSDoc, so the fleet card and the search haystack can share it.

## Task Commits

1. **Task 1: Locale strings, rename and the card-width token** - `1f275617` (feat)
2. **Task 2: Prove the bare sv and sv:view entry path** - `9bdba4ca` (test)
3. **Task 3: Export ownerFor and add the onSubmitted hook** - `92d64659` (feat)

TDD: Tasks 1 and 3 had a failing RED run before the implementation (9 and 10 failures). Each task was committed as a single unit, as the plan's commit messages prescribe. Task 2 tests behavior that already existed, so there was no RED run. The mutation check above confirms it can fail.

## Files Created/Modified

- `sources/ReactApp/src/locales/en.json`, `de.json`: fleet vocabulary and the renamed section values
- `sources/ReactApp/src/styles/tokens.css`: `--utf-fleet-card-min: 22rem`
- `sources/ReactApp/tests/locales/server-keys.test.ts`: required fleet keys, plural rendering, the count option on a key without plural forms, rename equality, a check for admin wording, and a check for bare button labels
- `sources/ReactApp/tests/e2e/public-shell.spec.ts`: exact matches for the renamed labels
- `sources/ReactApp/tests/hosting/server-section-entry.test.tsx`: the D-02 / ACC-02 proof
- `sources/ReactApp/src/components/server/costConfirmationModel.ts`: exported `ownerFor`
- `sources/ReactApp/src/components/server/useCostConfirmation.ts`: `onSubmitted?: (() => void) | undefined`
- `sources/ReactApp/src/components/server/CostConfirmationProvider.tsx`: `ActiveRequest.onSubmitted` and the `notifySubmitted` guard
- `sources/ReactApp/tests/hosting/cost-confirmation-provider.test.tsx`, `cost-confirm-model.test.ts`: callback and mapping cases

## Decisions Made

- A throwing `onSubmitted` is swallowed silently. The callback only updates the requesting surface, and nothing in `src` writes to the console.
- No `identical-allowed.json` entry was needed.
- Legacy keys stay in the locale files, as the plan's decision on legacy keys requires (until the Phase 69 cleanup).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The renamed labels broke hard-coded e2e assertions**
- **Found during:** Task 1
- **Issue:** `public-shell.spec.ts` asserted a link named "Game servers" and a heading named "Spielserver". The new German description ("was läuft") also substring-matched `getByText("Läuft")`, which caused a strict-mode violation.
- **Fix:** The spec now asserts `{ name: "Servers", exact: true }`, `{ name: "Server", exact: true }` and `getByText("Läuft", { exact: true })`.
- **Files modified:** `sources/ReactApp/tests/e2e/public-shell.spec.ts`
- **Verification:** The full Playwright suite passes (47 tests).
- **Committed in:** `1f275617`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** The fix was needed to keep the e2e suite green after the rename. No production scope was added.

## Issues Encountered

- `format:check` flags three files that plan 62-01 committed and that are already unformatted at HEAD (`FleetLiveUpdatesProvider.tsx`, `fleet-fixtures.test.ts`, `fleet-signal-tracer.test.tsx`). This is out of scope and is logged in `deferred-items.md`. Every file touched here passes `prettier --check`.

## Verification

- `npm test`: 95 files, 760 tests passing
- Playwright: 47 tests passing
- `locales:check`: 0 failures, 0 warnings
- `typecheck`, `lint --max-warnings 0`: clean
- `git diff --exit-code -- sources/Common/Ui/routes.json`: no change
- No changes to `CommunityLayout.tsx`, `CommandCenterLayout.tsx` or `commandNavigation.tsx` since `de6261e4`

## User Setup Required

None. No external service configuration is required.

## Next Phase Readiness

- Fleet UI plans can now use the typed `Server_Fleet_*` keys, `--utf-fleet-card-min`, `ownerFor` and `request({ ..., onSubmitted })`.

## Self-Check: PASSED

- FOUND: sources/ReactApp/tests/hosting/server-section-entry.test.tsx
- FOUND: 1f275617, 9bdba4ca, 92d64659 (ancestors of HEAD)

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*
