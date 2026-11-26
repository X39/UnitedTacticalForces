---
phase: 69-consistency-accessibility-verification
plan: 05
subsystem: testing
tags: [playwright, visual-regression, fonts, determinism, react-app]

requires:
  - phase: 68
    provides: React app, mocked Playwright suite and test support helpers this plan extends
provides:
  - Playwright projects mobile-390 and mobile-320 next to the unchanged desktop chromium project
  - Platform-free snapshot path and screenshot defaults
  - stabilize, settle, assertVendoredFonts, fixedNow and visualBaselinesEnabled in tests/e2e/support/visual.ts
  - Vendored, hash-pinned Roboto and Barlow Condensed test fonts with OFL licenses and MANIFEST.json
  - One-container baseline script scripts/Update-VisualBaselines.ps1
affects: [69 audit specs, 69 journeys, 69 mobile visual baselines]

actuals:
  tokens: 7345
  tasks: 3
  commits: 3
plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: fded2df0bf9b1c16eaac09aafbb3bdff8350e78c

tech-stack:
  added: []
  patterns:
    - "Test-only web fonts served through page.route from a hash-pinned manifest; production keeps Google Fonts"
    - "Pixel comparisons opt-in through UTF_VISUAL_BASELINES=1, layout metrics unconditional"

key-files:
  created:
    - sources/ReactApp/tests/e2e/support/visual.ts
    - sources/ReactApp/tests/e2e/mobile/visual-determinism.spec.ts
    - sources/ReactApp/tests/e2e/fonts/MANIFEST.json
    - sources/ReactApp/tests/e2e/fonts/.gitattributes
    - sources/ReactApp/tests/e2e/fonts/OFL-Roboto.txt
    - sources/ReactApp/tests/e2e/fonts/OFL-BarlowCondensed.txt
    - sources/ReactApp/tests/e2e/fonts/Roboto-300.woff2
    - sources/ReactApp/tests/e2e/fonts/Roboto-400.woff2
    - sources/ReactApp/tests/e2e/fonts/Roboto-500.woff2
    - sources/ReactApp/tests/e2e/fonts/Roboto-700.woff2
    - sources/ReactApp/tests/e2e/fonts/Roboto-900.woff2
    - sources/ReactApp/tests/e2e/fonts/BarlowCondensed-500.woff2
    - sources/ReactApp/tests/e2e/fonts/BarlowCondensed-600.woff2
    - sources/ReactApp/tests/e2e/fonts/BarlowCondensed-700.woff2
    - sources/ReactApp/tests/e2e/fonts/BarlowCondensed-800.woff2
    - sources/ReactApp/tests/visual/font-manifest.test.ts
    - scripts/Update-VisualBaselines.ps1
    - .planning/phases/69-consistency-accessibility-verification/deferred-items.md
  modified:
    - sources/ReactApp/playwright.config.ts
    - sources/ReactApp/package.json

key-decisions:
  - "Roboto is one variable woff2 upstream, so the five Roboto weight files are byte-identical copies kept under the plan's file names; each is listed and hashed in the manifest"
  - "The baseline script copies sources into the container and uses a named volume only for the npm cache, because npm ci cannot remove a node_modules directory that is itself a volume mount point (EBUSY)"
  - "A .gitattributes with '* -text' in the fonts directory keeps font and license bytes identical on every checkout, so the SHA-256 guard cannot fail on CRLF conversion"

patterns-established:
  - "stabilize(page, culture) before goto/setContent; settle(page) before measuring; test.use supplies locale and timezoneId"
  - "Manifest test fails on any added, removed or changed vendored font or license file"

requirements-completed: [VER-03]

coverage:
  - id: D1
    description: "Playwright has chromium, mobile-390 and mobile-320 projects with the specified viewports, touch flags, test matching, snapshot path and screenshot defaults; existing specs still run only in chromium"
    requirement: VER-03
    verification:
      - kind: command
        ref: "npx playwright test --list --project=chromium (238 tests, none under tests/e2e/mobile); mobile-390 and mobile-320 list 5 tests each"
        status: pass
      - kind: e2e
        ref: "tests/e2e/mobile/visual-determinism.spec.ts under mobile-390 and mobile-320 (10 passed)"
        status: pass
    human_judgment: false
  - id: D2
    description: "stabilize serves only vendored fonts, fixes clock, time zone, colour scheme and reduced motion, and screenshots are byte-identical"
    requirement: VER-03
    verification:
      - kind: e2e
        ref: "tests/e2e/mobile/visual-determinism.spec.ts#renders only the vendored Roboto and Barlow Condensed faces"
        status: pass
      - kind: e2e
        ref: "tests/e2e/mobile/visual-determinism.spec.ts#fails the font assertion when a page asks for a font outside the vendored set"
        status: pass
      - kind: e2e
        ref: "tests/e2e/mobile/visual-determinism.spec.ts#fixes the clock and the time zone while timers keep running"
        status: pass
      - kind: e2e
        ref: "tests/e2e/mobile/visual-determinism.spec.ts#renders two consecutive screenshots byte for byte identical"
        status: pass
    human_judgment: false
  - id: D3
    description: "Vendored font files and licenses are guarded by SHA-256 in MANIFEST.json and a Vitest test; src never imports from tests"
    requirement: VER-03
    verification:
      - kind: unit
        ref: "tests/visual/font-manifest.test.ts (6 passed)"
        status: pass
    human_judgment: false
  - id: D4
    description: "scripts/Update-VisualBaselines.ps1 generates or compares @visual baselines in the digest-pinned Playwright container image"
    requirement: VER-03
    verification:
      - kind: command
        ref: "pwsh Get-Help ./scripts/Update-VisualBaselines.ps1 -Full (parses, lists -Update and -Engine); -Engine docker with a stopped daemon fails with the clear message"
        status: pass
    human_judgment: true
    rationale: "The container run itself could not be executed here because no container engine daemon was running; the first real run happens when the first @visual baselines are generated (deferred real-environment check)"

duration: 38min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 05: Mobile Playwright projects and deterministic fonts Summary

**Playwright now runs desktop plus 390x844 and 320x568 touch projects with platform-free snapshots, a `stabilize` helper that serves hash-pinned vendored Roboto and Barlow Condensed and a fixed clock, and a digest-pinned one-container baseline script.**

## Performance

- **Duration:** 38 min
- **Started:** 2026-11-25T10:35:00Z (approximate, start time was not recorded)
- **Completed:** 2026-11-25T11:13:36Z
- **Tasks:** 3
- **Files modified:** 20 (18 created, 2 modified)

## Accomplishments

- `playwright.config.ts` gained `mobile-390` and `mobile-320` (isMobile, hasTouch, scale 1), `snapshotPathTemplate` without a platform segment and the `toHaveScreenshot` defaults; `chromium` keeps every existing spec and ignores only `**/mobile/**`. `webServer`, retries and reporter are untouched.
- `tests/e2e/support/visual.ts` provides `stabilize`, `settle`, `assertVendoredFonts`, `fixedNow` and `visualBaselinesEnabled`. A self-test (5 tests, run in both mobile projects) proves that exactly the nine vendored faces load for the `index.html` Google Fonts URL, that a rogue font request is aborted and fails the assertion, that `Date.now()` stays at 2026-11-20T12:00:00Z while timers run, and that two screenshots are byte-identical.
- Nine latin woff2 files and two OFL-1.1 license texts are vendored with SHA-256, source URL and license in `MANIFEST.json`; a Vitest test fails on any hash, file-set or license change and guards that `src` never imports from `tests`.
- `scripts/Update-VisualBaselines.ps1 [-Update] [-Engine docker|podman]` runs `@visual` tests with `UTF_VISUAL_BASELINES=1` in `mcr.microsoft.com/playwright:v1.62.1-noble@sha256:dcc5531e...` (index digest resolved from the registry).

## Task Commits

1. **Task 1: Tracer - mobile projects and a stabilized page that renders only vendored fonts** - `087d98d5` (test)
2. **Task 2: Font manifest guard** - `30ee417a` (test)
3. **Task 3: One-container baseline workflow and the measured suite duration** - `fded2df0` (build)

**Plan metadata:** the SUMMARY commit that follows (docs).

## Decisions Made

- Roboto is served by Google Fonts as one variable woff2 for all weights (`KFO7CnqEu92Fr1ME7kSn66aGLdTylUAMa3yUBA.woff2`, v51). The five `Roboto-<weight>.woff2` files are byte-identical copies, each with its own manifest entry, so the file layout and the nine-file acceptance criterion hold. The generated `@font-face` rule per weight clamps the variable axis to that weight.
- Both licenses were confirmed from the downloaded text as SIL Open Font License 1.1 (Roboto: "Copyright 2011 The Roboto Project Authors", Barlow: "Copyright 2017 The Barlow Project Authors"), which permits redistribution with the license text.
- `culture` in `stabilize(page, culture)` selects the app culture through the `UtfCulture` localStorage key (the key the existing specs use); locale and time zone come from `test.use`, as documented in the JSDoc.
- Toast masking in screenshots is applied only when `UTF_VISUAL_BASELINES=1`, so audit specs that assert toast text keep seeing it. `[data-visual-mask]` elements are always hidden.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Keep vendored bytes stable across checkouts**
- **Found during:** Task 1 (staging)
- **Issue:** Git warned that LF would be converted to CRLF for the license texts and manifest. A CRLF checkout changes the license bytes and breaks the SHA-256 guard on Windows.
- **Fix:** Added `sources/ReactApp/tests/e2e/fonts/.gitattributes` with `* -text`; the manifest test allows that one extra file.
- **Files modified:** `sources/ReactApp/tests/e2e/fonts/.gitattributes`, `sources/ReactApp/tests/visual/font-manifest.test.ts`
- **Commit:** 087d98d5, 30ee417a

**2. [Rule 1 - Bug] Test file-set expectation missed `.gitattributes`**
- **Found during:** Task 2 (first run of the new test; the directory-contents test failed as designed when an unlisted file was present)
- **Fix:** Added `.gitattributes` to the expected directory contents.
- **Commit:** 30ee417a

**3. [Rule 3 - Blocking] Baseline script copies sources instead of mounting a node_modules volume**
- **Found during:** Task 3
- **Issue:** The plan asks for a container-local `node_modules` volume. `npm ci` deletes `node_modules` first, which fails with EBUSY when that directory is a mount point.
- **Fix:** The sources are mounted read-only and copied into the container without `node_modules`, `dist`, `test-results`, `playwright-report` and `__screenshots__`; only `__screenshots__` is mounted read-write, and a named volume caches the npm download cache. `node_modules` is still container-local.
- **Commit:** fded2df0

**4. [Rule 3 - Blocking] `npm ci` was needed in the worktree**
- **Found during:** Task 1
- **Issue:** The fresh worktree had no `node_modules`. Ran `npm ci` from the committed lockfile (no package added, T-69-SC accepted).

**Total deviations:** 4 (1 missing critical, 1 bug in own test, 2 blocking). **Impact:** none on scope; the plan's artifacts and behaviors are all delivered.

## Suite duration

Measured on this Windows machine against the new three-project config (`npx playwright test`, the command `npm run test:e2e` runs; webServer already built/cached):

| Project | Tests listed |
|---------|--------------|
| chromium | 238 (15 spec files, 36 of them skipped by their own conditions) |
| mobile-390 | 5 (1 spec file, the determinism self-test) |
| mobile-320 | 5 (1 spec file, the determinism self-test) |
| Total | 248 in 16 files |

| Run | Result | Wall clock |
|-----|--------|------------|
| Local default workers (10), 3 runs | 211 passed, 36 skipped, 1 failed | 68 s, 71 s, 88 s |
| CI-like: `CI=true --workers=1` (what `.mozaic/react-browser.Containerfile` runs) | 212 passed, 36 skipped, 0 failed | 371 s (6.1 min) |

The 10 new tests add under 10 s. The 30-minute Mozaic budget stays a known risk once the audit and journey specs (about 130 audit tests and the journeys) land in later plans; this plan records and does not change CI. The single failure in the parallel runs is pre-existing (see below); it also fails with the original base config and passes serially and alone.

## Issues Encountered

- `hosting-fleet.spec.ts:558` fails in parallel full runs (`toHaveTitle(/Servers/)` receives "Server fleet") with the original and the new config alike; logged in `deferred-items.md`. `hosting-a11y.spec.ts:215` failed once in the original-config run (flake).
- Other worktree agents share ports 4173 and 4174, which caused one aborted measurement run ("already used"); it was repeated.
- The container run of `Update-VisualBaselines.ps1` was not executed: Docker's client is installed but its daemon was stopped and Podman had no running machine. Parsing, `Get-Help` and the no-engine failure path were verified. Real-environment verification belongs to the first baseline generation.

## Known Stubs

None.

## Threat Flags

None. The threat register mitigations are in place: T-69-05 (hash manifest plus Vitest guard), T-69-06 (image pinned by digest, test rendering only), T-69-07 (no screenshots committed by this plan; `test-results/` and `playwright-report/` stay ignored).

## Self-Check: PASSED

- Files exist: `visual.ts`, `visual-determinism.spec.ts`, `font-manifest.test.ts`, `Update-VisualBaselines.ps1`, `MANIFEST.json`, nine woff2 files, two license files: FOUND.
- Commits `087d98d5`, `30ee417a`, `fded2df0` are ancestors of HEAD: FOUND.
- Acceptance criteria: `mobile-320` count 2, `snapshotPathTemplate` 1, nine woff2 files, `test:e2e:mobile` 1, `sha256` in the manifest test 6, `UTF_VISUAL_BASELINES` 4, `v1.62.1` 1: PASS.
- `npm run typecheck`, `npm run lint`, `npm test -- tests/visual` and the mobile Playwright run: PASS.
