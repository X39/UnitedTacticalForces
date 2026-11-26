---
phase: 36-policy-gate-and-approval-ux
plan: 01
subsystem: testing
tags: [xunit, blazor, policy-gate, repository-boundary, redaction]

requires:
  - phase: 34-generated-client-and-repository-gate-foundation
    provides: repository-local Kiota sender, continuation query identity, and source-boundary test patterns
  - phase: 35-dry-run-planning-and-review
    provides: /servers dry-run review source markers and deferred UAT copy
provides:
  - Phase 36 /servers policy gate UX source-boundary tests
  - Repository boundary assertions for policy review, warning acknowledgement, approval continuation, approve, and deny flows
  - RED TDD contract for POLICY-01, POLICY-02, and POLICY-03 implementation plans
affects: [phase-36, phase-37, policy-gate, approval-ux, cost-03]

tech-stack:
  added: []
  patterns:
    - xUnit source-boundary tests over production source files
    - Exact marker arrays for redaction and repository boundary enforcement
    - RED-only TDD contract for later implementation plans

key-files:
  created:
    - tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs
    - .planning/phases/36-policy-gate-and-approval-ux/36-01-SUMMARY.md
  modified:
    - tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs

key-decisions:
  - "Phase 36 Plan 01 intentionally stops at RED boundary tests; production implementation remains assigned to Plans 02-04."
  - "Policy gate UI tests scan the marked /servers region only, using exact forbidden marker arrays to avoid broad false positives."
  - "Repository tests check extracted Phase 36 method bodies so older legacy raw helper code outside those methods does not fail this plan."

patterns-established:
  - "PolicyGateApprovalUxBoundaryTests: marked Razor-region source scans with path:line marker diagnostics."
  - "CloudWorkflowRepositoryBoundaryTests: Phase 36 repository facade checks are scoped to method bodies and approval command support files."

requirements-completed: [POLICY-01, POLICY-02, POLICY-03]

duration: 30min
completed: 2026-06-20
---

# Phase 36 Plan 01: Early Policy Gate UX/Repository Boundary Tests Summary

**RED xUnit boundary tests now define the Phase 36 policy gate UX and repository facade contract before implementation.**

## Performance

- **Duration:** 30 min
- **Started:** 2026-06-20T10:19:00Z
- **Completed:** 2026-06-20T10:49:19Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- Added `PolicyGateApprovalUxBoundaryTests` for `/servers` policy gate copy, repository-only UI boundaries, forbidden field redaction, and deferred UAT copy.
- Extended `CloudWorkflowRepositoryBoundaryTests` with Phase 36 facade, method-body, continuation identity, approval command sender, and approval model checks.
- Verified both focused commands compile and fail RED only because Phase 36 production implementation is not present yet.

## Task Commits

1. **Task 1: Add Phase 36 /servers policy gate source-boundary tests** - `bf1fe07` (test)
2. **Task 2: Extend repository boundary tests for policy review and approval commands** - `84e8246` (test)

**Plan metadata:** summary-only docs commit

## Files Created/Modified

- `tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs` - New source-boundary tests for `ServersPolicyGateApproval` UI copy, redaction, repository use, and deferred UAT text.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - Added Phase 36 repository facade and approval command boundary assertions.
- `.planning/phases/36-policy-gate-and-approval-ux/36-01-SUMMARY.md` - This execution summary.

## Decisions Made

- Kept this plan RED-only as specified. The new tests compile but intentionally fail until Plans 02-04 add the API, repository, safe models, and Razor implementation.
- Scoped UI scans to the future `ServersPolicyGateApproval` marked region rather than scanning all of `Index.razor`.
- Scoped repository raw-marker checks to extracted Phase 36 method bodies and command support files so existing Phase 34/35 helper code remains unchanged.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- The focused test commands return exit code 1 by design because the RED assertions detect missing Phase 36 implementation markers, methods, and model files.
- Existing compiler warnings from API XML documentation and test nullable/xUnit analyzer warnings remain outside this plan's scope.
- The repository had pre-existing unrelated dirty items: `.planning/config.json`, `.planning/tmp/`, and `docs/change-requests/`. They were left untouched and unstaged.
- Utility `rg` stub-scan quoting failed under this PowerShell shell; the equivalent `Select-String` scan completed with no hits.

## Verification

- RED as expected: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --verbosity minimal`
  - Compiled successfully.
  - Failed only for missing `ServersPolicyGateApproval` marker and missing Phase 36 UI/repository facade copy.
- RED as expected: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal`
  - Compiled successfully.
  - Existing Phase 34 checks still ran; new failures are missing Phase 36 facade methods, model files, and command sender support.
- RED as expected: `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PolicyGateApprovalUxBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal`
  - Compiled successfully.
  - Result: 8 passed, 9 failed RED contract assertions.
- PASS: `Select-String` stub scan over the two touched test files returned no TODO/FIXME/placeholder or empty hardcoded UI stubs.
- DEFERRED: Manual authenticated browser UAT, live Hetzner, Steam, Arma, provider mutation, provisioning, teardown, and Phase 30 evidence remain deferred by instruction and were not claimed.

## TDD Gate Compliance

This plan is an intentional RED contract plan. It produced test-only commits and does not include GREEN implementation commits because implementation is explicitly assigned to later Phase 36 plans.

## Known Stubs

None.

## Threat Flags

None. The plan adds tests only and introduces no network endpoints, auth paths, file access paths beyond repository-local source reads, or runtime trust boundaries.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Ready for Phase 36 Plan 02. The next implementation must satisfy the RED tests by adding safe policy approval repository models/senders/facades and keeping Razor bound to repository-safe models only.

## Self-Check: PASSED

- Found `tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs`.
- Found `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs`.
- Found commits `bf1fe07` and `84e8246` in git history.
- Summary file created at `.planning/phases/36-policy-gate-and-approval-ux/36-01-SUMMARY.md`.

---
*Phase: 36-policy-gate-and-approval-ux*
*Completed: 2026-06-20*
