# Phase 66 - UI Review

**Audited:** 2026-11-22
**Baseline:** 66-UI-SPEC.md (approved 2026-11-21), plus the 66-15..66-17 gap-closure decisions in 66-CONTEXT.md
**Screenshots:** not captured (no dev server on 3000, 5173 or 8080; the full Aspire stack was out of scope). The Playwright spec `tests/e2e/hosting-cost-alerts.spec.ts` was not run. All scores are code-derived.
**Interaction captures:** off (workflow.ui_interaction_capture is false)

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Keys and EN/DE parity are complete (no missing `Server_Alerts_*` key in `de.json`). The banner row copy and the toast set drifted from the contract without the contract being amended. |
| 2. Visuals | 3/4 | Clear hierarchy in the banner and settings. The contract's flat-panel language was replaced by raised cards, and a Dismiss icon plus a status badge were added. None of these is in the contract. |
| 3. Color | 3/4 | Gold is only on "Save alert settings", and the banner stays warning-toned. Badge tones are an unspecified new use of color. Not verifiable visually. |
| 4. Typography | 3/4 | No hard-coded sizes in the audited TSX. Weights go through tokens. No visual check of the badge's font in the hint line. |
| 5. Spacing | 3/4 | Only `--utf-space-*` tokens in `cost-alerts.css`. The contract's 520px stacking and 44px touch rules are present. Row first/last padding overrides and the unreviewed card CSS are unverified at runtime. |
| 6. Experience Design | 3/4 | Strong state coverage: pending, failure, focus management, no timer, stale line, reload on conflict. Gaps are the contract deviations (dismissal toasts, ended-state button removal) and the lack of any rendered verification. |

**Overall: 18/24**

No pillar scores 1, so there are no BLOCKERs. Every finding below is a WARNING.

---

## Top 3 Priority Fixes

1. **The contract and the implementation disagree in several places, and the contract was never amended.** The gap-closure plans changed the design but 66-UI-SPEC.md still states the old one. Examples are raised cards where the spec says "flat (elevation 0)", the banner row text, the extra billing line and the dismissal toasts. This leaves a future audit or re-plan with a false baseline. Fix: add a "Gap-closure amendments (66-15..17)" section to 66-UI-SPEC.md that lists each deviation, or revert the code to the contract.
2. **No rendered evidence exists for the visual pillars.** The gap-closure pass was purely a visual redesign, and no screenshot or visual regression artifact was produced in this audit. Fix: run `tests/e2e/hosting-cost-alerts.spec.ts` against the mocked API at 1440, 768 and 375 in EN and DE. Capture the banner with three long rows, the settings page with badges, and the strip. These are the exact cases the spec calls out for 320px German.
3. **The cost-stop control disappears when the session has ended, and the banner then offers nothing but dismissal.** `CostAlertBanner.tsx` sets `stop = ended ? undefined : costStopAction(...)`. This is consistent with the "Ended line", but the contract says the Ended line is the only addition and the panel action is based on action existence. Fix: confirm this is intended and record it in the amendments. Otherwise render the action by existence and let `ActionUnavailableReason` explain.

Further recommendations (minor):
4. Dismiss buttons carry a `Dismiss16Regular` icon plus the text "Dismiss alert" on every row. With three rows plus "Dismiss all alerts" this is repetitive and not in the contract. Consider dropping the icon from the row buttons.
5. `Server_Workspace_Cost_AccruedUnavailable` remains in the locale files. The contract allows this until Phase 69, so schedule its removal.
6. `AccruedCost.tsx` carries a stale doc comment. It says "the billing line says how the provider bills" but the spec's three-line anatomy has no billing line. Either document it in the contract or remove the line.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- All keys are localized via `t()`. No hard-coded user-facing strings were found in the audited files. `en.json` and `de.json` have the same line count (3697) and no `Server_Alerts_*` key is missing from `de.json`.
- Banned generic labels: none. The buttons are "Dismiss alert", "Dismiss all alerts", "Save alert settings", "Add an amount", "Remove amount", "Reload settings" and "Discard changes", so every button names a verb and a noun.
- WARNING: The banner row line 1 was changed. The spec has `Server_Alerts_Row_Reached`, "{{name}} reached {{threshold}}". The code uses `Server_Alerts_Banner_RowReached`, "{{threshold}} reached", with no server name (`CostAlertBanner.tsx:44-48`). The doc comment justifies this because the workspace heading names the server. This is reasonable, but it is a contract deviation. It also means the spec's `<bdi>` requirement for names does not apply to the banner.
- WARNING: Locale keys exist for `Server_Alerts_Dismissed_Toast` and `Server_Alerts_DismissedAll_Toast_*` (en.json:337-339). The spec says dismissal is silent and gives only the polite "Alert dismissed." announcement. The code header says "success toast confirms (D-17)". Check that D-17 is recorded in CONTEXT, since the UI-SPEC does not list these strings.
- WARNING: The `Server_Workspace_Cost_AccruedBilling_*` keys (en.json:3547-3549) add billing-granularity copy that is not in the Copywriting Contract.
- The button label inside the dismiss button is "Dismiss alert" and its aria-label is "Dismiss alert at {{threshold}}". The accessible name contains the visible text, which satisfies WCAG 2.5.3.

### Pillar 2: Visuals (3/4)
- The banner is a single `StatePanel kind="warning"` with a title, a divided `<ul>`, an older-alerts line, an ended line and one footer. The focal point is clear and it matches the contract.
- WARNING: Both settings panels use `workspace-panel` raised cards (`CostAlertSettingsPanel.tsx` imports `sections.css`). The spec's Layout section says "Panels are flat (elevation 0) with a 1px divider between them". The plans 66-15 to 66-17 changed this on purpose, but the spec says otherwise.
- WARNING: Channel rows carry `StatusBadge` elements (via `channelHint`). This is new and unspecified. The badge sits inside the checkbox description, so it is read as part of the hint. That is acceptable for accessibility, but the contract's three-row layout is changed.
- The icon-only-button check passes: all icons are `aria-hidden` and paired with text. The settings language line uses `Info16Regular` with `aria-hidden`.
- Not verified: the 320px German banner fits one screen (spec requirement). The CSS stacks the row and the footer at <=520px, which is the right mechanism. The rendered result is unchecked.

### Pillar 3: Color (3/4)
- Hard-coded colors: none (grep for hex and rgb in the five audited TSX files returned nothing). `cost-alerts.css` uses `--utf-*` tokens only.
- Gold (`variant="gold"`) appears once, on the Save button (`CostAlertSettingsPanel.tsx:485`). The banner and menu rows use no gold, as the contract requires.
- The banner uses the warning state tokens, not danger. The dismiss error line uses `--utf-state-danger-fg`. Both match the spec.
- WARNING: Badge tones (`channelStatus(...).tone`) introduce a new semantic color use that the contract's Color section does not enumerate. The contrast of these badges against `--surface-raised` is not covered by the contract. Confirm that `tokens-contrast.test.ts` covers the badge token pairs.
- The cost-alert menu row sets `border-left-color: var(--utf-state-warning-border)`, which is a reasonable use of existing tokens.

### Pillar 4: Typography (3/4)
- No `text-*` utility classes are used. The CSS only sets `font-weight: var(--utf-weight-bold)` and `--utf-weight-regular`, so the weights stay at 400 and 700.
- `tabular-nums` is applied to amounts in the banner and menu. The accrued value uses its own class `accrued-cost__value`; the contract requires tabular-nums there, which is in `workspace-shell.css` and was not re-read in this audit.
- `overflow-wrap: anywhere` is set on banner and menu text, with no ellipsis. The one exception is the menu `strong`, which is reset to `text-overflow: clip; white-space: normal`. This is correct for 256-character names.
- WARNING: Not confirmed at runtime: whether the `StatusBadge` inside a hint uses the 12px Label size, or inherits 14px body text in Barlow Condensed. The type contract allows only four sizes.

### Pillar 5: Spacing (3/4)
- `cost-alerts.css` has no spacing literals other than the allowed 1px border. Every gap and padding uses `--utf-space-*`.
- The touch target rules are present: 44px at <=800px for banner and menu buttons.
- WARNING: `.cost-alert-banner__row:first-child { padding-top: 0 }` and `:last-child { padding-bottom: 0 }` mean a single row has neither top nor bottom padding. This is fine inside a panel, but it depends on the `StatePanel` padding and is unverified.
- The contract's reserved status line (`--utf-alerts-status-min-height`) is used by the settings save row. This is not re-checked in CSS in this audit.

### Pillar 6: Experience Design (3/4)
- Loading, error, forbidden and not-found states are handled in `CostAlertsPage.tsx`. The settings panel has pending, invalid, save error, conflict (with Reload) and discard states. `useLeaveGuard` with `ConfirmDialog` protects unsaved changes with "Stay here" as the default focus.
- Dismiss: the row stays until the API accepts, `aria-disabled` is used while pending (not `disabled`, so focus stays), and an inline `role="alert"` is shown on failure. Focus moves to the next row, then the stop button, then the workspace title. Dismiss all moves focus to the title. This matches the contract.
- Accrued: no `setInterval` or `refetchInterval` was found in the accrued code paths audited, and the delayed disconnect (`useDelayedDisconnect`) is reused for the stale line.
- WARNING: The banner hides the cost-stop control once cost is `none` (see Fix 3). For a `maybe` cost state the control stays, which is correct.
- WARNING: Because the Dismiss buttons use `aria-disabled` and ignore clicks while `busy`, the "Dismiss all alerts" button is also blocked whenever any single dismissal is pending, with no visible reason. This is minor.
- Registry audit: skipped. No `components.json` and no shadcn registries (the spec says `shadcn_initialized: false`).

---

## Files Audited
- `sources/ReactApp/src/components/server/AccruedCost.tsx`
- `sources/ReactApp/src/components/server/CostStrip.tsx` (grep only)
- `sources/ReactApp/src/components/server/CostAlertBanner.tsx`
- `sources/ReactApp/src/components/server/CostAlertMenuItem.tsx` (grep only)
- `sources/ReactApp/src/components/server/cost-alerts.css`
- `sources/ReactApp/src/features/hosting/CostAlertSettingsPanel.tsx`
- `sources/ReactApp/src/features/hosting/CostAlertsPage.tsx` (existence and size only)
- `sources/ReactApp/src/locales/en.json`, `sources/ReactApp/src/locales/de.json` (key parity)
- `.planning/phases/66-accrued-cost-cost-alerts/66-UI-SPEC.md` (lines 1-458 of 724 read; the Interaction Contract's cost-stop section and later parts were not read)

## Orchestrator Correction (2026-11-22)

Priority fix 1 ("UI-SPEC drift") is withdrawn. The auditor read only lines 1-458 of `66-UI-SPEC.md`; the contract already carries a `## Gap Closure Amendments (2026-11-21)` section (line 597 onward) that explicitly overrides the earlier rows for the raised cards and channel status badges (66-15), the accrued-row billing line (D-15, 66-16), the banner and menu row presentation and the dismissal toasts (D-16/D-17, 66-17). The code matches the amended contract on those points.

Priority fix 2 (no screenshots) stands as a limitation of this code-only audit; the gap closure plans recorded their own screenshot review and the e2e journey is green.

Priority fix 3 (banner hides cost-stop once the session ended) remains open for triage against the Interaction Contract ("Nothing is rendered when the server has no cost-stop action; the ended line explains why only when the cost state is `none`").
