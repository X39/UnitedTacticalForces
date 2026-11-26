# Phase 46 Research

## Objective

Identify what already supports controlled live provisioning evidence and what must be added without performing live provider mutation in the workspace.

## Findings

### R-46-01: API preflight gate remains authoritative

`sources/Api/Controllers/GameServersController.cs` calls `ValidateReadyForProvisioningAsync` before policy evaluation or provider provisioning. Phase 46 UI work must not weaken this server-side boundary.

### R-46-02: WebApp provisioning is already repository-backed

`sources/WebApp/Pages/Servers/Index.razor` invokes `ProvisionReviewedCloudResourcesAsync`, which delegates to `IGameServerRepository.ProvisionReviewedCloudDryRunPlanResultAsync`. Generated client details remain behind repository-local helpers.

### R-46-03: Phase 45 added selected readiness UI gating

`CanProvisionReviewedCloudResources` now requires `HasVerifiedSelectedCloudLivePreflightReadiness`. Phase 46 can rely on that helper for display and action availability, while still treating API validation as final.

### R-46-04: Cloud workflow result models already expose most evidence

`CloudWorkflowModels.CloudWorkflowActionResult` exposes action kind, state, status, summary, next step, cleanup risk, partial failure, remaining billable count, policy gate, cost summary, resources, audit records, and metadata.

### R-46-05: The current cloud operation result panel lacks a stable Phase 46 evidence checklist

The result panel renders metadata and resource tables, but required Phase 46 labels are not stable. A derived checklist can expose reviewed plan, verified preflight, provisioning action, cloud session, provider resources, endpoint, owner, template/profile, cost source, policy, reconciliation, cleanup risk, audit, and redaction state.

### R-46-06: Partial failure and duplicate-resource evidence can be derived safely

Existing result fields and cost/reconciliation visibility expose partial failure, cleanup-needed count, remaining billable count, orphan risk, unknown provider resource count, and finding counts. These are safe counts/states, not secret values.

### R-46-07: Live execution is not available in this workspace

No operator approval values, authenticated browser session, live provider account confirmation, or selected verified live readiness record were supplied in the current run. Phase 46 verification must classify live provisioning execution evidence as `human_needed` or blocked.

## Implications

- Add UI checklist/display helpers and tests rather than live provider execution.
- Keep all evidence display derived from repository facade models and allowlisted fields.
- Record automated verification separately from live operator/provider evidence.
