---
phase: 65-change-a-server
plan: 15
subsystem: testing
tags: [playwright, e2e, axe, fake-clock, parity-matrix, credentials, files, url-state]

requires:
  - phase: 65-07
    provides: the Files folder in the URL (filesLocation.ts, ServerFilesPanel with ?path= and ?area=)
  - phase: 65-11
    provides: the credentials panel with Reveal, auto-hide and staged values
  - phase: 65-14
    provides: the mock pattern and conventions of hosting-change.spec.ts
provides:
  - hosting-files-secrets.spec.ts, a real-browser proof of Files folder URLs and of credential reveal hygiene
  - All 52 Phase 65 parity rows recorded as kept or relocated with a new location and test evidence
  - A guard test that fails if a Phase 65 row is deleted, pending, removed or lacks evidence
  - The configuration layout selectors orphaned by the Settings rewrite removed from hosting.css
  - A green phase gate run on the finished tree
affects: [phase-65-verification, 69-consistency-accessibility-verification]

plan_head_before: f6d78eaa651b9d3b5183574b88640466c6176290
plan_head_after: b37bf2720d7549ea2bb41e079dd365ab4819a9a2

actuals:
  tokens: 14000
  tasks: 3
  commits: 6

tech-stack:
  added: []
  patterns:
    - "A leak check helper (expectNoTrace) asserts a secret is absent from DOM, localStorage, sessionStorage, cookies, URL and every console message and page error"
    - "page.clock.install() before navigation, then fastForward(29_000) and fastForward(1_500), proves an auto-hide boundary instead of only the end state"
    - "Fallback-with-replaced-history is proven in a real browser by opening a good entry, then the bad link, then page.goBack(): the bad link must not be in the history"

key-files:
  created:
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
  modified:
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/tests/e2e/hosting-placement.spec.ts

key-decisions:
  - "The parity guard also requires a Phase 65: evidence sentence and a tests/ path on every Phase 65 row, so an evidence cell cannot silently go empty or lose its test reference"
  - "Phase 65 owns 52 rows (4 TAB, 23 SVC, 12 CHG, 13 FIL); the guard threshold stays at the plan's 48 as a floor"
  - "Only the two orphaned selectors (.next-hosting-configuration-panel and -savebar) were removed, from inside shared selector lists; the other five .next-hosting-configuration rules are still used by ConfigurationProfileFields.tsx"

requirements-completed: [CHG-06, CHG-07, CHG-01, CHG-02, CHG-03, CHG-04, CHG-05]

coverage:
  - id: D1
    description: "A Files folder link survives reload, Back, Forward and a fresh page of the same context; the breadcrumb marks the current folder and the immediate-change note is shown (English and German, 390x844)"
    requirement: CHG-06
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts#files folders in the URL restores a folder on reload, Back and Forward and in a fresh page"
        status: pass
    human_judgment: false
  - id: D2
    description: "Invalid (a/../b) and missing paths show the top level with the invalid or not-found notice, drop path from the URL and replace the history entry so Back returns to the previous good folder and never to the bad link"
    requirement: CHG-06
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts#files folders in the URL falls back to the top level for invalid and missing folders without keeping the bad link"
        status: pass
    human_judgment: false
  - id: D3
    description: "The live context of a server without a runtime shows the error panel, keeps area=live in the URL and requests the live top level at most twice"
    requirement: CHG-06
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts#files folders in the URL shows the error panel for the live context without a runtime and does not loop"
        status: pass
    human_judgment: false
  - id: D4
    description: "axe at WCAG 2.1 A and AA reports zero violations with a non-zero passing rule count on the Files section showing a fallback notice (English and German)"
    requirement: CHG-06
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts#files folders in the URL has no axe violation on the Files section with a fallback notice"
        status: pass
    human_judgment: false
  - id: D5
    description: "A revealed credential is still shown at 29 seconds and gone with the masked text back at 30 seconds on a fake clock; a hidden tab hides it at once; the value is in no DOM, storage, cookie, URL or console message afterwards (English and German, 320x568 and 390x844)"
    requirement: CHG-07
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts#credential reveal hides after 30 seconds and when the tab is hidden and leaves no trace"
        status: pass
    human_judgment: false
  - id: D6
    description: "A 172 character unbroken revealed value wraps inside its code element at 320 px without horizontal page scroll"
    requirement: CHG-07
    verification:
      - kind: e2e
        ref: "sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts#credential layout wraps a long revealed value without horizontal scroll"
        status: pass
    human_judgment: false
  - id: D7
    description: "Every Phase 65 parity row is kept or relocated with a New location and Phase 65 evidence, none is deleted, and the guard enforces it; the orphaned configuration selectors are removed from hosting.css"
    requirement: CHG-05
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/parity-matrix.test.ts#keeps every Phase 65 row kept or relocated, with test evidence and no deleted row (D-08)"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/styles (vitest run, 36 passed)"
        status: pass
    human_judgment: false
  - id: D8
    description: "Phase gates: locales check, contracts check, lint, typecheck, 1791 unit tests, build, 92 hosting e2e tests, routes.json unchanged, 705 API tests, no pending EF model changes, OpenAPI export equal to the snapshot"
    requirement: CHG-01
    verification:
      - kind: other
        ref: "npm run lint, typecheck, test, build:assets; npm run test:e2e (7 hosting specs); dotnet test --filter GameServerHosting|OpenApi; dotnet ef migrations has-pending-model-changes; cmp of the exported OpenAPI document"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 15: Files URL and credential e2e, parity matrix and phase gates Summary

**A 14-test Playwright spec proves in Chromium that Files folder links survive reload, history and sharing and that a revealed credential hides on a fake clock and leaves no trace, all 52 Phase 65 parity rows are recorded with evidence behind a guard test, and every phase gate passes on the finished tree.**

## Performance

- **Duration:** about 12 min
- **Tasks:** 3 (1 tracer, 2 auto) plus one test fix found by the gate run
- **Files:** 5 (1 created, 4 modified)

## Accomplishments

- Tracer: `mockApi` serves the workspace shell and a Files listing keyed by path (top level with `profiles`, `profiles` with `child` and a file, `profiles/child` empty, everything else 404), counts files requests per context and path, and records and aborts anything unmocked. At 390x844 in both languages the spec proves reload, folder link push, Back and Forward, a shared link opened in a fresh page, the traversal and missing path fallbacks with a replaced history entry (Back returns to the previous good folder), the live-context error panel with at most two live requests, the immediate-change note, and a clean axe scan with a fallback notice.
- Credentials: on `page.clock.install()` the spec reveals a distinctive value, asserts it is still shown at 29 s and gone at 30 s with the masked text back, then dumps the DOM, both storages, cookies, URL and every console message and page error for the value; a second reveal is hidden by a dispatched hidden `visibilitychange`; a 172 character unbroken value is shown at 320 px without horizontal scroll. Both languages, both phone widths.
- Parity matrix: all 52 Phase 65 rows (4 TAB rows appended, 23 SVC, 12 CHG, 13 FIL) updated in place. CHG and FIL rows follow the UI-SPEC table exactly; every SVC row is kept with the new caller named; every Evidence cell carries a `Phase 65:` sentence and existing test files (checked on disk). Phase 63 evidence of the four TAB rows is kept and appended to. No row deleted.
- Guard: `parity-matrix.test.ts` filters rows whose Phase is 65, requires at least 48, lists offenders that are neither kept nor relocated, and lists rows without a `Phase 65:` evidence sentence and a `tests/` path.
- CSS: the selectors `.next-hosting-configuration-panel` (three places in shared lists) and `.next-hosting-configuration-savebar` were referenced by no file under `src`; they left their shared rules. The other `.next-hosting-configuration-*` rules stay because `ConfigurationProfileFields.tsx` still uses them (15 matching lines became 10).

## Task Commits

1. **Task 1 (tracer): Files folders in the URL** - `d2435d4d` (test(webapp))
2. **Task 2: credential reveal, auto-hide and leak checks** - `d6c6ff28` (test(webapp))
3. **Task 3a: parity matrix** - `be209519` (docs(65))
4. **Task 3b: parity guard** - `6d45a8ef` (test(webapp))
5. **Task 3c: orphaned configuration CSS** - `e4f4b917` (refactor(webapp))
6. **Gate fix: placement e2e capability mock** - `b37bf272` (test(webapp))

## Verification Results

- Tracer gate (automated-only verify, end-of-phase mode): 8 passed, then expansion proceeded.
- `tests/e2e/hosting-files-secrets.spec.ts`: 14 passed (8 Files, 4 reveal, 2 layout).
- Acceptance: `path=profiles` 3, `goBack` 2, `page.clock` 3, `sessionStorage` 2, pending Phase 65 rows 0 (was 47), `Phase 65:` 52, `Phase 65` in the guard 5, `.next-hosting-configuration` lines 10 (was 15), `routes.json` diff empty. All meet their thresholds.
- Gate run on the finished tree: `locales:check` 3526 keys 0 failures; `contracts:check` no diff; `eslint --max-warnings 0` clean; `tsc -b` clean; `vitest run` 150 files, 1791 tests passed; `build:assets` built; hosting e2e (change, files-secrets, workspace, a11y, placement, create, fleet) 92 passed; `dotnet build` 0 errors; `dotnet test --filter GameServerHosting|OpenApi` 705 passed (Api.Tests); `dotnet ef migrations has-pending-model-changes` reports no changes; the exported OpenAPI document is byte-equal to `sources/ReactApp/openapi/utf-api.json`; `git status --porcelain -- sources/WebApp` empty.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The placement e2e aborted on the unmocked capabilities request**
- **Found during:** Task 3 (phase gate run of the hosting e2e specs)
- **Issue:** `hosting-placement.spec.ts` threw `Unexpected API request: GET /api/game-servers/capabilities`. Since the Mods panel (plan 65-13) the Settings section reads the game capability to decide whether the panel exists; the spec predates that read, so the page never rendered the placement selector (the "Protocol error ... session closed" lines were fallout from the thrown route handler). Production behavior is correct.
- **Fix:** the spec now answers the capabilities request with an Arma 3 capability that has no mod pack support, so the Mods panel is not rendered and no further request is needed.
- **Files modified:** `sources/ReactApp/tests/e2e/hosting-placement.spec.ts` (owned by plans 61-13 and 63-12, last touched by 65-12)
- **Verification:** the spec passes alone and in the full 92-test hosting e2e run
- **Commit:** b37bf272

### Plan wording that did not match the page

- The plan names the Files axe scope as "the Files section"; the Files panel has no `.workspace-section` wrapper (unlike Settings), so the scan includes `.next-hosting-files-panel`, the panel's own section element.
- The plan lists "48" Phase 65 rows; the matrix holds 52 (the four workspace TAB rows owned by Phase 65 were already kept or relocated). The guard keeps 48 as the floor and the Phase 65 evidence is appended to all 52.

**Total deviations:** 1 auto-fixed (Rule 3, test mock only) plus 2 wording notes. **Impact:** no production change beyond the planned CSS removal; the gate run found one stale test mock from an earlier plan of this phase.

## Issues Encountered

- **`npm run ci` stops at `format:check` in this working copy.** 34 files fail Prettier only because their working-tree line endings are CRLF (git `core.autocrlf=true` on this Windows checkout) while the index holds LF (`git ls-files --eol` shows `i/lf w/crlf`). They are Phase 64 files and unrelated to this plan; `prettier --check . --end-of-line auto` passes for the whole project, and `ci`'s remaining steps (lint, typecheck, test, build:assets) were run as one command and pass. A Linux CI checkout has LF and is unaffected. Nothing was changed for this.
- The review dialog and sticky bar visual baselines remain Phase 69 work, as in plan 65-14.

## Known Stubs

None.

## Threat Flags

None. T-65-40 is mitigated (the spec asserts the revealed value is absent from DOM, storage, cookies, URL and console after auto-hide and after a hidden tab). T-65-41 is mitigated (traversal and missing paths fall back to the top level with a replaced history entry and the request counter shows no loop). T-65-42 is mitigated (contracts check, the OpenAPI export comparison and the pending-model-changes check all pass).

## Next Phase Readiness

All 15 plans of Phase 65 are complete. The phase is ready for verification: the change journey (65-14), Files URL and credential hygiene (this plan) are proven in a real browser, the parity matrix accounts for every Phase 65 capability, and the gates are green.

## Self-Check: PASSED

- `sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts` verified on disk; commits `d2435d4d`, `d6c6ff28`, `be209519`, `6d45a8ef`, `e4f4b917` and `b37bf272` verified as ancestors of HEAD; `git rev-list --count` from the ledger base gives 6.
