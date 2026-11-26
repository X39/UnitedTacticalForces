---
phase: 62-fleet-quick-actions
plan: 16
subsystem: docs
tags: [documentation, websocket, fleet, authorization, gap-closure, decision-log]
status: complete

requires:
  - phase: 62-fleet-quick-actions
    provides: claim-scoped fleet delivery (62-13 per-user registration, 62-14 audience query, 62-15 notifier cutover)
provides:
  - Fleet change signal section of docs/features/react-ui-beta.md describing per-user paths, the publish-time audience, fail-closed delivery and the grant and revoke limitation
  - Decision log fleet bullet recording claim-scoped delivery and the withdrawal of the earlier disclosure acceptance
  - D-13 amendment and new decision D-17 in 62-CONTEXT.md
  - 62-VALIDATION.md with the G-62-2 manual re-test row and the per-task verification rows of plans 62-13 to 62-15
affects: [62-verify-work, 63]

actuals:
  tokens: 4500
  tasks: 2
  commits: 2
plan_head_before: b07f85365fdae238eea843654b3025f8df5124b7
plan_head_after: 69bbc70c9dcce87b83a774cdf368972642f19e83

tech-stack:
  added: []
  patterns:
    - "Amend a withdrawn decision in place: keep the original text for history and append a dated Amended line pointing at the superseding decision"

key-files:
  created: []
  modified:
    - docs/features/react-ui-beta.md
    - docs/features/cloud-based-game-server-hosting/decision-log.md
    - .planning/phases/62-fleet-quick-actions/62-CONTEXT.md
    - .planning/phases/62-fleet-quick-actions/62-VALIDATION.md

key-decisions:
  - "62-16: D-17 recorded: a user receives a fleet change signal for a server only if the REST sv:view rule holds for them at publish time; per-user path game-servers/fleet/{userId}; fail closed; claim and role changes emit no signal (IN-10)"
  - "62-16: D-13 kept verbatim with an Amended 2026-11-19 line; the signal-and-refetch design and the removal of the 10 second poll stand"
  - "62-16: the accepted id and timing disclosure is withdrawn in the operator docs, the hosting decision log and the validation strategy"

patterns-established:
  - "Pipes inside code spans of a Markdown table cell are escaped as \\| so the table stays intact"

requirements-completed: [FLT-04]

coverage:
  - id: D1
    description: "Operator documentation describes claim-scoped delivery, the audience rule, fail-closed behavior and the grant and revoke limitation, and no longer states or accepts the disclosure"
    requirement: FLT-04
    verification:
      - kind: other
        ref: "grep gates: 'Accepted residual disclosure'=0, 'no per-viewer filtering'=0, 'fleet/{userId}'=1, 'fail closed'=1, 'until the next refetch'=1 in docs/features/react-ui-beta.md"
        status: pass
    human_judgment: false
  - id: D2
    description: "Hosting decision log records claim-scoped delivery and withdraws the earlier acceptance while keeping the signal, keep-alive and backplane statements"
    requirement: FLT-04
    verification:
      - kind: other
        ref: "grep gates: 'is accepted. A multi-instance'=0, 'claim-scoped'=1, 'fleet-changed signal'=1; git ls-files --error-unmatch exits 0 for both docs"
        status: pass
    human_judgment: false
  - id: D3
    description: "62-CONTEXT.md carries the D-13 amendment and D-17; 62-VALIDATION.md lists the G-62-2 re-test and six verification rows for plans 62-13 to 62-15"
    requirement: FLT-04
    verification:
      - kind: other
        ref: "grep gates: D-17=2, Amended=1, accepted-disclosure row=0, 'G-62-2 re-test'=1, '^| 62-1[3-5] ' rows=6"
        status: pass
      - kind: unit
        ref: "dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~GameServerHosting (641 passed)"
        status: pass
      - kind: other
        ref: "git diff --quiet 7bec477c -- sources/WebSocketContracts sources/Api/Services/UpdateStreamService sources/ReactApp sources/WebApp exits 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "Deployed claim-scoped delivery: a sv:view user bound to one server receives frames only for that server (re-run of UAT test 2)"
    requirement: FLT-04
    verification: []
    human_judgment: true
    rationale: "Real browser socket, two sessions and real claims; the xunit suites prove the rule and the delivery path but not the deployed behavior"

duration: 6 min
completed: 2026-11-19
---

# Phase 62 Plan 16: Claim-Scoped Fleet Delivery Documentation Summary

**The operator docs, the hosting decision log, the phase context and the validation strategy now describe claim-scoped fleet signal delivery. Each socket is registered on `game-servers/fleet/{userId}`. The audience is resolved at publish time by the same rule as `HasServerPermission` with the view permission. Delivery fails closed. Claim and role changes deliberately emit no signal. The withdrawn acceptance of id and timing disclosure is gone everywhere, and the decision is recorded as D-17.**

## Performance

- **Duration:** about 6 min
- **Started:** 2026-11-19T12:32Z
- **Completed:** 2026-11-19T12:38:21Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- `docs/features/react-ui-beta.md`, "Fleet change signal" section:
  - Lead paragraph: the frame is delivered only to users who may view the server (D-17). The "no per-viewer filtering" clause is removed.
  - Subscription: the client still requests `game-servers/fleet`, and the API registers `game-servers/fleet/{userId}` from the authenticated user. The section gate is unchanged and wide. Holders of `crt:sv` or a bare `sv:view` can connect but never receive frames. The paragraph explains why per-user paths are needed (raw-prefix matching, no recipient identity) and removes the "Accepted residual disclosure" sentences.
  - New Audience paragraph: the rule, identical to `HasServerPermission` with the view permission and backed by a drift test. Archived servers are included, the audience is read once per batch with no cache, and delivery is designed to fail closed, with recovery through the reconnect refetch and the Refresh button (D-15).
  - "Deliberately not signaled": describes what a revoked viewer and a newly granted viewer see ("until the next refetch"), with the IN-10 reason (more than five mutation sites in four files; AGENTS.md forbids broadening shared administration code).
  - Delivery: one frame per server and viewer, each on that viewer's own path, with failures logged per recipient. Process-local (D-14): the audience is read per API instance as well.
- `decision-log.md`: in the single fleet bullet, the acceptance sentence is replaced by the claim-scoped delivery statement (UAT G-62-2, product owner decision) and an explicit withdrawal. The signal, path, placement, 25 second keep-alive and backplane statements are kept, and "fleet-changed signal" still appears exactly once.
- `62-CONTEXT.md`: D-13 is kept verbatim with an "Amended 2026-11-19" line. New D-17 sets out the view rule, the per-user path, per-recipient fail-closed delivery, the unchanged update stream service and contracts, and the grant and revoke limitation. It also says D-17 is not the deferred full-state payload idea.
- `62-VALIDATION.md`: the accepted-disclosure manual row is replaced by "UAT: claim-scoped fleet delivery (G-62-2 re-test)". Six green per-task rows for plans 62-13, 62-14 and 62-15 are added, using the full `dotnet test` command form.

## Task Commits

1. **Task 1: Rewrite the fleet change signal documentation and the decision log** - `2fc7323d` (docs)
2. **Task 2: Amend D-13, record D-17 and update the validation strategy** - `69bbc70c` (docs)

## Files Created/Modified

- `docs/features/react-ui-beta.md` - Fleet change signal section rewritten for claim-scoped delivery
- `docs/features/cloud-based-game-server-hosting/decision-log.md` - fleet decision bullet
- `.planning/phases/62-fleet-quick-actions/62-CONTEXT.md` - D-13 amendment and D-17
- `.planning/phases/62-fleet-quick-actions/62-VALIDATION.md` - manual re-test row and per-task verification rows

## Decisions Made

- The 62-13 task 1 row uses the combined filter `UpdateStreamControllerFleet|FleetSignalTracer`, and the task 2 row uses `UpdateStreamControllerFleet` alone. This matches the two filters the plan names for 62-13 and the actual verify commands of those tasks.
- The pipes in the combined xunit filters are escaped as `\|` inside the table cells, so the GFM table does not split the command column.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Staged the force-added decision log with `git add -u`**
- **Found during:** Task 1 commit
- **Issue:** `git add docs/features/cloud-based-game-server-hosting/decision-log.md` was refused because the directory is covered by `docs/.gitignore`, even though the file is already tracked.
- **Fix:** Staged the two tracked files with `git add -u -- <paths>`, which updates tracked files only. No `-f` was used, and no untracked file was added.
- **Files modified:** none beyond the plan's files
- **Commit:** 2fc7323d

**Total deviations:** 1 auto-fixed (blocking). **Impact:** none on content or scope.

## Issues Encountered

None.

## Known Stubs

None.

## User Setup Required

None - no external service configuration required.

## Verification

- Task 1 text gates: "Accepted residual disclosure" 0, "no per-viewer filtering" 0, "fleet/{userId}" 1, "fail closed" 1, "until the next refetch" 1 in react-ui-beta.md. In decision-log.md: "is accepted. A multi-instance" 0, "claim-scoped" 1, "fleet-changed signal" 1. `git ls-files --error-unmatch` exits 0 for both docs.
- Task 2 text gates: D-17 2, Amended 1, accepted-disclosure row 0, "G-62-2 re-test" 1, `^| 62-1[3-5] ` rows 6.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerHosting"`: 641 passed, 0 failed.
- Guard `git diff --quiet 7bec477c -- sources/WebSocketContracts sources/Api/Services/UpdateStreamService sources/ReactApp sources/WebApp`: exit 0.
- Manual: the G-62-2 re-test (UAT test 2) remains for the end-of-phase review. It is listed in 62-VALIDATION.md.

## Next Phase Readiness

This was the last plan of phase 62, so the phase is complete. Ready for `/gsd-verify-work 62`, including the re-run of UAT test 2 against the deployed build.

---
*Phase: 62-fleet-quick-actions*
*Completed: 2026-11-19*

## Self-Check: PASSED

- All four modified files exist on disk
- Commits 2fc7323d and 69bbc70c are ancestors of HEAD
