---
phase: 62-fleet-quick-actions
plan: 08
subsystem: webapp
tags: [react, shared-components, search, filter-chips, live-region, accessibility, css-tokens, vitest]

# Dependency graph
requires:
  - phase: 62-01
    provides: icon mock exports Dismiss20Regular, Search20Regular, Checkmark16Regular and PlugDisconnected20Regular
  - phase: 62-03
    provides: Server_Fleet_Search*, Server_Fleet_Filter*, Server_Fleet_ResultCount_* and Server_Fleet_Live_* keys
  - phase: 62-06
    provides: UpdateStreamStatus and the disconnect epoch exposed by useFleetLiveUpdates
  - phase: 62-07
    provides: FleetFilter, fleetFilters and the counts record from fleetModel.ts
provides:
  - Shared SearchField (instant native search input, icon-only clear control, Escape clears, focus returns)
  - Shared generic FilterChipGroup<TId> (role group, aria-pressed chips, check icon, locale counts)
  - FleetToolbar with the four counted chips and an always-present polite result summary region
  - FleetLiveNotice shown after 3 seconds of continuous disconnection, keyed by the disconnect epoch
  - Optional icon prop on StatePanel
  - Token-only fleet-toolbar.css covered by the server style guard
affects: [62-11, 62-12, 63]

# Actuals (#2632)
actuals:
  tokens: 11500
  tasks: 3
  commits: 3
plan_head_before: 2363ab9e0a552495305edfa5d8b4c0cd526d0b58
plan_head_after: b3f2b2ec2c9cabe747994b60be7b14323d23bf51

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Generic shared controls (SearchField, FilterChipGroup) import their stylesheet from src/components/server so the existing token-only style guard covers them"
    - "Locale-formatted plural text: pass the numeric count for plural selection and the formatted numbers through the i18next replace option"
    - "Delayed visibility without a synchronous setState in an effect: a timer callback records the epoch it was started for, and visibility compares that epoch with the current one"

key-files:
  created:
    - sources/ReactApp/src/components/SearchField.tsx
    - sources/ReactApp/src/components/FilterChipGroup.tsx
    - sources/ReactApp/src/components/server/FleetToolbar.tsx
    - sources/ReactApp/src/components/server/FleetLiveNotice.tsx
    - sources/ReactApp/src/components/server/fleet-toolbar.css
    - sources/ReactApp/tests/search-field.test.tsx
    - sources/ReactApp/tests/filter-chip-group.test.tsx
    - sources/ReactApp/tests/hosting/fleet-toolbar.test.tsx
    - sources/ReactApp/tests/hosting/fleet-live-notice.test.tsx
  modified:
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/tests/primitives.test.tsx

key-decisions:
  - "The result summary formats both numbers with Intl before interpolation through the i18next replace option, while the numeric count still selects the plural form (1234 reads 1,234 or 1.234)"
  - "The FleetLiveNotice timer effect depends on the disconnected flag, the epoch and the delay, not on the raw status, so a reconnecting to unavailable switch in the same episode keeps the notice and only swaps the copy"
  - "The result summary live region is never hidden with display none; it stays in the layout as an empty full-width row while no summary applies"
  - "At or below 800 px the toolbar keeps a wrapping row layout with full-basis children instead of a column direction, because the search field uses a flex-basis that would turn into a height in a column"

patterns-established:
  - "New standard controls are shared components rendered through ActionButton; no raw button element in SearchField or FilterChipGroup"

requirements-completed: [FLT-02, FLT-04, FLT-05]

coverage:
  - id: D1
    description: "SearchField is a labelled native search input that reports every keystroke, shows an icon-only clear control only for a value, clears on the control or Escape and returns focus, and hands its input to a supplied ref"
    requirement: FLT-02
    verification:
      - kind: unit
        ref: "tests/search-field.test.tsx (5 tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "FilterChipGroup renders a named group of aria-pressed chips, exactly one pressed with a check icon, locale counts including zero and German grouping, reports every activation and does not intercept arrow keys"
    requirement: FLT-02
    verification:
      - kind: unit
        ref: "tests/filter-chip-group.test.tsx (5 tests)"
        status: pass
    human_judgment: false
  - id: D3
    description: "FleetToolbar shows the search, the All, Mine, Running and Off chips with counts and an always-present polite summary region with English plural and German text and locale numbers"
    requirement: FLT-02
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-toolbar.test.tsx (5 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "FleetLiveNotice is hidden while connected, appears after 3 seconds of continuous disconnection, restarts the delay for a new epoch, hides silently on recovery, switches to the unavailable copy and calls onReconnect; StatePanel accepts an icon without changing existing kinds"
    requirement: FLT-04
    verification:
      - kind: unit
        ref: "tests/hosting/fleet-live-notice.test.tsx (9 tests)"
        status: pass
      - kind: unit
        ref: "tests/primitives.test.tsx#StatePanel icon (9 tests)"
        status: pass
    human_judgment: false
  - id: D5
    description: "fleet-toolbar.css is token-only and passes the server style guard"
    requirement: FLT-05
    verification:
      - kind: unit
        ref: "tests/styles/server-style-guard.test.ts#keeps fleet-toolbar.css free of raw colours and pixel literals"
        status: pass
    human_judgment: false
  - id: D6
    description: "320 px layout without overflow in German, 44 px touch targets at or below 800 px, and the visual pressed and hover states"
    requirement: FLT-05
    human_judgment: true
    rationale: "jsdom does not compute layout; the page-level Playwright spec of Plan 62-12 and the UAT verify widths, target sizes and visuals once the toolbar is mounted on the page"

# Metrics
duration: 10min
completed: 2026-11-19
status: complete
---

# Phase 62 Plan 08: Fleet Toolbar, Shared Search and Filter Chips, Live Notice Summary

**The fleet now has its interactive controls as stateless, tested components. `SearchField` is a shared instant-search input with an icon-only `ActionButton` clear control and Escape-to-clear that returns focus. `FilterChipGroup<TId>` is a shared `role="group"` of `aria-pressed` chips with a check icon and locale-formatted counts. `FleetToolbar` combines them with the All, Mine, Running and Off counts and an always-present polite result summary ("3 of 12 servers shown", "3 von 12 Server angezeigt"). `FleetLiveNotice` appears only after 3 seconds of continuous disconnection, keyed by the disconnect epoch, in the info tone with the plug icon and Reconnect now. `StatePanel` gained an optional `icon`, and all styles sit in the guarded token-only `fleet-toolbar.css`.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-11-19T09:36:39Z
- **Completed:** 2026-11-19T09:46:43Z
- **Tasks:** 3
- **Files modified:** 11 (9 created, 2 modified)

## Accomplishments
- `SearchField`: visually hidden `label` bound with `useId`, decorative search icon, `input type="search"` with `autoComplete="off"` and `spellCheck={false}`, no form and no submit. The clear control renders only for a non-empty value and is outside the label, so the input's accessible name stays clean. The ref is read only in event handlers.
- `FilterChipGroup<TId extends string>`: one ghost `ActionButton` per item with `className="filter-chip"` and `aria-pressed`. The pressed chip carries `Checkmark16Regular`. The count sits in `.filter-chip__count` behind a space text node, so the accessible name is "Mine 0". Chips are never disabled and never gold. The default formatter is `Intl.NumberFormat(currentLocale())`.
- `FleetToolbar`: builds the chip items from `fleetFilters` and typed `Server_Fleet_Filter_*` keys. The summary `div` (`aria-live="polite"`, `aria-atomic="true"`) is always rendered and has text only when `summary` is not null. The plural form follows the total.
- `FleetLiveNotice`: `shownEpoch` state is set only from a `window.setTimeout` callback and cleared on cleanup. It is visible only while the status is reconnecting or unavailable and `shownEpoch === epoch`. It renders `StatePanel kind="info"` with the plug icon, the variant copy and a ghost Reconnect now button.
- `StatePanel`: optional `icon?: ReactNode` replaces the kind's default icon (including the loading spinner) inside the existing `aria-hidden` wrapper. Every existing kind renders as before, and a test proves it for all seven kinds.
- `fleet-toolbar.css` (tokens only):
  - Search control with a 1px `--utf-border-control` border. The native search cancel glyph is removed.
  - Chips use doubled `.button.filter-chip` selectors that override the legacy nowrap and gold ghost hover. Hover changes only the background through `--utf-motion-fast`.
  - Pressed chips get the overlay background and a 1px `--utf-text` border. Focus uses `--utf-focus-ring`.
  - Toolbar layout: the summary has its own row. At or below 800 px the search and chips take full rows and controls use `--utf-control-height-touch`. At or below 520 px the chips form a two-column grid.
  - The live notice uses the `--utf-state-info-*` tokens.
- 33 new tests. The full ReactApp suite is green (102 files, 850 tests), typecheck and lint (`--max-warnings 0`) are clean, and the style guard covers the new stylesheet.

## Task Commits

1. **Task 1: Shared SearchField and FilterChipGroup with their stylesheet** - `be7478ef` (feat)
2. **Task 2: FleetToolbar with the result summary live region** - `0a9cad0f` (feat)
3. **Task 3: StatePanel icon prop and the delayed FleetLiveNotice** - `b3f2b2ec` (feat)

**Plan metadata:** recorded in the following docs(62-08) commit

## Files Created/Modified
- `sources/ReactApp/src/components/SearchField.tsx` - shared instant search field
- `sources/ReactApp/src/components/FilterChipGroup.tsx` - shared generic toggle chip group
- `sources/ReactApp/src/components/server/FleetToolbar.tsx` - search, counted chips and result summary region
- `sources/ReactApp/src/components/server/FleetLiveNotice.tsx` - delayed, epoch-keyed live updates notice
- `sources/ReactApp/src/components/server/fleet-toolbar.css` - token-only styles for all four components
- `sources/ReactApp/src/components/primitives.tsx` - optional `icon` on `StatePanel`, plus TSDoc for `StatePanel`
- `sources/ReactApp/tests/search-field.test.tsx`, `tests/filter-chip-group.test.tsx`, `tests/hosting/fleet-toolbar.test.tsx`, `tests/hosting/fleet-live-notice.test.tsx`, `tests/primitives.test.tsx` - component tests

## Decisions Made
- **Locale numbers in a plural string.** i18next interpolates `{{count}}` from the raw number, so a total of 1234 would read "1234". The toolbar passes `count: total` for plural selection and `replace: { count, shown }` with the `Intl` formatted strings for interpolation. The locale strings did not change.
- **Timer dependencies.** The notice's effect depends on `disconnected`, `epoch` and `delayMs`. A reconnecting to unavailable switch within one episode therefore neither restarts the delay nor hides an already visible notice. A test covers this.
- **Live region never display none.** The empty summary row stays mounted and visible to assistive technology, so the first summary is announced reliably.
- **Row layout at narrow widths.** The search field's `flex: 1 1 var(--utf-fleet-card-min)` would become a height basis in a column flex container. Below 800 px the toolbar keeps a wrapping row, and the children take `flex: 1 1 100%`.

## Deviations from Plan

None in behavior. Small details:
- `StatePanel` received a TSDoc block, required by the AGENTS.md documentation rule for the touched public member.
- Each task ran its tests first and then the implementation, but was committed as one `feat(webapp)` commit per task, matching the commit messages the plan prescribes.

**Total deviations:** 0 auto-fixed. **Impact:** none.

## Issues Encountered
None.

## Known Stubs

None. The components are complete but not yet mounted on the fleet page; Plan 62-11 wires them.

## Notes for Later Plans
- 62-11: pass `useFleetLiveUpdates().disconnectEpoch` as `epoch` and `reconnectNow` as `onReconnect` to `FleetLiveNotice`. Pass a `searchInputRef` to `FleetToolbar` so the no-match "Clear search and filter" action can focus the input. Pass `summary` as null unless a search or a filter other than All is active.
- The notice relies on the hook's contract that the epoch grows for every new disconnection episode. A drop that reused the old epoch would show the notice immediately.
- `tests/hosting/fleet-fixtures.test.ts` and the CRLF-only `src/auth/claims.ts` still report under `format:check` (pre-existing, see `deferred-items.md`). Every file touched here passes `prettier --check`.

## Next Phase Readiness
Ready for 62-09 to 62-12. The page (62-11) can compose `FleetToolbar` and `FleetLiveNotice` directly. Phase 63 can reuse `SearchField`, `FilterChipGroup` and the `StatePanel` icon.

## Self-Check: PASSED
- FOUND: all 9 created files and both modified files
- FOUND: commits be7478ef, 0a9cad0f and b3f2b2ec (ancestors of HEAD)
- Acceptance:
  - Task 1: `aria-pressed` in FilterChipGroup 2 (at least 1). `type="search"` in SearchField 1. `<button` in SearchField 0 and in FilterChipGroup 0.
  - Task 2: `aria-live="polite"` in FleetToolbar 1. `FilterChipGroup` in FleetToolbar 2. `Metric` in FleetToolbar 0.
  - Task 3: `icon?: ReactNode` in primitives 1. `PlugDisconnected20Regular` in FleetLiveNotice 2. `setInterval` in FleetLiveNotice 0.
- Plan verification: `npm test` (102 files, 850 tests), `typecheck` and `lint` are green, and `tests/styles` passes. `format:check` reports only the two pre-existing deferred files. `git diff --exit-code -- sources/Common/Ui/routes.json` exits 0.
