---
phase: 69-consistency-accessibility-verification
plan: 25
subsystem: testing
tags: [playwright, vitest, german-walk, l10n, intl, plural, leak-detection]

requires:
  - phase: 69-03
    provides: "en.json and de.json locale files and the identical-allowed.json allow-list the leak candidates derive from"
  - phase: 69-05
    provides: "mobile-390 and mobile-320 Playwright projects that run every spec under tests/e2e/audit"
  - phase: 69-06
    provides: "installApp strict mock layer and the serverScreens registry the walk iterates"
provides:
  - "tests/e2e/support/germanLeak.ts: leakCandidates, findLeaks, collectVisibleTexts, assertNoGermanLeak, realLeakCandidates, functionWords"
  - "tests/audit/german-leak.test.ts: unit tests with English and German samples (non-vacuity)"
  - "tests/e2e/audit/german-walk.spec.ts: registry-driven German walk plus German formatting assertions"
affects: [69-26, 69-27, 69-28, 69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: 34446394114bda4eb3c818d66a566ca1249dc7b4

actuals:
  tokens: 6800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Leak candidates computed once from the real locale files: English values whose German differs, minus allow-listed keys, minus values found inside German text, minimum two words or six characters, template values contribute their literal segments"
    - "Word-boundary phrase matching so a candidate never matches inside a longer word"
    - "Known leaks recorded as exact texts (registry known finding with rule german-leak, or a recordedLeaks table with a tracking id) and surfaced as test annotations"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/germanLeak.ts
    - sources/ReactApp/tests/audit/german-leak.test.ts
    - sources/ReactApp/tests/e2e/audit/german-walk.spec.ts
  modified: []

key-decisions:
  - "Template values (with {{placeholders}}) contribute their literal segments as candidates, because a rendered text never holds the placeholder; the German-contained filter still applies per segment"
  - "Candidates match on word boundaries, not raw substrings, so 'active' does not match inside 'interactive'"
  - "assertNoGermanLeak fails on a page with no visible text, so a walk over an empty or fully excluded page cannot pass vacuously"
  - "assertNoGermanLeak takes exact leaking texts as known findings and returns the subtracted ones; the walk maps registry known findings with rule german-leak onto it and annotates every subtraction"

patterns-established:
  - "Slice plans silence a German leak they cannot fix with a KnownFinding { rule: 'german-leak', target: <exact text> } on the state"
  - "German walk tests are titled '<id> (de) @<slice>' and run in chromium, mobile-390 and mobile-320 because the spec lives under tests/e2e/audit"

requirements-completed: [L10N-03]

coverage:
  - id: D1
    description: "Deterministic, explainable German leak detector: English locale value (word boundary, two words or six characters, differs from German, not allow-listed, not inside German text) or two English function words, outside lang other than de, bdi, code, kbd, time and data-user-content"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/audit/german-leak.test.ts (17 tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Non-vacuity: English samples are detected by the unit test and by the browser test that poses the English S-02 page as German; German samples and shared short words pass"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/audit/german-leak.test.ts#english samples are detected (non-vacuity)"
        status: pass
      - kind: e2e
        ref: "tests/e2e/audit/german-walk.spec.ts#detects English text in the browser (non-vacuity)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Registry-driven German walk over every entry with audit.german, html lang de, empty unexpected-request list, in chromium, mobile-390 and mobile-320"
    requirement: L10N-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/german-walk.spec.ts#S-02 (de) @fleet (3 projects)"
        status: pass
    human_judgment: false
  - id: D4
    description: "German formatting: fleet count selects the _one and _other plural forms (1 Server verursacht, 2 Server verursachen), accrued cost reads 0,42 EUR with the euro sign after the number, the as-of time is the German short date, the live badge reads a German relative time"
    requirement: L10N-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/german-walk.spec.ts#german formatting (3 tests, 3 projects)"
        status: pass
    human_judgment: false

duration: 24min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 25: German leak detector and German walk Summary

**A word-boundary German leak detector built from the real locale files (English values that differ in German, plus a seventeen-word function list without "an" and "will"), proven non-vacuous in Vitest and in the browser, a registry-driven German walk in three Playwright projects, and German plural, currency, date and relative-time assertions.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-11-25T12:35:00Z
- **Completed:** 2026-11-25T12:59:00Z
- **Tasks:** 2
- **Files modified:** 3 (all created)

## Accomplishments

- `germanLeak.ts` exports `leakCandidates` (pure), `findLeaks` (pure), `collectVisibleTexts` (DOM walk of text nodes and `aria-label`, `title`, `placeholder`, `alt` and option `label`, skipping hidden and excluded subtrees) and `assertNoGermanLeak`. The real locale files yield well over a thousand candidates.
- The unit test uses real locale values, the plan's samples ("The servers could not be loaded", "this is your server"), German samples ("Server starten", "Konfigurationsprofile", a German sentence containing "will" and "an"), and shows that `Name` and `Status` are never flagged.
- `german-walk.spec.ts` walks every registry entry with `audit.german` (today S-02; Wave 3 slices add theirs for free), asserts `html lang="de"`, no page errors and no unexpected API request, and has a count guard plus a browser non-vacuity test.
- Formatting block: fleet cost line `1 Server verursacht` versus `2 Server verursachen` (proves `_one` and `_other` selection, since the noun is identical), accrued cost `0,42 €`, as-of line `Stand <de-DE short date and time>` in Europe/Berlin, and the activity badge relative time (`jetzt` or `vor N Sekunden/Minuten`).

## Task Commits

1. **Task 1: Tracer - leak detector proven on samples and on the German fleet** - `85e23705` (test)
2. **Task 2: German formatting and plural assertions** - `34446394` (test)

**Plan metadata:** committed with this SUMMARY (docs: complete plan)

## Files Created/Modified

- `sources/ReactApp/tests/e2e/support/germanLeak.ts` - detector (Playwright types only, `node:fs` for the locale files)
- `sources/ReactApp/tests/audit/german-leak.test.ts` - Vitest unit tests
- `sources/ReactApp/tests/e2e/audit/german-walk.spec.ts` - walk and formatting specs

## Decisions Made

See `key-decisions` above. No new stylesheet, so no `tests/styles/legacy-ratchet.json` entry is needed. No file was deleted (`files_deleted:` none).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Plan mismatch] Sample "Start the server now" is not flagged**
- **Found during:** Task 1 (unit test design)
- **Issue:** The plan lists it as flagged against the real candidate set. No English locale value occurs inside it and it holds only one function word ("the"), so the F5 rules (which the plan also mandates) cannot flag it without loosening them.
- **Fix:** The unit test asserts it is NOT flagged by the function-word rule alone, documenting the trade-off, and covers the intent with real locale values (verbatim and upper-cased inside other text) plus "The servers could not be loaded" (the, not).
- **Files modified:** `tests/audit/german-leak.test.ts`

**2. [Rule 3 - Type mismatch] `knownFindings` type**
- **Found during:** Task 1
- **Issue:** The interface says `readonly string[]`, the registry state carries `KnownFinding` objects.
- **Fix:** The helper takes exact leaking texts; the walk maps registry known findings with rule `german-leak` (target is the exact text) onto it, and `assertNoGermanLeak` returns the subtracted texts (a `Promise<string[]>` instead of `Promise<void>`, compatible with the interface) so the walk can annotate them.

**3. [Cross-plan finding XPF-25-01] S-02 leaks "Echo Archive"**
- **Found during:** Task 1 (first walk run)
- **Issue:** The S-02 fixture title "Echo Archive" is not German-neutral (it contains the English locale value "archive"), and `FleetServerCard` renders the server title in a plain `h3`, not `bdi`, although the plan's verified facts say names render in `bdi`. The fix sits in `fleetScreens.ts` (registry slice) or `FleetServerCard.tsx` (source), both prohibited for this plan.
- **Fix:** Recorded in a `recordedLeaks` table in `german-walk.spec.ts` (exact text, tracking id, annotation on the test). Plan 69-26 should rename the fixture to a German-neutral name or wrap server titles in `bdi` (also `WorkspaceHeader`), then delete the record.
- **Files modified:** `tests/e2e/audit/german-walk.spec.ts`

**4. [Rule 2 - Missing critical] Non-vacuity guards beyond the plan**
- Added: failure on a page with no visible text, a walk count guard, and a browser test that poses the English S-02 page as German and expects a failure (T-69-31).

---

**Total deviations:** 4 (1 plan-sample mismatch, 1 type adaptation, 1 cross-plan finding, 1 added guard)
**Impact on plan:** No scope creep; all inside the three planned files.

## Issues Encountered

- The default Playwright ports 4173 and 4174 can be taken by sibling executors, so the specs were run through an uncommitted private-port copy of the config (deleted before the SUMMARY commit). The plan's `npm run test:e2e -- tests/e2e/audit/german-walk.spec.ts` selects the same spec and projects.
- `npm run build:assets` rewrites `src/generated/api.ts` (line endings only); it was restored and is not committed.
- Under `lang="en"` the detector treats the whole page as excluded, so the browser non-vacuity test sets `lang="de"` before checking.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Slice plans 69-26, 69-27 and 69-28 get the German walk by setting `audit.german` on their entries; a leak they cannot fix is recorded as `KnownFinding { rule: "german-leak", target: <exact text> }`.
- 69-26 should resolve XPF-25-01 and remove the `recordedLeaks` record.
- Deferred follow-up: German wording review of any text the detector cannot judge (human language review) belongs to the milestone-end checks (D-20).

## Self-Check

- Created files exist: germanLeak.ts, german-leak.test.ts, german-walk.spec.ts - FOUND
- Commits `85e23705` and `34446394` are ancestors of HEAD - FOUND
- `npm --prefix sources/ReactApp test -- tests/audit/german-leak.test.ts`: 17 passed
- German walk and formatting specs: 18 passed (chromium, mobile-390, mobile-320)
- `npx tsc -b`: no errors
- `npm run lint`: clean (0 errors, 0 warnings)

## Self-Check: PASSED

---
*Phase: 69-consistency-accessibility-verification*
*Completed: 2026-11-25*
