# Phase 69: Consistency, Accessibility & Verification - Research

**Researched:** 2026-11-22
**Domain:** React 19 + Vite frontend consistency convergence, axe/WCAG automation, i18n gating, Playwright visual and mobile verification (all under `sources/ReactApp`)
**Confidence:** HIGH on code inventory and tooling facts (read or executed this session); MEDIUM on CI runtime and cross-platform pixel stability (cannot be measured here)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
None. `69-CONTEXT.md` is auto-generated (discuss skipped via `workflow.skip_discuss`); its `## Implementation Decisions` section only says: "All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, and codebase conventions to guide decisions."

### Claude's Discretion
All implementation choices. Carry-forward working agreements named in CONTEXT.md `<specifics>`:
- Automate UAT wherever possible (mocked Playwright plus the seeded-stack suite) instead of handing checkpoints to the user; only visual design judgment and real-environment checks remain human, and they are deferred to the milestone end.
- Known user UI dislikes: no `<details>` disclosure triangles, no ugly native selects, no misaligned dialogs.
- Visual baselines must be stable on Windows and CI (avoid flaky font or animation dependent screenshots).

### Deferred Ideas (OUT OF SCOPE)
None — discuss phase skipped.

### Locked inputs from the APPROVED `69-UI-SPEC.md` (treat as locked; research does not re-open them)
D-01 tiered convergence (A full, B/C light pass, ratchet allowlist that may only shrink); D-02 exactly three header kinds (`PageHeader`, `PublicHero`, `WorkspaceHeader`), `PageHeader` normalized to Display 28/700; D-03 `LoadingState`/`EmptyState`/`ErrorState`/`ForbiddenState`+`AccessBoundary` over `StatePanel`, `HostingLoading|Error|AccessBoundary` and `features/hosting/copy.ts` deleted; D-04 closed nav label list, sentence case, h1 equals label; D-05 five-tone badge lexicon through `statusTones.ts`; D-06 spacing via `--utf-space-*`, new `src/components/shared-primitives.css` scanned by the style guard, legacy CSS held by a ratchet; D-07 closed control list enforced by ESLint `no-restricted-syntax` + ratchet; D-08 `Disclosure` re-implemented as a button disclosure; D-09 native `SelectField` with padded chevron; D-10 one dialog shell geometry; D-11 banned generic dismiss labels; D-12 registry-driven FND-06 audits in three Playwright projects; D-13 L10N-03 proven by static scan + German walk; D-14 identical-EN/DE warning promoted to failure, `locales:unused`; D-15 URL-parameter contract + link scan + route walk, `routes.json` unchanged; D-16 four mocked journeys + one seeded-stack journey file + mobile-320/390 projects with 15 key-screen baselines; D-17 deterministic visuals; D-18 no new dependency; D-19 close all 26 pending parity rows as `kept`; D-20 human items deferred as "Deferred follow-up:".
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CON-01 | React pages share one page header rhythm | `PageHeader` normalization; 11 hand-written `h1` sites listed in Finding F4; early-return pages without any `h1` (F12); one-`h1` route walk |
| CON-02 | Common loading, empty, error state panels | `StateViews.tsx` over existing `StatePanel` (90 files import it, 78 loading, 52 error panels); `HostingLoading/Error/AccessBoundary` have 18 usages; `copy.ts` deletion |
| CON-03 | Nav labels, spacing, badge tones on an agreed closed list | Locale value changes verified against live `en.json`; five h1/label mismatches the UI-SPEC table omits (F4); ten page-local tone functions enumerated (F7) |
| FND-06 | Keyboard operable, axe WCAG AA | axe-core 4.13.0 tag `wcag22aa` contains exactly one rule, `target-size` [VERIFIED]; 11 `outline: none/0` declarations in 5 stylesheets (F3); keyboard-walk and reflow helpers |
| L10N-03 | No hard-coded user-facing text | ESLint-selector static scan found 32 literal hits in 11 files across the whole app (F8); German walk design and false-positive controls (F5) |
| VER-01 | Route certification green and in step with params | 59 routes / 56 certified / 3 uncertified [VERIFIED]; URL-state readers enumerated, API-query `URLSearchParams` are false positives (F9) |
| VER-02 | E2E journeys: create→start, change mods, German | Shared strict mock layer needed (F10); seeded stack cannot reach `Running` through a created server (F2) |
| VER-03 | Playwright mobile 320/390, EN+DE, visual baselines | Three-environment pixel risk and CI budget (F1); Playwright snapshot/clock facts |
</phase_requirements>

## Summary

Phase 69 is a closure phase with two very different halves. The **convergence half** touches roughly 76 `.tsx` files that still contain 529 raw `<button|input|select|textarea|details|dialog>` elements (310 of them in `features/admin`, 105 in Tier A hosting/server code) and 280 `className="button …"` look-alikes. The **verification half** is almost all new test infrastructure: a registry of server screens, three audit helpers, a German walk, a route walk, four journeys, a seeded-stack journey file and 60 pixel baselines. There is no new runtime dependency (D-18) and no package install, so the Package Legitimacy gate does not apply.

The approved `69-UI-SPEC.md` is accurate in its large numbers (verified: 81 route patterns, 26 pending parity rows, 280 button look-alikes, 3,830 locale keys with 0 failures and 0 warnings today) but this research found **17 concrete corrections or additions** (Findings F1–F17 below) that change planning. The ones with the highest planning impact: (F1) pixel baselines would be generated on Windows but compared on at least two different Linux CI environments, and the Mozaic `react-browser` job has a hard 30 minute / 2 CPU / `--workers=1` budget that the new suite will exceed; (F2) the seeded UAT stack never registers a worker, so a freshly created server stops at "waiting for worker" and the seeded "create then start to Running" journey cannot pass as written; (F3) `outline: none/0` appears 11 times in 5 stylesheets, not 3, and several remove the only focus indicator; (F4) five navigation destinations have an `h1` that differs from the label for reasons the UI-SPEC does not list; (F11) Phase 68 plans 13–19 (all the automation React components) have not executed yet, so Phase 69 execution must start after they merge and Tier A raw-control counts must be re-measured then.

**Primary recommendation:** Slice the work into about 25 plans in four waves (foundations, family conversions that own disjoint files, registry audits and journeys, baselines/seeded/closure). Build the ESLint ratchet and the shared Playwright mock layer first, because every later plan depends on them. Make `@visual` pixel comparisons opt-in through one environment variable and generate baselines inside the same Linux image that compares them; keep all layout-metric assertions (the real German-overflow guarantee) unconditional.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Page header, state panels, badge tones, nav labels | Browser / Client (React primitives + CSS) | — | Pure presentation in `sources/ReactApp/src/components`; no API involvement |
| Route certification, URL-parameter contract | Browser / Client (`src/app/routes.ts`, `urlParameters.ts`) | Shared manifest `sources/Common/Ui/routes.json` | The manifest is read by both Blazor and React; this phase changes neither it nor Blazor |
| Locale gates (`check-locales`, `locales:unused`) | Build tooling (Node scripts, CI) | Browser (i18next runtime) | Gates run in `npm run ci`/`build`; runtime fallback behavior is unchanged |
| Accessibility, German walk, reflow audits | Test tier (Playwright against `vite preview` with mocked API) | — | Mocked API keeps results deterministic; no backend change |
| Seeded-stack journeys | Test tier against the real local stack (Aspire, API, emulated Hetzner) | API / Backend (read only) | Must not mutate anything except the UAT database; AGENTS.md forbids turning the harness into a runtime provider |
| Visual baselines | Test tier (Chromium screenshots) | CI environment | Result depends on OS, fonts and Chromium build, see F1 |
| Parity matrix closure | Planning docs + a Vitest guard | — | `.planning/phases/61-*/61-PARITY-MATRIX.md` read by `tests/hosting/parity-matrix.test.ts` |

## Project Constraints (from CLAUDE.md / AGENTS.md)

No `CLAUDE.md` exists; `AGENTS.md` at the repo root applies and is treated with the same authority as locked decisions.
- **UI Component Reuse:** build WebApp UI from shared components; never recreate a standard control page-locally; never use a raw `<input>` as a button. D-07's lint rule is the mechanical form of this.
- Do **not** touch the legacy Blazor `sources/WebApp` except to keep API compatibility (nothing in this phase needs it). `tests/routes.test.ts` only *reads* `sources/WebApp/Pages/*.razor`.
- Handwritten code identifiers, comments and technical docs are English; localized end-user text lives in resource files (here `src/locales/en.json`/`de.json`). C# XML-doc and enum `E`-prefix rules are not applicable to this TypeScript-only phase.
- Generated code is exempt (`src/generated/api.ts`, Kiota client, EF migrations); do not edit or restyle it.
- Commit cadence and message format: one cohesive, verified unit per commit, subject `type(scope): summary`, under 80 chars, scope from `api|worker|hosting|webapp|database|parser`; `!` only for breaking public contracts. The orchestrator commits in this run; executors must follow the format.
- Architecture boundaries (no game process in the API, no worker coupling) are untouched: this phase is frontend and test only. No API, database or OpenAPI change is expected, so no OpenAPI snapshot regeneration.
- User memory feedback applies: automate first with Playwright against the real seeded stack before handing checkpoints to the user; UAT start/stop/seed commands accompany every human checkpoint; real-env tests deferred to milestone end (this phase *is* the milestone end for the seeded journeys); GPG locked means commit unsigned.

## Standard Stack

No new packages. Everything below is already pinned in `sources/ReactApp/package.json` (read this session).

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `@playwright/test` | 1.62.1 | Mocked e2e, audit, journey, mobile and visual projects | Already the repo's browser test runner; `.mozaic/check-browser.sh` hard-codes 1.62.1 for `react-browser` |
| `@axe-core/playwright` | 4.13.0 (axe-core 4.13.0) | WCAG scans | Pinned; `axe.getRules(['wcag22aa'])` returns `target-size` only [VERIFIED: ran `require('axe-core')` in `sources/ReactApp`] |
| `vitest` | 4.1.10 | Static guards, ratchets, unit tests | Existing runner; `css: true`, jsdom, Fluent components/icons aliased to mocks (`vitest.config.ts`) |
| `eslint` + `@typescript-eslint/parser` | 9.39.2 / 8.67.0 | `no-restricted-syntax` ratchet and literal-text scan | Verified working with JSX selectors this session (see Code Examples) |
| `typescript` | 6.0.3 | Optional AST scans in Vitest (URL parameters, toast literals) | Dev dependency already; no test imports it yet, `@typescript-eslint/parser` is an equal alternative |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `@testing-library/react` + `user-event` | 16.3.2 / 14.6.1 | Component tests of the converted primitives | Existing pattern in `tests/*.test.tsx` |
| `msw` | 2.12.10 | Existing Vitest API mocking | Not for Playwright; Playwright uses `page.route` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Registry-driven axe (UI-SPEC D-12) | per-phase specs only | Rejected by the UI-SPEC; per-phase specs miss Phase 68 and adjacent pages |
| Windows-generated baselines (UI-SPEC D-17) | Linux-container-generated baselines | Recommended, see F1; costs one script and a running Docker daemon |
| Pixel baseline everywhere in CI | opt-in `@visual` via env var | Recommended; avoids a flaky red CI across three environments |

**Installation:** none. **Version verification:** versions above were read from `sources/ReactApp/package.json` and `npx playwright --version` (1.62.1) and `npx vitest --version` (4.1.10) on 2026-11-22.

## Package Legitimacy Audit

No external package is installed by this phase (D-18). **Packages removed due to [SLOP] verdict:** none. **Packages flagged as suspicious [SUS]:** none. The only new third-party artifacts are two vendored font families used by tests only (E-03). Treat them as untrusted downloads: fetch into an empty scratch directory, record SHA-256 in a committed manifest, copy the license text from the exact release downloaded, and add a Vitest test that fails if a font file hash or the license file changes. Roboto's license depends on the release (Apache 2.0 for older, SIL OFL 1.1 for current per [web search 2026-11-22, sources conflict](https://www.fontsquirrel.com/license/roboto)); the UI-SPEC's blanket "SIL OFL" [ASSUMED] must be confirmed from the downloaded license file (both permit redistribution). Barlow Condensed is SIL OFL [ASSUMED, from training knowledge].

## Architecture Patterns

### System Architecture Diagram

```
                    +----------------------------------------------------+
  git push / PR --> | CI: GitHub `react` job  |  Mozaic `react-browser`  |
                    +--------------+-------------------+-----------------+
                                   |                   |
        npm run ci (vitest static guards, ESLint ratchet, locales:check/unused)
                                   |
                       playwright test (webServer: build:assets + vite preview + classic-stub)
                                   |
   +-------------------------------+--------------------------------------------+
   | projects                                                                   |
   |  chromium 1280x900  --> existing 14 specs (196 tests, unchanged)           |
   |                     --> audit/*  journeys/*                                |
   |  mobile-390 / mobile-320 --> audit/* (+ journeys on 390) + mobile/* @visual|
   +-------------------------------+--------------------------------------------+
                                   |
        per test: stabilize(page) -> install strict mock API (page.route /api/**)
                  -> registry entry setup(page, culture) -> app renders
                  -> scanAxe | walkKeyboard | checkReflow | checkTextSpacing
                  -> assertNoGermanLeak (culture=de) | toHaveScreenshot (@visual only)
                                   |
   unmocked request? -> aborted + recorded -> test asserts empty list

   Seeded stack (separate, manual or milestone-end):
   Start-Uat.ps1 -Fresh -> Aspire (Postgres, API, emulated Hetzner, React dev :5173)
        -> playwright.uat.config.ts -> tests/uat/journeys.spec.ts (@fresh, @restarted)
```

### Recommended Project Structure
```
sources/ReactApp/
├── eslint.config.js                  # + control and literal-text rules, ratchet imports
├── playwright.config.ts              # projects: chromium, mobile-390, mobile-320
├── src/
│   ├── app/urlParameters.ts          # NEW URL-state contract per route id
│   ├── components/
│   │   ├── StateViews.tsx            # NEW Loading/Empty/Error/Forbidden + AccessBoundary
│   │   ├── statusTones.ts            # NEW closed tone lexicon
│   │   ├── PageStack.tsx             # NEW
│   │   └── shared-primitives.css     # NEW tokens only, style-guard scanned
│   └── features/hosting/copy.ts      # DELETED
├── tests/
│   ├── ratchets/                     # NEW one JSON per family (see F7), not one shared file
│   ├── locales/hard-coded-text.test.ts, routes ... (new Vitest guards)
│   ├── app/url-parameters.test.ts, route-links.test.ts
│   ├── hosting/screen-registry.test.ts
│   └── e2e/
│       ├── audit/        (a11y-audit.spec.ts, german-walk.spec.ts, route-walk.spec.ts)
│       ├── journeys/     (j1..j4 specs)
│       ├── mobile/       (visual.spec.ts @visual)
│       ├── fonts/        (vendored woff2 + LICENSE + MANIFEST.json)
│       ├── __screenshots__/{projectName}/{arg}.png
│       └── support/      (mockApi.ts, serverScreens.ts, a11y.ts, germanLeak.ts, visual.ts, fixtures/)
└── tests/uat/journeys.spec.ts        # NEW seeded-stack journeys
```

### Pattern 1: Strict per-test mock with recorded leftovers (existing, to be extracted)
**What:** `page.route("**/api/**")` handler chain that answers known paths and aborts + records the rest; every test asserts the recorded list is empty. Today six specs each carry a ~90 line private copy (`hosting-a11y`, `hosting-workspace`, `hosting-fleet`, `hosting-create`, `hosting-files-secrets`, …) [VERIFIED: read `hosting-a11y.spec.ts:100-160`, `hosting-workspace.spec.ts:222-285`].
**When to use:** every new audit, walk and journey. Extract once into `tests/e2e/support/mockApi.ts` taking `(page, culture, handlers[])`; keep existing specs untouched (UI-SPEC requires them unchanged).
**Reuse:** the six `tests/helpers/*Fixtures.ts` files import only types (`grep "^import" | grep -v "import type"` returned nothing), so Playwright specs can import `fleetServer`, `workspaceJob`, `createCapability`, `costAlert`, `autoStopAlert`, `serverAutomation`, `metricsHistory` directly. Do **not** import one `*.spec.ts` from another (Playwright forbids it).

### Pattern 2: Registry entry shape
**What:** `{ id, kind, route, setup(page, culture), ready, audit: {axe,keyboard,reflow,spacing,german}, baseline? }` per UI-SPEC. Group an entry's *states* inside one Playwright test per (screen, culture, project) so the page loads once; this keeps the new test count near 130 for audits instead of about 340 (see F1 budget).
**Guard:** `tests/hosting/screen-registry.test.ts` fails when a `hostingRouteDefinitions` pattern (11, [VERIFIED `tests/hosting/routes.test.ts`]) or a `workspaceTabIds` value (`overview|activity|settings|files|machine`, [VERIFIED `workspaceSections.ts:4`]) has no entry.

### Pattern 3: Ratchet = ceiling, per family file
A Vitest test computes the current count per file (AST or the ESLint JSON formatter) and fails when it exceeds the committed ceiling or when a file absent from the ratchet has any. Converter plans never edit a shared JSON; one ratchet JSON per family (`tests/ratchets/controls.admin-authority.json`, …) so parallel executors do not conflict. A final closure plan tightens every ceiling to the then-current count with a small `--update` script.

### Anti-Patterns to Avoid
- **One shared `eslint-control-ratchet.json` edited by 10 parallel plans:** guaranteed merge conflicts. Split per family (above).
- **Importing a spec file into another spec or a `*.spec.ts` into Vitest:** `vitest.config.ts` excludes `tests/e2e/**` and `tests/uat/**`.
- **jsdom layout assertions for dialog geometry:** jsdom has no layout engine, `getBoundingClientRect()` returns zeros. D-10 geometry (shared left edge within 1px, stacked actions at <= 520px) must be asserted in Playwright.
- **Per-plan hand edits to `styles.css`:** it is 7,445 lines and the main merge hot spot. Only the shared-primitives plan and the closure plan may edit it; page plans put new rules in their own feature CSS.
- **Screenshot tests that depend on Google Fonts or on the wall clock.** `index.html` loads Roboto/Barlow from `fonts.googleapis.com`; the existing `settleLabelFont` helper in `hosting-workspace.spec.ts` already exists because of this.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Accessibility rule engine | Custom contrast or label checks | `@axe-core/playwright` with tags `wcag2a, wcag2aa, wcag21a, wcag21aa, wcag22aa` | 70 rules for that tag set in axe 4.13.0 [VERIFIED] |
| Raw-control detection | Regex over source text | ESLint `no-restricted-syntax` selectors | Verified selectors below; AST-correct, shows in editors |
| Hard-coded text detection (JSX text and attribute literals) | A custom TypeScript walker | The same ESLint mechanism with `JSXText[value=/[A-Za-z]{2}/]` and attribute selectors | Found only 32 hits; no walker needed. Keep a Vitest AST test only for `showToast("…")`-style call literals |
| Screenshot diffing | Image comparison code | `expect(page).toHaveScreenshot` | Built in; options `animations`, `caret`, `scale`, `maxDiffPixelRatio`, `threshold`, `stylePath` exist [CITED: playwright.dev/docs/api/class-testconfig] |
| Frozen time | Monkey-patching `Date` | `page.clock.setFixedTime(...)` | "Makes `Date.now` and `new Date()` return a fixed fake time … keeps all the timers running" [CITED: playwright.dev/docs/api/class-clock]; install before `goto` [ASSUMED: docs do not state the ordering] |
| Color contrast of tokens | Manual spot checks | Existing `tests/helpers/cssTokens.ts` + `tokens-contrast.test.ts` | Already computes composited ratios per surface |
| German-leak detection of a single source | Free-text language detection | The locale files themselves (English values whose German differs) plus a short function-word list | Deterministic and explainable; see F5 for the false-positive controls |
| Menu/popover keyboard roving for the Files row menu | A bespoke popover | Inline row actions (or one small shared `RowMenu`) | The current `details > div[role=menu]` has no arrow-key behavior (F6) |

**Key insight:** every verification surface this phase needs already exists as a pattern in Phases 61–68 (strict mock, axe scan with a non-vacuity guard, 320/390 metrics, token contrast, source-scan Vitest tests). The work is consolidation and scale, not invention.

## Findings that correct, extend or resolve the UI-SPEC (read before planning)

**F1 — Pixel baselines span three rendering environments, and the CI budget is exceeded.** UI-SPEC D-17 says generate baselines once on the executor's Windows machine and compare in CI. Playwright's own guidance: "Screenshots differ between browsers and platforms due to different rendering, fonts and more" and tests should run "in the same environment where the baseline screenshots were generated" [CITED: playwright.dev/docs/test-snapshots]. Two CI environments exist: `.github/workflows/dotnet.yml` job `react` (ubuntu-latest, `npx playwright install --with-deps chromium`, `npm run test:e2e`) and `.mozaic/pipeline.yml` job `react-browser` (Fedora host, `.mozaic/check-browser.sh` stages "Jammy browser dependencies with the admitted Bookworm worker libc", runs `.mozaic/react-browser.Containerfile`: `CI=true npm run test:e2e -- --workers=1`, **`timeout_minutes: 30`, `cpu_millis: 2000`, `memory_mib: 4096`**, and `needs: dotnet-release`) [VERIFIED: read both files]. Vendored fonts remove font-file variance but not Skia/FreeType/OS differences (Windows vs Linux). Existing suite: 196 tests in 14 files [VERIFIED `npx playwright test --list`]. Estimated additions (assuming states grouped per screen per test): audits about 130, route walk about 115, journeys about 10, visuals 60, mobile metrics inside the audits → roughly 300 new tests; at 2–4 s each and one worker that is 10–20 extra minutes before the existing 196 tests, plus `build:assets` inside `webServer` [ASSUMED: timings not measured]. **Recommendation:** (a) tag pixel tests `@visual` and run them only when `UTF_VISUAL_BASELINES=1` (set in exactly one CI job, never by default on Windows); (b) generate baselines with a script (`scripts/Update-VisualBaselines.ps1`) that runs `playwright test --grep @visual --update-snapshots` inside the same Linux image as the comparing job (the repo's own `node:24.11.1-bookworm` Containerfile is the natural base; Docker CLI 29.5.3 is installed but the daemon was **not running** when probed, Podman is also installed); (c) measure the real wall-clock of `npx playwright test` early (a Wave 0 task) and, if over budget, raise `timeout_minutes` and `--workers=2` for `react-browser` or move audits to a separate job; (d) keep every layout-metric assertion unconditional in every environment. If the human insists on Windows-generated baselines, loosen `maxDiffPixelRatio` and expect red CI; this is the single biggest risk of VER-03 (see Open Question 1).

**F2 — The seeded stack cannot take a created server to `Running`.** `scripts/Start-Uat.ps1` documents: "The emulation never executes a game process and no worker registers, so a started server stays in 'waiting for worker'." `UatFleetScenarioSeeder` seeds `UAT 3 Starting (waiting for worker)` and `UAT 2 Running` as fixed rows [VERIFIED: read both]. UI-SPEC VER-02 lists "create then start against the emulated Hetzner backend to Running". **Resolution:** the seeded journey creates a server, starts it through the cost confirmation, and asserts the job reaches the "waiting for worker" stage with the progress strip naming it; the `Running` transition is covered by the mocked J-1 (pushed frame) and by the seeded `UAT 2 Running` row for stop/automation flows. Also: `AppHost/Program.cs` has an optional `AddDevelopmentCloudWorker` gated on `Hosting:DevelopmentWorker`, but enabling it is out of scope (it changes the harness). Record "real start to Running" as a `Deferred follow-up:` (consistent with `64-UAT.md`).

**F3 — `outline: none|0` appears 11 times in 5 stylesheets, not 3.** [VERIFIED by grep]: `styles.css` 2733 (`.search-field input`, wrapper likely draws focus), 5881 (`.media-gallery-assets__grid > button:focus-visible`, replaced by a 1px inset shadow only), 6955 (`.event-role-symbol-picker > summary:focus-visible`, border color only); `features/hosting/finance.css:51` (`.finance-table tbody tr:focus`); `features/hosting/hosting.css:1836` (`.next-hosting-file-search input`); `features/wiki/wiki.css` lines 88, 218, 403, 478, 503, 543 (mostly `:focus-visible` rules that set `outline: none` and change border color). The keyboard-walk assertion (visible >= 2px ring with 3:1 contrast) will fail on any of these on a screen the registry reaches. A global `:focus-visible` rule exists at `styles.css:1578` and `--utf-focus-ring: 2px solid var(--gold-bright)` at `tokens.css:121`. **Plan:** the style guard scans *all* `src/**/*.css`, not only the three named sheets; each occurrence is fixed (replace with the token ring) or listed in the ratchet with a replacement selector. `features/admin` search/wiki ones belong to the family plans that own those components.

**F4 — Five h1/label mismatches that D-04 and the nav table do not list.** Compared live `en.json` values of each destination's `PageHeader` title with its nav label [VERIFIED by script]: Events page title `Pages_Events_All_PageTitle` = "Event Administration" vs nav "Events"; `Pages_ModPacks_AllModPacks` = "All mod packs" vs "Mod packs"; `Pages_Terrains_AllTerrains` = "All Terrains" (title case) vs "Terrains"; `Pages_Command_Gallery_PageTitle` = "Image gallery" vs "Gallery"; Site images title = `{0} images` (dynamic, kicker carries "Public images") vs "Public images". Allowed exceptions per the UI-SPEC: Overview ("Command Center"), Verein contribution settings. Matches already: Claims, Roles, Members, Field reports, Content and documents, Membership requests, Verein documents, Kassenbuch, Operational settings, Configuration profiles, Hosting finance, Servers. **Plan:** the shell/nav plan changes these locale *values* (keys unchanged, five keys plus DE) and adds an exception row for the dynamic site-images title; the h1==label test runs in Playwright (route walk) because it needs rendered text. Also 11 `h1` elements are hand-written outside `PageHeader`: `EventEditPage:328`, `EventDetailPage:233`, `ConfigurationProfileWorkspacePage:239`, `WikiPage:309`, `FieldReportDetailPage:87`, `GdprPage:203`, `UpcomingEventsPage:279`, `VereinPages:308`, plus `PublicHero:71`, `WorkspaceHeader:38`, `main.tsx:32` (bootstrap) [VERIFIED by grep]; 45 files render `<PageHeader>` (UI-SPEC said 46; recount at plan time).

**F5 — German walk false positives.** The UI-SPEC function-word list includes `an` and `will`, which are ordinary German words (preposition "an", verb "will") [ASSUMED: language knowledge]; two of them in one German sentence is plausible ("… will an …"). **Remove `an` and `will`** (and consider `or`/`to` if fixtures include names). Precompute the "English leak candidates" once: English values whose German value differs, minus any English value that also occurs as a substring of any German value, minus `identical-allowed.json` values; then match visible text only against that set (minimum six characters or two words). This removes coincidences like `Name`/`Status`. Fixtures must use German-neutral names. The helper needs a non-vacuity unit test (a deliberately English sample must be detected) as the UI-SPEC says.

**F6 — `Disclosure` rewrite and the `<details>` inventory.** Eight `<details>` exist: `Disclosure.tsx:45`, `CostConfirmDialog.tsx:146`, `EventRoleSymbolPicker.tsx:52`, `ConfigurationProfileRevisionHistory.tsx:149`, `HostingAuditFeed.tsx:216`, `CloudMachinePanel.tsx:128`, `ServerFilesPanel.tsx:872`, `ServerLogsPanel.tsx:90` [VERIFIED]. Tests that depend on `summary`/`details` and must change in the same plan as D-08: `tests/disclosure.test.tsx` (queries `summary` and `details`), `tests/e2e/hosting-change.spec.ts:732`, `hosting-create.spec.ts:383-386` and `:455` (`details.create-advanced`), `event-authoring.spec.ts:258-259` (`details.event-role-symbol-picker`). The Files row menu (`ServerFilesPanel.tsx:872-915`) is a `role="menu"` popup holding "Open", "Download", "Delete" with no arrow-key roving; converting it to inline `Disclosure` (as D-08 says for all seven) would expand table rows. **Recommend:** render the row actions as inline compact `ActionButton`/`ActionAnchor` controls (24 raw buttons already live in that file), or one small shared `RowMenu` with a real menu keyboard contract; do not use `Disclosure` there. This is an Open Question for the planner (Q3).

**F7 — Raw-control and tone baselines (verified numbers).** Ran an ESLint probe (selector below, temp config deleted afterwards): **529** raw elements in **76** files — Tier A hosting/server 105, `features/admin` 310, `components` 29 (incl. primitives that stay), `pages` 36, `events-member` 27, `wiki` 19, other 3. Separate selector for the look-alikes: **280** `className="button…"` literals in **52** files outside the primitive files (matches the UI-SPEC's 280). The look-alike selector is needed in addition to the element selector, because `<Link className="button button--ghost">` and `<a className="button">` (about 60 on single lines) are not raw `<button>` elements; they must become `ActionLink`/`ActionAnchor`. Ten page-local tone functions confirmed [VERIFIED by grep]: `EventSlotPlanner.statusTone`, `FieldReportsPage.stateTone`, `MemberDetailPage.statusTone` and `.membershipTone`, `MembersPage.statusTone`, `SettingsPage.settingsFeatureTone`, `VereinMembershipRequestsPage.statusTones`, `HomePage.operationTone` and `.attendanceTone`, `UpcomingEventsPage.attendanceTone`; plus `serverState.stateTones` and `hostingAuditTone`/`jobTone` that already map into the five tones.

**F8 — Hard-coded text: small and fixable, but not fully static.** ESLint selectors over all `src/**/*.tsx` found **32** hits in **11** files [VERIFIED by running]: `ServerRuntimePanel.tsx` 9, `CommunityLayout.tsx` 6, `CloudMachinePanel.tsx` 4, `CommandCenterLayout.tsx` 2, `EventCreatePage.tsx` 2, `EventEditPage.tsx` 2, `ModPackContentEditor.tsx` 2, `AuthenticationPage.tsx` 2, `StorageDemandDetails.tsx` 1, `main.tsx` 1, `GdprPage.tsx` 1. Many are language-neutral (`vCPU`, `GiB`, `EN`/`DE` option labels, `UTF`, `Steam`, shell commands in `<code>`, a bootstrap error). The scan needs an allow-list for units/brand tokens and `<code>`. Not caught by JSX selectors (found by reading): `ServerRuntimePanel.tsx:304` `<h2>Terrain and scenario</h2>` is caught as text, but `CreateServerPage.tsx:156` (`?? "Scenario"`) and `ModPackWorkspacePage.tsx:248` (`? "Content"`) are string literals inside expressions; the German walk is the net for those. The UI-SPEC's named offenders (`EventCreatePage`/`EventEditPage` `label="Scenario"`, `placeholder="Search scenarios"`) are confirmed (`:390-391`, `:540-541`).

**F9 — URL-parameter scan must separate URL state from API queries.** `new URLSearchParams({take: …})` appears in 15 data-layer files to build *API* query strings. The scan must key on URL-state sources only: `useSearchParams`, `window.location.search`, and the helpers `filesLocation.ts`, `fleetModel.ts` (`parseFleetSearchParams`/params writers), `CostConfirmationProvider.tsx` (`action`), `workspaceSections.ts` (`tab`). Actual URL keys found: `tab`, `action`, `path`, `area`, `q`, `filter`, `owner`, `search`, `kind`, `status`, `page`, `slotSetup`, `returnUrl`, `mode`; anchors: `server-automation` (Phase 68), activity entry anchors from `activityEntryAnchor`. `returnUrl` is also written by `auth.tsx`/`events-member/services.ts` toward the API login endpoint. Route ids for the manifest are listed in `routes.json` (59 routes, 56 certified, uncertified: `field-report`, `gdpr`, `verein-ledger`) [VERIFIED by `node`]. React-only routes carrying a `classicFallback` (finance account/settings, configuration profile create/workspace, cost alerts) are certified and must be in the route walk.

**F10 — The shared mock layer is the critical path of VER-02/03.** Each existing spec inlines its own fixtures and handlers. Registry entries need server states (running, off, starting, attention, unconfirmed), creation capabilities, cost owners, metrics, activity jobs, files tree, configuration profiles, finance accounts, cost alerts and the Phase 68 automation block. Fixture builders exist for most (see Pattern 1); `tests/e2e/support/{costMocks,metricsMocks,fleetStream}.ts` already provide cost, metrics and WebSocket mocks (`fleetStream.ts` pins wire type names). A file-tree mock and finance/profile mocks must be added. Budget this as its own plan.

**F11 — Phase 68 is not merged yet.** `68-01…68-12` have SUMMARY files; `68-13…68-19` are PLAN-only (waves 5–7). `AutomationPanel`, `AutomationStatusList`, `ScheduleRuleFields`, `ScheduledStartConsentDialog`, `AutoStopNotice`, `useAutoStopNotice` do not exist under `src` today (only `automationModel.ts`, `useAutomationSettings.ts`, `services.ts`, `types.ts` and `SettingsSection.tsx` mention automation) [VERIFIED by grep]. 68-14/15/16 edit `SettingsSection`, `ServerWorkspacePage`, `OverviewSection`, `CostAlertMenuItem`, `CostAlertBanner`, `ActivityEntry`, `JobProgressStrip` — files Tier A conversion also touches — and 68-18/19 add `tests/e2e/hosting-automation.spec.ts`, `tests/e2e/support/automationMocks.ts`, `tests/uat/automation.spec.ts`, and edit `hosting-a11y.spec.ts`. **Consequences:** Phase 69 execution starts only after 68 completes; re-run the raw-control and literal-text counts then; the registry entries for S-07/O-04/notices resolve controls by role and accessible name (`68-UI-SPEC.md`: panel `section#server-automation` with `tabIndex=-1`, heading focus on arrival); reuse `automationMocks.ts`; do not duplicate Phase 68's seeded `automation.spec.ts`, extend it with the German pass; retired keys `Server_Settings_AutomationUnavailable` and `Server_Overview_MetricsUnavailable` text are only deletable after 68-14/16 land.

**F12 — Pages that early-return a panel have no `h1`.** `RoleDetailPage.tsx:176-184` returns a bare `StatePanel` for forbidden and not-found before `PageHeader`, so those states render zero headings, which contradicts "exactly one `h1` per routed page" and would also fail a permissive route walk on detail routes (empty stub → not found). **Decision for the planner:** forbidden, not-found and error states of a routed page render the shared `PageHeader` (route label as title) above `ForbiddenState`/`ErrorState`; add an AST or render-based check for "StatePanel returned before any PageHeader" in the page families. The route walk should use per-route minimal fixtures (not a blind empty stub) so it exercises the loaded state; add a second tiny pass for the not-found state.

**F13 — `wcag22aa` adds only `target-size` (24px).** [VERIFIED] Expect findings on small icon buttons (the `×` in `AppToasts`), table row actions, wiki/tree buttons and chips on non-server pages. The spec's 24px floor for server screens already holds from Phases 61–68; Tier B/C adjacent smoke screens are the unknown. Run a non-gating **discovery pass** over the registry plus one list/detail/editor/public page per family early (Wave 0) so conversion plans fix known classes instead of finding them in the Wave 2 gate. axe 4.13's `color-contrast` also runs on legacy pages; `--quiet` (`#778190`, `styles.css:1462`, 49 `var(--quiet)` uses) is the known offender.

**F14 — Dialog geometry cannot be a jsdom test.** See anti-patterns. Dialog users: `ConfirmDialog` (primitives) plus four hand-rolled `<dialog>` markups outside it: `CommunityLayout.tsx` (mobile nav), `GalleryAssetPicker.tsx`, `FinanceDialog.tsx`, `ServerFilesPanel.tsx` (two). `.dialog` CSS lives in `styles.css:4199-…`.

**F15 — Counts drifted since the UI-SPEC.** `locales:check` now reports 3,830 EN and 3,830 DE keys, 0 failures, 0 warnings [VERIFIED by running `node scripts/check-locales.mjs`]. Raw hex literals measured now: `styles.css` 53, `commandCenter.css` 14, `ProfileMenu.css` 9 (UI-SPEC: 50/12/6; the regex differs). **Compute the ratchet baseline inside the test at implementation time; never copy the UI-SPEC numbers.** `Common_Cancel`/`Hosting_Common_Cancel` references: 17 in 12 files (UI-SPEC: 16). `identical-allowed.json` has about 170 lines. Other legacy sheets outside the three named: `hosting.css` (2,324 lines, 97 hex), `eventsMember.css` (1,223 lines, 26 hex), `publicEvents.css`, `wiki.css`, `finance.css`, `modPacks.css`; include every `src/**/*.css` that the server style guard does not already scan in the ratchet.

**F16 — The `tests/` tree is type-checked.** `tsconfig.app.json` includes `["src","tests"]` with `strict`, `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes`, and `npm run typecheck` is `tsc -b`. New Playwright support files must compile under those flags (optional props need `| undefined`, as the existing `ConfirmDialogProps` does). `playwright.config.ts` is in `tsconfig.node.json`.

**F17 — Seeded journeys mutate state.** `playwright.uat.config.ts` reads `UAT_BASE_URL`, default `http://localhost:5173/next/`, `workers: 1`, `retries: 0`. A mistaken `UAT_BASE_URL` pointing at a real deployment would create servers there. Add a guard in `tests/uat/support/uat.ts` that fails unless the hostname is `localhost` or `127.0.0.1` (Security Domain).

## Suggested Plan Slicing (parallel-safe by file ownership)

Rule: one plan owns each file; nothing outside Wave 0 touches `styles.css`, `eslint.config.js`, `playwright.config.ts` or `package.json`. New locale keys go in sorted positions grouped by the plan's own prefix (e.g. `Pages_Command_Roles_*`) so `en.json`/`de.json` merges are line-disjoint. Counts are raw controls as of today; re-measure after Phase 68 merges.

| Wave | Plan slice | Owns (files) | Size |
|------|-----------|--------------|------|
| 0 | **A. Shared primitives and CSS** | `primitives.tsx` (PageHeader, StatePanel, StatusBadge, ConfirmDialog shell), new `shared-primitives.css`, `styles.css` block moves and `--quiet` fix, `tokens.css` (`--utf-measure`), `Disclosure.tsx` + `form-fields.css` (select chevron), tests (`primitives`, `disclosure`, `tokens-contrast`, style guard extension, legacy ratchet), the four e2e lines that reference `summary`/`details` | Large |
| 0 | **B. StateViews, statusTones, PageStack** (new files), `Common_State_*` keys, unit tests | Small |
| 0 | **C. Gates:** `eslint.config.js`, `tests/ratchets/*.json`, ratchet and hard-coded-text tests, `check-locales.mjs` promotion + `--unused`, `server-keys` banned-label extension, `package.json` scripts | Medium |
| 0 | **D1. Playwright config and visual determinism:** projects, snapshot path template, vendored fonts + manifest test, `stabilize`, `@visual` gating, baseline script, wall-clock measurement | Medium |
| 0 | **D2. Mock layer, audit helpers, registry skeleton and guard,** `germanLeak` unit test, non-gating discovery pass (F13) | Large |
| 0 | **E. URL-parameter contract:** `src/app/urlParameters.ts`, `url-parameters.test.ts`, `route-links.test.ts` | Small–Medium |
| 1 | **F. Shell and navigation:** `CommandCenterLayout`, `CommunityLayout`, `ProfileMenu`, `commandNavigation`, nav and h1-alignment locale values (F4), `AuthenticationPage`, `CommandOverviewPage` | 29 controls, Medium |
| 1 | **G. Hosting fleet, create, shared hosting components:** `FleetPages`, `components.tsx`, `CreateServerPage`, `copy.ts` deletion, `HostingLoading|Error|AccessBoundary` removal (18 usages) | Medium |
| 1 | **H. Hosting Files:** `ServerFilesPanel` (34 raw, 1,481 lines, row menu decision F6, three `Common_Cancel`), `hosting.css` focus fixes | Large |
| 1 | **I. Hosting profiles:** `ConfigurationProfile*` (7 files, about 35 controls), `ConfigurationProfileRevisionHistory` `<details>` | Medium |
| 1 | **J. Hosting finance, alerts, runtime, machine, logs, audit:** `HostingFinance*`, `Finance*`, `CostAlertsPage`, `CostAlertSettingsPanel`, `ServerRuntimePanel` (literals), `CloudMachinePanel`, `ServerLogsPanel`, `HostingAuditFeed`, `CostConfirmDialog`, `finance.css` | Medium–Large |
| 1 | **K. Authority and members:** `ClaimsPage`, `RolesPage`, `RoleDetailPage`, `MembersPage`, `MemberDetailPage` (about 70 controls, h1 early-return fix F12, tone functions) | Large |
| 1 | **L. Events (admin and member):** `EventAdminPage`, `EventCreatePage`, `EventEditPage`, `EventEntityPicker`, `EventSlotPlanner` and pickers, `EventDetailPage`, `AccountSettingsPage`, `VereinLedgerPage` (about 90 controls; editors stay on the ratchet) | Large |
| 1 | **M. Content, media, field reports:** `ContentHubPage`, `PublicPages*`, `MediaGalleryPage`, `SiteImagesPage`, `GdprContentEditor`, `GalleryAssetPicker`, `FieldReports*` admin (about 95 controls) | Large |
| 1 | **N. Mod packs and terrains:** `ModPack*`, `Terrain*` (about 55 controls) | Medium |
| 1 | **O. Verein admin and settings:** `Verein*` admin pages, `SettingsPage` (about 55 controls) | Medium |
| 1 | **P. Public community and wiki (Tier C):** `pages/*`, `WikiPage` (editor ratcheted), `PublicHero`, `wiki.css` focus fixes, `statusTones` replacements | Medium–Large |
| 2 | **Q–S. Registry entries and audit specs** in three slices: S-01…S-04 + O-01/O-09 + notices; S-05…S-09 + automation (S-07, O-04) + O-02…O-06; S-10…S-13 + O-07/O-08 + the four non-server smoke entries. Each slice fixes only issues in shared primitives/server components that its audit surfaces | 3 plans |
| 2 | **T. German walk** spec and German formatting assertions | Medium |
| 2 | **U. Route walk** (56 certified + 3 redirects, sample ids guid/long) and the h1==label check | Medium |
| 2 | **V. Journeys J-1…J-4** (J-4 reuses `automationMocks.ts`) | Medium |
| 3 | **W. Visual baselines K-01…K-15** (60 images, Linux generation, human review item) | Medium |
| 3 | **X. Seeded-stack journeys** (`tests/uat/journeys.spec.ts`, F2 and F17) | Medium |
| 3 | **Y. Closure:** parity rows (26 to `kept`, guard rule), retired locale keys and `Common_Cancel` deletion, ratchet tightening, CI timeout/env changes (F1), requirement traceability | Medium |

About 25 plans. Wave 1 plans F–P are mutually disjoint by file; their only shared surfaces are `en.json`/`de.json` (line-disjoint inserts) and the family ratchet files they own.

## Common Pitfalls

### Pitfall 1: Red CI from pixel drift
**What goes wrong:** baselines generated on Windows fail on Linux, or differ between the GitHub and Mozaic images.
**Why:** platform rendering differences, F1. **How to avoid:** `@visual` opt-in, Linux generation in the comparing image, vendored fonts, fixed clock, `animations: "disabled"`, `reducedMotion: "reduce"`, `deviceScaleFactor: 1`, platform-free `snapshotPathTemplate` (omit `{platform}`; default includes it per the docs). **Warning signs:** diffs concentrated on text edges.

### Pitfall 2: `retries: 2` and 30-minute budget hide cost
**What goes wrong:** CI retries (`retries: process.env.CI ? 2 : 0`) triple the time of a flaky visual test and trip the Mozaic 30-minute limit. **How to avoid:** measure early (F1c); keep audits in few, state-grouped tests; do not retry deterministic audits.

### Pitfall 3: Fixing the ESLint rule before the ratchet exists
**What goes wrong:** turning on the rule at error severity with 529 hits fails `npm run lint` (`--max-warnings 0`) for every parallel plan. **How to avoid:** the gates plan (C) lands the rule *with* the ratchet files in the same plan; ratchet files are exempted from the rule through config built from the JSON, and the Vitest ratchet test enforces the ceiling.

### Pitfall 4: Converting controls changes accessible names and test queries
**What goes wrong:** `TextField` wraps label/hint/error and may change `aria-describedby` wiring, breaking `getByLabelText`/role queries in the many `tests/admin-*.test.tsx`. **How to avoid:** run the matching Vitest files in the same plan; keep `name`, `id`, `register(...)` refs working (React 19 passes `ref` as a prop, `TextFieldProps` extends the input props). React Hook Form pages need a quick check that `register` props still land on the input.

### Pitfall 5: `Disclosure` on a button changes keyboard semantics tests
`<summary>` toggled with Enter/Space natively; a `<button aria-expanded aria-controls>` does the same, but tests and e2e that call `.focus()` on `summary` or assert the `open` attribute must be rewritten (F6).

### Pitfall 6: One `h1` and early returns (F12), plus `PublicHero`
Detail routes with forbidden/not-found early returns render no heading; the loading state on those routes also renders none today. Decide once (shared header above state panels) before the family plans start.

### Pitfall 7: Fixed clock and live elements
`page.clock.setFixedTime` freezes `Date` but timers run; `useNow` consumers (relative times, accrued cost counters) must be driven by fixture timestamps relative to the fixed instant. Metrics and accrued-cost fixtures that use `Date.now()` offsets must be rebuilt around the fixed time or masked with `data-visual-mask`.

### Pitfall 8: Static scan allow-lists
Language-neutral tokens (`vCPU`, `GiB`, `EN`/`DE`, `UTF`, brand names, `<code>` commands) are legitimate literals; build the allow-list from the 32 known hits instead of weakening the selector.

## Code Examples

### ESLint ratchet selectors (verified by running a temporary config; temp files removed)
```javascript
// Source: executed this session against sources/ReactApp/src (529 / 280 hits)
const primitiveFiles = [
  "src/components/ActionControl.tsx", "src/components/FormFields.tsx", "src/components/Disclosure.tsx",
  "src/components/SearchField.tsx", "src/components/primitives.tsx", "src/components/AppToasts.tsx",
];
export const restrictedControls = {
  files: ["src/**/*.tsx"],
  ignores: primitiveFiles, // plus ratchet files, built from tests/ratchets/*.json
  rules: {
    "no-restricted-syntax": ["error",
      { selector: "JSXOpeningElement[name.type='JSXIdentifier'][name.name=/^(button|input|select|textarea|details|dialog)$/]",
        message: "Use the shared control (ActionButton, TextField, SelectField, Disclosure, ConfirmDialog)." },
      { selector: "JSXAttribute[name.name='className'] > Literal[value=/^button/]",
        message: "Use ActionButton, ActionLink or ActionAnchor instead of a button look-alike." },
    ],
  },
};
// Literal-text scan (32 hits today); tier A files:
//  "JSXText[value=/[A-Za-z]{2}/]"
//  "JSXAttribute[name.name=/^(aria-label|aria-description|title|placeholder|alt|label|hint|description|summary|heading|legend)$/] > Literal[value=/[A-Za-z]{2}/]"
// ESLint flat config: a later block with the same rule key REPLACES the earlier one, so merge selector arrays
// per file group instead of stacking blocks.
```
Note `src/components/ActionControl.tsx` itself contains `<button>`, and `.tsx` files only: `JSXText`-style selectors do not apply to `.ts`.

### Playwright projects and snapshot path (shape; options per docs)
```typescript
// Source: UI-SPEC Verification Contract; option names from playwright.dev/docs/api/class-testconfig
import { defineConfig, devices } from "@playwright/test";
const visual = process.env.UTF_VISUAL_BASELINES === "1";
export default defineConfig({
  testDir: "./tests/e2e",
  snapshotPathTemplate: "{testDir}/__screenshots__/{projectName}/{arg}{ext}", // no {platform}
  expect: { toHaveScreenshot: { animations: "disabled", caret: "hide", scale: "css", maxDiffPixelRatio: 0.02, threshold: 0.2 } },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] }, testIgnore: ["**/audit/**", "**/journeys/**", "**/mobile/**"] },
    // audit and journeys run in chromium too: add a second chromium-audit project or use testMatch per project
    { name: "mobile-390", use: { ...devices["Desktop Chrome"], viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 } },
    { name: "mobile-320", use: { ...devices["Desktop Chrome"], viewport: { width: 320, height: 568 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 } },
  ],
  // pixel tests skip themselves unless `visual` is true: test.skip(!visual, "visual baselines are opt-in")
});
```
The UI-SPEC's single `chromium` project cannot both ignore and run `audit`/`journeys`; use a second desktop project (for example `chromium-audit` with `testMatch`) or `testMatch` on each project. [ASSUMED: project-level `testMatch`/`testIgnore` semantics per general Playwright knowledge; docs fetched cover them only at config level.]

### Deterministic visual stabilization (shape)
```typescript
// tests/e2e/support/visual.ts
export async function stabilize(page: Page) {
  await page.route("https://fonts.googleapis.com/**", (r) =>
    r.fulfill({ contentType: "text/css", body: vendoredFontFaceCss }));      // @font-face -> https://fonts.gstatic.com/vendored/*.woff2
  await page.route("https://fonts.gstatic.com/vendored/**", (r) =>
    r.fulfill({ contentType: "font/woff2", body: readFileSync(fontPathFor(r.request().url())) }));
  await page.emulateMedia({ reducedMotion: "reduce", colorScheme: "dark" });
  await page.clock.setFixedTime(new Date("2026-11-20T12:00:00Z"));
}
// after goto: await page.evaluate(() => document.fonts.ready); assert document.fonts contains only vendored families.
```
Download fonts only into an empty scratch directory, copy the final `.woff2` files, license text and a `MANIFEST.json` with SHA-256 into `tests/e2e/fonts/`, and assert the manifest in a Vitest test.

### German-leak candidate set (shape)
```typescript
// tests/e2e/support/germanLeak.ts (pure functions, unit-tested under tests/)
export function leakCandidates(en: Record<string,string>, de: Record<string,string>, allowed: Set<string>) {
  const germanBlob = Object.values(de).join("\n").toLowerCase();
  return Object.entries(en)
    .filter(([k, v]) => de[k] !== v && !allowed.has(k) && (v.trim().split(/\s+/).length >= 2 || v.length >= 6))
    .map(([, v]) => v.toLowerCase())
    .filter((v) => !germanBlob.includes(v));   // drop English values that appear verbatim in German text
}
// functionWords = ["the","and","to","of","is","your","you","for","with","this","that","are","not","can","or","from","has"]
// ("an" and "will" removed: ordinary German words, F5)
```

## Runtime State Inventory

Not a rename or migration phase; **omitted** except for the locale-key retirement: the keys listed in UI-SPEC Localization Contract are in `en.json`/`de.json` only (no stored data, no service config, no OS registration, no secrets, no build artifact references them). Vite bundles locale JSON at build time, so no runtime cache carries a retired key.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-phase a11y specs scanning only what the phase touched | One registry iterated by every audit | This phase (D-12) | Covers Phase 68 and adjacent pages; new screens cannot ship unverified |
| WCAG 2.1 AA tags only | plus `wcag22aa` (24px target size) | axe 4.13 already supports it | One extra rule, `target-size` |
| Native `<details>` disclosure with hidden marker | Button disclosure with `aria-expanded`/`aria-controls` | D-08 | Removes the owner's disliked triangle, uniform styling |
| Google-Fonts-dependent screenshots | Vendored test fonts + fixed clock | D-17 | Deterministic text metrics |

**Deprecated/outdated:** `features/hosting/copy.ts` (EN/DE object invisible to the locale gate), `HostingLoading|Error|AccessBoundary`, ten page-local tone functions, `Common_Cancel`/`Hosting_Common_Cancel`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Roboto test fonts may be redistributed under the license of the exact downloaded release (Apache 2.0 or SIL OFL 1.1); Barlow Condensed is SIL OFL | Package Legitimacy | A license mismatch is a repo compliance issue; executor must read the license file shipped with the download |
| A2 | German "an" and "will" are ordinary German words that would cause false positives | F5 | If a false positive is wanted anyway, keep them; low impact |
| A3 | `page.clock.setFixedTime` must be installed before `page.goto` for deterministic first render | Don't Hand-Roll | Date-dependent text could render once with real time; verify in the D1 plan |
| A4 | Project-level `testMatch`/`testIgnore` work as in config-level docs | Code Examples | Config shape may need adjusting; easy to test |
| A5 | New suite adds roughly 300 tests and 10–20 minutes at one worker; Mozaic 30 minute limit is therefore at risk | F1 | If faster, no CI change needed; measure in Wave 0 |
| A6 | Baselines generated in the repo's `node:24.11.1-bookworm` image match ubuntu-latest and the Mozaic staged environment within `maxDiffPixelRatio` 0.02 | F1 | Residual drift between the two CIs; pick one authoritative job for `UTF_VISUAL_BASELINES=1` |
| A7 | The UI-SPEC claim that no test file today uses the TypeScript compiler API is irrelevant; `@typescript-eslint/parser` is an acceptable AST source | Standard Stack | None |

## Open Questions

1. **Which environment is authoritative for pixel baselines and which CI job enables them?**
   - Known: Windows dev, GitHub ubuntu-latest, Mozaic staged Bookworm/Jammy environment all render differently in principle; Mozaic has a 30 minute/2 CPU budget.
   - Unclear: which CI is the merge gate in practice.
   - Recommendation: `UTF_VISUAL_BASELINES=1` in exactly one job, baselines generated by a script in that image; human reviews the first set (D-20).
2. **Does the human accept raising the Mozaic `react-browser` timeout and workers?** Recommend measuring first; if over budget set `timeout_minutes: 60`, `--workers=2`, or split audits to their own job.
3. **Files row menu:** inline actions or a shared `RowMenu` (not `Disclosure`)? Recommend inline actions.
4. **Heading on state-only pages (F12):** confirm "shared `PageHeader` above `ForbiddenState`/`ErrorState`" as the rule for every routed page.
5. **Tier C hero scope** (the UI-SPEC's one unresolved consideration): recommend token-izing only the hero title/lede/actions and the first block rhythm on Home, Unit and Verein pages; everything else stays on the legacy ratchet with a one-line reason.
6. **Site images h1:** dynamic `{0} images` title; recommend a recorded exception (label in the kicker) rather than reshaping the page.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | all tooling | yes | v25.5.0 (`package.json` engines say `>=24 <25`) | works; npm may warn |
| npm | scripts | yes | installed with Node | — |
| Playwright + Chromium | all e2e | yes | 1.62.1; several chromium builds cached under `%LOCALAPPDATA%\ms-playwright` (dry run points at `chromium-1234`) | `npx playwright install chromium` |
| Docker CLI | Linux baseline generation | CLI yes, **daemon not running** | 29.5.3 | Start Docker Desktop, or use Podman (installed) |
| Podman | alternative container runtime | yes | present (`C:\Users\msili\AppData\Local\Programs\Podman`) | — |
| Aspire/.NET/PostgreSQL stack | seeded journeys only | not probed (instructed not to start the stack) | — | Journeys written but run at milestone end, `Deferred follow-up:` |
| Internet (fonts, Playwright CDN) | font vendoring, browser install | yes (this session web access) | — | Tests never use Google Fonts once vendored |
| `gsd-tools` research seam | provider caching | not used | — | WebFetch/WebSearch used directly |

**Missing dependencies with no fallback:** none for the mocked suites.
**Missing dependencies with fallback:** running Docker daemon (needed only for Linux baseline generation; fallback Podman or a CI artifact job).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 (jsdom, `tests/setup.ts`) and Playwright 1.62.1 (Chromium) |
| Config file | `sources/ReactApp/vitest.config.ts`, `playwright.config.ts`, `playwright.uat.config.ts` |
| Quick run command | `npx vitest run <file>` (run in `sources/ReactApp`) |
| Full suite command | `npm run ci` then `npm run test:e2e` (CI also runs `npm run test:e2e` after `build:assets`) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CON-01 | `PageHeader` Display 28/700, token rhythm, no `clamp`, exactly one h1 per routed page | unit + e2e | `npx vitest run tests/primitives.test.tsx tests/styles/server-style-guard.test.ts`; `npx playwright test tests/e2e/audit/route-walk.spec.ts --project=chromium` | partial / Wave 0 |
| CON-02 | Loading/Empty/Error/Forbidden anatomy, roles, `copy.ts` gone | unit | `npx vitest run tests/state-views.test.tsx tests/locales/ownership.test.ts` | Wave 0 |
| CON-03 | Nav labels closed list, h1==label, `statusTones` closed lexicon, unknown to neutral | unit + e2e | `npx vitest run tests/status-tones.test.ts tests/locales/nav-labels.test.ts`; route walk | Wave 0 |
| CON-01..03 | No raw controls outside ratchet; no look-alikes; ratchet never grows | static | `npm run lint`; `npx vitest run tests/ratchets` | Wave 0 |
| FND-06 | axe `wcag2a/2aa/21a/21aa/22aa` zero violations, keyboard walk, reflow, text spacing, zoom, reduced motion over every registry entry | e2e | `npx playwright test tests/e2e/audit/a11y-audit.spec.ts` (all three projects) | Wave 0/2 |
| FND-06 | Focus ring, contrast pairs, `--quiet` AA, no `outline:none` without ring | static | `npx vitest run tests/styles` | extend existing |
| L10N-03 | No literal user text in Tier A; German walk; `locales:check` identical = failure; `locales:unused` | static + e2e | `npm run locales:check && node scripts/check-locales.mjs --unused`; `npx vitest run tests/locales`; `npx playwright test tests/e2e/audit/german-walk.spec.ts` | extend |
| VER-01 | 81 patterns, manifest equals Blazor `@page` set, URL parameter contract, link resolution, route walk | unit + e2e | `npx vitest run tests/routes.test.ts tests/hosting/routes.test.ts tests/app`; route walk | partial |
| VER-02 | J-1 create→start, J-2 change mods, J-3 German, J-4 automation | e2e | `npx playwright test tests/e2e/journeys` | Wave 2 |
| VER-02 | Seeded create→waiting for worker, change mod, automation, German, `@restarted` persistence | e2e (real stack) | `npm run test:uat` (after `scripts/Start-Uat.ps1 -Fresh -NoBrowser`) | Wave 3 |
| VER-03 | 320 and 390, EN and DE, metrics on every entry, 15 baselines | e2e | `npx playwright test --project=mobile-320 --project=mobile-390`; baselines with `UTF_VISUAL_BASELINES=1 … --grep @visual` | Wave 0/3 |
| FND-07 (closure) | No parity row `pending`; Phase 69 evidence on 26 rows | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | extend |

### Sampling Rate
- **Per task commit:** `npx vitest run <touched test files>` plus `npx eslint <touched files>` (each under 30 s).
- **Per wave merge:** `npm run lint && npm run typecheck && npm test`, plus `npx playwright test tests/e2e/audit --project=chromium` once registry entries exist.
- **Phase gate:** `npm run ci`, the full `npm run test:e2e`, and `npm run test:uat` against the seeded stack (milestone-end real-environment run); then `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `tests/e2e/support/mockApi.ts`, `serverScreens.ts`, `a11y.ts`, `germanLeak.ts`, `visual.ts`, fixtures adapter, `tests/hosting/screen-registry.test.ts` (covers FND-06, VER-02, VER-03)
- [ ] `tests/ratchets/*.json` and ratchet/hard-coded-text tests, ESLint config (covers CON-01..03, L10N-03)
- [ ] `tests/e2e/fonts/` vendored fonts + manifest test (covers VER-03)
- [ ] `src/app/urlParameters.ts`, `tests/app/url-parameters.test.ts`, `tests/app/route-links.test.ts` (covers VER-01)
- [ ] `tests/state-views.test.tsx`, `tests/status-tones.test.ts`
- [ ] Framework install: none (all pinned and installed)

## Security Domain

`security_enforcement` is not set to false in this research's inputs; included.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no auth change; seeded stack uses the dev login bypass) | — |
| V3 Session Management | no | — |
| V4 Access Control | yes (forbidden states, `AccessBoundary`) | Keep server-side authorization authoritative; `AccessBoundary` is presentation only; keep existing `authorization.ts` tests green |
| V5 Input Validation | yes (converted form controls) | Existing zod/react-hook-form validation must survive control conversion; no new input handling |
| V6 Cryptography | no | — |
| V14 Configuration / supply chain | yes | No new dependencies (D-18); vendored fonts hashed in a manifest; Playwright and axe pinned exactly |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Seeded journeys pointed at a real environment through `UAT_BASE_URL` | Tampering | Hostname guard in `uat.ts` (localhost/127.0.0.1 only), F17 |
| Secrets or tokens in committed fixtures, screenshots or traces | Information disclosure | Fixtures use synthetic ids; `trace: "on-first-retry"`/`retain-on-failure` artifacts stay out of git (`playwright-report`, `test-results` are ignored; add `test-results` check) |
| Tampered vendored test fonts | Tampering | SHA-256 manifest test |
| `tests/` support code or screenshot assets leaking into the production bundle | Information disclosure | Tests live outside `src`; `vite build` only bundles `src`; no `import` from `tests/` in `src` (add a guard) |
| Client-only forbidden state mistaken for authorization | Elevation of privilege | State copy and docs say server enforces; no new client trust |

## Sources

### Primary (HIGH confidence)
- Local code and tooling executed or read this session: `sources/ReactApp/package.json`, `playwright.config.ts`, `playwright.uat.config.ts`, `eslint.config.js`, `vitest.config.ts`, `scripts/check-locales.mjs`, `src/components/{primitives,Disclosure,ActionControl,FormFields,commandNavigation}.tsx`, `src/app/routes.ts`, `src/styles/tokens.css`, `src/styles.css` (selected ranges), `tests/hosting/*`, `tests/e2e/*`, `tests/uat/support/uat.ts`, `.github/workflows/dotnet.yml`, `.mozaic/{pipeline.yml,check-browser.sh,react-browser.Containerfile}`, `scripts/Start-Uat.ps1`, `sources/Api/Data/UatFleetScenarioSeeder.cs`, `sources/AppHost/Program.cs`, `61-PARITY-MATRIX.md`, `68-*` plan front matter.
- `require('axe-core')` in `sources/ReactApp`: version 4.13.0, `wcag22aa` rules = `target-size`.
- ESLint probe runs (temporary configs, deleted): 529 raw elements/76 files; 280 look-alikes/52 files; 32 literal-text hits/11 files.
- [Playwright test-snapshots](https://playwright.dev/docs/test-snapshots) and [TestConfig](https://playwright.dev/docs/api/class-testconfig) (snapshot platform differences, `snapshotPathTemplate` tokens, `toHaveScreenshot` options), [Clock API](https://playwright.dev/docs/api/class-clock) (`setFixedTime`).

### Secondary (MEDIUM confidence)
- [Font Squirrel Roboto license page](https://www.fontsquirrel.com/license/roboto) and web search on Roboto license (versions differ: Apache 2.0 vs SIL OFL 1.1).

### Tertiary (LOW confidence)
- CI runtime estimate, cross-environment pixel parity, German function-word judgments (see Assumptions Log).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages, versions read from `package.json`
- Architecture and slicing: HIGH for file ownership and counts, MEDIUM for plan size estimates
- Pitfalls: HIGH for the code-derived ones (F2, F3, F4, F6, F11, F12), MEDIUM for CI timing and pixel parity (F1)

**Research date:** 2026-11-22
**Valid until:** 2026-12-22 for stack facts; re-run the raw-control, literal-text and locale counts after Phase 68 merges, since 68-14…68-19 change Tier A files.
