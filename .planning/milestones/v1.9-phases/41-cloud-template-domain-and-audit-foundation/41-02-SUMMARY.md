---
phase: 41-cloud-template-domain-and-audit-foundation
plan: 02
subsystem: api
tags: [aspnet-core, ef-core, hosting, templates, audit]
requires:
  - phase: 41-cloud-template-domain-and-audit-foundation
    plan: 01
    provides: durable cloud server template schema and audit event vocabulary
provides:
  - secret-safe cloud server template DTO and mutation payloads
  - versioned cloud server template service with validation and audit records
  - administrator cloud server template controller
affects: [cloud-template-management, admin-api, audit]
tech-stack:
  added: []
  patterns: [controller-service-dto, version-snapshot-service, sanitized-audit-records]
key-files:
  created:
    - sources/Api/DTO/CloudServerTemplateDto.cs
    - sources/Api/DTO/Payloads/CloudServerTemplateConfigurationPayload.cs
    - sources/Api/DTO/Payloads/CloudServerTemplateDisablePayload.cs
    - sources/Api/Services/CloudTemplates/ICloudServerTemplateService.cs
    - sources/Api/Services/CloudTemplates/CloudServerTemplateService.cs
    - sources/Api/Controllers/CloudServerTemplatesController.cs
    - tests/Api.Tests/CloudTemplates/CloudServerTemplateServiceTests.cs
    - tests/Api.Tests/CloudTemplates/CloudServerTemplateControllerTests.cs
  modified:
    - sources/Api/Constants.cs
    - sources/Api/Transform.cs
key-decisions:
  - "Template mutations are append-only version operations: update and disable supersede the current row and create the next current row."
  - "Missing current template rows are represented as KeyNotFoundException so the controller can return NotFound distinctly from validation BadRequest."
  - "Audit reasons are sanitized before being stored on template rows and before audit details become durable."
patterns-established:
  - "Cloud template service has no provider client, HttpClient, Steam, worker dispatch, provisioning, or teardown dependency."
  - "Controller tests verify route metadata and authorization attributes directly without requiring Aspire or provider calls."
requirements-completed: [TPL-01, TPL-04]
duration: 12 min
completed: 2026-06-21
---

# Phase 41 Plan 02: Cloud Server Template API Contracts And Admin Service Summary

**Admin-authorized template management API with versioned service operations and secret-safe transport contracts**

## Performance

- **Duration:** 12 min
- **Started:** 2026-06-21T22:26:00Z
- **Completed:** 2026-06-21T22:38:00Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- Added `CloudServerTemplateDto`, create/update payload, disable payload, route constant, and allowlisted `Transform.ToDto` projection.
- Added `ICloudServerTemplateService` and `CloudServerTemplateService` for create, update-as-new-version, disable-as-new-version, list, current lookup, validation, and audit recording.
- Added `CloudServerTemplatesController` with admin-only read and mutation endpoints.
- Added service tests for create/update/disable versioning, persistence reload, audit rows, validation, and dependency boundaries.
- Added controller tests for route metadata, server-admin authorization attributes, actor resolution, HTTP result mapping, not-found handling, and contract redaction terms.

## Task Commits

1. **Task 1: Add route, payload, DTO, and transform contracts** - `4ded13e` (feat)
2. **Tasks 2-3: Add template service, controller, and focused tests** - `187774a` (feat)

**Plan metadata:** pending current commit

## Files Created/Modified

- `sources/Api/Constants.cs` - Added `CloudServerTemplates` route constant.
- `sources/Api/DTO/CloudServerTemplateDto.cs` - Secret-safe template projection.
- `sources/Api/DTO/Payloads/CloudServerTemplateConfigurationPayload.cs` - Create/update payload.
- `sources/Api/DTO/Payloads/CloudServerTemplateDisablePayload.cs` - Disable payload.
- `sources/Api/Transform.cs` - Template entity-to-DTO projection.
- `sources/Api/Services/CloudTemplates/ICloudServerTemplateService.cs` - Service contract.
- `sources/Api/Services/CloudTemplates/CloudServerTemplateService.cs` - Versioning, validation, and audit implementation.
- `sources/Api/Controllers/CloudServerTemplatesController.cs` - Admin template API.
- `tests/Api.Tests/CloudTemplates/CloudServerTemplateServiceTests.cs` - Service coverage.
- `tests/Api.Tests/CloudTemplates/CloudServerTemplateControllerTests.cs` - Controller coverage.

## Decisions Made

- Mapped absent current template versions to `KeyNotFoundException`, allowing the controller to return `404 NotFound` while keeping validation failures as `400 BadRequest`.
- Kept provider selection constrained to Hetzner and game type constrained to the existing Arma 3 controller identifier for this milestone.
- Stored sanitized audit reasons on the template row because DTOs expose operator reason text.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Returned NotFound distinctly for missing current template rows**
- **Found during:** Task 3 (Add admin template controller and controller tests)
- **Issue:** The initial service threw `InvalidOperationException` for missing current rows, which would collide with validation BadRequest mapping.
- **Fix:** Changed missing current template lookup to throw `KeyNotFoundException`; controller maps that to `NotFound`.
- **Files modified:** `sources/Api/Services/CloudTemplates/CloudServerTemplateService.cs`, `sources/Api/Controllers/CloudServerTemplatesController.cs`, `tests/Api.Tests/CloudTemplates/CloudServerTemplateControllerTests.cs`
- **Verification:** Targeted cloud template tests pass.
- **Committed in:** `187774a`

### Execution Notes

- Tasks 2 and 3 were committed together after the service/controller test slice passed, because the controller not-found mapping required the small service exception adjustment.

---

**Total deviations:** 1 auto-fixed (1 missing critical)
**Impact on plan:** HTTP behavior now better matches the plan's not-found acceptance criteria.

## Issues Encountered

None blocking. The test build emits existing XML documentation and nullable warning noise from older files.

## User Setup Required

None - no external provider, Aspire, PostgreSQL, Steam, or worker setup required.

## Verification

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateServiceTests" --no-restore --verbosity normal` - passed with existing warnings.
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplate" --no-restore --verbosity normal` - passed with existing warnings.

## Next Phase Readiness

The admin API, service, and transport contracts are ready for Plan 41-03 expanded persistence, redaction, and invariant tests.

---
*Phase: 41-cloud-template-domain-and-audit-foundation*
*Completed: 2026-06-21*
