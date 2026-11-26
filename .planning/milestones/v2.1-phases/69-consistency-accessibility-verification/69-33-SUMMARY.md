---
phase: 69-consistency-accessibility-verification
plan: 33
subsystem: testing
tags: [locales, check-locales, unused-keys, banned-labels, cross-plan-findings, playwright, vitest, dotnet]
status: complete

requires:
  - phase: 69-26
    provides: "fleet slice and its recorded findings"
  - phase: 69-28
    provides: "adjacent slice and its five recorded known findings"
  - phase: 69-29
    provides: "route walk and its recorded route finding"
  - phase: 69-32
    provides: "seeded UAT journeys and the two recorded app findings"
provides:
  - "check-locales --unused (and --src) and the locales:unused script, wired into ci only"
  - "Server_ and Hosting_ keys without a reference in src now fail CI; other families warn"
  - "App-wide banned dismiss, confirm and button label scan over every component (D-11)"
  - "Every recorded cross-plan finding fixed or recorded below with a reason; no XPF-69 or F-69 id is left in src or tests"
  - "EmulatedHetznerRuntimeProvider no longer reports a successful provision as a closed partial graph"
affects: []

plan_head_before: 7ded42dd1d3118b8f49c5848bf7a609d7a77fcc4
plan_head_after: cb425296621984cda0f661e4ea11182c9eadf6e1
commits: 10

actuals:
  tokens: 71000
  tasks: 3
  commits: 10

tech-stack:
  added: []
  patterns:
    - "Unused-key scan: identifier-shaped string literals (looked at with lookarounds so nesting cannot misalign quotes), template literals with substitutions and quoted prefixes followed by a plus become key patterns, plural bases count for their forms"
    - "ConfirmDialog renders into document.body and stops submit propagation, so a dialog declared inside a page form still receives its own submit"
    - "Banned-label scan resolves t(...) keys of cancelLabel, confirmLabel and button children through the TypeScript AST against both locale files, with a non-vacuity sample and a Close exception test"

key-files:
  created:
    - sources/ReactApp/tests/not-found-page.test.tsx
    - sources/ReactApp/tests/styles/closed-findings.test.ts
  modified:
    - sources/ReactApp/scripts/check-locales.mjs
    - sources/ReactApp/package.json
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/tests/locales/check-locales.test.ts
    - sources/ReactApp/tests/locales/ownership.test.ts
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - sources/ReactApp/tests/locales/plural.test.ts
    - sources/ReactApp/tests/e2e/support/screens/adjacentScreens.ts
    - sources/ReactApp/tests/e2e/audit/route-walk.spec.ts
    - sources/ReactApp/src/components/primitives.tsx
    - sources/ReactApp/src/styles.css
    - sources/Api/Services/HostingRuntime/EmulatedHetznerRuntimeProvider.cs

key-decisions:
  - "The unused-key check fails only for Server_ and Hosting_ keys (the hosting UI owns them); every other family warns, because keys that contain a space or are bare words cannot be found by an identifier scan"
  - "Only structured keys (with an underscore) that the scan reports as unreferenced were deleted; the 49 remaining warnings are bare-word or sentence keys and stay"
  - "The nav label is the page and document title of a closed-list destination: the fleet reads Servers in the layout and the page, the event administration and gallery e2e specs assert the Events and Gallery h1"
  - "The default draft body of a new public page is a localized level-2 heading, so the Markdown preview no longer renders a second h1 (XPF-69-29-1 fixed in the app instead of recorded)"

requirements-completed: [L10N-03, CON-03, FND-06]

coverage:
  - id: D1
    description: "Every recorded cross-plan finding is fixed or deferred with a reason, and no known finding, known route finding or ratchet reason cites a cross-plan id"
    requirement: FND-06
    verification:
      - kind: e2e
        ref: "playwright tests/e2e/audit (613 passed, 263 skipped by design, three projects)"
        status: pass
      - kind: command
        ref: "grep -rc XPF-69 sources/ReactApp/tests sources/ReactApp/src prints 0 files"
        status: pass
    human_judgment: false
  - id: D2
    description: "Unreferenced Server_ and Hosting_ keys fail CI through locales:unused; retired keys, generic cancel keys and orphaned title keys are gone"
    requirement: L10N-03
    verification:
      - kind: test
        ref: "tests/locales/check-locales.test.ts, tests/locales/ownership.test.ts"
        status: pass
      - kind: command
        ref: "npm run locales:check and npm run locales:unused exit 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "No cancelLabel, confirmLabel or button label key anywhere in src resolves to a banned generic value"
    requirement: CON-03
    verification:
      - kind: test
        ref: "tests/locales/server-keys.test.ts banned dismiss and confirm labels app-wide"
        status: pass
    human_judgment: false
  - id: D4
    description: "The emulated provider's start no longer ends in permanent-execution-failed"
    verification:
      - kind: test
        ref: "tests/Api.Tests/GameServerHosting/HetznerRuntimeProviderTests.cs EmulatedProvider_Provision_SucceedsWithoutClosingTheProvisioningAttempt"
        status: pass
    human_judgment: true
    rationale: "The seeded create-then-start journey (69-32-1) runs against the Aspire UAT stack, which this plan did not start; a human re-run confirms the server now reaches waiting for worker"
---

# Phase 69 Plan 33: Finding closure and localization gate Summary

**Every recorded Phase 69 cross-plan finding is fixed or recorded with a reason, `check-locales --unused` fails CI on unreferenced `Server_` and `Hosting_` keys (169 dead keys removed), generic dismiss and confirm labels are banned app-wide by an AST scan, and the emulated Hetzner provider no longer tears down every successful start.**

## Tasks

| Task | Name | Commits |
|------|------|---------|
| 1 | Tracer: fix every recorded cross-plan finding and drop its known-finding entry | 6ec88fdc, 173a4888, 27ef1750, 21c3dc4e, 79e353e6, 4b95ac58, 5448a710, cb425296 |
| 2 | Unused-key check in CI and retired keys removed | d3987353 (tooling), 173a4888 (key removal) |
| 3 | Banned dismiss and confirm labels app-wide | 6a2343e8 |

The tracer verify (`playwright test tests/e2e/audit`, all three projects) ran green after the fixes: 613 passed, 263 skipped by design, 0 failed. It was re-run as part of the complete suite at the end (see Verification).

## Orchestrator notes: fixed and deferred

| # | Item | Result | How or why |
|---|------|--------|------------|
| 1 | `public-shell.spec.ts` expects h1 "Event Administration" (XPF-69-21-2) | Fixed (79e353e6) | The page title is the nav label `Shared_CommandCenter_Nav_Events` ("Events"); both assertions now look for the level 1 heading "Events" |
| 2 | `hosting-fleet.spec.ts:558` expects title /Servers/ but the layout sets "Server fleet" | Fixed (21c3dc4e) | The layout `titleKey` for `/command/servers` is now `Hosting_Nav_Servers`, the fleet page title is `Hosting_Servers_Title` ("Servers"), the dead overview view link reads the same label; `Shared_CommandCenter_CurrentArea_ServerFleet` was removed as unreferenced. Layout, page and spec agree and the order-dependence is gone because both effects set the same value |
| 3 | e2e failures `public-shell:406`, `:809`, `:1367`, `hosting-files-secrets:794` at 320x568 | Fixed | `:406` is item 1. `:809`: the gallery h1 is the nav label "Gallery" (79e353e6). `:1367`: real app defect, see Deviations (portal for ConfirmDialog, 21c3dc4e). `hosting-files-secrets:794`: the spec measured its baseline before the web font loaded and never served the vendored fonts; it now calls `stabilize` and `settle` (cb425296) |
| 4 | XPF-69-27-01 `.dialog__actions > *` loses to `.button` | Fixed (21c3dc4e) | The phone rule is `.dialog .dialog__actions > *`; the `dialog--record` class and its CSS block in `workspace-shell.css` and `SettingsSection.tsx` were removed; `tests/styles/closed-findings.test.ts` covers it |
| 5 | F-69-32-1 / XPF-25-01 `<bdi>` on header and breadcrumb | Fixed (21c3dc4e, 79e353e6) | `WorkspaceHeader` wraps the title in `bdi`; `Breadcrumbs` gained a `userContent` item flag that wraps the label in `bdi`, used for the server title; the subtracted title leak in `tests/uat/automation.spec.ts` is removed and the German pass now asserts every text. The German walk comment that cited XPF-25-01 was rewritten |
| 6 | F-69-32-2 emulated provider returns `Succeeded` with `IsProvisioningAttemptDefinitivelyClosed: true` | Confirmed and fixed (6ec88fdc) | `LifecycleJobExecutor` line 475 branches on the flag without checking success and tears the deployment down through `TriggerFaultCostStopAsync`; `HetznerRuntimeProvider` sets the flag only on a discovery failure. The emulation now leaves it at its default `false`. Test `EmulatedProvider_Provision_SucceedsWithoutClosingTheProvisioningAttempt` added. `dotnet build X39.UnitedTacticalForces.slnx` exit 0; `dotnet test` of `HetznerRuntimeProviderTests`, `UatWorkerChannelEmulatorTests` and `GameServerLifecycleServiceTests`: 115 passed |
| 7 | Missing locale keys | Fixed (173a4888, 4b95ac58) | See the key table below |
| 8 | `NotFoundPage` h1 (XPF-69-17-3, XPF-69-19-1); invisible `.em-attendance-choice__mark` (XPF-69-17-4) | Fixed (27ef1750, 21c3dc4e) | `NotFoundPage` renders its title through `PageHeader` (one h1) above the panel, with the home link in the panel action area. The mark's `background: currentColor` resolved against its own dark glyph color; it now uses an explicit `--em-choice-tone` set by each tone class |

### Item 7 in detail

| Gap | Key or change |
|-----|---------------|
| Generic search clear (XPF-69-15-2, 69-16-1) | `Common_SearchClear`; 13 callers repointed (fleet toolbar and the server files panel keep `Server_Fleet_SearchClear`) |
| File classification labels (XPF-69-28-01) | `Hosting_FileClassification_*` for every API classification; the Files panel maps the token and filters on the localized label |
| Provider resource kind (XPF-69-28-02) | `Hosting_ResourceKind_*` (server, primary IP, firewall, volume, machine) used by the cloud machine panel |
| "Delete terrain" (XPF-69-20-1) | `Pages_Data_Terrains_All_ConfirmDeleteTerrain_Confirm` ("Delete terrain" / "Karte löschen") |
| Event editor leave keys (XPF-69-16-2) | `React_EventAuthoring_Leave_Title`, `_Body`, `_Stay`, `_Leave` on the event create and edit pages |
| Minute not found body (XPF-69-22-1) | `Pages_Unit_Beschluesse_DocumentId_NotFoundDescription` is now a sentence naming the next step, in both languages |
| Title case create titles (XPF-69-20-3) | `Auto_CreateModPack_04e0630f` and `Auto_CreateTerrain_636ac171` are sentence case; `admin-asset-pages.test.tsx` and `public-shell.spec.ts` updated |
| 64 mod pack `t("English sentence")` lookups (XPF-69-20-2, deferred-items-69-03) | 64 distinct `React_ModPackEditor_*` keys in both languages; the three `t = (key) => translate(key, { defaultValue: key })` shims were replaced by the typed `useTranslation` function, so a missing key is now a type error |
| Event action failure and account sign-in titles (XPF-69-17-2) | `Pages_Events_Detail_Error_AttendanceTitle`, `_SlotTitle`, `_DownloadTitle`, `React_AccountSettings_SignInTitle` |

## Cross-plan finding table

Every id recorded in a `69-*-SUMMARY.md` or a `deferred-items*.md` file. "Commit" is the commit that closed it.

| Id | Result | Commit | Note |
|----|--------|--------|------|
| XPF-69-01-1 | Fixed | 21c3dc4e | Command center h1 size rule deleted; the shared Display token applies |
| XPF-69-01-2 | Fixed | 21c3dc4e | `.command-shell .page-header` keeps border and token padding; the 780px stack duplicate is gone |
| XPF-69-01-3 | Fixed | 21c3dc4e | Dead `.dialog__helper` and `.field` selectors of the cost dialog removed. The doubled `.dialog.dialog--cost` shell rules were kept: they still win over legacy `.dialog` rules |
| XPF-69-01-4 | Already resolved | 21c3dc4e | No outline removal is left in finance, hosting or wiki CSS; the stale ceilings (1, 1, 6) and other slack ceilings were lowered to the measured values |
| 69-12 note | Partly fixed | 173a4888 | The four unreferenced `HostingFinance_*` keys and `Server_Settings_Retry` readers were retired by the scan. A dedicated sentence per finance action needs new product copy: deferred |
| 69-14 note | Fixed | 173a4888, 21c3dc4e | Six unreferenced roles and claims keys purged; `.authority-page`, `.role-create-panel`, `.role-detail-*`, `.filter-chips` rules removed |
| XPF-69-15-1 | Fixed | 21c3dc4e | Dead member and Verein request rules removed from `styles.css` |
| XPF-69-15-2 | Fixed | 173a4888 | `Common_SearchClear` |
| XPF-69-15-3 | Fixed | 173a4888 | Unreferenced keys purged |
| XPF-69-15-4 | Kept | none | One-line edit of `public-shell.spec.ts` stays |
| XPF-69-15-5 | Already resolved | none | `CommandListToolbar` is built on `SearchField` (69-24) |
| XPF-69-16-1 | Fixed | 173a4888 | `Common_SearchClear` |
| XPF-69-16-2 | Fixed | 173a4888 | Event leave keys |
| XPF-69-16-3 | Not needed | none | A Save icon is not wanted back on the editor, so the mock stays as it is |
| XPF-69-17-1 | Fixed | 27ef1750 | `ForbiddenState` has an `action` slot; both sign-in buttons use it |
| XPF-69-17-2 | Fixed | 4b95ac58 | Titles added; the Verein ledger empty state already has `Pages_Unit_Kassenbuch_Index_NoEntriesDescription` |
| XPF-69-17-3 | Fixed | 27ef1750 | See item 8 |
| XPF-69-17-4 | Fixed | 21c3dc4e | See item 8 |
| X69-18-01 | Deferred | none | A publish confirmation for public page revisions is a product decision plus new copy |
| X69-18-02 | Deferred | none | Markdown preview headings are a closed non-goal. The route walk case that hit it (XPF-69-29-1) no longer depends on it |
| X69-18-03 | Resolved | 173a4888 | `Components_GalleryImagePicker_DialogCancel` has no reader and was purged; the picker uses outcome-specific labels |
| X69-18-04 | Fixed | 21c3dc4e | `.public-page-editor-check`, `.gdpr-editor-field` input rules and `.public-page-editor-fields` label and input rules removed |
| XPF-69-19-1 | Fixed | 27ef1750 | See item 8 |
| XPF-69-19-2 | Fixed | 21c3dc4e | `.media-gallery-*`, `.site-image-*`, `.gallery-picker-dialog*` and related blocks removed |
| XPF-69-19-3 | Fixed | 173a4888 | Keys purged |
| XPF-69-19-4 | Already resolved | none | See XPF-69-15-5 |
| XPF-69-20-1 | Fixed | 173a4888 | Confirm label "Delete terrain" |
| XPF-69-20-2 | Fixed | 173a4888 | 64 keys, typed lookups |
| XPF-69-20-3 | Fixed | 173a4888 | Sentence case |
| XPF-69-20-4 | Fixed | 173a4888 | Keys purged |
| XPF-69-21-1 | Fixed | 21c3dc4e | Dead Verein settings and document editor rules removed |
| XPF-69-21-2 | Fixed | 79e353e6 | Item 1 |
| XPF-69-22-1 | Fixed | 173a4888 | Item 7 |
| XPF-69-22-2 | Fixed | 173a4888 | Five keys purged |
| XPF-69-22-3 | Partly fixed | 21c3dc4e | Unreferenced `.governance-document`, `.filter-chips` and `.view-toggle` rules removed. The hero rules stay raw in `styles.css`; they are covered by the existing `styles.css` entry of `legacy-ratchet.json`, no separate reason was added |
| XPF-69-26-01 | Fixed | 21c3dc4e | Item 2 |
| XPF-69-26-02 | Deferred | none | `HostingOverviewPage` is listed in `hostingRouteDefinitions` and asserted by `routes.test.ts`, `screen-registry.test.ts`, `fleet.test.tsx` and `hosting-states.test.tsx`; deleting it means rewriting that integration contract, which is not a contained change |
| XPF-69-26-03 | Informational | none | No action |
| XPF-69-27-01 | Fixed | 21c3dc4e | Item 4 |
| XPF-69-28-01 | Fixed | 173a4888, 79e353e6 | Item 7; known finding removed |
| XPF-69-28-02 | Fixed | 173a4888, 79e353e6 | Item 7; known finding removed |
| XPF-69-28-03 | Already resolved | 79e353e6 | The helper already counts date and time segments as one tab stop (4c56e540); the records were removed and the adjacent audit passes |
| XPF-69-28-04 | Fixed | 21c3dc4e, 79e353e6 | Command table row ring uses `var(--utf-focus-ring)` |
| XPF-69-28-05 | Fixed | 21c3dc4e, 79e353e6 | The legacy `.public-page-editor-fields input:focus` rule is deleted; the shared field draws the ring |
| XPF-69-29-1 | Fixed | 173a4888, 79e353e6 | The default draft body is `Pages_Command_Content_PageDefaultMarkdown` ("## ..."), so the preview has no second h1; the `knownRouteFindings` entry was removed |
| F-69-32-1 | Fixed | 21c3dc4e, 79e353e6 | Item 5 |
| F-69-32-2 | Fixed | 6ec88fdc | Item 6 |
| deferred 69-03 | Fixed | 173a4888 | Mod pack lookups; the dynamic `kind` StatePanels were converted by the family plans |
| deferred 69-08 #2 | Deferred | none | The sign-in "no login method" state needs a new empty title and body copy pair |
| deferred 69-08 #3, #4 | Fixed | 173a4888, 21c3dc4e | Key purged; `.language-select`, `.command-overview-page`, `.account-profile__button*` rules removed |
| deferred 69-23 | Fixed / non-goal | 79e353e6 | Heading assertion; unreferenced keys purged. A wiki article that starts with `# Heading` still renders a second h1 (closed non-goal) |
| deferred 69-24 | Fixed / by design | 21c3dc4e | Fleet title fixed; the profile menu ring and the editor h1 no longer fail the audit (613 passed with no known findings); the column-reverse dialog action order is D-10 and stays |
| deferred 69-30 | Fixed | see items 1 to 3 | All five failures are green in the full suite |
| deferred 69-05 | Fixed / flake | 21c3dc4e | Fleet title; `hosting-a11y.spec.ts:215` passed in every run of this plan |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A ConfirmDialog nested in a page form never confirmed**
- **Found during:** Task 1 (`public-shell.spec.ts` terrain create, deferred by 69-30)
- **Issue:** `TerrainImageField` sits inside the terrain wizard form, so the picker's own `form method="dialog"` was nested in it. In Chromium the click on Apply fired the browser submit event, but neither the dialog's nor the page form's React `onSubmit` ran (verified with instrumentation and a synthetic submit event), so the picker never closed and covered the Next button.
- **Fix:** `ConfirmDialog` renders through `createPortal` into `document.body` and stops submit propagation, so a dialog never nests in a page form and never also submits it. `tests/primitives.test.tsx` gained a regression test; the helper of that file and three `member-pages` tests now search the body.
- **Files modified:** `src/components/primitives.tsx`, `tests/primitives.test.tsx`, `tests/events-member/member-pages.test.tsx`
- **Commit:** 21c3dc4e

**2. [Rule 2 - Missing critical functionality] The unused-key check needs to understand run-time keys**
- **Found during:** Task 2
- **Issue:** A scan for literal quoted keys flagged keys that are used (`t(\`Server_Action_${id}_Label\`)`, keys inside nested template strings).
- **Fix:** The scan looks at identifier-shaped literals with lookarounds, derives patterns from template literals and `"Prefix_" + x`, and counts a plural base for its forms. Tests cover each reference form and the non-vacuity case.
- **Commit:** d3987353

**3. [Rule 3 - Blocking] Locale test expectations referenced removed keys**
- **Issue:** `server-keys.test.ts` required lists named five removed keys and `plural.test.ts` used the removed `Server_Fleet_PendingJobs`.
- **Fix:** The lists drop the keys; the plural test uses the live `Server_Progress_MoreQueued`.
- **Commit:** 173a4888

## Not deleted on purpose

- `Hosting_Servers_LoadError` is still read by `pages/CommandOverviewPage.tsx` (the scan found it), so it stays although the plan expected it to be orphaned.
- `Server_Settings_AutomationTitle` and `Server_Overview_MetricsTitle` stay (D-14).
- 49 `[unused-warn]` keys remain: sentence or bare-word keys that an identifier scan cannot judge. They only warn.

## Known Stubs

None.

## Threat Flags

None. T-69-40 is mitigated: no XPF-69 or F-69 id remains anywhere in `src` or `tests` (`grep -rc XPF-69` lists no file).

## Verification

Run in `sources/ReactApp` unless noted, on the worktree at the end of the plan:

- `npx tsc -b`: exit 0.
- `npm run lint`: exit 0 (`--max-warnings 0`).
- `npm run locales:check`: 3900 keys, 0 failures, 0 warnings. `npm run locales:unused`: 0 failures, 49 warnings.
- `npx vitest run`: 207 files, 3255 passed, 1 skipped (the opt-in ratchet writer).
- Playwright on a private-port copy of the config (deleted before committing): `tests/e2e/audit` 613 passed, 263 skipped by design, 0 failed. The complete suite (chromium, mobile-390, mobile-320) ran 839 passed, 365 skipped, 2 failed (the credential staging layout at 320x568); after the font fix (cb425296) that spec passes 12 of 12 over three repeats.
- `dotnet build X39.UnitedTacticalForces.slnx`: exit 0. `dotnet test tests/Api.Tests` with the hosting provider, UAT worker emulator and lifecycle filters: 115 passed.
- Acceptance greps: `locales:unused` in package.json 1; `Common_Cancel` in en.json 0; the three retired keys in de.json 0; `cancelLabel`, `confirmLabel`, `Schließen` in `server-keys.test.ts` 7, 9, 5; `XPF-69` files 0.

## Issues Encountered

- `node_modules` was absent; a directory junction to the main checkout's install was created (git-ignored, nothing installed, lockfile untouched).
- `npm run build:assets` (run by the Playwright web server) rewrote `src/generated/api.ts` with line endings; it was restored with `git checkout --` and never committed.
- The seeded UAT journey `69-32-1` needs the Aspire UAT stack, which this plan did not start. The provider fix is covered by a unit test; a human re-run of `./scripts/Start-Uat.ps1 -Fresh` then `npm run test:uat` confirms the create-then-start journey.
- Files deleted by this plan: none (dead CSS rules, locale keys and a CSS block were removed from existing files).

## Self-Check: PASSED
