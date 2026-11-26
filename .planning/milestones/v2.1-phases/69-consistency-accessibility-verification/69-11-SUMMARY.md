---
phase: 69-consistency-accessibility-verification
plan: 11
subsystem: ui
tags: [react, hosting, configuration-profiles, shared-controls, state-views, disclosure, ratchet, accessibility]

requires:
  - phase: 69-03
    provides: "StateViews, the Hosting_ConfigurationProfiles_Archive_Dismiss key and the closed tone lexicon"
  - phase: 69-04
    provides: "family.hosting-profiles.json ratchet and the UTF_RATCHET_UPDATE writer"
  - phase: 69-06
    provides: "Discovery finding: configuration profile workspace had no visible h1"
provides:
  - "All six configuration profile files free of raw controls and button look-alikes; family.hosting-profiles.json is empty"
  - "Profile library, create wizard and workspace on PageHeader, PageStack and the shared state views"
  - "Revision snapshots on the shared Disclosure; access matrix on shared checkbox, radio and text fields"
  - "Profile workspace sections as a link-based SectionNavigation with aria-current"
affects: [69-33]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: 2ab6f0b403dfe3659a84d893593964789f551713
commits: 3

actuals:
  tokens: 22000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Early-return states render the same PageHeader above LoadingState, ForbiddenState, ErrorState or EmptyState"
    - "Section switching through SectionNavigation links to ?tab=, not role=tab buttons"
    - "Configuration definitions render through TextField, TextAreaField, SelectField and CheckboxField with hint, error and required wiring"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/ConfigurationProfilesPage.tsx
    - sources/ReactApp/src/features/hosting/ConfigurationProfileCreatePage.tsx
    - sources/ReactApp/src/features/hosting/ConfigurationProfileWorkspacePage.tsx
    - sources/ReactApp/src/features/hosting/ConfigurationProfileFields.tsx
    - sources/ReactApp/src/features/hosting/ConfigurationProfileAccessSection.tsx
    - sources/ReactApp/src/features/hosting/ConfigurationProfileRevisionHistory.tsx
    - sources/ReactApp/tests/hosting/configuration-profiles.test.tsx
    - sources/ReactApp/tests/ratchets/family.hosting-profiles.json

key-decisions:
  - "Workspace sections moved from role=tab buttons to SectionNavigation links; the plan's own note that the test asserts a tab by role signalled this change, and links keep the section URL shareable."
  - "Forward actions use the gold variant (wizard Next, Review and Create, savebar Publish, Save access); at most one is on screen per view."
  - "Archived profile badge tone is neutral (UI-SPEC lexicon) instead of warning."

patterns-established:
  - "Contribution (game-specific) fields keep a label heading and their own description and error wiring because they are rendered by registry components outside this plan"

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "Library, create and workspace pages render one PageHeader h1 above loading, forbidden, error, not-found and loaded states"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/hosting/configuration-profiles.test.tsx#profile workspace header, states and dismiss label"
        status: pass
    human_judgment: false
  - id: D2
    description: "No raw control or button look-alike remains in the six profile files; revision snapshots use Disclosure and the access matrix uses shared fields"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts (family.hosting-profiles.json has zero files)"
        status: pass
      - kind: unit
        ref: "tests/hosting/configuration-profiles.test.tsx#revision snapshots on the shared disclosure"
        status: pass
    human_judgment: false
  - id: D3
    description: "The archive dialog dismiss button names its effect in English and German"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/hosting/configuration-profiles.test.tsx#names the effect of the archive dialog dismiss button in German"
        status: pass
    human_judgment: false
  - id: D4
    description: "Library, create and workspace states use the shared state views; the filtered-empty library names and performs how to clear the filter; the access matrix save payload is unchanged"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/hosting/configuration-profiles.test.tsx#library, create form and access matrix on shared fields"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual rhythm of the converted profile pages (header, spacing, savebar placement) matches the other converted hosting pages"
    requirement: CON-01
    verification: []
    human_judgment: true
    rationale: "Pixel baselines for S-11 and S-12 belong to the Wave 3 slice plans; no visual snapshot was taken here"

duration: ~55min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 11: Configuration Profile Pages on Shared Header, States and Controls Summary

**The profile library, create wizard and workspace now use PageHeader, PageStack, the shared state views, shared fields and buttons, a link-based section navigation and Disclosure, and the profiles ratchet family is empty.**

## Performance

- **Duration:** about 55 min (start time was not captured; estimated)
- **Completed:** 2026-11-25
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files modified:** 8 (6 source, 1 test, 1 ratchet)

## Accomplishments

- **Workspace (tracer):** the hand-written `h1` is gone. Loading, forbidden, error and not-found states render `PageHeader` ("Configuration profiles") above `LoadingState`, `ForbiddenState`, `ErrorState` (retry calls the real `refetch`) or `EmptyState`. The loaded state carries the profile title, game kicker, revision description, status badge and the danger Archive action in the header. Sections are a `SectionNavigation` with `aria-current="page"`. The archive dialog dismiss label is `Hosting_ConfigurationProfiles_Archive_Dismiss` ("Keep profile active" / "Profil aktiv lassen").
- **Library and create:** `SearchField`, two `SelectField`s, `ActionLink` and `ActionButton` replace the raw controls. A filter with no match shows `EmptyState` with a "Clear search and filter" action that resets search, game and status. The wizard stepper is `ActionButton`s with `aria-current="step"`; the create form uses `TextField` and `SelectField` with hint, required marker and the original validation message wiring.
- **Fields:** every configuration definition renders through `TextField`, `TextAreaField`, `SelectField` or `CheckboxField` (the indeterminate Boolean keeps its `ref`, `aria-checked="mixed"` and tri-state payload). Game-specific contribution fields keep their own label and description wiring.
- **Access matrix:** `RadioGroupField` for the subject, `TextField` combobox with the unchanged keyboard handling, `CheckboxField` per permission cell (visually hidden label "Name: Permission"), `ActionButton`s for the option list, Remove ("Remove: Name") and the savebar. The save payload is asserted unchanged (T-69-16).
- **Revisions:** each snapshot expands through `Disclosure` with per-entry state, by click and by Enter.
- **Ratchet:** `family.hosting-profiles.json` lowered to an empty `files` object with the writer.

## Task Commits

1. **Task 1 (tracer): profile workspace on the shared header, states and named dismiss label** - `2087b3ae` (refactor)
2. **Task 2: library and create pages, fields and access matrix on shared controls** - `288e8bb7` (refactor)
3. **Task 3: revision snapshots on Disclosure and the profiles family at zero** - `2ab6f0b4` (refactor)

Tracer gate: the automated-only verify (`configuration-profiles.test.tsx`, `tests/ratchets`) re-ran green before expansion; tracer verified end-to-end, expansion continued.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Workspace tabs converted to SectionNavigation links**
- **Found during:** Task 1
- **Issue:** The three `role="tab"` buttons were raw `button` elements counted by the ratchet; a shared tab widget does not exist, and `SectionNavigation` (a nav of router links, not a tab widget) is the shared section control used by the server workspace.
- **Fix:** `SectionNavigation` with `to={{ search: "?tab=<id>" }}`; the Access section stays hidden without `canManageAccess` and the deep-link fallback is unchanged. The test that asserted a tab by role (plan line 377 reference) now asserts the "Configuration" link with `aria-current="page"`.
- **Files modified:** `ConfigurationProfileWorkspacePage.tsx`, `tests/hosting/configuration-profiles.test.tsx`
- **Commit:** `2087b3ae`

**2. [Rule 1 - Bug] Test for the not-found state needed a reachable route**
- **Found during:** Task 1
- **Issue:** React Query rejects `undefined` query data, so a mocked `profile()` returning undefined produces the error state, not not-found. The real not-found state is reached when the route has no `:profileId`.
- **Fix:** the test registers the page on a route without the param.
- **Commit:** `2087b3ae`

**3. [Rule 3 - Blocking] Missing profile-specific keys reuse existing generic keys**
- The 69-03 key map has no profile library keys for "Clear search" and "Clear search and filter". Locale files are off limits, so the page reuses `Server_Fleet_SearchClear` and `Server_Fleet_NoMatch_Clear`, whose English and German text fit exactly. 69-33's unused-key pass should treat them as shared, not server-only.
- **Commit:** `288e8bb7`

### Behavior changes inside the plan's intent

- Forward actions became `gold` (UI-SPEC accent rule): wizard Next, Review profile, Create profile, savebar Publish revision, Save access.
- The archived badge tone changed from warning to neutral to match the UI-SPEC lexicon.
- The visible "Required" word beside a field became the shared asterisk marker plus `required` and `aria-required`; the "Sensitive" word moved into the hint.
- The choice select no longer carries a per-option `title` tooltip (`SelectOption` has no such field); the option label and the field hint still carry the information.
- The operation-failed panel of the create wizard is now an `ErrorState` whose retry calls `create.mutate()`.
- Tests now query required fields with a `^Label` regex because the shared label includes the decorative asterisk.

**Total deviations:** 3 auto-fixed (2 Rule 3, 1 Rule 1) plus the listed behavior changes. **Impact:** none on payloads or permissions.

## Authentication Gates

None.

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (untracked, git-ignored). Nothing was installed and the lockfile is untouched.
- The machine was heavily loaded by sibling executors: the default 5 s vitest timeout tripped for the ratchet tree scan and the large workspace-page tests, and one metrics performance backstop (a 200 ms budget) measured 406 ms. The same run with `--testTimeout=60000 --maxWorkers=2` passed completely. None of the affected files belong to this plan.
- Game-specific contribution fields (`implementations/*`) were not touched; their label wiring is preserved by keeping the legacy label heading for that path.

## Verification

- `npx vitest run tests/hosting tests/ratchets tests/locales --testTimeout=60000 --maxWorkers=2`: 117 files, 2245 passed, 1 skipped (the opt-in ratchet writer).
- `npx eslint` on the six source files and the test file: clean. The whole-project `npm run lint` was not run because of machine load; these files are no longer on the ESLint ratchet ignore list, so the raw-control rule applies to them in full.
- `npx tsc -b --noEmit`: clean. `node scripts/check-locales.mjs`: 3980 keys, 0 failures, 0 warnings.
- Acceptance greps: raw controls in `ConfigurationProfileAccessSection.tsx` 0 and `ConfigurationProfileCreatePage.tsx` 0; `<details` in the revision history 0; `<h1` and `Hosting_Common_Cancel` in the workspace page 0; `Object.keys(family.hosting-profiles.json files).length` 0.

## Known Stubs

None.

## Threat Flags

None. No network, auth or trust-boundary surface was added; T-69-16 (access matrix payload) is mitigated by the payload assertion in `configuration-profiles.test.tsx`.

## Next Phase Readiness

The profiles family needs no further conversion. The Wave 3 slice plans still own the S-11 and S-12 registry entries, audits and visual baselines for these pages.

## Self-Check: PASSED

- Modified files exist and commits `2087b3ae`, `288e8bb7`, `2ab6f0b4` are ancestors of HEAD.
- All five acceptance criteria greps return the expected zero.
