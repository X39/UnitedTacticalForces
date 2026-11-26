# Phase 68 - UI Review

**Audited:** 2026-11-25
**Baseline:** 68-UI-SPEC.md (approved design contract)
**Screenshots:** not captured (no dev server on 3000, 5173 or 8080; the Aspire stack was not started per constraints). Code-only audit.
**Interaction captures:** off (workflow.ui_interaction_capture is false). All interaction findings are code-derived.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Contract keys and banned-label rules are met (121 `Server_Automation_*`/trigger keys in both EN and DE). Next-run fallback is a bare "-", and an unknown pending-stop reason renders no reason line. |
| 2. Visuals | 3/4 | Panel anatomy, always-rendered status list, rule blocks, consent dialog and the first-in-slot notice match the spec. Visual layout is unverified by any render, and the second `primary` button (paused notice) competes with Save. |
| 3. Color | 4/4 | `automation.css` and the new components use `--utf-*` tokens only. No hex or rgb literals, no gold, danger color only on the save alert. |
| 4. Typography | 3/4 | Sizes and weights come from the four-size, two-weight token set. Wrapping and no ellipsis follow the contract. German 320px wrapping is unverified, and `letter-spacing: 0.06em` is a literal. |
| 5. Spacing | 3/4 | Token-only spacing and the three additive layout tokens are in place. `--utf-automation-day-min: 5rem` yields 3 weekdays per row at 320px, not the 4 the spec promises. |
| 6. Experience Design | 3/4 | Draft/save/discard, 409 reload, 403 refetch, consent flow, leave guard, unavailable and read-only states and no `refetchInterval` or extra timers are covered. Gaps are below. |

**Overall: 19/24**

---

## Top 3 Priority Fixes

1. **Weekday grid shows 3 per row at 320px, not 4** (WARNING) - On the smallest phones the Mo..Su grid wraps into three rows (3/3/1), and the lone "So." cell looks orphaned. The spec explicitly says "four per row at 320px". `automation.css:218-223` uses `minmax(var(--utf-automation-day-min), 1fr)` with a 5rem (80px) minimum. At 320px the usable rule-block width is about 320 - 2x24 (panel) - 2x16 (rule) - borders, about 230px. Four columns of 80px plus gaps need 344px. Fix: lower the token minimum (about 3.25rem, 52px, still 4-multiple friendly) or use `repeat(4, minmax(0, 1fr))` in the 520px media query, then check that "Wed"/"Mi." fits on one line at the touch height.
2. **Render-level verification of the panel is missing** (WARNING, `needs_human_review`) - No screenshot of the closed and open time zone select, the German notice at 320x568, or the consent dialog alignment exists. The user dislikes unstyled selects and misaligned review dialogs. The code uses the shared `SelectField` (`form-field__control--select`) and the shared `ConfirmDialog` with `dialog--cost` and `cost-confirm__facts`, so the risk is low, but the spec made these a required visual-review capture. Capture them against the seeded stack at milestone end.
3. **Silent or placeholder fallbacks in time and reason copy** (WARNING) - `ScheduleRuleFields.tsx:170` renders "Next run: -" when a saved rule has no `nextRunAt`. That is an unexplained dash in an otherwise sentence-based panel. `AutoStopNotice.tsx:56` drops the reason paragraph when `reasonSentence` returns null (unknown reason from a newer API), so a member sees a shutdown title and consequence with no why. Fix: add a closed fallback sentence for both ("Next run: not scheduled yet." and a generic "An automatic shutdown was announced."), both in EN and DE.

Additional minor items (not in the top 3):

4. The "Confirm scheduled starts" button in the paused notice (`ScheduledStartConsentDialog.tsx:128`) is `variant="primary"`, so with Save also primary the panel can show two primary buttons at once. The spec allows it (it names the button `primary`), but the visual hierarchy is flat. Consider a `secondary`/`ghost` treatment or accept it consciously.
5. `automation.css:66` uses a `0.06em` letter-spacing literal on the status `dt`. Prefer an existing label token if one exists, or record it in the style-guard allowlist.
6. The remove-schedule action has no confirmation, by contract (undone by Discard). That is consistent, but an unsaved rule is lost silently on a mis-click. The polite announcement region covers screen readers only.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

- Primary CTA "Save automation settings" (`Server_Automation_Save`, en.json:3325) matches the contract. Consent dialog uses "Allow scheduled starts" and "Keep editing" (`ScheduledStartConsentDialog.tsx:65-68`). Buttons are verb plus noun: "Add a schedule", "Remove schedule", "Reload settings", "Confirm scheduled starts", "Discard changes" (carried). No "Submit", "OK", "Cancel" or bare "Save" in the new files.
- Rule help text distinguishes cost: "Costs money from the start until the server is shut down." / "Saves the server data, stops the server and ends billing." (en.json:3317-3318). Action labels reuse `Server_Action_Start_Label` and `Server_Action_Stop_Label`, so the closed cost-stop vocabulary holds.
- Notice copy follows the spec: title with `formatInstant`, consequence sentence, relative line.
- Gaps: `ScheduleRuleFields.tsx:170` "-" placeholder; `AutoStopNotice.tsx:56` omitted reason on an unknown value; the stop notice and the Settings status list do not give the lead time of the warning the member just received (acceptable).
- EN and DE key counts are equal (121 each).

### Pillar 2: Visuals (3/4)

- Anatomy matches the spec: `AutomationPanel` replaces the placeholder at the reserved slot, `h2`, lead block, notices, three-row status list (`AutomationStatusList`), three `role="group"` sub-sections with `h3`, rule blocks, add row with counter, save row, hidden polite region (`AutomationPanel.tsx:553-783`).
- Icons are paired with text and `aria-hidden` (`Add16Regular`, `Delete16Regular`); the icon-only-looking Remove button also carries an indexed `aria-label`.
- The `AutoStopNotice` is first in the shell notices (`ServerWorkspacePage.tsx:235`, above `CostAlertBanner`), as contracted. `CostAlertBanner` filters AutoStop alerts out, so there is no duplicate surface.
- The Overview fact renders only when something is configured and omits empty lines (`OverviewSection.tsx:58-109`).
- No `<details>`, no raw `<select>` and no raw `<input>` buttons in any of the new files (grep clean). The time zone uses the shared `SelectField` with the shared select class, which addresses the standing select dislike. The consent dialog reuses `ConfirmDialog` and the cost-confirm facts layout, which addresses the dialog-alignment dislike.
- Not verifiable without a render: focal point on a narrow viewport, select popup appearance, dialog alignment. Scored 3, not 4, for that reason and the two-primary-button case.

### Pillar 3: Color (4/4)

- Zero hex, rgb or hsl literals in `automation.css`, `AutoStopNotice.tsx`, `AutomationStatusList.tsx`, `ScheduleRuleFields.tsx`, `ScheduledStartConsentDialog.tsx` and `AutomationPanel.tsx`.
- Colors used: `--utf-text`, `--utf-text-muted`, `--utf-border-divider`, `--utf-surface-overlay`, `--utf-state-danger-fg` (only on `[role="alert"]` in the save row). Notices use `StatePanel kind="warning"` / `"info"`. No gold on the panel: Save is `primary`, everything else `ghost`, so the header Start remains the one gold control.
- Destructive color is not used on Remove schedule, matching the contract.

### Pillar 4: Typography (3/4)

- Sizes and weights come from `--utf-text-body`, `--utf-text-label`, `--utf-weight-bold` and `--utf-weight-regular`; `h3` is Body 14/700, the rule title is Body 14/700, `dt` is Label 12/700 in `--font-label`. No ad hoc `text-*` or font-weight classes.
- `font-variant-numeric: tabular-nums` is on status `dd` and the counter. Text uses `overflow-wrap: anywhere` and no ellipsis, as the German-expansion rule requires.
- Findings: `letter-spacing: 0.06em` literal (`automation.css:66`). German rendering at 320px was never viewed.

### Pillar 5: Spacing (3/4)

- Every gap and padding is `--utf-space-*`; the only literals are `1px` borders and the `520px`/`800px` breakpoints, which the style guard allows. The three additive tokens exist in `tokens.css:46-48` with the specified values.
- Status line reserves `--utf-automation-status-min-height` so save outcomes cannot shift the buttons.
- 44px touch targets are applied to buttons and rule checkboxes at <= 800px (`automation.css:240-252`). Radio rows, the select, the text fields and the panel-level checkboxes rely on the shared `FormFields` touch styling (not re-verified here).
- Defect: weekday grid minimum (see fix 1).
- Spec deviation worth noting: the spec has the status list switch to a two-column `dt`/`dd` list at <= 520px; this is implemented.

### Pillar 6: Experience Design (3/4)

- State coverage: unavailable panel when `automation` is absent, read-only notice, archived notice (replaces read-only), pristine/dirty/saved/discarded/invalid/saveError/conflict/forbidden save outcomes, a reload action on 409, a refetch on 403, consent dialog with busy and error support, `initialFocus="dismiss"`, and focus return to Save on discard (`AutomationPanel.tsx:486-493`).
- Validation: touched-or-attempted gating of messages, focus moves to the first invalid field on a failed save (`:384-391`).
- Disabled handling: native `disabled` for lack of rights, `aria-disabled` plus read-only for in-flight saves; the Add button gets `aria-disabled` and `aria-describedby` the counter at the limit.
- Capability-driven idle option with a structural reason, not a silent control.
- Timers: only `useNow(30_000)` in the notice; no `setInterval`, `setTimeout` or `refetchInterval` in the new files (grep clean).
- Deep link: `#server-automation` scrolls into view with `behavior: "auto"`.
- Gaps: the two fallbacks in fix 3; no confirmation for Remove schedule (by contract); no screenshots or interaction captures. UAT summary 68-19 shows automated Playwright coverage with two deferred real-environment items (real Hetzner auto-stop, scheduled start completion), which is consistent with the project's deferral rule but means those flows were never exercised visually against a real worker.

---

## Standing user-dislike check

| Dislike | Result |
|---------|--------|
| `<details>` elements | None in the Phase 68 files. |
| Unstyled native `<select>` | The only select is the time zone `SelectField` with the shared styled class. Not render-verified. |
| Misaligned review dialog | Reuses `ConfirmDialog` with `dialog--cost` and `cost-confirm__facts` (same layout as the cost confirmation). Not render-verified. |

## needs_human_review

- Time zone select closed and open (alignment, right padding, popup appearance, 400-entry list).
- Scheduled-start consent dialog alignment in EN and DE, and at 320px.
- Automatic shutdown notice at 320x568 in German with the longest reason sentence (spec: title and reason within one screen).
- Weekday grid at 320px after fix 1.
- Settings panel at 1440, 768 and 375 with a rule that has Failed and Paused run lines.

## Files Audited

- `sources/ReactApp/src/features/hosting/AutomationPanel.tsx`
- `sources/ReactApp/src/features/hosting/automationModel.ts` (structure only)
- `sources/ReactApp/src/features/hosting/useAutoStopNotice.ts`, `useAutomationSettings.ts` (timer scan)
- `sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx`, `SettingsSection.tsx`
- `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx`
- `sources/ReactApp/src/components/server/AutoStopNotice.tsx`
- `sources/ReactApp/src/components/server/AutomationStatusList.tsx` (size and grep checks)
- `sources/ReactApp/src/components/server/ScheduleRuleFields.tsx`
- `sources/ReactApp/src/components/server/ScheduledStartConsentDialog.tsx`
- `sources/ReactApp/src/components/server/CostAlertMenuItem.tsx` (grep only)
- `sources/ReactApp/src/components/server/automation.css`
- `sources/ReactApp/src/styles/tokens.css`
- `sources/ReactApp/src/locales/en.json`, `de.json` (key counts and selected values)
- `.planning/phases/68-auto-stop-schedules/68-UI-SPEC.md` (lines 1-405 read; the remainder of the copy tables was not read)
- `.planning/phases/68-auto-stop-schedules/68-19-SUMMARY.md` (grep)
