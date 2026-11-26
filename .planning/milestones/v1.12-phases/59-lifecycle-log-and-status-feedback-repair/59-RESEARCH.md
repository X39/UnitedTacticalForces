# Phase 59: Lifecycle, Log, And Status Feedback Repair - Research

**Phase:** 59 - Lifecycle, Log, And Status Feedback Repair
**Status:** Ready for planning
**Generated:** 2026-07-05

## Research Complete

Phase 59 is an operational feedback repair over the selected-server workspace, durable lifecycle jobs, log
collection, and server-scoped runtime projections. It should be planned as a narrow control-plane/UI projection
repair, not as a new provider integration or a replacement for the existing game-controller abstraction.

## Scope Interpretation

The phase must satisfy OPS-01 through OPS-04:

- OPS-01: lifecycle jobs must show accurate queued, running, retrying, succeeded, failed, cancelled, stale, and
  recovery evidence with timestamps and owner-safe actor/worker context.
- OPS-02: log collection must be visible as progress/result/freshness and refreshed logs without a stuck UI. The
  user decision changes this from "manual collect" to "follow existing collection and include final log capture in
  stop".
- OPS-03: selected-server operational surfaces must replace unexplained `n/a` with honest labels such as Unknown,
  Not available, Not supported, Deferred, Waiting for worker, Stale, Failed, and Unavailable until setup completes.
- OPS-04: failed and blocked operations must point to the right next step. Safe failed operations should leave the
  normal action available rather than forcing a separate retry action.

## Standard Stack

- ASP.NET Core controllers project DTOs from EF entities.
- EF Core/Npgsql is the durable source of lifecycle, worker, ownership, cost, policy, and audit truth.
- The WebApp uses Blazor WebAssembly, Microsoft Fluent UI Blazor, existing `Utf*` components, and repository-facing
  hand-written models.
- Generated Kiota files under `sources/WebApp/UtfApi/**` are generated output. API DTO changes require regenerating
  or intentionally updating the generated client output, but hand-written behavior stays in repository classes.

## Existing Implementation Findings

### Lifecycle Jobs

- `LifecycleJob` already persists job kind, execution status, requested user, billing account/assignment, attempt
  counts, created/updated/started/claimed/lease/next-attempt/completed timestamps, assigned worker id, and sanitized
  failure reason.
- `ELifecycleJobStatus` already includes `Queued`, `Running`, `Succeeded`, `Failed`, `Cancelled`, and
  `RetryPending`.
- `LifecycleJobQueueService.RecoverStaleJobsAsync` detects expired running job leases and turns them into
  `RetryPending` or `Failed` with an audit record and sanitized failure reason. This is real recovery evidence that
  can be projected instead of inventing a fake `Recovered` terminal status.
- `LifecycleJobDto` and `LifecycleJobInfo` do not expose status labels, status reasons, next-step targets, or
  log-refresh semantics. Razor currently formats raw enum names and fills missing cells with `n/a`.

### Logs

- `CollectGameServerLogsAsync` queues an `ELifecycleJobKind.CollectLogs` job, but Phase 58 removed user-facing
  collect-log actions from the workspace.
- `LifecycleJobExecutor.ExecuteAsync` currently no-ops `CollectLogs`.
- `Stop` currently calls `controller.StopAsync(requestedBy)` without an explicit final log-capture step.
- Local game controllers already persist process and Steam command output through `GameServerLogs`; cloud workers
  can append logs through `CloudWorkerDispatchService.AppendLogAsync`.
- The Logs tab currently reloads persisted logs only when section data loads. It does not follow a running
  log-related job or show log freshness/progress above the persisted log list.

### Status Labels

- The WebApp repository currently maps many backend states with `ToString()` or empty strings:
  `ServerStatusLabel`, runtime `StatusLabel`, worker/provider status labels, policy labels, and workspace job labels.
- `Details.razor` has `MissingValue = "n/a"` and still uses it in selected-server operational surfaces including
  runtime, worker/provider, cost/policy, ownership, jobs, logs, and danger/diagnostic panels.
- `CloudRuntimeStatusDto` already carries current job, progress percent/message, last failed job, latest log
  timestamp, retry/cleanup flags, and available action strings. It is the right API projection point for server-side
  operational truth.

### Next-Step Routing

- `SelectPrimaryWorkspaceAction` currently ranks provider teardown, policy review, provisioning, start, stop, logs,
  and refresh. It does not yet account for active lifecycle jobs, failed-but-safe normal actions, explicit
  stale/unknown status labels, or diagnostics always visible.
- `CloudWorkflowResultMapper` often maps failures to `Retry cloud action` or `Refresh cloud status`. Phase 59 should
  refine this so a failed start that leaves the server stopped can simply show the failure context and keep
  `Start server` available.
- Diagnostics currently remain gated by `CanViewProviderDiagnostics`; the user decided diagnostics should be
  visible for now, with owner-safe/redacted content and later claims alignment.

## Planning Recommendations

### Plan Shape

Use the roadmap's three-plan split:

1. Job/status projection fixes and label taxonomy.
2. Log collection progress and refresh behavior.
3. Retry/cleanup/approval next-step feedback and regression tests.

### Implementation Approach

- Keep `ELifecycleJobStatus` as execution state. Add explicit status-reason and operational-label projection fields
  rather than making "recovered" a misleading terminal execution status.
- Persist recovery/cancellation/status reason where needed because there is no deployed migration obligation. Generate
  EF migrations through the project tooling rather than hand-editing migration files.
- Create a small shared operational status vocabulary in contract/API DTOs and map it server-side when the truth
  depends on lifecycle jobs, worker heartbeat, provider state, policy state, cleanup risk, or log freshness.
- Keep generated client churn contained. Razor should consume `LifecycleJobInfo`, `CloudRuntimeStatusInfo`, and
  `ServerWorkspace*` models, not generated DTOs.
- Preserve the backend `CollectLogs` lifecycle kind for workers/internal flows, but do not expose a manual collect
  action in the selected-server UI.
- Implement final log capture as part of the `Stop` job's execution path. If cloud worker behavior needs linked
  stop/log semantics, the UI must still present one `Stop server` operation.
- The Logs tab should follow existing relevant jobs (`CollectLogs`, `Stop`, `Restart` while stopping, and any
  current job that reports log/progress data), show freshness/progress, keep persisted logs visible, and refresh after
  completion. Opening Logs must not enqueue work.

## Validation Architecture

Automated validation should combine:

- API lifecycle tests for status-reason persistence, stale recovery evidence, cancellation reason/timestamp, stop
  with final log capture, and sanitized failure/status details.
- API projection tests for `CloudRuntimeStatusDto` and lifecycle job DTO labels/reasons/next steps without secret
  values.
- WebApp source boundary tests for no selected-server `n/a` labels in operational surfaces and no user-facing
  collect-log action.
- Repository mapping tests or source tests for action priority: provider teardown/cost risk, failed-but-safe normal
  action availability, policy approval routing, active job/log follow-up, and diagnostics visibility.
- UI mock-backend tests for stuck/recovered jobs, running log collection, stop with automatic log refresh, failed
  start with `Start server` still available, cleanup risk, provider teardown, and unknown/waiting/stale labels.

Recommended commands:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GameServerLifecycle|FullyQualifiedName~CloudInventoryVisibilityTests"`
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"`
- `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"`
- `dotnet build X39.UnitedTacticalForces.slnx`
- `dotnet test X39.UnitedTacticalForces.slnx`

## Risks And Constraints

- Do not fabricate job completion. Stale/recovered UI states must cite lease expiry, retry scheduling, failure, audit,
  or cancellation data.
- Do not start surprise background jobs from the Logs tab.
- Do not reintroduce `Collect logs` as a user-facing workspace action.
- Do not expose provider tokens, Steam credentials, worker credentials, hashes, authorization headers, bearer tokens,
  cookies, or raw secret-bearing payloads in DTOs, logs, audit details, or WebApp models.
- Live Hetzner, Steam, Arma, authenticated browser, and provider mutation UAT remain deferred to later verification.

## Research Complete

Planner can proceed using this research, `59-CONTEXT.md`, `59-UI-SPEC.md`, `59-VALIDATION.md`, and
`59-PATTERNS.md`.
