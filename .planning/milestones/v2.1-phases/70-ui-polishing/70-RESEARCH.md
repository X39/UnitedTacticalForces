# Phase 70: UI Polishing - Research

**Researched:** 2026-11-26
**Domain:** React 19 + Vite 8 app (`sources/ReactApp`): shared form/disclosure/dialog/button primitives, CSS token system, Settings workspace section, Vitest + Playwright audit gates
**Confidence:** HIGH for code facts and gate mechanics (read in this session); MEDIUM for browser-support claims; recommendations marked `[ASSUMED]` where they are engineering judgement

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
None. `70-CONTEXT.md` is auto-generated (discuss skipped via `workflow.skip_discuss`). Its `## Decisions` section contains only: "All implementation choices are at Claude's discretion ... Use the ROADMAP phase goal, the input list below, and codebase conventions (shared React primitives, EN/DE locale JSON, existing Playwright/axe gates) to guide decisions. Fix shared primitives once rather than page-locally."

The APPROVED `70-UI-SPEC.md` (approved 2026-11-26, gsd-ui-checker) is the primary spec. Its decisions D-01..D-18 are treated as locked input by the orchestrator ("research HOW to implement it in the existing code, do not redesign it"). Where this research finds a conflict between the UI-SPEC and the code, it is listed in "Spec Conflicts To Resolve In Planning" and never silently overridden.

### Claude's Discretion
"All implementation choices are at Claude's discretion." One item is explicitly open for the planner in the UI-SPEC (UI Considerations, row "very long configuration lists with the filter off"): paginate vs virtualize. Recommendation is in "Open Configuration Panel Length".

### Deferred Ideas (OUT OF SCOPE)
"Real-environment and real-device checks stay at milestone end (see 69-UAT.md Deferred Follow-Ups)." VER-03 visual baselines (`scripts/Update-VisualBaselines.ps1`) are generated after this phase, at milestone end. Scope is `sources/ReactApp` only; the legacy Blazor WebApp is never touched.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are assigned (ROADMAP: "TBD"). Work is mapped to the UI-SPEC decisions instead.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01 | Native inputs stay, browser drawing goes; no `accent-color` in shared CSS | "Check/radio/switch anatomy" (aria-hidden conflict C-01), style guard additions |
| D-02 | Check semantics: checkbox vs switch vs radio | Call-site mapping verified by grep (21 CheckboxField sites in 13 files, 4 RadioGroupField sites in 4 files) |
| D-03 | `RadioGroupField` choice cards / segmented | Anatomy section; `form-field__radio-row` class is referenced by `hosting-automation.spec.ts:657` |
| D-04 | `SwitchField` | Anatomy; icon mock gap C-11; `aria-checked` note |
| D-05 | Weekday chips, 4 columns at <=520px | `automation.css:218-249`, `--utf-automation-day-min` has exactly two references |
| D-06 | Select designed surface + `base-select` enhancement | lightningcss 1.33.0 accepts the CSS (probe run); existing `form-fields.test.tsx` regex stays green if `appearance: none` rule is kept |
| D-07 | `Disclosure` gains `headingLevel`, `id`, `badge`, `variant` | 12 `<Disclosure` sites, 6 local override sites listed |
| D-08 | Five button variants in `action-control.css` | Conflicts C-02, C-03, C-04; 43 `variant="primary"` sites |
| D-09 | Settings as five collapsible panels | Panel ownership finding (ModPackPanel returns null), test blast radius (8 vitest + 8 e2e files) |
| D-10 | Configuration search + three-chip filter | Data sizes, pure `filterEntries`, `SearchField`/`FilterChipGroup` APIs |
| D-11 | No layout shift on staging | `ConfigurationEntryField` today; contribution branch needs the head row too |
| D-12 | Review dialog compact rows | `change.css:100-349` shell duplication; `Server_Change_Item_Remove` becomes unused (C-10) |
| D-13 | Leave dialog one row + backdrop click | `ConfirmDialog` DOM order and hard-coded `ghost` dismiss (C-05) |
| D-14 | Files panel rebuilt around `file-browser` | Root cause confirmed (`hosting.css:1750-1758`); selector blast radius in 3 test files |
| D-15 | Credentials grid + focus on one-time value | `credential__value` block located; `freshOneTime` flow |
| D-16 | WR-01, WR-02, IN-06, IN-08, refresh target | jsdom `:modal` gap (C-14); exact lines located |
| D-17 | No new dependency | Confirmed: nothing to install; Package Legitimacy Audit not applicable |
| D-18 | Phase 69 gates stay green | Validation Architecture |
</phase_requirements>

## Summary

The phase is a CSS-and-primitive craft pass over an already structurally sound React app. Everything the UI-SPEC needs exists as an extension point: `FormFields.tsx` (primitive file, allowed to hold raw `input`), `Disclosure.tsx` (already a button + region), `ConfirmDialog` in `primitives.tsx`, the shared dialog shell in `shared-primitives.css`, and a token layer in `tokens.css`. No dependency is added (D-17), so there is no Package Legitimacy Audit. I read the primitives, the Settings section and its five panels, the dialog shell, the style/contrast/ratchet guards, the Playwright audit helpers and the screen registry, and ran four probes (jsdom selector support, lightningcss on `base-select` CSS, a contrast calculation, an 82/300/1000-field render benchmark).

The UI-SPEC is approved but contains **eleven implementation conflicts with the code or with the repo's own gates** (section "Spec Conflicts To Resolve In Planning"). The three that change the plan most: (C-01) the drawn marks are specified as `aria-hidden` *ancestors* of the native input, which hides a focusable control from assistive technology and trips axe `aria-hidden-focus`; (C-02) `.command-shell .button--primary` is currently the **gold** gradient, and 43 call sites use `variant="primary"`, so making `primary` neutral (D-08) removes gold from most command-center primary actions unless the sites are re-classified; (C-03) the style guard bans `color-mix(` in every guarded stylesheet, so every hover rule the UI-SPEC writes as `color-mix(...)` must go through tokens defined in `tokens.css`. Also, the UI-SPEC's danger hover (20% tint) fails 4.5:1 text contrast on the raised and overlay surfaces (measured 4.40 and 4.02).

The second planning driver is **test blast radius**: collapsed-by-default Settings panels (D-09) hide their content with the `hidden` attribute, so every vitest and Playwright flow that touches configuration, credentials, mods or automation must open its panel first. Eight vitest files render `SettingsSection` and eight e2e files address the panels. A single shared helper that seeds `sessionStorage["utf.settings.open"]` (vitest and `addInitScript`) keeps that migration to one mechanical change per file.

**Primary recommendation:** Build bottom-up in three waves: (1) tokens + `action-control.css` + guard extensions + `FormFields`/`Disclosure` primitives, (2) the screens that consume them (Settings panels and filter, dialogs, Files, Credentials, Automation, review leftovers) in parallel, (3) test/registry migration and the audit additions. Render the Configuration list in full (no pagination, no virtualization); the filter is the navigation tool.

## Spec Conflicts To Resolve In Planning

Each row is a place where following the UI-SPEC literally fails a gate, breaks accessibility, or contradicts the code. The planner must record the chosen resolution in the plan.

| # | UI-SPEC says | Finding | Recommended resolution | Evidence |
|---|--------------|---------|------------------------|----------|
| C-01 | `span.check-mark` is `aria-hidden` and *contains* the native input (check, switch track, radio mark) | An `aria-hidden` ancestor removes a focusable input from the accessibility tree; axe rule `aria-hidden-focus` fails and `getByRole("checkbox")` finds nothing | Keep the wrapper non-hidden: `span.check-mark` > `input` (overlay) + `span.check-mark__box[aria-hidden]` (drawn box and glyph) as a **sibling**; style the box with `input:checked + .check-mark__box`. Draw the focus ring on the wrapper with `.check-mark:has(input:focus-visible)` because the audit's ring probe walks **ancestors** (see C-12) | `[CITED: dequeuniversity.com/rules/axe/html/aria-hidden-focus]` `[ASSUMED: rule wording from training]`; ring probe `[VERIFIED: tests/e2e/support/a11y.ts:379-388]` |
| C-02 | "`.button--primary` has no rule in `styles.css`; ... primary looks like default in the community layout" | True for `styles.css`, but `commandCenter.css:1-6` makes primary the gold gradient inside `.command-shell`: `.command-shell .button--primary {` `border-color: #e1c56e;` `background: linear-gradient(135deg, #e2c66d, #b9953e);`. `grep` finds 43 `variant="primary"` JSX sites (admin pages, activity, fleet card, job failure, consent dialog) plus `confirmVariantFor` returning `"primary"` for unknown actions | D-08 stays, but add a task that classifies the 43 sites: the single main CTA of a view becomes `gold`; toggle-like `active ? "primary" : "ghost"` sites (`MemberDetailPage.tsx:843`, `RoleDetailPage.tsx:587`) keep neutral `primary` as the selected look; secondary ones stay neutral. Without this the whole admin area loses its primary action color | `[VERIFIED: src/components/commandCenter.css:1-6]`; count `[VERIFIED: grep -rn 'variant="primary"\|variant={...}' src, 43]` |
| C-03 | Hover rules written as `color-mix(in srgb, var(--utf-text) 8%, <fill>)` inside `action-control.css`, `files.css`, `form-fields.css` | `cssGuard.ts:10`: `const colourFunction = /\b(?:rgb|rgba|hsl|hsla|hwb|lab|lch|oklab|oklch|color|color-mix)\(/i;` flags any such value in every guarded sheet (`components/server/*.css`, `shared-primitives.css`; the spec adds two more) | Define the hover as a **token** in `tokens.css` (unguarded: it is the token source of truth). Recommended: one alpha tint `--utf-hover: rgb(244 241 232 / 8%)` (the `--ink` value, same pattern as the `--utf-state-*-bg` 12% tints) applied as a layer: `background-image: linear-gradient(var(--utf-hover), var(--utf-hover));` over the existing `background-color`. It works for every fill and `compositeOver` in the contrast helper can measure it. Alternative: four named `color-mix` tokens (transparent, base, raised, overlay) | guard `[VERIFIED: tests/helpers/cssGuard.ts:10]`; no `color-mix` exists in guarded sheets `[VERIFIED: grep color-mix src]` |
| C-04 | Danger hover: fill `color-mix(in srgb, var(--utf-state-danger-fg) 20%, transparent)`; all pairs must pass 4.5:1 | Computed this session from `--danger #e47777` and the surfaces: 12% tint 5.49 (base), 5.04 (raised), 4.58 (overlay); **20% tint 4.80 / 4.40 / 4.02** - fails on raised and overlay | Do not lighten the danger fill on hover. Hover = same fill, border `--utf-state-danger-fg` (solid) plus the neutral `--utf-hover` layer capped so the pair still passes, or border-only. Add the measured pair to `tokens-contrast.test.ts` so it cannot regress | `[VERIFIED: node computation, tokens from styles.css:1253-1280]` |
| C-05 | Leave dialog: Stay is `primary` and `dismiss`, "No button is `ghost` in this dialog"; additive props are only `closeOnBackdrop` and `confirmPlacement` | `ConfirmDialogForm` hard-codes the dismiss button as `variant="ghost"` (`primitives.tsx:465`) and has no prop to change it | Add an additive `dismissVariant?: ActionControlVariant` (default `ghost`) beside the two named props; `confirmPlacement="start"` moves the confirm button before the dismiss button in DOM order | `[VERIFIED: src/components/primitives.tsx:465-472]` |
| C-06 | Switch track radius "full via `--utf-radius`" | `--radius` is `5px` (`styles.css`), not a pill; a literal `9999px` is flagged by the pixel guard | `border-radius: calc(var(--utf-switch-track-height) / 2)` | `[VERIFIED: src/styles.css --radius: 5px]` |
| C-07 | Overlaid input "covers the whole mark (at least 24x24px)" with `--utf-check-size` 20px | The mark is 20px, so the overlay is 20x20. axe `target-size` (WCAG 2.2 AA is in the tag set) tolerates an undersized target only with 24px spacing clearance | Make the overlay input cover the whole label row (`label{position:relative}`, input `inset:0`) or give the wrapper a 24px minimum hit area; keep the drawn box 20px. The row is already 40/44px high | `[VERIFIED: tests/e2e/support/a11y.ts:54 "wcag22aa adds the 24px target-size rule"]` |
| C-08 | Deep link focuses "the heading"; `h2` inside the disclosure button wrapper | Today `AutomationPanel` focuses its own `h2` (`tabIndex={-1}`), and three Playwright assertions expect `getByRole("heading", { level: 2, ... }).toBeFocused()` (`hosting-automation.spec.ts:247, 486, 627`) plus `workspaceScreens.ts:421` (`document.activeElement === document.querySelector("#server-automation h2")`). A heading inside the accordion pattern is not focusable; the button is | Focus the disclosure **button** after opening on a hash or invalid-field cause; migrate those four assertions to `getByRole("button", { name })` / `#server-automation .disclosure__summary` | `[VERIFIED: AutomationPanel.tsx:140-148, hosting-automation.spec.ts grep]` |
| C-09 | Call-site counts: CheckboxField "19 sites in 15 files", RadioGroupField "6 sites in 5 files" | Counted this session: **21** `<CheckboxField` JSX sites in **13** files, **4** `<RadioGroupField` sites in **4** files | Use the verified counts; the planner re-runs the greps as the UI-SPEC asks | `[VERIFIED: grep -rn "<CheckboxField" src, grep -rn "<RadioGroupField" src]` |
| C-10 | Review row drops the visible text `Server_Change_Item_Remove`; `Server_Files_ImmediateNote`, `Server_Files_FolderHeading` deleted | `npm run ci` includes `locales:unused`; these three keys exist today and become unreferenced. Deleting must happen in the same plan that removes the last use, in both locale files | Plan task per key group: remove usage, delete key from `en.json` and `de.json`, run `locales:check` and `locales:unused` | `[VERIFIED: keys present in en.json; package.json scripts.ci]` |
| C-11 | `Checkmark16Filled` is a "new import" | `vitest.config.ts` aliases `@fluentui/react-icons` to `tests/mocks/react-icons.tsx`, an explicit list of named exports. `Checkmark16Filled` is not in it (`Dismiss16Regular` is, at line 95) so the component would render `undefined` in every vitest file | Add the icon exports the phase introduces to the mock in the first plan that imports them | `[VERIFIED: vitest.config.ts alias; grep of tests/mocks/react-icons.tsx]` |
| C-12 | "The ring is drawn on the mark" | The focus-ring audit measures the focused element, then up to **three ancestors that match `:focus-within`**, and requires an outline or box-shadow of >=2px in the gold-bright color with >=3:1 contrast. A ring on a *sibling* (`input:focus-visible + .box`) is invisible to it | Ring on the wrapper (`:has(input:focus-visible)`), a card, chip or segment label | `[VERIFIED: tests/e2e/support/a11y.ts:379-394, measureRing allowAncestor]` |
| C-13 | "Every pair in the control state table passes 3:1 / 4.5:1" | The Disabled row uses `--utf-border-divider` (`#293442`) which is 1.25:1 on overlay; WCAG exempts inactive components | Exclude the Disabled row from the contrast assertion and say why in the test | `[VERIFIED: node computation]`; exemption `[CITED: w3.org WCAG 1.4.3/1.4.11 "inactive user interface component"]` `[ASSUMED]` |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Drawn check/radio/switch/chip marks | Browser / Client (CSS on native inputs) | - | Pure presentation; native input keeps form, focus, a11y semantics |
| Settings panel open state | Browser / Client (`sessionStorage`, React state) | - | UI preference only; ids only, no data; no server round trip |
| Configuration search and filter | Browser / Client (local list) | - | List already loaded by `GET .../configuration`; filtering never hits the API |
| Staged-change draft, review, apply | Browser / Client (existing draft provider) | API / Backend (apply endpoints, unchanged) | Phase 65 contract; this phase changes only presentation |
| Provider/worker/cost behavior | API / Backend | - | Untouched; explicitly out of scope |
| Visual baselines | CI container (Playwright, pinned Linux) | - | Generated at milestone end, not in this phase |

## Standard Stack

No new packages (D-17). Existing, pinned in `sources/ReactApp/package.json` (read this session):

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | existing |
| vite | 8.2.1 | build (lightningcss minify) | existing |
| vitest + jsdom + @testing-library/react + user-event | 4.1.10 / 28.0.0 / 16.3.2 / 14.6.1 | unit/component tests | existing gate |
| @playwright/test + @axe-core/playwright | 1.62.1 / 4.13.0 | e2e + a11y audits | pinned by D-17 |
| @fluentui/react-icons | 2.0.320 | icons (aliased to a mock in vitest) | existing |
| react-hook-form | 7.85.0 | `register`/`ref` must keep reaching the native input | existing |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Overlaid native input | Fluent UI Checkbox/Radio/Switch | Rejected by UI-SPEC (second styling system) |
| Native `select` + `base-select` | Custom listbox | Rejected by UI-SPEC |
| Render-all config list | `@tanstack/react-virtual` or pagination | Not needed at <=82 entries; would break focus-first-invalid and find-in-page. Also adds a dependency (D-17) |

**Installation:** none.
**Version verification:** `npm view` not run because nothing is installed. Pinned versions read from `package.json` and the UI-SPEC lockfile resolution.

## Package Legitimacy Audit

Not applicable: this phase installs no external package (D-17). `gsd_run query package-legitimacy check` was not run.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Settings tab (SettingsSection, draft provider)
  |
  |-- useSettingsPanelState (context)  <-- sessionStorage["utf.settings.open"] (ids only)
  |        ^  open(id) on: location.hash match | first staged change | review attempt with aria-invalid
  |        |  never closes by itself; Expand/Collapse all skip panels that must stay open
  |
  |-- ModPackPanel        --renders--> SettingsPanel(id server-mods)          [returns null if capability lacks mod packs]
  |-- ServerConfigurationPanel -----> SettingsPanel(id server-configuration)
  |        |-- ConfigurationFilter (SearchField + FilterChipGroup, toolbar from >= 8 entries)
  |        |        filterEntries(entries, query, mode, draft) -> flat grouped list | Common + Advanced split when filter empty
  |        '-- ConfigurationEntryField (head row: label | state slot, height reserved by capability)
  |                 '-- SwitchField / SelectField / TextField / TextAreaField / game contribution
  |-- ServerSecretsPanel  --(canManageSecrets)--> SettingsPanel(id server-credentials)
  |-- AutomationPanel     -----> SettingsPanel(id server-automation) --> chips + switches
  |-- record block        --(showRecord)--> SettingsPanel(id server-record)
  |
  '-- PendingChangesBar -> ReviewChangesDialog (ConfirmDialog, compact rows) / leave dialog (ConfirmDialog variant)

Shared primitives (CSS + TSX):  tokens.css -> action-control.css, form-fields.css, shared-primitives.css
Guards:  server-style-guard (token-only CSS) | tokens-contrast | legacy-ratchet | locales:check/unused | ratchet.test.ts | a11y audits
```

### Recommended Project Structure
```
src/
├── styles/tokens.css                      # + check/switch/chip/compact/dropzone/review/choice-card tokens, --utf-hover
├── components/
│   ├── FormFields.tsx                     # CheckboxField(variant), RadioGroupField(layout), SwitchField, labelEnd prop
│   ├── Disclosure.tsx                     # + headingLevel, id, badge, variant
│   ├── ActionControl.tsx                  # imports action-control.css
│   ├── action-control.css                 # NEW: five variants + compact, tokens only
│   ├── primitives.tsx                     # ConfirmDialog: closeOnBackdrop, confirmPlacement, dismissVariant
│   └── server/
│       ├── SettingsPanel.tsx, useSettingsPanelState.ts      # NEW (panel context + registration)
│       ├── ConfigurationFilter.tsx, configurationFilter.ts  # NEW (pure filterEntries)
│       ├── FileBrowser.tsx, FileUploadDropzone.tsx, files.css  # NEW
│       └── form-fields.css, change.css, automation.css, credentials.css  # edited
└── features/hosting/                      # panels render SettingsPanel themselves
```

### Pattern 1: Panels render `SettingsPanel` themselves (not the section)
**What:** each of the five panel components composes `<SettingsPanel id title summary badge>` around its body; `SettingsSection` only provides the open-state context.
**Why:** the summary data and the render condition live inside the panel. `ModPackPanel` returns `null` when the capability query says mod packs are unsupported (`ModPackPanel.tsx:211-232`), and `ServerConfigurationPanel` early-returns loading/error `StatePanel`s today (`ServerConfigurationPanel.tsx:253-269`). A wrapper in `SettingsSection` would render an empty Mods header for games without mod packs and no header while loading. Panels register their id with the context on mount, which gives `Expand all` its list of rendered panels.
**Keep mounted:** `Disclosure` renders children with the `hidden` attribute, it does not unmount. `AutomationBody` holds its draft in React state, so unmounting on collapse would silently lose unsaved automation edits. Never `{open && children}`.

### Pattern 2: Adjust state during render keyed on a signature (auto-open)
Already used by the Advanced disclosure (`ServerConfigurationPanel.tsx:218-232`: `triggerSignature` / `seenTriggers`). Reuse it for "open the panel the moment a change is staged": it opens only for a trigger not seen before, so the member can still close a panel and it reopens only for new staged items. `[VERIFIED: ServerConfigurationPanel.tsx]`

### Pattern 3: Check mark anatomy that keeps the input in the accessibility tree
```tsx
// Source: resolution of C-01/C-07/C-12; structure is [ASSUMED] engineering, constraints are [VERIFIED] above
<label className="form-field__check" htmlFor={wiring.controlId}>
  <span className="check-mark">
    <input {...inputProps} id={wiring.controlId} type="checkbox" className="check-mark__input" />
    <span className="check-mark__box" aria-hidden="true">
      <Checkmark16Filled />
    </span>
  </span>
  <span className="form-field__check-text">{label}</span>
</label>
```
```css
.check-mark { position: relative; }
.check-mark__input { position: absolute; inset: 0; width: 100%; height: 100%; margin: 0; opacity: 0; cursor: inherit; }
.check-mark__input:checked + .check-mark__box { background: var(--utf-text); border-color: var(--utf-text); }
.check-mark:has(.check-mark__input:focus-visible) { outline: var(--utf-focus-ring); outline-offset: var(--utf-focus-offset); }
```
Keep the existing classes `form-field__check` and `form-field__radio-row` (referenced by `hosting-automation.spec.ts:657` and the existing `:has` rules).

### Pattern 4: Hover through one token layer (C-03)
```css
/* tokens.css (unguarded) */  --utf-hover: rgb(244 241 232 / 8%);
/* action-control.css (guarded) */
.button--ghost:hover:not([aria-disabled="true"]) { background-image: linear-gradient(var(--utf-hover), var(--utf-hover)); }
```
`linear-gradient(` is not in `colourFunction`; `var()` is not flagged. `[VERIFIED: guard regex]`. The transform survives lightningcss `[VERIFIED: probe run, lightningcss 1.33.0 emitted linear-gradient(var(--h), var(--h)) unchanged]`.

### Pattern 5: Backdrop close needs pointerdown AND click on the dialog element
Because `.dialog form` fills the dialog (`shared-primitives.css`), a click inside normally has `target` = a descendant; a click on the dialog's own box padding also has `target === dialog`, so the handler must test the pointer coordinates against `getBoundingClientRect()` and require the preceding `pointerdown` also on the backdrop (drag-from-inside rule). Ignore while `busy`. Escape keeps using `onCancel` (`primitives.tsx` `onCancel` handler). `[VERIFIED: ConfirmDialog source]`

### Anti-Patterns to Avoid
- **`aria-hidden` ancestor of an input** (C-01).
- **Conditional rendering of panel children** (loses Automation draft).
- **`color-mix(` / `rgb(` / hex / `Npx` in guarded CSS** (guard fails); put values in `tokens.css`.
- **Per-page restyling of buttons/disclosures**: `Disclosure` already has 6 page-local override blocks (below); new base rules must not leak into them.
- **Removing CSS without lowering the ratchet**: `legacy-ratchet.test.ts` has a test that fails when a measured count is *below* its ceiling ("holds every ceiling at the current count so that a conversion must lower it"). Run `UTF_LEGACY_RATCHET_UPDATE=1 npm --prefix sources/ReactApp test -- tests/styles/legacy-ratchet.test.ts` after each CSS removal.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Search box with clear + Escape | new input | `SearchField` | Existing, tested; label is visually hidden, clear is a named icon button |
| Single-select chip filter with counts | new chip row | `FilterChipGroup` (`aria-pressed`, check icon, zero counts stay selectable) | Existing semantics + German number formatting |
| Empty/loading/error panels | custom markup | `EmptyState`/`LoadingState`/`ErrorState`/`StatePanel` | Phase 69 closed header kinds and gates |
| Segmented selector, Files context switch | `role="tablist"` buttons | `RadioGroupField layout="segmented"` | Arrow keys and `fieldset`+`legend` for free |
| Modal backdrop | custom overlay | native `<dialog>` + `::backdrop` (existing) | UI-SPEC rejects replacing it |
| Deep-link scrolling | manual offset math | `scroll-margin-block-start` + existing `html { scroll-padding-block-start: var(--command-header-height, 0) }` | already in `shared-primitives.css` |
| Config list virtualization | windowing lib | render all (see below) | 82 entries max, no dependency allowed |

**Key insight:** every control in this phase already has a project primitive; the work is making the primitive correct once and deleting page-local variants (the owner's complaint list is almost entirely "primitive is plain" rather than "screen is wrong").

## Runtime State Inventory

Not a rename/refactor/migration phase. The only persisted state introduced is `sessionStorage["utf.settings.open"]` (a JSON array of anchor ids). Nothing else stores or caches renamed strings: no stored data, live service config, OS-registered state, secrets or build artifacts are affected. One state-adjacent item: the token `--utf-automation-day-min` is renamed `--utf-day-chip-min`; it has exactly two references (`tokens.css:50` and `automation.css:220`) `[VERIFIED: grep]`.

## Open Configuration Panel Length (paginate vs virtualize)

**Recommendation: neither. Render the full list; the Phase 70 filter plus the collapsed Advanced split are the length control.**

Evidence:
- The largest game catalog in the codebase is Arma 3 with **82** definitions (81 `Definition(` calls plus the control-plane Headless Client entry); Aska has 7, Counter-Strike 2 has 12. `[VERIFIED: grep -cE "^\s+(Definition|ControlPlane...)\(|^\s+new\($" in sources/GameServerImplementations/*ConfigurationImplementation.cs]`. Definitions are compiled into the API per game, not user-extensible, so "hundreds" is not reachable with the current product.
- Only 6 Arma entries are `Common` (`tier: EGameConfigurationTier.Common`, e.g. `Arma3ConfigurationImplementation.cs:188`). The default open Configuration panel therefore shows ~6 fields; the other ~76 are behind the Advanced disclosure, which is collapsed.
- Render cost measured in this repo's jsdom (slower than a browser, so an upper bound): 82 `ConfigurationEntryField`s = 100-140 ms and 725 DOM nodes; 300 = 301 ms / 2,641 nodes; 1,000 = 521 ms / 8,801 nodes. `[VERIFIED: temporary vitest probe, deleted after the run]`
- Virtualization or pagination would break existing behavior that depends on every field being in the DOM: `SettingsSection` focuses the first `[aria-invalid="true"]` control on a review attempt, the Advanced disclosure auto-opens from the staged set, the draft must survive filtering ("the filter never unmounts a staged entry's draft"), and browser find-in-page and axe/keyboard audits walk the real DOM.
- Escape hatch if a game ever ships hundreds: add a threshold (for example above 200) that renders groups collapsed behind per-group `Disclosure`s. Do not build it now `[ASSUMED]`. Do **not** add `content-visibility: auto` now: it changes off-screen box heights and would make the D-11 bounding-box test fragile `[ASSUMED]`.

## Common Pitfalls

### Pitfall 1: Collapsed panels hide content from tests
**What goes wrong:** `Disclosure` content carries `hidden`; Testing Library `getByRole`/`getByLabelText` ignore it and Playwright `fill` waits forever.
**Why:** D-09 collapses every panel on a first visit.
**How to avoid:** One helper seeds `sessionStorage["utf.settings.open"]` with all (or chosen) ids. Vitest: a `renderSettings`-level helper plus `afterEach(() => sessionStorage.clear())` in `tests/setup.ts` (same jsdom window serves a whole file, so state leaks between tests otherwise). Playwright: `page.addInitScript` in `installWorkspace` behind an option (default: seed all open so the existing flows are unchanged; the new collapsed-state tests opt out).
**Affected (verified by grep):** vitest - `arma-placement`, `change-flow`, `configuration-panel`, `credentials-panel`, `mod-pack-panel`, `review-changes-dialog`, `workspace-page`, `workspace-section-panels` (all import `SettingsSection`), plus `automation-panel` via `tests/helpers/automationPanelHarness.tsx`. e2e - `hosting-a11y`, `hosting-automation`, `hosting-change`, `hosting-files-secrets`, `hosting-placement`, `journeys/automation`, `journeys/change-mods`, `journeys/german-locale`, and the registry helpers `stageSetting`, `stageEverything`, `openReviewDialog`, `openLeaveDialog` in `tests/e2e/support/screens/workspaceScreens.ts:215-280` (`.mods-panel select`, `getByLabel("Slots")`, `.credential__edit input` live in panels).

### Pitfall 2: `#server-automation` selectors change meaning
Today the id sits on the Automation `section` that is also the `workspace-panel`. The registry uses `#server-automation > .state-panel--info`, `#server-automation .automation-status`, `#server-automation h2` (`workspaceScreens.ts:410-469`). Keep the id on the outer `section` (so deep links and `Overview` anchors keep working: `OverviewSection.tsx:46`, `AutoStopNotice.tsx:48`, `urlParameters.ts:61`), but the child-combinator selector breaks because content now sits inside the disclosure region.

### Pitfall 3: Invalid-field focus into a hidden panel
`SettingsSection` does `sectionRef.current?.querySelector('[aria-invalid="true"]')?.focus()` on `reviewAttempt`. A control inside `hidden` cannot take focus; `focus()` silently no-ops. Resolve the panel from the element (`closest("[data-settings-panel]")`), open it, then focus on the next animation frame (UI-SPEC rule).

### Pitfall 4: Page-local `Disclosure` overrides leak
New `variant="inline"` base styles (bordered full-width header, overlay fill) apply to every existing use. Local override blocks that must be reviewed: `eventAuthoring.css:31-75` (`.event-role-symbol-picker .disclosure__summary`), `hosting.css:526-544` (`.next-hosting-audit-evidence`), `hosting.css:2006-2037` (`.next-hosting-protocol-list`, `.next-hosting-diagnostics`), `server.css:246-247` (`.cost-confirm__note`), `activity.css:151` (`.activity-entry__disclosure`). `ActivityEntry` renders one disclosure per timeline entry, so a heavy bordered header there multiplies; verify visually.

### Pitfall 5: Button height changes are global
`.button` today is `min-height: var(--control-height)` (38px) with no mobile rule; touch height exists only in a few per-component rules (`shared-primitives.css:386-388` for dialogs). D-08/Refresh-target ("`--utf-control-height` and `--utf-control-height-touch` at <= 800px through the shared button rule") makes every button 40px desktop / 44px at <=800px. Expect layout movement in toolbars, fleet cards and headers; reflow and text-spacing audits will catch real overflow. Visual baselines regenerate anyway.

### Pitfall 6: Ratchet and guard bookkeeping
- `action-control.css` lives in `src/components/`, not `components/server/`. `server-style-guard.test.ts` scans only `components/server/*.css` and `shared-primitives.css`, and `legacy-ratchet.test.ts` `isGuarded()` lists only those plus `styles/tokens.css`; an unlisted new stylesheet fails "lists every legacy stylesheet in legacy-ratchet.json". Either extend both lists (spec intent: tokens-only) or place the file under `components/server/`. Recommended: extend `isGuarded` and the guard's file list, and add the new sheet to neither ratchet entry. `files.css` is under `components/server/` and is guarded automatically.
- `ratchet.test.ts` (Phase 69 raw-element ratchet): new components in `src/components/server` and `src/features/hosting` are Tier A. No raw `button/input/select/textarea/details/dialog` outside `primitiveFiles` (`ActionControl`, `FormFields`, `Disclosure`, `SearchField`, `primitives`, `AppToasts`), no literal `aria-label`/`title`/`label`/`legend`/`hint` strings, and no `className="button ..."` lookalikes. The dropzone must use `ActionButton`/`FileChooserButton`; every string via `t()`.
- Legacy CSS removed from `styles.css`, `commandCenter.css`, `hosting.css` lowers the measured hex/pixel counts below ceilings, which fails the "stale ceiling" test until the writer is run.

### Pitfall 7: jsdom limits
`document.querySelector("dialog:modal")` returns `null` in jsdom 28 even for an open dialog (`dialog:modal` and `dialog[open]` probed: first false, second true), and the test setup polyfills `showModal()` as `this.open = true` only. For WR-01 use a selector that works in both: `document.querySelector("dialog[open]")` (the app opens dialogs only through `showModal()` in `ConfirmDialog`) and keep a Playwright test for the real modal behavior. jsdom does not apply CSS layout, so geometry assertions (D-11, D-13, chip rows) are Playwright-only.

### Pitfall 8: Existing tests that assert markup or CSS text
- `form-fields.test.tsx:278-296` asserts `appearance` computes to `none`, `padding-inline-end` is `var(--utf-space-xl)` and the CSS matches `/\.form-field__control--select\s*\{[^}]*appearance:\s*none;/`. Keep that base rule untouched and put `base-select` only in an `@supports` rule whose selector list is comma-separated (so it does not match the regex).
- `files-panel-controls.test.tsx:385` reads `hosting.css` (`expect(hostingCss).not.toContain("next-hosting-file-search")`).
- `files-panel-location.test.tsx:146-149` and `workspace-page.test.tsx:424` use `getByRole("tab", { name: "Live worker files" })`; the Files context switch becomes a radio group, so these become `getByRole("radio", ...)`. `adjacentScreens.ts:161-165` uses `.next-hosting-file-contexts [role=tab]`.
- `adjacentScreens.ts:86` clicks `.next-hosting-file-toolbar .button--primary` (the Upload action); the spec makes Upload `gold`.
- `a11y-static.test.ts` and `hard-coded-text.test.ts` scan all handwritten source.
- To minimize churn keep the existing `next-hosting-*` hook classes on the Files DOM and add the new `file-browser*` classes; only the CSS moves to `files.css`.

### Pitfall 9: Locale gates
`locales:check` fails on identical EN/DE values and on English function words or carry-over in German values; keys are a flat object (3,897 keys each, today). New German values in the UI-SPEC table avoid listed words. The IN-06 pass touches today: `Modpack` 35, `Modpaket` 16, `Gelände` 43 German values (`Mod-Paket` 34, `Karte` 33 already). `Pages_Events_Detail_Status_ConfirmedTeam` is "Confirmed member" / "Bestätigtes Mitglied", identical to `_ConfirmedMember` today. `[VERIFIED: node over en.json and de.json]`

### Pitfall 10: Registry cost
`S-06` already carries a comment that two keyboard-walk states pushed the test to two thirds of its timeout (`workspaceScreens.ts:555-570`, `allowLongWalk()`). The UI-SPEC wants six Settings states. Collapsed panels make most states cheap, but "all open" is the longest page in the app. Add states as separate entries or with the heavy ones audited `keyboard: false` and a justification, rather than six states on one entry.

## Code Examples

### Segmented / card radio (keeps `fieldset` + `legend`)
```tsx
// Source: structure [ASSUMED]; semantics per UI-SPEC "Choice cards and segmented bar"
<fieldset className="form-field form-field--radio" data-layout={layout}>
  <legend className="form-field__legend">{legend}</legend>
  {options.map((option) => (
    <label key={option.value} className="choice-card form-field__radio-row">
      <span className="check-mark check-mark--radio">
        <input type="radio" name={name} value={option.value} checked={option.value === value} /* ... */ />
        <span className="check-mark__box" aria-hidden="true" />
      </span>
      <span className="form-field__radio-text">{/* label + help */}</span>
    </label>
  ))}
</fieldset>
```

### Panel state hook (storage failure means collapsed, no error)
```ts
// Source: UI-SPEC "Open state"; implementation [ASSUMED]
const storageKey = "utf.settings.open";
function readOpenIds(): string[] {
  try {
    const parsed: unknown = JSON.parse(sessionStorage.getItem(storageKey) ?? "[]");
    return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === "string") : [];
  } catch {
    return [];
  }
}
```

### `filterEntries` contract (pure, unit tested)
Matches the display name, group name and (only when present) description, case-insensitive, trimmed, diacritics ignored (`localeCompare(..., { sensitivity: "base" })` or `normalize("NFD")`), and never the value of a `sensitive` or `isRedacted` entry. `isRequiredEmpty(entry, value)` already exists in `ServerConfigurationPanel.tsx` and should move to the shared module so the "Needs a value" chip and the Advanced auto-open share one definition. `[VERIFIED: ServerConfigurationPanel.tsx isRequiredEmpty]`

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Fully custom listbox for branded selects | Native `select` + `appearance: base-select` + `::picker(select)` | Chrome/Edge 135 | Progressive enhancement; Firefox lacks it, Safari uncertain. Falls back to the padded native select. `[CITED: developer.chrome.com/blog/a-customizable-select]`; Safari support `[ASSUMED]`, not confirmed by the search |
| Styling inputs with `accent-color` | Overlaid native input + drawn mark | n/a | D-01; the style guard test to add fails on `accent-color` |

**Deprecated/outdated:** the `<details>` use called out in the Phase 65 UAT is already gone: `grep -rn "<details" src` returns 0 `[VERIFIED]`; `Disclosure` is already a button + region.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `role="switch"` on a native checkbox should not also render `aria-checked`; assert with jest-dom `toBeChecked()` (checks `.checked` for `input[type=checkbox]`) instead of the attribute the UI-SPEC test names | Pattern/Verification | A Wave 0 test decides; if a literal `aria-checked` is required, render it from controlled state only |
| A2 | Safari desktop support for `appearance: base-select` unknown | State of the Art | None for correctness (fallback), only for the deferred human check |
| A3 | `--utf-hover` alpha-layer approach is preferable to four `color-mix` tokens | C-03 | Visual difference between the two is nil on opaque fills; either passes the guard |
| A4 | Classification of the 43 `primary` call sites (main CTA -> `gold`) is the intended reading of D-08 plus the accent list | C-02 | If the owner wants gold removed from admin pages, skip the re-classification; needs one confirmation |
| A5 | Escape hatch threshold (200 entries) and rejecting `content-visibility: auto` | Open Configuration Panel Length | Only matters if a game catalog grows >100x |
| A6 | WCAG inactive-component exemption applies to the disabled row | C-13 | If not exempt, disabled needs a 3:1 border (would conflict with the muted-disabled look) |
| A7 | Axe `aria-hidden-focus` and `target-size` rule behavior as described | C-01, C-07 | The audit specs run both rules; a Wave 1 axe run on a small fixture page confirms before building screens on top |

## Open Questions

1. **Re-classifying `variant="primary"` (A4).**
   - Known: 43 sites, gold today inside `.command-shell`, neutral by D-08.
   - Unclear: owner intent for admin pages (the UAT complaint was about the server UI).
   - Recommendation: classify in a dedicated task with a table in the plan summary; default main-CTA -> `gold` so nothing the owner sees today loses its accent. Flag in the plan so the user can veto at the checkpoint.
2. **Where `labelEnd` renders for the game-contribution branch.** `ConfigurationEntryField` builds its own label markup for `Contribution` (the Arma placement field), bypassing `TextField`/`SelectField`. The head row must also be applied there or D-11 holds only for generic controls. Recommendation: build the head row once as a small internal component used by all five branches.
3. **`SettingsPanel` for "Collapse all" when only staged panels are open.** UI-SPEC says skip them; the button label then stays "Collapse all" with nothing to do. Recommendation: label swap is driven by "any non-pinned panel open", which keeps the button honest.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | everything | ✓ | v25.5.0 (package.json engines say `>=24 <25`; commands ran, npm may warn) | - |
| npm | scripts | ✓ | 11.8.0 | - |
| vitest, eslint, tsc, playwright (bin) | gates | ✓ | node_modules present | - |
| Playwright Chromium | e2e | ✓ | several `chromium-*` builds in `%LOCALAPPDATA%/ms-playwright` | - |
| Docker | `scripts/Update-VisualBaselines.ps1` | ✓ | 29.5.3 | deferred to milestone end by CONTEXT |
| Backend / real stack | none (suite is mocked) | n/a | - | Playwright uses mock API + `scripts/classic-stub.mjs` |

**Missing dependencies with no fallback:** none.

## Validation Architecture

(`workflow.nyquist_validation: true` in `.planning/config.json`.)

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 (jsdom, `css: true`, globals) + Playwright 1.62.1 with `@axe-core/playwright` 4.13.0 |
| Config file | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts` |
| Quick run command | `npm --prefix sources/ReactApp test -- <files>` (subset of 7 files incl. `tests/styles` ran in 5.7 s) |
| Full suite command | `npm --prefix sources/ReactApp run ci` (locales:check, locales:unused, contracts:check, format:check, lint, typecheck, test, build:assets); e2e: `npm --prefix sources/ReactApp run test:e2e` (builds assets first; first run waits up to 360 s for the web server) |
| Project gates (also scripts) | `npm --prefix sources/ReactApp run locales:check`, `run locales:unused`, `run lint`, `run typecheck`, `run format:check` |

Baseline: the full vitest suite is green on the untouched tree; see "Baseline status" at the end of this section.

### Phase Decisions -> Test Map
| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01, D-02, D-03, D-04, D-05 | native input of right type/role, mark `aria-hidden` sibling, `register`/`ref` reach input, switch role, chip/card/segmented variants, disabled stays visible, `onLabel`/`offLabel` required | unit | `npm --prefix sources/ReactApp test -- tests/form-fields.test.tsx` | ✅ extend |
| D-01, D-06, D-08 | no `accent-color`, no `appearance: auto`, no raw hex/px outside allowlist, `.button--ghost:hover` has no `--gold`, new sheets scanned | style guard | `npm ... test -- tests/styles/server-style-guard.test.ts tests/styles/legacy-ratchet.test.ts` | ✅ extend (add `action-control.css`) |
| D-01, D-08 | control-state and button pairs: 3:1 non-text, 4.5:1 text on base, raised, overlay; danger hover pair; Disabled row exempt | unit | `npm ... test -- tests/styles/tokens-contrast.test.ts` | ✅ extend |
| D-07 | heading wrapper, `id`, `badge`, `variant`; region `hidden` when closed; Enter/Space | unit | `npm ... test -- tests/disclosure.test.tsx` | ✅ extend |
| D-09 | collapsed by default; summaries; hash opens; staging opens; invalid opens then focuses; storage persistence and corrupt value; Expand/Collapse all skip staged; credentials absent without `canManageSecrets`; Mods absent for unsupported games | component | `npm ... test -- tests/settings-panel.test.tsx tests/hosting/settings-section.test.tsx` | ❌ Wave 0 (both new; UI-SPEC names them) |
| D-09 | existing flows still pass with panels seeded open | component | `npm ... test -- tests/hosting/change-flow.test.tsx tests/hosting/configuration-panel.test.tsx tests/hosting/credentials-panel.test.tsx tests/hosting/mod-pack-panel.test.tsx tests/hosting/arma-placement.test.tsx tests/hosting/workspace-page.test.tsx tests/hosting/workspace-section-panels.test.tsx tests/hosting/automation-panel.test.tsx tests/hosting/settings-leave-guard.test.tsx` | ✅ migrate |
| D-10 | `filterEntries` pure rules, chips, flat grouped result, count text, empty state, staged value survives filtering, toolbar from 8 entries | unit/component | `npm ... test -- tests/hosting/configuration-filter.test.ts tests/hosting/configuration-panel.test.tsx` | ❌ Wave 0 (filter test new) |
| D-11 | no layout shift on staging at 390 and 1280 within 0.5px | e2e | `npm ... run test:e2e -- --project=chromium --project=mobile-390 tests/e2e/hosting-change.spec.ts` | ✅ extend (layout is Playwright-only) |
| D-12 | compact row, `dl` kept with sr-only Before/After, icon-only Remove with named label, mod diff closed disclosure, only width class on dialog; 10 rows without scroll at 1280x900, 40 scroll, Apply reachable at 320 | component + e2e | `npm ... test -- tests/hosting/review-changes-dialog.test.tsx tests/hosting/change-flow.test.tsx` and `... test:e2e -- tests/e2e/hosting-change.spec.ts` | ✅ extend existing `review-changes-dialog.test.tsx` (UI-SPEC names a new `review-dialog.test.tsx`; extending the existing file avoids a duplicate) |
| D-13 | `closeOnBackdrop` (backdrop calls cancel, inside does not, ignored while busy, drag from inside does not), `confirmPlacement="start"` DOM order, `dismissVariant`; one row at 1280 and 521 with German labels, stacked at 520 | unit + e2e | `npm ... test -- tests/primitives.test.tsx tests/hosting/settings-leave-guard.test.tsx`; e2e `hosting-change.spec.ts`, `audit/dialog-geometry.spec.ts` | ✅ extend |
| D-14 | table first row within 24px of folder heading, heading never empty, Upload/New folder in card, footer note, drop zone and queue at 390 | unit + e2e | `npm ... test -- tests/hosting/files-panel-controls.test.tsx tests/hosting/files-panel-location.test.tsx`; e2e `hosting-files-secrets.spec.ts` | ✅ migrate + extend |
| D-15 | grid columns align across rows, one-time block focused and inside the viewport above the pending bar after Rotate | component + e2e | `npm ... test -- tests/hosting/credentials-panel.test.tsx`; e2e `hosting-files-secrets.spec.ts` at 390x844 | ✅ extend |
| D-05 (Automation) | two distinct chip `top` values at <=520, one above; switches operate with Space; forced-colors renders distinct checked/unchecked | e2e | `npm ... run test:e2e -- tests/e2e/hosting-automation.spec.ts` | ✅ extend |
| D-16 WR-01 | Tab with blank-area focus inside a modal opened over the open menu lands in the dialog, not the drawer; no-dialog case keeps the cycle | unit + e2e | `npm ... test -- tests/shell-layouts.test.tsx` and e2e `tests/e2e/public-shell.spec.ts` | ✅ extend |
| D-16 WR-02 | `ConfirmedTeam` differs from `ConfirmedMember` in both locales | unit | `npm ... test -- tests/locales` | ❌ Wave 0 assertion |
| D-16 IN-06 | no `Modpack`/`Modpaket`/terrain-noun `Gelände` in German values, exceptions listed with a reason | unit | `npm ... test -- tests/locales/de-glossary.test.ts` | ❌ Wave 0 |
| D-16 IN-08 | `loadMediaAsset` returns exactly `null` on 404; caller drops `?? null` | unit | `npm ... test -- tests/admin-site-images.test.ts tests/admin-gallery-asset-picker.test.ts` | ✅ extend (no existing test calls `loadMediaAsset`) |
| D-16 refresh target | `.workspace-details__refresh` button >=44px high at 390 and 320 | e2e | `npm ... run test:e2e -- --project=mobile-390 --project=mobile-320 tests/e2e/audit` | ✅ extend |
| D-18 | axe WCAG 2.0-2.2 AA, keyboard walk, 320/390 reflow, text spacing, German walk, reduced motion over the registry additions | e2e | `npm ... run test:e2e -- tests/e2e/audit tests/e2e/hosting-a11y.spec.ts` | ✅ extend registry |
| all | locale parity, no unused keys, no hard-coded text in Tier A | script + unit | `npm ... run locales:check && npm ... run locales:unused`; `test -- tests/locales` | ✅ |
| all | raw-element ratchet, a11y-static | unit | `npm ... test -- tests/ratchets tests/a11y-static.test.ts` | ✅ |

### Sampling Rate
- **Per task commit:** `npm --prefix sources/ReactApp test -- <touched test files> tests/styles` plus `run typecheck` and `run lint` for a TS change.
- **Per wave merge:** full vitest (`npm --prefix sources/ReactApp test`), `locales:check`, `locales:unused`, `lint`, `typecheck`, `format:check`; for CSS-removal waves also run the ratchet writer once and commit the lowered `legacy-ratchet.json`.
- **Phase gate:** `npm --prefix sources/ReactApp run ci` green, then `test:e2e` on `chromium`, `mobile-390`, `mobile-320` (audit, hosting specs) green. Pixel baselines are deliberately not part of the phase gate (CONTEXT: regenerated at milestone end; pixel comparison runs only with `UTF_VISUAL_BASELINES=1`).

### Existing files and gates that must stay green
`tests/form-fields.test.tsx`, `tests/disclosure.test.tsx`, `tests/primitives.test.tsx`, `tests/styles/{server-style-guard,tokens-contrast,legacy-ratchet,button-wrap,closed-findings}.test.ts`, `tests/ratchets/ratchet.test.ts`, `tests/a11y-static.test.ts`, `tests/locales/*`, `tests/hosting/{screen-registry,change-flow,configuration-panel,credentials-panel,mod-pack-panel,review-changes-dialog,settings-leave-guard,workspace-page,workspace-section-panels,automation-panel,files-panel-controls,files-panel-location,actions}.test.*`, `tests/app/{route-links,url-parameters}.test.ts` (the hash anchors and `urlParameters` contract are unchanged), and e2e `audit/*`, `hosting-*.spec.ts`, `journeys/*`, `mobile/*`.

### Wave 0 Gaps
- [ ] `tests/settings-panel.test.tsx`, `tests/hosting/settings-section.test.tsx` - D-09
- [ ] `tests/hosting/configuration-filter.test.ts` - D-10
- [ ] `tests/locales/de-glossary.test.ts` + `de-glossary-allowed.json` - IN-06
- [ ] a shared helper that seeds `utf.settings.open` for vitest and Playwright, `sessionStorage.clear()` in `tests/setup.ts`
- [ ] icon exports added to `tests/mocks/react-icons.tsx` (`Checkmark16Filled`)
- [ ] extend `server-style-guard.test.ts` file list and `legacy-ratchet.test.ts` `isGuarded` for `action-control.css`
- [ ] a small axe fixture run of the new `check-mark` structure before screens depend on it (A7)

### Baseline status
Full vitest run on the untouched tree (2026-11-26, before any Phase 70 change): **209 test files passed, 3,337 tests passed, 2 skipped, 100 s** `[VERIFIED: npm --prefix sources/ReactApp test]`. Any later failure is therefore attributable to Phase 70 work. Lint, typecheck, `locales:*` and Playwright were not run for the baseline.

## Security Domain

`security_enforcement` is enabled (absent/true); ASVS level 1. This phase is presentation-only (no API, auth, or data-model change), so the surface is small.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | unchanged |
| V3 Session Management | marginal | `sessionStorage` holds panel anchor ids only; never tokens, values or secrets |
| V4 Access Control | yes (preserve) | `SettingsPanel` for Credentials stays absent without `canManageSecrets` (no heading, no request); server record panel only for `canModify` and non-archived; disabled switches/chips must stay disabled for readers |
| V5 Input Validation | yes (preserve) | configuration value validation stays in `configurationEntryIssue`; the boolean `SwitchField` must still stage the strings `"true"`/`"false"`; server is authoritative |
| V6 Cryptography | no | none |
| V8 Data Protection | yes | one-time credential value: focus and scroll only, never URL, storage, log or toast text; review dialog must keep not showing typed secret values (`workspaceScreens.ts` `reviewReady` asserts this) |

### Known Threat Patterns
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| API-supplied text rendered as HTML (names, descriptions, choices) | Tampering | keep `ApiText` plain-text rendering in every new row, filter and summary (the filter must also render matches as text) |
| Secret disclosure through UI feedback | Information disclosure | one-time value exposed only in its row; filter never matches `sensitive`/`isRedacted` values; panel summaries show counts only |
| Backdrop click dismissing a destructive confirmation | Tampering / UX | `closeOnBackdrop` off by default, on only for leave and discard; never for dialogs with a challenge field (UI-SPEC D-13) |
| Click-through while `busy` | Tampering | backdrop handler ignores clicks while `busy` |

## Project Constraints (from CLAUDE.md / AGENTS.md)

No `CLAUDE.md` exists in the repository (checked); the project instructions are `AGENTS.md`. Directives that apply:
- Scope: `sources/ReactApp` only. Do not touch the legacy Blazor WebApp. No API/DB/worker change in this phase.
- Handwritten source: English identifiers, comments and docs; localized end-user text only in resource (locale JSON) files.
- UI component reuse: reuse the shared Fluent/primitive components; never render a raw `<input>` as a button; keep page files to composition and put repeated presentation in reusable components.
- Commits: cohesive, verified, regularly; subject `type(scope): summary` (scope e.g. `webapp`; this phase's area is the React app), under 80 characters, no Markdown, `!` only for breaking changes; end with the attribution line the session supplies. No migrations are involved.
- Subagent delegation is encouraged by `AGENTS.md` for independent work (the three Wave 2 groups are independent).
- User memory (feedback): no `<details>`, ugly select or misaligned review dialog; automate Playwright e2e against the mocked suite first; real-environment and seeded-stack re-checks only at milestone end, using `Deferred follow-up:` skips; UAT checkpoints must include copy-paste start/stop commands and practical steps.

## Sources

### Primary (HIGH confidence - read or executed this session)
- `70-CONTEXT.md`, `70-UI-SPEC.md` (596 lines, fully read), `.planning/config.json`, `.planning/STATE.md`
- `sources/ReactApp`: `package.json`, `vitest.config.ts`, `playwright.config.ts`, `src/components/{FormFields,Disclosure,ActionControl,primitives,SearchField,FilterChipGroup,useDrawerFocus}.ts(x)`, `src/components/server/{ConfigurationEntryField,ChangeDiffItem,ReviewChangesDialog,ModPackPanel}.tsx`, `src/components/server/{form-fields,change,sections,credentials}.css`, `src/components/{shared-primitives,commandCenter}.css`, `src/styles/tokens.css`, `src/styles.css` (button rules 1748-1850, `:root` 1250-1290), `src/features/hosting/{sections/SettingsSection,ServerConfigurationPanel,AutomationPanel,ServerFilesPanel,hosting.css}`, `src/features/admin/siteImages.ts`, `tests/{setup.ts,form-fields.test.tsx,styles/*,helpers/cssGuard.ts,ratchets/*,mocks/react-icons.tsx,e2e/audit/dialog-geometry.spec.ts,e2e/support/{a11y.ts,screens/*}}`, `scripts/check-locales.mjs`
- `sources/GameServerImplementations/{Arma3,Aska,CounterStrike2}ConfigurationImplementation.cs` (definition counts)
- Local probes: jsdom 28 selector support; lightningcss 1.33.0 transform of `base-select`/`::picker(select)`/`:has`/`forced-colors` CSS; contrast math from the repo tokens; render benchmark (temporary test file, removed); vitest subset run (171 passed, 1 skipped)

### Secondary (MEDIUM confidence)
- WebSearch: customizable select support - `https://developer.chrome.com/blog/a-customizable-select` (Chrome/Edge 135+), MDN "Customizable select elements" `https://developer.mozilla.org/en-US/docs/Learn_web_development/Extensions/Forms/Customizable_select`, caniuse `appearance: base-select` `https://caniuse.com/mdn-css_properties_appearance_base-select`

### Tertiary (LOW confidence)
- axe rule behavior for `aria-hidden-focus` and `target-size`, WCAG inactive-component exemption, and ARIA-in-HTML guidance on `aria-checked` with `role=switch` on a native checkbox: from training knowledge, tagged `[ASSUMED]` (A1, A6, A7) and to be confirmed by a Wave 0 axe fixture run.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no change, versions read from `package.json`
- Architecture: HIGH - panels, dialog, tokens, guards read from source; the spec conflicts are verified against code
- Pitfalls: HIGH for gate mechanics (read), MEDIUM for AT/axe behavior (assumed, confirmable in Wave 0)

**Research date:** 2026-11-26
**Valid until:** 2026-12-26 (code-local findings; invalidated by any change to `FormFields.tsx`, the guards, or the Settings section before planning)
