---
status: complete
phase: 70-ui-polishing
source: [70-VERIFICATION.md, 70-15-SUMMARY.md]
started: 2026-11-26T08:20:00Z
updated: 2026-11-26T08:20:00Z
---

## Current Test

[testing complete]

## How to start

- Start the seeded stack: `pwsh ./scripts/Start-Uat.ps1 -Fresh` (from the repository root), stop with Ctrl+C.
- Regenerate visual baselines: `pwsh ./scripts/Update-VisualBaselines.ps1 -Update` (Docker or Podman running), review `sources/ReactApp/tests/e2e/__screenshots__/`, then run with `UTF_VISUAL_BASELINES=1`.

## Tests

### 1. Visual look of the Phase 70 controls and panels on the seeded stack
expected: Checkboxes, radio cards, segmented bars, switches, weekday chips and the styled select look designed rather than native; the Settings tab shows five collapsible panels (Mods, Configuration, Credentials, Automation, Server record) and Automation is reachable without long scrolling; the review dialog is aligned with one compact row per change; the leave dialog reads Discard and leave / Stay here / Review changes on one row; the Files panel is one card with the drop zone; the More actions grid shows no gold buttons
result: skipped
reason: "Deferred follow-up: milestone-end human visual pass on the seeded stack (Start-Uat.ps1 -Fresh), together with the regenerated and accepted visual baselines (VER-03)"

### 2. Screen-reader pass over the new switches, panel disclosures and radio cards
expected: Switches announce their on and off state, disclosures announce expanded or collapsed with their badge, radio cards read their label and description once, in English and German
result: skipped
reason: "Deferred follow-up: milestone-end human pass (screen reader) per project policy"

### 3. Non-chromium check of the select fallback and control geometry (Firefox, Safari or a real phone at 320 and 390 px)
expected: Without `appearance: base-select` the native select fallback stays usable and aligned; drawn controls keep their size and alignment; several geometry specs only run on chromium
result: skipped
reason: "Deferred follow-up: milestone-end real-device and cross-browser pass per project policy"

## Summary

total: 3
passed: 0
issues: 0
pending: 0
skipped: 3

## Gaps

[none]
