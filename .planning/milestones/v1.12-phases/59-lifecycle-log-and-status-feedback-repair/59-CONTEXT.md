# Phase 59: Lifecycle, Log, And Status Feedback Repair - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 59 repairs selected-server operational feedback so background lifecycle operations, log freshness,
runtime state, worker/provider state, policy blockers, cleanup risk, and next actions are truthful and
actionable. Users should no longer see stuck-looking lifecycle jobs or unexplained `n/a` labels. The phase may
change persisted lifecycle/status schema where that creates clearer durable truth, because the service has no
deployed database migration obligation yet. It must preserve PostgreSQL as the source of job truth, avoid fake
completion, keep provider teardown as the cost-stop action, and avoid exposing secrets or broad provider inventory
unnecessarily.

</domain>

<decisions>
## Implementation Decisions

### Job Timeline Language
- **D-01:** Phase 59 may add or revise persisted lifecycle status fields/enums if that is the cleanest model. No
  deployed database compatibility constraint should force projection-only workarounds.
- **D-02:** Stale and recovered job states must be backed by real evidence: lease expiry, recovery transitions,
  retry scheduling, failure state, audit records, or equivalent durable data. Do not fabricate completion or
  recovery.
- **D-03:** The Jobs tab should show timeline rows with status reasons, not badge-only audit rows. Rows should
  explain why a job is waiting, running, retrying, recovered, failed, cancelled, or completed.
- **D-04:** Overview may highlight the current/problem job before the full history, while the Jobs tab keeps the
  durable timeline.
- **D-05:** Job rows should show owner-safe actor/requester context where available, assigned worker, attempt count,
  key timestamps, next retry time, completion time, and sanitized failure/progress reason. Broader fleet/provider
  details remain admin diagnostics.
- **D-06:** `Cancelled` should be an honest first-class terminal state with reason and timestamp. Add or repair
  cancellation mechanics where needed for truthful feedback.

### Log Collection Interaction
- **D-07:** Do not add a standalone manual log-collection step, and do not start surprise background jobs when users
  open the Logs tab.
- **D-08:** The Logs tab should automatically follow an already-running `CollectLogs` or lifecycle job and refresh
  logs when it completes. If no relevant job exists, opening Logs reloads persisted logs only.
- **D-09:** Stopping a server must always capture all logs as part of the stop operation. Users should not need to
  trigger separate log collection after stop.
- **D-10:** The planner may implement final log capture as one `Stop` job with log-capture progress or as linked
  `Stop` and `CollectLogs` jobs. The UI must present it as one user operation: `Stop server`, with final log capture
  included and automatic log refresh after completion.
- **D-11:** While waiting for logs, keep persisted logs visible and show inline progress/status above them, including
  last-refreshed and current-job timestamps. On failure, preserve last known logs and show sanitized failure plus a
  next step.
- **D-12:** Log freshness should use honest labels such as `Logs current`, `Refreshing after stop`, `Last refreshed`,
  `Waiting for worker`, `Collection unavailable`, and `Collection failed`. Do not use `n/a` for log freshness.

### Unknown Status Taxonomy
- **D-13:** Replace selected-server workspace `n/a` labels with a small owner-safe operational taxonomy:
  `Unknown`, `Not available`, `Not supported`, `Deferred`, `Waiting for worker`, `Stale`, `Failed`, and
  `Unavailable until setup completes`.
- **D-14:** Operational truth should be projected server-side when it depends on lifecycle jobs, worker heartbeat,
  provider session state, policy state, stale metadata, cleanup state, or log freshness. The WebApp may style and
  display these values, but it should not infer core truth in Razor.
- **D-15:** Phase 59 can use stable English labels/reasons server-side. Do not spend this phase on localization
  plumbing for these operational labels.
- **D-16:** Use strict status meanings. `Not supported` means the game, hosting mode, provider shape, or action
  cannot support the capability. `Not available` means current state/configuration blocks a possible capability.
  `Waiting for worker` may unlock after background work. `Deferred` is intentionally parked. `Unknown` lacks safe
  evidence. `Stale` has old or superseded evidence. `Failed` includes a sanitized reason.
- **D-17:** Overview should promote unknown, stale, and failed states only when actionable: they block an action,
  affect cost/cleanup risk, make runtime ambiguous, or require user/admin next steps. Detailed cards/tabs carry
  non-actionable status detail.
- **D-18:** Remove visible `n/a` from selected-server workspace operations, status, logs, jobs, runtime, worker,
  provider, cost, policy, and cleanup surfaces. Do not perform a global WebApp cleanup.

### Next-Step Routing
- **D-19:** Primary action priority is cost and safety first: provider teardown/cleanup risk when active cost or
  cleanup exposure exists, then failed/retryable jobs, then policy approval/blockers, then active job refresh/log
  visibility, then ordinary runtime actions.
- **D-20:** Do not show a separate retry action when the normal state is usable. If `Start` failed and the server is
  still stopped, show the failure context and leave `Start server` available. Use special retry/recover actions only
  when an intermediate or corrupted state cannot safely expose the normal action.
- **D-21:** Blocked states should route to the smallest useful repair target: approval/review for policy approval,
  cloud review for stale setup, provider teardown or billable-resource review for cleanup/cost risk, Logs for log
  freshness, Jobs for job state, and diagnostics only when the fix is not otherwise clear.
- **D-22:** Keep diagnostics always visible for now with owner-safe content. Future work may align claims with all
  features more precisely; Phase 59 should not block on that. Preserve redaction and avoid secrets, raw credentials,
  and unnecessary broad provider inventory exposure.
- **D-23:** When a lifecycle job is running, promote following that job or related logs as the primary action unless
  provider teardown/cleanup risk has higher priority. Conflicting lifecycle actions should be disabled or omitted
  with a clear waiting-for-current-job reason.

### the agent's Discretion
- The agent may choose the exact lifecycle schema shape, status enum additions, or supplemental status reason fields
  as long as persisted truth stays durable and honest.
- The agent may choose one stop job with embedded log capture or linked stop/log jobs as long as the UI presents one
  `Stop server` operation and refreshes logs automatically.
- The agent may choose exact component splits and label field names, but operational status/reason inference belongs
  server-side when it depends on backend state.
- The agent may choose exact Overview action-ranking implementation as long as cost/safety outranks normal runtime
  actions and normal actions remain available after safe failed attempts.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning Scope
- `.planning/ROADMAP.md` - Phase 59 goal, boundaries, success criteria, and planned split.
- `.planning/REQUIREMENTS.md` - OPS-01 through OPS-04 and VER follow-on constraints for v1.12.
- `.planning/PROJECT.md` - current milestone direction, no-live-UAT boundary, cost-stop semantics, and security
  constraints.
- `.planning/STATE.md` - current project state, Phase 59 readiness, and pending warnings.
- `.planning/phases/58-unified-server-workspace/58-CONTEXT.md` - workspace placement, logs-tab decision, provider
  teardown cost-stop wording, and owner/admin boundary.
- `.planning/phases/57-guided-server-creation-journey/57-CONTEXT.md` - task-oriented language and workspace handoff
  decisions.
- `.planning/phases/56-generic-runtime-template-and-plan-snapshots/56-CONTEXT.md` - generic status/review language,
  stale-state split, and worker-recipe boundaries.

### Codebase Maps
- `.planning/codebase/ARCHITECTURE.md` - API, WebApp repository, generated-client, lifecycle, and update-stream
  architecture.
- `.planning/codebase/STRUCTURE.md` - active source locations, WebApp page/service layout, and generated-code
  boundaries.
- `.planning/codebase/CONVENTIONS.md` - C#, Razor, repository, DTO, and test conventions.

### Server Workspace And Repository
- `sources/WebApp/Pages/Servers/Details.razor` - selected-server workspace, Overview, Logs, Jobs, status formatting,
  and remaining `n/a` paths.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - WebApp repository contract for server
  details, lifecycle, logs, cloud status, and workspace overview.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - workspace overview aggregation,
  action ranking, lifecycle actions, runtime status mapping, and log calls.
- `sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs` - overview, action, job, runtime,
  worker/provider, cost/policy, cleanup, and teardown UI-facing models.
- `sources/WebApp/Services/GameServerRepository/LifecycleJobInfo.cs` - current Razor-facing lifecycle job data.
- `sources/WebApp/Services/GameServerRepository/CloudRuntimeStatusInfo.cs` - current runtime/job/progress/failure
  projection consumed by the WebApp.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - lifecycle, runtime,
  policy, teardown, failure, and required-next-step mapping.

### API Lifecycle And Runtime Status
- `sources/Api/Controllers/GameServersController.cs` - lifecycle job list, log collection endpoint, cloud runtime
  status projection, runtime actions, and enqueue behavior.
- `sources/Api/DTO/LifecycleJobDto.cs` - current lifecycle job DTO fields available to clients.
- `sources/Contract/GameServer/ELifecycleJobStatus.cs` - current durable job status enum.
- `sources/Contract/GameServer/ELifecycleJobKind.cs` - current lifecycle job kind enum.
- `sources/Api/Services/GameServerLifecycle/GameServerLifecycleJobService.cs` - enqueue validation and active job
  behavior.
- `sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs` - claim, retry, failure, success, and stale
  lease recovery transitions.
- `sources/Api/HostedServices/LifecycleJobRecoveryWorker.cs` - background stale job recovery loop.
- `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs` - worker-side lifecycle execution switch,
  including log collection.

### Verification Surfaces
- `tests/Api.Tests/GameServerLifecycle/LifecycleJobQueueServiceTests.cs` - existing retry and stale recovery coverage.
- `tests/Api.Tests/CloudInventory/CloudInventoryVisibilityTests.cs` - current cloud runtime status/action visibility
  tests.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - workspace boundary tests, including no user-facing
  collect-logs action and cost-stop wording.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - repository and cloud workflow mapping boundary
  tests.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` - mock backend lifecycle, log, runtime, cost, cleanup, and provider
  states for browser-level verification.
- `tests/Ui.Tests/CloudAgentUiTests.cs` - existing browser tests for selected-server workspace behavior.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `sources/WebApp/Pages/Servers/Details.razor` already has Overview, Logs, Jobs, Cloud, Ownership, Audit, and action
  sections. Phase 59 should repair this workspace rather than create a competing operations page.
- `ServerWorkspaceOverviewInfo`, `ServerWorkspaceActionInfo`, and related records already centralize the selected
  server Overview model and action ranking.
- `LifecycleJobInfo`, `LifecycleJobDto`, and `ELifecycleJobStatus` already carry attempts, timestamps, worker id,
  retry time, completion time, and failure reason. They are the natural base for richer job timeline projections.
- `CloudRuntimeStatusInfo` and `CloudRuntimeStatusDto` already carry current job, progress percent/message, last
  failed job, recent log timestamp, and available actions.
- `LifecycleJobQueueService.RecoverStaleJobsAsync` already records stale lease recovery as retry-pending or failed
  durable transitions; Phase 59 can make this visible instead of inventing client-only guesses.
- Existing UI tests and `GameServerUiMockBackend` can model running, failed, retry-pending, log, cleanup, and
  provider states without live Hetzner, Steam, Arma, credentials, or secrets.

### Established Patterns
- Razor should bind hand-written repository models and call generated Kiota clients only through repository
  implementations.
- API controllers project DTOs rather than exposing EF entities.
- Generated Kiota files under `sources/WebApp/UtfApi` are not edited by hand.
- Provider teardown/deletion is the cost-stop boundary. Runtime stop and runtime cleanup do not stop provider cost.
- User-facing server workspace state must remain owner-safe and redacted. Provider tokens, Steam credentials,
  worker credentials, hashes, bearer headers, cookies, and raw payload dumps must not appear in frontend models,
  logs, audit details, or planning artifacts.
- v1.12 verification uses automated and mock/fake browser states. Live Hetzner/Steam/Arma UAT remains parked.

### Integration Points
- Add or revise API DTO/projection fields for lifecycle status labels, status reasons, stale/recovered/cancelled
  context, log freshness, worker/provider state, and exact next-step routing.
- Update repository models and mappers so Razor receives ready-to-display operational state instead of inferring
  backend truth from enum `ToString()` and `n/a` fallbacks.
- Rework Logs tab behavior to follow existing relevant lifecycle jobs, show inline progress, keep persisted logs
  visible, and refresh automatically after stop/log-related completion.
- Rework primary/secondary workspace action ranking around cost/safety, failed-but-safe normal actions, exact fix
  surfaces, visible diagnostics, and active-job follow behavior.
- Expand API boundary tests, WebApp source-boundary tests, and UI mock-backend scenarios for stuck/recovered jobs,
  stop-with-final-log capture, log freshness, status labels, and action availability.

</code_context>

<specifics>
## Specific Ideas

- The service is not deployed anywhere yet, so schema cleanup is allowed when it improves durable truth.
- Stop must collect all logs automatically as part of stopping the server.
- Opening the Logs tab must not start new jobs, but it should follow and refresh for already-running relevant jobs.
- Failed operations should leave the normal action available when the resulting state is safe. For example, after a
  failed start that leaves the server stopped, show context and keep `Start server` available.
- Diagnostics should remain visible for now. Claims alignment for all features is a future goal, not Phase 59 scope.
- Unpackaged sketches were detected under `.planning/sketches/MANIFEST.md`, but no packaged sketch-findings skill was
  available. Downstream agents should not treat sketch findings as canonical unless they are wrapped up separately.

</specifics>

<deferred>
## Deferred Ideas

- Full feature/claims alignment for diagnostics and other server workspace capabilities belongs in a future goal.
- Localization plumbing for the new operational labels is intentionally deferred; Phase 59 may use stable English
  server-projected labels.

</deferred>

---

*Phase: 59-Lifecycle, Log, And Status Feedback Repair*
*Context gathered: 2026-07-05*
