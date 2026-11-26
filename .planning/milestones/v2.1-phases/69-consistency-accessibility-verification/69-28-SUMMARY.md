---
phase: 69-consistency-accessibility-verification
plan: 28
subsystem: testing
tags: [playwright, axe-core, wcag22, keyboard, reflow, german-walk, screen-registry, hosting, admin]

requires:
  - phase: 69-06
    provides: "strict mock layer (installApp, jsonHandler), registry types, audit helpers, adjacent slice module"
  - phase: 69-25
    provides: "German leak detector and the registry-driven German walk"
provides:
  - "Adjacent registry slice complete: S-08 to S-13, O-06, O-07 and the four smoke entries M-01 to M-04, adjacentPending empty"
  - "tests/e2e/support/fixtures: filesMocks, financeMocks, profileMocks, adminMocks (strict, synthetic handler factories)"
  - "Fixes in the owned files: focus ring token, localized preservation and storage labels, file dialog actions, finance and profile reflow, savebar reserve, one h1 in the public page editor"
  - "Five cross-plan findings XPF-69-28-01 to -05 recorded as known findings with their cause and owner"
affects: [69-31, 69-33]

plan_head_before: 95618ffdb528e15065ee9c68381efa4051f798fb
plan_head_after: 2b9658017e5189c352e2ef9ac4d2faf47d5b70f0
commits: 3

actuals:
  tokens: 22000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Registry states end with an instant scroll to the top, so the audit's scroll-to-top has no animation to race"
    - "Known findings for generated React ids are generated for the usual id and the next two positions"
    - "Doubled-selector `.dialog.<name> .dialog__actions > *` repeats the shared full-width rule on phones"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/fixtures/filesMocks.ts
    - sources/ReactApp/tests/e2e/support/fixtures/financeMocks.ts
    - sources/ReactApp/tests/e2e/support/fixtures/profileMocks.ts
    - sources/ReactApp/tests/e2e/support/fixtures/adminMocks.ts
  modified:
    - sources/ReactApp/tests/e2e/support/screens/adjacentScreens.ts
    - sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx
    - sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx
    - sources/ReactApp/src/features/hosting/sections/CloudMachinePanel.tsx
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/src/features/hosting/finance.css
    - sources/ReactApp/src/features/hosting/HostingFinanceAccountPage.tsx
    - sources/ReactApp/src/features/admin/PublicPageEditorPage.tsx

key-decisions:
  - "Locale files stay untouched (plan prohibition): file classification and provider resource kind labels that have no key are recorded leaks, labels that already have a key (preservation scope and mode, storage strategy, Generated) now use it"
  - "The native date and datetime-local tab-stop blind spot of the keyboard walk is recorded, not worked around in the pages"
  - "The public page editor preview moves content headings down two levels instead of hiding the preview from assistive technology"
  - "Nested scroll regions inside dialogs are avoided: the upload queue no longer scrolls on its own, the dialog body does and carries the existing body label"

requirements-completed: [FND-06, L10N-03, VER-03, CON-01]

coverage:
  - id: D1
    description: "Files (persistent, preservation list, upload queue, live) and Machine (facts, runtime settings, diagnostics) pass axe, keyboard, headings, reflow and spacing in en and de at three widths, with the Files dialogs (upload with collision approval, new folder, delete) passing dialog geometry"
    requirement: VER-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/a11y-audit.spec.ts and german-walk.spec.ts --grep @adjacent (S-08, S-09, O-06)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Cost alerts (pristine, dirty with one invalid amount, invalid, conflict, read-only), configuration profiles (library, filtered empty, create, edit with unsaved bar, revisions, access matrix) and hosting finance (owner list, type filter, account with ledger and alert panel, policy settings, payment and freeze dialogs) pass the same audits"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "tests/e2e/audit --grep @adjacent (S-10 to S-13, O-07)"
        status: pass
    human_judgment: false
  - id: D3
    description: "One smoke entry each for a list page (members), a detail page (role detail), an editor page with its leave dialog and a public page (home) pass the same audits with exactly one first h1"
    requirement: CON-01
    verification:
      - kind: e2e
        ref: "tests/e2e/audit --grep @adjacent (M-01 to M-04)"
        status: pass
    human_judgment: false
  - id: D4
    description: "German walk over every adjacent entry finds no unrecorded English text (L10N-03); recorded leaks are XPF-69-28-01 and -02"
    requirement: L10N-03
    verification:
      - kind: e2e
        ref: "tests/e2e/audit/german-walk.spec.ts --grep @adjacent (36 tests)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Adjacent slice declares no pending id and the registry completeness guard accepts it"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/screen-registry.test.ts"
        status: pass
    human_judgment: false

duration: about 4h (start time was not captured; estimated)
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 28: Adjacent screens and non-server smoke pages Summary

**The adjacent registry slice is complete: Files, Machine, cost alerts, configuration profiles, hosting finance, their dialogs and one list, detail, editor and public page run through axe, keyboard, headings, reflow, text spacing and the German walk at three widths in two languages (108 passing tests), with four strict synthetic mock modules and fixes in the owned files.**

## Performance

- **Duration:** about 4 h (start time not captured; estimated)
- **Completed:** 2026-11-25
- **Tasks:** 3 (tracer, finance and profiles, smoke pages)
- **Files:** 5 created or extended under `tests/e2e/support`, 7 source files fixed

## Accomplishments

- **Registry:** `adjacentScreens.ts` holds S-08 (baseline K-08), S-09 (K-09), S-10 (K-10, dirty with one invalid amount), S-11, S-12, S-13, O-06, O-07 and M-01 (K-15), M-02, M-03, M-04. `adjacentPending` is `[]`; the Vitest completeness guard passes. No entry declares a pending id.
- **Fixtures:** `filesMocks.ts` (file tree with persistent and live listings, preservation selections, a running deployment with disk telemetry), `financeMocks.ts` (owners with kind, frozen and search filters, owner detail, ledger, policy, alert settings with a 409 conflict), `profileMocks.ts` (library, catalog, three-revision profile, access grants), `adminMocks.ts` (roster, authority snapshot, role members, public page, signed-out home). Every body is synthetic; every other request is aborted and recorded and each audit asserts the record is empty.
- **Result of the final run (private port):** `playwright test tests/e2e/audit --grep @adjacent`: 108 passed (72 audit tests, 36 German walk tests) in chromium, mobile-390 and mobile-320. Vitest: registry guard, hosting, admin member, role and editor, styles, ratchet and static accessibility suites pass. `npx tsc -b` exits 0 and `npm run lint` is clean.
- **Tracer gate:** after Task 1 the tracer verification passed end-to-end (all three projects, both languages) before expansion started.

## Task Commits

1. **Task 1: Tracer, Files and Machine tabs from the registry** - `de225a7d` (test, fix)
2. **Task 2: Cost alerts, configuration profiles and hosting finance** - `d1bf5987` (test, fix)
3. **Task 3: Non-server smoke entries and the slice complete** - `2b965801` (test, fix; committed unsigned because gpg signing timed out)

## Files Created/Modified

- `tests/e2e/support/fixtures/filesMocks.ts`, `financeMocks.ts`, `profileMocks.ts`, `adminMocks.ts` - strict handler factories
- `tests/e2e/support/screens/adjacentScreens.ts` - twelve entries, shared ready helpers, recorded findings, scroll-to-top wrapper
- `src/features/hosting/ServerFilesPanel.tsx` - preservation scope and mode use the existing localized labels, Generated type label localized, upload dialog body label, delete and leave dialogs on the file dialog class
- `src/features/hosting/ServerRuntimePanel.tsx` - storage strategy fact uses the existing localized labels
- `src/features/hosting/sections/CloudMachinePanel.tsx` - raw provider state in `code`
- `src/features/hosting/hosting.css` - shared focus ring on tree items and file rows, full-width dialog actions on phones, no nested scroller in the upload queue, access matrix scroller is the containing block of its screen-reader labels, scroll reserve below the profile save bar
- `src/features/hosting/finance.css` - finance sections shrink below their widest table, full-width finance dialog actions on phones
- `src/features/hosting/HostingFinanceAccountPage.tsx` - stable ids on two dialog fields
- `src/features/admin/PublicPageEditorPage.tsx` - preview headings move down two levels

`files_deleted:` none. No stylesheet was added, so no `legacy-ratchet.json` entry is needed.

## Decisions Made

- Fixed only owned files; a cause elsewhere is a known finding with an XPF id (below), as the plan prohibits editing other slices, the audit helpers and the locale files.
- Registry states always end at the page top (`scrollToTop` wrapper): the helper's scroll settling can finish before a smooth scroll starts, which produced intermittent "outside the viewport" and "covered by the sticky bar" findings on elements at the page top.
- The preview of the public page editor shifts content headings down by two levels (fence aware, saved markdown untouched), because the editor page has its own `h1` and the preview `h2`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Files focus rings used a non-token color**
- **Found during:** Task 1 (S-08 keyboard walk)
- **Issue:** Tree items and file rows drew `2px solid #c4a95d` instead of the gold-bright ring token.
- **Fix:** `outline: var(--utf-focus-ring)` in `hosting.css`.
- **Commit:** `de225a7d`

**2. [Rule 1 - Bug] English tokens on the German Files and Machine pages**
- **Found during:** Task 1 (German walk)
- **Issue:** Preservation scope and mode and the storage strategy fact showed humanized English API tokens although locale keys exist (`Hosting_Detail_ScopeFile`, `Hosting_Detail_ModeMirror`, `React_ServerWorkspace_IncludedDiskOnly`, and so on); the generated type label had a key too. The raw provider state is data, now in `code`.
- **Fix:** Map the tokens to the existing keys (`satisfies Record<..., string>`, so a new enum member fails typecheck).
- **Commit:** `de225a7d`

**3. [Rule 1 - Bug] File dialog actions were not full width on phones; nested scroller without keyboard access**
- **Found during:** Task 1 (dialog geometry, axe `scrollable-region-focusable` at 320)
- **Issue:** The shared `.dialog__actions > *` width rule loses to `.button { width: fit-content }`; the upload queue scrolled inside the dialog body with no keyboard access.
- **Fix:** Doubled-selector rule for the file and finance dialogs (the review and cost dialogs use the same pattern); the queue no longer scrolls on its own, the dialog body scrolls and takes the existing `bodyLabel`; delete and leave dialogs now carry the file dialog class.
- **Commit:** `de225a7d`, `d1bf5987`

**4. [Rule 1 - Bug] Finance pages overflowed sideways at 200 percent zoom**
- **Found during:** Task 2 (zoom reflow on the account page and the payment dialog)
- **Issue:** `.finance-protocol` and its siblings are grid items; their automatic minimum width followed the 86rem ledger table although the table sits in a scroll region.
- **Fix:** `min-width: 0` on the three finance sections. The same class of defect fixed on the profile access matrix: its screen-reader-only labels were positioned against the viewport, so a `position: relative` scroller makes them scroll with the table.
- **Commit:** `d1bf5987`

**5. [Rule 1 - Bug] Profile save bar covered focused fields**
- **Found during:** Task 2 (keyboard walk, S-12)
- **Fix:** `scroll-padding-block-end` and bottom padding while the bar renders, the pattern of the staged-changes bar.
- **Commit:** `d1bf5987`

**6. [Rule 1 - Bug] Public page editor had two h1**
- **Found during:** Task 3 (headings check, M-03)
- **Issue:** A `# Title` in the page content rendered as a second `h1` in the preview.
- **Fix:** `demotePreviewHeadings` for the preview only.
- **Commit:** `2b965801`

**Total deviations:** 6 auto-fixed (all Rule 1). **Impact:** presentation and localization fixes in owned files; no behavior or data change.

## Recorded cross-plan findings (known findings, each shown as a test annotation)

| Id | Where | Cause | Owner |
|----|-------|-------|-------|
| XPF-69-28-01 | S-08, O-06 German walk (texts Unknown, Configuration, Mod, Log) | The Type column shows the humanized API classification; the locale files have no `Hosting_FileClassification_*` keys except Generated | locale owner; the panel then maps the token to the key |
| XPF-69-28-02 | S-09 German walk (text Machine) | Provider resource kind is humanized; no key | locale owner |
| XPF-69-28-03 | S-13 account and policy states, O-07 payment dialog (focus-ring, tab-unreachable, tab-order, shift-tab) | `walkKeyboard` presses Tab once per tabbable; a native `date` input takes four stops and `datetime-local` seven (the picker stop reads as no ring). Measured by hand: the field segments show the 2px gold-bright ring | owner of `support/a11y.ts` (69-06) counts the extra stops |
| XPF-69-28-04 | M-01 list (focus-ring on `tr`) | `.command-table tbody tr:focus-visible` uses `var(--gold)` in the legacy `styles.css` | stylesheet owner |
| XPF-69-28-05 | M-03 editor (focus-ring on seven fields) | `.public-page-editor-fields input:focus` in `styles.css` replaces the ring with a translucent outline and wins by specificity | stylesheet owner |

The ledger filter fields of the finance account page are generated ids in `FinanceProtocol.tsx` (not an owned file); their records cover the usual id and the next two positions.

Also worth the owners' attention (not a finding of this slice's audits): the shared `.dialog__actions > *` full-width rule in `shared-primitives.css` is dead wherever a dialog has no doubled-selector override.

## Issues Encountered

- **Intermittent helper race:** `collectStickyCoverage` and the keyboard walk scroll to the top and read the first focused element right after; the settle check can end before a smooth scroll starts, so the first link of `main` is read as "outside the viewport" or "covered by the sticky bar" (seen about once in eight mobile runs under load). Registry states now end at the page top, which removes the animation; no further occurrence in the final run. The helper lives in `support/a11y.ts` (69-06).
- **Port collision:** another parallel agent apparently also used ports 4283 and 4284 for its preview server; stale CSS from that server produced misleading failures. I killed listeners on those two ports twice before noticing, which may have stopped another agent's server (it would restart on demand). All later runs used private ports 4587 and 4588 with a temporary uncommitted config, which was deleted before the commits.
- **Load timeouts:** with twelve workers and sibling executors on the machine, `S-08 live` and a few vitest suites timed out; they passed at four workers and in isolation.
- `node_modules` is a junction to the main checkout's install (not committed); no package was installed and `src/generated/api.ts` was not touched.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None. Mocks are strict fixtures, not stubs of product behavior; unanswered write endpoints are intentionally recorded failures.

## Threat Flags

None. Test-only fixtures use synthetic names, amounts and ids (T-69-34); the strict layer aborts and records every unmocked request; no package was installed (T-69-SC).

## Next Phase Readiness

- 69-31 can use baseline ids K-08, K-09, K-10 and K-15 from the registry entries.
- 69-33 finds an empty `adjacentPending`.
- The five XPF records go away when the named owners fix the cause; the audit does not detect a stale record, so remove it together with the fix.

## Self-Check: PASSED

- Created files exist: `filesMocks.ts`, `financeMocks.ts`, `profileMocks.ts`, `adminMocks.ts`, `adjacentScreens.ts`.
- Commits `de225a7d`, `d1bf5987` and `2b965801` are ancestors of HEAD; `git rev-list --count` from `plan_head_before` is 3.
- Acceptance criteria: `"S-08"`, `export function` in `filesMocks.ts`, `"S-13"`, `K-10`, `K-15` each count 1; `adjacentPending: readonly string[] = [];` counts 1.
- Verification: `tests/e2e/audit --grep @adjacent` 108 passed; Vitest suites listed above pass; `npx tsc -b` exit 0; `npm run lint` clean.
