---
status: complete
phase: 71-server-access-and-cost-owner-ui
source: [71-VERIFICATION.md, 71-12-SUMMARY.md, 71-REVIEW-DISPOSITION.md]
started: 2026-11-26T19:45:00Z
updated: 2026-11-26T19:45:00Z
---

## Current Test

[testing complete]

## How to start

- Start the seeded stack: `pwsh ./scripts/Start-Uat.ps1 -Fresh` (from the repository root; it opens the fleet page of the React app), stop everything with Ctrl+C.
- Regenerate visual baselines (K-18, K-19 and the re-captured cost strip key screens): `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` (Docker or Podman running), review `sources/ReactApp/tests/e2e/__screenshots__/`, then run with `UTF_VISUAL_BASELINES=1`.

Automated first: `hosting-access.spec.ts` and `hosting-cost-owner.spec.ts` (41 browser flows) and the S-15, S-16, O-10, O-11 audit states (axe, reflow at 320 and 390 px, text spacing, German walk, headings) pass against the mocked API.

## Tests

### 1. Visual look and live API path of the Access and Cost owner panels on the seeded stack
expected: Open a UAT server you own, go to Settings, open Access: give a member access, open their row, switch Operate on and off, remove all access. Open Cost owner on a stopped server: choose another owner, transfer, check the cost strip. Both panels look like the other Settings panels, every change applies at once and is announced, the transfer dialog says exactly what happens, nothing moves when a candidate is chosen, and the German layout fits without sideways scrolling at about 390 px
result: skipped
reason: "Deferred follow-up: milestone-end human visual pass and real-API check on the seeded stack (Start-Uat.ps1 -Fresh), together with the regenerated and accepted visual baselines (VER-03, K-18, K-19)"

### 2. Reason layout between 521 and 800 px in English and German
expected: The unavailable reasons under Give access and Transfer (choose a member first, machine still running, jobs pending) wrap without moving the panels below when the reason appears or clears; the reserved space looks intentional, not like a gap
result: skipped
reason: "Deferred follow-up: milestone-end human visual pass; geometry asserted by the no-shift e2e checks, the look needs human judgment (code review iteration 1 WR-03)"

### 3. Focus after access toggles against the real API
expected: Switching off a member's last permission moves focus to the access list heading once the row disappears, also on a slow connection; switching a permission on someone who stays listed leaves focus on the switch; a later change by another manager does not pull focus
result: skipped
reason: "Deferred follow-up: milestone-end real-API pass on the seeded stack; covered by unit tests with a deferred refetch (code review iterations 2 and 3)"

### 4. Screen-reader pass over the search picker and the permission switches
expected: The combobox announces its expanded state, result count, loading and the "Showing the first 20 matches" row; switches announce their on and off state; the remove and transfer dialogs read their title and consequence once, in English and German (NVDA and VoiceOver)
result: skipped
reason: "Deferred follow-up: milestone-end human pass (screen reader) per project policy; roles and states asserted by tests"

### 5. Touch scrolling of the picker list on a real phone
expected: Scrolling the cost owner list by touch does not choose an owner; a tap chooses it; the list stays inside the viewport at 320 and 390 px
result: skipped
reason: "Deferred follow-up: milestone-end real-device pass per project policy; jsdom covers the pointer-type logic (code review iteration 1 WR-01)"

## Summary

total: 5
passed: 0
issues: 0
pending: 0
skipped: 5

## Gaps

[none]
