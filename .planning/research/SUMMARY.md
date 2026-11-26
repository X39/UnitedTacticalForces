# Project Research Summary

**Project:** United Tactical Forces - v2.1 Server Experience Reimagined
**Domain:** Member-first operations UI for cost-bearing, owner-attributed cloud game servers (React app `sources/ReactApp`, UI-only milestone)
**Researched:** 2026-11-18
**Confidence:** MEDIUM-HIGH (repo findings HIGH; competitor and UX-practice findings MEDIUM)

## Executive Summary

v2.1 is a presentation-layer redesign of the server section (fleet, create, workspace, mods/config, cost and teardown) inside the existing React 19 / Vite app, plus a bounded consistency pass over the rest of the app. The real design system is hand-written CSS custom properties and native elements; Fluent supplies only a theme, `Spinner` and one `Select`. Game panels (Pterodactyl, Crafty, AMP, Aternos) lead with one obvious power action and plain status; cloud consoles (Hetzner, Fly, Railway, Vercel) add cost visibility and review-before-apply. UTF is a non-expert panel with real money attached, so cloud-console patterns matter most: cost owner, rate, "costing now", and an explicit cost-stop.

Recommended approach: keep the URL contract (three server routes, `?tab=` and `?action=`, plus new `?path=`) and the domain core (`services.ts`, `types.ts`, `model.ts`, `createServerModel.ts`, `authorization.ts`). Rebuild presentation on one new pure module, `serverState.ts`, so fleet card, header, overview and notifications derive a single state with a separate billing axis. Add shared primitives under `src/components` using existing tokens plus a semantic token tier. Strangler migration for the workspace. No new runtime UI dependency; only `@axe-core/playwright`, `eslint-plugin-jsx-a11y`, self-hosted fonts, and Playwright's built-in screenshots.

Key risks: Start is a one-click cost event (API marks Start non-confirming; the UI dialog is the only guard); friendly status labels hide "still billing"; capabilities silently lost in the rewrite; English leaking into German UI (three copy mechanisms, RESX outside `sources/ReactApp`); consistency pass ballooning across certified routes.

## Key Findings

### Recommended Stack

No new runtime dependency. Evolve the CSS token layer (state, cost, spacing, elevation, motion in one `tokens.css`) rather than migrating to Fluent components. Keep pins: Playwright 1.62.1 (coupled to `.mozaic/check-browser.sh`), react-router 7.x, Fluent 9.74.6. See `STACK.md`.

**Core technologies:**
- React 19 / Vite / react-router 7: unchanged; the certified route manifest is the risk surface
- Existing CSS tokens plus a new semantic tier
- TanStack Query 5: adaptive polling, prefetch; no optimistic status writes
- `@axe-core/playwright` 4.14.0, `eslint-plugin-jsx-a11y` (dev, scoped)
- Playwright `toHaveScreenshot`: ~10-15 shots, Linux-CI-only baselines
- Self-hosted fonts (`@fontsource-variable/roboto`, `@fontsource/barlow-condensed`)

Rejected: motion/framer-motion, MUI/Tailwind/shadcn, react-table, Storybook, Chromatic, vitest-axe, Playwright or router bumps.

### Expected Features

**Must have (table stakes):**
- One persistent primary action per server on fleet card and workspace header
- Single plain-language member status; deployment stage as secondary detail
- Fleet that answers "what exists, what is on, who pays, what next"; Off servers grouped, never hidden
- Cost visibility everywhere (rate, "costing now", owner); confirmations state cost
- Honest progress strip and failure panel
- Guided create flow with live cost estimate and early frozen-owner check
- Connection info with copy; connected navigation and breadcrumbs; disabled actions show a reason
- Mobile-usable lists and workspace (fixes UI-60-001); EN and DE copy

**Should have (competitive):**
- Cost strip with member-language cost-stop
- "Change this server" flow: review, explicit apply, "takes effect on next start"
- Capability-driven guided mod/mod-pack change
- Create presets; unified Activity timeline

**Defer (v2.2+):** auto-stop/scheduling, accrued cost and alerts, metrics, mod catalog search, server-defined presets, undo, truthful "restart to apply" drift badge (needs API signal).

**Anti-features:** console-first layout, Kill button, flat all-settings form, live-apply of config, provider vocabulary in member UI, redesigning non-server pages.

## Architecture Approach

Keep the three server route patterns and query params so `sources/Common/Ui/routes.json` stays byte-identical and certification tests stay green. Keep pure logic; rebuild presentation. Strangler for the workspace: new header and `SectionNav` first, old panels mounted inside, replace one at a time. Nine tabs collapse into ~seven sections with a legacy-tab map. Delete dead `HostingOverviewPage` and `hosting/routes.tsx`.

**Major components:**
1. `serverState.ts` (new): phase, tone, label, billing exposure, primary action, transitional flag
2. `queries.ts` (new): fleet/workspace hooks, adaptive polling
3. Shared primitives: `SectionNav`, `Breadcrumbs`, `Panel`, `FactList`, `CopyValue`, fields
4. Hosting kit: `ServerStatusBadge`, `ServerActionButton`, `CostActionDialog`, `CostSummary`, `NextActionCard`
5. Rebuilt pages: fleet, create (quick path + Advanced), workspace sections

## Critical Pitfalls

1. **One-click Start** - shared `CostActionDialog` for every entry point incl. `?action=`; fix test fixture that models Start as dangerous.
2. **Status hides billing** - billing axis separate from lifecycle; combinatorial tests.
3. **Capability loss** - parity matrix in phase 1.
4. **Localization** - RESX keys only, German completeness gate, run journeys in `de`.
5. **Optimistic UI vs durable jobs; cost-stop vocabulary** - no optimistic status writes; one RESX vocabulary.

## Implications for Roadmap

### Phase 1: Foundations and guardrails
Decisions, parity matrix, tokens, shared primitives, `serverState.ts`, `queries.ts`, cost dialog, axe fixture, fonts, German gate, dead-code removal.

### Phase 2: Fleet and quick actions
State-first fleet, inline primary action through the cost dialog, `?action=` handling.

### Phase 3: Workspace shell
Header with cost strip, `SectionNav` with legacy map, merged Activity, Cloud and Cost; fixes UI-60-001.

### Phase 4: Create flow (parallel with 3)
Quick path plus Advanced, live estimate, early owner check, keep-off vs start review.

### Phase 5: Content, mods, files, configuration
Capability-driven mods, staged change bar, unsaved-changes guard, `?path=` for files, localized strings.

### Phase 6: Consistency pass, verification, cleanup
Closed-list consistency pass, mobile and German e2e, axe, visual baselines, manifest byte-diff gate.

### Research Flags
- Phase 1: RESX decision, CI baseline approval, contrast audit.
- Phase 3: member vs operator field visibility, "paused" reason code.
- Phase 5: API projection gaps, Minecraft mod mapping, `useBlocker` with search params.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH / MEDIUM | Repo-verified; vendor docs partly unavailable |
| Features | MEDIUM | Competitor layouts partly unconfirmed |
| Architecture | HIGH | Read from code and tests |
| Pitfalls | HIGH / MEDIUM | Repo-specific HIGH |

**Overall confidence:** MEDIUM-HIGH

### Gaps to Address

1. **RESX location** - resource files live in `sources/WebApp/Properties/Language*.resx`; additive keys vs React-owned overlay.
2. **Mods / restart-needed API gaps** - UI-only staged apply with "takes effect on next start" copy; drift badge needs API approval.
3. **Minecraft not defined in API** - only `arma3`, `aska`, `counterstrike2`; validate mod flow with Arma 3 mod packs.
4. **Member claims to reach `/command/servers`** - gated by `canAccessGameServers`.
5. Stable "paused by operator" code; one cost-stop vocabulary; CI baseline approval.

## Sources

### Primary (HIGH confidence)
- `sources/ReactApp`, `sources/Common/Ui/routes.json`, `docs/features/react-ui-beta.md`, API projection code, `.mozaic/` CI scripts, npm registry

### Secondary (MEDIUM confidence)
- Playwright, Fluent motion, AMP, Aternos, Crafty docs

### Tertiary (LOW confidence)
- Unofficial Pterodactyl mirrors and third-party panel guides

---
*Research completed: 2026-11-18*
*Ready for roadmap: yes*
