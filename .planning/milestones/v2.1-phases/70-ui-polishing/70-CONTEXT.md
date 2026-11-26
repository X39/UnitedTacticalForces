# Phase 70: UI Polishing - Context

**Gathered:** 2026-11-26
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

The React server UI looks finished and is quick to navigate: form controls (radio buttons, checkboxes, selects) have a
designed appearance instead of the plain native look, and settings sections such as Automation are reachable without
long scrolling. Source: Phase 68 UAT test 1 (2026-11-25) and the deferred UI follow-ups of the Phase 65 UAT. Known
items: weekday grid shows two days per row at 320 px where the Phase 68 UI spec says four.

Scope is `sources/ReactApp` only. The legacy Blazor WebApp is not touched.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use the ROADMAP
phase goal, the input list below, and codebase conventions (shared React primitives, EN/DE locale JSON, existing
Playwright/axe gates) to guide decisions. Fix shared primitives once rather than page-locally.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research.

</code_context>

<specifics>
## Specific Ideas (inputs collected from earlier UAT and review)

### Phase 68 UAT test 1 (user, 2026-11-25)
- "Shit works but looks like shit still. Radio buttons and simplistic checkboxes eg. make me wonder where time was
  spend for actual UI work. The general amount of scrolling required to get to these sections is also massive."
- Weekday grid shows two days per row at 320 px; the Phase 68 UI spec says four.

### Phase 65 UAT deferred UI follow-ups (2026-11-21)
- Replace native `<details>` (used in ~90% of places) with a proper shared disclosure component.
- Mod pack select dropdown looks bad; right-hand side lacks padding.
- Review-changes dialog: poor alignment and odd layout; eats too much vertical space with many staged changes.
  Redesign to scale (compact, grouped, scannable diff). Removing items there is fine.
- Changing a configuration value shifts the layout (changed stamp and reset button appear inline). Reserve space.
- Configuration section cannot be filtered or searched; needs a redesign.
- Leave dialog: two button rows and inconsistent styling (Stay barely visible, Discard flat dark red, Review gold in
  a separate row). One row with clear hierarchy; gold for the recommended action is fine. Clicking the backdrop
  should close it as Stay.
- Files panel: list sticks to the bottom, empty folder title, "changes apply immediately" subheader, create-folder
  control disconnected from the list, upload layout problems. Redesign.
- Credentials UI not properly aligned; after saving, the rotated value should be scrolled into view or focused.

### Phase 69 review leftovers (recommended before milestone end)
- WR-01: Tab is dead after a blank-area click inside a modal dialog opened over the open community menu
  (`useDrawerFocus.ts` `cycleTab` should ignore focus when a modal dialog is open). Add a test.
- WR-02: separate "Confirmed team" wording from "Confirmed member" in both locale files.
- IN-08: `loadMediaAsset` should return `null` instead of `undefined`.
- IN-06: one German terminology pass over `de.json` (Modpack / Modpaket / Mod-Paket, Gelände / Karte).
- `.workspace-details__refresh` touch-target size.

### Milestone-end note
VER-03 visual baselines (`scripts/Update-VisualBaselines.ps1`) are generated after this phase, because this phase
changes the key screens.

</specifics>

<deferred>
## Deferred Ideas

Real-environment and real-device checks stay at milestone end (see 69-UAT.md Deferred Follow-Ups).

</deferred>
