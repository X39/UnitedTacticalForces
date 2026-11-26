---
phase: 65-change-a-server
plan: 14
subsystem: testing
tags: [playwright, e2e, axe, accessibility, hosting, staged-changes, leave-guard, i18n]

requires:
  - phase: 65-08
    provides: the Settings section with the pending bar, the review dialog and the apply flow
  - phase: 65-11
    provides: the credentials panel that stages values in the draft
  - phase: 65-12
    provides: the Common and Advanced configuration panel
  - phase: 65-13
    provides: the Mods panel and the mod pack contents diff
provides:
  - hosting-change.spec.ts, a real-browser proof of the change journey with a stateful mocked API
  - Layout, focus-not-obscured, axe, partial apply and leave guard checks at 320x568 and 390x844 in English and German
  - A phone-width fix of the leave dialog actions (found by the spec)
affects: [65-15]

plan_head_before: ebe8a9886d32669fc59efd073851f5ad2781747a
plan_head_after: 42c05b8ffa6e459093dd4ffa7506f7516709532c

actuals:
  tokens: 14000
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A stateful page.route mock: reads answer from a mutable state, writes are recorded in order, change the state like the API and flip the pending-start signal for the area they touched; unmocked requests are aborted and recorded"
    - "Staging helpers scroll the field into view first, so the sticky bar is measured at the page position a member is actually at"
    - "Bar height is recorded as a test annotation (bar-height) so a run shows the measured value, not only pass or fail"

key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-change.spec.ts
  modified:
    - sources/ReactApp/src/components/server/change.css
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx

key-decisions:
  - "The leave dialog gets its own dialog--leave class and stacks its three actions in document order on narrow screens (column, not column-reverse), so the visual order, the tab order and the initial focus on Stay here agree"
  - "The pending banner is asserted through role status filtered by its copy, because the waiting-areas line lives inside the same status panel and an exact text match on the first sentence never matches"
  - "The waiting-areas line is matched with a regular expression because the list separator comes from Intl.ListFormat and differs between runtimes (with or without the Oxford comma in English)"

requirements-completed: [CHG-01, CHG-02, CHG-03, CHG-04, CHG-05]

coverage:
  - id: D1
    description: "In a real browser a member stages a mod pack, a Common setting and a credential, sees one bar with the count, reviews the contents diff, the take-effect notice and no credential value, applies in the order mod pack, configuration, credential with the right bodies, and sees the success toast and the pending-start banner with its waiting areas"
    requirement: CHG-03
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#change journey (en, 390x844)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A partial apply with one mocked 500 keeps the failed setting staged with its reason, reports the counts in an alert, offers Try 1 change again, and the second apply sends only the configuration request (English and German)"
    requirement: CHG-03
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#partial apply (en, 390x844)"
        status: pass
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#partial apply (de, 390x844)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The leave guard asks on a section switch and on Back with Stay here, Discard and leave and Review changes in that order; Stay keeps the URL and the bar, Review opens the review, Discard reaches tab=files; a dispatched beforeunload is prevented only while changes are staged"
    requirement: CHG-04
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#leave guard (en, 390x844)"
        status: pass
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#leave guard (de, 390x844)"
        status: pass
    human_judgment: false
  - id: D4
    description: "At 320x568 and 390x844 in English and German there is no horizontal page scroll, the bar stays within one third of the viewport (163 px at 320x568), and the review dialog and its Apply stay inside the viewport"
    requirement: CHG-05
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#change layout"
        status: pass
    human_judgment: false
  - id: D5
    description: "The sticky bar never covers the last Advanced field that takes focus by keyboard at 320x568 (English and German)"
    requirement: CHG-05
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#change focus"
        status: pass
    human_judgment: false
  - id: D6
    description: "axe at WCAG 2.1 A and AA reports zero violations with a non-zero passing rule count on Settings with a staged draft and with the review dialog open, in English and German; no unmocked API request happens in any test"
    requirement: CHG-05
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-change.spec.ts#change accessibility"
        status: pass
    human_judgment: false
  - id: D7
    description: "Visual baselines of the review dialog in German at 390 px and of the sticky bar over focused fields; this plan asserts geometry only"
    requirement: CHG-05
    verification: []
    human_judgment: true
    rationale: "Both are held-out visual baselines that arrive with Phase 69 (VER-03); geometry and axe are asserted here"

duration: 62min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 14: Change flow end-to-end Summary

**A 13-test Playwright spec runs the full staged-change journey against a stateful mocked API in Chromium, proves the bar, dialog, focus, partial apply and leave guard at 320 and 390 px in English and German with clean axe scans, and found and fixed a real overflow of the leave dialog actions on phones.**

## Performance

- **Duration:** about 62 min
- **Tasks:** 2 (1 tracer, 1 auto) plus one production fix commit found by the spec
- **Files:** 3 (1 created, 2 modified)

## Accomplishments

- Tracer: `mockApi(page, culture, state)` answers every request the Settings section makes (server, capabilities, mod pack options and contents, configuration, profile options, credentials, storage demand, files, preservation, notifications, site images) from a mutable state, records every PUT and POST body in order, updates the state like the API, flips `hasChangesPendingStart` and the waiting areas per area written, and records and aborts anything unmocked. The journey stages Extended, maximum players 80 and a typed credential, asserts the three-change bar, an axe scan, the review dialog (Added C, Removed A, the not-running notice, the credential shown as "Changed. The value is not shown." and the typed value absent from the dialog HTML and text), the apply order `PUT /mod-pack`, `PUT /configuration`, `PUT /secrets` with their bodies, the toast, and the banner with "Waiting: Mod pack, Configuration, and Credentials".
- Expansion: layout loops (bar 163 px of 568 at 320 in both languages, 142 and 163 px of 844 at 390, within one third; no horizontal scroll; review dialog and Apply inside the viewport), a keyboard focus-not-obscured check on the last Advanced field at 320x568, axe on Settings with a draft and on the open review dialog in both languages, a partial apply with one mocked 500 (alert with the counts, failed row with "Not applied" and the "other" reason, "Try 1 change again", and a second apply that sends only the configuration request), and the leave guard in both languages (section switch, Back via `page.goBack()`, all three outcomes, `beforeunload` prevented only while dirty).

## Task Commits

1. **Task 1 (tracer): the change journey** - `cd1af10f` (test(webapp))
2. **Production fix found by the spec: leave dialog actions on phones** - `17b9da52` (fix(webapp))
3. **Task 2: layout, focus, partial apply and the leave guard** - `42c05b8f` (test(webapp))

The fix commit sits before the tracer commit in history because the spec was written and run as one piece; the tracer commit holds only the journey (the Task 2 loops were stripped for it and restored for the third commit).

## Verification Results

- Tracer gate (automated-only verify, end-of-phase mode): the journey passed, then expansion proceeded.
- Plan-level: `npm run test:e2e -- tests/e2e/hosting-change.spec.ts` 13 passed, 0 failed, 0 skipped.
- Acceptance greps: `hasChangesPendingStart` 4, `AxeBuilder` 4 (import plus scans), `beforeunload` 4, `568` 4, `status: 500` 1, all at least 1.
- `eslint --max-warnings 0` and `prettier --check` clean on the spec and the two production files; `tsc -b` exit 0; `vitest run tests/styles tests/hosting/change-flow.test.tsx tests/hosting/workspace-section-panels.test.tsx tests/locales` 131 passed.
- The preview was rebuilt with `npm run build:assets` (locales check 0 failures, contracts regenerated with no diff) before the final runs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The leave dialog actions overflowed the dialog on phones**
- **Found during:** Task 2 (leave guard test at 390x844, English and German)
- **Issue:** The generic `.dialog__actions` row has no wrap. With three actions (Stay here, Discard and leave, Review changes) the row was wider than the 346 px dialog at 390 px; "Stay here" sat at x = -60 px, outside the viewport and unclickable (German worse). The initial focus is on that button.
- **Fix:** `SettingsSection` passes `className="dialog--leave"` to the leave `ConfirmDialog`; `change.css` lets the actions wrap and stacks them in document order up to 520 px with the touch height up to 800 px (token-only, same breakpoints as the review dialog; the style guard passes).
- **Files modified:** `sources/ReactApp/src/components/server/change.css`, `sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx` (owned by plans 65-07 and 65-10)
- **Verification:** the leave guard test passes in both languages; vitest styles, change-flow, section-panels and locales pass
- **Commit:** 17b9da52

### Plan wording that did not match the page

- The plan describes the Apply order as mod pack, profile, configuration, secrets; the profile request is sent only when a profile change is staged, so the journey asserts three requests.
- The first version of the layout and focus checks typed into an offscreen field. Playwright's `fill` focuses without scrolling, so the page stayed at the top and the sticky bar, which cannot rise above its section, sat below the fold (bar bottom at 824 px). A member scrolls to a field before typing, so the staging helper now scrolls it into view first and the focus test also asserts that the bar is inside the viewport, which keeps the field-versus-bar comparison from passing vacuously. This is a test-side correction, not a page defect.

**Total deviations:** 1 auto-fixed (Rule 1, one production CSS and class change) plus 2 wording notes. **Impact:** the spec caught a genuine phone usability bug in the leave guard; no scope change.

## Issues Encountered

None open. The review dialog German-at-390 visual baseline and the sticky-bar-over-focus visual baseline remain backstops for Phase 69 as the UI-SPEC states.

## Known Stubs

None.

## Threat Flags

None. T-65-38 is mitigated (the journey asserts the typed credential value is absent from the dialog HTML and text). T-65-39 is mitigated (every test records and aborts unmocked API requests and asserts the list is empty).

## Next Phase Readiness

Plan 65-15 can build on the spec's mock pattern. The Files `?path=` reload and Reveal e2e checks of UI-SPEC automated checks (c) and (d) are not part of this plan and are left to 65-15. The Files section now loads in the leave-guard test only to prove the redirect; its own behavior is not asserted here.

## Self-Check: PASSED

- `sources/ReactApp/tests/e2e/hosting-change.spec.ts` verified on disk; commits `cd1af10f`, `17b9da52` and `42c05b8f` verified as ancestors of HEAD; `git rev-list --count` from the ledger base gives 3.
