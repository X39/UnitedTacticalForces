# Phase 41 Research: Cloud Template Domain And Audit Foundation

<user_constraints>

- Keep the existing game-controller abstraction; do not replace current Arma 3 support.
- Lifecycle, job, worker, ownership, and audit state must remain persistent across API restarts.
- Every hosting or cost-generating session must have an owner from creation through cleanup or transfer.
- Do not implement Hetzner provisioning or create provider resources in this phase.
- Do not expose Steam credentials, provider tokens, worker credentials, cookies, authorization headers, bearer tokens, or raw secret-bearing payloads to clients or unredacted logs.
- Existing Aspire, PostgreSQL, API, and Blazor WebApp development flows must keep working.
- Add focused tests around persistence, versioning, authorization-sensitive actions, audit, redaction, and recovery-safe state.

</user_constraints>

## Question

What does Phase 41 need to know before planning a durable administrator-managed cloud template foundation?

## Phase Requirements

| ID | Requirement | Phase 41 Interpretation |
|----|-------------|-------------------------|
| TPL-01 | Administrator can create, update, disable, and inspect versioned cloud server templates for supported game type, provider, server type, location, storage, ports, runtime defaults, and override limits. | Add an API-owned, durable template version snapshot model plus admin service/controller operations for create, update-as-new-version, disable-as-new-version, and inspect/list. |
| TPL-04 | Template create, update, disable, selection, and provisioning-use events are authorization-sensitive, audited, and projected without provider tokens, Steam credentials, worker credentials, authorization headers, bearer tokens, cookies, or raw secret-bearing payloads. | Phase 41 implements create/update/disable/inspect authorization, audit event kinds and audit rows for admin mutations, and allowlisted DTOs. Selection and provisioning-use emission is prepared by audit event vocabulary and redaction tests; actual selection/provisioning paths are Phase 43 scope. |

## Scope Boundary

Phase 41 is API foundation only. It must not add WebApp template management screens, guided-entry template selection, dry-run template snapshots, provider provisioning checks, runtime-profile behavior, or live provider UAT. Those belong to Phases 42-44 in the roadmap.

## Project Constraints From AGENTS.md

- New hand-written C# files use file-scoped namespaces, nullable references, one public type per file, PascalCase public types, `_` private fields, async suffixes, and existing API/DTO/service folder conventions.
- Persisted server-side entities live under `sources/Api/Data/Hosting` and are exposed through `ApiDbContext`.
- API response/request shapes use DTO and Payload records under `sources/Api/DTO` and `sources/Api/DTO/Payloads`; server entities are not returned directly.
- Controllers use `[ApiController]`, route constants, `[Authorize]`, `Claims.Administrative.Server` or resource policies, `GetDatabaseUserAsync`, and HTTP result methods.
- Application services use `X39.Util.DependencyInjection` attributes and existing interfaces.
- Tests use xUnit, SQLite in-memory EF contexts, and focused API/service tests under `tests/Api.Tests`.
- Do not hand-edit generated WebApp Kiota files or generated EF designer/snapshot files except as generated output from EF tooling.
- Do not log or expose secret values from config, provider payloads, worker credentials, or auth/session material.

## Standard Stack

| Concern | Decision | Evidence |
|---------|----------|----------|
| Persistence | EF Core 9 with Npgsql production model and SQLite tests. | `sources/Api/Data/ApiDbContext.cs`, `tests/Api.Tests/**/*.cs` |
| API | ASP.NET Core controllers with route constants and policy attributes. | `CloudInventoryController`, `CloudSpendingPoliciesController`, `CloudPlanningController` |
| Service registration | `[Scoped<Implementation, Interface>]` attributes. | `CloudInventoryService`, `CloudSpendingPolicyConfigurationService` |
| Audit | Append `HostingAuditRecord` through `IHostingAuditService`. | `CloudInventoryService.CreateDryRunPlanAsync`, `HostingAuditService` |
| Sanitization | Use `LifecycleFailureReasonSanitizer` via audit service and avoid raw payload persistence. | `HostingAuditService`, cloud inventory JSON redaction |
| Tests | `tests/Api.Tests/Api.Tests.csproj` with xUnit and SQLite. | Existing cloud policy, planning, inventory, preflight tests |

No package additions are required.

## Architectural Responsibility Map

| Tier | Owns | Must Not Own |
|------|------|--------------|
| Contract/API constants | Route string and audit event enum values needed by API and later consumers. | Secret values, generated client edits, provider calls. |
| API data model | Durable `CloudServerTemplate` version snapshots, keys, indexes, bounded columns, and restrict-delete relationships. | Live provider resource state or runtime-profile details. |
| API service | Template validation, create/update/disable version transitions, current-version lookup, and audit append calls. | HTTP response shaping, frontend view state, provider provisioning. |
| API controller | Admin authorization, actor resolution, route/payload binding, and HTTP status translation. | Validation internals or direct audit row construction. |
| DTO/payload/transform layer | Allowlisted template projections and bounded mutation commands. | Raw request JSON, tokens, headers, cookies, Steam credentials, worker hashes. |
| Tests | Persistence/versioning/audit/auth/redaction verification. | Aspire, PostgreSQL, browser automation, live Hetzner, or Steam runtime UAT. |
| WebApp | Out of scope for Phase 41; Phase 44 consumes generated/repository surfaces. | Direct raw HTTP bridges or generated DTO binding in this phase. |

## Recommended Domain Model

Use one `CloudServerTemplate` row per version snapshot:

- `PrimaryKey`: database row id for this version snapshot.
- `TemplateId`: stable `Guid` shared by all versions of one logical template.
- `Version`: monotonic integer per `TemplateId`.
- `IsCurrent`: one current row per `TemplateId`.
- `IsEnabled`: whether the current version can be selected by later phases.
- `SupersededByTemplateFk` and `TimeStampSuperseded`: preserve old versions and link to the replacement version.
- `GameTypeIdentifier`: start with `Arma3GameServerController.Identifier`.
- `ProviderIdentifier`: start with `HetznerProviderConfiguration.HetznerProviderIdentifier`.
- Provider shape defaults: server type, location, datacenter, image.
- Storage defaults and bounds: default/min/max GiB plus storage override flag.
- Port defaults and bounds: game, query, Steam, optional RCON, min/max port, port override flag, RCON allowed flag.
- Runtime defaults and bounds: default/min/max runtime hours.
- Administrative defaults: expose SSH, administrative CIDR, CIDR override flag.
- Update history: created by, updated by, reason, created/updated/superseded timestamps.

Reasoning: a single version-snapshot table satisfies the stable id/version requirement without a parent/current-version circular FK. Historical dry-run/provisioning snapshots in Phase 43 can later reference `TemplateId` plus `Version` or `PrimaryKey`.

## Files To Plan

| File | Role | Notes |
|------|------|-------|
| `sources/Api/Data/Hosting/CloudServerTemplate.cs` | Entity | New version snapshot persistence model. |
| `sources/Api/Data/ApiDbContext.cs` | EF model | Add DbSet, indexes, unique keys, restrict delete relationships. |
| `sources/Contract/GameServer/EHostingAuditEventKind.cs` | Contract enum | Add template create/update/disable/selection/provisioning-use audit event kinds. |
| `sources/Api/DTO/CloudServerTemplateDto.cs` | DTO | Allowlisted admin/creator projection. |
| `sources/Api/DTO/Payloads/CloudServerTemplateConfigurationPayload.cs` | Payload | Create/update version command. |
| `sources/Api/DTO/Payloads/CloudServerTemplateDisablePayload.cs` | Payload | Disable-as-new-version command. |
| `sources/Api/Transform.cs` | Transform | Entity to DTO mapping. |
| `sources/Api/Services/CloudTemplates/ICloudServerTemplateService.cs` | Service contract | Admin template operations. |
| `sources/Api/Services/CloudTemplates/CloudServerTemplateService.cs` | Service implementation | Validation, versioning, audit. |
| `sources/Api/Controllers/CloudServerTemplatesController.cs` | Controller | Admin routes and HTTP translation. |
| `sources/Api/Constants.cs` | Route constants | Add `cloud-server-templates`. |
| `sources/Api/Data/Migrations/*` | Migration | Add template table and model snapshot. |
| `tests/Api.Tests/CloudTemplates/CloudServerTemplateServiceTests.cs` | Tests | Persistence, versioning, disable, validation, audit. |
| `tests/Api.Tests/CloudTemplates/CloudServerTemplateControllerTests.cs` | Tests | Authorization attributes, actor resolution, response shapes. |
| `tests/Api.Tests/CloudTemplates/CloudServerTemplateRedactionTests.cs` | Tests | DTO/payload/audit forbidden-term checks. |

## Architecture Patterns

1. Controller pattern: copy `CloudSpendingPoliciesController` for admin-only route, actor resolution, `BadRequest` on validation exceptions, and DTO projection.
2. Service pattern: copy `CloudSpendingPolicyConfigurationService` for payload validation, normalized provider identifier, bounded strings, timestamp updates, and `InvalidOperationException` for missing rows.
3. Versioning pattern: copy `Arma3CloudDryRunPlannerService.SupersedePreviousUsablePlansAsync` for marking previous records superseded while keeping them queryable.
4. Audit pattern: copy `CloudInventoryService.CreateDryRunPlanAsync` and `TransitionResourceStateAsync` for transactional state change plus `IHostingAuditService.AppendAsync`.
5. DTO mapping pattern: add an extension method to `Transform.cs`, matching existing `ToDto` methods.
6. Tests pattern: copy SQLite setup from cloud policy/planning tests, including `DateTimeOffsetToBinaryConverter`, `EnsureCreatedAsync`, and direct controller instantiation with fake services.

## Do Not Hand Roll

- Do not create a custom persistence layer or raw SQL for normal template operations; use EF Core.
- Do not implement a new authorization system; use existing `Claims.Administrative.Server` for admin template management.
- Do not create provider clients, resolve Hetzner tokens, call Steam, or enqueue workers.
- Do not persist raw request payloads or arbitrary JSON for template shape if structured fields can express the allowed values.
- Do not edit generated `sources/WebApp/UtfApi/**` in Phase 41.

## Common Pitfalls

| Pitfall | Mitigation |
|---------|------------|
| Updating a row in place destroys history. | Every update/disable creates a new version row and marks the previous current row superseded. |
| Multiple current versions for one template. | Add a filtered unique index on `TemplateId` where `IsCurrent` is true. |
| Template bounds allow invalid defaults. | Validate min/default/max relationships for storage, runtime hours, and ports before persisting. |
| Secret vocabulary enters audit details. | Use bounded reason fields and rely on `IHostingAuditService` sanitization; tests assert forbidden markers are absent. |
| Phase 41 absorbs Phase 43/44 work. | Only create the foundation and safe projections; do not wire selection, dry-run, provisioning, or WebApp views. |

## Code Examples

### Admin Controller Shape

`CloudSpendingPoliciesController` shows the target structure:

- `[ApiController]`
- `[Route(Constants.Routes.CloudSpendingPolicies)]`
- `[Authorize]`
- Admin endpoints with `[Authorize(Claims.Administrative.Server)]`
- `GetActorAsync` resolves `ClaimsPrincipal` to database `User`
- `ArgumentException`, `ArgumentOutOfRangeException`, and `InvalidOperationException` map to `BadRequest`

### Service Validation Shape

`CloudSpendingPolicyConfigurationService.SavePolicyAsync` shows the validation and timestamp pattern:

- `ArgumentNullException.ThrowIfNull(payload)`
- `ValidatePayload(payload)`
- look up related rows before mutation
- set created fields for new rows and update fields for existing rows
- normalize provider identifier and bounded reason text
- call `SaveChangesAsync`

### Supersession Shape

`Arma3CloudDryRunPlannerService.SupersedePreviousUsablePlansAsync` marks prior usable rows with:

- a timestamp
- a link to the replacement row
- a non-current or superseded status
- a final `SaveChangesAsync`

Template updates should follow the same persistence idea while preserving disabled and superseded versions for future snapshots.

## Validation Architecture

Phase 41 can be verified without Aspire, PostgreSQL, browser sessions, Steam, or live Hetzner:

| Layer | Test Type | Command |
|-------|-----------|---------|
| Service | SQLite xUnit tests for create/update/disable/versioning/audit/validation. | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateServiceTests" --no-restore --verbosity normal` |
| Controller | Reflection plus direct controller tests for route constants, admin authorization, actor resolution, and HTTP result mapping. | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateControllerTests" --no-restore --verbosity normal` |
| Redaction | DTO/payload/audit serialization checks for forbidden field names and values. | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudServerTemplateRedactionTests" --no-restore --verbosity normal` |
| Build | API and full solution compile after EF model/migration output. | `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity normal` |

## Security Domain

| Threat | Risk | Mitigation |
|--------|------|------------|
| Unauthorized template mutation | Admin-only server shapes could affect later cost-generating paths. | Controller methods require `Claims.Administrative.Server`; tests verify route attributes. |
| History tampering or repudiation | Administrators need durable update history and audit. | Version snapshots keep old rows; `HostingAuditRecord` captures actor, timestamp, kind, summary, and reason. |
| Secret leakage through projections | Template payloads or audit could accidentally include secret-bearing text. | DTOs are allowlisted, text fields are bounded/sanitized, and tests scan serialized projections and audit rows. |
| Invalid provider shape defaults | Later dry-run/provisioning could inherit bad ports, storage, or runtime bounds. | Service validates ranges and defaults before persistence. |
| Scope creep into live provider actions | Template management must not create billable resources. | No provider client dependencies; tests check service/controller constructors do not depend on `IHetznerCloudClient`, `HttpClient`, Steam, or worker runtime services. |

## Open Questions (RESOLVED)

1. **Stable id type** - RESOLVED: Use a `Guid TemplateId` shared by all versions because it is provider-independent, durable, and easy to reference from later snapshots.
2. **Version storage** - RESOLVED: Use one row per version snapshot instead of a parent/version pair to avoid circular current-version FK complexity.
3. **Selection/provisioning-use audit in TPL-04** - RESOLVED: Add audit event kinds and redaction tests in Phase 41; actual selection and provisioning-use event emission happens in Phase 43 where those workflows exist.
4. **Frontend implementation** - RESOLVED: No WebApp implementation in Phase 41; Phase 44 owns repository and UI consumption.

## Environment Availability

- `dotnet --version` returned `10.0.201`; projects target `net9.0`.
- `dotnet ef --version` returned `10.0.5`; migration generation is available, though generated files must still be reviewed.
- Existing API test project is `tests/Api.Tests/Api.Tests.csproj`; do not create a second API test project.

## Sources

- `AGENTS.md`
- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/phases/41-cloud-template-domain-and-audit-foundation/41-CONTEXT.md`
- `.planning/phases/41-cloud-template-domain-and-audit-foundation/41-UI-SPEC.md`
- `sources/Api/Data/ApiDbContext.cs`
- `sources/Api/Data/Hosting/HostingAuditRecord.cs`
- `sources/Api/Services/GameServerLifecycle/HostingAuditService.cs`
- `sources/Api/Controllers/CloudSpendingPoliciesController.cs`
- `sources/Api/Services/CloudPolicies/CloudSpendingPolicyConfigurationService.cs`
- `sources/Api/Services/CloudInventory/CloudInventoryService.cs`
- `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs`
- `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyControllerTests.cs`
- `tests/Api.Tests/CloudPlanning/Arma3CloudPlannerTests.cs`
- `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs`

## Research Complete

The plan can proceed with an API-only foundation: durable versioned template snapshots, admin service/controller APIs, audit event vocabulary, redacted DTOs, EF migration, and focused SQLite/controller/redaction tests.
