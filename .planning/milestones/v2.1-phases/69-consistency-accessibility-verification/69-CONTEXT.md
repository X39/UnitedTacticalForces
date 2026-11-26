# Phase 69: Consistency, Accessibility & Verification - Context

**Gathered:** 2026-11-22
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

The rest of the React app meets the same visual standard as the redesigned server section, and the whole milestone is
proven keyboard operable, accessible, fully localized, route-safe, and usable on mobile in both languages.

Requirements: CON-01, CON-02, CON-03, FND-06, L10N-03, VER-01, VER-02, VER-03.

Success criteria (from ROADMAP):
1. React pages share one page header rhythm and common loading, empty, and error state panels, and navigation labels,
   spacing, and badge tones follow an agreed closed list.
2. Every redesigned server screen is fully keyboard operable and passes automated accessibility scans (axe, WCAG AA).
3. Walking every redesigned server screen in German shows no hard-coded or untranslated English text.
4. The route certification tests are green and match the routes and URL parameters the redesign uses.
5. End-to-end journeys pass for create then start, change mods, and German locale, and Playwright runs mobile (320 and
   390) in English and German with visual baselines for key screens.

</domain>

<decisions>
## Implementation Decisions

### Auto-decided after research (autonomous mode, reversible, logged for user review)
- D-AUTO-1 Pixel baselines (`@visual`) are opt-in via an environment variable and are generated and compared in one environment; CI runs the layout-metric assertions unconditionally. CI timeouts and worker counts are not changed in this phase (external pipeline config stays as is). Generating and accepting the first baselines is a deferred human follow-up.
- D-AUTO-2 The server Files row menu uses inline actions (no Disclosure and no new RowMenu primitive).
- D-AUTO-3 Every routed page renders its PageHeader (one h1) above Forbidden/Error/Loading state panels, including detail pages that early-return a state.
- D-AUTO-4 Tier C public hero blocks keep their own scale as the single documented exception (UI-SPEC E-01); their remaining raw CSS literals go on the ratchet allowlist, not converted now.
- D-AUTO-5 The Site images page keeps its dynamic count title as a recorded exception to the nav-label equals h1 rule; Events, Mod packs, Terrains and Gallery are aligned to the closed nav list or recorded as exceptions by the planner.
- D-AUTO-6 The seeded create-then-start journey asserts the waiting-for-worker stage; reaching Running stays a deferred real-environment follow-up.

### Claude's Discretion
All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use ROADMAP phase
goal, success criteria, and codebase conventions to guide decisions.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research. Known anchors: Phase 61 design tokens and parity matrix
(`61-PARITY-MATRIX.md`), the mocked Playwright suite (`sources/ReactApp/tests/e2e`, `playwright.config.ts`) with an axe
spec (`hosting-a11y.spec.ts`), and the seeded-stack UAT suite (`sources/ReactApp/tests/uat`,
`playwright.uat.config.ts`, `npm run test:uat`).

</code_context>

<specifics>
## Specific Ideas

No specific requirements — discuss phase skipped. Refer to ROADMAP phase description and success criteria.

Carry-forward working agreements from earlier phases of this milestone:
- Automate UAT wherever possible (mocked Playwright plus the seeded-stack suite) instead of handing checkpoints to the
  user; only visual design judgment and real-environment checks remain human, and they are deferred to the milestone
  end.
- Known user UI dislikes: no `<details>` disclosure triangles, no ugly native selects, no misaligned dialogs.
- Visual baselines must be stable on Windows and CI (avoid flaky font or animation dependent screenshots).

</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.

</deferred>
