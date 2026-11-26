# Phase 71 - UI Review

**Audited:** 2026-11-26
**Baseline:** 71-UI-SPEC.md (approved design contract), plus the standing user preferences (no native details, no native select, no layout shift on staged or chosen values, compact aligned dialogs, search on long people lists)
**Screenshots:** not captured. A server answered on localhost:5173, but the Access and Cost owner panels sit behind an authenticated, seeded server workspace, and a capture of `/` would not show them. Visual baselines are deferred to milestone end. All visual findings below are code-derived (CSS, markup, locale keys) and unverified in a browser.
**Interaction captures:** off (`workflow.ui_interaction_capture` is false). Interaction findings are code-derived.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 4/4 | All 126 `Server_Access_*`, `Server_CostOwner_*` and `Picker_*` keys exist in en and de with no gaps. Plurals are correct. Dismiss labels follow the "keep" pattern. |
| 2. Visuals | 3/4 | The combobox, token and disclosure rows follow the contract. The Retry control is unreachable by keyboard, and the dt labels use an uppercase condensed style the spec never declared. |
| 3. Color | 4/4 | Token-only CSS with no literal colors. Gold appears only on the transfer confirm. Warning, danger and neutral tones are used as specified. |
| 4. Typography | 3/4 | Four sizes and two weights hold. `letter-spacing: 0.06em` and `text-transform: uppercase` on `.cost-owner-facts dt` are literals the spec does not declare. |
| 5. Spacing | 3/4 | Spacing is token-only. The no-layout-shift guarantee depends on hand-computed `min-height` calcs that assume a one-line (access) or two-line (cost owner) reason. |
| 6. Experience Design | 2/4 | The picker chooses an option on `pointerdown`, so touch-scrolling the 256px listbox selects an option by accident. The Retry control is also unreachable by keyboard. |

**Overall: 19/24**

Pillar 6 is scored 2 rather than 3 because the touch-selection defect hits the exact long-list case (administrator cost-owner list, up to 50 options) that the spec built the picker for.

---

## Top 3 Priority Fixes

1. **WARNING (borderline BLOCKER on touch): option choice fires on `pointerdown`.** `SearchPicker.tsx:270-274` and `:404`. On a touch device a finger that starts a scroll inside the 256px listbox selects the option under it, closes the listbox and swaps the input for the token before the member can read the list. This affects the administrator cost-owner list and any candidate search with more than about 5 matches at 320px. Fix: for `pointerType === "mouse"` keep the pointerdown choice. For `touch` and `pen`, choose on `click` (or on `pointerup` when the pointer did not move more than a small threshold), and keep `preventDefault` on `mousedown` so the input keeps focus.
2. **WARNING: the error row's Retry button is unreachable by keyboard.** `SearchPicker.tsx:262-264` and `:433-445`. Tab calls `close()`, which sets `expanded` to false and unmounts the error box, so focus can never land on Retry. The only keyboard path is Enter in the input, and nothing on screen says so. Fix: let Tab from the input move to Retry while `status === "error"` (do not close on Tab in that state), or render the Retry action outside the `expanded` gate. Alternatively, add a visible "Press Enter to retry" hint to `Picker_Error`.
3. **WARNING: layout-shift protection rests on fixed line-count assumptions.** `access.css:60-67` reserves one reason line, and `cost-owner.css:96-103` reserves two. The unavailable reasons are `Server_Access_Grant_ChooseMember`, `ChooseRole` and `Server_Access_Grant_CannotGiveView`, plus the cost-owner running and jobs reasons. The jobs and running reasons are much longer than "Choose first", yet the reserve assumes the shorter text. In the narrow `minmax(min-content, 1fr)` column, or in German, the long reasons wrap to 3 or more lines and push the panels below. Fix: reserve the space structurally. Put the reason in a fixed-height slot, or in a full-width row under the picker and button row, rather than a magic `calc()`. Verify at 320, 390 and 800px in German for every reason string.

Further fixes are listed under each pillar below (items 4 to 9).

---

## Detailed Findings

### Pillar 1: Copywriting (4/4)

- A node comparison of en.json and de.json over the prefixes `Server_Access_`, `Server_CostOwner_`, `Picker_` and the new `Server_Settings_*` keys found 126 keys in each, with no key missing from either locale. No German value is an untranslated copy of the English value.
- `_one` and `_other` plural pairs exist for `Picker_Results`, `Server_Access_Role_Members` and `Server_Access_Filter_Result`. The picker takes a `results(count)` function so the plural is chosen by i18next (`SearchPicker.tsx:45-52`). This is cleaner than the spec's separate singular and plural props.
- No dialog uses a Cancel label. The dismiss labels are "Keep access", "Keep permission" and "Don't transfer", as in the Phase 69 contract. The confirm labels are specific ("Remove access", "Transfer cost owner").
- The cost-owner dialog copy states what changes ("From now on ... pays", "Costs already incurred stay with ...", "Cost alerts follow ...") and matches what the service does. The 409 and 400 errors are mapped to actionable text, and server messages are never shown (`useAccessMutations.ts:117-130`).
- Unplanned additions: `Server_Access_NotAssignable`, `Server_Access_Remove_NotAssignable`, `Server_Access_Grant_CannotGiveView` and `Server_Change_ReadOnly_AccessOnly`. They come from the `assignablePermissions` rule, which the spec did not contain. They are well worded. Treat them as scope the contract should be amended to record, not as a defect.
- Minor: `Server_Access_Remove_Confirm` reads "Remove access" while the opening button reads "Remove all access". Both are acceptable, but the confirm no longer says it removes everything.

### Pillar 2: Visuals (3/4)

- Focal point and hierarchy are clear. Each subject is an inline `Disclosure` with an `h3` title, badges (kind, You, "Can't see this server") and a held-permissions summary. The member sees who has what without opening a row (`AccessSubjectRow.tsx:226-253`).
- The preferences are met. There is no native `<details>` and no native `<select>`. The picker is an ARIA 1.2 combobox with an in-flow listbox, so no ancestor `overflow` can clip it. The chosen token has the input's border box and `min-height`, so choosing does not move the layout (`search-picker.css:128-153`). The icon-only clear button has `aria-label={texts.clear}` (`SearchPicker.tsx:337-347`), and decorative icons are `aria-hidden`.
- Long-list filtering is present: `accessFilterThreshold = 8` shows a `SearchField` plus a polite count (`ServerAccessPanel.tsx:49`, `:493-507`). A subject that cannot see the server is never hidden by the filter (`matchesFilter`), which is a good protective choice.
- The state is not carried by color alone. The switches show the Granted and Not granted words. The active option has a fill plus a focus ring. Forced-colors overrides exist.
- WARNING: the keyboard dead end on Retry (Top Fix 2) is a visible-affordance failure. A button is drawn that the keyboard cannot reach.
- Minor: `.cost-owner-facts dt` renders as an uppercase Barlow Condensed label. The spec lists the facts `dt` as a Label (12px, 700) but declares no uppercase or tracking. It is consistent with `--font-label` use elsewhere, so it is acceptable, but it is undeclared.
- Minor: an option is a flat `li` with the name and a muted detail on one wrapping line (`search-picker.css:96-101`). When the detail wraps next to a long nickname, name and detail are not visually grouped. Two equal nicknames are told apart only by the detail text.

### Pillar 3: Color (4/4)

- A hex and `px` scan of `search-picker.css`, `access.css` and `cost-owner.css` found no literal color. Every px hit outside `var(--...)` is inside a comment or a media query breakpoint. The listbox, token and option states use `--utf-surface-overlay`, `--utf-border-control`, `--utf-border-divider`, `--utf-hover` and `--utf-focus-ring`, as in the Listbox and token state table.
- Accent discipline holds. `confirmVariant="gold"` is used once, on the Transfer cost owner dialog (`ServerCostOwnerPanel.tsx:268`). Give access is `primary`. Both the self-removal and the Remove all access dialogs use `danger`. Remove all access opens from a `ghost` button. The 60/30/10 distribution is unchanged, because the panels add only surfaces and neutral and warning badges.
- Warning uses the `StatePanel kind="warning"` and `StatusBadge tone="warning"` pair for no-View and frozen owner. Error uses `--utf-state-danger-fg` on the picker's error row only.
- Minor: the Remove all access button is `ghost`, so a destructive action opens with no destructive cue. That follows the spec (D-09, "opening button is ghost") and is accepted, but it is unusual.

### Pillar 4: Typography (3/4)

- The CSS uses `--utf-text-body`, `--utf-text-label`, `--utf-weight-bold` and `--font-label` only. No new size or weight appears. Sub-headings "Give access" and "People and roles with access" are `h3` at Body 700, as specified (`access.css:31-35`, `:121-126`).
- WARNING (minor): `letter-spacing: 0.06em` and `text-transform: uppercase` on `.cost-owner-facts dt` (`cost-owner.css:46-54`) are literals outside the token set and outside the spec. Either move them into a shared label token or drop them.
- Names, descriptions and reasons use `overflow-wrap: anywhere` with no ellipsis truncation, as the Phase 69 rule requires. Names are inside `<bdi>`.

### Pillar 5: Spacing (3/4)

- Spacing uses `--utf-space-*` tokens throughout, with no arbitrary px or rem values. Touch height (`--utf-control-height-touch`) applies at 800px and below. The two-column to one-column switch grid and the 520px stacking match the layout rules.
- WARNING: `.access-grant__action` (`access.css:60-67`, with overrides at `:171-191` and `:193-211`) and `.cost-owner-transfer__action` (`cost-owner.css:96-103`) hold the button row steady with long `calc()` chains of label height, gap, control height and reason lines. These chains are copied between breakpoints, and the access form assumes one reason line while the cost-owner form assumes two. The access form also shows `Server_Access_Grant_CannotGiveView` (long) in the same narrow `minmax(min-content, 1fr)` cell. A single extra wrapped line moves the list below the form. This is exactly the shift the user's preference forbids.
- The picker's idle hint (`search-picker__hint`) is added below the input in the access form, while the action cell reserves a fixed height. If the hint wraps (German "Tippe, um zu suchen." is short, but the field is narrow at 320px), the row grows and moves content.
- Minor: opening the in-flow listbox moves content below the form. The spec accepts this ("the only element that moves content, only while it is open"), but at 320px a 256px listbox pushes the whole subject list down. Consider keeping the listbox open only while the input has focus (it already closes on blur) and verify the scroll position at the form.

### Pillar 6: Experience Design (2/4)

State coverage is strong:
- Loading, error (with a real retry), empty and filter-empty states exist for the access list (`ServerAccessPanel.tsx:419-434`, `:452-463`). The picker has idle, loading, ready, error and no-match states, with a polite live region for result counts.
- Optimistic toggles roll back and show an inline alert under the failed switch. A 403 maps to its own message. Overlapping toggles are tracked per subject-permission pair (`runToggle`, `ServerAccessPanel.tsx:270-300`). Mutations run in a per-server serial scope, and cache refreshes are exact-key, so a half-typed search is never discarded.
- Self-lockout (removing your own Full control, View or Manage access) and Remove all access use a `ConfirmDialog` with `initialFocus="dismiss"`. A partial failure keeps the dialog open with an alert. Losing manage-access removes the panel with a toast and a focus hand-off (D-15). Focus moves to the list heading after Remove all access.
- The transfer button stays visible and unavailable with a stated reason (running, jobs, no owner chosen). A 409 still maps to a dialog message. Gold confirm has no backdrop close, and the dismiss button holds the initial focus.
- Claim gating: the Access panel renders only with `canManageAccess === true`, and its query lives in a child component that is not mounted otherwise, so there is no request. The Cost owner panel is absent for archived servers. The CostStrip link to `?tab=settings#server-cost-owner` appears only with `canModify` on a non-archived server (`CostStrip.tsx:48-78`).

Defects:
- WARNING: touch selection on `pointerdown` (Top Fix 1). `SearchPicker.tsx:404` uses `onPointerDown`, which on touch fires at finger-down, before a scroll gesture can start. The listbox is a 256px scroll box, so scrolling it selects the option under the finger. On a long administrator list this is a likely accident, and the next step is a financial transfer. The confirmation dialog catches a wrong owner but does not fix the picker.
- WARNING: Retry is keyboard-unreachable (Top Fix 2). Tab calls `close()`, which unmounts the error box.
- WARNING: `onPointerMove` sets the active option on any pointer movement (`SearchPicker.tsx:405-407`). While the keyboard is driving `aria-activedescendant`, a resting mouse that is nudged steals the active option and the announcement. This is minor but real.
- Minor: Tab with the listbox open closes it without selecting the active option (`case "Tab": close()`). That is valid combobox behavior, but members who type a name and press Tab may expect the first match to be taken. It is unspecified, so it is not scored.
- Minor: the cost-owner picker only starts its query on first focus (`engaged`), so the first focus shows a loading row. This is intentional and saves requests.
- Minor: the access list is not pushed when another manager changes it. The spec accepts this with refetch on focus and on panel open.
- The panels' end-to-end coverage (axe, reflow, German walk, visual stability) is claimed by 71-12 in `tests/e2e`. This audit did not run it, so none of it is confirmed here.

---

## Registry Safety

Not applicable. `components.json` does not exist (shadcn not initialized), and the spec lists no third-party registry.

---

## Files Audited

- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/SearchPicker.tsx`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/search-picker.css`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/server/AccessSubjectRow.tsx`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/server/AccessGrantForm.tsx`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/server/access.css`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/server/cost-owner.css`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/components/server/CostStrip.tsx` (link only)
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx`
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/features/hosting/serverAccessModel.ts` (self-confirm rule only)
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/features/hosting/useAccessMutations.ts` (first 140 lines)
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx` (panel wiring only)
- `A:/dev/git/UnitedTacticalForces/sources/ReactApp/src/locales/en.json` and `de.json` (key parity check)
- `A:/dev/git/UnitedTacticalForces/.planning/phases/71-server-access-and-cost-owner-ui/71-UI-SPEC.md` (lines 1 to 387), `71-CONTEXT.md`

Not read in full: the 71-01 to 71-12 PLAN and SUMMARY files (only the 71-12 summary header was grepped), the second half of `71-UI-SPEC.md` (accessibility, data and cache, and verification contracts), `useCostOwnerTransfer.ts` and the remainder of `useAccessMutations.ts`.
