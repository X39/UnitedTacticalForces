---
phase: 69-consistency-accessibility-verification
plan: 08
subsystem: ui
tags: [react, shell, accessibility, select-field, action-button, state-views, ratchet, localization]

requires:
  - phase: 69-03
    provides: "StateViews, the Login_Dismiss key and the closed navigation labels the shell now uses"
  - phase: 69-04
    provides: "family.shell.json ratchet and the writer that lowers it"
  - phase: 69-06
    provides: "a11y audit, screen registry and the discovery findings assigned to the shell"
provides:
  - "Community layout, command center layout and profile menu on shared controls only (SelectField, ActionButton, ActionLink)"
  - "src/components/shellLayout.css: the shell-only rules the shared controls need (hidden toggles, scrims, compact language switch, sign-in button grid)"
  - "Arrow key movement through the profile menu rows, Escape returning focus to the avatar, drawer and menu Escape returning focus to the toggle"
  - "Sign-in page and command overview on PageHeader, PageStack, LoadingState and ErrorState"
  - "tests/shell-layouts.test.tsx (22 tests) and extended profile menu and overview tests"
affects: [69-09, 69-24, 69-33]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: 4f0c3a2cef0ebcaebb75e234c5cc019b9106e8a9
commits: 3

actuals:
  tokens: 17600
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "A shared control that carries the global .button rule gets a two-class selector in shellLayout.css instead of an edit to styles.css"
    - "One sr-only SelectField label plus a .shell-language wrapper makes the language switch a labelled shared select"
    - "Layouts return focus to their menu toggle on Escape through an onKeyDown handler beside useResponsiveNavigation"

key-files:
  created:
    - sources/ReactApp/src/components/shellLayout.css
    - sources/ReactApp/tests/shell-layouts.test.tsx
    - .planning/phases/69-consistency-accessibility-verification/deferred-items-69-08.md
  modified:
    - sources/ReactApp/src/components/CommunityLayout.tsx
    - sources/ReactApp/src/components/CommandCenterLayout.tsx
    - sources/ReactApp/src/components/ProfileMenu.tsx
    - sources/ReactApp/src/components/ProfileMenu.css
    - sources/ReactApp/src/components/commandCenter.css
    - sources/ReactApp/src/pages/AuthenticationPage.tsx
    - sources/ReactApp/src/pages/CommandOverviewPage.tsx
    - sources/ReactApp/tests/ratchets/family.shell.json
    - sources/ReactApp/tests/profile-menu.test.tsx
    - sources/ReactApp/tests/command-overview.test.ts
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/e2e/support/screens/fleetScreens.ts

key-decisions:
  - "The community sign-in dialog is the one raw control left in the shell (family.shell.json, reason 'layout sign-in dialog (UI-SPEC Control convergence allowance)'): ConfirmDialog needs a confirm action and the sign-in chooser has none."
  - "Menu toggles and scrims became ActionButtons too, not only the buttons the plan required; shellLayout.css restores their layout with two-class selectors."
  - "The footer brand name is built from the three Shared_CommunityBrand_NamePart keys, so no new locale key was needed."
  - "Command center loading and forbidden early returns now render a PageHeader h1 (Command Center) above LoadingState and ForbiddenState, so those states have exactly one h1."
  - "Footer link 'Command Center' in the profile menu uses the gold variant because .button--primary only has a style inside the command shell."

patterns-established:
  - "Escape closes the menu through useResponsiveNavigation; the layout moves focus to the toggle if it was inside the menu"
  - "ProfileMenu rules are prefixed with .account-profile so they win over the global .button rule that the shared control carries"

requirements-completed: [CON-01, CON-02, CON-03, L10N-03]

coverage:
  - id: D1
    description: "Both layouts and the profile menu render only shared controls; the language switch is a labelled SelectField with the padded chevron"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx#renders every button of the layout as a shared action control (both layouts)"
        status: pass
      - kind: unit
        ref: "tests/profile-menu.test.tsx#renders every control of the open menu as a shared action control"
        status: pass
      - kind: other
        ref: "tests/ratchets/ratchet.test.ts (family.shell.json holds one control, the sign-in dialog)"
        status: pass
    human_judgment: false
  - id: D2
    description: "No hard-coded English in the shell; the German layout shows German text including the closed sign-in dialog"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx#shows no English text in German, including the closed sign-in dialog"
        status: pass
      - kind: unit
        ref: "tests/locales/hard-coded-text.test.ts"
        status: pass
    human_judgment: false
  - id: D3
    description: "Skip links stay the first tab stop and target #main-content and #command-content; the active navigation item carries aria-current page; Escape keeps focus on the toggle"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx (skip link, aria-current and Escape tests for both layouts)"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/audit/a11y-audit.spec.ts S-02 keyboard walk in chromium, mobile-390 and mobile-320, en and de, with XPF-06-01 removed"
        status: pass
    human_judgment: false
  - id: D4
    description: "German navigation labels wrap in the drawer with no ellipsis"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx#wraps navigation labels in the drawer instead of truncating them (computed style)"
        status: pass
    human_judgment: true
    rationale: "Wrapping is asserted through computed style in jsdom; the visual result in the 320px drawer with real German labels was not looked at."
  - id: D5
    description: "Profile menu keeps Arrow and Tab movement and Escape returning focus to the avatar"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/profile-menu.test.tsx (Arrow keys and Tab wrap; Escape closes and focuses the avatar)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Sign-in page and command overview render one PageHeader h1 above PageStack bodies and shared states; the provider buttons call auth.login with the safe local return path"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/shell-layouts.test.tsx (authentication page, 6 tests including foreign return URL fallback)"
        status: pass
      - kind: unit
        ref: "tests/command-overview.test.ts (command overview page, 4 tests including retry and stale snapshot)"
        status: pass
    human_judgment: false

duration: ~40min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 08: Shell Convergence Summary

**The community layout, command center layout, profile menu, sign-in page and command overview now use only shared controls, shared states and locale keys, with the language switch as a SelectField, Arrow and Escape keyboard contracts on the menus, wrapping navigation labels, and a shell ratchet that holds one control and one bootstrap literal.**

## Performance

- **Duration:** about 40 min (start time was not captured; estimated)
- **Completed:** 2026-11-25
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files:** 3 created (one is the deferred-items note), 12 modified

## Accomplishments

- `CommunityLayout`: language switch is a `SelectField` with an sr-only label, the menu toggle, the scrim, the classic switch and both identity buttons are `ActionButton`s, the footer brand name comes from the three brand keys, and the sign-in dismiss button reads `Shared_CommunityDialog_Login_Dismiss`. Escape returns focus to the toggle. The `<dialog>` is the only raw control left.
- `CommandCenterLayout`: `SelectField` language switch, `ActionButton` drawer toggles and scrim, `aria-expanded` and `aria-controls` on the opener, Escape returning focus to the opener, and `PageHeader` plus `LoadingState` and `ForbiddenState` in the access early returns (one h1 each).
- `ProfileMenu`: trigger, scrim, close, and sign out are `ActionButton`s, the two footer links are `ActionLink`s, the literal `&times;` became an icon, Arrow Up and Arrow Down walk and wrap the rows like Tab, and an explicit gold focus ring replaces the default outline on the trigger. The dead `.account-profile__button*` rules were removed.
- `commandCenter.css`: focus rings use `var(--gold-bright)` (the translucent ring was the discovery finding on nav links, primary buttons and work areas), navigation labels wrap (`white-space: normal`, no ellipsis), and the XPF-06-01 known finding was removed from `fleetScreens.ts` with its fix.
- `AuthenticationPage` and `CommandOverviewPage`: `PageHeader` first, `PageStack` bodies, `LoadingState`, `ErrorState` with a real refetch, and stale rows stay visible under a failed refetch.
- Ratchets: `family.shell.json` went from 5 files (17 controls, 3 literals) to 2 files (1 control, 1 literal); `ProfileMenu.css` lost two hex colors and two off-grid pixel values; `shellLayout.css` is a new stylesheet with zero of each.

## Task Commits

1. **Task 1 (tracer): community layout on shared controls and keys** - `c9fd2d54` (refactor)
2. **Task 2: command center layout, navigation and profile menu** - `e6891feb` (refactor)
3. **Task 3: sign-in and overview pages on the shared header and states** - `4f0c3a2c` (refactor)

Tracer gate: the tracer `<verify>` is automated only; it passed (`shell-layouts`, ratchets, hard-coded-text, plus lint and typecheck) before expansion. Commits were created with the standard hooks.

## Verification

- `npm test -- tests/command-overview.test.ts tests/shell-layouts.test.tsx tests/profile-menu.test.tsx tests/ratchets tests/locales tests/styles tests/locale-recording.test.tsx`: 15 files, 312 passed, 1 skipped (the opt-in ratchet writer).
- `npm run lint`, `npm run typecheck`, `npm run locales:check` (3980 keys, 0 failures, 0 warnings): all exit 0.
- Playwright on a private port (a temporary config, deleted afterwards): `public-shell.spec.ts`, `audit/a11y-audit.spec.ts` (S-02 in chromium, mobile-390 and mobile-320, en and de, with the known finding removed), `hosting-a11y.spec.ts`: all pass. `hosting-fleet.spec.ts`: 1 failure, see Issues Encountered. 75 passed, 1 failed.
- Acceptance greps: AuthenticationPage raw controls 0, CommandOverviewPage `PageStack` 3, CommunityLayout `SelectField` 2, ProfileMenu raw controls 0, CommandCenterLayout `<select` 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] New stylesheet `shellLayout.css` and a legacy-ratchet entry**
- **Found during:** Task 1
- **Issue:** The shared controls carry the global `.button` and `.form-field` rules, which break the shell-only layout (the menu toggle would show on desktop, the scrim would not cover the viewport, the language select would pick up the legacy padding that overlaps the chevron). `styles.css` is off limits and no existing sheet is loaded by both layouts.
- **Fix:** Added `src/components/shellLayout.css` (tokens and rem only) and its entry in `tests/styles/legacy-ratchet.json`, because the legacy ratchet test requires every stylesheet outside the guarded folders to be listed.
- **Files modified:** `shellLayout.css`, `tests/styles/legacy-ratchet.json` (also lowered the `ProfileMenu.css` ceilings to the measured 7 and 32)
- **Commit:** `c9fd2d54`, `e6891feb`

**2. [Rule 2 - Missing critical] Escape returns focus to the toggle; Arrow keys in the profile menu**
- **Found during:** Tasks 1 and 2
- **Issue:** The plan's behavior lists name Escape returning focus to the toggle and Arrow movement through the profile rows. The code only closed the menus on Escape and trapped Tab.
- **Fix:** `onKeyDown` handlers on the header and the command shell, and an Arrow branch in the profile menu focus handler.
- **Commit:** `c9fd2d54`, `e6891feb`

**3. [Rule 1 - Bug] Controls whose literal text counted as hard-coded text**
- **Found during:** Task 2
- **Issue:** `&times;` in `ProfileMenu` is raw JSX text with letters, so the literal scan counted it, and the footer carried the English brand name.
- **Fix:** Icon for the close button, brand name joined from the three existing `Shared_CommunityBrand_NamePart` keys.
- **Commit:** `c9fd2d54`, `e6891feb`

**4. [Scope] Files outside `files_modified`**
- `src/components/shellLayout.css` (new), `tests/styles/legacy-ratchet.json` (see 1), and `tests/e2e/support/screens/fleetScreens.ts` (the 69-06 summary asks 69-08 to remove the XPF-06-01 entry together with its fix). `shell-layouts.test.tsx` was first committed without its authentication section and completed in the third commit so every commit stays green; `commandCenter.css` and `shellLayout.css` carry two small rules (`.command-overview-page` removal and the sign-in button grid) that belong to Task 3 but landed in the Task 2 commit.

**Total deviations:** 3 auto-fixed (1 Rule 3, 1 Rule 2, 1 Rule 1) plus the scope note. **Impact:** additive; no behavior was removed from the shell.

## Issues Encountered

- `tests/e2e/hosting-fleet.spec.ts:558` fails: it expects the page title to match `/Servers/` while the layout title is "Server fleet". The title comes from `Shared_CommandCenter_CurrentArea_ServerFleet`, which this plan did not touch. Logged in `deferred-items-69-08.md`.
- The sign-in page's "no login method" state has no title and body key pair, so it stays a `<p role="status">` (cross-plan finding, same file).
- jsdom applies the real stylesheets (`css: true`), so a `display: none` toggle is hidden from role queries; the tests query the toggle by label text and stub `matchMedia`.
- The worktree had no `node_modules`; a directory junction to the main checkout's install was used (git-ignored, nothing installed). `npm run build:assets` regenerated `src/generated/api.ts` with a line-ending diff; it was restored and never committed.
- The discovery pass (`UTF_A11Y_DISCOVERY=1`) was not re-run; the shell rows of its findings table are covered by the S-02 keyboard walk passing in three projects and by the focus ring and target rules now in `commandCenter.css` and `ProfileMenu.css`.

## Known Stubs

None.

## Threat Flags

None. T-69-12 is mitigated: both provider buttons still call `auth.login(provider, returnUrl)` with the value from `localReactReturnPath`, and tests assert the arguments for a local path and the fallback for a foreign origin.

## Next Phase Readiness

The shell is on shared controls; Wave 2 family plans that render inside the layouts can rely on `shellLayout.css` for toggle, scrim and language switch layout. 69-33 can retire `Shared_CommunityDialog_Cancel` and the unused legacy rules listed in the deferred items.

## Self-Check: PASSED

- Created files exist: `shellLayout.css`, `shell-layouts.test.tsx`, `deferred-items-69-08.md`.
- Commits `c9fd2d54`, `e6891feb`, `4f0c3a2c` are ancestors of HEAD.
- Plan verification re-run: vitest set (312 passed), lint, typecheck, locales check, Playwright (75 passed, 1 pre-existing unrelated failure).
