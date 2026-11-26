# Phase 47: Live Runtime And Visibility Evidence - Context

**Gathered:** 2026-06-22
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Operator can prove cloud worker bootstrap and Arma 3 runtime behavior through authenticated views, or record an explicit environment blocker.

The selected `/servers` workflow is the primary owner-facing surface. Phase 47 must make worker bootstrap, registration, heartbeat, capabilities, runtime lifecycle, log collection, authenticated visibility, owner/cost/policy/reconciliation context, and redaction state visible as safe evidence. It must not mark Steam/Arma runtime as passed unless live install, configure, start, stop, and log evidence exists.

</domain>

<decisions>
## Implementation Decisions

### The agent's discretion

All implementation choices are at the agent's discretion because discuss was skipped. Use ROADMAP phase goal, success criteria, requirements, Phase 46 verification, existing `/servers` runtime panel, and codebase conventions to guide decisions.

### Safety boundary

Phase 46 classified actual live provisioning evidence as `human_needed`. Phase 47 may harden UI evidence and automated checks, but live runtime execution remains `human_needed` or blocked until an operator supplies a provisioned cloud session and worker/runtime evidence.

</decisions>

<code_context>
## Existing Code Insights

- `sources/WebApp/Pages/Servers/Index.razor` already renders selected-server cloud runtime status with endpoint, worker, heartbeat, runtime status, current job, progress, recent log, failure, available actions, cost, policy, reconciliation, cleanup risk, provider teardown, lifecycle jobs, and logs.
- `CloudRuntimeStatusInfo` exposes session, worker, worker status, heartbeat, readiness, current job/progress, last failure, recent log timestamp, retry/cleanup flags, and safe action labels.
- Runtime actions already route through `IGameServerRepository` facade methods and `RunCloudLifecycleActionAsync`; raw HTTP/generated client details stay outside the Razor page.
- Phase 46 added a stable provisioning evidence grid and boundary tests in `CloudWorkflowRepositoryBoundaryTests.cs`.

</code_context>

<specifics>
## Specific Ideas

- Add a compact runtime evidence checklist beside the selected-server `Cloud runtime` panel.
- Keep labels stable for worker bootstrap, registration, heartbeat, capabilities, install/configure, start, stop, logs, authenticated visibility, owner/cost/policy/reconciliation, cleanup separation, and redaction.
- Use existing status badges, metadata grids, lifecycle job rows, logs, and cost/policy/reconciliation helpers.
- Create a Phase 47 verification artifact that classifies automated UI/source evidence separately from live runtime evidence.

</specifics>

<deferred>
## Deferred Ideas

Provider teardown, final billable count closure, global redaction closeout, Phase 30 pass/fail, and CR acceptance classification stay in Phases 48-49.

</deferred>
