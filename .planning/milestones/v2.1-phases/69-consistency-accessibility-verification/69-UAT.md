---
status: complete
phase: 69-consistency-accessibility-verification
source: [69-VERIFICATION.md]
started: 2026-11-26T10:00:00Z
updated: 2026-11-25T23:17:51.825Z
---

## Current Test

[testing complete]

## Tests

### 1. Generate and accept the 60 visual baselines: `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` (Docker or Podman running), review the images in sources/ReactApp/tests/e2e/__screenshots__/, commit them, then run with UTF_VISUAL_BASELINES=1
expected: Fifteen key screens (K-01..K-15) in mobile-320 and mobile-390, English and German, match their accepted baselines
result: skipped
reason: "Deferred follow-up: milestone-end pass. Baselines are generated after Phase 70 (UI Polishing) changes the key screens; must be accepted before the milestone is archived"

### 2. Screen-reader pass (NVDA or VoiceOver) over the fleet, a workspace, the create flow, the Automation panel, and a 'confirm in a dialog, then a toast' case
expected: The persistent polite and assertive toast regions announce a saved or failed result once, without reading the dismiss button name (review 69-37 IN-01) and also when the toast arrives right after a modal dialog closes (IN-02); error panels are announced assertively; dialog titles and tab or segment controls are spoken sensibly in English and German
result: skipped
reason: "Deferred follow-up: milestone-end human pass (screen reader) per project policy"

### 3. Mixed pointer and keyboard pass on the mobile navigations at 390 px. (a) Community menu open, tap 'Sign in' (signed out), click blank area inside the sign-in dialog, press Tab and Shift+Tab, then Escape. (b) Command Center drawer: click the brand block or the blank area below the last link, press Tab and Shift+Tab. (c) Community menu with the profile popover opened by mouse: Tab from a middle item, then Escape twice
expected: (a) Tab moves between the dialog controls (review 69-REVIEW WR-01 predicts Tab is dead until a control is clicked; Escape must still close the dialog); (b) and (c) focus stays in the drawer or popover, one Escape closes only the popover, the second closes the menu
result: skipped
reason: "Deferred follow-up: milestone-end human pass; review WR-01 (Tab after blank click in a dialog over the community menu) to be fixed with the UI polish work"

### 4. Real phone check at 320 and 390 px (fleet, workspace section, create flow, Automation panel, both mobile navigations)
expected: No layout, touch-target or browser-chrome problems; the workspace refresh button target-size case is confirmed or cleared
result: skipped
reason: "Deferred follow-up: pre-release real-device testing at milestone end"

### 5. Seeded-stack UAT: `./scripts/Start-Uat.ps1 -Fresh -NoBrowser` then `npm run test:uat`, and the real-environment create to Running journey
expected: Seeded journeys pass fresh and after a restart; create reaches Running against a real provider and worker
result: skipped
reason: "Deferred follow-up: pre-release real-environment testing — all real tests are done at the very end, when there is something releasable"

### 6. Native beforeunload prompt: edit a configuration profile form and close or reload the tab
expected: The browser's native leave prompt appears
result: skipped
reason: "Deferred follow-up: milestone-end human pass (native browser prompt)"

### 7. German hero balance: review the public hero pages and calendar controls in German
expected: Balanced layout, no awkward wrapping
result: skipped
reason: "Deferred follow-up: milestone-end visual review, after Phase 70 UI Polishing"

## Summary

total: 7
passed: 0
issues: 0
pending: 0
skipped: 7
blocked: 0

## Gaps

## Deferred Follow-Ups

- test: 1
  idea: "milestone-end pass. Baselines are generated after Phase 70 (UI Polishing) changes the key screens; must be accepted before the milestone is archived"
  deferred_at: 2026-11-26
- test: 2
  idea: "milestone-end human pass (screen reader) per project policy"
  deferred_at: 2026-11-26
- test: 3
  idea: "milestone-end human pass; review WR-01 (Tab after blank click in a dialog over the community menu) to be fixed with the UI polish work"
  deferred_at: 2026-11-26
- test: 4
  idea: "pre-release real-device testing at milestone end"
  deferred_at: 2026-11-26
- test: 5
  idea: "pre-release real-environment testing — all real tests are done at the very end, when there is something releasable"
  deferred_at: 2026-11-26
- test: 6
  idea: "milestone-end human pass (native browser prompt)"
  deferred_at: 2026-11-26
- test: 7
  idea: "milestone-end visual review, after Phase 70 UI Polishing"
  deferred_at: 2026-11-26
