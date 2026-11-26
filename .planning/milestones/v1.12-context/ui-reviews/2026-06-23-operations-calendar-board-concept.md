# Operations Calendar And Board Concept - 2026-06-23

## Contract Basis

This concept follows `.planning/ui-reviews/2026-06-22-cinematic-dual-shell-ui-contract.md`.

The public operations surface stays in the Community Shell and uses the accepted dark/gold cinematic operations UI. Calendar is a future mode for date-oriented planning; Board remains the current normal operations view for scanning and slot decisions.

## Concept Images

- Desktop Calendar mode: `.planning/ui-reviews/concepts/operations-calendar-board/01-calendar-mode-desktop.png`
- Desktop Board mode: `.planning/ui-reviews/concepts/operations-calendar-board/02-board-mode-desktop.png`
- Mobile Calendar mode: `.planning/ui-reviews/concepts/operations-calendar-board/03-calendar-mode-mobile.png`

## Routing And Language

Use `/events/upcoming` or a future `/operations` route as the destination for both modes.

Until Calendar mode is implemented, the home page should not say `View calendar`. Use `View operations` or `Open operations board` instead. Once Calendar mode exists, the home page can link to the same operations route with Calendar selected by default.

Recommended labels:

- Home hero primary action: `View slots` when a next operation exists, otherwise `View operations`.
- Home operations band action before Calendar exists: `Open operations board`.
- Home operations band action after Calendar exists: `View calendar`.
- Operations page mode switch: `Calendar` and `Board`.

## Mode Contract

### Calendar Mode

Calendar mode is the date-planning view.

Desktop uses:

- Cinematic Community Shell header.
- Operations page title, not a marketing homepage hero.
- Segmented mode switch with `Calendar` selected and `Board` available.
- Month grid with thin borders, compact event strips, and a selected-day highlight.
- Right-side selected-operation detail panel with date, time, terrain, mod pack, host, slot progress, status, and `View slots`.

Mobile uses:

- Agenda-first layout, not a compressed desktop month grid.
- Horizontal week strip for date selection.
- Collapsible month overview.
- Vertical operation agenda grouped by date.
- Large touch targets for `View slots`.

### Board Mode

Board mode is the current implemented operations view.

Use:

- Same page header and segmented mode switch with `Board` selected.
- Existing filters: `All`, `Open`, `Confirmed`, `Needs signups`.
- Three-column desktop operation-card grid.
- Featured next operation card with gold border and primary action.
- Existing card facts: date, time, terrain, mod pack, host, slots, attendance, and slot progress.

## Interaction Rules

- Switching between Calendar and Board must not navigate to unrelated pages.
- Preserve active filters where practical, but date selection belongs to Calendar mode only.
- `View slots` remains the primary operation action in both modes.
- Calendar event strips and agenda rows open the same event detail route as Board cards.
- Do not expose Command Center, cloud, server, billing, or audit workflows in this public mode switch.

## Implementation Notes

- Keep event data source aligned with the current upcoming operations repository.
- Add a route query such as `?mode=calendar` or `?mode=board` only if deep-linking is useful; otherwise keep local UI state.
- Calendar implementation should have loading, empty, failed-load, and no-matching-events states matching the existing board page.
- The Calendar month grid should render solid panels and real HTML text; generated concept imagery is visual direction only.
- Use gold for selected mode, selected date, and primary action. Keep low-slot or attention states amber.

## Acceptance Criteria

- The home page no longer promotes an unavailable Calendar mode.
- Calendar and Board are clearly two modes of the same public operations surface.
- Board mode continues to match the existing operations-card workflow.
- Calendar mode is useful on mobile without squeezing a desktop grid.
- Both modes remain in the Community Shell and visually match the cinematic dark/gold system.
