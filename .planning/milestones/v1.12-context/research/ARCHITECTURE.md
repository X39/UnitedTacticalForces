# Architecture Research: v1.8 Cloud Server Creation Experience

**Domain:** Server-oriented integration of existing cloud planning, provisioning, runtime, policy, teardown, and cost services  
**Researched:** 2026-06-19  
**Confidence:** HIGH

## Summary

v1.8 should not add a second orchestration stack. The backend already has the durable control-plane pieces: server-scoped dry-run planning, persisted cloud sessions/resources/estimates, provider provisioning and teardown, runtime job execution, cost visibility, policy gates, approval records, and reconciliation. The architecture work is to compose those pieces into one server workflow while keeping `/servers/cloud` as the administrator fleet view.

Use `/servers` as the owner-facing creation and operation surface. A user should create or select an Arma 3 `GameServer`, select the active billing owner, generate a dry-run plan, review cost/policy/cleanup implications, provision after passing policy gates, operate runtime jobs, tear down provider resources, and inspect owner cost history from the selected server details. `/servers/cloud` should remain administrator-only for provider status, metadata sync, global inventory, reconciliation, policy configuration/approval, imports, and fleet cleanup risk.

The largest missing seam is not backend capability; it is repository and route composition. `CloudPlanningController` exposes server-scoped Arma 3 dry-run creation, and generated Kiota builders exist, but `IGameServerRepository` does not wrap them. Admin provisioning/teardown exists in `ICloudAdminRepository`, but it does not accept policy-gate continuation IDs or map `409 CloudSpendingPolicyGateResultDto`. Runtime lifecycle endpoints also accept gate query params, but the WebApp repository currently posts without those params and treats policy conflicts as generic failures.

## Existing Architecture To Reuse

| Area | Reuse | Why |
|------|-------|-----|
| WebApp DI and API access | `Program.cs` registers attributed services and a named `API` client; repositories inherit `RepositoryBase` and use generated Kiota clients. | Keep all new UI data access behind repository facades; do not edit generated client code. |
| Owner server surface | `Pages/Servers/Index.razor` already lists servers, creates local server records, displays selected server cloud runtime, cost, policy, cleanup risk, jobs, owner, and audit context. | This should become the guided workflow host instead of adding a disconnected cloud wizard page. |
| Admin cloud surface | `Pages/Servers/Cloud.razor` already reads provider metadata/status, sessions, plans, resources, estimates, reconciliation, policy visibility, and admin provision/teardown actions. | Keep as fleet/admin operations, not the primary owner creation path. |
| Server-scoped API projections | `GameServersController` exposes cloud status, cloud runtime, cloud cost visibility, cloud policy visibility, cost snapshot refresh, and lifecycle controls guarded by server claims. | These are the owner-safe read/operation endpoints for selected server views. |
| Server-scoped dry run | `CloudPlanningController` provides `POST /game-servers/{gameServerId}/cloud-dry-run-plans/arma3` with `Claims.ResourceBased.Server.Configuration`. | This is the correct entry point for guided plan generation. |
| Admin inventory/provisioning | `CloudInventoryController` provides admin dry-run details, inventory lists, provisioning, teardown, cost visibility, and reconciliation. | Reuse for `/servers/cloud`; add server-scoped wrappers only where owner workflow needs non-admin access. |
| Policy gate service | `ICloudSpendingPolicyGateService` evaluates provisioning, teardown, cloud lifecycle, and future resize actions with acknowledgement/approval continuation IDs. | The UI should model policy gates as first-class action results, not generic HTTP errors. |
| Cost history model | `CloudCostVisibility`, `CloudRuntimeCostSummary`, owner intervals, billing reconciliation, adjusted cost, and resource contributions already exist in `CloudAdminModels`. | Use these projections for owner cost responsibility instead of inventing a billing dashboard model. |

## Missing Integration Points

| Gap | Add/Modify | Recommended Shape |
|-----|------------|-------------------|
| Guided dry-run creation repository method | Modify `IGameServerRepository` / `GameServerRepositoryImpl` | Add `CreateArma3CloudDryRunPlanAsync(gameServerId, payload)` using `Client.GameServers[gameServerId].CloudDryRunPlans.Arma3.PostAsync(...)`. Map or expose the generated `Arma3CloudDryRunPlanDto` through a hand-written model. |
| Server-scoped dry-run details | Add API endpoint or repository access with ownership check | Add `GET /game-servers/{gameServerId}/cloud-dry-run-plans/{planId}/details` that verifies the plan belongs to the server and returns `CloudDryRunPlanDetailsDto`. Do not force owners through admin-only `cloud-inventory/dry-run-plans/{planId}/details`. |
| Provider metadata for wizard choices | Add owner-safe API projection or restrict wizard to admins | Current Hetzner metadata endpoints are admin-only. For non-admin creators, add a sanitized read-only planning metadata endpoint with server creation/config authorization, excluding token/provider config diagnostics. |
| Server-owner provisioning | Add server-scoped operation if owners may mutate provider resources | Add `POST /game-servers/{gameServerId}/cloud-dry-run-plans/{planId}/provision` that validates plan ownership, applies policy gate, and delegates to `ICloudProvisioningService`. Keep admin global provisioning for `/servers/cloud`. |
| Server-owner provider teardown | Add server-scoped operation if owners may delete provider resources | Add `DELETE /game-servers/{gameServerId}/cloud-sessions/{sessionId}` with server/resource authorization, policy gate handling, plan/session ownership checks, and explicit cost-stop warning semantics. |
| Policy gate continuation in repositories | Modify both repositories | Add `CloudPolicyGateContinuation { AcknowledgedDecisionId, ApprovalRequestId }` and pass query params for provisioning, teardown, and lifecycle jobs. |
| Policy gate result handling | Modify repositories | Catch Kiota/API `409` responses and return a typed `CloudActionResult<T>` or throw a dedicated `CloudPolicyGateException` carrying `CloudSpendingPolicyGateResultDto`. The UI needs access to `Decision.PrimaryKey` and `ApprovalRequest.PrimaryKey`. |
| Admin approval repository methods | Modify `ICloudAdminRepository` | Add `GetApprovalRequestsAsync`, `GetApprovalRequestAsync`, `ApproveApprovalRequestAsync`, and `DenyApprovalRequestAsync` wrapping `CloudSpendingPoliciesController`. |
| Lifecycle controls with gate IDs | Modify `IGameServerRepository` lifecycle methods | Add optional continuation params to `Start/Stop/Restart/Install/Update/CollectLogs/CleanupCloudRuntime/Delete`. Existing endpoints accept query params, but current repository helpers do not. |
| Runtime cleanup vs provider teardown clarity | Modify UI and possibly policy action mapping | Runtime cleanup queues a lifecycle delete job but must be presented separately from provider teardown. If policy decisions currently label that cleanup as provider teardown, add a distinct action kind or UI copy that makes the cost boundary explicit. |
| Owner cost history view | Modify `/servers` selected detail | Use server-scoped `GetCloudCostVisibilityAsync`, `OwnerIntervals`, billing owner history, and audit records to show active and historical responsibility with estimated/reconciled/adjusted labels. |

## Proposed Data Flow

```text
/servers
  -> create or select GameServer
  -> select active BillingAccount/BillingAssignment
  -> load safe planning metadata and modpack/runtime inputs
  -> POST server-scoped Arma 3 dry-run
  -> review persisted plan details, resources, validation, assumptions, freshness, costs, policy visibility
  -> POST provision with optional policy continuation
      -> 200: show session/endpoint/bootstrap/worker readiness
      -> 409 warning: show gate, repeat with acknowledgedDecisionId
      -> 409 approval: show request id, wait for admin approval, repeat with approvalRequestId
      -> 409 denied: stop workflow with decision reason
  -> operate runtime through server lifecycle endpoints with same gate handling
  -> DELETE server-scoped cloud session for provider teardown/cost stop
  -> display owner cost history from server-scoped cost visibility plus billing/audit history
```

The dry-run review should be the main handoff point. It should show the `Arma3CloudDryRunPlanDto` fields that users must understand before provider mutation: selected/recommended server type, location/datacenter/image, ports, storage, firewall rules, validation failures, assumptions, metadata source timestamp, hourly/monthly estimates, currency/VAT note, estimate freshness, and cleanup/cost-stop warnings.

Provisioning should not be initiated from a global admin row when the user arrived through a server workflow. The server workflow should call server-scoped routes so authorization can prove the selected server, plan, session, billing assignment, and actor all match before delegation to provider services.

## New/Modified Components

| Component | New/Modified | Responsibility |
|-----------|--------------|----------------|
| `GameServersController` or new `CloudServerOperationsController` | Modified/New | Server-scoped plan details, provision, and provider teardown wrappers with resource authorization and policy gates. |
| `CloudPlanningController` | Modified | Keep Arma 3 dry-run creation; optionally add details route here if the team prefers all plan routes under `cloud-dry-run-plans`. |
| `HetznerProviderController` | Modified | Add safe read-only metadata projection for creator flows, or document that only admins can choose size/location. |
| `ICloudSpendingPolicyGateService` usage | Modified | Keep as the single gate for billable actions; ensure runtime cleanup and provider teardown are not conflated in action semantics. |
| `IGameServerRepository` / `GameServerRepositoryImpl` | Modified | Add dry-run, details, provision, teardown, and gate-aware lifecycle methods. Prefer generated Kiota builders over raw `HttpClient` helpers where builders exist. |
| `ICloudAdminRepository` / `CloudAdminRepositoryImpl` | Modified | Keep admin global methods; add gate continuation and approval-management wrappers. |
| `/servers` selected-server workflow | Modified | Add guided creation/review/provision/operate/teardown/cost-history panels tied to the selected server. |
| `/servers/cloud` admin page | Modified | Keep fleet tables; add policy approval actions and use gate-aware admin provision/teardown methods. |
| WebApp action result model | New | Centralize `Success`, `PolicyGate`, and `TransportFailure` handling so every billable action behaves consistently. |

## Authorization/Redaction Boundaries

Server-owner views should be server-scoped. They may read and operate only through `Claims.ResourceBased.Server.*` routes for servers they can access. They should see dry-run inputs, planned resources, session status, endpoint, worker health class, lifecycle progress, policy warnings/approval requirements, cleanup risk, and cost responsibility for their allowed server.

Administrator-only operations should remain global: provider token status, metadata sync, all sessions/resources/plans, reconciliation runs/findings, billing imports/matches/adjustments, policy configuration, policy approval/denial, global cost visibility, and orphan cleanup triage.

Provider mutation should be explicit. Provisioning creates billable resources; provider teardown is the cost-stop boundary; runtime stop and runtime cleanup are not cost-stop actions. The UI and API route names should preserve that distinction.

Do not expose provider tokens, Steam credentials, worker bearer/bootstrap secrets, raw secret config, or unredacted failure details. DTOs used in owner flows should remain sanitized projections. Bootstrap credential projections should keep only status/timestamps/display identifiers, never credential material.

Policy gate data is safe to show in scoped form, but it is still authorization-sensitive. Owners can see gate results for their attempted action and server-scoped policy visibility. Only administrators should approve or deny approval requests unless a future explicit approver role is added.

## Suggested Build Order

1. **Contract and authorization pass** - Decide whether non-admin server owners may provision/teardown provider resources. Add server-scoped API wrappers and safe metadata projection accordingly.
2. **Repository gate foundation** - Add shared action result/gate mapping, continuation IDs, and gate-aware lifecycle/provision/teardown methods in repositories.
3. **Dry-run wizard slice** - Build `/servers` create/select + billing owner + metadata/options + Arma 3 dry-run generation and review. No provider mutation yet.
4. **Policy gate UX slice** - Handle warning acknowledgement, approval-required, denied, and admin approval/denial flows. Verify repeat actions pass `acknowledgedDecisionId` or `approvalRequestId`.
5. **Provisioning integration slice** - Wire server-scoped provision from reviewed plan to session status refresh, worker readiness, endpoint display, and audit/status messages.
6. **Runtime operation slice** - Reuse existing lifecycle controls with policy continuation support and clear runtime cleanup vs provider teardown labels.
7. **Teardown and cost-stop slice** - Add explicit provider teardown confirmation, cleanup failure display, retry path, and final remaining-billable-resource count.
8. **Owner cost history slice** - Present active/historical owner intervals, estimated/reconciled/adjusted labels, billing assignment history, and audit records in selected-server detail.
9. **Verification slice** - Add focused API/repository/UI tests for route authorization, 409 gate mapping, dry-run review fields, redaction, teardown semantics, and owner cost history projections.

## Sources/Evidence

- `.planning/PROJECT.md` - v1.8 goal and active requirements define the guided WebApp creation/review/provision/operate/teardown/cost path.
- `docs/change-requests/CR-0001-cloud-game-server-orchestration.md` - desired end state requires frontend creation, estimated cost before provisioning, billing owner, lifecycle controls, delete, logs, costs, historical attribution, and admin audit/reconciliation.
- `.planning/codebase/ARCHITECTURE.md` - WebApp uses generated Kiota clients wrapped by repositories; generated code should not be edited.
- `sources/WebApp/Program.cs` - registers the named `API` client and attributed WebApp services.
- `sources/WebApp/Pages/Servers/Index.razor` - existing owner/server page already displays selected-server runtime, cost, policy, cleanup, job, billing, and audit data.
- `sources/WebApp/Pages/Servers/Cloud.razor` - current admin page loads provider metadata/status, sessions, plans, resources, estimates, reconciliation, policy visibility, and calls admin provision/teardown.
- `sources/WebApp/Services/RepositoryBase.cs` - repository facade creates the generated Kiota `ApiClient`.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` and `GameServerRepositoryImpl.cs` - server repository has status/runtime/cost/policy methods but lacks dry-run creation/details/provision/teardown and gate-aware continuations.
- `sources/WebApp/Services/CloudAdminRepository/ICloudAdminRepository.cs` and `CloudAdminRepositoryImpl.cs` - admin repository has global cloud inventory/provision/teardown methods but lacks policy gate continuation/result handling and approval-management methods.
- `sources/Api/Controllers/CloudPlanningController.cs` - server-scoped Arma 3 dry-run creation already exists.
- `sources/Api/Controllers/CloudInventoryController.cs` - admin-only global provision/teardown/details/cost/reconciliation endpoints exist and return `409 CloudSpendingPolicyGateResultDto` for gated mutations.
- `sources/Api/Controllers/GameServersController.cs` - server-scoped cloud status/runtime/cost/policy and lifecycle endpoints exist; lifecycle endpoints already accept `acknowledgedDecisionId` and `approvalRequestId`.
- `sources/Api/Controllers/CloudSpendingPoliciesController.cs` - admin-only policy visibility and approval/denial endpoints exist.
- `sources/Api/Services/CloudPolicies/ICloudSpendingPolicyGateService.cs` - policy gate contract supports provisioning, teardown, lifecycle, and resize with continuation IDs.
