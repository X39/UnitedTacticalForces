---
phase: "71"
slug: "server-access-and-cost-owner-ui"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-26"
---

# Phase 71 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Source: `71-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Vitest 4.1.10 (jsdom) + Playwright 1.62.1 (chromium, mobile-390, mobile-320, strict mocked API) + xunit 2.9.3 (SQLite) for the API |
| **Config file** | `sources/ReactApp/vitest.config.ts`, `sources/ReactApp/playwright.config.ts`, `tests/Api.Tests/Api.Tests.csproj` |
| **Quick run command** | `cd sources/ReactApp && npx vitest run <touched test files>`; API: `dotnet test tests/Api.Tests --filter <class>` |
| **Full suite command** | ReactApp: `locales:check`, `locales:unused`, `contracts:check`, `npx prettier --check . --end-of-line auto`, `lint`, `typecheck`, `npx vitest run`, `build:assets`; API: `dotnet test X39.UnitedTacticalForces.slnx`; e2e: Playwright through the serializing lock helper |
| **Estimated runtime** | quick ~10 s; full vitest ~250 s; API ~2 min; e2e several minutes |

---

## Sampling Rate

- **After every task commit:** quick run command scoped to the touched files; `dotnet test ... --filter` for API tasks
- **After every plan wave:** lint, typecheck, full vitest, API filter suite, contracts:check
- **Before `/gsd-verify-work`:** full suite green, `dotnet test` green, Playwright audit + `hosting-access.spec.ts` on chromium, mobile-390, mobile-320
- **Max feedback latency:** 120 seconds (vitest)

---

## Per-Task Verification Map

Decision-level map (the planner fills task ids); status column added during execution.
| Req ID | Behavior | Test Type | Automated Command | File Exists? | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| D-01, D-02 | order Mods, Configuration, Credentials, Automation, Access, Cost owner, Server record; Access absent without flag and no request; archived keeps Access, hides Cost owner | component | `npx vitest run tests/hosting/settings-section.test.tsx` | extend | ✅ |
| D-01 | anchors in allowlist and as literals in panel files | unit | `npx vitest run tests/app/url-parameters.test.ts` | extend | ✅ |
| D-03 | Cost owner read-only sentence, no picker, no options request without `canModify` | component | `npx vitest run tests/hosting/server-cost-owner-panel.test.tsx` | Wave 0 | ✅ |
| D-04, D-05 | grant calls `sv:view` once, no staging, no leave guard | component | `npx vitest run tests/hosting/server-access-panel.test.tsx` | Wave 0 | ✅ |
| D-06, D-07 | row anatomy, eight switches in fixed order, Full control makes seven disabled-on | component + pure | `... server-access-model.test.ts server-access-panel.test.tsx` | Wave 0 | ✅ |
| D-08 | optimistic move, rollback of the single pair, alert under switch, scope-serialized parallel toggles | component | `... server-access-panel.test.tsx` | Wave 0 | ✅ |
| D-09 | confirm for self Full control/View/Manage access and for Remove all; none for Operate | component | `... server-access-panel.test.tsx` | Wave 0 | ✅ |
| D-10 | combobox roles, keys, pointer select keeps focus, token/clear, states, live region, all strings are props | component | `npx vitest run tests/search-picker.test.tsx` | Wave 0 | ✅ |
| D-11, D-12 | dialog copy with names, reasons by priority, 409/400/403/generic mapping, `server.deployment === null` rule | component | `... server-cost-owner-panel.test.tsx` | Wave 0 | ✅ |
| D-13 | link only with `canModify` and not archived; hash `#server-cost-owner` | component + route-link scan | `npx vitest run tests/hosting/cost-strip.test.tsx tests/app/route-links.test.ts` | extend | ✅ |
| D-14 | new keys are sub-keys; exact invalidations (spy asserts `exact: true`) | unit | `npx vitest run tests/hosting/services.test.ts tests/hosting/server-access-panel.test.tsx` | extend | ✅ |
| D-15 | panel unmounts + toast + focus Settings h2; 403/404 redirect with toast | component | `... server-access-panel.test.tsx` | Wave 0 | ✅ |
| D-16 | filter from eight subjects, never hides warning subject, empty state | component | `... server-access-panel.test.tsx` | Wave 0 | ✅ |
| D-17 | snapshot contains new fields/paths; `api.ts` in sync | contract | `npm run contracts:check`; CI `--export-openapi` + `cmp` | existing gate | ✅ |
| D-18 | no dependency change | review | `git diff package.json package-lock.json` empty | manual | ✅ (verified unchanged since 5fbb2d99 in 71-12) |
| D-19 | axe, keyboard, reflow, spacing, German, headings for S-15/S-16/O-10/O-11; two key screens K-18/K-19 | e2e | `npx playwright test tests/e2e/audit tests/e2e/hosting-access.spec.ts --project=chromium` (then mobile projects) | Wave 0 + registry | ✅ |
| D-19 | style guard and contrast pairs | unit | `npx vitest run tests/styles` | extend | ✅ |
| D-19 | locale parity, glossary, unused, hard-coded text | unit | `npm run locales:check && npm run locales:unused && npx vitest run tests/locales` | extend | ✅ |
| D-20 | profile section untouched | review | `git diff --stat sources/ReactApp/src/features/hosting/ConfigurationProfileAccessSection.tsx` empty | manual | ✅ (verified unchanged since 5fbb2d99 in 71-12) |
| API-01 | `CanManageAccess` equals `HasExactPermission` for admin claims, `sv`, `sv:manage-access`, `sv:modify` only, `sv:view` only, none; detail and list; frozen/assigned-at null when owner hidden | xunit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~GameServerProjection\|FullyQualifiedName~GameServerListAccess"` | extend | ✅ |
| API-02, API-03 | 403 without ManageAccess, verified/not deleted/not banned, exclusion of granted subjects, ordering, `take` 0/51 -> 400, default 20, `memberCount`, fingerprint `detail`; Npgsql translation of the search offline | xunit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~GameServerAccess"` | extend | ✅ |
| API-05 | `Modify` policy; administrator all owners, `co:use`, own owner first; current owner excluded; frozen flagged; unknown/archived 404; `kind` filled; creation endpoint only gains `kind` | xunit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~CostOwnerOptions"` | extend | ✅ |
| API-04 | OpenAPI snapshot has `Detail`, `MemberCount`, `CanManageAccess`, `CostOwnerIsFrozen`, `CostOwnerAssignedAt` | contract | `npm run contracts:check` + CI step | existing gate | ✅ |
| parity | one row per new `hostingService` method (`it.each(Object.keys(hostingService))`) and the two ADJ rows | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | extend + matrix rows | ✅ |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Plan and task ids (filled by the planner; every task carries an automated verify with a stated failing direction):

| Req ID | Plan / task |
|--------|-------------|
| API-02, API-03, API-04 | 71-01 T1, T2 (candidates, detail, memberCount); orchestrator decision 2 in 71-01 T3 |
| API-01, API-05 | 71-03 T1 (options, kind), T2 (canManageAccess, frozen, assigned-at) |
| D-17 | 71-01 T1-T3, 71-03 T1-T2 (snapshot, api.ts, WebApp build; Kiota not regenerated per orchestrator decision 1) |
| D-10, D-18 | 71-02 T1, T2 |
| D-01, D-02, D-06, D-14 | 71-04 T1-T3 |
| D-04, D-05 | 71-05 T1, T2 (banner: orchestrator decision 4) |
| D-12, D-14 | 71-06 T1, T2 |
| D-01, D-03, D-11, D-12, D-13 | 71-07 T1-T3 |
| D-06, D-07, D-08, D-09 | 71-08 T1, T2 |
| D-11, D-12, D-13, D-19 (browser) | 71-09 T1, T2 |
| D-09, D-15, D-16 | 71-10 T1-T3 |
| D-04, D-08, D-09, D-15, D-19 (browser) | 71-11 T1, T2 |
| D-18, D-19, D-20 | 71-12 T1-T3 (registry S-15, S-16, O-10, O-11; K-18, K-19; phase gate) |

---

## Wave 0 Requirements
- [x] `tests/hosting/server-access-model.test.ts`, `tests/search-picker.test.tsx`, `tests/hosting/server-access-panel.test.tsx`, `tests/hosting/server-cost-owner-panel.test.tsx` — the four new Vitest files of the UI-SPEC.
- [x] `tests/e2e/hosting-access.spec.ts` plus registry entries S-15, S-16, O-10, O-11 and key screens K-18, K-19 (`keyScreenCount` 17 -> 19) in `tests/e2e/support`.
- [x] Helper updates: both `allSettingsPanelIds` lists (`tests/helpers/settingsPanels.ts`, `tests/e2e/support/settingsPanels.ts`) gain `server-access` and `server-cost-owner` before `server-record`; `fleetServer` and e2e server builders gain the three fields; `tests/mocks/react-icons.tsx` gains any new glyph; e2e mocks gain `GET .../access` and `GET .../cost-owner/options` handlers.
- [x] API: new test classes/cases listed above; update `NoOpGameServerAccessService` and `RecordingAccessService`.
- [x] Parity matrix rows (`61-PARITY-MATRIX.md`): `SVC-serverAccess`, `SVC-serverUserCandidates`, `SVC-serverRoleCandidates`, `SVC-changeServerUserAccess`, `SVC-changeServerRoleAccess`, `SVC-serverCostOwnerOptions`, `SVC-changeServerCostOwner`, `ADJ-server-access-panel`, `ADJ-server-cost-owner-panel`, all `kept`, Phase `71`, "Today" cell naming `hostingService.<method>`, and a new Phase 71 offender block in `parity-matrix.test.ts` following the Phase 66-69 pattern (no row may stay `pending`).
- [x] `tests/styles/tokens-contrast.test.ts` pairs for the listbox and token; `--utf-listbox-max-height` token; `access.css` is auto-scanned by `server-style-guard.test.ts` (it lists `src/components/server/*.css`), `search-picker.css` is NOT in that directory and must be added to the guard's file list.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Visual look of the Access and Cost owner panels | D-19 | Visual design judgment | Deferred follow-up: milestone-end visual review on the seeded stack (`pwsh ./scripts/Start-Uat.ps1 -Fresh`) |
| Screen-reader output of the search picker combobox and permission switches | D-10, D-19 | Spoken output | Deferred follow-up: milestone-end screen-reader pass |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-26

## Validation Audit 2026-11-26

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
