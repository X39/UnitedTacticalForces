---
phase: 69-consistency-accessibility-verification
plan: 15
subsystem: ui
tags: [react, shared-controls, state-views, badge-tones, dismiss-labels, filter-chips, vitest]

requires:
  - phase: 69-consistency-accessibility-verification
    provides: "69-01 PageStack and shared primitives, 69-02 shared form fields, 69-03 state views, tone lexicon and dismiss keys, 69-04 family ratchet"
provides:
  - "Members list, member detail and membership requests on ActionButton, ActionLink, shared fields, FilterChipGroup, CommandListToolbar and the shared state views"
  - "Account, membership and membership request badge tones resolved only through toneOf"
  - "Outcome-specific dismiss labels in the three member detail flows, English and German"
  - "family.admin-members.json at zero files; members.css with token-only layout rules"
affects: [69-24, 69-33, 69-31]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: 2b92111fd82815ce6105c56556a04a2944e41c1b
commits: 5

actuals:
  tokens: 11900
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Page root is a PageStack with the PageHeader as its first child; every early return renders the same PageHeader above the state view"
    - "Read-only summary tiles are Metric blocks; the FilterChipGroup in the toolbar owns the filtering"
    - "A failed load with no data shows only the ErrorState; the list or queue is not rendered as empty"

key-files:
  created:
    - sources/ReactApp/src/features/admin/members.css
    - sources/ReactApp/tests/admin-member-pages.test.tsx
  modified:
    - sources/ReactApp/src/features/admin/MembersPage.tsx
    - sources/ReactApp/src/features/admin/MemberDetailPage.tsx
    - sources/ReactApp/src/features/admin/VereinMembershipRequestsPage.tsx
    - sources/ReactApp/tests/admin-members.test.ts
    - sources/ReactApp/tests/admin-membership-requests.test.tsx
    - sources/ReactApp/tests/ratchets/family.admin-members.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json
    - sources/ReactApp/tests/e2e/public-shell.spec.ts

key-decisions:
  - "Unverified accounts render neutral, not warning: the 69-03 lexicon (accountTones) is the binding source, so the plan's behavior line 'unverified warning' is superseded."
  - "The five member summary tiles and the three request summary tiles became read-only Metric blocks. They duplicated the filter chips; keeping them as buttons would have needed a second tile-button control."
  - "The legacy form classes (.member-detail-form, .verein-membership-request-form) were not reused: their element selectors (input, select, textarea, label) would restyle the shared fields and break the padded select chevron. New token-only classes live in members.css."
  - "The SearchField in the member assignment panels reuses Server_Fleet_SearchClear for its clear control because no generic key exists (XPF-69-15-2)."

patterns-established:
  - "Member pages: PageStack root, shared state views, toneOf lookups, no raw controls"

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "Members list, member detail and membership requests take every badge tone from statusTones; the page-local tone functions and table are gone"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/admin-member-pages.test.tsx#takes every badge tone from the account lexicon"
        status: pass
      - kind: unit
        ref: "tests/admin-member-pages.test.tsx#takes account and membership tones from the lexicon"
        status: pass
      - kind: unit
        ref: "tests/admin-membership-requests.test.tsx#takes every request status tone from the membership request map"
        status: pass
    human_judgment: false
  - id: D2
    description: "The three generic cancel uses in member detail are replaced by outcome-specific dismiss labels in English and German"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/admin-member-pages.test.tsx#names every dismiss in en and never reads Cancel (en and de)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The three pages contain no raw control and no button look-alike; the members list uses the shared toolbar, search and filter chips; the family ratchet is empty"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts (family.admin-members.json has no files)"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run lint (eslint no-restricted-syntax, no output)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Each page renders one h1 above loading, forbidden, not-found and error states; Members and Membership requests h1 equal their navigation labels"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/admin-member-pages.test.tsx and tests/admin-membership-requests.test.tsx (one h1 per state)"
        status: pass
      - kind: e2e
        ref: "playwright tests/e2e/public-shell.spec.ts -g 'member roster|membership request' --project=chromium (2 passed)"
        status: pass
    human_judgment: false
  - id: D5
    description: "How the converted members list, detail and request screens look at desktop and phone widths (tile styling, form grid, chip wrapping)"
    verification: []
    human_judgment: true
    rationale: "jsdom has no layout; the K-15 baseline of the members list in 69-31 and the Wave 3 audits are the first to measure geometry."

duration: 75min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 15: Member Administration Convergence Summary

**The members list, member detail and membership requests now use only shared controls, shared state views and lexicon tones, with outcome-named dismiss labels, one h1 per state and an empty members ratchet.**

## Performance

- **Duration:** about 75 min (start time was not captured; estimated)
- **Tasks:** 3 (1 tracer, 2 auto), 5 commits
- **Files:** 2 created, 8 modified

## Accomplishments

- **MembersPage (tracer):** buttons and links are `ActionButton` and `ActionLink`; the toolbar is `CommandListToolbar` with a `FilterChipGroup` (one pressed chip, group named "Member filters"); loading, forbidden, error and empty states are `LoadingState`, `ForbiddenState`, `ErrorState` (retry calls the real refetch) and `EmptyState` (title and body name how to clear the filter). The page-local `statusTone` is replaced by `toneOf(accountTones, ...)`. A failed first load now shows only the error state; before, it rendered an empty roster.
- **MemberDetailPage:** forms use `TextField`, `TextAreaField`, `SelectField` and `CheckboxField`; the two reset buttons and the ban and end-membership confirm dialogs use `Pages_Command_Member_ProfileReset_Dismiss`, `_MembershipReset_Dismiss`, `_ConfirmBan_Dismiss` and `_ConfirmEndMembership_Dismiss`; the confirm dialog takes initial focus on the dismiss control (T-69-01 pattern); badges use `toneOf(accountTones)` and `toneOf(membershipTones)`; the role and claim search is a `SearchField`; forbidden, not-found, loading and error all render the h1 first.
- **VereinMembershipRequestsPage:** filter chips with counts, `TextAreaField`/`TextField` decision form, `ActionButton` decisions (ghost, primary, danger), tones from `membershipRequestTones`, states from the shared views.
- **Tests:** new `tests/admin-member-pages.test.tsx` (17 tests: tones, one h1 per state, retry, empty state, chips, English and German dismiss labels, never "Cancel"); 7 added tests in `admin-membership-requests.test.tsx`; one lexicon test in `admin-members.test.ts`.
- **Ratchet:** `family.admin-members.json` lowered to an empty file map (was 41 controls and 19 look-alikes).

## Task Commits

1. **Task 1 (tracer): members list** - `7651dff4` (refactor)
2. **Task 2: member detail** - `ed8e7581` (refactor)
3. **Task 3: membership requests** - `9923456c` (refactor)
4. **Exact-match fix for the ban confirm button in the roster e2e journey** - `77ee2e00` (test)
5. **Type fix for the detail fixture (exactOptionalPropertyTypes)** - `2b92111f` (test)

Tracer gate: the automated-only verify (`admin-member-pages`, `admin-members`, `ratchets`) was re-run after the tracer commit and passed, so expansion continued.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Unknown member surfaced as a load error**
- **Found during:** Task 2 (not-found test)
- **Issue:** `loadMemberDetail` resolves `undefined` for a missing member, which a React Query query may not hold, so the page showed "Could not load member: Query data cannot be undefined" instead of the not-found state.
- **Fix:** the query function returns `null` for a missing member; the page reads `query.data ?? undefined`.
- **Files modified:** `MemberDetailPage.tsx`
- **Commit:** `ed8e7581`

**2. [Rule 1 - Bug] Failed first load rendered a misleading empty list or queue**
- **Found during:** Tasks 1 and 3
- **Issue:** with an error and no data, the roster and the request workspace rendered as "no match".
- **Fix:** the list and workspace render only when data exists; the error state with retry is shown instead.
- **Commits:** `7651dff4`, `9923456c`

**3. [Rule 1 - Bug] Roster e2e journey broke on the new dismiss label**
- **Found during:** Task 2 (searching e2e specs for the changed labels)
- **Issue:** `getByRole("button", { name: "Ban member" })` is a case-insensitive substring match and now also matches "Don't ban member", a strict-mode violation.
- **Fix:** `exact: true` on that one locator in `tests/e2e/public-shell.spec.ts` (a file outside `files_modified`; one line, reported as XPF-69-15-4). Both affected e2e journeys pass.
- **Commit:** `77ee2e00`

**4. [Plan premise differed from tree] Tone behavior line for unverified accounts**
- The plan's behavior line says an unverified member is warning. The 69-03 SUMMARY records the lexicon change to neutral ("Not verified"), and `accountTones` is the binding source (truth: tones come from `statusTones`). Tests assert neutral. The same applies to a member with no formal membership, now neutral instead of info.

**5. [Plan premise differed from tree] No approve or reject dialogs exist**
- The plan speaks of approve and reject dialogs and T-69-21 asks for confirm labels such as "Approve request". The page decides through direct buttons ("Mark for contact", "Approve", "Reject"), each already naming its action, and the 69-03 key map has no keys for new dialogs. No dialog was introduced (a new confirm step is a behavior change and the locale files are off limits). The decision buttons keep their existing, action-naming labels and the API audit logging is unchanged.

**6. [Scope] New stylesheet needed a ratchet entry**
- Creating `members.css` required an entry in `tests/styles/legacy-ratchet.json` (zero ceilings), a file outside `files_modified`. The legacy ratchet test fails for any stylesheet missing from that JSON.

**Total deviations:** 3 auto-fixed bugs, 3 recorded plan-premise or scope differences. **Impact:** none on the deliverables; two behavior fixes beyond the conversion.

## Cross-plan findings

| ID | File | Problem | Proposed fix |
|----|------|---------|--------------|
| XPF-69-15-1 | `src/styles.css` (`.members-admin-metrics > button*`, `.member-detail-form*`, `.member-detail-toggle-grid*`, `.member-detail-field--wide`, `.member-detail-membership-timeline`, `.member-detail-page`, `.members-admin-page`, `.verein-membership-request-form*`) | Rules for raw buttons, inputs and labels the three pages no longer render; now dead | 69-33 removes them; the replacements are in `features/admin/members.css`. |
| XPF-69-15-2 | `src/locales/en.json`, `de.json` | `SearchField` needs a clear label; no generic key exists, so member detail reuses `Server_Fleet_SearchClear` ("Clear search") | 69-33 adds `Common_SearchClear` and repoints both users. |
| XPF-69-15-3 | `src/locales/en.json`, `de.json` | Now unreferenced by these pages: `Pages_Command_Members_NoRows`, `Pages_Command_Verein_MembershipRequests_NoRows`; `Common_Cancel` loses its member detail readers | 69-33 unused-key check retires them once no other reader remains. |
| XPF-69-15-4 | `tests/e2e/public-shell.spec.ts:733` | One-line edit made by this plan (exact match on the ban confirm button) | Keep; 69-08 and 69-33 should expect the line. |
| XPF-69-15-5 | `src/components/CommandListToolbar.tsx` | Still renders a raw `<input>` for the search box; the members pages inherit it | 69-24 converts it (already in its plan). |

## Verification

- `npx vitest run tests/admin-membership-requests.test.tsx tests/admin-member-pages.test.tsx tests/admin-members.test.ts tests/ratchets tests/styles tests/locales`: 14 files, 289 passed, 1 skipped (the opt-in ratchet writer).
- `npm --prefix sources/ReactApp run lint`: exit 0, no output. `npm --prefix sources/ReactApp run typecheck`: exit 0 after the fixture type fix.
- `npm --prefix sources/ReactApp run test:e2e -- tests/e2e/public-shell.spec.ts -g "member roster|membership request" --project=chromium`: 2 passed.
- Acceptance greps (all at 0): `function statusTone` in MembersPage, raw controls in all three pages, `Common_Cancel` in MemberDetailPage, `function (statusTone|membershipTone)` in MemberDetailPage, `const statusTones` in VereinMembershipRequestsPage; `Object.keys(family.admin-members.json.files).length` is 0.

## Issues Encountered

- The worktree had no `node_modules`; an untracked directory junction to the main checkout's install was created (git-ignored, nothing installed).
- `npm run test:e2e` runs `build:assets`, which rewrote `src/generated/api.ts` with line-ending differences; it was restored with `git checkout --` and is not in any commit.
- `tsc -b` and `eslint .` were slow under parallel executor load; they were run in the background and finished clean.

## Known Stubs

None.

## Threat Flags

None. T-69-21 stays mitigated by the existing action-naming decision labels and the unchanged API audit logging; see deviation 5. No package was installed (T-69-SC).

## Self-Check: PASSED

- Created files exist: `members.css`, `admin-member-pages.test.tsx`.
- Commits `7651dff4`, `ed8e7581`, `9923456c`, `77ee2e00`, `2b92111f` are ancestors of HEAD.
