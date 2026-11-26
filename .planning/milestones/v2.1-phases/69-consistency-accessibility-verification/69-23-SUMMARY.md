---
phase: 69-consistency-accessibility-verification
plan: 23
subsystem: ui
tags: [react, forms, wiki, accessibility, focus-ring, state-views, ratchet]

requires:
  - phase: 69-03
    provides: "Shared state views, the _Dismiss and empty-state keys of the key map"
  - phase: 69-04
    provides: "Family ratchet files and the UTF_RATCHET_UPDATE writer"
provides:
  - "Join form on TextField, TextAreaField, CheckboxField and ActionButton with labels, aria-required and per-field errors"
  - "GDPR, public field report list and detail pages with one h1 in every state, shared links, chips and state views"
  - "Wiki with one h1 through PageHeader, shared SearchField, shared controls and state views outside the editor, visible focus rings"
  - "tests/public-form-pages.test.tsx and extended tests/wiki/page.test.tsx"
affects: [69-33]

plan_head_before: 33900a1eb33cae97ddfef5e6dbd3fcc658ca705b
plan_head_after: 6650198147a6f133e417de6d63da881cb00d67b9

actuals:
  tokens: 60000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Structured GDPR section headers are h2 so a host page (hero, admin preview) owns the only h1"
    - "role=tab buttons are ActionButtons styled by their container rules; focus ring is an inset token ring inside overflow-hidden panels"

key-files:
  created:
    - sources/ReactApp/tests/public-form-pages.test.tsx
  modified:
    - sources/ReactApp/src/pages/JoinPage.tsx
    - sources/ReactApp/src/pages/GdprPage.tsx
    - sources/ReactApp/src/pages/FieldReportsPage.tsx
    - sources/ReactApp/src/pages/FieldReportDetailPage.tsx
    - sources/ReactApp/src/features/wiki/WikiPage.tsx
    - sources/ReactApp/src/features/wiki/wiki.css
    - sources/ReactApp/tests/wiki/page.test.tsx
    - sources/ReactApp/tests/ratchets/family.public-forms-wiki.json
    - sources/ReactApp/tests/e2e/public-shell.spec.ts

key-decisions:
  - "GDPR and the field report detail hero use PublicHero (the Tier C hero kind), the wiki uses PageHeader with the shared SearchField in its actions slot."
  - "The wiki ratchet keeps one entry (WikiPage.tsx, 1 control) for the raw markdown editor textarea, reason 'wiki editor (UI-SPEC allowance)'; the editor title and comment fields, tabs and buttons were converted anyway."
  - "Acknowledgement checkboxes on the join form now show an error on an invalid submit; before, an unchecked box blocked the submit silently."

patterns-established:
  - "Empty and error panels carry a title that names the situation, so an e2e heading query must pin the level when it matches by substring."

requirements-completed: [CON-01, CON-02, CON-03, FND-06, L10N-03]

coverage:
  - id: D1
    description: "Join form uses only shared labelled fields, marks required fields with aria-required, shows errors and submits the unchanged payload"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/public-form-pages.test.tsx#join page"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/public-shell.spec.ts (German Join journey, Verein settings shared with Join)"
        status: pass
    human_judgment: false
  - id: D2
    description: "GDPR, field report list and detail render exactly one h1 in loading, error, empty and loaded states with shared controls"
    requirement: CON-01
    verification:
      - kind: unit
        ref: "tests/public-form-pages.test.tsx#GDPR page and #public field report pages"
        status: pass
    human_judgment: false
  - id: D3
    description: "Wiki renders one h1 through a header kind, shared controls outside the editor, and a focus ring on tree, tabs, related links, revisions and the close control"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/wiki/page.test.tsx#wiki page header and controls and #wiki stylesheet focus"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/public-shell.spec.ts (public Wiki article without protected history)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Visual fit of the new PublicHero on GDPR and report detail and of the wiki hero with the shared search"
    requirement: CON-01
    verification: []
    human_judgment: true
    rationale: "Presentation after the hero swap was not compared against a screenshot; the gating audits of Wave 3 and the visual baselines cover it."

duration: 75min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 23: Public Forms and Wiki Light Pass Summary

**The join form now runs on the shared fields with aria-required and per-field errors (payload unchanged and tested), GDPR, the public field reports and the wiki each render one h1 through a shared header kind, and the wiki shows the focus ring token on every interactive element it used to strip.**

## Accomplishments

- Join: 19 raw controls and 3 button look-alikes are gone; `JoinPage.tsx` scans clean. Provider buttons keep calling `auth.login("steam")` and `auth.login("discord")` unchanged; request load error uses `ErrorState` with a real refetch; the sign-in prompt uses `ForbiddenState`.
- GDPR: the hand-written hero h1 became a `PublicHero`; the four structured section headers are h2 so the hero (or the admin preview host) owns the only h1; all four button-class links are `ActionAnchor` or `ActionLink`; loading, error (with a working retry) and unavailable use the shared views. The page-literal count was already 0 at this HEAD, so no new key was needed.
- Field reports: the list uses `FilterChipGroup`, `LoadingState`, `ErrorState` and two `EmptyState` variants from the 69-03 key map; the detail page renders a `PageHeader` above its loading, error and not-found states (it had no heading there) and a `PublicHero` for the report itself.
- Wiki: the hand-written hero became a `PageHeader` with the shared `SearchField`; every button, tab and revision row is an `ActionButton`; title and change comment in the editor are `TextField` and `TextAreaField`; state panels are the shared views. `wiki.css` lost its six `outline: none/0` rules, the dead `.wiki-search`, `.wiki-empty-article` and wiki h1 rules, and gained token focus rings (inset on tabs because the panels clip overflow).
- Ratchet: `family.public-forms-wiki.json` is reduced from five files to one (`WikiPage.tsx`, 1 control, the raw markdown textarea).

## Task Commits

1. **Task 1 (tracer): join form on shared fields** - `89d4c7c5` (refactor)
2. **Task 2: GDPR and field report pages with one heading** - `7a0504d3` (refactor)
3. **Task 3: wiki heading, focus rings, editor-only ratchet** - `66501981` (refactor)

Tracer gate: the tracer verify (unit tests plus the public-shell e2e spec) was re-run end to end; the join workflow tests passed, expansion continued. See deviation 1 for the one unrelated e2e failure.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Pre-existing e2e failure unrelated to this plan**
- **Found during:** Task 1 tracer verify
- **Issue:** `public-shell.spec.ts` "filters the event administration ledger on canonical and legacy routes" fails at the `Event Administration` heading on the plan base (before any of this plan's changes). It belongs to the events-admin conversion (69-16), not to a file of this plan.
- **Fix:** None here; logged in `deferred-items-69-23.md`.

**2. [Rule 1 - Bug] E2E heading query became ambiguous after the shared empty state**
- **Found during:** Task 3 e2e run
- **Issue:** `loads the beta shell at a deep link` queried `getByRole("heading", { name: "Field reports" })`; the shared `EmptyState` title "No field reports yet" (69-03 key map) is a second match, so Playwright's strict mode failed.
- **Fix:** Pinned the query to `{ name: "Field Reports", level: 1 }`. `tests/e2e/public-shell.spec.ts` is outside this plan's `files_modified`; the change is one line and was unavoidable.
- **Files modified:** `sources/ReactApp/tests/e2e/public-shell.spec.ts`
- **Commit:** `66501981`

**3. [Rule 2 - Missing critical functionality] Unchecked acknowledgements failed silently**
- **Found during:** Task 1
- **Issue:** The three required checkboxes had no error output, so an invalid submit did nothing visible.
- **Fix:** `CheckboxField` with `aria-required` and the shared invalid message.
- **Commit:** `89d4c7c5`

**Total deviations:** 3 (one Rule 1, one Rule 2, one Rule 3 logged and deferred). **Impact:** none on the deliverables.

## Decisions Made

See `key-decisions` in the frontmatter. In addition: the report detail page title above its early states is the list title ("Field Reports"), because no report title exists before the data loads; `useDocumentTitle` already covers the tab title.

## Issues Encountered

- Wiki article Markdown that starts with `# Heading` renders an h1 inside the article, so a published article can still add a second h1. Markdown rendering is a closed non-goal; the new wiki test uses heading-free content. Flagged for the content owners rather than changed.
- The worktree had no `node_modules`; a junction to the main checkout was used and nothing was installed. `src/generated/api.ts` was rewritten by the asset build twice and restored each time; it is in no commit.
- A temporary Playwright config on private ports (4373 and 4374) ran the e2e spec so a sibling's server on 4173 could not be reused; it was deleted before the commits and is untracked.
- `tests/styles/legacy-ratchet.json` was not touched; this plan adds no stylesheet. Removing wiki.css rules only lowered its counts.

## Known Stubs

None.

## Threat Flags

None. T-69-29 is mitigated by the payload equality test in `tests/public-form-pages.test.tsx`; no endpoint, auth path or schema changed.

## Verification

- `npm --prefix sources/ReactApp test -- tests/public-form-pages.test.tsx tests/wiki tests/ratchets tests/styles tests/locales`: 15 files, 291 passed, 1 skipped.
- `tests/admin-public-page-editor.test.ts tests/admin-public-pages.test.ts tests/admin-field-reports.test.ts tests/app tests/routes.test.ts`: 73 passed (the GDPR section headers also render in the admin preview).
- `npm run lint` (max warnings 0), `npm run typecheck`: clean.
- `tests/e2e/public-shell.spec.ts` (chromium): 27 of 28 pass; the failure is the pre-existing event administration test (deviation 1). Re-run of the three tests touching join, wiki and the field report deep link: 3 passed.
- Acceptance criteria: JoinPage raw controls 0; GdprPage and FieldReportDetailPage `<h1` 0 (2 of 2 files); GdprPage `className="button` 0; wiki.css `outline: none|0` 0; WikiPage `<h1` 0; family file entries 1.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/public-form-pages.test.tsx`.
- Task commits are ancestors of HEAD: 89d4c7c5, 7a0504d3, 66501981.
- No files were deleted by this plan (`files_deleted:` none).
