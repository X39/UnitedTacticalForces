# Stack Research

**Domain:** Server-operations UI redesign inside an existing React 19 / Vite 8 / Fluent UI v9 app (`sources/ReactApp`)
**Milestone:** v2.1 Server Experience Reimagined
**Researched:** 2026-11-18
**Confidence:** HIGH for "what is already installed and how it is wired" (read from the repo and `node_modules`); MEDIUM for the few additions (npm registry versions verified; vendor docs only partly retrievable; Context7 MCP was not available in this session)

## Verdict

**The redesign needs no new runtime dependency.** Every capability it needs (theming, layout, navigation, tabs,
dialogs, motion, forms, data fetching) is already installed or is plain CSS/HTML. Add only:

1. `@axe-core/playwright` (dev) for accessibility checks in the Playwright suite that already runs in CI.
2. Playwright's built-in `toHaveScreenshot` for visual regression. This is a config change, not a package.
3. `eslint-plugin-jsx-a11y` (dev), scoped to the redesigned folders. Optional but cheap.
4. Self-hosted fonts via `@fontsource*` (runtime). Recommended, because visual regression is not deterministic
   without it. It also removes a Google Fonts call that is a GDPR liability for a German association.

Everything else you might reach for (motion library, component kit, table library, Storybook, Chromatic) should be
rejected. See "What NOT to Use".

## Critical Finding: The "Fluent UI app" is really a custom-CSS app

This changes how the redesign should be planned.

| Observation | Evidence |
|-------------|----------|
| Fluent components used in the whole app: `Spinner`, `Select`, `FluentProvider` only | grep of `@fluentui/react-components` imports in `src/` |
| Fluent theme is `webDarkTheme` plus about 10 token overrides (brand gold, 3 neutral backgrounds, 2 foregrounds, 1 stroke) | `src/app/App.tsx` |
| The real design system is hand-written CSS custom properties on `:root` (`--ink`, `--gold`, `--surface*`, `--stroke*`, `--success/warning/danger/info`, `--control-height`, `--radius`), plus compatibility aliases (`--text`, `--line`, `--border`) | `src/styles.css` (about line 1457) |
| About 2,760 lines of CSS in two files for the command center and hosting; the shell, nav drawer, buttons, tabs, dialogs, toasts, and state panels are all custom | `hosting.css` 2,428 lines, `commandCenter.css` 337 lines, `ActionControl.tsx`, `primitives.tsx`, `CommandCenterLayout.tsx` |
| Tabs are already custom `role="tablist"` and URL-driven (`?tab=`); dialogs use native `<dialog>` | `ServerWorkspacePage.tsx`, `FinanceDialog.tsx` |
| Vitest aliases `@fluentui/react-components` and `@fluentui/react-icons` to hand-written mocks (22 and 57 lines) | `vitest.config.ts`, `tests/mocks/*` |

**Recommendation (opinionated):** treat the existing CSS token layer as the single source of truth for the redesign
and evolve it. Do not migrate the server section to Fluent components wholesale. Doing so would put two visual
systems side by side, and the rest of the app (which gets only a "light consistency pass") would not match. The
Fluent theme override is nearly cosmetic today because almost nothing renders Fluent components.

### What to do about tokens (no new package)

- Add a **semantic token tier** on top of the raw palette, in one new `tokens.css` imported before the feature CSS.
  The server UI needs tokens that do not exist yet:
  - Server state colors: `--state-running`, `--state-starting`, `--state-stopped`, `--state-disabled`,
    `--state-error`, `--state-teardown`. Pair each with an icon and a label so state never relies on color alone.
  - Cost-signal tokens, for example `--cost-active`, to mark paid and cost-bearing states.
  - A spacing scale (`--space-1` to `--space-8`), an elevation scale (replace the single `--shadow`), a type scale,
    and `--z-*` layers.
  - Motion tokens: `--motion-fast: 120ms`, `--motion-base: 200ms`, `--ease-standard`.
- Retire the "Compatibility aliases" block gradually. Do not mass-rename inside certified slices.
- Keep `FluentProvider` and the tacticalTheme. Make its values derive from the same constants, or document that
  the two are kept in sync, so a future Fluent component does not render with blue defaults.

### If and when a Fluent interactive component is adopted

Fluent's value is accessible behavior that is expensive to hand-build: combobox or tag-picker multi-select,
anchored popovers and menus, tooltips. Use it only for those, and only after these steps:

1. **Generate a full brand ramp.** `createDarkTheme(brandVariants)` needs a 16-step ramp (10 to 160). Generate it
   once from `#baa05a` with the Fluent Theme Designer and commit the ramp as a constant. This is not a dependency.
   Overriding 10 tokens on `webDarkTheme` leaves hover, pressed, compound-brand and selected states blue.
2. **Fix the test mock.** Vitest replaces `@fluentui/react-components` with a 3-export stub. A new Fluent import
   resolves to `undefined` in tests and fails at render. Either delete the alias for `react-components` so real
   components run in jsdom, or extend the stub per component. Prefer deleting the alias, because a stub hides real
   behavior (focus, portals, ARIA) that the redesign wants tested.
3. **Icons.** `@fluentui/react-icons` is aliased to a whitelist stub (`tests/mocks/react-icons.tsx`). Every new icon
   import must be added there, or the unit test for that screen throws. Budget this into each phase that adds icons.
   Server-state iconography will add many.
4. Candidate adoption points (decide during phase research, not now): the mod picker for "add a mod to a Minecraft
   server" (Fluent `TagPicker` or `Combobox`), and contextual row-action menus (`Menu`).

## Recommended Stack

### Core Technologies (all already installed; keep pinned)

| Technology | Installed | Latest on npm | Purpose in redesign | Why keep as is |
|------------|-----------|---------------|---------------------|----------------|
| React | 19.2.8 | n/a | UI runtime | No new capability needed. Use `useTransition` and `useOptimistic` only if a flow needs them. |
| Vite | 8.2.1 | n/a | Build | No change. |
| `@fluentui/react-components` | 9.74.6 | 9.75.2 | Theme provider plus selective interactive primitives | Already contains Nav, Drawer, TagPicker, Skeleton, MessageBar, Breadcrumb, Toolbar, Tree, motion. No upgrade needed. A minor bump gives nothing the redesign needs. |
| `@fluentui/react-icons` | 2.0.320 | 2.0.346 | Server-state and action iconography | Large set. No change. |
| `@tanstack/react-query` | 5.101.4 | 5.108.0 | Server state | Already used with `keepPreviousData` and `refetchInterval`. See patterns below. |
| `react-router` | 7.18.2 | 8.5.1 | Navigation and layout routes | Stay on 7.x. A major bump is unrelated to the redesign and risky given the certified route manifest (`routeManifest`, `RouteCertificationGate`). |
| `react-hook-form` + `zod` + `@hookform/resolvers` | 7.85.0 / 4.4.3 / 5.2.2 | n/a | Create-server and configuration forms | Already the form stack. Build any stepper or wizard feel with these. |
| `react-i18next` | 17.0.11 | n/a | EN/DE copy | Constraint: all strings via RESX-driven locale resources. |

### Supporting Libraries (the only additions)

| Library | Version | Type | Purpose | When to Use |
|---------|---------|------|---------|-------------|
| `@axe-core/playwright` | 4.14.0 | devDependency | Automated WCAG scans inside existing e2e specs | Always. Peer is `playwright-core >= 1.0.0` (satisfied by 1.62.1); pulls `axe-core ~4.14.0`. |
| `eslint-plugin-jsx-a11y` | 6.10.2 | devDependency | Static a11y lint (div-with-onClick, missing labels, bad ARIA roles) | Scope to `src/features/hosting/**` and `src/components/**` first. A global `--max-warnings 0` rollout would fail on existing code. Peer range includes ESLint 9, and the repo is on 9.39.2. |
| `@fontsource-variable/roboto` | 5.3.0 | dependency | Self-hosted body font (variable) | Recommended. Replaces the Google Fonts `<link>` in `index.html`. |
| `@fontsource/barlow-condensed` | 5.3.0 | dependency | Self-hosted label font; import only weights 500, 600, 700, 800 | Recommended. `--font-label` uses it. Import only the weights the CSS uses to keep the bundle small. |
| `@fluentui/react-motion-components-preview` | 0.15.7 (already installed transitively; do not add a second version) | optional direct dependency | `Collapse`, `Fade`, `Slide`, `Scale` presence components | Only if a feature needs animated mount and unmount, such as an expanding cost-breakdown or a teardown confirmation. Otherwise use CSS. Preview API, so pin it exactly. |
| `@tanstack/react-virtual` | 3.14.26 | conditional | Windowed log list | Only if the Logs panel is shown to render thousands of rows and stall. Measure first. Do not add speculatively. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `@playwright/test` 1.62.1 (installed) | E2E, visual regression (`toHaveScreenshot`), axe host | **Do not bump.** `.mozaic/check-browser.sh` pins staged browser dependencies to exactly `react-browser) version=1.62.1`. A bump needs a coordinated CI change. 1.65.1 exists upstream. |
| Vitest 4.1.10 + Testing Library (installed) | Unit and component tests | Keep. Do not add `vitest-axe` (see below). |
| ESLint 9.39.2 + `eslint-plugin-react-hooks` (installed) | Lint | Add the jsx-a11y flat config only to the scoped file globs. |

## Capability-by-Capability Answers

### Fluent theming and design tokens
Use the existing CSS custom-property system plus a new semantic tier (see above). No package. Optionally commit a
16-step Fluent brand ramp for `#baa05a`. Do not introduce CSS-in-JS beyond Griffel, which comes bundled with Fluent.
Do not introduce Tailwind, vanilla-extract, or style-dictionary. The token set is about 40 variables and does not
justify a pipeline.

### Layout and navigation primitives
Nothing new.
- **Shell:** the existing `CommandCenterLayout`, nav drawer, and `useResponsiveNavigation` are sound. The redesign
  work is information architecture (what is in the nav, how servers relate to configuration profiles and finance),
  not a new library.
- **Connected navigation:** react-router `NavLink`, nested routes, and route `handle` metadata plus `useMatches` to
  build breadcrumbs ("Servers / UTF Test Server / Mods"). Write a small `Breadcrumbs` component in
  `src/components`. Fluent `Breadcrumb` is available but unnecessary.
- **Tabs:** keep the URL-driven `?tab=` approach already in `ServerWorkspacePage` so every tab is deep-linkable and
  shareable. This directly serves "disconnected feel". Fix UI-60-001 (mobile tab clipping) in CSS:
  `overflow-x: auto`, `scroll-snap-type`, and `scrollbar-width: none` on the tablist, plus
  `scrollIntoView({ inline: "center" })` on the selected tab. No overflow library.
- **Responsive layout:** CSS container queries (baseline in all current evergreen browsers) for cards and panels,
  with media queries only for the shell. No JS layout library.
- **Stepper or create flow:** one react-hook-form instance with a zod schema and sections, or route-per-step. No
  wizard library.

### Data and state clarity (TanStack Query, already installed)
- `refetchInterval` while a lifecycle job is non-terminal, switched off at terminal states, so state is live
  without a new push channel. The existing `ProfileMenu` poll shows the pattern.
- `placeholderData: keepPreviousData` on list and filter views (already used in admin lists).
- `queryClient.prefetchQuery` on card hover or focus to make fleet to workspace navigation feel instant.
- `useMutation` with `onMutate` optimistic state for "Start server", so the card moves to "Starting" immediately.
  Roll back on error.
- Do not add Suspense data loaders or route loaders. They would conflict with the existing `lazy` plus `Component`
  route pattern and the certification gate.

### Motion
Use CSS transitions and keyframes driven by the new `--motion-*` tokens. Wrap non-essential motion in
`@media (prefers-reduced-motion: no-preference)`. The repo already has three `prefers-reduced-motion: reduce`
blocks (`commandCenter.css`, `ProfileMenu.css`, `modPacks.css`), so keep that convention. Motion is for state
change feedback (status pill cross-fade, skeleton shimmer, panel expand), not decoration. Fluent's `react-motion`
already honors reduced motion (`useIsReducedMotion` in the installed source), so Fluent presence components are
safe if used. Use `Skeleton` from Fluent or a CSS shimmer for the loading state. Do not add `motion` (14.14.0) or
`framer-motion`.

### Visual regression and screenshot testing
Use Playwright's built-in `toHaveScreenshot`. No service, no new package. Required configuration decisions:

- **Baseline OS.** Playwright baseline filenames include browser and OS (`-chromium-linux.png`). CI runs in a Linux
  container, and developers are on Windows. Generate and commit baselines from CI only. Add a dedicated `visual`
  Playwright project, skipped when `process.platform !== "linux"`, and set `snapshotPathTemplate` so local
  Windows runs cannot create `-win32` files that CI never reads.
- **Determinism.**
  - Fonts must be self-hosted (see `@fontsource*`), because `index.html` currently loads Google Fonts from a CDN.
    That is not guaranteed reachable in CI, and the CI egress allowlist in `.mozaic/pipeline.yml` lists only
    `registry.npmjs.org` plus the Playwright CDNs.
  - Use the existing `page.route` fixtures (see `tests/e2e/public-shell.spec.ts`, `hosting-placement.spec.ts`).
  - Use `animations: "disabled"`, `maxDiffPixelRatio: 0.01`, and `mask` for timestamps and elapsed-time text.
  - Move the pointer away before capture.
- **Scope.** Keep it small, about 10 to 15 shots: fleet with mixed states (running, disabled, error), create flow,
  workspace in each major state, teardown confirmation, and the same at a 390px viewport (the UI-60-001 regression
  case). Visual tests that cover everything become noise.
- **Output.** `test-results/` is already git-ignored. Decide whether the snapshot directory is committed (the usual
  approach). If the CI runner is read-only for commits, upload diffs as a CI artifact and update baselines via a
  manual pipeline run.

### Accessibility tooling
- `@axe-core/playwright` in a `makeAxeBuilder` fixture (Playwright's documented pattern) with
  `withTags(["wcag2a","wcag2aa","wcag21a","wcag21aa"])`. Run it on each key server screen in its main states,
  and again with an open dialog and an open menu. It runs in a real browser, so it catches **color-contrast**
  failures. That matters on a dark theme with a gold brand (`#baa05a`) and muted text (`--quiet: #778190`, which is
  worth checking explicitly).
- `eslint-plugin-jsx-a11y` `recommended` flat config on the scoped globs. It targets the custom tablist, dialog, and
  button-like patterns the project has hand-built.
- Manual checks remain required: keyboard-only walk-through of start-server and add-mod, and a screen-reader
  pass over status announcements. Use `role="status"` or `aria-live="polite"` for lifecycle state changes. Automated
  tooling will not cover this.

## Installation

```bash
# From sources/ReactApp (versions pinned exactly, matching the existing package.json convention)
npm install --save-exact @fontsource-variable/roboto@5.3.0 @fontsource/barlow-condensed@5.3.0

# Dev
npm install --save-exact -D @axe-core/playwright@4.14.0 eslint-plugin-jsx-a11y@6.10.2
```

Then, in code:

```ts
// src/main.tsx: replace the Google Fonts <link> in index.html
import "@fontsource-variable/roboto";
import "@fontsource/barlow-condensed/500.css";
import "@fontsource/barlow-condensed/600.css";
import "@fontsource/barlow-condensed/700.css";
import "@fontsource/barlow-condensed/800.css";
```

The `--font-body` and `--font-display` variables list `Roboto`. With the variable package the family name is
`"Roboto Variable"`, so update those variables and the `font-family` in `:root`. Check the CSP and the
`preconnect` links in `index.html` when removing Google Fonts.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Evolve the custom CSS token layer | Rebuild the server UI on Fluent components with a full `createDarkTheme` | Only if the team decides to migrate the whole app to Fluent. That is a different, much larger milestone. |
| Playwright `toHaveScreenshot` | Chromatic or Percy, with Storybook | If a component catalog is wanted for its own sake. There is no Storybook today, and these need external egress and secrets that the hermetic CI forbids. |
| `@axe-core/playwright` | `vitest-axe` 0.1.0 | Never here. jsdom cannot evaluate color contrast, and the package is 0.x. |
| CSS transitions + reduced-motion guards | Fluent `react-motion-components-preview` | When you need exit animations on unmounting elements, which CSS cannot do without extra state. |
| Self-hosted `@fontsource` | Keep Google Fonts CDN | Never for CI screenshots. Also a GDPR exposure for a German Verein. |
| Hand-built breadcrumbs from route `handle` | Fluent `Breadcrumb` | If overflow collapsing at narrow widths is required. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `motion` / `framer-motion` (14.14.0) | Adds a large runtime for effects CSS handles. Needs its own reduced-motion handling. | CSS + tokens, or Fluent presence components if exit animations are needed |
| MUI, Chakra, shadcn/ui, Tailwind | A second design system next to Fluent and 2.7k lines of existing CSS. It breaks "consistent visual standard". | Existing tokens and primitives |
| `@tanstack/react-table` 9.x | A new major, and unnecessary: the fleet and logs are cards and lists, not data grids. | Plain semantic markup, or Fluent `Table` if a real grid appears |
| `@fluentui/react-virtualizer` | Alpha (9.0.0-alpha.116) | `@tanstack/react-virtual` if virtualization is ever needed |
| Storybook | A new build target, with no CI job and no consumer in this milestone | Playwright screenshots of real routes with fixtures |
| Bumping Playwright (1.65.1) | CI browser deps are pinned to 1.62.1 | Stay on 1.62.1 until `.mozaic/check-browser.sh` is updated deliberately |
| Bumping `react-router` to 8.x or `react-components` for "freshness" | No redesign benefit. Risk to certified routes and snapshot stability. | Keep pins |
| New wizard, stepper, toast, or modal libraries | `AppToasts`, native `<dialog>`, and RHF already cover these | Existing components |
| Editing `src/generated/**` or the Kiota client | Generated output. Changes belong in the OpenAPI source. | Wrapper hooks in `features/hosting/services.ts` |

## Stack Patterns by Variant

**If a new screen only needs layout, status, and buttons (most of the redesign):**
- Use `PageHeader`, `StatePanel`, `ActionButton`/`ActionLink`, plus new tokens.
- Because these are tested, themed, and already consistent with the rest of the app.

**If a screen needs multi-select with search (mods) or an anchored menu:**
- Adopt the matching Fluent primitive, after the three prerequisites above (brand ramp, remove the test alias,
  register icons).
- Because hand-rolled listbox and combobox semantics are the most common source of a11y regressions.

**If a state change needs an exit animation:**
- Use Fluent `Collapse` or `Fade` from `react-motion-components-preview`.
- Because it ships reduced-motion handling and presence logic, so there is no extra mount-state code.

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| `@axe-core/playwright@4.14.0` | `@playwright/test@1.62.1` | Peer is `playwright-core >= 1.0.0`. Works with the pinned version. |
| `eslint-plugin-jsx-a11y@6.10.2` | `eslint@9.39.2` | Peer range includes `^9`. Use `jsxA11y.flatConfigs.recommended`. |
| `@fluentui/react-motion-components-preview@0.15.7` | `@fluentui/react-components@9.74.6` | Already resolved transitively via dialog, drawer, menu, accordion. A direct import must resolve to that single copy (check `npm ls`). |
| `@playwright/test@1.62.1` | `.mozaic/check-browser.sh` | Exact-version coupling to staged browser libs. |
| Vitest mocks | Any new Fluent import or new `@fluentui/react-icons` icon | Must be reflected in `tests/mocks/*` or the alias removed. |

## Open Items for Phase-Level Research

- Whether the CI runner can commit or upload Playwright baselines, and how snapshot updates are approved. Check
  `.mozaic/pipeline.yml` and `check-ordinary.sh` when the visual-regression phase is planned.
- Contrast audit of `--quiet`, `--muted`, and state colors on `--surface` and `--canvas`. The theme is a gold on
  near-black dark theme, so automated contrast results should be reviewed before choosing the final state palette.
- Whether the Logs panel needs virtualization (measure with a realistic log volume).
- Whether the mod-add flow should adopt Fluent `TagPicker`. Decide once the mod data shape and the capability
  projection are reviewed.
- A light theme is not in scope. The app is dark-only (`color-scheme: dark`). Do not generate a light ramp.

## Sources

- `sources/ReactApp/package.json`, `vitest.config.ts`, `playwright.config.ts`, `eslint.config.js`, `index.html`,
  `src/app/App.tsx`, `src/styles.css`, `src/components/*`, `src/features/hosting/*`, `tests/mocks/*`: HIGH
  (read directly).
- `.mozaic/pipeline.yml`, `.mozaic/build.sh`, `.mozaic/check-browser.sh`: HIGH (Playwright version pin, network
  allowlist).
- Installed `node_modules/@fluentui/react-components@9.74.6` type definitions and `react-motion` source: HIGH
  (Nav, Drawer, TagPicker, Skeleton, motion exports present; `useIsReducedMotion` present).
- npm registry (`npm view`) on 2026-11-18: HIGH for latest versions of `@axe-core/playwright` 4.14.0,
  `eslint-plugin-jsx-a11y` 6.10.2, `@fontsource-variable/roboto` 5.3.0, `@fontsource/barlow-condensed` 5.3.0,
  `@fluentui/react-components` 9.75.2, `@playwright/test` 1.65.1, `react-router` 8.5.1.
- https://playwright.dev/docs/test-snapshots: MEDIUM (platform-suffixed baselines, `maxDiffPixels` and
  `threshold`; the fetch summary did not confirm `animations` defaults or masking).
- https://playwright.dev/docs/accessibility-testing: MEDIUM (AxeBuilder fixture pattern, `withTags`).
- https://github.com/microsoft/fluentui motion-components-preview README: MEDIUM (component list; marked beta).
- Not verified: Fluent theming docs (the Storybook page did not render through the fetch tool). The
  `createDarkTheme` 16-step brand ramp requirement comes from the installed type definitions plus prior knowledge:
  MEDIUM.
- Context7 MCP was unavailable in this session, so library docs were not cross-checked through it.

---
*Stack research for: React server-operations UI redesign (v2.1)*
*Researched: 2026-11-18*
