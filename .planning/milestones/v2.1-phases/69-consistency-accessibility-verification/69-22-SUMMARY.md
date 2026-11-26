---
phase: 69-consistency-accessibility-verification
plan: 22
subsystem: ui
tags: [react, public-hero, state-views, status-tones, filter-chips, css-tokens, vitest]

requires:
  - phase: 69-01
    provides: "PageHeader, PageStack and the --utf-* spacing tokens the hero rhythm uses"
  - phase: 69-03
    provides: "LoadingState, EmptyState, ErrorState, the tone lexicon and the empty-state keys"
  - phase: 69-04
    provides: "The public-hero family ratchet and the ESLint raw-control restriction"
provides:
  - "One PublicHero h1 as the first heading on home, upcoming operations, unit and Verein pages, in every state"
  - "Operation and attendance badges on lexicon tones, page-local tone functions removed"
  - "Upcoming operations without a raw control or button look-alike; mode switch and status filter on FilterChipGroup"
  - "Verein document pages (Satzung, minutes detail) render loading, error and not-found below the same hero h1"
  - "Token-only hero lede, hero actions and first-block rhythm in publicEvents.css; public-hero family ratchet at zero"
affects: [69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: 8ed8f72ee79bcd483846cc634447dc067fc8f662
commits: 5

actuals:
  tokens: 33000
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Public hero pages import publicEvents.css for the token rhythm; two-class selectors win over the single-class hero rules in styles.css"
    - "DocumentPageFrame: one hero plus a page-wrap body, so every state of a document page shows the same single h1"
    - "Calendar ActionButtons keep their legacy classes and restore geometry with two-class overrides in the page stylesheet"

key-files:
  created:
    - sources/ReactApp/tests/public-hero-pages.test.tsx
  modified:
    - sources/ReactApp/src/components/PublicHero.tsx
    - sources/ReactApp/src/pages/UpcomingEventsPage.tsx
    - sources/ReactApp/src/pages/HomePage.tsx
    - sources/ReactApp/src/pages/UnitPage.tsx
    - sources/ReactApp/src/pages/VereinPages.tsx
    - sources/ReactApp/src/pages/publicEvents.css
    - sources/ReactApp/tests/ratchets/family.public-hero.json

key-decisions:
  - "The mode switch and the status filter use FilterChipGroup (aria-pressed buttons) instead of the former role=tab buttons, because FilterChipGroup is the closed-list control for single-choice chips and the tab widget had no tab panels."
  - "The operations skeleton is replaced by LoadingState (D-03); its CSS is removed."
  - "The Verein document header is a PublicHero (compact, single column) with the version badge and date in the lede; the loading, error and not-found states render inside the same frame under the same h1."
  - "Request-status badges on the Verein page use membershipRequestTones; the no-request case resolves to neutral (it was info) because the lexicon reserves info for in-progress states."
  - "Hero lede and actions are token-ized from publicEvents.css with two-class selectors because the hero rules live in styles.css, which only 69-01 and 69-33 may edit."

patterns-established:
  - "Hero pages keep their title scale (E-01) and take only lede, actions and first-block spacing from tokens (D-AUTO-4)"

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "Home, upcoming operations, unit and Verein pages render exactly one h1 through PublicHero, first in DOM order, including loading, error and not-found states"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/public-hero-pages.test.tsx (upcoming, home, unit, Verein describes)"
        status: pass
    human_judgment: false
  - id: D2
    description: "PublicHero keeps its marketing title scale and the slot order kicker, h1, lede, actions"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/public-hero-pages.test.tsx (hero h1 is the first heading in every page)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Operation and attendance badges take their tone from statusTones; unknown attendance renders neutral"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/public-hero-pages.test.tsx#takes attendance badge tones from the lexicon and renders unknown values neutral"
        status: pass
      - kind: unit
        ref: "tests/public-hero-pages.test.tsx#renders one h1 through the hero and lexicon tones for operation and attendance badges"
        status: pass
    human_judgment: false
  - id: D4
    description: "No raw control or button look-alike on upcoming operations; shared state views with a real retry on the four pages"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/public-hero-pages.test.tsx#renders no raw control and no button look-alike outside the shared controls"
        status: pass
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts (family.public-hero.json is empty)"
        status: pass
    human_judgment: false
  - id: D5
    description: "The first block below every hero starts at the --utf-space-lg rhythm; hero lede and actions use spacing tokens"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/public-hero-pages.test.tsx#starts the first block of the %s page at the large spacing token"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/public-events.spec.ts (2 passed, chromium)"
        status: pass
    human_judgment: false
  - id: D6
    description: "German visual balance of the hero and public pages"
    requirement: CON-03
    verification: []
    human_judgment: true
    rationale: "Visual balance of long German copy on image heroes is a design judgment; the backstop is a human screenshot review at milestone end"

duration: 75min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 22: Public Hero Pages Summary

**Every public hero page now has one PublicHero h1 in all of its states, lexicon-driven operation and attendance tones, shared controls and state views, and a token-only hero lede, actions and first-block rhythm, with the public-hero ratchet at zero.**

## Performance

- **Duration:** about 75 min
- **Completed:** 2026-11-25T13:10:00Z
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 7 modified, 1 created (no file deleted)

## Accomplishments

- **Upcoming operations (tracer):** the hand-written h1 moved into `PublicHero` (compact, single column; the back link is now a glass `ActionLink` in the actions slot, so the slot order is kicker, h1, lede, actions). The calendar and board mode switch and the status filter are `FilterChipGroup`s; refresh, month steps, week strip, day numbers, event chips and agenda rows are `ActionButton`s; "View slots" links are `ActionLink`s. Loading, error (with a real `refetch` retry) and empty views are the shared state views; the skeleton and every legacy view-toggle, filter-chip and back-link rule are gone from `publicEvents.css`. Readiness and attendance badges use `operationTones` and `attendanceTones`; the page-local `attendanceTone` is gone. Tracer verified end to end (Vitest, ratchet, style guard) before expanding.
- **Home and Verein pages:** `operationTone` and `attendanceTone` are gone from the home page (`toneOf` lookups); its hero aside and both preview sections use shared loading, error and empty views with retry. `VereinPages` has no hand-written h1: the document header is a `PublicHero`, and the Satzung and minutes-detail pages render loading, error and not-found states inside the same frame under the same h1 (they had no header before). The minutes list keeps `PageHeader`, gets a `PageStack` body and shared states. Request-status badges use `membershipRequestTones`.
- **Unit page and rhythm:** the unit page already rendered one hero h1 with `h2` sections; it now carries a test. `publicEvents.css` gains token-only rules for the hero lede (`--utf-space-md` over `--utf-space-lg`, `--utf-measure` width), the hero actions gap (`--utf-space-sm`) and `padding-top: var(--utf-space-lg)` for the first block below any hero; the four hero pages import the sheet. The `public-hero` ratchet family is empty.

## Task Commits

1. **Task 1 (tracer): upcoming operations, one hero heading, lexicon tones, shared controls** - `a52920b3` (refactor)
2. **Task 2: home and Verein pages on PublicHero with lexicon tones** - `a3006d61` (refactor)
3. **Task 3: hero rhythm tokens, unit page test, ratchet at zero** - `57830162` (refactor)

Follow-up commits: this summary (`f22aaca3`, docs) and `8ed8f72e` (fix), which types the status filter chips after the final full `tsc -b` run reported TS2322 on `FilterChipGroup` inference. Commits were created with hooks enabled; `commits: 5` is measured from the plan ledger (3 task commits, the summary and the fix).

## Files Created/Modified

- `sources/ReactApp/tests/public-hero-pages.test.tsx` - 19 tests: heading, tone, state, control and rhythm assertions for the four pages
- `sources/ReactApp/src/pages/UpcomingEventsPage.tsx`, `HomePage.tsx`, `UnitPage.tsx`, `VereinPages.tsx` - converted pages
- `sources/ReactApp/src/components/PublicHero.tsx` - JSDoc documenting the slot order and the E-01 exception (no behavior change; the slot order was already correct)
- `sources/ReactApp/src/pages/publicEvents.css` - dead rules removed, calendar control geometry overrides, token rhythm rules
- `sources/ReactApp/tests/ratchets/family.public-hero.json` - `files` is empty

## Decisions Made

See `key-decisions`. In short: FilterChipGroup replaces the tab-role mode switch, the skeleton is replaced by `LoadingState`, the Verein document header is a hero, and hero lede and actions are token-ized with two-class selectors because `styles.css` is closed to this plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Verein document pages had no h1 in loading, error and not-found states**
- **Found during:** Task 2
- **Issue:** `StatutesPage` and `MinuteDetailPage` returned a bare `StatePanel` in a `narrow-page` before any heading, so those states had no h1 (the same defect class as research F12).
- **Fix:** Introduced `DocumentPageFrame` (hero plus body) used by every state and by the loaded document.
- **Files modified:** `sources/ReactApp/src/pages/VereinPages.tsx`
- **Verification:** `tests/public-hero-pages.test.tsx` (Verein pages describe)
- **Committed in:** `a3006d61`

**2. [Rule 1 - Bug] The home hero aside showed "no operations" while the query was still loading**
- **Found during:** Task 2
- **Issue:** The aside showed the empty summary whenever there was no primary operation, including during loading.
- **Fix:** The aside now shows `LoadingState`, `ErrorState` or `EmptyState` by query state.
- **Files modified:** `sources/ReactApp/src/pages/HomePage.tsx`
- **Committed in:** `a3006d61`

**3. [Plan scope] Two listed test files needed no change**
- `tests/upcoming-events.test.ts` (pure helper tests) and `tests/e2e/public-events.spec.ts` (no broken locator) are unchanged; the page behavior is covered by the new `public-hero-pages.test.tsx`.

---

**Total deviations:** 2 auto-fixed (1 missing critical, 1 bug), 1 scope note. **Impact:** no scope creep; both fixes are in files the plan owns.

## Cross-plan findings

| Id | File and line | Problem | Proposed fix |
|----|---------------|---------|--------------|
| XPF-69-22-1 | `src/locales/en.json:2657`, `de.json:2657` (`Pages_Unit_Beschluesse_DocumentId_NotFoundDescription`) | The value is an untranslated placeholder ("Document ID Not Found Description", German "Dokument ID Nicht Found Beschreibung") and is the body of the minute not-found state. The plan may not edit locale files. | 69-33: replace with a sentence that names the next step (for example "Go back to the archive to see the published documents.") in both languages. |
| XPF-69-22-2 | `src/locales/*.json` | After this plan nothing references `Pages_Events_Upcoming_NoScheduledInMonth`, `Pages_Public_Home_UpcomingSummaryEmpty`, `Pages_Public_Home_NoFieldReportsPublished`, `Pages_Unit_Satzung_Index_NotFoundTitle` and `Pages_Unit_Satzung_Index_NotFoundDescription`. | 69-33: retire the five keys together with its unused-key check. |
| XPF-69-22-3 | `src/styles.css` (`.public-hero h1`, `.public-hero__*`, `.governance-document header`, `.governance-document h1`, `.filter-chips`, `.view-toggle`) | Hero and governance rules stay raw in `styles.css`; `.governance-document header`, `.governance-document h1`, `.filter-chips` and `.view-toggle` have no reader left on these pages. | 69-33: record the reason "Tier C hero (D-AUTO-4)" for the hero rules, and delete the unreferenced selectors when `.filter-chips`/`.view-toggle` have no other reader. |

## Issues Encountered

- `node_modules` was missing in the fresh worktree; a directory junction to the main checkout's install was created (git-ignored, nothing installed).
- `npm run build:assets` (run by the Playwright web server) rewrote `src/generated/api.ts` with line endings only; it was restored with `git checkout` and is not committed.
- The e2e run used a temporary, uncommitted config on private ports 4373 and 4374 (deleted afterwards) so a sibling executor's server could not be reused by mistake.
- Source files in this checkout use CRLF; edits kept the existing endings and Prettier ran with `--end-of-line auto` on the touched files only.

## Verification

- `npx vitest run tests/public-hero-pages.test.tsx tests/upcoming-events.test.ts tests/ratchets tests/styles tests/locales tests/app`: 17 files, 338 passed, 1 skipped (the opt-in ratchet writer).
- `npm run lint`: clean. `npx tsc -b` (real exit code captured, not piped): exit 0 after `8ed8f72e`.
- `playwright test tests/e2e/public-events.spec.ts --project=chromium` (private ports): 2 passed.
- Acceptance greps: `<h1` in `UpcomingEventsPage.tsx` 0, raw controls in it 0, `function attendanceTone` 0, `function (operationTone|attendanceTone)` in `HomePage.tsx` 0, `<h1` in `VereinPages.tsx` 0, `family.public-hero.json` files 0, `--utf-space-lg` in `publicEvents.css` 2.
- Deferred follow-up: human review of the German visual balance of the hero pages and of the restyled calendar controls at milestone end (backstop).

## Known Stubs

None.

## Threat Flags

None. No data is fetched or rendered that was not before (T-69-28); no package was installed (T-69-SC). No new stylesheet was added, so `tests/styles/legacy-ratchet.json` needs no new entry; its `pages/publicEvents.css` ceilings were not edited and the sheet's counts did not rise.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/public-hero-pages.test.tsx`.
- Task commits `a52920b3`, `a3006d61`, `57830162` and the fix `8ed8f72e` are ancestors of HEAD.
- `files_deleted:` none.
