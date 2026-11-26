---
phase: 69-consistency-accessibility-verification
plan: 31
subsystem: testing
tags: [playwright, visual-baselines, toHaveScreenshot, determinism, mobile, react-app]

requires:
  - phase: 69
    provides: visual helpers and mobile projects (69-05), screen registry with K-xx baseline ids (69-26, 69-27, 69-28)
provides:
  - Fifteen key screens (K-01 to K-15) as 60 opt-in @visual tests in mobile-320 and mobile-390, en and de, named {id}-{culture}.png
  - Unconditional capture-stability proof for K-01, K-04 and K-11 (two consecutive captures and one after a reload, byte-identical, en and de)
  - Shared key-screen resolver over the registry baseline ids (support/keyScreens.ts) with a repaint-from-scratch step that removes a real raster nondeterminism
affects: [69-33, 69-34, 69 verification]

requirements-completed: [VER-03]

actuals:
  tokens: 5500
  tasks: 2
  commits: 2
plan_head_before: ddbb4944079e1b33dda28b5f98a1676ad9894ff1
plan_head_after: b497201efcd7687692172c8d1dc3f944005622ec

tech-stack:
  added: []
  patterns:
    - "Key screens are resolved from registry baseline ids (entry baseline = first state, state baseline = that state, three named later states) and shared by the baseline spec and the stability spec"
    - "Element captures (K-07, K-14) are a clip of a full-page capture taken at scroll 0, so fixed and sticky overlays stay out of tall elements"
    - "Every capture starts from a fresh raster (body display toggled), because partial-raster history made avatar edge pixels differ by one level between page loads"

key-files:
  created:
    - sources/ReactApp/tests/e2e/mobile/key-screens.spec.ts
    - sources/ReactApp/tests/e2e/mobile/visual-stability.spec.ts
    - sources/ReactApp/tests/e2e/support/keyScreens.ts
  modified: []

key-decisions:
  - "K-05, K-10 and K-15 capture the later state of their entry (expanded failed entry, dirty with one invalid amount, state panel), as the UI-SPEC table describes; the registry baseline id names the entry, so a three-row name table selects the state."
  - "K-14 has no wrapper element (the automatic shutdown notice and the cost alert banner render straight into the workspace shell), so the element capture is the union box of the two panels."
  - "Pixel baselines are not committed (D-AUTO-1); acceptance stays a human follow-up."

patterns-established:
  - "Determinism is proven by comparing renderings of the same machine with each other, without any committed image, and runs everywhere"

coverage:
  - id: D1
    description: "Capture of K-01, K-04 and K-11 is deterministic in mobile-390 in en and de: two consecutive captures and a capture after a reload are byte-identical, with the vendored fonts asserted"
    requirement: VER-03
    verification:
      - kind: e2e
        ref: "tests/e2e/mobile/visual-stability.spec.ts (mobile-390: 7 passed, repeated 8 times: 56 passed; mobile-320 skipped by design)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Sixty key-screen tests (15 screens x en/de x mobile-320/390) are listed and skipped without UTF_VISUAL_BASELINES, and run (stabilize, open state, mask live values, toHaveScreenshot) with it"
    requirement: VER-03
    verification:
      - kind: e2e
        ref: "tests/e2e/mobile/key-screens.spec.ts --list (60 tests); default run 30 skipped in mobile-390; container run of scripts/Update-VisualBaselines.ps1 -Update 60 passed, then compare run 60 passed"
        status: pass
    human_judgment: false
  - id: D3
    description: "The first committed images are visually acceptable and stay stable across Windows and CI rendering"
    requirement: VER-03
    verification: []
    human_judgment: true
    rationale: "Judging a baseline image is a human review step; generation and review are the deferred follow-up (D-AUTO-1)."

duration: 95min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 31: Key-screen visual baselines Summary

**Fifteen key screens captured as 60 opt-in @visual tests driven by the registry baseline ids, with an unconditional stability proof that exposed and removed a per-page-load raster nondeterminism (repaint from scratch before every capture).**

## Performance

- **Duration:** about 95 min
- **Completed:** 2026-11-25
- **Tasks:** 2 (tracer plus one auto task)
- **Files created:** 3 (two specs and one support module), none modified

## Accomplishments

- `support/keyScreens.ts` resolves the 15 key screens from the registry (`entry.baseline`, `state.baseline`, and the three later states K-05, K-10, K-15), throws when an id is claimed twice or the count is not 15, and exposes `openKeyScreen`, `reachKeyState`, `captureKeyScreen`, `expectKeyScreenshot`, `liveMasks` and `repaintFresh`.
- `visual-stability.spec.ts` (unconditional, mobile-390 only) stabilizes, reaches, settles and captures K-01, K-04 and K-11 twice, reloads, reaches the state again and captures a third time, in en and de. All three must be byte-identical and the vendored fonts and the strict mock record must be clean. It also asserts the registry yields K-01 to K-15.
- `key-screens.spec.ts` holds the 60 `@visual` tests. Without `UTF_VISUAL_BASELINES=1` they are listed and skipped with the reason "visual baselines are opt-in"; with it each test stabilizes, opens its state, settles, asserts the vendored fonts and compares `{id}-{culture}.png` with the live values (accrued cost, relative times) masked through the Playwright `mask` option. K-07 (`#server-automation`) and K-14 (the notices) are element captures, the other thirteen the viewport.
- **Container generation proven:** `pwsh ./scripts/Update-VisualBaselines.ps1 -Update -Engine podman` ran in the pinned Playwright image (a Podman machine was running) and wrote 60 images (30 per project, 60 passed); a second run of the script without `-Update` compared against them and passed 60 of 60 ("All visual baselines match"). The images were deleted, not committed.

## Task Commits

1. **Task 1: Tracer, deterministic capture proven on three key screens** - `cb94f630` (test)
2. **Task 2: Fifteen @visual key-screen baselines behind the opt-in switch** - `b497201e` (test)

Tracer gate: the tracer `<verify>` passed end to end before expansion (7 passed in mobile-390, then 56 passed over 8 repeats).

## Files Created/Modified

- `sources/ReactApp/tests/e2e/mobile/key-screens.spec.ts` - the 60 opt-in `@visual` tests
- `sources/ReactApp/tests/e2e/mobile/visual-stability.spec.ts` - the unconditional capture-stability proof
- `sources/ReactApp/tests/e2e/support/keyScreens.ts` - registry-driven key screen resolver, capture and comparison helpers shared by both specs

## Decisions Made

- The later-state screens (K-05 expanded failed entry, K-10 dirty with one invalid amount, K-15 state panel) are selected by state name, because the registry `baseline` on the entry names the entry's first state by contract.
- K-14 is the union box of the shutdown notice panel and the cost alert banner panel (no wrapper element exists, and app source is off limits).
- No image is committed and no CI file is touched.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Per-page-load raster nondeterminism in the capture**
- **Found during:** Task 1 (tracer)
- **Issue:** K-01 differed between a capture before and after a reload (and between two runs): 38 edge pixels of the profile avatar (a rounded, semi-transparent element at a fractional offset) blended one level (1/255) differently. Measured with a hash sequence over 6 to 24 repeats: two variants, picked per page load, stable within a load (not time dependent, not GPU related; `--disable-gpu`, `--disable-lcd-text` did not change it). Cause: Chromium's partial raster after a font swap or data arrival leaves a different edge blend than a single paint.
- **Fix:** `repaintFresh` takes the body out of rendering and back (restoring scroll offsets and focus) before every capture, so all tiles paint from scratch. Verified 48 of 48 identical on K-01 and K-04 and 24 of 24 on K-11, then 56 of 56 in the final spec. A `visibility` toggle and a viewport resize were tried and rejected (still varied).
- **Files modified:** `support/keyScreens.ts`
- **Commit:** `cb94f630`

**2. [Rule 3 - Blocking] Shared helper module outside the plan's files_modified**
- **Found during:** Task 1
- **Issue:** both specs need the same registry resolution, state reaching and capture framing, and one Playwright spec file cannot import another.
- **Fix:** new `sources/ReactApp/tests/e2e/support/keyScreens.ts` (a new file, no sibling plan owns it).
- **Commit:** `cb94f630`, reworked in `b497201e`

**3. [Rule 1 - Bug] Tall element captures contained viewport-fixed overlays**
- **Found during:** Task 2 (host smoke run with `--update-snapshots`, images inspected)
- **Issue:** `locator.screenshot` of K-07 (taller than the phone viewport) drew the sticky header and the parked skip link in the middle of the section.
- **Fix:** element captures are a clip of a full-page capture taken after `scrollTo(0, 0)`, so those overlays sit at the top of the page, outside the box. K-14 uses the same path with the union of two locators.
- **Files modified:** `support/keyScreens.ts`
- **Commit:** `b497201e`

**Total deviations:** 3 auto-fixed (2 bugs, 1 blocking). **Impact:** no change in scope; the helper module is a test-only file.

## Verification

- `npx playwright test tests/e2e/mobile/visual-stability.spec.ts --project=mobile-390` (private ports 47731/47741, temporary config deleted): 7 passed; 56 passed over 8 repeats; mobile-320 skipped by design.
- `playwright test tests/e2e/mobile/key-screens.spec.ts --list --project=mobile-320 --project=mobile-390`: 60 tests. Default run in mobile-390: 30 skipped, 0 failed.
- `playwright test tests/e2e/mobile` (both projects): 18 passed, 66 skipped.
- Host smoke with `UTF_VISUAL_BASELINES=1 --update-snapshots`: 60 passed (images deleted, never used as baselines).
- Container: `-Update` 60 passed and 60 images written; compare run 60 passed. Images deleted.
- `npx tsc -b` exit 0; `npm run lint` exit 0.
- Acceptance greps: `assertVendoredFonts` in the stability spec 3; `@visual` in `key-screens.spec.ts` 2; `visualBaselinesEnabled` 2.

## Deferred follow-up

Deferred follow-up: generate the 60 baselines with `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` (Docker or Podman running), review the images in `sources/ReactApp/tests/e2e/__screenshots__/`, then commit them naming each screen (D-AUTO-1). Generation was proven in the pinned container during this plan (60 written, 60 compared equal on a second run), so the follow-up is the human review and the commit, not a technical unknown.

## Known Stubs

None.

## Threat Flags

None. Captures come from synthetic fixtures only and no image is committed (T-69-37).

## Issues Encountered

- `npm run build:assets` (the Playwright web server) rewrote `src/generated/api.ts` with line-ending-only changes after each run; it was restored with `git checkout` and never committed.
- The `node_modules` directory of the worktree is a junction to the main checkout's install; no package was installed and the lockfile is untouched.

## Next Phase Readiness

- 69-33 and 69-34 can cite the opt-in baseline workflow and the container proof; the human acceptance of the first images is the only open item (VER-03 backstop).

## Self-Check: PASSED

- Files exist: `key-screens.spec.ts`, `visual-stability.spec.ts`, `support/keyScreens.ts`.
- Commits `cb94f630` and `b497201e` are ancestors of HEAD; `git rev-list --count` from `plan_head_before` is 2.
- Acceptance criteria re-run: all pass; no generated image or temporary config is left in the tree.
