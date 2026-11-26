---
phase: 65-change-a-server
plan: 13
subsystem: ui
tags: [react, hosting, mod-packs, staged-changes, review-diff, accessibility, vitest]

requires:
  - phase: 65-07
    provides: the Settings draft provider (stageModPack, unstageModPack), the pending bar and the review dialog with applyBlockedReason and renderItemExtra
  - phase: 65-09
    provides: hostingService.modPackContents, its query key and the availability fields on mod pack options
  - phase: 65-10
    provides: the reviewed Settings section the Mods panel is mounted in
provides:
  - ModPackPanel, the capability-driven Mods panel of Settings with a native chooser, current and staged lines, a contents disclosure and an unavailable-packs disclosure
  - modPackDiff and useModPackComparison, the pure contents diff and the one comparison hook shared by the review item and the Apply block
  - ModPackDiff, the Added, Removed and Unchanged review rendering with its comparing, failed, unknown and same states
  - The mod pack change moved out of the Machine section into the staged draft
affects: [65-14, 65-15]

plan_head_before: 0111639612a7050f81bed41654ad170b9465e9e7
plan_head_after: d5fbeaf5bbec6617dc0b1820bfe066cfb4932579

actuals:
  tokens: 16000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "API titles inside translated sentences are built with a private marker and split back out, so the title renders in ApiText while the surrounding sentence keeps the page language"
    - "One comparison hook (useModPackComparison) feeds both the dialog extra and the Apply block; a null side is an empty list and issues no request"
    - "Contents are cached per server and pack under hostingQueryKeys.modPackContents, shared by the panel disclosure and the review comparison"

key-files:
  created:
    - sources/ReactApp/src/components/server/ModPackPanel.tsx
    - sources/ReactApp/src/components/server/ModPackDiff.tsx
    - sources/ReactApp/src/features/hosting/modPackDiff.ts
    - sources/ReactApp/src/components/server/mods.css
    - sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx
    - sources/ReactApp/tests/hosting/mod-pack-diff.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - sources/ReactApp/tests/hosting/workspace-page.test.tsx
    - sources/ReactApp/tests/mocks/react-icons.tsx

key-decisions:
  - "The panel renders nothing until the capability is known and supports mod packs, so a game without mod pack support issues no mod pack request and shows no empty heading"
  - "The saved pack stays a selectable option even when the API marks it unavailable, so the select never shows a value it cannot represent; it also appears in the unavailable list with its reason"
  - "A required-pack game with no saved pack shows a disabled No mod pack placeholder, so the select still represents its value without offering the option"
  - "The contents of the effective selection render as a collapsed disclosure only when there are mods; loading, failed, unavailable and empty contents are one plain line each, visible without opening anything"
  - "Review groups without mods are left out; the count stays visible in the heading of each group that is shown"

patterns-established:
  - "Panel extras for the review dialog are passed through renderItemExtra keyed by the review item id, with the Apply block taken from the same hook"

requirements-completed: [CHG-01, CHG-03]

coverage:
  - id: D1
    description: "Settings shows a Mods panel before Configuration for games whose capability supports mod packs; the native chooser lists No mod pack (only when not required), the available packs with the current one marked, and unavailable packs disabled and last; an option without availability data is selectable; the panel and its request are absent when the capability has no mod packs"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#shows Mods before Configuration with the chooser and the currently applied line"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#leaves out No mod pack when the capability requires a pack"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#renders neither the panel nor a mod pack request when the capability has no mod packs"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#treats an option without availability data as selectable and matches the game ignoring case"
        status: pass
    human_judgment: false
  - id: D2
    description: "Choosing another pack stages it at once with the staged line, the head badge and the pending bar; choosing the saved pack again removes it; the review shows Before and After and Apply calls the existing change endpoint once; read-only viewers see the select disabled"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#stages another pack at once and removes the change when the saved pack is chosen again"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#reviews the pack change with Before and After and applies it once"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#shows the select disabled with the read-only notice for a viewer"
        status: pass
    human_judgment: false
  - id: D3
    description: "The Machine section no longer offers a mod pack control or requests mod pack options; runtime profile, storage and terrain and scenario stay immediate and unchanged"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx#keeps the runtime profile, storage and content panels and offers no mod pack control (D-01)"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/workspace-page.test.tsx#changes desired compute and storage selections without external provider calls"
        status: pass
    human_judgment: false
  - id: D4
    description: "Mod packs loading, load error with Try again, and no packs for the game each show their own copy; the chooser is omitted when there are none"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#shows the load error with Try again and retries the request"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#shows the empty copy and no chooser when the game has no mod packs"
        status: pass
    human_judgment: false
  - id: D5
    description: "The contents disclosure is collapsed, lists the first 10 mods with Show all and Show fewer and follows the staged pack; the unavailable disclosure lists packs with their API reason or the fallback and is omitted when none"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#keeps the contents disclosure collapsed and lists the first ten mods with Show all and Show fewer"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#lists unavailable packs with their API reason or the fallback and omits the list when all are available"
        status: pass
    human_judgment: false
  - id: D6
    description: "The review shows Added, Removed and Unchanged by key with counts, Unchanged collapsed behind Show all, Apply blocked with a reason while contents load, a failed or unknown comparison that keeps Apply available, equal contents in one sentence, and No mod pack requesting only the saved pack"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-diff.test.ts#modPackDiff"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#Mod pack contents diff in the review"
        status: pass
    human_judgment: false
  - id: D7
    description: "No game identifier literal and no raw select or button in the mod components"
    requirement: CHG-01
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/mod-pack-panel.test.tsx#Mod components source scan"
        status: pass
    human_judgment: false
  - id: D8
    description: "Visual rhythm of the Mods panel and the diff groups, wrapping of long pack and mod titles at 320 and 390 px in English and German; jsdom cannot measure layout"
    requirement: CHG-01
    verification: []
    human_judgment: true
    rationale: "The CSS is token-only and passes the style guard, but measured reflow and the long-title walk belong to the end-of-phase UAT and the Phase 69 localization pass"

duration: 14min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 13: Mods panel and the mod pack contents diff Summary

**Members now choose a server's mod pack in a capability-driven Mods panel in Settings, where the change is staged, reviewed with an exact Added, Removed and Unchanged contents diff keyed by mod, and applied through the existing endpoint, while the Machine section no longer applies pack changes immediately.**

## Performance

- **Duration:** 14 min (2026-11-20T22:12Z to 22:26Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 11 (6 created, 5 modified)

## Accomplishments

- Tracer: `ModPackPanel` reads the capability (case-insensitive game match), returns nothing when mod packs are unsupported, and renders the Mods heading, description, head badge and a native `SelectField` with No mod pack (only when not required), available packs with the saved one marked, and unavailable packs disabled and last. Choosing a pack stages it, choosing the saved pack again unstages it. The current and staged lines render the pack title in `ApiText`; loading, load error with Try again and the empty state are separate. The mod pack section, query, state and mutation were removed from `ServerRuntimePanel`.
- Expansion: `modPackDiff` compares by key through Maps (added and unchanged in the order of the new pack, removed in the order of the current pack), and `useModPackComparison` loads the saved and staged pack contents through one `useQueries` call (a null side is an empty list with no request). `ModPackDiff` renders the comparing, failed, unknown and same lines or the Added, Removed and Unchanged groups (first 10 plus Show all, Unchanged collapsed). `SettingsSection` feeds both `applyBlockedReason` and `renderItemExtra` from that one hook. The panel gained the collapsed contents disclosure and the unavailable-packs disclosure.
- 34 new tests across `mod-pack-panel.test.tsx` and `mod-pack-diff.test.ts`, including deferred promises for the loading state and a live source scan.

## Task Commits

1. **Task 1 (tracer): move the mod pack choice into staged settings** - `9c938964` (feat(webapp))
2. **Task 2: show mod pack contents and the review contents diff** - `d5fbeaf5` (feat(webapp))

## Verification Results

- Tracer gate (Task 1, automated only): mod-pack-panel, workspace-section-panels, change-flow and tests/styles green (87), `locales:check`, `typecheck` and `lint` clean, then the broader `tests/hosting tests/styles tests/locales` run (1418 tests) before expanding. Tracer verified end-to-end, then expanded.
- Plan-level: `vitest run tests/hosting tests/styles tests/locales` 89 files, 1440 tests passed; `locales:check` 3526 keys, 0 failures; `typecheck` exit 0; full `npm run lint` exit 0; Prettier clean on every touched file; `git diff --exit-code -- sources/Common/Ui/routes.json` exit 0.
- Acceptance greps: `export function ModPackPanel` 1, `changeModPack` in `ServerRuntimePanel` 0, `modPackOptions` in `ServerRuntimePanel` 0, `ModPackPanel` in `SettingsSection` 2, game literals in the three mod files 0, raw `<select`/`<button` in the panel 0, `export function modPackDiff` 1, `export function ModPackDiff` 1, `applyBlockedReason` in `SettingsSection` 1.
- Repo-wide `prettier --check` still lists files this plan did not touch (as recorded in plans 65-09 and 65-12); none of the files changed here is among them.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The test icon mock lacked the diff marker icons**
- **Found during:** Task 2
- **Issue:** `vitest.config.ts` aliases `@fluentui/react-icons` to `tests/mocks/react-icons.tsx`, which exports only a fixed icon set. `Add16Regular` and `Subtract16Regular` resolved to undefined and the diff group crashed with "Element type is invalid" (file not in the plan list).
- **Fix:** Added both exports to the mock.
- **Files modified:** `tests/mocks/react-icons.tsx`
- **Commit:** d5fbeaf5

**2. [Rule 3 - Blocking] An existing runtime-panel test encoded the removed Machine control**
- **Found during:** Task 1
- **Issue:** `workspace-page.test.tsx` selected a mod pack in the runtime panel and clicked "Save mod pack", which the plan removes (file not in the plan list).
- **Fix:** The case now covers profile and storage only and asserts that no Mod pack control exists; its title no longer mentions the mod pack.
- **Files modified:** `tests/hosting/workspace-page.test.tsx`
- **Commit:** 9c938964

### Plan wording that did not match the model

- The comparing line inside the review item reads "Comparing the mod pack contents…" (`Server_Change_Blocked_Comparing`), the same text as the Apply reason, not "Comparing the contents…". `tests/locales/server-keys.test.ts` pins the Phase 65 key vocabulary and a new key failed it, so the plan's own key list was kept; both lines are on screen and the test asserts two matches.
- Review groups with no mods (for example no added mods when switching to No mod pack) are omitted instead of rendered as "Added (0)"; the count stays visible in every group that is shown.
- Loading, failed, unavailable and empty contents in the panel are plain lines next to the chooser rather than text inside a collapsed disclosure, so the reason is visible without opening anything. The disclosure appears only when the pack has mods.
- The Unchanged group offers Show fewer after Show all so it can collapse again; the plan asked only for Show all.
- `ReviewChangesDialog` needed no change: plan 65-07 already forwards `applyBlockedReason` and `renderItemExtra`.
- The saved pack stays a selectable option when unavailable, and a required-pack game with no saved pack gets a disabled No mod pack placeholder; both keep the controlled select consistent with its value and are additions to the plan text.

**Total deviations:** 2 auto-fixed (both blocking, test-side) plus 6 wording notes. **Impact:** none on scope; every behavior line of both tasks is covered by a test.

## Issues Encountered

None.

## Known Stubs

None.

## Threat Flags

None. T-65-36 is mitigated (pack titles, mod titles and reasons render as React text inside `ApiText`; diff keys are compared as strings in Maps, with a test for `__proto__`, `constructor` and similar keys). T-65-37 is mitigated (contents load only for the saved, staged and effective pack of the viewed server, asserted for the No mod pack case; the API enforces Server.View and game scope per plan 65-05).

## Next Phase Readiness

Plans 65-14 and 65-15 can rely on the Mods panel and the review diff being part of the staged flow. The mod pack end-to-end walk at 320 and 390 px in English and German (staged pack, review diff, apply) is left to the plan 65-15 e2e and the end-of-phase UAT.

## Self-Check: PASSED

- Created and modified files verified on disk; commits `9c938964` and `d5fbeaf5` verified as ancestors of HEAD.
