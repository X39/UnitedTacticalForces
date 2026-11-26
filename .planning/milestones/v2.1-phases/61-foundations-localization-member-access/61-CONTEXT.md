# Phase 61: Foundations, Localization & Member Access - Context

**Gathered:** 2026-11-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Members can reach the server section and trust what it shows and asks of them. This phase builds the shared
foundations that Phases 62-69 consume: one shared server state/cost model, one cost confirmation for every
cost-bearing action, React-owned English/German JSON with a build-failing parity check, the `--utf-*` design tokens, a
parity matrix of existing server capabilities, and the `sv` / `sv:view` member-access path with
disabled-with-reason actions.

It does NOT redesign the fleet or workspace layout (Phase 62/63). It makes existing screens use the shared pieces.
Scope is `sources/ReactApp` only; `sources/WebApp`, generated Kiota output, and `sources/Common/Ui/routes.json` are
untouched. Requirements: FND-01, FND-02, FND-03, FND-04, FND-05, FND-07, L10N-01, L10N-02, ACC-01, ACC-03.

</domain>

<decisions>
## Implementation Decisions

The approved `61-UI-SPEC.md` is the locked design contract. The user ran discuss after the UI-SPEC step and chose to
accept the UI-SPEC plus the defaults below without further questioning.

### Locked by UI-SPEC (do not re-ask; read the spec for detail)
- **D-01:** State and cost come from one pure function `deriveServerState` in `@/features/hosting/serverState`; no
  component compares `deployment.status` itself. Property-tested invariant: `possibleCost` true never yields `off` or
  cost `none`; `off` always yields cost `none`.
- **D-02:** Notifications join to the fleet query by `serverId` (same React Query key, no new endpoint) and render
  `ServerStateBadge` + `CostIndicator`; `statusLabel` is never rendered. No API changes.
- **D-03:** Every `StartsCost`/`StopsCost` action (start, stop, terminate) opens `CostConfirmDialog` via
  `useCostConfirmation().request({ serverId, actionId, source })`. Entry points converted: profile-menu cost-stop,
  fleet card terminate link (still navigates to `?action=terminate`), workspace primary action and `ServerActions`,
  `?action=` deep link (now opens for every found, enabled, non-pending action, including `start`).
- **D-04:** Dialog reuses and additively extends `ConfirmDialog`; initial focus on dismiss; outcome-specific dismiss
  labels per action (`Server_Action_{ActionId}_Dismiss`), never a generic Cancel.
- **D-05:** Disabled actions use `aria-disabled` plus a visible, `aria-describedby`-linked reason via the additive
  `unavailableReason` capability of `ActionControl` and `UnavailableReason` (owns the reason-to-copy mapping; unknown
  API text is quoted in a `lang="en"` span).
- **D-06:** Locale source of truth is hand-edited `src/locales/en.json` and `de.json` (flat keys, `Server_{Area}_{Item}`
  convention for new keys). RESX parsing and `locales:generate` are removed. `scripts/check-locales.mjs` runs in
  `build`, `build:assets`, and `ci`, failing on missing/empty German, orphan German, and placeholder mismatch.
- **D-07:** Tokens live in `src/styles/tokens.css` (additive `--utf-*` layer, imported before `styles.css`). A contrast
  unit test and a style-guard test (no raw hex or pixel spacing literals in `components/server/*`) are required.
- **D-08:** Parity matrix lives at `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md`
  with columns `ID, Capability, Today, Status, New location, Phase, Evidence`; statuses `pending|kept|relocated|removed`.
- **D-09:** Fleet page gains `HostingAccessBoundary`; a user with no server claim does not issue the fleet query. A bare
  `sv`/`sv:view` member gets read experience with operate actions disabled-with-reason and the Create control disabled
  with a reason.

### Locale seeding (default chosen)
- **D-10:** Seed `en.json`/`de.json` once from the current generated output (6,055 keys). Drop unreferenced keys.
  Every still-referenced key whose German value equals English (~628 today) receives a real German value, unless it is
  listed in `src/locales/identical-allowed.json` with a reason (proper nouns, "Server"). Identical-value check stays a
  warning in this phase and becomes a failure in Phase 69. — **Reversibility:** costly — key names are referenced by
  every component, so renaming later touches many call sites.

### ACC-01 proof (default chosen)
- **D-11:** Prove a bare `sv` and a bare `sv:view` holder end to end with an `Api.Tests` integration test asserting the
  real list-endpoint response (servers returned match view permission), plus a React-side check that the navigation,
  fleet, and boundary behave for both claims. UI-only mocks do not satisfy ACC-01. If the API list endpoint is found to
  filter wrongly for these claims, a minimal API fix is in scope for this phase because ACC-01 cannot be met without it;
  keep it inside the existing `HasServerPermission` boundary and do not broaden contracts.

### Parity matrix (default chosen)
- **D-12:** One row per capability (per workspace tab, `hostingService` method, projected action id, fleet entry point,
  create-form field group), seeded from the UI-SPEC inventory and completed by reading the code. Rows are never
  deleted; `removed` requires a written reason and explicit user sign-off recorded in `Evidence`. The completeness
  unit test (every `WorkspaceTab` id and `hostingService` method has a row, no empty status) runs in CI from this phase.

### Rollout and cleanup (default chosen)
- **D-13:** `ActionConfirmation` and `ServerStatus` are replaced in one cut by `CostConfirmDialog` and
  `ServerStateBadge`, with no long-lived compatibility shim. Test fixtures modelling Start as `dangerous: true` are
  corrected to `dangerous: false` (the dialog must still open). Copy entries used by shared server components move
  from `copy.ts` into the JSON; `copy.ts` otherwise shrinks only as screens are redesigned and must be gone for
  redesigned screens by Phase 69.

### Claude's Discretion
- Exact Fluent icon glyph per state (fixed per state and tested), file/module layout under
  `src/components/server/`, test-runner choice for the axe scan (jest-axe or `@axe-core/playwright`), and the
  mechanics of the check scripts.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase design contract
- `.planning/phases/61-foundations-localization-member-access/61-UI-SPEC.md` - Approved visual, copy, state-model,
  confirmation, deep-link, access, localization, token, parity, and accessibility contract (authoritative).

### Requirements and roadmap
- `.planning/ROADMAP.md` - Phase 61 goal and five success criteria.
- `.planning/REQUIREMENTS.md` - FND-01..05, FND-07, L10N-01/02, ACC-01/03 (FND-06, L10N-03 close in Phase 69).
- `.planning/PROJECT.md` - v2.1 scope, React-only constraint, backend-addition limits.
- `.planning/STATE.md` - Current position and deferred verification items.

### Codebase maps and project rules
- `.planning/codebase/ARCHITECTURE.md`, `CONVENTIONS.md`, `STRUCTURE.md`, `TESTING.md`, `CONCERNS.md`
- `AGENTS.md` - Architecture boundaries, English-only identifiers, one type per file, commit format.
- `docs/features/react-ui-beta.md` - React app structure and `/next/` hosting.
- `docs/features/cloud-based-game-server-hosting/` - Hosting feature pack (control plane boundaries).

### Code touchpoints
- `sources/ReactApp/src/auth/claims.ts`, `sources/ReactApp/src/features/hosting/authorization.ts` - Claim checks
  (`sv`, `sv:view`).
- `sources/Claims/Claims.cs` - Claim constants (`Server.All = "sv"`, `Server.View = "sv:view"`).
- `sources/ReactApp/scripts/export-locales.mjs`, `src/generated/locales/*`, `src/app/i18n.ts`,
  `src/app/localizationTypes.ts`, `src/i18next.d.ts` - Localization pipeline to replace.
- `sources/ReactApp/src/features/hosting/copy.ts` - Second copy mechanism to retire incrementally.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/components/ActionControl.tsx` (ActionButton/Link/Anchor): extend additively with `unavailableReason`.
- `src/components/primitives.tsx` (`ConfirmDialog`, `StatusBadge`, `StatePanel`, `PageHeader`): extend `ConfirmDialog`
  additively (`confirmVariant`, `children`, `initialFocus`).
- `src/components/AppToasts.tsx`: success feedback after a request is accepted.
- `src/features/hosting/components.tsx` (`HostingAccessBoundary`, `ServerStatus`, `ServerCard`, `ActionConfirmation`,
  `ServerActions`): adapt or replace per UI-SPEC.
- `src/features/hosting/model.test.ts`: existing model tests and fixtures to correct.

### Established Patterns
- Fluent UI React v9 only for `FluentProvider`, theme, `Spinner`, `Select`; buttons and panels are project primitives.
- React Query for data; notifications and fleet share query keys.
- Flat i18next keys with `_` separators (no dots); `formatResource` handles legacy `{0}` placeholders.

### Integration Points
- `sources/ReactApp/src/main.tsx` imports `tokens.css` before `styles.css`.
- `package.json` scripts: wire `locales:check` into `build`, `build:assets`, `ci`; remove `locales:generate` from
  `generate`.
- API list endpoint and `HasServerPermission` in `sources/Api` for the ACC-01 integration test (`tests/Api.Tests`).

</code_context>

<specifics>
## Specific Ideas

- "Shut down and stop billing" is the single cost-stop phrase everywhere; Phase 63 extends but must not rename it.
- A server that may still cost is never labelled off, stopped, or terminated.

</specifics>

<deferred>
## Deferred Ideas

- Fleet card grid overflow at many servers: Phase 62 owns fleet layout.
- Stable API reason code replacing exact-string reason matching: Phase 63.
- Global `--quiet` contrast fix and identical-EN/DE warning promoted to failure: Phase 69.
- Full Playwright mobile projects and German 320/390 visual baselines: Phase 69.
- Create-and-start confirmation (`source: create`): Phase 64.

</deferred>

---

*Phase: 61-Foundations, Localization & Member Access*
*Context gathered: 2026-11-18*
