# Phase 70: UI Polishing - Pattern Map

**Mapped:** 2026-11-26
**Files analyzed:** 41 new or modified files (all under `sources/ReactApp`)
**Analogs found:** 38 / 41 (3 have no analog or only a partial one, listed at the end)

All analog paths below are git-tracked source (`git ls-files` verified for the primitives, panels, guards and tests quoted). Paths are relative to `A:/dev/git/UnitedTacticalForces/sources/ReactApp/`. Line numbers refer to the tree as of 2026-11-26 (HEAD `0341d18d`).

Planner note: RESEARCH.md conflicts C-01 to C-13 override the UI-SPEC where they disagree. The excerpts below follow the RESEARCH resolutions (input as non-hidden sibling of an `aria-hidden` box, ring on the wrapper, hover through a token layer, icon mock additions).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/styles/tokens.css` (additive tokens, `--utf-hover`, rename day-min) | config | n/a | itself (existing `--utf-*` blocks) | exact |
| `src/components/action-control.css` (NEW) | config (stylesheet) | n/a | `src/components/server/form-fields.css` | role-match |
| `src/components/ActionControl.tsx` (import css) | component | request-response | itself; `FormFields.tsx` (`import "@/components/server/form-fields.css"`) | exact |
| `src/components/FormFields.tsx` (CheckboxField variant, RadioGroupField layout, SwitchField, `labelEnd`) | component | request-response (controlled input) | itself (`CheckboxField`, `RadioGroupField`, `useFieldWiring`) | exact |
| `src/components/server/form-fields.css` (marks, cards, segmented, chip, switch, select, disclosure variants) | config (stylesheet) | n/a | itself | exact |
| `src/components/Disclosure.tsx` (`headingLevel`, `id`, `badge`, `variant`) | component | request-response | itself | exact |
| `src/components/primitives.tsx` (`ConfirmDialog`: `closeOnBackdrop`, `confirmPlacement`, `dismissVariant`) | component | event-driven | itself (`ConfirmDialogForm`, `ConfirmDialog`) | exact |
| `src/components/server/SettingsPanel.tsx` (NEW) | component | request-response | `src/components/Disclosure.tsx` plus the existing `section.workspace-panel` markup in `SettingsSection.tsx` | role-match |
| `src/components/server/useSettingsPanelState.ts` (NEW) | hook/provider | event-driven (sessionStorage) | `src/features/hosting/useServerChangeDraft` provider pattern (context + hook) and RESEARCH example | partial |
| `src/components/server/ConfigurationFilter.tsx` (NEW) | component | transform | `src/components/FilterChipGroup.tsx` + `src/components/SearchField.tsx` | role-match |
| `src/components/server/configurationFilter.ts` (NEW, pure `filterEntries`, `isRequiredEmpty`) | utility | transform | `isRequiredEmpty` in `src/features/hosting/ServerConfigurationPanel.tsx:74`; pure model files such as `src/features/hosting/automationModel.ts` | role-match |
| `src/components/server/ConfigurationEntryField.tsx` (head row, `SwitchField` for boolean) | component | request-response | itself | exact |
| `src/features/hosting/ServerConfigurationPanel.tsx` (SettingsPanel wrap, filter, shared `isRequiredEmpty`) | component | CRUD (staged draft) | itself | exact |
| `src/features/hosting/sections/SettingsSection.tsx` (heading row, Expand all, panel list, dialogs, invalid focus) | component | event-driven | itself | exact |
| `src/components/server/ModPackPanel.tsx`, `src/features/hosting/ServerSecretsPanel.tsx`, `src/features/hosting/AutomationPanel.tsx` (lose own wrapper and `h2`, compose `SettingsPanel`) | component | CRUD | themselves | exact |
| `src/components/server/ScheduleRuleFields.tsx` (chips, radio cards) | component | request-response | itself | exact |
| `src/components/server/automation.css` (day grid 4/7 columns, remove touch rule) | config | n/a | itself (lines 205-274) | exact |
| `src/features/hosting/CostAlertSettingsPanel.tsx` (checkbox to switch) | component | request-response | `AutomationPanel` enable controls (same change) | role-match |
| `src/components/server/ReviewChangesDialog.tsx`, `ChangeDiffItem.tsx`, `change.css` (compact rows, shell dedupe, leave variant) | component | request-response | `ChangeDiffItem.tsx` / `change.css` themselves; shell in `src/components/shared-primitives.css` | exact |
| `src/features/hosting/ServerFilesPanel.tsx` (split out card and dropzone, segmented context switch) | component | CRUD / file-I/O | itself (data logic stays) | exact |
| `src/components/server/FileBrowser.tsx`, `FileUploadDropzone.tsx` (NEW) | component | file-I/O | extracted from `ServerFilesPanel.tsx`; `ConfirmDialog` upload usage | role-match |
| `src/components/server/files.css` (NEW), `src/features/hosting/hosting.css` (rules removed) | config | n/a | `src/components/server/credentials.css` (token-only sheet) | role-match |
| `src/components/server/credentials.css`, credential row in `ServerSecretsPanel.tsx` (grid, focus one-time block) | component | CRUD | itself; focus-after-action idiom in `SettingsSection.tsx` (`headingRef`/`headingFocus`) | exact |
| `src/components/useDrawerFocus.ts` (WR-01) | hook | event-driven | itself | exact |
| `src/locales/en.json`, `src/locales/de.json` (new keys, ConfirmedTeam, German glossary pass) | config | n/a | existing flat key objects | exact |
| `src/features/admin/siteImages.ts` + `GalleryAssetPicker.tsx` (IN-08) | service | request-response | itself | exact |
| `src/styles.css`, `src/components/commandCenter.css` (remove `button*` rules) | config | n/a | itself | exact |
| ~43 `variant="primary"` call sites (reclassify) | component | n/a | `MemberDetailPage.tsx:843`, `RoleDetailPage.tsx:587` toggle idiom | partial |
| `tests/helpers/cssGuard.ts`-driven guards: `tests/styles/server-style-guard.test.ts`, `legacy-ratchet.test.ts`, `tokens-contrast.test.ts` (extend) | test | transform | themselves | exact |
| `tests/form-fields.test.tsx`, `tests/disclosure.test.tsx`, `tests/primitives.test.tsx`, `tests/shell-layouts.test.tsx` (extend) | test | n/a | themselves | exact |
| `tests/settings-panel.test.tsx`, `tests/hosting/settings-section.test.tsx` (NEW) | test | n/a | `tests/disclosure.test.tsx` (harness) and `tests/hosting/settings-leave-guard.test.tsx` (section render) | role-match |
| `tests/hosting/configuration-filter.test.ts` (NEW) | test | transform | `tests/hosting/automation-model.test.ts` (pure model test) | role-match |
| `tests/locales/de-glossary.test.ts` + `de-glossary-allowed.json` (NEW) | test | transform | existing `tests/locales/*` | role-match |
| `tests/mocks/react-icons.tsx` (add `Checkmark16Filled`) | test support | n/a | itself (`Dismiss16Regular` entry) | exact |
| `tests/setup.ts` (`sessionStorage.clear()` afterEach) | test support | n/a | itself (`afterEach(() => FakeWebSocket.reset())`) | exact |
| e2e specs and registry (`tests/e2e/hosting-*.spec.ts`, `tests/e2e/support/screens/workspaceScreens.ts`, `serverScreens.ts`) | test | request-response | themselves | exact |

## Pattern Assignments

### `src/components/FormFields.tsx` - `CheckboxField` (component, controlled input)

**Analog:** itself, lines 323-359. Keep the `Omit<..., "className" | "children" | "type">` prop typing, `useFieldWiring`, `FieldHint`, `FieldError`. Keep `form-field__check` (referenced by `hosting-automation.spec.ts:657`).

**Existing core** (lines 334-358):
```tsx
export function CheckboxField({ label, hint, error, id, "aria-describedby": describedByProp, ...inputProps }: CheckboxFieldProps) {
  const wiring = useFieldWiring(id, describedByProp, hint != null, error != null);
  return (
    <div className="form-field form-field--check">
      <label className="form-field__check" htmlFor={wiring.controlId}>
        <input {...inputProps} id={wiring.controlId} type="checkbox"
          aria-invalid={error != null ? true : undefined}
          aria-describedby={wiring.describedBy} />
        <span>{label}</span>
      </label>
      {hint != null && <FieldHint id={wiring.hintId}>{hint}</FieldHint>}
      {error != null && <FieldError id={wiring.errorId} message={error} />}
    </div>
  );
}
```

**Target shape (RESEARCH Pattern 3, resolves C-01/C-07/C-12):** the input stays the same element with all props; wrap it in `span.check-mark` (non-hidden), add an `aria-hidden` sibling box with the glyph, never put `aria-hidden` on an ancestor of the input. The overlay input must cover the whole row (C-07); ring is `.check-mark:has(input:focus-visible)` (C-12).
```tsx
<label className="form-field__check" htmlFor={wiring.controlId}>
  <span className="check-mark">
    <input {...inputProps} id={wiring.controlId} type="checkbox" className="check-mark__input" ... />
    <span className="check-mark__box" aria-hidden="true"><Checkmark16Filled /></span>
  </span>
  <span className="form-field__check-text">{label}</span>
</label>
```
Add `variant?: "box" | "chip"` to `CheckboxFieldProps` (additive). Chip renders `label.toggle-chip` with the same overlaid input and no glyph.

**New import:** `Checkmark16Filled` from `@fluentui/react-icons`; add it to `tests/mocks/react-icons.tsx` in the same plan (C-11).

### `src/components/FormFields.tsx` - `RadioGroupField` (component)

**Analog:** itself, lines 401-453. Keep `fieldset.form-field.form-field--radio`, `legend`, `baseId`/`reasonId`/`helpId` wiring, `form-field__radio-row` class (+ `--disabled`), and the `aria-describedby` id list builder.

**Existing core** (lines 421-444):
```tsx
<label key={option.value} className={isDisabled ? "form-field__radio-row form-field__radio-row--disabled" : "form-field__radio-row"}>
  <input type="radio" name={name} value={option.value} checked={option.value === value}
    disabled={isDisabled} aria-describedby={ids.length > 0 ? ids.join(" ") : undefined}
    onChange={() => onChange(option.value)} />
  <span className="form-field__radio-text">
    <span>{option.label}</span>
    {option.help != null && <FieldHint id={helpId}>{option.help}</FieldHint>}
  </span>
</label>
```
Add `layout?: "cards" | "segmented"` (default `cards`) as a `data-layout` attribute on the fieldset; cards add class `choice-card` to the label and the `check-mark check-mark--radio` mark (same input-plus-sibling-box structure as the checkbox); segmented omits the mark and help.

### `src/components/FormFields.tsx` - `SwitchField` (NEW component)

**Analog:** `CheckboxField` (chrome props: `Omit<FieldChromeProps, "required">`, `useFieldWiring`, hint/error wiring) in the same file.

Copy the `CheckboxFieldProps` type, extend with required `onLabel: string` and `offLabel: string` (type error rather than an English fallback), render `input type="checkbox" role="switch"` with `className="check-mark__input"` and a `span.switch-track[aria-hidden]` sibling (thumb inside), state word `aria-hidden`. Track radius: `calc(var(--utf-switch-track-height) / 2)` (C-06). Do not render a literal `aria-checked` unless a Wave 0 test demands it (A1); use `toBeChecked()` in tests.

### `src/components/FormFields.tsx` - `labelEnd` on `TextField`, `TextAreaField`, `SelectField`, `SwitchField`

**Analog:** the `trailing` prop pattern in `TextField` (lines 132-190): an additive `ReactNode` prop that only changes markup when present.
```tsx
{trailing != null ? (
  <div className="form-field__row">{control}<div className="form-field__trailing">{trailing}</div></div>
) : control}
```
Apply the same "unchanged markup when absent" rule to `FieldLabel`: wrap `FieldLabel` and `labelEnd` in `div.form-field__head` only when `labelEnd != null`.

### `src/components/server/form-fields.css` (stylesheet, guarded)

**Analog:** itself. Header comment (lines 1-9) states the guard contract: tokens only; allowed literals are a 1px border and the 520px/800px breakpoints. Existing patterns to extend, not replace:
- Row targets (lines 139-148) `.form-field__check, .form-field__radio-row { display:flex; min-height: var(--utf-control-height); ... }` and the touch override in the `@media (max-width: 800px)` block (lines 244-256), where new rows (switch, chip, segment, panel header) must be added.
- Ring pattern (lines 157-161): `outline: var(--utf-focus-ring); outline-offset: var(--utf-focus-offset);` using `:has(input:focus-visible)`. Reuse on `.check-mark`, `.choice-card`, `.toggle-chip`, segment labels.
- Remove `accent-color: var(--utf-text);` (line 154); the new style guard test fails on `accent-color`.
- Keep the base rule `.form-field__control--select { appearance: none; ... }` untouched (lines 84-89), because `tests/form-fields.test.tsx:278-296` matches `/\.form-field__control--select\s*\{[^}]*appearance:\s*none;/`. Add `base-select` only in a separate `@supports (appearance: base-select)` rule with a comma selector list.
- Hover: use `--utf-hover` layer, never `color-mix(` (guard regex `colourFunction` in `tests/helpers/cssGuard.ts:10`):
```css
.button--ghost:hover:not([aria-disabled="true"]) { background-image: linear-gradient(var(--utf-hover), var(--utf-hover)); }
```
- Reduced-motion idiom (lines 234-242): `@media (prefers-reduced-motion: no-preference) { ... transition: ... var(--utf-motion-base) }`. Put the glyph and thumb transitions there.
- Forced colors block: `@media (forced-colors: active)` with system colors; no existing analog (see No Analog Found).

### `src/components/Disclosure.tsx` (component)

**Analog:** itself, lines 34-81. Additive props: `headingLevel?: 2 | 3`, `id?: string`, `badge?: ReactNode`, `variant?: "inline" | "panel"`. Keep the id wiring and the always-mounted content (`hidden={!open}`, never `{open && children}`, RESEARCH Pattern 1 "keep mounted").
```tsx
<button type="button" className="disclosure__summary" aria-expanded={open} aria-controls={contentId}
  aria-labelledby={titleId} aria-describedby={summary != null ? summaryLineId : undefined}
  onClick={() => onOpenChange(!open)}>
  <span className="disclosure__title" id={titleId}>{title}</span>
  <ChevronDown20Regular className="disclosure__chevron" aria-hidden="true" />
  {summary != null && <span className="disclosure__summary-line" id={summaryLineId}>{summary}</span>}
</button>
<div className="disclosure__content" id={contentId} role="region" aria-labelledby={titleId} hidden={!open}>
```
When `headingLevel` is set, render the button inside `const Heading = \`h${headingLevel}\` as "h2" | "h3"` (`margin: 0`, font inherited). A caller `id` goes on the outer `div.disclosure`. Place the `badge` between title and chevron (grid column; `.disclosure__summary` is a two-column grid today, lines 185-201 of the CSS, so add a column).

Page-local `Disclosure` override blocks to review (Pitfall 4): `eventAuthoring.css:31-75`, `hosting.css:526-544` and `2006-2037`, `server.css:246-247`, `activity.css:151`.

### `src/components/ActionControl.tsx` + `src/components/action-control.css` (NEW)

**Analog for TSX:** itself. Variant to class mapping is already in `variantClassNames` (lines 59-66) and `size === "compact" ? "button--small"` (line 84). Only add `import "@/components/action-control.css";` at the top (same style as `FormFields.tsx:3`). Keep the class names `button--gold|primary|ghost|glass|danger|small|icon-only|full-width`; the css must define them with tokens.

**Analog for CSS:** `src/components/server/form-fields.css` (token-only header comment, touch override at `max-width: 800px`). Danger hover must not lighten the fill (C-04: the 20% tint fails 4.5:1 on raised and overlay). Register the file in the two guards (Pitfall 6): `tests/styles/server-style-guard.test.ts` file list and `isGuarded` in `tests/styles/legacy-ratchet.test.ts:21-27`:
```ts
function isGuarded(path: string): boolean {
  return (
    path.startsWith("components/server/") ||
    path === "components/shared-primitives.css" ||
    path === "styles/tokens.css"
  );
}
```
Add `|| path === "components/action-control.css"`. After deleting `button*` rules from `styles.css` and `commandCenter.css` (note `commandCenter.css:1-6` `.command-shell .button--primary` is the gold gradient, C-02), run `UTF_LEGACY_RATCHET_UPDATE=1 npm --prefix sources/ReactApp test -- tests/styles/legacy-ratchet.test.ts`.

### `src/components/primitives.tsx` - `ConfirmDialog` additions (component, event-driven)

**Analog:** itself. Add three additive props to `ConfirmDialogProps` (lines 338-381): `closeOnBackdrop?: boolean`, `confirmPlacement?: "end" | "start"`, `dismissVariant?: ActionControlVariant` (C-05). Thread them through `ConfirmDialog`'s `...formProps` spread into `ConfirmDialogForm` (its `Omit<ConfirmDialogProps, "open" | "initialFocus" | "className">` type must also omit `closeOnBackdrop`, which belongs to the dialog element).

**Dismiss button to parameterize** (lines 463-472):
```tsx
<div className="dialog__actions">
  <ActionButton variant="ghost" type="button" onClick={onCancel} disabled={busy} data-dialog-dismiss="">
    {cancelLabel}
  </ActionButton>
  <ActionButton variant={confirmVariant} type="submit" ...>{...}</ActionButton>
  {extraAction && (<ActionButton variant={extraAction.variant} type="button" onClick={extraAction.onClick} disabled={busy}>{extraAction.label}</ActionButton>)}
</div>
```
`confirmPlacement="start"` renders confirm first in DOM order, then dismiss, then `extraAction` (Discard, Stay, Review).

**Backdrop handler goes on the `<dialog>` element** (lines 547-561). Existing cancel/close handlers to copy the style from:
```tsx
onCancel={(event) => { event.preventDefault(); if (!busy) onCancel(); }}
onClose={() => { const dialog = dialogRef.current; if (openRef.current && dialog && !dialog.open) dialog.showModal(); }}
```
Add `onPointerDown`/`onClick` where `event.target === dialogRef.current`, coordinates outside `getBoundingClientRect()`, the preceding pointerdown also on the backdrop (drag-from-inside rule), and `!busy` (RESEARCH Pattern 5). A ref for "pointerdown started on backdrop" follows the existing `openRef` idiom (line 524).

**Test analog:** `tests/primitives.test.tsx` describe `"ConfirmDialog additive capabilities"` (line 160), helper `renderDialog`, e.g. lines 335-354 "renders the extra action after the confirm button" for the DOM order assertion:
```tsx
const buttons = within(dialog).getAllByRole("button");
expect(buttons.map((button) => button.textContent)).toEqual(["Keep off", "Start", "Review changes"]);
expect(buttons[2]).toHaveClass("button--gold");
```

### `src/components/server/SettingsPanel.tsx` (NEW, component) and `useSettingsPanelState.ts` (NEW, hook/provider)

**Analog for panel:** the wrapper currently inlined in `SettingsSection.tsx` lines 220-231 plus `Disclosure.tsx`:
```tsx
<section className="workspace-panel">
  <div className="workspace-panel__head">
    <h2>{t("Server_Settings_CredentialsTitle")}</h2>
    {draft.secrets.size > 0 && (<StatusBadge tone="info">{t("Server_Change_PanelCount", { count: draft.secrets.size })}</StatusBadge>)}
  </div>
  <ServerSecretsPanel server={server} />
</section>
```
`SettingsPanel` = `section.workspace-panel` (id on the outer section, so `#server-automation` deep links and `OverviewSection.tsx:46`, `AutoStopNotice.tsx:48` keep working, Pitfall 2) holding `<Disclosure variant="panel" headingLevel={2} badge={...} summary={...}>`. Panels compose it themselves (RESEARCH Pattern 1) because `ModPackPanel` returns `null` for unsupported games and `ServerConfigurationPanel` early-returns loading/error `StatePanel`s.

**Analog for auto-open on a new trigger (RESEARCH Pattern 2):** `ServerConfigurationPanel.tsx:136` and `218-224`:
```tsx
const triggerSignature = triggers.join("\n");
if (triggerSignature !== seenTriggers) {
  const known = new Set(seenTriggers.split("\n"));
  setSeenTriggers(triggerSignature);
  if (triggers.some((trigger) => !known.has(trigger))) setAdvancedOpen(true);
}
```
Reuse "adjust state during render keyed on a signature" for "open when something is staged"; it never closes by itself.

**Analog for hash-driven open and focus:** `AutomationPanel.tsx:130-148`:
```tsx
const location = useLocation();
const targeted = location.hash === `#${panelId}`;
useEffect(() => {
  if (!targeted || !loaded) return;
  if (typeof sectionRef.current?.scrollIntoView === "function") sectionRef.current.scrollIntoView({ behavior: "auto" });
  headingRef.current?.focus();
}, [targeted, loaded]);
```
Change to: open panel, then focus the disclosure **button** on the next frame (C-08). Migrate `hosting-automation.spec.ts:247, 486, 627` and `workspaceScreens.ts:421` from `heading ... toBeFocused()` to the button.

**Analog for hook storage (no repo precedent; RESEARCH example):**
```ts
const storageKey = "utf.settings.open";
function readOpenIds(): string[] {
  try {
    const parsed: unknown = JSON.parse(sessionStorage.getItem(storageKey) ?? "[]");
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === "string") : [];
  } catch { return []; }
}
```
Context-provider shape: copy from `useServerChangeDraft` (`ServerChangeDraftProvider` + `useServerChangeDraft`, used at `SettingsSection.tsx:28-31, 76`).

### `src/features/hosting/sections/SettingsSection.tsx` (component, event-driven)

**Analog:** itself. Changes: wrap the content in the panel-state provider; heading row gains the Expand all / Collapse all `ActionButton variant="ghost" size="compact"`; remove the inline `section.workspace-panel` wrappers (lines 219-231 credentials, 237-269 record) in favor of `SettingsPanel`; the record block keeps the `ConfirmDialog` (lines 254-267) and its `deletion` mutation unchanged.

**Invalid-field focus must open the panel first** (current lines 147-150, Pitfall 3):
```tsx
useEffect(() => {
  if (reviewAttempt > 0)
    sectionRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
}, [reviewAttempt]);
```
Change to: find the element, `closest("[data-settings-panel]")`, `open(id)`, then `requestAnimationFrame(() => el.focus())`.

**Leave dialog (lines 297-324) becomes the documented variant:**
```tsx
<ConfirmDialog open={guard.blocked} className="dialog--leave" ... cancelLabel={t("Server_Change_Leave_Stay")}
  confirmLabel={t("Server_Change_Leave_Discard")} confirmVariant="danger" initialFocus="dismiss"
  onCancel={guard.stay} ... extraAction={count > 0 ? { label: t("Server_Change_Review"), variant: "gold", onClick: ... } : undefined} />
```
Add `confirmPlacement="start"`, `closeOnBackdrop`, `dismissVariant="primary"`. The discard dialog (lines 282-296) gets `closeOnBackdrop` only.

### `src/features/hosting/ServerConfigurationPanel.tsx` + `ConfigurationFilter.tsx` + `configurationFilter.ts`

**Analog for the pure module:** `isRequiredEmpty` at `ServerConfigurationPanel.tsx:74` plus `splitByTier` / `groupByTarget` (lines 29-62, pure functions with TSDoc). Move `isRequiredEmpty` into `configurationFilter.ts` so the "Needs a value" chip and the Advanced auto-open (line 208) share it. `filterEntries(entries, query, mode, draft)`: case-insensitive, trimmed, diacritics ignored, matches display name, group name and description; never matches `sensitive` or `isRedacted` values.

**Analog for the component:** `FilterChipGroup` props (`label`, `items: {id,label,count}[]`, `value`, `onChange`, optional count formatter) and `SearchField` (`src/components/SearchField.tsx`, label visually hidden, clear is a named icon button). Compose both inside `ConfigurationFilter`; polite result count uses the same `sr-only`/`aria-live="polite"` idiom as `SettingsSection.tsx:210-212`.

**Existing structure to edit:** root is `<section className="workspace-panel configuration-panel">` with `<header className="workspace-panel__head">` (lines 332-333) and a `Disclosure` for Advanced (line 393); loading/error `StatePanel`s at lines 232-262. Keep the early-return `StatePanel`s but inside `SettingsPanel` so the header remains.

**Test analog for the pure filter test:** `tests/hosting/automation-model.test.ts` (pure model, no render). For toolbar-from-eight-entries and staged-survives-filter use `tests/hosting/configuration-panel.test.tsx` and `tests/helpers/configurationFixtures.ts` (`configurationEntry`).

### `src/components/server/ConfigurationEntryField.tsx` (component)

**Analog:** itself. Current state controls (lines 230-259) live in a conditional `configuration-field__actions` row that adds a line on staging (the D-11 defect):
```tsx
{!protectedEntry && (changed || (onReset && entry.source === "Server")) && (
  <div className="configuration-field__actions">
    {changed && <StatusBadge tone="info">{t("Server_Change_Field_Changed")}</StatusBadge>}
    {resetStaged && (<span className="configuration-field__staged">{t("Server_Settings_ResetStaged")}</span>)}
    {changed && (<ActionButton variant="ghost" size="compact" disabled={disabled} aria-label={t("Server_Change_Field_Undo_Name", { name: entry.displayName })} onClick={onUndo}>{t("Server_Change_Field_Undo")}</ActionButton>)}
    {!resetStaged && onReset && entry.source === "Server" && (<ActionButton ... aria-label={t("Server_Settings_Reset_Name", { name: entry.displayName })} onClick={onReset}>{t("React_ServerWorkspace_ResetInherited")}</ActionButton>)}
  </div>
)}
```
Move this content into a `stateSlot` node passed as `labelEnd` to the field components via the shared `common` object (lines 117-125), and build it once as an internal head-row component. The Contribution branch (lines 143-172) builds its own `label.form-field__label` markup and bypasses `TextField`/`SelectField`; apply the same head row there (RESEARCH open question 2). Boolean branch (lines 173-184) becomes `SwitchField` staging the strings `"true"` / `"false"`:
```tsx
<SelectField {...common} value={value || "false"} options={[{ value: "true", label: t("React_ServerWorkspace_Enabled") }, { value: "false", label: t("React_ServerWorkspace_Disabled") }]} onChange={(event) => onChange(event.target.value)} />
```
Height rule: the head row's height depends on `canModify`-style capability (`!protectedEntry && (onReset && entry.source === "Server")` or stageable), never on `changed`.

### `src/components/server/ChangeDiffItem.tsx`, `ReviewChangesDialog.tsx`, `change.css` (D-12)

**Analog:** `ChangeDiffItem.tsx` lines 72-134. Keep: `renderValue` switch (lines 35-61), `failureReasonKeys`, `dl.change-item__pair` with `dt` Before/After (make `dt` `sr-only`; the existing `sr-only` utility class is used at `SettingsSection.tsx:210`), `data-remove-item={item.id}` (focus-after-removal depends on it), the `aria-label={t("Server_Change_Item_Remove_Name", { name })}`.

Change the Remove button from text to icon-only:
```tsx
<ActionButton variant="ghost" size="compact" className="change-item__remove"
  aria-label={t("Server_Change_Item_Remove_Name", { name })} data-remove-item={item.id} onClick={onRemove}>
  {t("Server_Change_Item_Remove")}   // becomes <Dismiss16Regular aria-hidden="true" /> with iconOnly
</ActionButton>
```
`ActionButton` type requires `iconOnly: true` plus `aria-label` together (`ActionControl.tsx:21-31`). `Server_Change_Item_Remove` becomes unreferenced: delete in both locale files in the same plan (C-10). Mod diff: wrap the `children` slot content in `<Disclosure title={t("Server_Change_ModDiff_Toggle", { count })} ...>` closed by default (the slot is filled by `renderItemExtra` at `SettingsSection.tsx:336-338`).

**Shell dedupe:** `change.css:102-157+` re-declares `.dialog.dialog--review` `[open]`, `form`, `.kicker`, `h2`, `h2:focus-visible`, `p`, `.dialog__body`, `.dialog__body:focus-visible`, `.dialog__error`, `.dialog__actions`. Delete them; keep only the width declaration `width: min(var(--utf-review-dialog-width), calc(100vw - var(--utf-dialog-gutter)))`. The shell lives in `src/components/shared-primitives.css`. Replace the `dialog--leave` wrapping rules with the one-row rule (`margin-inline-end: auto` on the first action above 520px).

### `src/features/hosting/ServerFilesPanel.tsx`, `FileBrowser.tsx`, `FileUploadDropzone.tsx`, `files.css` (D-14)

**Analog:** `ServerFilesPanel.tsx` itself keeps all data logic. Existing context switch to replace (lines 593-614):
```tsx
<div className="next-hosting-file-contexts" role="tablist" aria-label={t("Hosting_Detail_FileContexts")}>
  <ActionButton variant="ghost" role="tab" aria-selected={context === "persistent"} onClick={() => navigateTo("", "persistent")}>{t("Hosting_Detail_PersistentFiles")}</ActionButton>
  <ActionButton variant="ghost" role="tab" aria-selected={context === "live"} onClick={() => navigateTo("", "live")}>{t("Hosting_Detail_LiveFiles")}</ActionButton>
</div>
```
becomes `<RadioGroupField layout="segmented" legend={t("Hosting_Detail_FileContexts")} name="file-context" value={context} onChange={(next) => navigateTo("", next)} options={[...]}/>`; the legend is visually hidden. Update tests that use `getByRole("tab", { name: "Live worker files" })` (`files-panel-location.test.tsx:146-149`, `workspace-page.test.tsx:424`) and `adjacentScreens.ts:161-165`.

**Defect to remove** (`hosting.css:1750-1758`): `.next-hosting-file-content { display: grid; grid-template-rows: auto minmax(0, 1fr) auto; ... }` with more than three children. Replace with a flex column in `files.css`. Heading at `ServerFilesPanel.tsx:720-723` (`t("Server_Files_FolderHeading", { folder: currentFolder })`, empty at root) becomes folder name or the context label; the paragraph at line 728 (`Server_Files_ImmediateNote`) moves into the footer as `Server_Files_Footer_Immediate`. Keep existing `next-hosting-*` hook classes on the DOM and add `file-browser*` classes (Pitfall 8); only the CSS moves. `hosting.css:1760-1763` gold dashed drag outline `#c4a95d` becomes a 1px dashed `--utf-border-control`.

**Token-only stylesheet analog:** `src/components/server/credentials.css` (183 lines, token-only, guarded automatically because it is under `components/server/`). Toolbar: `role="toolbar"` + `aria-label={t("Server_Files_Toolbar_Label")}`. Upload and New folder use `ActionButton`/`FileChooserButton`, never raw `button` or `input` (ratchet, Pitfall 6).

### Credentials row (`ServerSecretsPanel.tsx`, `credentials.css`) (D-15)

**Analog:** itself. Row markup lines 176-230 (`article.credential` > `header.credential__head` > `p.credential__status` > `div.credential__value` > `div.credential__value-text` / `div.credential__value-actions` > `div.credential__edit`). Reserved-slot idiom to preserve (lines 183-190):
```tsx
{/* Always rendered, so staging never changes how the name wraps; hidden while nothing is staged. */}
<span className={`credential__badge-slot${staged ? "" : " credential__badge-slot--idle"}`} aria-hidden={staged ? undefined : true}>
```
Focus-after-own-action: add `tabIndex={-1}` and a ref to `div.credential__value`, focus and `scrollIntoView({ block: "nearest", behavior: "auto" })` in an effect keyed on a counter incremented only by the member's successful Rotate/Generate/Save. Counter-plus-effect idiom is already in `SettingsSection.tsx:124, 139-141`:
```tsx
const [headingFocus, setHeadingFocus] = useState(0);
useEffect(() => { if (headingFocus > 0) headingRef.current?.focus(); }, [headingFocus]);
```
Do not move focus on the background refetch path (`freshOneTime` memo at lines 382-410).

### `src/components/useDrawerFocus.ts` (WR-01, hook)

**Analog:** itself, lines 113-142. Insert the modal guard at the top of `cycleTab`, before `nowhere` (line 118):
```ts
const cycleTab = (event: KeyboardEvent) => {
  if (event.key !== "Tab" || event.defaultPrevented) return;
  // NEW: if (document.querySelector("dialog[open]")) return;
  const active = document.activeElement;
  const nowhere = !active || active === document.body || active === document.documentElement;
```
Use `dialog[open]`, not `dialog:modal`: jsdom 28 returns `null` for `dialog:modal` (Pitfall 7); the app opens dialogs only via `showModal()` in `ConfirmDialog`. Test in `tests/shell-layouts.test.tsx`, copying the blank-surface click idiom at lines 286-304 (`await user.click(nav); await user.tab();`); open a `ConfirmDialog` over the open menu, click blank dialog area, `user.tab()`, assert focus is inside the dialog and not the drawer; a second case without a dialog keeps the existing cycle.

### `src/features/admin/siteImages.ts` + `GalleryAssetPicker.tsx` (IN-08)

**Analog:** itself. `siteImages.ts:151-161`:
```ts
export async function loadMediaAsset(assetId: string, signal?: AbortSignal): Promise<MediaAsset | undefined> {
  ...
  return asset ? normalizeAsset(asset) : undefined;
}
```
Return `Promise<MediaAsset | null>` and `null`. Drop `?? null` at `GalleryAssetPicker.tsx:87` (`queryFn: async ({ signal }) => (await loadMediaAsset(selectedAssetId, signal)) ?? null`) and its comment at line 86. Add a 404-returns-`null` unit test in `tests/admin-site-images.test.ts` (no existing test calls `loadMediaAsset`).

### Automation (`AutomationPanel.tsx`, `ScheduleRuleFields.tsx`, `automation.css`) (D-05)

**Analog:** `automation.css:205-274`. Replace the auto-fill grid:
```css
.schedule-rule__day-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(var(--utf-automation-day-min), 1fr));
  gap: var(--utf-space-sm);
  min-width: 0;
}
```
with `repeat(7, minmax(0, 1fr))` above 520px and `repeat(4, minmax(0, 1fr))` inside the existing `@media (max-width: 520px)` block (line ~243). Remove the `.schedule-rule .form-field__check { min-height: var(--utf-control-height-touch) }` rule (the 800px block, lines 261-263) once the chip rule owns touch height. Rename `--utf-automation-day-min` to `--utf-day-chip-min` in `tokens.css:50` and `automation.css:220` (exactly two references). Enable controls: `CheckboxField` to `SwitchField` (`onLabel={t("React_ServerWorkspace_Enabled")}`, `offLabel={t("React_ServerWorkspace_Disabled")}`).

### Tests and guards to extend

| File | Pattern to copy |
|------|-----------------|
| `tests/disclosure.test.tsx` | `Harness` stateful wrapper (lines 1-25); assertions `getByRole("button", { name })`, `toHaveAttribute("aria-expanded", ...)`, `container.querySelector(".disclosure__content")`. Add heading wrapper, `id`, `badge`, `variant` cases. |
| `tests/styles/server-style-guard.test.ts` | `findStyleViolations(".a { ... }")` one-liner assertions; add `accent-color`, `appearance: auto` cases and extend the stylesheet list to `action-control.css`. |
| `tests/styles/tokens-contrast.test.ts` | `colorOf`, `compositeOver`, `contrastRatio` over `SURFACES`; add control-state and button pairs (Disabled row exempt, C-13; danger hover pair, C-04). |
| `tests/helpers/cssGuard.ts:10` | The guard regex defining what is forbidden (`color-mix(` included). |
| `tests/setup.ts` | Add `afterEach(() => sessionStorage.clear());` beside the existing `afterEach(() => FakeWebSocket.reset());`. |
| `tests/mocks/react-icons.tsx` | Add `Checkmark16Filled` next to the `Dismiss16Regular` entry (line ~95). |
| Panel-seeding helper (NEW) | One helper that sets `sessionStorage["utf.settings.open"]` for vitest, and `page.addInitScript` in `installWorkspace` for Playwright (default all open so existing flows are unchanged). Needed by 8 vitest files rendering `SettingsSection` (`arma-placement`, `change-flow`, `configuration-panel`, `credentials-panel`, `mod-pack-panel`, `review-changes-dialog`, `workspace-page`, `workspace-section-panels`) plus `automation-panel` via `tests/helpers/automationPanelHarness.tsx`. |
| `tests/hosting/settings-leave-guard.test.tsx` | Full-page render recipe for `tests/hosting/settings-section.test.tsx`: `vi.hoisted` auth claims mock, `renderWithProviders`, fixtures `fleetServer`, `serverAutomation`, `configurationEntry`. |

## Shared Patterns

### Token-only guarded CSS
**Source:** `src/components/server/form-fields.css` header (lines 1-9) and `tests/helpers/cssGuard.ts`.
**Apply to:** `action-control.css`, `files.css`, every edit to `form-fields.css`, `change.css`, `automation.css`, `credentials.css`.
No hex, no colour function (`rgb(`, `color-mix(` included), only `1px` on border properties and `520px`/`800px` breakpoints as literals, outline removals need a replacement ring. Put new raw values in `src/styles/tokens.css` (unguarded source of truth). `linear-gradient(var(--utf-hover), var(--utf-hover))` passes the guard.

### Focus ring
**Source:** `form-fields.css:157-161`.
**Apply to:** check mark, radio mark, switch, chip, card, segment, disclosure header.
```css
outline: var(--utf-focus-ring);
outline-offset: var(--utf-focus-offset);
```
Draw it on an ancestor of the input (wrapper, card, chip, segment label), because the audit's ring probe in `tests/e2e/support/a11y.ts:379-394` measures the focused element and up to three `:focus-within` ancestors (C-12).

### Touch target at narrow widths
**Source:** `form-fields.css:244-256`.
**Apply to:** every new control row (switch, chip, card, segment, panel header, review Remove, file row actions).
```css
@media (max-width: 800px) { .form-field__check, .form-field__radio-row, .disclosure__summary { min-height: var(--utf-control-height-touch); } }
```

### Additive-prop primitive extension
**Source:** `TextField` `trailing` prop (`FormFields.tsx:132-190`) and `ConfirmDialog` additive props (`primitives.tsx:347-380`).
**Apply to:** `variant`, `layout`, `labelEnd`, `headingLevel`, `id`, `badge`, `closeOnBackdrop`, `confirmPlacement`, `dismissVariant`.
Optional props, TSDoc on every prop, markup unchanged when the prop is absent, defaults keep all existing call sites compiling.

### Localized strings
**Source:** `useTranslation()` + `t("Key")` everywhere (`SettingsSection.tsx:84`).
**Apply to:** every new component. Tier A files (`src/components/server`, `src/features/hosting`) forbid literal `aria-label`, `title`, `label`, `legend`, `hint` strings (`tests/ratchets/ratchet.test.ts`, `tests/hard-coded-text.test.ts`). New keys go in both `en.json` and `de.json`; unreferenced keys (`Server_Files_ImmediateNote`, `Server_Files_FolderHeading`, `Server_Change_Item_Remove`) are deleted in the plan that removes the last use (`locales:unused`).

### Controlled state without unmounting
**Source:** `Disclosure.tsx:70-78` (`hidden={!open}`).
**Apply to:** `SettingsPanel` and every collapsible panel. `AutomationBody` keeps its draft in React state, so conditional rendering of children would silently drop unsaved automation edits.

### Focus after a member's own action
**Source:** `SettingsSection.tsx:124, 139-141` (counter state plus effect).
**Apply to:** credentials one-time block, invalid-field focus, deep-link header focus.

## No Analog Found

| File / Concern | Role | Data Flow | Reason |
|----------------|------|-----------|--------|
| `useSettingsPanelState.ts` sessionStorage persistence | hook | event-driven | No existing `sessionStorage`/`localStorage` hook in the React app was found in the files read; use the RESEARCH example (`readOpenIds`) and the `ServerChangeDraftProvider` context shape. |
| `@media (forced-colors: active)` rules in the marks, switch and chip CSS | config | n/a | No existing forced-colors block in the guarded sheets; follow the UI-SPEC state table (borders `CanvasText`, checked `Highlight`/`HighlightText`, disabled `GrayText`). System-colour keywords contain no hex or function, so the guard accepts them. |
| `@supports (appearance: base-select)` + `::picker(select)` popup styling | config | n/a | No precedent; lightningcss 1.33.0 passes it through (RESEARCH probe). Keep it in a separate comma-selector rule so the `appearance: none` regex test stays green. |
| Backdrop click handling on `<dialog>` | component | event-driven | No existing pointer handler on the dialog element; closest is the `onCancel`/`onClose` pair at `primitives.tsx:553-560`. Implement per RESEARCH Pattern 5. |

## Open decisions the planner must record (from RESEARCH)

1. C-02 / A4: classification of the ~43 `variant="primary"` sites (main CTA to `gold`, toggle-like sites keep neutral `primary`). Needs an owner veto point.
2. C-03: hover implemented as `--utf-hover` layer (recommended) or four named `color-mix` tokens in `tokens.css`.
3. Paginate vs virtualize: RESEARCH recommends neither (max 82 entries; ~6 Common entries open by default).
4. Registry cost: add Settings states as separate registry entries or mark heavy ones `keyboard: false` with a justification (`workspaceScreens.ts:555-570`, `allowLongWalk()`).

## Metadata

**Analog search scope:** `src/components`, `src/components/server`, `src/features/hosting`, `src/features/admin/siteImages.ts`, `src/styles`, `tests`, `tests/styles`, `tests/helpers`
**Files read or grepped:** about 30
**Pattern extraction date:** 2026-11-26
