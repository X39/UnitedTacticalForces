---
phase: 71-server-access-and-cost-owner-ui
plan: 04
subsystem: ui
tags: [react, react-query, settings-panel, access-control, i18n, parity-matrix]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-01 access routes, candidate routes, detail, memberCount and assignablePermissions in the generated client"
provides:
  - "Claim-gated Access panel (anchor server-access) in the server Settings section, after Automation and before Server record"
  - "serverAccessModel.ts: ordered eight-permission catalog and pure access rules"
  - "AccessSubjectRow: closed inline disclosure row per member or role with badges, held-permission summary and no-View warning"
  - "hostingService.serverAccess, serverUserCandidates, serverRoleCandidates, changeServerUserAccess, changeServerRoleAccess"
  - "hostingQueryKeys.access and accessCandidates under the server key; GameServer.canManageAccess; access types"
affects: [71-05, 71-07, 71-08, 71-09, 71-10, 71-11, 71-12, server-access-panel, parity-matrix]

actuals:
  tokens: 18600
  tasks: 3
  commits: 3
plan_head_before: 43dec80f8b1f2cca1d5e9d9f13f756b001fd3841
plan_head_after: 3b5f26de7112ef8cc76dec4a075b9581bfe78dc2

tech-stack:
  added: []
  patterns:
    - "Claim-gated Settings panel split into a gate component (no hooks) and a content component that owns the query"
    - "Query with explicit staleTime 0 and refetchOnWindowFocus true, refetched on a closed-to-open panel flip through a ref updated inside the effect"
    - "Held-permission summary built with Intl.ListFormat formatToParts so unknown ids can render as code nodes"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx
    - sources/ReactApp/src/features/hosting/serverAccessModel.ts
    - sources/ReactApp/src/components/server/AccessSubjectRow.tsx
    - sources/ReactApp/src/components/server/access.css
    - sources/ReactApp/tests/hosting/server-access-panel.test.tsx
    - sources/ReactApp/tests/hosting/server-access-model.test.ts
  modified:
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/app/urlParameters.ts
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/helpers/fleetFixtures.ts
    - sources/ReactApp/tests/helpers/settingsPanels.ts
    - sources/ReactApp/tests/e2e/support/settingsPanels.ts
    - sources/ReactApp/tests/app/url-parameters.test.ts
    - sources/ReactApp/tests/hosting/services.test.ts
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - sources/ReactApp/tests/hosting/settings-section.test.tsx
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "The Access panel is a gate component plus an inner content component, not one component with all hooks first: the content calls useAuth, which throws without an AuthProvider, and dozens of existing Settings tests render SettingsSection without one"
  - "heldSummaryItems is exported beside the planned heldSummaryKeys so an id the catalog does not know can still be shown as raw code in the row summary"
  - "Catalog label and description keys are typed TranslationKey, so a missing locale key fails the typecheck"
  - "canManageAccess is typed optional and normalized to false; the generated GameServerDto gains it through 71-03 in parallel"

patterns-established:
  - "Permission controls read assignablePermissions through canAssign(access, permission), never allowedPermissions (XPF-71-01-1)"
  - "Row open state lives in the panel content keyed user:<id> or role:<id>, so a refetch never closes an open row"

requirements-completed: [D-01, D-02, D-06, D-14]

coverage:
  - id: D1
    description: "Access panel renders after Automation and before Server record only with canManageAccess; without it no heading, summary or request; an archived server keeps it"
    requirement: "D-02"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#renders no heading, summary or request without canManageAccess"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#renders the Access panel after Automation and before the Server record"
        status: pass
      - kind: unit
        ref: "tests/hosting/settings-section.test.tsx#keeps the Access panel and its request on an archived server"
        status: pass
    human_judgment: false
  - id: D2
    description: "Access summary reads Access: {count}, Nobody has been given access at zero, and no count while loading or failed; empty, loading and error states with a real retry"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#lists the members and roles and counts them in the summary"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#reads Nobody has been given access and shows the empty state at zero"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#shows the error state without a count and retries with a real request"
        status: pass
    human_judgment: false
  - id: D3
    description: "Access query keyed under the server with staleTime 0 and refetch on window focus, refetched when the panel is opened by hand, never polled"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#caches the list under the server key with staleTime 0 and refetch on focus"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#refetches when the member opens a collapsed panel by hand, and not on the first render"
        status: pass
    human_judgment: false
  - id: D4
    description: "Subject rows: closed inline disclosure h3 named by the name, kind and You badges, held-permission summary, member count and fingerprint, no-View badge and warning, open row survives a refetch"
    requirement: "D-06"
    verification:
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#summarizes the held permissions, Full control alone, the member fingerprint and the role reach"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#warns about a subject that holds permissions without View and Full control"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-panel.test.tsx#keeps an open row open after the access query refetches"
        status: pass
      - kind: unit
        ref: "tests/hosting/server-access-model.test.ts#needsViewWarning"
        status: pass
    human_judgment: false
  - id: D5
    description: "Whole access data layer (five hostingService methods, keys, types, canManageAccess default) with kept parity matrix rows of phase 71"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/services.test.ts#posts server access grants and revokes to the exact user and role endpoints"
        status: pass
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts#normalizes a legacy server payload without canManageAccess to false"
        status: pass
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#keeps the Phase 71 rows kept with Phase 71 test evidence (FND-07)"
        status: pass
    human_judgment: false
  - id: D6
    description: "Look and feel of the Access panel and its rows in the regenerated visual baselines, the screen reader name of the disclosure rows, and the 320px and German reflow"
    verification: []
    human_judgment: true
    rationale: "Visual quality and assistive-technology speech are not asserted by unit tests; deferred to the milestone verification (Deferred follow-up) as the UI-SPEC lists"

# Metrics
duration: 32min
completed: 2026-11-26
---

# Phase 71 Plan 04: Server access panel and access data layer Summary

**A claim-gated Access panel in the server Settings section that lists every member and role with access as a closed disclosure row (kind, You and no-View badges, held-permission summary), backed by a refetch-on-focus access query and the complete access data layer of `hostingService` with kept parity rows.**

## Performance

- **Duration:** about 32 min
- **Started:** 2026-11-26T10:50:00Z (approximate)
- **Completed:** 2026-11-26
- **Tasks:** 3 (1 tracer, 2 auto/TDD)
- **Files changed:** 21 (6 created, 15 modified)
- **Commits:** 3 (measured from the plan head ledger)

## Accomplishments

- **Access panel (D-01, D-02):** `ServerAccessPanel` renders `SettingsPanel id="server-access"` between Automation and Server record only when `server.canManageAccess === true`. Without the flag there is no heading, summary or request (the content component that owns the query is never mounted). An archived server keeps the panel.
- **Truthful summary and states:** `Access: {{count}}` (users plus roles), `Nobody has been given access` at zero, no count while loading or failed; `LoadingState`, `EmptyState` and an `ErrorState` whose retry issues a real second request.
- **Query contract (D-14):** `hostingQueryKeys.access(id)` sits under `server(id)`; `staleTime: 0` and `refetchOnWindowFocus: true` are explicit, there is no polling, and a ref updated inside an effect refetches only when the member flips the panel from closed to open.
- **Subject rows (D-06):** `AccessSubjectRow` is an `li` with a `Disclosure variant="inline" headingLevel={3}`, closed by default and never opened by the app; name in `bdi` is the title; Member or Role, You and a warning badge for subjects with permissions but neither View nor Full control; summary lists held labels with `Intl.ListFormat` (Full control alone when `sv` is held), the role member count with `_one`/`_other` or the member fingerprint; in-row warning panel; role description as plain wrapped text. Open state is kept by `user:<id>`/`role:<id>` so a refetch never closes a row.
- **Model:** `serverAccessModel.ts` with the eight-permission catalog in fixed order (label and description keys typed `TranslationKey`), `effectivePermissions`, `heldSummaryKeys`, `heldSummaryItems`, `needsViewWarning`, `canAssign` (reads only `assignablePermissions`).
- **Data layer:** `serverAccess`, `serverUserCandidates`, `serverRoleCandidates`, `changeServerUserAccess`, `changeServerRoleAccess`; `access` and `accessCandidates` keys; `ServerAccess`, `ServerAccessUserGrant`, `ServerAccessRoleGrant`, `ServerAccessUserCandidate`, `ServerAccessRoleCandidate` types; `GameServer.canManageAccess` normalized to `false`.
- **Anchor and parity:** `server-access` added to `serverSettingsPanelAnchors`, both `allSettingsPanelIds` lists and the `url-parameters` panel-file map; six kept phase 71 matrix rows with a `phase71CapabilityIds` / `phase71Offenders` block, live negative sample and "owned by phase 71 but missing" check.

## Task Commits

1. **Task 1: tracer, the Access panel lists who has access** - `50815dfd` (feat)
2. **Task 2: permission catalog and the subject row** - `36816614` (feat)
3. **Task 3: remaining access service methods, fixtures, contract tests and panel order** - `3b5f26de` (feat)

## Verification

- `npx vitest run --maxWorkers=4` (whole project): 219 files, 3654 passed, 2 skipped, 0 failed.
- `locales:check` 0 failures; `locales:unused` 0 failures (only pre-existing warnings); `contracts:check` clean (the line-ending-only change to `src/generated/api.ts` was restored); `npx prettier --check . --end-of-line auto` over `src` and `tests`: clean; `lint` (max-warnings 0): clean; `typecheck` (`tsc -b`): clean; `build:assets`: built.
- `tests/styles` style guard passes with the new `access.css` (tokens only, one `max-width: 520px` breakpoint).
- Acceptance greps (all as planned): `"server-access"` in `urlParameters.ts`, `settingsPanels.ts` and the e2e `settingsPanels.ts` is 1 each; `ServerAccessPanel` in `SettingsSection.tsx` is 2; `async serverAccess(`, `async serverUserCandidates(`, `async changeServerUserAccess(` are 1 each; `ADJ-server-access-panel` 1; `SVC-changeServerRoleAccess` 1; `phase71CapabilityIds` 8 occurrences; `"Server_Access_Summary_Held"` 0; `export const serverPermissionCatalog` 1; `headingLevel={3}` 1; the German RevealSecrets description key 1; `canManageAccess` in `fleetFixtures.ts` 2.
- Tracer gate: automated verify only, `human_verify_mode` is `end-of-phase` and auto advance is on, so the tracer verify was re-run and passed before expansion.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Gate and content split instead of "all hooks first, then return null"**
- **Found during:** Task 2
- **Issue:** The row needs `useAuth().user?.primaryKey`, and `useAuth` throws without an `AuthProvider`. With every hook in one component that runs before the claim check, every existing test that renders `SettingsSection` without an auth provider (credentials, configuration, automation, mod pack and more) would throw even for a server without `canManageAccess`.
- **Fix:** `ServerAccessPanel` only checks `server.canManageAccess !== true` and returns null; `ServerAccessPanelContent` owns `useQuery`, `useAuth`, the open-state ref and the row state. Observable behavior is identical to the plan: no heading, no summary, no request without the flag. The `enabled: canManageAccess === true` option became unnecessary because the query's component only mounts with the flag.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx`
- **Verification:** `server-access-panel.test.tsx` gate tests; full suite green.
- **Committed in:** `36816614`

**2. [Rule 3 - Blocking] Catalog keys typed TranslationKey**
- **Found during:** Task 2
- **Issue:** `t(item.labelKey)` with a plain `string` key failed `tsc` against the typed i18n resources.
- **Fix:** `labelKey` and `descriptionKey` are `TranslationKey`, which also makes a missing locale key a compile error.
- **Files modified:** `sources/ReactApp/src/features/hosting/serverAccessModel.ts`
- **Committed in:** `36816614`

**3. [Rule 2 - Missing critical functionality] Extra export `heldSummaryItems`**
- **Found during:** Task 2
- **Issue:** The plan says an id missing from the catalog must show as a raw id in a `code` element, but `heldSummaryKeys` returns label keys only and cannot carry such an id.
- **Fix:** `heldSummaryItems` returns `{ id, labelKey | null }` entries (known permissions in catalog order, then unknown ids); `heldSummaryKeys` is derived from it.
- **Files modified:** `sources/ReactApp/src/features/hosting/serverAccessModel.ts`, `sources/ReactApp/src/components/server/AccessSubjectRow.tsx`
- **Committed in:** `36816614`

**4. [Rule 3 - Blocking] Panel tests need the auth hook mocked and the mod pack capability for the order test**
- **Found during:** Task 1 and Task 3
- **Issue:** Tests that render the content with `canManageAccess` need `@/auth/auth` mocked (the repo's pattern); the Mods panel only exists when a capability reports `supportsModPacks`, so the full order test mocks `capabilities` and `modPackOptions`.
- **Fix:** `vi.mock("@/auth/auth", ...)` in `server-access-panel.test.tsx` and `settings-section.test.tsx`; the order test mocks the capability.
- **Committed in:** `50815dfd`, `3b5f26de`

**5. [Plan clarification] Fixture option landed with Task 3**
- Tasks 1 and 2 used `overrides` (`canManageAccess: true`) for the server fixture; the `canManageAccess` option on `fleetServer` was added in Task 3 as the plan lists it.

---

**Total deviations:** 4 auto-fixed (3 Rule 3, 1 Rule 2) plus 1 clarification. **Impact:** none on scope or contract; the gate split keeps the plan's observable behavior and protects the existing Settings tests.

## Authentication Gates

None.

## Issues Encountered

- The shell wrapper refused compound `git` commands and heredocs in the worktree; they were split into plain commands. No effect on the result.
- `contracts:check` and `build:assets` rewrite `src/generated/api.ts` with LF line endings only; restored with `git checkout -- sources/ReactApp/src/generated/api.ts` each time.
- The full-suite vitest run takes about 7.5 minutes on this machine.

## Known Stubs

None that block the plan goal. Two intentional, plan-scoped placeholders are visible until their plans land:
- `Server_Access_Empty_Body` (UI-SPEC text) points at "Give access above", which the grant form of 71-05 adds.
- The row content region holds only the role description and the no-View warning; the eight permission switches and Remove all access arrive in 71-08.

## Threat Flags

None. The access query is disabled by construction without `canManageAccess` (T-71-10, covered by tests that assert no request) and names and descriptions render as React text, names inside `bdi` (T-71-11, covered by a test whose role description contains markup). No package was installed (T-71-SC).

## Cross-plan findings

- **XPF-71-04-1:** Mutations, dialogs and any hook the later access plans add (71-05 grant form, 71-08 switches, 71-09 and later) belong in `ServerAccessPanelContent`, not in the `ServerAccessPanel` gate, which must stay hook-free. Tests that render the content (or `SettingsSection` with `canManageAccess: true`) must mock `@/auth/auth`, as `server-access-panel.test.tsx` and `settings-section.test.tsx` now do.
- **XPF-71-04-2:** For 71-08: pass the switches as `children` of `AccessSubjectRow` (the row renders them after the description and warning), drive every switch from `canAssign(access, permission)` and `assignablePermissions` (XPF-71-01-1; the field is typed and defaulted in `serverAccess`), and keep open state in the panel content (`openRows`, keys `user:<id>` and `role:<id>`). The optimistic cache update must edit the `hostingQueryKeys.access(id)` data, which is the shape of `ServerAccess`.
- **XPF-71-04-3:** `canManageAccess` is not in the generated `GameServerDto` at this base (71-03 adds it in parallel). It is typed optional on `GameServer` through the `Omit` list and normalized to `false`; after the merge the generated key is simply omitted and re-added optionally, so no follow-up is needed, but a typecheck after the merge is worthwhile.
- **XPF-71-04-4:** `StatePanel` renders its `title` as an `h2`, so the access `EmptyState` and `ErrorState` titles add an `h2` inside the Settings panel body while the UI-SPEC says internal sub-headings are `h3`. The plan prescribes the shared `EmptyState` and `ErrorState`, so it was kept; 71-12's accessibility audit may want to decide whether a heading-level prop is needed.
- **XPF-71-02-2 (applied scope):** this plan renders no `SearchPicker`; the immediate-query `status` rule stays for 71-05 and 71-07.

## Next Phase Readiness

The panel, the row, the model and all five access service methods are in place and tested; 71-05 (grant form) can use `serverUserCandidates`, `serverRoleCandidates` and `changeServer*Access` directly, and 71-08 can add the switches as row children. No blockers.

## Self-Check: PASSED

- Created files exist: `ServerAccessPanel.tsx`, `serverAccessModel.ts`, `AccessSubjectRow.tsx`, `access.css`, `server-access-panel.test.tsx`, `server-access-model.test.ts`.
- Commits `50815dfd`, `36816614`, `3b5f26de` are ancestors of HEAD.
