---
phase: 19
slug: cost-and-reconciliation-visibility
status: draft
created: 2026-06-18
---

# Phase 19 - Pattern Map

**Mapped:** 2026-06-18

## File Classification

| New/Modified File Or Concern | Role | Closest Analog | Match Quality |
|------------------------------|------|----------------|---------------|
| Runtime cost summary DTOs | API DTO | `CloudCostEstimateDto`, `CloudProviderReconciliationRunDto` | role-match |
| Cost visibility service | API service | `CloudRuntimeCostSnapshotService`, `CloudInventoryService` | role-match |
| Admin cost/reconciliation endpoints | API controller | `CloudInventoryController` cloud inventory endpoints | exact |
| Server-scoped cost endpoints | API controller | `GameServersController.GetCloudSessionStatusesAsync`, `GetCloudRuntimeStatusAsync` | exact |
| DTO transforms | API transform | Existing `Transform.ToDto` cloud methods | exact |
| Generated client updates | WebApp generated client | `sources/WebApp/UtfApi/CloudInventory`, `GameServers/Item` | exact |
| Admin repository methods | WebApp facade | `CloudAdminRepositoryImpl` mapping methods | exact |
| Owner repository methods | WebApp facade | `GameServerRepositoryImpl.GetCloudSessionStatusesAsync` | exact |
| Admin cloud operations UI | Blazor page | `sources/WebApp/Pages/Servers/Cloud.razor` | exact |
| Owner server detail UI | Blazor page | `sources/WebApp/Pages/Servers/Index.razor` | exact |
| Styling | scoped CSS | `Cloud.razor.css`, `Index.razor.css` | exact |
| API tests | controller/service tests | `CloudRuntimeCostSnapshotServiceTests`, `CloudProviderReconciliationApiTests` | exact |

## Implementation Patterns

### API

- Keep new DTO records in `sources/Api/DTO`, one public type per file when practical.
- Keep composition logic in a scoped service rather than duplicating EF queries inside controllers.
- Use `AsNoTracking` for read-only visibility queries.
- Use existing DTO/transform conventions and `ProducesResponseType` attributes.
- Return `NotFound()` for missing session/server resources and `Unauthorized()` when an authenticated
  principal cannot be resolved to a database user for refresh operations.
- Do not expose unknown provider resources through server-scoped endpoints unless they are attributable
  to the requested `GameServerFk`.

### WebApp Client

- Treat `sources/WebApp/UtfApi/**` as generated code. Hand-written behavior belongs in repository
  facades and WebApp-local model records.
- Repository facade methods should convert generated nullable Kiota models into non-null local records
  with default values, matching current `CloudAdminRepositoryImpl` style.
- Do not introduce new `HttpClient.GetFromJsonAsync`, `PostAsJsonAsync`, or string-path bridges for
  Phase 19 cost/reconciliation surfaces.
- If generated names duplicate existing DTO names with suffixes, map both as needed in repository code
  but keep public repository models stable.

### UI

- Use existing Fluent UI controls and the current operational dashboard layout.
- Add compact summary cards and table columns; do not create a marketing or landing page.
- Use badges for source/freshness/reconciliation/risk state.
- Keep copy operational and precise:
  - "Estimated runtime cost"
  - "Reconciled runtime cost" only when source is `Reconciled`
  - "Adjusted runtime cost" only when source is `Adjusted`
  - "Provider teardown stops billable resources"
- Avoid claiming actual invoice totals.

### Tests

- Service tests should use SQLite-backed EF contexts for composed cost/reconciliation projections.
- Controller tests should cover admin and server-scoped behavior, not-found paths, authenticated actor
  handling, and secret-safe serialization.
- WebApp repository tests can be static/reflection checks if no WebApp test project exists.
- UI verification should include static checks for required labels and no raw HTTP bridge additions;
  browser UAT remains operator-gated without authenticated representative data.

