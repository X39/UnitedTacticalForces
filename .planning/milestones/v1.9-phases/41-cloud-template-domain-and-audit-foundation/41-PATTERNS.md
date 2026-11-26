# Phase 41: Cloud Template Domain And Audit Foundation - Pattern Map

**Mapped:** 2026-06-21
**Files analyzed:** 15 planned files
**Analogs found:** 15 / 15

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/Api/Data/Hosting/CloudServerTemplate.cs` | model | CRUD/versioning | `sources/Api/Data/Hosting/CloudSpendingPolicy.cs` | role-match |
| `sources/Api/Data/ApiDbContext.cs` | config/model | CRUD/versioning | `ApiDbContext` cloud hosting relationships | exact |
| `sources/Contract/GameServer/EHostingAuditEventKind.cs` | contract enum | event-driven | existing cloud audit event values | exact |
| `sources/Api/DTO/CloudServerTemplateDto.cs` | DTO | transform | `CloudSpendingPolicyDto` | exact |
| `sources/Api/DTO/Payloads/CloudServerTemplateConfigurationPayload.cs` | payload | request-response | `CloudSpendingPolicyConfigurationPayload` | role-match |
| `sources/Api/DTO/Payloads/CloudServerTemplateDisablePayload.cs` | payload | request-response | `CloudSpendingPolicyApprovalDecisionPayload` | role-match |
| `sources/Api/Transform.cs` | utility | transform | `CloudSpendingPolicy.ToDto` | exact |
| `sources/Api/Services/CloudTemplates/ICloudServerTemplateService.cs` | service contract | CRUD/versioning | `ICloudSpendingPolicyConfigurationService` | role-match |
| `sources/Api/Services/CloudTemplates/CloudServerTemplateService.cs` | service | CRUD/versioning/audit | `CloudSpendingPolicyConfigurationService`, `CloudInventoryService` | role-match |
| `sources/Api/Controllers/CloudServerTemplatesController.cs` | controller | request-response | `CloudSpendingPoliciesController` | exact |
| `sources/Api/Constants.cs` | config | route constants | `Constants.Routes.CloudSpendingPolicies` | exact |
| `sources/Api/Data/Migrations/*` | migration | schema | `20260618095241_CloudSpendingPolicies.cs` | role-match |
| `tests/Api.Tests/CloudTemplates/CloudServerTemplateServiceTests.cs` | test | CRUD/versioning | `CloudSpendingPolicyConfigurationTests`, `Arma3CloudPlannerTests` | role-match |
| `tests/Api.Tests/CloudTemplates/CloudServerTemplateControllerTests.cs` | test | request-response | `CloudSpendingPolicyControllerTests` | exact |
| `tests/Api.Tests/CloudTemplates/CloudServerTemplateRedactionTests.cs` | test | security/redaction | `CloudSpendingPolicyControllerTests`, `CloudInventoryVisibilityTests` | role-match |

## Pattern Assignments

### `CloudServerTemplatesController.cs` (controller, request-response)

**Analog:** `sources/Api/Controllers/CloudSpendingPoliciesController.cs`

**Imports/auth/route pattern** (lines 1-19):

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route(Constants.Routes.CloudSpendingPolicies)]
[Authorize]
public class CloudSpendingPoliciesController(...) : ControllerBase
```

Apply the same shape with `Constants.Routes.CloudServerTemplates` and method-level `[Authorize(Claims.Administrative.Server)]`.

**Mutation endpoint pattern** (lines 129-159):

```csharp
[HttpPost("", Name = nameof(SavePolicyAsync))]
[Authorize(Claims.Administrative.Server)]
public async Task<IActionResult> SavePolicyAsync(...)
{
    var user = await GetActorAsync(cancellationToken).ConfigureAwait(false);
    if (user is null)
        return Unauthorized();

    try
    {
        var policy = await cloudSpendingPolicyConfigurationService.SavePolicyAsync(...);
        return Ok(policy.ToDto());
    }
    catch (ArgumentException)
    {
        return BadRequest();
    }
    catch (InvalidOperationException)
    {
        return BadRequest();
    }
}
```

Use the same exception-to-HTTP translation for create/update/disable.

**Actor resolution pattern** (lines 408-420): private `GetActorAsync(CancellationToken)` reads `ControllerContext.HttpContext.User`, calls `GetDatabaseUserAsync`, and returns null for `InvalidOperationException`.

### `CloudServerTemplateService.cs` (service, CRUD/versioning/audit)

**Analogs:** `CloudSpendingPolicyConfigurationService`, `CloudInventoryService`, `Arma3CloudDryRunPlannerService`

**Validation and normalization pattern** (`CloudSpendingPolicyConfigurationService`, lines 23-76 and 121-207):

```csharp
ArgumentNullException.ThrowIfNull(payload);
ArgumentNullException.ThrowIfNull(actor);
ValidatePayload(payload);
...
policy.ProviderIdentifier = NormalizeProviderIdentifier(payload.ProviderIdentifier);
policy.AuditReason = NormalizeAuditReason(payload.AuditReason);
policy.TimeStampUpdated = now;
```

Reuse the bounded string helpers and explicit range validation style for template provider, display name, storage, ports, runtime hours, and audit reason.

**Audit transaction pattern** (`CloudInventoryService`, lines 60-136 and 213-251):

```csharp
await using var transaction = await apiDbContext.Database.BeginTransactionAsync(cancellationToken);
...
await hostingAuditService.AppendAsync(
    new HostingAuditRecord
    {
        Kind = EHostingAuditEventKind.CloudDryRunPlanCreated,
        TimeStampCreated = now,
        ActorFk = actor.PrimaryKey,
        Summary = "Cloud dry-run plan created.",
        Details = "...",
    },
    cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

Apply this for template create, update, and disable so the version row and audit record commit together.

**Supersession pattern** (`Arma3CloudDryRunPlannerService`, lines 634-657):

```csharp
previousPlan.Status = ECloudDryRunPlanStatus.Superseded;
previousPlan.SupersededByPlanFk = newPlan.PrimaryKey;
previousPlan.TimeStampSuperseded = now;
```

Template updates should mark the previous current version non-current, set `SupersededByTemplateFk`, and preserve the old row.

### `CloudServerTemplate.cs` and `ApiDbContext.cs` (model/config, CRUD/versioning)

**Analogs:** `CloudSpendingPolicy`, `CloudDryRunPlan`, `ApiDbContext`

Use `[Key]`, `[MaxLength]`, `[ForeignKey]`, and explicit `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Restrict)` relationship configuration. Follow existing hosting model naming: `PrimaryKey`, `CreatedByFk`, `UpdatedByFk`, `TimeStampCreated`, `TimeStampUpdated`.

For indexes, follow existing `HasIndex(...).HasDatabaseName(...)` style in `ApiDbContext`. Add:

- unique `(TemplateId, Version)`
- filtered unique current version index for `TemplateId` where `"IsCurrent" = TRUE`
- lookup index over `GameTypeIdentifier`, `ProviderIdentifier`, `IsEnabled`, `IsCurrent`

### DTOs, Payloads, And Transform

**Analogs:** `CloudSpendingPolicyDto`, `CloudSpendingPolicyConfigurationPayload`, `Transform.ToDto`

DTO records expose safe primitive/id fields only. Payload records carry the bounded command fields but no raw JSON, provider token, authorization header, cookie, worker credential, or Steam credential properties.

`Transform.cs` adds a `ToDto(this CloudServerTemplate self)` method next to the existing cloud DTO methods. Existing line anchors:

- `HostingAuditRecord.ToDto`: line 81
- `CloudDryRunPlan.ToDto`: line 170
- `CloudSpendingPolicy.ToDto`: line 260

### `EHostingAuditEventKind.cs` (contract enum, event-driven)

**Analog:** existing cloud audit event sequence.

Append template event values after `CloudLivePreflightReadinessRefreshed = 36`:

- `CloudServerTemplateCreated`
- `CloudServerTemplateUpdated`
- `CloudServerTemplateDisabled`
- `CloudServerTemplateSelected`
- `CloudServerTemplateProvisioningUsed`

Selection/provisioning-use emission is implemented by Phase 43, but Phase 41 owns the event vocabulary and redaction-safe projection foundation for TPL-04.

### Tests Under `tests/Api.Tests/CloudTemplates`

**Analog:** `CloudSpendingPolicyControllerTests`

Use the same patterns:

- forbidden term array at lines 26-37
- route/auth reflection test at lines 40-80
- direct controller instantiation through `CreateController` at lines 556-588
- SQLite context factory at lines 590-599
- `SqliteApiDbContext` DateTimeOffset converter at lines 832-840

**Analog:** `Arma3CloudPlannerTests`

Use service-level persistence assertions:

- valid persistence test at lines 27-119
- invalid validation test at lines 168-226
- secret-safety test at lines 229-258
- constructor dependency isolation test at lines 260-269

## Shared Patterns

### Authorization

**Source:** `CloudSpendingPoliciesController`
**Apply to:** `CloudServerTemplatesController`

Controllers carry class-level `[Authorize]`; every template endpoint carries `[Authorize(Claims.Administrative.Server)]`.

### Audit

**Source:** `CloudInventoryService`
**Apply to:** `CloudServerTemplateService`

Append through `IHostingAuditService` only. Do not call `apiDbContext.HostingAuditRecords.Add` directly in the template service.

### Redaction

**Source:** `HostingAuditService` and existing cloud redaction tests
**Apply to:** payload validation, audit reason handling, DTO tests

Persist bounded text only and assert serialized DTOs/audit details do not contain: `Token`, `Password`, `Secret`, `Authorization`, `Bearer`, `Cookie`, `ApiKey`, `CredentialHash`, `Steam`.

### No Provider Mutation

**Source:** `Arma3CloudPlannerTests.Planner_DoesNotDependOnProviderClientOrHttpClient`
**Apply to:** template service/controller tests

Add a constructor dependency test proving `CloudServerTemplateService` does not depend on `IHetznerCloudClient`, `HttpClient`, `IHttpClientFactory`, worker dispatch, Steam repositories, or provider provisioning services.

## No Analog Found

None. Every planned file has a close API, service, model, or test analog in the existing cloud hosting code.

## Metadata

**Analog search scope:** `sources/Api`, `sources/Contract`, `tests/Api.Tests`
**Files scanned:** targeted cloud hosting controller/service/test files plus route constants and transform helpers
**Pattern extraction date:** 2026-06-21
