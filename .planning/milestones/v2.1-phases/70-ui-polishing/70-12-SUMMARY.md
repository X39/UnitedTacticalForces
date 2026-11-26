---
phase: 70-ui-polishing
plan: 12
subsystem: webapp
tags: [react, hosting, credentials, focus, accessibility, css-grid, subgrid]
requires:
  - phase: 70-06
    provides: ServerSecretsPanel composed on SettingsPanel (id server-credentials)
  - phase: 70-09
    provides: SettingsSection leave dialog state
provides:
  - focusRequest prop on ServerSecretsPanel and the focus-after-apply flow from SettingsSection
  - Credential rows on one shared grid with a fixed action column
affects: [credentials panel, Settings section, hosting-files-secrets e2e]
tech-stack:
  added: []
  patterns:
    - "Focus request as { key, seq }: a new seq is a new request, a remount never repeats the last one"
    - "Panel body owns the columns; rows, value block and edit block take them as subgrids"
key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/components/server/credentials.css
    - sources/ReactApp/tests/hosting/credentials-panel.test.tsx
    - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
decisions:
  - "A staged rotation wins over a staged typed value when choosing the credential to focus, because only a rotation shows a one-time value"
  - "The two grid columns live on .credentials (rows are subgrids) instead of on each article, so the action column x is identical across rows by construction"
metrics:
  completed: 2026-11-26
  tasks: 2
status: complete
commits: 2
plan_head_before: e6eece4e142655c27046c8515a4d17ed9555f3c4
plan_head_after: b3366a3c818f091e024b9ad431fb357a32a1f6cf
actuals:
  tokens: 7800
  tasks: 2
  commits: 2
---

# Phase 70 Plan 12: Credential focus after apply and one grid per row Summary

After the member's own apply of a credential change, focus lands on the row's one-time value block (or its status line) and scrolls into view; every credential row now shares one two-column grid with a fixed, end-aligned action column.

## What was built

**Task 1 (tracer), commit daff4931.** `SettingsSection` captures the credential to focus when Apply starts (a complete run keeps no per-item results and the draft is empty afterwards), and `onAllApplied` sets `focusRequest = { key, seq }` instead of bumping the heading focus when such a key exists; configuration-only applies still focus the Settings heading. `ServerSecretsPanel` takes `focusRequest`, opens the `server-credentials` panel if the member closed it, and the matching `SecretRow` focuses on the next animation frame: the one-time block when rendered (`tabIndex -1`, `role="group"`, named through `aria-labelledby` by the once-label), otherwise the `p.credential__status` (`tabIndex -1`). It then calls `scrollIntoView({ block: "nearest", behavior: "auto" })` when available. A request already present when a row mounts is not repeated, so a remount or a background refetch moves no focus. `credentials.css` shows the focus ring (`--utf-focus-ring`) on `:focus` of both targets. The one-time value lifetime rules and the announcement are untouched.

**Task 2, commit b3366a3c.** `.credentials` is a grid with `minmax(0, 1fr) minmax(var(--utf-review-name-min), auto)`; each `.credential`, its `.credential__value` and its `.credential__edit` (with `.credential__edit-actions`) are subgrids. Value actions and the toggle slot sit in column 2 and are end-aligned; at or below 520px the panel is one column and the actions wrap below the value. Every reserved-slot rule (badge slot, two-line status, edit actions and toggle slot) is unchanged.

## Verification

- `tests/hosting/credentials-panel.test.tsx` (73 tests, 4 new for focus), `tests/hosting/change-flow.test.tsx` (17), `tests/styles` (147 passed, 1 skipped): pass. Vitest files were run with `--maxWorkers=2`.
- `npm run typecheck`, `eslint . --max-warnings 0`: clean. Prettier applied with `--end-of-line auto`.
- E2E on chromium (serialized through the lock helper): the whole `hosting-files-secrets.spec.ts` (50 tests, including the Phase 65-19 staging layout test, the no-trace tests and axe) and `hosting-change.spec.ts` (19) pass. New e2e: focus of the one-time block at 390x844 (inside the viewport, above the pending bar when one is shown, ring visible, value in one element only, no trace in storage, cookies, URL or console), 1280px alignment of the action column across rows (also while a row shows its one-time value), and one-column checks at 390 and 320px.

## Deviations from Plan

**1. [Rule 3 - Blocking] The plan's e2e verify command matches no tests.** `playwright.config.ts` runs `hosting-files-secrets.spec.ts` only in the `chromium` project; `mobile-390` and `mobile-320` match only the audit, journeys and mobile specs, so `--project=mobile-390 --project=mobile-320` reports "No tests found". The tests set their own viewport (320x568, 390x844, 1280x720), so they were run under `--project=chromium` instead. No config change was made.

**2. [Design choice] Columns on `.credentials`, rows as subgrids.** The plan put the two columns on `article.credential`. With one grid per article the second column would size to its own content (`auto` maximum), so a row with Copy and Hide would shift that row's action column away from the others. Owning the columns on the panel body satisfies the stated truth (same x in every row) by construction. The `minmax(var(--utf-review-name-min), auto)` and `subgrid` strings are present in `credentials.css`. Named grid areas were replaced by explicit `grid-column` spans.

**3. [Design choice] Credential picked for focus.** The plan says "first staged credential key (display order)". Implemented as: first staged rotation in display order, else first staged typed value, so a mixed apply lands on the row that shows a one-time value.

No auth gates. No threat flags: focus and scroll move only elements that already show the value; nothing new writes it anywhere.

## Known Stubs

None.

## Deferred Items

Real-device, screen-reader and visual-baseline checks stay deferred to milestone end. The mobile `scrollIntoView` position above the pending bar was verified only in emulated Chromium.

## Self-Check: PASSED

- daff4931 and b3366a3c are ancestors of HEAD; the five planned files are modified and committed.
