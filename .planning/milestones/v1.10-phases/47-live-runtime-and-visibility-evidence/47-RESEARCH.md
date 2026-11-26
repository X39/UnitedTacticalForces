# Phase 47 Research

## Objective

Identify what already supports live runtime and visibility evidence, and what must be added without inventing live Steam/Arma or worker facts.

## Findings

### R-47-01: The selected-server runtime panel already aggregates core runtime status

`sources/WebApp/Pages/Servers/Index.razor` renders `Cloud runtime` with readiness, endpoint, worker, last heartbeat, runtime status, current job, progress, recent log, last failure, actions, cost source, billing records, metadata freshness, findings, cleanup risk, policy state, and policy next step.

### R-47-02: Runtime actions are already repository-backed

Install, update, start, stop, restart, collect logs, and cleanup runtime actions call `QueueCloudLifecycleJobAsync` or `CleanupSelectedCloudRuntimeAsync`, which route through `IGameServerRepository` methods. The Razor page does not need raw HTTP or generated client builders.

### R-47-03: `CloudRuntimeStatusInfo` exposes safe evidence fields

The WebApp facade model exposes session, worker, worker status, last heartbeat, runtime readiness, current job, progress, recent log timestamp, failure summary, retry/cleanup booleans, and available action labels.

### R-47-04: Lifecycle jobs and logs already provide visibility evidence

The selected-server details load recent lifecycle jobs, logs, hosting audit records, owner history, cloud status rows, cost visibility, policy visibility, and live preflight records. Phase 47 can derive stable evidence rows from these sources.

### R-47-05: The current runtime panel lacks a stable Phase 47 evidence checklist

The UI displays runtime information, but required Phase 47 labels are not stable enough for acceptance evidence. A derived checklist can expose worker bootstrap, registration, heartbeat, capabilities, install/configure, start/stop, logs, authenticated views, owner/cost/policy/reconciliation, cleanup separation, blocker state, and redaction state.

### R-47-06: Live execution is not available in this workspace

Phase 46 verification classified actual live provisioning, provider resource inventory, endpoint, provider-side inspection, and billable-resource closure as `human_needed`. Without a supplied live session and worker/runtime evidence, Phase 47 must classify live runtime facts as `human_needed` or blocked.

## Implications

- Add runtime evidence rows and tests rather than live Steam/Arma execution.
- Preserve existing runtime action gates and repository boundaries.
- Record automated UI/source evidence separately from live operator/runtime evidence.
