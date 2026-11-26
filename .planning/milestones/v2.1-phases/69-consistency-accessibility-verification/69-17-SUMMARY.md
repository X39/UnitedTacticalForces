---
phase: 69-consistency-accessibility-verification
plan: 17
subsystem: ui
tags: [react, public-hero, page-header, state-views, action-button, text-field, ratchet, accessibility]

requires:
  - phase: 69-01
    provides: "PageHeader Display rhythm, PageStack, shared dialog shell"
  - phase: 69-03
    provides: "StateViews and the dismiss, empty-state keys used here"
  - phase: 69-04
    provides: "family.events-member.json ratchet and its writer"
  - phase: 69-06
    provides: "audit backbone (no discovery finding was assigned to this plan)"
provides:
  - "Event detail on PublicHero with one h1 above every state (loading, not found, forbidden, error)"
  - "Account settings on TextField, FileChooserButton, ActionButton, StateViews and one PageHeader h1 with h2 sections"
  - "Verein ledger on PageHeader, shared states and a refresh next step for an empty ledger (route still uncertified)"
  - "tests/events-member/member-pages.test.tsx (26 tests)"
  - "family.events-member.json empty; eventsMember.css legacy ceilings lowered"
affects: [69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: 17070b62
commits: 3

actuals:
  tokens: 60000
  tasks: 3
  commits: 3

key-files:
  created:
    - sources/ReactApp/tests/events-member/member-pages.test.tsx
  modified:
    - sources/ReactApp/src/features/events-member/EventDetailPage.tsx
    - sources/ReactApp/src/features/events-member/AccountSettingsPage.tsx
    - sources/ReactApp/src/features/events-member/VereinLedgerPage.tsx
    - sources/ReactApp/src/features/events-member/eventsMember.css
    - sources/ReactApp/src/components/FileChooserButton.tsx
    - sources/ReactApp/tests/ratchets/family.events-member.json
    - sources/ReactApp/tests/styles/legacy-ratchet.json

key-decisions:
  - "Event detail hero: kicker is the Operation label, the meta strip is the PublicHero description, the back link is a glass ActionLink in the hero actions (DOM order equals visual order)."
  - "Non-loaded event states use a compact PublicHero titled Operation above the shared state view; an invalid event id now takes the same not-found path instead of NotFoundPage."
  - "Forbidden API errors on the event page render ForbiddenState instead of the generic error panel."
  - "The three inline mutation error panels on the event page stay StatePanel (no title keys exist and the plan forbids locale edits); see XPF-69-17-2."
  - "The deletion dialog and the slot-loss dialog both open with focus on the dismiss control."

patterns-established:
  - "Early-return pages render the same PageHeader above the state view; section headers inside a page use PageHeader headingLevel 2."

requirements-completed: [CON-01, CON-02, CON-03]

coverage:
  - id: D1
    description: "Event detail renders exactly one h1 through PublicHero, first heading, above loading, not-found, forbidden and error states; retry refetches both queries"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/events-member/member-pages.test.tsx (event detail page, 8 tests)"
        status: pass
      - kind: e2e
        ref: "playwright tests/e2e/event-detail.spec.ts (chromium, private port): 1 passed, no locator changes needed"
        status: pass
    human_judgment: true
    rationale: "Hero layout was checked on desktop and 390px screenshots by the executor; no human visual review."
  - id: D2
    description: "Account form fields are shared fields with labels and errors, save sends the same payload, deletion dialog dismiss names keeping the account with initial focus, submitAccountDeletion called once with the unchanged return URL only after confirm"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/events-member/member-pages.test.tsx (account settings page, 11 tests)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Verein ledger renders one h1, an h2 ledger section, shared controls, shared states and an empty state with a next step; membership-required state never requests the ledger"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/events-member/member-pages.test.tsx (Verein ledger page, 7 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "events-member ratchet is empty and eventsMember.css ceilings only fell"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/ratchets, tests/styles/legacy-ratchet.test.ts"
        status: pass
    human_judgment: false

duration: ~2h (estimated)
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 17: Member event and account pages Summary

**Event detail on PublicHero, account settings on shared fields and the Verein ledger on PageHeader, all with one h1 above every state, shared state views, shared controls, named dismiss labels and an empty events-member ratchet.**

## Accomplishments

- `EventDetailPage`: PublicHero hero (one h1), `EventStatePage` frame (compact PublicHero + shared view) for not found, loading, forbidden and error with a real retry of both queries; EmptyState for briefing and slots; every button is an ActionButton (gold assign and download, ghost attendance and slot actions), the slot-loss dialog dismiss reads "Keep slot" with initial focus on it.
- `AccountSettingsPage`: 11 TextFields (labels, errors, autocomplete kept), avatar via `FileChooserButton` (image filter kept), ActionButtons, ForbiddenState/LoadingState/ErrorState under the page header, four section headers demoted from h1 to h2, deletion dialog dismiss "Keep account" with initial focus.
- `VereinLedgerPage`: header kept in every state, ledger section header is an h2, table gets a caption and column scopes, empty ledger offers a refresh action.
- CSS: dead rules removed (`em-operation-tag`, `em-event-hero__back`, `em-primary-slot-action`, `em-avatar-upload`, ledger h1 override), PublicHero fit rules added with tokens only; `eventsMember.css` ceilings 26/26 to 24/20.
- `family.events-member.json`: 3 files (27 controls, 15 look-alikes) to empty.

## Task Commits

1. Task 1 (tracer): `0d578d39` refactor(webapp): put the event detail title on the public hero
2. Task 2: `8ae5646c` refactor(webapp): converge account settings on shared fields
3. Task 3: `17070b62` refactor(webapp): converge the Verein ledger page

Tracer gate: the tracer verify (vitest events-member and ratchets, event-detail e2e in chromium) passed before expansion.

## Deviations from Plan

**1. [Rule 2 - Missing critical] `accept` prop on FileChooserButton**
- Issue: the avatar picker used `accept="image/*"`; `FileChooserButton` had no such prop, so converting would drop the file-type filter.
- Fix: additive optional `accept?: string` prop (no other plan lists this file). Test asserts it reaches the input.
- Files: `src/components/FileChooserButton.tsx`. Commit `8ae5646c`.

**2. [Scope] Files outside `files_modified`**
- `tests/styles/legacy-ratchet.json` (lowered `eventsMember.css` ceilings, same precedent as 69-08) and `FileChooserButton.tsx`. `tests/events-member/account-settings.test.ts` and `tests/e2e/event-detail.spec.ts` needed no change (mapping tests unaffected; the spec passes unmodified). No new stylesheet, so no zero-ceiling entry was added to `legacy-ratchet.json`.

**3. [Rule 1 - Bug] Attendance buttons stopped stretching after conversion**
- Found in a screenshot: the converted ghost buttons shrank to content width. Added `width: 100%` to `.em-attendance-choice`. Commit `17070b62`.

**4. [Rule 3] Icon double**
- `tests/mocks/react-icons.tsx` lacks nine icons the event page draws; member-pages.test.tsx extends it locally with `vi.mock` instead of editing the shared mock (avoids sibling merge conflicts).

## Cross-plan findings

| Id | File and line | Problem | Proposed fix |
|----|---------------|---------|--------------|
| XPF-69-17-1 | `src/components/StateViews.tsx` ForbiddenState | No action slot, so the sign-in button had to go into the children (rendered in the body, not the panel action area). | Add an optional `action` prop forwarded to StatePanel. |
| XPF-69-17-2 | `src/locales/*.json` | No title keys exist for the three event action failures (attendance, slot, download), so those inline panels stay StatePanel without a title or retry. Also the empty Verein ledger has no next-step body key (a Refresh action stands in) and the account sign-in state reuses the join-page title "Sign in before applying". | Add `Pages_Events_Detail_Error_*Title`, `Pages_Unit_Kassenbuch_Index_NoEntriesBody` and an account sign-in title key; then convert. |
| XPF-69-17-3 | `src/components/NotFoundPage.tsx` | Renders no h1 (state panel only). Event detail no longer uses it; other callers may. | Render a PageHeader above the panel. |
| XPF-69-17-4 | `eventsMember.css` `.em-attendance-choice__mark` | Glyph color equals its `currentColor` background, so the check mark is invisible (pre-existing). | Give the mark its own contrasting color token. |

## Issues Encountered

- `npm run lint` flagged my temporary Playwright config and screenshot spec; both were deleted before the final lint and typecheck runs (lint on touched paths and `tsc -b` clean).
- Worktree node_modules is a junction to the main install; nothing installed. `src/generated/api.ts` line-ending changes from `build:assets` were restored and never committed.
- The plan's grep acceptance criteria use `$` anchors and cannot match CRLF working-tree files on this machine; equivalent greps without the anchor give 0 for raw controls, `<h1` and `Common_Cancel` in the three pages.

## Verification

- `vitest tests/events-member tests/ratchets tests/styles tests/locales tests/state-views.test.tsx tests/primitives.test.tsx`: 413 passed, 1 skipped (opt-in writer); `tests/hosting/files-panel-*` 35 passed (FileChooserButton callers).
- `npx tsc -b` and eslint over the touched paths: exit 0. A full `npm run lint` showed only the two temporary files since removed.
- Playwright `event-detail.spec.ts` (chromium, private ports 4373/4374, temporary uncommitted config): passed.
- `node -p` of the family file key count: 0.

## Known Stubs

None.

## Threat Flags

None. T-69-23 mitigated: deletion dialog opens with focus on "Keep account", the confirm names deleting the account, and the test asserts `submitAccountDeletion` is called exactly once with the unchanged `localReturnUrl` only after confirm.

## Notes for the orchestrator

- `files_deleted`: none.
- No new stylesheet and no `legacy-ratchet.json` entry added; only the `features/events-member/eventsMember.css` ceilings were lowered (merge conflicts with sibling edits to that file are possible only on that block).
- `routes.json` verein-ledger certification untouched (D-15).

## Self-Check: PASSED

- Created file exists: `tests/events-member/member-pages.test.tsx`.
- Commits `0d578d39`, `8ae5646c`, `17070b62` are ancestors of HEAD.
