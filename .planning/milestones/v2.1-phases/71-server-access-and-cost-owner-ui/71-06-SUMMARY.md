---
phase: 71-server-access-and-cost-owner-ui
plan: 06
subsystem: ui
tags: [react, react-query, cost-owner, data-layer, parity-matrix]
status: complete

requires:
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-03 generated contract with the owner options route, kind on the owner option, costOwnerIsFrozen and costOwnerAssignedAt"
  - phase: 71-server-access-and-cost-owner-ui
    provides: "71-04 phase71CapabilityIds block, canManageAccess typing through the GameServer Omit list"
provides:
  - "useCostOwnerTransfer: transfer mutation with exact server, fleet and accrued cost invalidation plus the owner options prefix"
  - "costOwnerTransferFailure and CostOwnerTransferFailure: status classification (conflict, unavailable, forbidden, generic)"
  - "transferBlocker: availability rule that mirrors the API unresolved-deployment predicate"
  - "hostingService.changeServerCostOwner and hostingService.serverCostOwnerOptions"
  - "hostingQueryKeys.costOwnerOptions, a sub-key of the server key"
  - "ServerCostOwnerOption type; GameServer.costOwnerIsFrozen and GameServer.costOwnerAssignedAt, null by default"
affects: [71-07, 71-08, 71-09, 71-10, 71-11, 71-12, server-cost-owner-panel, parity-matrix]

actuals:
  tokens: 8200
  tasks: 2
  commits: 2
plan_head_before: 7ffc03082038a17ef2f5be1e08e81307bc426d9d
plan_head_after: 0ed229ca7f2c3e481f89b04d0fbbcb6c9322c02f

tech-stack:
  added: []
  patterns:
    - "Mutation hook with hook-level onSuccess and onError that invalidate exact keys, so every caller gets the same refresh"
    - "Options prefix derived from the options key itself (key minus its search segment), one source of truth"

key-files:
  created:
    - sources/ReactApp/src/features/hosting/useCostOwnerTransfer.ts
    - sources/ReactApp/tests/hosting/cost-owner-transfer.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/tests/helpers/fleetFixtures.ts
    - sources/ReactApp/tests/hosting/services.test.ts
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
    - sources/ReactApp/tests/hosting/parity-matrix.test.ts
    - .planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md

key-decisions:
  - "transferBlocker reads server.deployment !== null, the API's unresolved-deployment predicate, and never the command-center deployment-active helper, which would allow a transfer for a stopped but unresolved deployment that the API rejects with 409"
  - "A transfer to the current owner is a normal 204 success; no special case exists in the hook"
  - "The hook-level onError refetches only what the failure makes stale: the exact server for a conflict, the options prefix for an unavailable owner, nothing for forbidden or generic"
  - "The server message of a failed transfer is never read; the classifier looks at ApiError.status only"

patterns-established:
  - "Owner options live under hostingQueryKeys.server(id), so exact invalidation of a fleet frame never reaches them (D-14)"

requirements-completed: [D-12, D-14]

coverage:
  - id: D1
    description: "A transfer PUTs { costOwnerId } to the server cost-owner route and a 204 (also for the unchanged owner) is a success"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#puts the chosen owner to the server endpoint and refreshes exactly the four affected entries"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#counts a 204 for the unchanged owner as a success"
        status: pass
      - kind: unit
        ref: "tests/hosting/services.test.ts#puts the chosen cost owner to the server cost owner endpoint and accepts a 204"
        status: pass
    human_judgment: false
  - id: D2
    description: "A success invalidates the exact server, the exact fleet, the exact accrued cost and the owner options prefix, and nothing else"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#puts the chosen owner to the server endpoint and refreshes exactly the four affected entries"
        status: pass
    human_judgment: false
  - id: D3
    description: "Failures are classified by status (409 conflict, 400 unavailable, 403 forbidden, anything else generic) and a conflict refetches the exact server, an unavailable owner refetches the options"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#classifies HTTP %i as %s"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#refetches the exact server after a conflict and nothing else"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#refetches the owner options after an unavailable owner and nothing else"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#invalidates nothing after a forbidden or generic failure"
        status: pass
    human_judgment: false
  - id: D4
    description: "Owner options load with search and take 20, an empty answer is an empty list, and the options key sits under the server key"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/services.test.ts#lists the cost owner options with the search text and a take of 20"
        status: pass
      - kind: unit
        ref: "tests/hosting/services.test.ts#returns an empty owner list for an empty body and for an empty array"
        status: pass
      - kind: unit
        ref: "tests/hosting/services.test.ts#keeps the owner options keys under the server key so a fleet frame never touches them"
        status: pass
    human_judgment: false
  - id: D5
    description: "Legacy payloads normalize costOwnerIsFrozen and costOwnerAssignedAt to null; the generated contract carries both, the owner kind and the transfer body"
    requirement: "D-12"
    verification:
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts#normalizes a legacy server payload without the cost owner facts to null"
        status: pass
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts#carries the owner kind, the frozen flag, the since instant and the transfer body"
        status: pass
    human_judgment: false
  - id: D6
    description: "transferBlocker equals the API predicate: running for any unresolved deployment (also terminated), jobs for pending jobs without a deployment, null otherwise"
    requirement: "D-12"
    verification:
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#blocks as running for any unresolved deployment, also a terminated one"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#blocks as jobs when no deployment exists and a job is pending"
        status: pass
      - kind: unit
        ref: "tests/hosting/cost-owner-transfer.test.tsx#allows a transfer for a server without a deployment and without pending jobs"
        status: pass
    human_judgment: false
  - id: D7
    description: "Both new service methods have kept phase 71 parity rows with test evidence"
    requirement: "D-14"
    verification:
      - kind: unit
        ref: "tests/hosting/parity-matrix.test.ts#keeps the Phase 71 rows kept with Phase 71 test evidence (FND-07)"
        status: pass
    human_judgment: false

# Metrics
duration: 25min
completed: 2026-11-26
---

# Phase 71 Plan 06: Cost owner transfer data layer Summary

**The cost owner transfer wired below the UI: two `hostingService` methods, an options key under the server key, the frozen and since facts on `GameServer`, a transfer mutation hook that refreshes exactly the server, fleet, accrued cost and owner options and classifies failures by status, and an availability rule that equals the API's 409 predicate.**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-11-26
- **Tasks:** 2 (1 tracer, 1 auto/TDD)
- **Files changed:** 9 (2 created, 7 modified)
- **Commits:** 2 (measured from the plan head ledger)

## Accomplishments

- **Transfer hook (D-14):** `useCostOwnerTransfer(serverId)` runs one PUT. The hook-level `onSuccess` invalidates `hostingQueryKeys.server(id)` exactly, `hostingQueryKeys.fleet` exactly, `hostingQueryKeys.accruedCost(id)` exactly and the owner options prefix of the server. A 204 for the unchanged owner is an ordinary success.
- **Failure classification (T-71-15):** `costOwnerTransferFailure` maps `ApiError` 409 to `conflict`, 400 to `unavailable`, 403 to `forbidden`, and everything else (other statuses, network errors, foreign errors) to `generic`. The server message is never read. The hook-level `onError` refetches the exact server after a conflict and the options prefix after an unavailable owner, and nothing otherwise.
- **Availability rule (D-12, T-71-16):** `transferBlocker(server)` returns `running` while `server.deployment !== null`, else `jobs` while `pendingJobCount > 0`, else `null`. The file never imports the command-center deployment-active helper (acceptance grep is 0).
- **Service and keys:** `changeServerCostOwner(serverId, costOwnerId, signal)` and `serverCostOwnerOptions(serverId, search, signal)` (search plus `take=20`, `?? []`), `hostingQueryKeys.costOwnerOptions(id, search)` under `server(id)`.
- **Server facts:** `GameServer.costOwnerIsFrozen` and `costOwnerAssignedAt` (optional, `boolean | null` and `string | null`), defaulted to `null` in `normalizeServer`; `ServerCostOwnerOption` type; `fleetServer` accepts both as options.
- **Parity:** `SVC-changeServerCostOwner` and `SVC-serverCostOwnerOptions` kept, phase 71, appended to `phase71CapabilityIds`.

## Task Commits

1. **Task 1: tracer, a transfer from the hook through the service with exact invalidations** - `e67426e2` (feat)
2. **Task 2: owner options, the server facts and the availability rule** - `0ed229ca` (feat)

## Verification

- `npx vitest run tests/hosting --maxWorkers=4` (whole hosting directory): 119 files, 2395 passed, 0 failed.
- `npm run typecheck` (`tsc -b`): clean after each task; `eslint` over the touched source and test files: clean; `prettier --check --end-of-line auto` over every touched file: clean.
- Acceptance greps: `async changeServerCostOwner(` 1, `async serverCostOwnerOptions(` 1, `exact: true` in the hook 4 (at least 3), `SVC-changeServerCostOwner ` 1 in the matrix, `costOwnerIsFrozen` in `services.ts` and `fleetFixtures.ts` at least 1 each, `isDeploymentActive` in the hook 0.
- Tracer gate: the `<verify>` is automated only, so after the commit the same command ran end to end (vitest on the three files, typecheck, lint) and passed before the expansion task.
- Not run, because nothing they cover changed: `locales:check` and `locales:unused` (no locale key in this plan), `contracts:check` and `build:assets` (no generated, contract or asset file touched). The whole-project vitest run is left to the orchestrator wave check.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Sample owner kind in a test was not part of the contract**
- **Found during:** Task 2
- **Issue:** The first draft of the contract-shape and services tests used the kind `Association`; the generated `ECostOwnerKind` is `"User" | "Verein" | "ExternalOrganization"`. Vitest does not typecheck, so it passed until `tsc` was considered.
- **Fix:** Used `Verein` before running the typecheck.
- **Files modified:** `tests/hosting/contract-shape.test.ts`, `tests/hosting/services.test.ts`
- **Committed in:** `0ed229ca`

**2. [Plan wording] The options prefix is derived, not hard-coded**
- **Found during:** Task 2
- **Issue:** Task 1 needed the options prefix before Task 2 added `hostingQueryKeys.costOwnerOptions`.
- **Fix:** Task 1 built the prefix from the server key; Task 2 switched it to `hostingQueryKeys.costOwnerOptions(id, "").slice(0, -1)` so the key and the invalidation cannot drift. Task 1 tests assert the literal prefix and still pass.
- **Files modified:** `sources/ReactApp/src/features/hosting/useCostOwnerTransfer.ts`
- **Committed in:** `0ed229ca`

Otherwise the plan was executed as written.

## Cross-plan findings

- **XPF-71-06-1:** The kind vocabulary is `User | Verein | ExternalOrganization`. Any copy or badge that 71-07 to 71-12 map from `ServerCostOwnerOption.kind` must use exactly these three values.
- **XPF-71-06-2:** The `SVC-serverCostOwnerOptions` matrix row already cites `tests/hosting/server-cost-owner-panel.test.tsx`. 71-07 must create the panel test under exactly that name, or the evidence text drifts (the parity guard only checks for a `tests/` path, so a rename would not fail a test).
- **XPF-71-06-3:** `serverCostOwnerOptions` returns `[]` for an empty answer and throws `ApiError` for a failed one, so a panel keeps empty and error apart through the query state (`isError` versus `data?.length === 0`), as XPF-71-03-3 asks. The service itself never swallows an error.
- **XPF-71-06-4:** `transferBlocker` depends on `server.deployment` and `server.pendingJobCount`, both present in list rows and in the detail. The "since" display (`costOwnerAssignedAt`) still needs the detail query (XPF-71-03-1); the fixture option exists for it.
- **XPF-71-06-5:** The mutation error is the raw `ApiError`; callers pass it to `costOwnerTransferFailure` and map the result to fixed copy keys. The invalidations run in the hook, so a panel must not add its own.

## Known Stubs

None. No placeholder data or unwired value was introduced.

## Threat Flags

None. The only new surface is the existing PUT route and the already-contracted options route, both covered by the plan's threat model (T-71-15, T-71-16).

## Self-Check: PASSED

- FOUND: `sources/ReactApp/src/features/hosting/useCostOwnerTransfer.ts`
- FOUND: `sources/ReactApp/tests/hosting/cost-owner-transfer.test.tsx`
- FOUND commit `e67426e2`
- FOUND commit `0ed229ca`
