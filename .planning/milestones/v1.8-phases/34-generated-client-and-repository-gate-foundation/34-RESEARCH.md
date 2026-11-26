# Phase 34: Generated Client And Repository Gate Foundation - Research

**Researched:** 2026-06-19  
**Domain:** Blazor WebAssembly repository facades, generated Kiota clients, cloud workflow redaction  
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
## Phase Boundary

Cloud workflow actions return consistent, typed, redacted WebApp results through generated clients and repository facades.

This phase depends on Phase 33 and addresses COST-03. It must use generated Kiota clients behind WebApp repository facades, must not add raw browser HTTP bridges, and must not hand-edit generated client files.

User-visible cloud workflow results must keep raw provider tokens, Steam credentials, worker credentials, worker hashes, authorization headers, bearer tokens, and secret-bearing payloads out of results and UI surfaces.

## Implementation Decisions

### Roadmap-Locked Constraints

- User-facing cloud planning, policy, provisioning, lifecycle, teardown, cost, and audit actions surface consistent success, policy, validation, and failure states through repository-backed WebApp code.
- Policy gate responses preserve decision identity and continuation details needed for follow-up actions without exposing raw secret-bearing payloads.
- User-visible cloud workflow views show only allowlisted fields and never display provider tokens, Steam credentials, worker credentials, worker hashes, authorization headers, bearer tokens, or raw secret-bearing payloads.
- Generated WebApp clients may be consumed, but generated files must not be hand-edited.
- Browser-facing code must not add raw HTTP bridges for the cloud workflow.
- Live Hetzner, Steam, Arma, authenticated browser UAT, provider mutation, provisioning, and teardown evidence remain deferred by operator instruction.

### the agent's Discretion

Implementation details, repository DTO shape, adapter naming, redaction helper placement, focused tests, and the exact number of plan waves are at the agent's discretion, subject to existing codebase conventions and the roadmap boundaries above.

### Deferred Ideas (OUT OF SCOPE)

- Manual/browser/authenticated UAT is deferred.
- Live provider, Steam, Arma runtime, provisioning, teardown, and Phase 30 evidence remain deferred.
- Payment processing, customer invoicing, checkout, taxes, multi-provider orchestration, and multi-session workers remain out of scope.
</user_constraints>

## Phase Scope And Non-Goals

Phase 34 should establish the WebApp boundary for cloud workflow action results: generated Kiota clients are used only behind hand-written repository facades, and Razor pages receive typed, allowlisted, redacted result projections rather than raw generated DTOs, raw JSON, `AdditionalData`, headers, or exception bodies. [VERIFIED: 34-CONTEXT.md] [VERIFIED: 34-PATTERNS.md]

The phase addresses `COST-03`: user-visible cost, policy, lifecycle, and audit views must not expose provider tokens, Steam credentials, worker credentials, worker hashes, authorization headers, bearer tokens, or raw secret-bearing payloads. [VERIFIED: .planning/REQUIREMENTS.md]

This phase is a foundation for later owner-facing planning, policy, provisioning, lifecycle, teardown, cost, and audit UX. It should normalize result state and repository projections now, but it must not claim live Hetzner, Steam, Arma, authenticated browser, provider mutation, provisioning, teardown, or Phase 30 evidence. [VERIFIED: 34-CONTEXT.md]

Non-goals:

- Do not hand-edit `sources/WebApp/UtfApi/**`; generated files are a boundary. [VERIFIED: AGENTS.md]
- Do not add raw browser HTTP bridges for cloud workflow actions. [VERIFIED: 34-CONTEXT.md]
- Do not implement payment processing, checkout, customer invoicing, taxes, multi-provider orchestration, or multi-session workers. [VERIFIED: .planning/REQUIREMENTS.md]
- Do not perform live provider mutation, provisioning, teardown, or live UAT. [VERIFIED: 34-CONTEXT.md]

## Existing API, Generated-Client, And Repository Surfaces

| Area | API Surface | Generated Kiota Surface | WebApp Repository / UI Surface | Planning Implication |
|------|-------------|-------------------------|--------------------------------|----------------------|
| Guided entry | `GameServersController.GetGuidedCloudEntryOptionsAsync`, `CreateArma3GuidedCloudEntryServerAsync`, `AssignGuidedCloudEntryBillingOwnerAsync`, `ValidateGuidedCloudEntryReadinessAsync`. [VERIFIED: codebase grep] | `GameServers/CloudEntry/**`, `GameServers/{id}/CloudEntry/**`, `GuidedCloudEntryOptionsDto`, `GuidedCloudEntryReadinessDto`, `GuidedCloudEntryServerOptionDto`. [VERIFIED: 34-PATTERNS.md] | `IGameServerRepository.GetGuidedCloudEntryOptionsAsync`, `CreateArma3GuidedCloudPlanningServerAsync`, `AssignGuidedCloudEntryBillingOwnerAsync`, `ValidateGuidedCloudEntryReadinessAsync`; `/servers` guided cloud panel. [VERIFIED: codebase grep] | Existing Phase 33 methods already follow generated-client behind repository facade; use them as the model for Phase 34 result methods. |
| Dry-run planning | `CloudPlanningController.CreateArma3DryRunPlanAsync` and admin inventory plan endpoints. [VERIFIED: 33-RESEARCH.md] | `GameServers/{id}/CloudDryRunPlans/Arma3`, `CloudInventory/DryRunPlans/**`, `Arma3CloudDryRunPlanDto`, `CloudDryRunPlanDto`, `CloudDryRunPlanDetailsDto`. [VERIFIED: 34-PATTERNS.md] | Admin mappings exist in `CloudAdminRepositoryImpl`; owner flow currently stops at readiness. [VERIFIED: 34-PATTERNS.md] | Add repository result projection before later UI binds dry-run action outcomes; do not expose `ValidationDetailsJson` or `PlannedOperationsJson` directly. |
| Policy gate | `CloudSpendingPoliciesController` approval routes; policy-gated API actions return `CloudSpendingPolicyGateResultDto` as `409 Conflict`. [VERIFIED: 34-PATTERNS.md] | `CloudSpendingPolicyGateResultDto`, `CloudSpendingPolicyDecisionDto`, `CloudSpendingPolicyApprovalRequestDto`, `CloudSpendingPolicyVisibilityDto`. [VERIFIED: 34-PATTERNS.md] | `GameServerRepositoryImpl.GetCloudPolicyVisibilityAsync` maps via `CloudAdminRepositoryImpl.MapPolicyVisibility`. [VERIFIED: codebase grep] | Repository results need a policy state taxonomy that preserves decision id, approval request id, action kind, scope ids, expiration, and sanitized summaries while excluding `InputSnapshotJson` and `RiskDetailsJson`. |
| Provisioning | `CloudInventoryController.ProvisionDryRunPlanAsync` returns `CloudProvisioningResultDto` or `CloudSpendingPolicyGateResultDto` conflict. [VERIFIED: 34-PATTERNS.md] | `CloudInventory/DryRunPlans/{id}/Provision`, `CloudProvisioningResultDto`. [VERIFIED: 34-PATTERNS.md] | Admin repository has projection analogs for `CloudProvisioningResult`. [VERIFIED: 34-PATTERNS.md] | Add a typed `Success` or `PolicyBlocked` result wrapper before Phase 37 consumes provisioning from `/servers`; do not run live provisioning in Phase 34. |
| Lifecycle/runtime | `GameServersController` exposes lifecycle job, cloud runtime, and cleanup runtime routes. [VERIFIED: codebase grep] | `GameServers/{id}/CloudRuntime`, `GameServers/{id}/LifecycleJobs/**`, `CloudRuntimeStatusDto`, `LifecycleJobDto`, `LifecycleWorkerDto`. [VERIFIED: 34-PATTERNS.md] | `GetCloudRuntimeStatusAsync` uses generated Kiota, but lifecycle job lists and lifecycle action posts still use raw `HttpClient` helper paths in `GameServerRepositoryImpl`. [VERIFIED: codebase grep] | Plan a repository cleanup slice to use generated builders where present and wrap lifecycle action results into typed redacted action results. |
| Teardown | `CloudTeardownService` and inventory/session teardown surfaces exist from earlier milestones. [VERIFIED: .planning/PROJECT.md] | `CloudTeardownResultDto`, `CloudInventory/Sessions/**`. [VERIFIED: 34-PATTERNS.md] | Admin repository has `CloudTeardownResult` projection analog. [VERIFIED: 34-PATTERNS.md] | Define redacted teardown result projection now; do not add or exercise destructive provider teardown in Phase 34. |
| Cost | `GameServersController.GetCloudCostVisibilityAsync`, `RefreshCloudCostSnapshotAsync`; cost visibility services. [VERIFIED: codebase grep] | `GameServers/{id}/CloudCostVisibility`, `GameServers/{id}/CloudCostSnapshot`, `CloudCostVisibilityDto`, cost summary DTOs. [VERIFIED: 34-PATTERNS.md] | `GetCloudCostVisibilityAsync` and `RefreshCloudCostSnapshotAsync` map via `CloudAdminRepositoryImpl.MapCostVisibility`; `/servers` renders current/accumulated cost and cleanup risk. [VERIFIED: codebase grep] | Keep cost projections allowlisted: amounts, currency, source labels, freshness, reconciliation counts, cleanup risk, timestamps. |
| Audit | `GameServersController.GetHostingAuditRecordsAsync` returns hosting audit records. [VERIFIED: codebase grep] | `GameServers/{id}/AuditRecords`, `HostingAuditRecordDto`. [VERIFIED: 34-PATTERNS.md] | `GetHostingAuditRecordsAsync` currently uses raw `HttpClient.GetFromJsonAsync`. [VERIFIED: codebase grep] | Convert or wrap audit retrieval through generated route builders and project only audit id, kind, actor id when allowed, timestamp, related ids, and sanitized summary. |
| Billing owner | `BillingAccountsController`, `GameServersController` billing owner routes, guided owner assignment. [VERIFIED: codebase grep] | `BillingAccounts/**`, `GameServers/{id}/BillingOwner/**`, `BillingAccountDto`, `GameServerBillingAssignmentDto`. [VERIFIED: 34-PATTERNS.md] | Some billing account and billing owner history methods still use raw `HttpClient`; guided billing owner uses generated Kiota. [VERIFIED: codebase grep] | Phase 34 can leave non-cloud admin paths alone, but cloud workflow result paths should not add more raw HTTP usage. |

## Recommended Implementation Slices

1. **Define repository result taxonomy and allowlisted projection records.**  
   Add hand-written models under `sources/WebApp/Services/GameServerRepository/` or a nearby `CloudWorkflow` subfolder: `CloudWorkflowActionResult`, `CloudWorkflowResultState`, `CloudWorkflowPolicyGateResult`, `CloudWorkflowValidationResult`, `CloudWorkflowFailureResult`, and compact summaries for cost, lifecycle job, audit, provisioning, and teardown. [VERIFIED: 34-PATTERNS.md] The state taxonomy should cover `Loading`, `Success`, `PolicyAllow`, `PolicyWarning`, `ApprovalRequired`, `Denied`, `ValidationBlocked`, `Failure`, and `Empty`, matching `34-UI-SPEC.md`. [VERIFIED: 34-UI-SPEC.md]

2. **Centralize generated DTO to WebApp result mapping.**  
   Add mapper methods in `GameServerRepositoryImpl` or a small internal mapper class in the repository folder. [ASSUMED] The mapper should accept generated DTOs and `ApiException` outcomes, return typed repository results, and never pass raw generated DTO instances to Razor. [VERIFIED: 34-PATTERNS.md] Use existing `CloudAdminRepositoryImpl.MapCostVisibility` and `MapPolicyVisibility` for established admin projections, but wrap any policy decision or action outcome in smaller user-facing result models. [VERIFIED: 34-PATTERNS.md]

3. **Normalize policy-gated action handling.**  
   For any cloud action that can return `409 Conflict` with `CloudSpendingPolicyGateResultDto`, catch `ApiException`, parse/map the generated error model only if available through Kiota, and return a `PolicyWarning`, `ApprovalRequired`, or `Denied` result. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors] Preserve decision id, approval request id, action kind, game server id, session id, dry-run plan id, billing account id, billing assignment id, expiration, reason code, projected/current cost, and sanitized failure summary/details. [VERIFIED: 34-UI-SPEC.md]

4. **Replace cloud-workflow raw HTTP usage where generated builders exist.**  
   `GameServerRepositoryImpl` currently uses generated Kiota for guided entry, cloud status, runtime, cost, and policy visibility, but raw `HttpClient` for lifecycle jobs, active owner, owner history, audit records, billing accounts, general server creation, lifecycle action posts, and cloud runtime cleanup. [VERIFIED: codebase grep] Phase 34 should avoid expanding raw HTTP usage and should convert cloud-result-relevant methods when generated request builders already exist, especially `GameServers/{id}/LifecycleJobs`, `GameServers/{id}/AuditRecords`, and `GameServers/{id}/BillingOwner/History`. [VERIFIED: 34-PATTERNS.md]

5. **Render only repository results in `/servers`.**  
   `/servers` should continue injecting `IGameServerRepository`, `ILifecycleWorkerRepository`, and `IModPackRepository`; it should not import generated request builders or call `HttpClient` directly. [VERIFIED: codebase grep] New result panels should use `FluentMessageBar`, `FluentProgress`, `section.detail-section`, `dl.meta-grid`, `.status-badge`, and existing action rows. [VERIFIED: 34-UI-SPEC.md]

6. **Add boundary tests before expanding UI behavior.**  
   Use API projection tests for server-side DTO redaction where appropriate and add source/boundary tests for WebApp repository projections if no WebApp test project is added. [ASSUMED] The existing test analogs serialize DTO/projection objects and assert forbidden terms and values are absent. [VERIFIED: 34-PATTERNS.md]

## Sensitive And Raw Fields That Must Not Appear

The WebApp must not display, log as UI text, or expose through repository result models any of the following:

| Category | Forbidden Examples | Existing Source / Risk |
|----------|--------------------|------------------------|
| Provider secrets | Provider tokens, token values, token references, authorization headers, bearer tokens, raw provider credentials. | Provider config and token-safe DTO concerns are established milestone constraints. [VERIFIED: AGENTS.md] |
| Steam secrets | Steam username, Steam password, Steam API key, Steam guard/session values, DepotDownloader credentials. | Steam configuration keys exist and must remain server-side. [VERIFIED: AGENTS.md] |
| Worker secrets | Worker credentials, bootstrap credential secrets, worker registration secrets, credential hashes, worker hashes, private keys, SSH private material. | `LifecycleWorker.CredentialHash` redaction is already tested; generated `CloudWorkerBootstrapCredentialDto` is a sensitive model requiring wrapping. [VERIFIED: 34-PATTERNS.md] |
| Raw policy evidence | `CloudSpendingPolicyDecisionDto.InputSnapshotJson`, `RiskDetailsJson`, raw policy input snapshots, raw risk details. | Pattern map explicitly warns existing policy projections include these fields and must not bind/render them. [VERIFIED: 34-PATTERNS.md] |
| Raw dry-run/resource JSON | `CloudDryRunPlanDto.ValidationDetailsJson`, `PlannedOperationsJson`, `CloudResourceInventoryRecordDto.DetailsJson`, raw validation details, raw planned operations, raw resource details. | API transform currently projects these fields to DTOs, so WebApp result models must avoid direct display. [VERIFIED: codebase grep] |
| Raw billing/reconciliation JSON | `CloudProviderReconciliationFindingDto.DetailsJson`, `CloudBillingMatchRecordDto.DetailsJson`, billing import raw rows, source hashes where not needed for user identity. | API transform exposes details/source hash fields for admin workflows; owner-visible results need smaller projections. [VERIFIED: codebase grep] |
| Kiota extension data | `AdditionalData`, unknown OpenAPI fields, raw generated DTO dumps. | Kiota generated models may carry additional data outside typed schema; repository mapping must explicitly choose allowed fields. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/overview] |
| Transport internals | Raw request/response bodies, exception bodies, headers, cookie values, bearer strings, serialized `ApiException`, stack traces. | Kiota errors surface through exceptions; repository results should map them to sanitized failure categories. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors] |

Allowlisted user-visible categories are: server/session/plan/resource/job/policy/audit/billing ids, display names, provider/resource labels, runtime/resource/job/policy statuses, cost amounts/currency/source labels/freshness, cleanup-risk counts, timestamps, retryability flags, required next steps, and sanitized failure summaries/details. [VERIFIED: 34-UI-SPEC.md]

## Verification Strategy And Commands

Manual/browser/authenticated UAT is deferred. Live Hetzner, Steam, Arma runtime, provider mutation, provisioning, teardown, and Phase 30 evidence are deferred. Phase 34 verification should rely on source, build, unit/API tests, and redaction/boundary scans. [VERIFIED: 34-CONTEXT.md]

Recommended automated checks:

```powershell
dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal
```

```powershell
dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CloudSpendingPolicyVisibility|FullyQualifiedName~CloudInventoryVisibility|FullyQualifiedName~CloudRuntimeCostSnapshot|FullyQualifiedName~LifecycleWorkerRegistry|FullyQualifiedName~GuidedCloudEntry" --no-restore --verbosity minimal
```

```powershell
rg -n "HttpClient|GetFromJsonAsync|PostAsJsonAsync|RequestBuilder|UtfApi" sources\WebApp\Pages\Servers sources\WebApp\Services\GameServerRepository
```

```powershell
rg -n "InputSnapshotJson|RiskDetailsJson|ValidationDetailsJson|PlannedOperationsJson|DetailsJson|AdditionalData|CredentialHash|TokenReference|Authorization|Bearer|password|secret|token|credential" sources\WebApp\Pages\Servers sources\WebApp\Services\GameServerRepository sources\WebApp\Services\CloudAdminRepository
```

```powershell
rg -n "live UAT passed|Hetzner verified live|Steam verified live|Arma runtime verified live|checkout|payment|tax|customer invoice" sources\WebApp .planning\phases\34-generated-client-and-repository-gate-foundation
```

Suggested focused tests:

- Serialize every new repository result projection and assert it does not contain forbidden keys or seeded forbidden values such as `fake-token`, `authorization bearer`, `steam password`, `worker credential`, `CredentialHash`, `InputSnapshotJson`, `RiskDetailsJson`, `AdditionalData`, or raw `DetailsJson`. [VERIFIED: 34-PATTERNS.md]
- Test policy gate mapping for allow, warning, approval-required, denied, validation blocked, and failure states with preserved decision/approval/scope identity. [VERIFIED: 34-UI-SPEC.md]
- Test that `/servers` does not reference generated request builders or direct `HttpClient` for cloud workflow actions. [VERIFIED: 34-CONTEXT.md]
- Test that new cloud workflow repository methods do not return generated DTO types when the DTO includes raw JSON, `AdditionalData`, or secret-bearing fields. [ASSUMED]

Environment note: a package-version probe with `dotnet list X39.UnitedTacticalForces.slnx package --include-transitive` hit NuGet/AppHost SDK resolution restrictions in the sandbox, so package versions should be read from project files or rechecked in a restored developer environment if the planner needs exact dependency metadata. [VERIFIED: shell probe]

## Risks And Planner Notes

- **Mixed repository transport patterns:** `GameServerRepositoryImpl` has both generated Kiota calls and raw `HttpClient` helpers. [VERIFIED: codebase grep] Do not treat "repository-backed" as sufficient by itself for this phase; cloud workflow action results should use generated Kiota builders where generated surfaces exist. [VERIFIED: 34-CONTEXT.md]

- **Generated DTOs can contain unsafe-but-useful fields:** API DTOs expose raw JSON fields for admin/debug workflows, and Kiota models may include `AdditionalData`. [VERIFIED: codebase grep] [CITED: https://learn.microsoft.com/en-us/openapi/kiota/overview] Planner tasks should create allowlisted projection records instead of binding raw DTOs into Razor.

- **Policy continuation identity is not optional:** Warning acknowledgements and approvals need decision identity, action kind, scope ids, owner ids, expiration, and approval request id to prevent mismatched continuation. [VERIFIED: 34-UI-SPEC.md] Do not over-redact these identity fields; they are safe and necessary when presented as ids/status metadata. [ASSUMED]

- **UI should not become the redaction authority:** Razor should display already-redacted repository result models. [VERIFIED: 34-UI-SPEC.md] UI-side filtering can be a final guard, but repository projections are the boundary. [ASSUMED]

- **Live evidence remains deferred:** Any plan slice that says "verify provisioning", "verify teardown", "run live Hetzner", "run Steam", "run Arma", or "close Phase 30 UAT" is out of scope for Phase 34. [VERIFIED: 34-CONTEXT.md]

- **Avoid broad redesign:** Keep `/servers` dense and operational. Use existing `detail-section`, `meta-grid`, status badges, and Fluent UI components. [VERIFIED: 34-UI-SPEC.md]

- **Testing gap:** No dedicated WebApp repository test project was identified in the pattern map. [VERIFIED: 34-PATTERNS.md] The planner can either add focused tests under an existing suitable project, introduce a narrow WebApp test project, or use source-boundary scans plus API projection tests if a new test project is too much phase weight. [ASSUMED]

## Sources

### Primary

- `AGENTS.md` - project constraints, stack, generated-code boundary, repository facade conventions. [VERIFIED: file read]
- `.planning/STATE.md` - current Phase 34 focus, generated-client/repository decision, deferred Phase 30/live UAT context. [VERIFIED: file read]
- `.planning/ROADMAP.md` - Phase 34 goal, success criteria, boundaries, dependency on Phase 33. [VERIFIED: file read]
- `.planning/REQUIREMENTS.md` - `COST-03` and v1.8 out-of-scope constraints. [VERIFIED: file read]
- `.planning/PROJECT.md` - current milestone context and existing cloud backend/admin capabilities. [VERIFIED: file read]
- `.planning/phases/34-generated-client-and-repository-gate-foundation/34-CONTEXT.md` - phase scope and deferred evidence constraints. [VERIFIED: file read]
- `.planning/phases/34-generated-client-and-repository-gate-foundation/34-UI-SPEC.md` - result state taxonomy, allowed/forbidden fields, UI rendering contract. [VERIFIED: file read]
- `.planning/phases/34-generated-client-and-repository-gate-foundation/34-PATTERNS.md` - concrete code analogs and generated/API/repository surface inventory. [VERIFIED: file read]
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` and `GameServerRepositoryImpl.cs` - current repository methods and generated/raw transport mix. [VERIFIED: file read]
- `sources/WebApp/Pages/Servers/Index.razor` - owner-facing `/servers` integration surface. [VERIFIED: file read]
- `sources/Api/Controllers/GameServersController.cs` - server-scoped cloud entry/runtime/cost/policy/audit route patterns. [VERIFIED: file read]

### Documentation

- Microsoft Kiota overview - generated client and model behavior, including extensible model data concerns. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/overview]
- Microsoft Kiota error handling - generated client exception handling for non-success responses. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors]
- OWASP ASVS - basis for web application security verification controls. [CITED: https://owasp.org/www-project-application-security-verification-standard/]

## Metadata

**Confidence breakdown:**

- Scope and non-goals: HIGH - directly locked by `34-CONTEXT.md`, roadmap, and requirements. [VERIFIED: file read]
- Existing surfaces: HIGH - grounded in `34-PATTERNS.md` and target file reads. [VERIFIED: file read]
- Recommended implementation slices: HIGH for repository/generated-client/redaction boundary; MEDIUM for exact model names because naming is at agent discretion. [VERIFIED: 34-CONTEXT.md]
- Verification strategy: HIGH for source/build/test commands and deferred UAT boundary; MEDIUM for any future WebApp test-project choice because no existing WebApp test project was identified. [VERIFIED: 34-PATTERNS.md]

**Research date:** 2026-06-19  
**Valid until:** 2026-07-19 or until generated Kiota artifacts/repository APIs change materially. [ASSUMED]
