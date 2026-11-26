---
phase: 71-server-access-and-cost-owner-ui
verified: 2026-11-26T19:45:00Z
status: passed
score: 34/34 must-haves verified
covered_files:
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-01-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-01-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-02-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-02-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-03-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-03-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-04-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-04-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-05-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-05-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-06-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-06-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-07-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-07-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-08-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-08-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-09-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-09-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-10-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-10-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-11-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-11-SUMMARY.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-12-PLAN.md"
  - ".planning/phases/71-server-access-and-cost-owner-ui/71-12-SUMMARY.md"
  - "sources/Api/Controllers/GameServersController.cs"
  - "sources/Api/Services/GameServerLifecycle/GameServerAccessService.cs"
  - "sources/ReactApp/openapi/utf-api.json"
  - "sources/ReactApp/src/components/SearchPicker.tsx"
  - "sources/ReactApp/src/components/server/AccessGrantForm.tsx"
  - "sources/ReactApp/src/components/server/AccessSubjectRow.tsx"
  - "sources/ReactApp/src/features/hosting/ServerAccessPanel.tsx"
  - "sources/ReactApp/src/features/hosting/ServerCostOwnerPanel.tsx"
  - "sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx"
  - "sources/ReactApp/src/features/hosting/serverAccessModel.ts"
  - "sources/ReactApp/src/features/hosting/services.ts"
  - "sources/ReactApp/src/features/hosting/useAccessMutations.ts"
  - "sources/ReactApp/src/features/hosting/useCostOwnerTransfer.ts"
covered_digest: "v4:sha256:68b9af119d58910a0026c96aeeef10ffbe81e8c45d6c4335e9f5559f1dd3470a"
behavior_unverified: 0
overrides_applied: 0
deferred:
  - truth: "GET /game-servers/{id}/actions has a React consumer (third endpoint of v2.1 audit integration finding 2)"
    addressed_in: "none (not claimed by any phase; accepted as redundant, see Audit Finding Closure)"
    evidence: "Not in the Phase 71 goal text or any plan; the same action list is already embedded in the GET /game-servers/{id} DTO that hostingService.server consumes. Listed here as informational only, not as an actionable gap."
human_verification: []
---

# Phase 71: Server Access and Cost Owner UI Verification Report

**Phase Goal:** Server owners and admins can manage who may see and operate a server and who pays for it, directly in the React server workspace: list the per-server access grants (`GET /game-servers/{id}/access`), grant and revoke per-server claims such as `sv:view` through the existing grant and revoke endpoints, and transfer the cost owner (`PUT /game-servers/{id}/cost-owner`) with a clear confirmation. Claim-gated, localized en/de, following the Phase 70 UI conventions (Settings panels, drawn controls, ConfirmDialog).
**Verified:** 2026-11-26
**Status:** passed
**Re-verification:** No, initial verification (verified against the final tree at f5e141d0, after all three code-review fix iterations)

## Goal Achievement

ROADMAP.md lists no separate Success Criteria for Phase 71, so the roadmap contract is the goal sentence. It is split into G1 to G9 below, then merged with the plan-level decisions D-01..D-20 and API-01..API-05 (all taken from the PLAN `requirements` frontmatter and the UI-SPEC).

### Observable Truths: Goal Contract

| #  | Truth | Status | Evidence |
| -- | ----- | ------ | -------- |
| G1 | The per-server access grants are listed in the React server workspace | VERIFIED | `ServerAccessPanel.tsx:205` queries `hostingService.serverAccess` (`services.ts:1196`, `GET /game-servers/{gameServerId}/access`) and renders one `AccessSubjectRow` per member and role; mounted in `SettingsSection.tsx:310`. Panel tests (list, empty, error and retry, cached list kept on failed refetch) pass. |
| G2 | A per-server claim such as `sv:view` can be granted (members and roles) | VERIFIED | `useGrantAccess` calls `changeServerUserAccess` or `changeServerRoleAccess` with `viewPermission` (`useAccessMutations.ts:154-157`); the permission switches grant any assignable permission (`useToggleAccess`). Both go to the existing `access/{users,roles}/grant` routes (`services.ts`). Test "grants View with exactly one call and ignores a second submit while pending" and the e2e "gives access, switches a permission and removes all access" (en and de) pass. |
| G3 | Per-server claims can be revoked | VERIFIED | Single revoke through the switch (same method, `grant: false`); Remove all access runs the revokes serially in `revocationOrder` (`useAccessMutations.ts:309-321`). Tests "revokes each stored permission once, in order", "stops at the first failure" pass. |
| G4 | The cost owner can be transferred with a clear confirmation | VERIFIED | `ServerCostOwnerPanel.tsx`: choose in `SearchPicker`, then `ConfirmDialog` (gold confirm, `initialFocus="dismiss"`, names the new and old owner and what changes, `busy` blocks dismissal) sends one `PUT /game-servers/{id}/cost-owner` via `hostingService.changeServerCostOwner` (`services.ts:976-985`). e2e "transfers the cost owner and shows the new owner in the panel and the cost strip" and the 409 and keyboard-only flows pass. |
| G5 | Claim-gated | VERIFIED | Access panel returns null before any hook that sends a request unless `server.canManageAccess === true` (`ServerAccessPanel.tsx:177`; the query lives in the child component, so no request). Transfer controls render only with `server.canModify` (`ServerCostOwnerPanel.tsx:216`), else a read-only sentence and no options request (`enabled: server.canModify && !server.isArchived && engaged`). Cost-strip link only with `canModify` on a non-archived server (`CostStrip.tsx:48`). API side: `[Authorize(ManageAccess)]` on all seven access routes, `[Authorize(Modify)]` on both cost-owner routes (`GameServersController.cs:333-360, 489-647`). Tests "renders no heading, summary or request without canManageAccess", "shows the read-only sentence, no picker and no owners request without canModify" pass. |
| G6 | Localized en/de | VERIFIED | `npm run locales:check`: 4037 English and 4037 German keys, 0 failures. `locales:unused`: 0 failures. `tests/audit/german-leak.test.ts` and the German e2e variants (de walk at 320 and 390 px) pass. Spot check: `Server_CostOwner_Dialog_Body` and `Server_Access_Self_Title` differ and are translated in `de.json`. |
| G7 | Follows the Phase 70 conventions (Settings panels, drawn controls, ConfirmDialog) | VERIFIED | Both panels are `SettingsPanel`s in the Settings section (`server-access`, `server-cost-owner`, before `server-record`, `SettingsSection.tsx:310-314`); rows use `Disclosure`, `SwitchField`, `RadioGroupField`, `ActionButton`; all confirmations use `ConfirmDialog`. No `<details>`, `<select>`, raw `<input>`/`<button>` and no `innerHTML` in the new files (grep clean). `tests/styles` (server style guard, contrast pairs, legacy ratchet) pass. |
| G8 | The endpoints are now in the Phase 61 parity matrix and no longer unconsumed | VERIFIED (with warning W1) | Seven `SVC-*` rows and `ADJ-server-access-panel`, `ADJ-server-cost-owner-panel` exist in `61-PARITY-MATRIX.md:102-108, 242-243`, all `kept`, Phase 71. `parity-matrix.test.ts` passes. `hostingService.serverAccess`, `serverUserCandidates`, `serverRoleCandidates`, `changeServerUserAccess`, `changeServerRoleAccess`, `serverCostOwnerOptions`, `changeServerCostOwner` each have a production caller (grep of `src/`). Five rows still carry a stale "Today" cell, see W1. |
| G9 | No new REQUIREMENTS.md ids; traceability stays 62/62 | VERIFIED | `REQUIREMENTS.md`: 62 traceability rows, 62 `[x]`, 0 `[ ]`; no `D-xx` or `API-xx` phase-71 id and no "Phase 71" row; the file's last commit is Phase 69 (`d1aea976`), untouched since the phase started (`git diff 5fbb2d99 HEAD -- REQUIREMENTS.md` empty). ROADMAP states "Requirements: none mapped". |

### Observable Truths: Plan-Level Decisions (D-01..D-20, API-01..API-05)

| ID | Truth | Status | Evidence |
| -- | ----- | ------ | -------- |
| D-01 | Access and Cost owner are two collapsible Settings panels in the order Mods, Configuration, Credentials, Automation, Access, Cost owner, Server record | VERIFIED | `SettingsSection.tsx:300-314`; `settings-section.test.tsx` and `url-parameters.test.ts` pass; `allSettingsPanelIds` helpers list both ids before `server-record`. |
| D-02 | Access panel only with `canManageAccess`; archived servers keep it | VERIFIED | `ServerAccessPanel.tsx:177` has no archived branch; settings-section tests cover absent and archived cases. |
| D-03 | Cost owner panel for every viewer of a non-archived server; transfer only with `canModify` | VERIFIED | `ServerCostOwnerPanel.tsx:110, 216-264`. |
| D-04 | Access changes apply immediately, one permission per call, no staging, no leave guard | VERIFIED | Test "stages nothing: no review bar and no dialog"; the panel contributes nothing to the pending-changes bar. |
| D-05 | Adding a subject grants exactly `sv:view` | VERIFIED | `useGrantAccess` passes `viewPermission` only; test "grants View with exactly one call". |
| D-06 | Subject row is an inline `Disclosure` h3 with kind badges and permission summary; rows start closed and survive refetch | VERIFIED | `AccessSubjectRow.tsx:257`; tests "shows every row closed", "keeps an open row open after the access query refetches". |
| D-07 | Eight switches in fixed order; Full control makes the other seven on and disabled with a hint, stored grants untouched | VERIFIED | `serverPermissionCatalog`; tests "shows eight switches in the fixed order", "shows the seven other switches on and disabled while Full control is held", "revokes only Full control and shows the stored grants again". |
| D-08 | Toggles are optimistic; failure rolls back only that pair with an inline alert; overlapping toggles are serialized | VERIFIED | `useToggleAccess`: `onMutate` cache write, `onError` inverse of that single change, `scope: accessMutationScope`. Behavior-dependent, test-proven: "rolls back only the failed pair and shows the alert directly under its switch", "refreshes the exact queries once, after the last pending toggle settled". |
| D-09 | Confirmation only for self Full control, View, Manage access and for Remove all access | VERIFIED | `requiresSelfConfirm` (`serverAccessModel.ts`), used at `ServerAccessPanel.tsx:354`; tests "asks before the signed-in member removes their own View, with Keep permission focused", "revokes Operate on the own row ... without a dialog", "opens a danger dialog with Keep access focused". |
| D-10 | Shared `SearchPicker` ARIA combobox, never a native select; member/role behind a segmented choice | VERIFIED | `SearchPicker.tsx` (`role="combobox"`, `role="listbox"`, `aria-activedescendant`, IME and touch guards present in the current tree); `search-picker.test.tsx` passes (part of the 370-test targeted run). |
| D-11 | Transfer is choose then confirm, dialog names new and old owner and states what changes; gold is the only confirm | VERIFIED | `ServerCostOwnerPanel.tsx:266-294`; e2e cost-owner specs. |
| D-12 | Transfer offered only while the API can accept it; reason shown; 409 still mapped | VERIFIED | `transferBlocker` mirrors `deployment !== null` (the API's unresolved-deployment predicate) plus pending jobs; `costOwnerTransferFailure` maps 409/400/403/generic to fixed keys; e2e "shows the conflict message inside the open dialog when the API answers 409", "keeps Transfer unavailable with its reason while a cloud machine exists / jobs are pending". |
| D-13 | Cost strip "Change cost owner" link to `?tab=settings#server-cost-owner` for `canModify` on non-archived servers | VERIFIED | `CostStrip.tsx:48-78`; `cost-strip.test.tsx` and `route-links.test.ts` pass. |
| D-14 | New query keys are sub-keys of the server key; invalidations are exact; candidate searches survive | VERIFIED | `services.ts:229-303`; `invalidateAfterAccessChange` uses `exact: true` for access, server, fleet and `me`, candidates by prefix without awaiting; `useCostOwnerTransfer` exact keys. Tests assert the exact flags. |
| D-15 | Losing own manage-access shows a toast and moves focus; losing View sends the member to the fleet with a toast | VERIFIED | `settleAccessChange` in the hook-level `onSettled` (runs after unmount); `ServerAccessPanel.tsx:241-250`; tests "tells the member, removes the panel and calls onAccessLost", "completes the settle step after the panel unmounted", "leaves for the fleet with replace and a toast when the refreshed server read answers 403". |
| D-16 | Filter from eight subjects; never hides a warning subject; empty state | VERIFIED | `accessFilterThreshold = 8`, `matchesFilter` (`ServerAccessPanel.tsx:52, 145`); tests "offers no filter with seven subjects", "offers the filter and a polite result line from eight subjects". |
| D-17 | API changes are additive; OpenAPI snapshot and `api.ts` regenerated and in sync | VERIFIED | Snapshot contains `canManageAccess`, `costOwnerIsFrozen`, `costOwnerAssignedAt`, `memberCount`, `assignablePermissions`, `access/users/options`, `access/roles/options`, `cost-owner/options`. `npm run contracts:check` re-run: exit 0 and no content diff in `api.ts` (only a CRLF working-copy note, restored). `sources/WebApp` is untouched (`git diff 5fbb2d99 HEAD -- sources/WebApp` empty), see W2. |
| D-18 | No new runtime or dev dependency | VERIFIED | `git diff 5fbb2d99 HEAD -- package.json package-lock.json` empty. |
| D-19 | Phase 69 gates stay green for the new screens; registry entries S-15, S-16, O-10, O-11, K-18, K-19 | VERIFIED | `registryTypes.ts:177-231`, `keyScreens.ts` (`keyScreenCount = 19`); `screen-registry.test.ts` 12 passed; I re-ran the e2e audit folder filtered to S-15/S-16 and O-10/O-11 on chromium: 6 and 6 passed (axe, reflow, spacing, German, headings). |
| D-20 | Legacy `ConfigurationProfileAccessSection` untouched | VERIFIED | `git diff 5fbb2d99 HEAD --` that file is empty. |
| API-01 | `GameServerDto` gains `CanManageAccess`, `CostOwnerIsFrozen`, `CostOwnerAssignedAt`; frozen and assigned-at only when the owner is visible | VERIFIED | `GameServerProjectionService.cs:1057-1061` (`HasExactPermission`, `canSeeOwner ? ... : null`); `GameServerProjectionServiceTests` pass. |
| API-02 | Member candidate search (verified, not deleted, not banned, excluding granted subjects, bounded take, ManageAccess) | VERIFIED | `GameServerAccessService.SearchUserCandidatesAsync`; controller route `access/users/options` with `[Authorize(ManageAccess)]`; `ValidateTake` 1..50; `LikePattern` escaping. |
| API-03 | Role candidate search with member count | VERIFIED | `BuildRoleCandidateQuery`; test "translates the production role candidate query" (offline Npgsql translation). |
| API-04 | Grant DTOs gain `Detail` (id fingerprint, no linked account) and `MemberCount` | VERIFIED | `Fingerprint(Guid)` = `#` plus last six hex characters; `memberCounts` in `GetAsync`. |
| API-05 | Transferable cost-owner options with `Kind`; `Modify` policy; current owner excluded | VERIFIED | `GameServersController.cs:333-334`; `GameServerCreationCostOwnerOptionsTests` pass. |

**Score:** 34/34 truths verified (9 goal truths, 20 decisions, 5 API contracts), 0 present-but-behavior-unverified. Every behavior-dependent truth (D-08 optimistic rollback and serial scope, D-09 self lock-out, D-15 lost access, the post-review focus-move behavior) is exercised by a named passing test, not by symbol presence.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | React consumer for `GET /game-servers/{id}/actions` | Not scheduled (accepted as redundant) | See Audit Finding Closure. Informational, not a gap: the phase goal does not name it. |

### Audit Finding Closure (v2.1-MILESTONE-AUDIT.md, integration finding 2)

The audit item reads: "API endpoints with no React consumer: GET /game-servers/{id}/access plus grant/revoke, PUT /game-servers/{id}/cost-owner, GET /game-servers/{id}/actions".

| Endpoint | Consumer now | Status |
| -------- | ------------ | ------ |
| `GET /game-servers/{id}/access` | `hostingService.serverAccess`, `ServerAccessPanel` | Closed |
| `POST .../access/{users,roles}/{grant,revoke}` | `useGrantAccess`, `useToggleAccess`, `useRemoveAllAccess` | Closed |
| `PUT /game-servers/{id}/cost-owner` | `useCostOwnerTransfer`, `ServerCostOwnerPanel` | Closed |
| `GET /game-servers/{id}/actions` | None. `grep` of `src/` finds only the `POST .../actions/{actionId}` caller (`executeAction`). The OpenAPI path `/game-servers/{gameServerId}/actions` is still in the snapshot with no `hostingService` method. | **Remains open.** No Phase 71 plan, summary or matrix row mentions it. It is redundant: `GetActionsAsync` returns `server.Actions`, the same projection the React app already reads from `GET /game-servers/{id}` (`services.ts:130, 153`). |

The `tech_debt` integration entry of `v2.1-MILESTONE-AUDIT.md` should therefore be narrowed to the `/actions` endpoint (or the endpoint accepted as intentionally unconsumed) when the milestone audit is refreshed; it should not be recorded as fully closed.

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `src/features/hosting/ServerAccessPanel.tsx` | Access panel, list, grant form, toggles, dialogs, settle | VERIFIED | 689 lines, substantive, mounted, data flows from the access query |
| `src/features/hosting/useAccessMutations.ts` | Grant, toggle, remove-all, settle step | VERIFIED | 324 lines, serial scope, exact invalidations |
| `src/features/hosting/serverAccessModel.ts` | Permission catalog, `requiresSelfConfirm`, `canAssign`, `applyOptimistic` | VERIFIED | 223 lines, unit tested |
| `src/features/hosting/ServerCostOwnerPanel.tsx` | Cost owner panel and transfer dialog | VERIFIED | 297 lines |
| `src/features/hosting/useCostOwnerTransfer.ts` | Transfer mutation, blocker rule, failure mapping | VERIFIED | 108 lines |
| `src/components/SearchPicker.tsx` | Shared combobox | VERIFIED | 520 lines, review fixes present |
| `src/components/server/AccessSubjectRow.tsx`, `AccessGrantForm.tsx`, `ReasonReserve.tsx`, `access.css`, `cost-owner.css`, `search-picker.css` | Row, form, structural reason reserve, token-only CSS | VERIFIED | Present and wired; style guard passes |
| `sources/Api/.../GameServerAccessService.cs`, `GameServerProjectionService.cs`, `GameServersController.cs`, new DTOs, `LikePattern.cs`, `HostingSearchLimits.cs` | Additive API | VERIFIED | Present, policies in place, 127 filtered API tests pass |
| `tests/e2e/hosting-access.spec.ts`, `hosting-cost-owner.spec.ts` | Browser flows | VERIFIED | Re-run: 41 passed on chromium |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| `SettingsSection.tsx` | `ServerAccessPanel`, `ServerCostOwnerPanel` | JSX at lines 310-314, `onAccessLost` bumps the heading focus request | WIRED | |
| `ServerAccessPanel` | `GET .../access`, candidates, grant and revoke | `hostingService` methods through `useQuery` and the three mutation hooks | WIRED | |
| `ServerCostOwnerPanel` | `GET .../cost-owner/options`, `PUT .../cost-owner` | `useQuery` (engaged on first focus) and `useCostOwnerTransfer` | WIRED | |
| `CostStrip` | `#server-cost-owner` panel | `to={{ search: "?tab=settings", hash: "#server-cost-owner" }}` | WIRED | |
| Mutation `onSettled` | Router and toast after unmount | hook-level `settleAccessChange` | WIRED | |
| `GameServersController` | `GameServerAccessService` assignment scope | `EnsureAssignable` before every grant and revoke | WIRED | |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| `ServerAccessPanel` | `access.data` to `subjects` | `GET .../access` through `GameServerAccessService.GetAsync` (EF query on users, roles, claims) | Yes | FLOWING |
| `ServerAccessPanel` | candidate `options` | `GET .../access/users/options` and `roles/options` (`ApplyNicknameSearch`, `BuildRoleCandidateQuery`) | Yes | FLOWING |
| `ServerCostOwnerPanel` | `owners.data` | `GET .../cost-owner/options` through the projection service rule | Yes | FLOWING |
| `ServerCostOwnerPanel` | owner, frozen, since | `GameServer` DTO from `ProjectServerAsync` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Access, picker, cost owner, settings section, parity, cost strip, route links, url parameters | `npx vitest run` on nine named files | 9 files, 370 tests passed | PASS |
| Screen registry and style and audit unit suites | `npx vitest run tests/hosting/screen-registry.test.ts tests/audit tests/styles` | 12, and 181 passed with 1 skipped | PASS |
| API access, cost owner options, projection | `dotnet test tests/Api.Tests --filter "...GameServerAccess\|CostOwnerOptions\|GameServerProjection"` | 127 passed, 0 failed | PASS |
| Browser flows | Playwright chromium, `hosting-access.spec.ts` and `hosting-cost-owner.spec.ts` | 41 passed | PASS |
| Audit states | Playwright chromium, `tests/e2e/audit` grep `S-1[56]` and `O-1[01]` | 6 and 6 passed | PASS |
| Contract and locale gates | `contracts:check`, `locales:check`, `locales:unused`, `typecheck` | all exit clean, 4037/4037 keys | PASS |

The orchestrator's full-suite results (Vitest 3,806 passed and 2 skipped; `dotnet test` of the solution 93 + 364 + 2 + 1,794 passed; OpenAPI export byte-identical; prettier, lint, build:assets clean; mobile-390 and mobile-320 audit runs) were taken as reported and not re-run. `src/generated/api.ts` was restored after the e2e and contracts runs; the working tree is unchanged apart from the pre-existing untracked `.planning/ui-reviews/`.

### Probe Execution

Step 7c: SKIPPED. The phase declares no `probe-*.sh` scripts and is not a migration or tooling phase.

### Requirements Coverage

Phase 71 maps no REQUIREMENTS.md ids (confirmed, see G9). All phase-local ids are accounted for: the union of the twelve PLAN `requirements` fields and of the SUMMARY `requirements-completed` fields is exactly D-01..D-20 plus API-01..API-05 (25 ids), each with at least one plan and one verified truth above. No orphaned ids.

| Requirement | Source Plan | Status | Evidence |
| ----------- | ----------- | ------ | -------- |
| API-01 | 71-03 | SATISFIED | API-01 row above |
| API-02, API-03, API-04 | 71-01 | SATISFIED | rows above |
| API-05 | 71-03 | SATISFIED | row above |
| D-01, D-02, D-06, D-14 | 71-04 | SATISFIED | rows above |
| D-04, D-05, D-10 | 71-05 | SATISFIED | rows above |
| D-12, D-14 | 71-06 | SATISFIED | rows above |
| D-01, D-03, D-11, D-12, D-13 | 71-07 | SATISFIED | rows above |
| D-04, D-06, D-07, D-08, D-09 | 71-08 | SATISFIED | rows above |
| D-11, D-12, D-13, D-19 | 71-09 | SATISFIED | rows above |
| D-09, D-15, D-16 | 71-10 | SATISFIED | rows above |
| D-04, D-08, D-09, D-15, D-19 | 71-11 | SATISFIED | rows above |
| D-10, D-17, D-18, D-19, D-20 | 71-01, 71-02, 71-03, 71-12 | SATISFIED | rows above |

### Anti-Patterns Found

Scanned the 78 handwritten source and test files changed since 5fbb2d99 (generated `api.ts`, OpenAPI snapshot and locale files excluded). No `TBD`, `FIXME`, `XXX`, `TODO`, `HACK` or "not yet implemented" marker, so the debt-marker gate is clear. No `innerHTML`, `<details>`, native `<select>` or raw `<input>`/`<button>` in the new components. The empty-array defaults in `services.ts` (`?? []`) normalize API payloads and are not stubs.

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| `.planning/phases/61-*/61-PARITY-MATRIX.md` | 103-106, 108 | Stale "Today" cell: five rows still read "has no screen caller yet (service and tests only)" and "the screen caller arrives with ... Phase 71", while the callers exist | Warning (W1) | Documentation only. The matrix is the artifact the audit finding said was missing, and an auditor reading these five rows would conclude the endpoints are still unconsumed. The matrix tests pass because they check row presence, status and phase, not this text. |
| `sources/WebApp/UtfApi` (Kiota client) | n/a | Not regenerated for the additive API fields | Warning (W2) | `AGENTS.md` says to regenerate generated clients when the API changes. The plan recorded an explicit orchestrator decision not to touch the legacy Blazor client (additive fields only; compatibility proven by building `sources/WebApp`). Consistent with the "do not touch the legacy WebApp unless an API change would break it" rule, but the Kiota client is now behind the API. |

### Notable Behavior Changes to Confirm (informational)

`GameServerAccessService` now enforces an assignment scope on every grant and revoke (`EnsureAssignable`, 403 when the permission is outside what the caller holds; administrators and `sv` holders may assign everything). This closes a self-escalation path (T-71-01) and is tested (`ManageAccessAlone_CannotGrantFullControlToSelf`), but it also changes the behavior of the legacy Blazor client and any direct API caller that held `sv:manage-access` without the other permissions. It is recorded in 71-01-SUMMARY as an orchestrator decision and in 71-SECURITY.md, so it is intentional.

### Human Verification Required

None that gate this verdict. Per the standing project policy, real-environment, real-device, screen-reader and visual-baseline checks are deferred to milestone end and do not make the status `human_needed` (precedent: `70-VERIFICATION.md`).

Deferred follow-up (milestone end, not blocking):

1. **Visual pass of the Access and Cost owner panels.** Test: on the seeded stack (`pwsh ./scripts/Start-Uat.ps1 -Fresh`) open a server workspace, Settings, and review both panels, the open subject row and the Transfer dialog in en and de. Expected: matches 71-UI-SPEC; no layout shift. Why human: visual judgment (71-VALIDATION Manual-Only row 1; 71-UI-REVIEW was code-derived, no screenshots).
2. **Screen-reader pass of the search picker and the permission switches.** Test: operate the combobox and the switches with NVDA or VoiceOver. Expected: roles, state words, live-region announcements are spoken sensibly. Why human: spoken output (71-VALIDATION Manual-Only row 2).
3. **521 to 800 px reason layout in English and German** (code review iteration 1, WR-03, carried by 71-REVIEW-DISPOSITION). Why human: visual check of the structural `ReasonReserve` at those widths; e2e covers 320, 390 and 600 px only.
4. **Access toggle focus behavior against a real API** (code review iterations 2 and 3). Test: switch off the last permission of a subject against the real API and watch where focus lands. Expected: focus moves to the "People and roles with access" heading, never to the document. Why human: timing against a real server; unit tests cover a late refetch with a deferred promise and a mocked e2e API.
5. **Touch scrolling of the picker listbox on a real phone** (UI review top fix 1, fixed by choosing on tap click for touch and pen). Why human: real device input.

### Gaps Summary

No blocking gaps. All nine goal truths, all twenty decisions and all five API contracts are backed by code in the final tree, wired end to end (UI to service to controller to EF query), and exercised by passing unit, API and browser tests that I re-ran. Three items need the orchestrator's attention, none of which blocks the phase:

- **W1 (documentation drift):** fix the five stale "Today" cells in `61-PARITY-MATRIX.md` (SVC-serverUserCandidates, SVC-serverRoleCandidates, SVC-changeServerUserAccess, SVC-changeServerRoleAccess, SVC-serverCostOwnerOptions) to name the real callers (`ServerAccessPanel.tsx`, `useAccessMutations.ts`, `ServerCostOwnerPanel.tsx`).
- **`GET /game-servers/{id}/actions` is still without a React consumer.** Phase 71 did not claim it and does not close it. Narrow the milestone audit's integration debt entry to this one redundant endpoint, or record it as accepted.
- **Bookkeeping left for the orchestrator:** ROADMAP.md still shows Phase 71 unchecked (line 53) and "12/12 In Progress" (progress table); REQUIREMENTS.md traceability needs no change (62/62).

---

_Verified: 2026-11-26_
_Verifier: Claude (gsd-verifier)_
