---
phase: 70-ui-polishing
verified: 2026-11-26T08:20:00Z
status: passed
score: 13/13 must-haves verified
covered_files:
  - ".planning/phases/70-ui-polishing/70-01-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-01-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-02-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-02-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-03-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-03-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-04-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-04-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-05-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-05-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-06-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-06-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-07-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-07-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-08-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-08-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-09-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-09-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-10-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-10-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-11-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-11-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-12-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-12-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-13-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-13-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-14-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-14-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-15-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-15-SUMMARY.md"
  - ".planning/phases/70-ui-polishing/70-16-PLAN.md"
  - ".planning/phases/70-ui-polishing/70-16-SUMMARY.md"
  - "sources/ReactApp/src/components/Disclosure.tsx"
  - "sources/ReactApp/src/components/FormFields.tsx"
  - "sources/ReactApp/src/components/action-control.css"
  - "sources/ReactApp/src/components/primitives.tsx"
  - "sources/ReactApp/src/components/server/ConfigurationFilter.tsx"
  - "sources/ReactApp/src/components/server/FileBrowser.tsx"
  - "sources/ReactApp/src/components/server/ReviewChangesDialog.tsx"
  - "sources/ReactApp/src/components/server/SettingsPanel.tsx"
  - "sources/ReactApp/src/components/server/automation.css"
  - "sources/ReactApp/src/components/server/change.css"
  - "sources/ReactApp/src/components/server/configurationFilterRules.ts"
  - "sources/ReactApp/src/components/server/files.css"
  - "sources/ReactApp/src/components/server/form-fields.css"
  - "sources/ReactApp/src/components/useDrawerFocus.ts"
  - "sources/ReactApp/src/features/hosting/AutomationPanel.tsx"
  - "sources/ReactApp/src/features/hosting/ServerConfigurationPanel.tsx"
  - "sources/ReactApp/src/features/hosting/ServerFilesPanel.tsx"
  - "sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx"
  - "sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx"
  - "sources/ReactApp/src/locales/de.json"
  - "sources/ReactApp/src/locales/en.json"
covered_digest: "v4:sha256:e4ffd09af76f28f23b262270d2dca04fe54ecbcbe2379093c9605b3972b96461"
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Start the seeded stack with `pwsh ./scripts/Start-Uat.ps1 -Fresh` from the repository root. Open 'UAT 11 Idle shutdown', go to Settings, look at the collapsed panels and summaries, press Expand all, open Automation, change a configuration value, open Review changes, leave through another tab and click the leave dialog backdrop, then look at the Files tab and upload dialog. Narrow the browser to about 390px once."
    expected: "Checkboxes, radios, switches, chips, selects and buttons look designed and consistent; Automation is one tap from the top of Settings; staging moves nothing; the review dialog is compact; the leave dialog is one row with gold Review and closes as Stay on the backdrop; the Files list sits under its heading inside one card."
    why_human: "Deferred follow-up: milestone-end visual review of the polished Settings, Review, Leave and Files UI. 'Looks finished' is a visual judgment; pixel baselines (scripts/Update-VisualBaselines.ps1) are generated at milestone end by the standing policy."
  - test: "Screen-reader pass over the switches (role=switch with state words), the Settings panel disclosures (heading wrapping a button) and the radio cards."
    expected: "Switch name and state, panel heading level and expanded state, and radio group legend are announced correctly."
    why_human: "Deferred follow-up: milestone-end screen-reader pass. Spoken output cannot be asserted by the automated gates (axe and role/name assertions already pass)."
  - test: "Open the Settings panel, a select and the weekday chips in a Firefox and a Safari build (non-chromium)."
    expected: "The select falls back to the padded native control without the base-select popup; chips and switches keep their layout."
    why_human: "Deferred follow-up: real-device and cross-engine check. Several geometry e2e specs (control-target-size, visual-stability, dialog-geometry) skip outside chromium by design (review IN-04 note), so only chromium geometry is machine-verified."
---

# Phase 70: UI Polishing Verification Report

**Phase Goal:** The React server UI looks finished and is quick to navigate: form controls (radio buttons, checkboxes, selects) have a designed appearance instead of the plain native look, and settings sections such as Automation are reachable without long scrolling. Known item: weekday grid shows two days per row at 320 px where the Phase 68 UI spec says four.
**Verified:** 2026-11-26
**Status:** passed (every automatable must-have verified; only deferred human visual items remain)
**Re-verification:** No, initial verification

## Goal Achievement

The ROADMAP lists no Success Criteria for this phase, only a goal and a known item. Truths were derived from the goal, the known item, the 18 decisions D-01..D-18 (defined in `70-UI-SPEC.md`) and the plan `must_haves`. The plans' D-ids are the contract; none were dropped.

### Observable Truths

| #  | Truth | Status | Evidence |
| -- | ----- | ------ | -------- |
| 1  | Checkbox, radio, switch, weekday chip and choice card are drawn over a real native input; no `accent-color` in shared CSS (D-01..D-04) | VERIFIED | `FormFields.tsx:392-652`: `CheckboxField` (box plus `Checkmark16Filled`, `variant="chip"`), `SwitchField` (`role="switch"` on a checkbox input, `aria-hidden` track and state words), `RadioGroupField` (`layout` cards or segmented). `form-fields.css` has `.check-mark*`, `.choice-card`, `.segmented`, `.toggle-chip`, `.switch-*`. `grep accent-color` over `components/**/*.css` finds only a comment in `files.css:475`. Style guard in `tests/styles` passed. |
| 2  | Call sites use the right control per semantics (D-02) | VERIFIED | `SwitchField` used in `ConfigurationEntryField:225`, `AutomationPanel:608,655`, `CostAlertSettingsPanel:421,432,461`; chip variant in `ScheduleRuleFields:252`; `RadioGroupField` cards in `AfterCreatingChoice`, segmented in `ConfigurationProfileAccessSection:166`, `ServerConfigurationPanel:434`, `ServerFilesPanel:676`. |
| 3  | Select has a designed surface and a token-styled `base-select` popup, native fallback elsewhere (D-06) | VERIFIED | `form-fields.css:90-135` (`.form-field__control--select` hover layer, `@supports (appearance: base-select)` with `::picker(select)`). No raw `<select>` outside `FormFields.tsx` (grep of `src`). |
| 4  | Native `<details>` replaced by one shared disclosure with heading level, id, badge, panel variant (D-07) | VERIFIED | `grep "<details"` across `src` returns nothing; `Disclosure.tsx` (123 lines) plus `tests/disclosure.test.tsx` passed. |
| 5  | Five button variants live once, token-only, in `action-control.css`; ghost has no gold hover; danger is a tinted outline (D-08) | VERIFIED | `action-control.css:1-147` read in full: tokens only, hover layer "never gold", `.button--danger` uses `--utf-state-danger-*`. `tokens-contrast.test.ts` and `server-style-guard.test.ts` passed. |
| 6  | Settings is a stack of five collapsible panels with live summaries; Automation is reachable without long scrolling; deep link, staging and invalid field open a panel (D-09) | VERIFIED | `SettingsPanel.tsx` is substantive (hash opens, scrolls below sticky chrome, focuses header button; staged keys open and pin; body stays mounted). Used by `ModPackPanel`, `ServerConfigurationPanel`, `ServerSecretsPanel`, `AutomationPanel` and `SettingsSection:303` (Server record). `settings-panel.test.tsx` and `settings-section.test.tsx` passed. Credentials return null without `canManageSecrets` (security T-70-09, closed). |
| 7  | Configuration has search plus a three-chip filter; filter state is component state (D-10) | VERIFIED | `ConfigurationFilter.tsx`, `configurationFilterRules.ts` (`filterEntries`, `chipKeys`, `countEntries`), wired in `ServerConfigurationPanel:11,137`. Searches name, group and description only (T-70-17). `configuration-filter.test.ts` passed. Review WR-01 fix (pinned rows) present and tested. |
| 8  | Staging a configuration value shifts no field or later panel (D-11) | VERIFIED | Real-browser check run by this verifier: `hosting-change.spec.ts` "change layout stability" on chromium and mobile-390, en and de, 1280 and 390 wide: 4 passed. |
| 9  | Review dialog is compact, one row per change, keeps `ConfirmDialog`, overrides only width (D-12) | VERIFIED | `change.css:105-107` is the single `.dialog.dialog--review` width rule; `ReviewChangesDialog.tsx`, `ChangeDiffItem.tsx`; `review-changes-dialog.test.tsx` passed. 70-15 e2e covered the 40-row dialog (orchestrator evidence). |
| 10 | Leave dialog is one row, Discard at start, gold Review, backdrop equals Stay (D-13) | VERIFIED | `primitives.tsx:388-402,513-523,565,623`: `closeOnBackdrop` guarded by `!busy && startedOnBackdrop && isOnBackdrop`, off by default; `confirmPlacement`, `dismissVariant`. Used only at `SettingsSection.tsx:358,375-377`. `primitives.test.tsx` and leave-guard tests passed. |
| 11 | Files panel is rebuilt around one `file-browser` card; list sits under its heading; Persistent/Live is a segmented radio group (D-14) | VERIFIED | `FileBrowser.tsx` (toolbar, tree pane, list pane, footer slots), `files.css` (652 lines, guarded), `ServerFilesPanel.tsx:676` segmented radio; footer note key `Server_Files_Footer_Immediate` present. Review fixes WR-02 (stray drop) and WR-03 (focus after remove) in place. |
| 12 | Credentials use a fixed-action grid and focus plus scroll the one-time value after Rotate or Generate (D-15) | VERIFIED | `ServerSecretsPanel.tsx:155-175`: `focusSeq` effect focuses `valueRef` (fallback status line), `scrollIntoView({ block: "nearest" })`, one retry for a still-hidden panel, no repeat on remount. `credentials-panel.test.tsx` passed. |
| 13 | Weekday grid shows four chips per row at 320 px (known item) and seven at 1280 (D-05) | VERIFIED | `automation.css:217-259` (7 columns, 4 at `max-width: 520px`). Real-browser check run by this verifier: `hosting-automation.spec.ts` weekday chips en and de at 320, 390, 1280: 6 passed; the spec asserts exactly 4 chips share the first row at 320 and 390, no clipping, no horizontal scroll. |

**Score:** 13/13 truths verified (0 present, behavior-unverified). Cross-cutting decisions D-16, D-17 and D-18 are covered below.

| Decision | Status | Evidence |
| -------- | ------ | -------- |
| D-16 WR-01 modal Tab | VERIFIED | `useDrawerFocus.ts:118` returns early when `dialog[open]` exists; `shell-layouts.test.tsx` passed. |
| D-16 WR-02 / IN-06 | VERIFIED | `en.json:2280` "Confirmed team" vs `:2279` "Confirmed member"; German `Bestätigtes Team`; `Modpaket` and `Gelände` count 0 in `de.json`; `tests/locales` passed (de-glossary, distinct-wording). |
| D-16 IN-08 | VERIFIED | `GalleryAssetPicker.tsx:86` uses `loadMediaAsset`; `siteImages.ts` changed; `admin-site-images` tests in the orchestrator's full run (3,555 passed). |
| D-16 refresh target | VERIFIED | `.workspace-details__refresh .button` asserted at touch height in `tests/e2e/audit/control-target-size.spec.ts` (70-15 gate, mobile-390 and mobile-320). |
| D-17 no new dependency | VERIFIED | `git diff d2d45ebe^..HEAD` over `sources/ReactApp/package.json`, `package-lock.json`, `sources/WebApp`, `sources/Api` is empty. |
| D-18 Phase 69 gates stay green | VERIFIED | Orchestrator: 215 Vitest files, 3,555 passed, 2 skipped; lint, typecheck, locales, contracts, build green; 776 Playwright passed on chromium, mobile-390, mobile-320 (audit, a11y, hosting specs). Spot-checked here (see below). |

### Requirements Coverage

`.planning/REQUIREMENTS.md` maps no milestone requirement to Phase 70 (the traceability table ends at Phase 69; ROADMAP shows `Requirements: TBD`). So there are no orphaned milestone requirements. The plans use the phase decision ids D-01..D-18, defined in `70-UI-SPEC.md` (and indexed in `70-RESEARCH.md`), as their `requirements:`.

| Decision | Source Plan(s) | Status | Evidence |
| -------- | -------------- | ------ | -------- |
| D-01 | 70-01, 70-05 | SATISFIED | Truth 1 |
| D-02 | 70-05, 70-08, 70-10 | SATISFIED | Truths 1, 2 |
| D-03 | 70-05, 70-11 | SATISFIED | Truth 1 (cards, segmented) |
| D-04 | 70-05, 70-08 | SATISFIED | Truth 1 (`SwitchField`) |
| D-05 | 70-05, 70-08 | SATISFIED | Truth 13 |
| D-06 | 70-05, 70-10 | SATISFIED | Truth 3 |
| D-07 | 70-03 | SATISFIED | Truth 4 |
| D-08 | 70-01, 70-04 | SATISFIED | Truth 5 |
| D-09 | 70-06, 70-10, 70-11, 70-15, 70-16 | SATISFIED | Truth 6 |
| D-10 | 70-11 | SATISFIED | Truth 7 |
| D-11 | 70-10 | SATISFIED | Truth 8 |
| D-12 | 70-13 | SATISFIED | Truth 9 |
| D-13 | 70-03, 70-09 | SATISFIED | Truth 10 |
| D-14 | 70-07, 70-14 | SATISFIED | Truth 11 |
| D-15 | 70-12 | SATISFIED | Truth 12 |
| D-16 | 70-01, 70-02 | SATISFIED | D-16 rows above |
| D-17 | 70-01, 70-05 | SATISFIED | no dependency change |
| D-18 | 70-01, 70-15 | SATISFIED | gates green |

All 18 ids appear in PLAN frontmatter and every one is accounted for; no PLAN id is undefined in the spec.

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `components/FormFields.tsx` | drawn check, radio, switch, chip, select | VERIFIED | 652 lines, substantive, imported across hosting features |
| `components/server/form-fields.css` | drawn marks, cards, segmented, switch, chip, `base-select` | VERIFIED | 848 lines, guarded by style test |
| `components/action-control.css` | five token-only button variants | VERIFIED | 147 lines, read in full |
| `components/Disclosure.tsx` | shared disclosure | VERIFIED | used by `SettingsPanel` and Advanced options |
| `components/server/SettingsPanel.tsx` (+ `useSettingsPanelState`) | collapsible Settings panel | VERIFIED | used by five panels |
| `components/server/ConfigurationFilter.tsx`, `configurationFilterRules.ts` | search and chips | VERIFIED | wired in `ServerConfigurationPanel` |
| `components/server/FileBrowser.tsx`, `files.css` | Files card | VERIFIED | wired in `ServerFilesPanel` |
| `components/server/ReviewChangesDialog.tsx`, `change.css` | compact review | VERIFIED | one width rule |
| `components/primitives.tsx` `ConfirmDialog` | `closeOnBackdrop`, `confirmPlacement`, `dismissVariant`, `blockFileDrops` | VERIFIED | `primitives.tsx:388-630` |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| `SettingsSection` | five panels | `SettingsPanel` composition | WIRED | `ModPackPanel`, `ServerConfigurationPanel`, `ServerSecretsPanel`, `AutomationPanel`, record panel at `SettingsSection:303` |
| `SettingsSection` | leave and discard dialogs | `closeOnBackdrop`, `confirmPlacement="start"`, `dismissVariant="primary"` | WIRED | `SettingsSection.tsx:358,375-377` |
| `ScheduleRuleFields` | weekday chips | `CheckboxField variant="chip"` | WIRED | `:252` |
| `AutomationPanel`, `CostAlertSettingsPanel`, `ConfigurationEntryField` | `SwitchField` | direct use | WIRED | see Truth 2 |
| `ServerFilesPanel` | `FileBrowser` | slots | WIRED | segmented context radio at `:676` |

### Data-Flow Trace (Level 4)

Presentation-only phase; no new data source. The Settings summaries, filter counts and credentials focus read existing query and draft state (checked in `ServerConfigurationPanel`, `ServerSecretsPanel`). No hollow props or static fallbacks found.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Core Phase 70 unit and style suites | `npx vitest run tests/form-fields tests/disclosure tests/settings-panel tests/hosting/settings-section tests/hosting/configuration-filter tests/primitives tests/styles tests/locales tests/hosting/review-changes-dialog tests/hosting/credentials-panel tests/shell-layouts` | 22 files, 642 passed, 1 skipped | PASS |
| Weekday grid at 320, 390 and 1280, en and de | `e2e-serial.mjs ... --project=chromium --project=mobile-320 tests/e2e/hosting-automation.spec.ts -g weekday.chips` | 6 passed (4 per row asserted at 320 and 390) | PASS |
| No layout shift on staging | `e2e-serial.mjs ... --project=chromium --project=mobile-390 tests/e2e/hosting-change.spec.ts -g layout.stability` | 4 passed | PASS |

Full Vitest and the 776-test Playwright gate were not re-run; the orchestrator's recorded results were relied on and the targeted subsets above agree with them. One note: the helper spawns through `cmd`, so a `|` inside a `-g` pattern breaks the command; the patterns above avoid it.

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| `locales/en.json` | 2298 | text "TBD" | Info | User-facing label value ("To be determined"), not a debt marker |
| `features/admin/EventObjectiveIconPicker.tsx:69`, `EventSlotPlanner.tsx:315,326`, `features/events/EventRoleSymbolPicker.tsx:113` | n/a | raw native radio and checkbox | Info | Explicit non-goal in `70-UI-SPEC.md` (line 332, Non-goals line 536): the three admin event editors stay on the Phase 69 ratchet. Not a gap against the roadmap goal, which targets the React server UI. |
| `styles.css`, `hosting.css` | n/a | legacy raw colors, five gold `accent-color` leftovers (`hosting.css:225,793,935,1050,1674`, `styles.css:3472`) | Info | Reported by the UI audit; outside the guarded shared sheets, which is where D-01 forbids `accent-color`. Not on the new controls. Candidate cleanup for a later phase. |
| `tests/setup.ts:17-20` | n/a | global `seedSettingsPanels()` | Info | Review IN-04, skipped by design; S-14 audit and `settings-panel.test.tsx` cover the collapsed default. |

No unreferenced `TBD`, `FIXME` or `XXX` marker in any of the 64 `src` files changed by the phase. No blocker anti-pattern.

### Gates Already Run (relied on, spot-checked)

- Code review: 7 findings, 6 fixed, IN-04 skipped; re-review clean (`70-REVIEW.md`, `70-REVIEW-DISPOSITION.md`). Fix commits `10d564f4..1bfe67cc` are ancestors of HEAD and their tests ran green here (`configuration-filter`, `primitives`, `disclosure` inside the 642).
- Security: 25 threats, 0 open (`70-SECURITY.md`).
- UI audit 18/24, code-only; remaining items are the stated non-goals above and the deferred visual judgment.
- Validation: `nyquist_compliant: true`; manual-only rows are marked "Deferred follow-up".

### Human Verification Required (all deferred by the standing policy)

1. **Visual look check on the seeded stack.** Test: start `pwsh ./scripts/Start-Uat.ps1 -Fresh`, walk Settings (collapsed panels, summaries, Expand all, Automation, change a value, Review changes, leave dialog backdrop), Files and the upload dialog, and narrow to about 390px. Expected: controls look designed and consistent, Automation is one tap from the top, staging moves nothing, review is compact, leave is one row with gold Review and a backdrop click is Stay, Files list sits under its heading in one card. Why human: Deferred follow-up: milestone-end visual review with regenerated baselines.
2. **Screen-reader pass.** Deferred follow-up: milestone-end pass over switches, panel disclosures and radio cards.
3. **Non-chromium engines.** Deferred follow-up: Firefox and Safari check of the select fallback and control geometry; several geometry specs skip outside chromium.

None of these blocks the phase: every geometry, focus, state and contract promise that can be asserted automatically has been asserted, and the two rendered-geometry claims the UI audit asked to prove (weekday grid at 320, no layout shift) were re-run here and pass.

### Gaps Summary

No gaps. All 18 decision ids are traced to implemented, wired and tested code; the known 320 px weekday item is fixed and proven in a real browser. REQUIREMENTS.md maps nothing to Phase 70, so there is no orphaned requirement. Housekeeping for the orchestrator: ROADMAP.md line 52 still shows `- [ ] **Phase 70: UI Polishing**` although the plan list reads 16/16 executed and the progress table has no Phase 70 row.

---

_Verified: 2026-11-26_
_Verifier: Claude (gsd-verifier)_
