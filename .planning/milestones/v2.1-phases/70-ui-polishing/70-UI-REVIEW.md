# Phase 70 - UI Review

**Audited:** 2026-11-26
**Baseline:** 70-UI-SPEC.md (approved design contract)
**Screenshots:** not captured (code-only audit; no Playwright run, the environment notes allow this)
**Interaction captures:** off (workflow.ui_interaction_capture is false). Every interaction finding below is code-derived.

Deferred to milestone end and not scored: pixel baselines, the human visual look check, screen-reader passes.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Contract keys landed and the glossary pass is done. Not every string was spot-checked in a rendered German UI. |
| 2. Visuals | 3/4 | Drawn controls, switch, chips, `FileBrowser` and `SettingsPanel` all exist. Three raw native inputs remain by design. Nothing was rendered, so visual quality is unproven. |
| 3. Color | 3/4 | Button variants moved to a token-only sheet. Hundreds of legacy raw hex values and several gold `accent-color` leftovers remain. |
| 4. Typography | 3/4 | No new sizes or weights were found in the guarded sheets. Legacy `styles.css` was not re-audited. |
| 5. Spacing | 3/4 | The new token sheets are guarded. Legacy sheets are only ratcheted, not cleaned. |
| 6. Experience Design | 3/4 | Backdrop close, panel state, filter and head-row fixes are present. Weak points are unverified layout shift, raw inputs and dead legacy rules. |

**Overall: 18/24**

No pillar scores 1 or 2 and there is no BLOCKER. Every pillar stops at 3 because the evidence is code-only. The contract's headline claims (no layout shift, 4-column weekday grid at 320px, review dialog capacity) have never been seen rendered, and legacy debt outside the guarded sheets is large.

---

## Top 3 Priority Fixes

1. **Legacy raw colors and gold `accent-color` remain outside the guard** (WARNING). `styles.css` holds 145 raw color literals, `ProfileMenu.css` 24 and `commandCenter.css` 18. `features/hosting/hosting.css` holds 52 hex values plus `accent-color: #c8ab5d` (lines 225 and 1674) and `accent-color: var(--gold)` (lines 793, 935 and 1050). `styles.css:3472` also sets `.acknowledgements input { accent-color: var(--gold) }`. This contradicts the spec rule "No `accent-color` anywhere in shared CSS" and "gold is not used for a checked mark". Fix: delete the dead rules. `next-hosting-configuration-toggle` and `next-hosting-access-check` have 0 references in `.tsx`. Move the live `next-hosting-access-picker` radio and the two `progress` bars onto token values or the shared control. Then migrate the remaining legacy sheets into the guarded set.
2. **Prove the contract's rendered claims** (WARNING). Run the layout-shift bounding-box test at 390 and 1280px, the 320px weekday grid (4 plus 3 chips) and the 40-row review dialog. All three are rendered-geometry promises, and the pixel baselines are deferred. Until they run, Experience Design and Visuals cannot score above 3. Run them through the serialized Playwright helper before the milestone closes.
3. **Three raw native inputs stay on the ratchet** (WARNING). `EventObjectiveIconPicker.tsx:69` (radio), `EventSlotPlanner.tsx:315` and `:326` (checkbox) and `EventRoleSymbolPicker.tsx:113` (radio) are still the browser's drawing. Their appearance depends on the legacy fallback rule. This leaves the owner's "simplistic checkbox" complaint unresolved on the admin event authoring screens. Fix: give these pickers the drawn mark (`check-mark` or `choice-card` styling) or document the exemption in the spec's exceptions.

Further issues (minor):
- `hosting.css` still carries `next-hosting-*` selectors that no longer match `.tsx`, so the dead-rule count is probably higher than the three found.
- `Disclosure.tsx` imports `components/server/form-fields.css`. A shared primitive depends on a server-folder sheet, which inverts the intended layering.
- The Files `RadioGroupField` (`ServerFilesPanel.tsx:640`) is the only call-site change I confirmed. I did not confirm the other mapping-table rows (`AfterCreatingChoice`, `ConfigurationProfileAccessSection`) against `layout="cards"` or `"segmented"`.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- `Server_Files_ImmediateNote` and `Server_Files_FolderHeading` are gone from both `en.json` and `de.json` (grep count 0). That matches the "deleted when unreferenced" rule.
- German glossary: `Modpaket` and `Gelände` have 0 hits in `de.json`. The only "Modpack" hit is the key name `"Modpack": "Mod-Paket"` at line 1081, so it is a key, not a value. IN-06 is satisfied.
- Not verified: the `_one` and `_other` plural pairs for the new Settings summary and filter keys, and the `Pages_Events_Detail_Status_ConfirmedTeam` change. These are test-covered per the plan but I did not read the tests.
- No generic "Submit", "OK" or "Click here" patterns were found in the surfaces touched.

### Pillar 2: Visuals (3/4)
- Present as specified: `SwitchField` in `ConfigurationEntryField`, `AutomationPanel` (2 uses) and `CostAlertSettingsPanel` (3 uses). Also present: `FileBrowser`, `SettingsPanel`, `ConfigurationFilter`, `files.css`, `action-control.css`, and the `.toggle-chip` weekday chip with `--utf-day-chip-min`.
- `@supports (appearance: base-select)` is implemented in `form-fields.css:101`.
- `grep "<details"` returns zero. Disclosure is a button plus a region, so the owner's dislike is met structurally.
- No native `<select>` is used outside `FormFields`, so the "ugly select" complaint is answered through the shared control.
- Gap: the three raw inputs listed above.
- Gap: nothing was rendered, so the focal point and hierarchy of the Settings and Files screens are inferred from structure only.

### Pillar 3: Color (3/4)
- `action-control.css` defines the variants with tokens (the gold rule uses `--utf-gold-border` and the gold gradient tokens). The only legacy `button--*` rule left is the scoped `.login-dialog .button--ghost` at `styles.css:3722`. The gold hover on ghost is removed (hover comment at `action-control.css:53`: "never gold").
- Guarded sheets (`components/server/*.css`, `shared-primitives.css`, `action-control.css`) are scanned by `server-style-guard.test.ts`, and `files.css` is inside that scan.
- Debt: 145 raw color literals in `styles.css`, 52 hex values in `hosting.css`, 24 in `ProfileMenu.css` and 18 in `commandCenter.css`. The legacy ratchet holds them but the gold `accent-color` instances break the accent-reserved list (see Fix 1).

### Pillar 4: Typography (3/4)
- The guarded sheets declare no sizes outside the four-size scale (grep for literal `font-size` in `files.css` returned nothing).
- Not re-audited: legacy `styles.css` and `hosting.css`, which still hold their own sizes under the ratchet.

### Pillar 5: Spacing (3/4)
- The new tokens are used (`--utf-day-chip-min`, `--utf-review-dialog-width`, `--utf-dialog-gutter`). `dialog--review` is reduced to a single width rule (`change.css:105`), which removes the shell duplication that caused the misaligned review dialog.
- Legacy sheets are only ratcheted, so arbitrary rem and px values remain in them.

### Pillar 6: Experience Design (3/4)
Weighted against the owner's stated dislikes:
- `<details>`: none. Pass.
- Ugly native select: shared designed select with a `base-select` popup enhancement. Pass, with progressive fallback in browsers that lack `appearance: base-select`.
- Misaligned review dialog: the shell rules are removed and `dialog--review` sets only width. Pass by construction, not yet seen rendered.
- Layout shift on staging: the head row with the reserved state slot is specified (D-11). The bounding-box test is not confirmed. Unverified.
- Config lists without filter or search: `ConfigurationFilter` and `configurationFilterRules.ts` exist. Pass.
- Backdrop click: `closeOnBackdrop` is implemented (`primitives.tsx:598`, guarded by `!busy && startedOnBackdrop`, so a drag that starts inside the dialog does not close it). It is used on two dialogs in `SettingsSection.tsx` (lines 358 and 377), which matches D-13. Pass.
- Credentials: `ServerSecretsPanel.tsx:167` focuses the one-time value and calls `scrollIntoView` with `block: "nearest"`, which satisfies D-15.
- Weak spots: dead legacy rules (Fix 1), raw native inputs (Fix 3), and `FormFields.tsx` has no reduced-motion handling of its own. Reduced motion is handled in `form-fields.css` only.

---

## Files Audited
- `.planning/phases/70-ui-polishing/70-UI-SPEC.md` (lines 1-405 read; the remainder was not read)
- `sources/ReactApp/src/components/`: `FormFields.tsx`, `Disclosure.tsx`, `primitives.tsx`, `action-control.css`, `server/form-fields.css`, `server/files.css`, `server/change.css`, `server/ConfigurationFilter.tsx`, `server/FileBrowser.tsx`, `server/SettingsPanel.tsx`
- `sources/ReactApp/src/features/hosting/`: `hosting.css`, `ServerSecretsPanel.tsx`, `ServerFilesPanel.tsx`, `AutomationPanel.tsx`, `CostAlertSettingsPanel.tsx`, `sections/SettingsSection.tsx`
- `sources/ReactApp/src/styles.css`, `locales/en.json`, `locales/de.json`
- `sources/ReactApp/tests/styles/` (file names only)

Registry audit: skipped (shadcn not initialized).
