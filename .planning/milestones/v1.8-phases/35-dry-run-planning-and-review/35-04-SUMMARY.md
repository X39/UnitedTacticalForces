---
phase: 35-dry-run-planning-and-review
plan: 04
subsystem: generated-client
tags: [kiota, blazor, webapp, dry-run-review, generated-client]

requires:
  - phase: 35-dry-run-planning-and-review
    provides: owner-safe dry-run review API contract from Plan 02
provides:
  - Generated Kiota request builder for Arma 3 dry-run review
  - Generated WebApp models for owner-safe dry-run review DTOs and payload
  - Generated-client verification showing no forbidden raw or secret fields in review artifacts
affects: [phase-35, dry-run-review, webapp-repository-plan-35-05, servers-ui-plan-35-06]

tech-stack:
  added: []
  patterns:
    - Narrow Kiota generation from the current local OpenAPI document for one route slice
    - Generated review endpoint rooted at Client.GameServers[gameServerId].CloudDryRunPlans.Arma3.Review
    - Generated DTOs remain behind the planned repository facade before Razor binding

key-files:
  created:
    - sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Review/ReviewRequestBuilder.cs
    - sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewDto.cs
    - sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewStateDto.cs
    - sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewResourceDto.cs
    - sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanCostBreakdownDto.cs
    - sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewRequestPayload.cs
  modified:
    - sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Arma3RequestBuilder.cs

key-decisions:
  - "Used narrow Kiota generation for `/game-servers/*/cloud-dry-run-plans/arma3/review#POST` after full lockfile generation produced unrelated churn and build breakage."
  - "Kept the review request builder under the generated Arma3 route segment because the API route is `/cloud-dry-run-plans/arma3/review`."
  - "AdditionalData remains confined to generated Kiota models and must be projected into hand-written safe review models in Plan 35-05."

patterns-established:
  - "Generated review client surface exposes `Client.GameServers[gameServerId].CloudDryRunPlans.Arma3.Review.PostAsync(...)` for repository consumption."
  - "Generated artifact scans exclude Kiota extension-data mechanics only when confined to generated files."

requirements-completed: [PLAN-01, PLAN-02, PLAN-03]

duration: resumed close-out
completed: 2026-06-20
---

# Phase 35 Plan 04: Generated Kiota Review Client Surface Summary

**Kiota-generated WebApp client surface for non-mutating Arma 3 dry-run review with owner-safe review DTO models.**

## Performance

- **Duration:** resumed close-out after generator recovery
- **Started:** 2026-06-20T03:52:11Z
- **Completed:** 2026-06-20T09:24:09Z
- **Tasks:** 2
- **Files modified:** 7 generated client files

## Accomplishments

- Added the generated review route builder at `Client.GameServers[gameServerId].CloudDryRunPlans.Arma3.Review`.
- Added generated Kiota models for review projection, review state, planned resource rows, cost breakdown, and review request payload.
- Preserved the existing generated Arma 3 dry-run creation POST method while adding the generated `Review` child builder.
- Verified the WebApp project compiles and the generated review artifacts contain no forbidden raw or secret field names.

## Task Commits

Each implementation task was handled atomically:

1. **Task 1: Add generated review request builder and models** - `7bc3772` (feat)
2. **Task 2: Scan generated review artifacts for forbidden surface drift** - verification-only task, no file changes beyond `7bc3772`

## Files Created/Modified

- `sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Review/ReviewRequestBuilder.cs` - Generated Kiota POST builder for `/game-servers/{gameServerId}/cloud-dry-run-plans/arma3/review`.
- `sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Arma3RequestBuilder.cs` - Generated `Review` child builder property added without removing the existing Arma 3 dry-run POST method.
- `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewDto.cs` - Generated review projection model including cost source label and typed review fields.
- `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewStateDto.cs` - Generated review state model.
- `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewResourceDto.cs` - Generated planned-resource row model.
- `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanCostBreakdownDto.cs` - Generated cost breakdown model.
- `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewRequestPayload.cs` - Generated review request payload model.

## Decisions Made

- Used a narrow Kiota route slice after full lockfile generation against the current OpenAPI document created broad unrelated generated-code churn and broke existing repository compilation.
- Kept the generated route path under `Arma3/Review` because the API contract from Plan 35-02 exposes `POST /game-servers/{gameServerId}/cloud-dry-run-plans/arma3/review`.
- Left all generated DTOs as generated transport types for Plan 35-05 to map into hand-written repository-safe models before Razor.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Recovered generator path with narrow Kiota route generation**
- **Found during:** Task 1 (Add generated review request builder and models)
- **Issue:** Lockfile/full Kiota generation against the current OpenAPI document produced broad unrelated generated-code churn and 44 WebApp build errors, which was unsafe for this narrow plan.
- **Fix:** Discarded the failed broad output, generated a focused Kiota client slice from the API-produced local OpenAPI JSON using include path `/game-servers/*/cloud-dry-run-plans/arma3/review#POST`, and copied the generated review artifacts verbatim into the existing generated client tree.
- **Files modified:** `sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Arma3RequestBuilder.cs`, `sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Review/ReviewRequestBuilder.cs`, `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlan*.cs`
- **Verification:** `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal` passed with 39 existing generated nullable warnings and 0 errors; generated-artifact forbidden field scan passed.
- **Committed in:** `7bc3772`

---

**Total deviations:** 1 auto-fixed (1 blocking generator recovery)
**Impact on plan:** The generated-client boundary was preserved. No manual generated request builders or models were authored, and unrelated full-generation churn was not retained.

## Issues Encountered

- Initial generator recovery was blocked until the local API OpenAPI endpoint became reachable through Aspire.
- Full Kiota generation was too broad for this plan and incompatible with existing hand-written repository code, so only the focused route-slice Kiota output was retained.
- Existing generated nullable warnings in `CloudSpendingPolicy*` WebApp models remain out of scope.

## Verification

- PASS: `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal` completed with 39 existing generated nullable warnings and 0 errors.
- PASS: Generated artifact scan over the review request builder and review DTO model files found no `ValidationDetailsJson`, `PlannedOperationsJson`, `DetailsJson`, `TokenReference`, `AuthorizationHeader`, `BearerToken`, `SteamPassword`, `SteamApiKey`, `ProviderToken`, `WorkerCredential`, `WorkerHash`, `RawJson`, `RawPayload`, `ExceptionBody`, or `ResponseHeaders`.
- PASS: Source checks confirmed `Arma3RequestBuilder` exposes a generated `Review` property, `ReviewRequestBuilder` exposes `ToPostRequestInformation`, and `Arma3CloudDryRunPlanReviewDto` contains `CostSourceLabel`.
- DEFERRED: Manual, authenticated browser, live provider, Steam, Arma, Hetzner, provider mutation, provisioning, teardown, and Phase 30 UAT were not run and remain deferred by instruction.

## Known Stubs

None.

## Threat Flags

None beyond the planned generated-client trust boundary. Generated Kiota `AdditionalData` remains confined to generated models and is explicitly assigned to Plan 35-05 repository projection before UI binding.

## User Setup Required

None - no external service configuration required for this plan.

## Next Phase Readiness

Ready for Phase 35 Plan 05 to map the generated review DTOs into repository-safe hand-written models and preserve the no-raw-browser-HTTP and no-generated-DTO-in-Razor boundaries.

## Self-Check: PASSED

- Found summary file: `.planning/phases/35-dry-run-planning-and-review/35-04-SUMMARY.md`
- Found generated review builder: `sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Review/ReviewRequestBuilder.cs`
- Found generated review DTO: `sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanReviewDto.cs`
- Found task commit `7bc3772` in git history.

---
*Phase: 35-dry-run-planning-and-review*
*Completed: 2026-06-20*
