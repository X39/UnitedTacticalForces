---
phase: 18
slug: provider-reconciliation-runs-and-findings
status: draft
created: 2026-06-18
---

# Phase 18 - Pattern Map

**Mapped:** 2026-06-18

## File Classification

| New/Modified File Or Concern | Role | Closest Analog | Match Quality |
|------------------------------|------|----------------|---------------|
| `IHetznerCloudClient` list methods | provider boundary | Existing metadata methods in `IHetznerCloudClient` | exact |
| `HetznerCloudClient` list methods | provider implementation | `GetServerTypesAsync`, `GetLocationsAsync`, `GetDatacentersAsync`, `GetImagesAsync` | exact |
| Live resource models | provider DTO | `HetznerCloudProvisioningModels.cs` and `HetznerCloudMetadataModels.cs` | role-match |
| Reconciliation run entity | model | `CloudCostSnapshot`, `CloudDryRunPlan`, `HostingAuditRecord` | role-match |
| Reconciliation finding entity | model | `CloudResourceInventoryRecord`, `CloudCostSnapshotContribution` | exact |
| DbContext wiring | EF config | Existing hosting entity configuration in `ApiDbContext` | exact |
| Reconciliation service | service | `CloudRuntimeCostSnapshotService`, `CloudInventoryService`, `CloudTeardownService` | role-match |
| Admin API trigger/read endpoints | controller | `CloudInventoryController` | exact |
| DTO transforms | API transform | `Transform.ToDto` cloud inventory methods | exact |
| Fake provider tests | test | `HetznerCloudClientTests` | exact |
| SQLite service tests | test | `CloudRuntimeCostSnapshotServiceTests`, `CloudInventoryServiceTests` | exact |

## Implementation Patterns

### Provider Client

- Keep provider types internal under `sources/Api/Services/CloudProviders/Hetzner`.
- Use `CancellationToken` on every provider call.
- Reuse `GetPagedAsync<TResponse, TModel>` for every live list endpoint.
- Tests should inspect fake handler requests to prove all list calls are GET requests and never call create/delete/detach endpoints.

### Entities

- Use file-scoped namespace `X39.UnitedTacticalForces.Api.Data.Hosting`.
- Use `PrimaryKey` as the key property.
- Use `[MaxLength]` bounds for provider strings and serialized details.
- Add indexes on provider id, status/kind, run foreign key, session, game server, billing account, and billing assignment.
- Prefer `DeleteBehavior.Restrict` for history records that reference sessions, users, game servers, billing records, and inventory records.

### Services

- Register scoped services through `X39.Util.DependencyInjection` attributes.
- Load EF records with explicit includes only when attribution data is needed by the service.
- Keep provider calls in the reconciliation service; do not add provider dependencies to cost snapshot or dry-run services.
- Store sanitized structured details as bounded JSON strings rather than raw provider payloads.
- Append audit records through existing hosting audit abstractions when runs complete or fail.

### Tests

- Provider boundary tests use fake HTTP responses and assert route/method/token behavior.
- Service tests should use local SQLite contexts with deterministic seed records.
- Reflection tests are acceptable for controller policy coverage when full auth integration is not needed.
- Secret-safety tests should scan persisted assumptions/details/audit payloads for token, bearer, password, secret, api key, user data, and bootstrap terms.

