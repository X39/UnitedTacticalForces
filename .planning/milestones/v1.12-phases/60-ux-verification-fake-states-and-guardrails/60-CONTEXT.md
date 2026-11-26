# Phase 60: UX Verification, Fake States, And Guardrails - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning after Phase 59 completion

<domain>
## Phase Boundary

Phase 60 proves that the redesigned server creation, workspace, lifecycle, log, cleanup, and troubleshooting path is
understandable and safe through automated tests and browser-level mock/fake backend scenarios. It verifies the UX and
guardrails created across Phases 55-59; it does not add live provider mutation, live Steam/Arma runtime evidence, new
game products, payment behavior, or deployed-user migration work. Because the product is not deployed yet, planners
should update fake states, fixtures, and test contracts to the current intended model instead of preserving stale mock
payload compatibility.

</domain>

<decisions>
## Implementation Decisions

### Browser Walkthrough Evidence
- **D-01:** Use a hybrid browser verification strategy: one coherent end-to-end walkthrough plus focused browser
  checks for the required state matrix. A single happy path is not enough, and a disconnected matrix alone is not enough
  to prove the redesigned journey is understandable.
- **D-02:** The coherent walkthrough should exercise the normal user story: start server creation, confirm that
  intermediate wizard steps do not persist setup state, create a fake-backed server, land in the server workspace,
  review the relevant next action, inspect logs/jobs/status, and reach a safe operation or troubleshooting next step.
- **D-03:** Browser assertions should prove visible meaning: selected game, hosting mode, billing owner, current state,
  current/problem job, log freshness, cost/cleanup risk, provider teardown as cost-stop, and the primary next action.
  Do not rely on screenshots alone as the acceptance signal.
- **D-04:** Browser tests may use screenshots or trace artifacts as supporting evidence, but the pass/fail signal should
  be stable text, role, route, and action assertions against the Playwright mock backend.

### Fake State Matrix
- **D-05:** Cover the full Phase 60 roadmap state matrix: local record, local managed install, cloud planned, cloud
  provisioned, running, failed job, stuck/recovered job, log collection, cleanup needed, and provider teardown ready.
- **D-06:** Also cover the Phase 59 operational taxonomy where it affects the user-facing workspace: unknown,
  unavailable, unsupported, waiting for worker, stale, failed, deferred, and unavailable-until-setup-completes states.
  These should replace visible `n/a` proof, not be treated as optional polish.
- **D-07:** Each fake state should define the expected visible state label, reason, primary action, disabled or omitted
  conflicting actions, relevant tab/panel, and next step. Missing expectations should be treated as a test design gap.
- **D-08:** Mock backend scenarios should be reshaped to current contracts after Phase 59. There is no deployed-product
  migration burden, so do not preserve old fake payload shapes, old route labels, or obsolete Arma-specific UI text just
  for compatibility with earlier tests.
- **D-09:** Keep Arma 3 compatibility in the matrix because it is the existing supported controller, but the browser and
  repository boundary proof must stay generic where the product surface is meant to be generic.

### Guardrail And Redaction Proof
- **D-10:** Use layered guardrails: API/DTO tests, WebApp repository/source-boundary tests, rendered DOM/browser
  diagnostics checks, and mock payload sentinel checks. No single grep-style scan is sufficient for this phase.
- **D-11:** Redaction checks should use exact unsafe sentinel values and forbidden raw field names to avoid false
  positives from safe infrastructure terms such as CORS `authorization` headers or explanatory security copy.
- **D-12:** Guardrails must fail if provider tokens, Steam credentials, worker credentials, worker credential hashes,
  authorization headers, bearer tokens, cookies, raw secret-bearing payloads, generated DTOs, Kiota transport types, or
  `AdditionalData` leak into Razor-facing models, rendered UI, browser diagnostics, evidence artifacts, or user-facing
  logs.
- **D-13:** Existing source-boundary tests should be expanded rather than replaced. Keep Razor bound to hand-written
  repository models, keep generated Kiota models behind repository implementations, and keep raw HTTP bridges out of
  the server owner workflow.
- **D-14:** Phase 60 evidence should include negative proof that no live provider token, Steam secret, bearer/cookie
  material, worker hash, or raw payload string appears in the fake browser output or final verification artifact.

### Mock Vs Live Evidence Boundary
- **D-15:** Every Phase 60 verification artifact must classify its evidence as mock/fake browser verification. Use
  direct language such as `mock backend`, `fake provider state`, and `no live provider mutation`.
- **D-16:** Phase 60 must not update v1.11 Phase 51-54 live evidence status, Phase 30 live acceptance, OPS-02 live
  evidence, final billable resource count evidence, or CR-0001 acceptance classification.
- **D-17:** The final evidence should explicitly state that live Hetzner provisioning, live Steam/Arma runtime,
  authenticated operator browser walkthroughs, provider teardown, final billable count `0`, and full live redaction
  review remain deferred until whole-CR readiness and explicit cost-controlled UAT resumption.
- **D-18:** Fake provider and fake runtime records should be visibly fake in test data and evidence. Do not use real
  provider identifiers, real project names, real tokens, real cookies, real usernames, or real secret references in
  Phase 60 mock artifacts.

### No Migration Burden
- **D-19:** There is no deployed-product migration requirement for Phase 60. Planners should prefer clean test fixtures,
  corrected fake backend contracts, and current terminology over compatibility shims for earlier undeployed UX states.
- **D-20:** This does not relax persisted-truth, security, or generic-model requirements. Existing local Arma 3 behavior,
  billing ownership, provider teardown cost-stop semantics, generated-client/repository boundaries, and redaction
  guarantees still need verification.
- **D-21:** If Phase 59 changes DTOs, labels, lifecycle status fields, or workspace models, Phase 60 should update tests
  and fake payloads to match the final Phase 59 contracts instead of planning migration/backfill work.

### the agent's Discretion
- The agent may choose exact test class names, helper splits, scenario-builder APIs, and assertion helpers.
- The agent may choose whether the coherent walkthrough is one longer Playwright test or a small set of ordered
  browser tests, provided the evidence reads as one user journey.
- The agent may choose exact sentinel values and source-scan helper mechanics, provided the checks are precise enough to
  fail on unsafe exposure without blocking safe security-related code.
- The agent may choose whether final evidence is a dedicated verification Markdown file, test output summary, or both,
  provided the mock/live boundary is explicit.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Planning Scope
- `.planning/ROADMAP.md` - Phase 60 goal, dependency on Phase 59, boundaries, success criteria, and verification
  matrix.
- `.planning/REQUIREMENTS.md` - VER-02 through VER-04 plus no-live-UAT and out-of-scope constraints.
- `.planning/PROJECT.md` - v1.12 milestone direction, security boundaries, provider cost-stop semantics, and parked
  live-evidence context.
- `.planning/STATE.md` - current project state, pending warnings, and parked v1.11 live UAT status.
- `.planning/phases/59-lifecycle-log-and-status-feedback-repair/59-CONTEXT.md` - operational taxonomy, log behavior,
  job timeline, and next-step decisions that Phase 60 must verify after Phase 59 implementation.
- `.planning/phases/58-unified-server-workspace/58-CONTEXT.md` - selected-server workspace proof surface, logs tab,
  provider teardown cost-stop wording, and owner/admin boundaries.
- `.planning/phases/57-guided-server-creation-journey/57-CONTEXT.md` - wizard persistence boundary, workspace handoff,
  cloud continuation, and task-oriented labels.
- `.planning/phases/56-generic-runtime-template-and-plan-snapshots/56-CONTEXT.md` - generic template/profile/review
  language and stale-state decisions.
- `.planning/phases/55-generic-game-hosting-capability-contracts/55-CONTEXT.md` - generic capability/repository
  boundaries and Arma 3 compatibility requirements.

### Codebase Maps
- `.planning/codebase/TESTING.md` - historical testing map; note that current API/UI test projects have expanded beyond
  the original parser-only snapshot.
- `.planning/codebase/CONVENTIONS.md` - C#, Razor, repository, DTO, source-boundary, and generated-code conventions.
- `.planning/codebase/STRUCTURE.md` - active source/test locations and generated-client boundaries.

### Browser Test Infrastructure
- `tests/Ui.Tests/Ui.Tests.csproj` - Playwright/xUnit UI test project.
- `tests/Ui.Tests/WebAppHostFixture.cs` - WebApp test host startup, browser setup, and optional
  `UTF_UI_TEST_BASE_URL` behavior.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` - route-level mock backend and fake server/cloud/job/log/provider states.
- `tests/Ui.Tests/CloudAgentUiTests.cs` - existing browser tests for create flow, workspace tabs, logs, cloud setup,
  teardown, failed jobs, and mock backend assertions.

### Automated Guardrails
- `tests/Api.Tests/Api.Tests.csproj` - API/source-boundary test project.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` - selected-server workspace model/action/logs/provider
  teardown boundary tests.
- `tests/Api.Tests/WebApp/GameServerCreationBoundaryTests.cs` - creation wizard non-persistence and no Arma-specific
  public marker boundary tests.
- `tests/Api.Tests/WebApp/GenericGameHostingContractBoundaryTests.cs` - generic hosting, generated-client, and no
  handwritten route fallback checks.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - repository facade, generated-client, policy,
  teardown, live-evidence classification, and redaction boundary tests.
- `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs` - unsafe display marker checks for provider,
  bearer, worker credential, and raw payload exposure.
- `tests/Api.Tests/WebApp/UserFacingErrorBoundaryTests.cs` - user-facing exception/message exposure guardrails.
- `tests/Api.Tests/GameServerCapabilities/GameServerCapabilityContractTests.cs` - generic capability and Arma 3
  compatibility contract proof.
- `tests/Api.Tests/GameServerCreation/GameServerCreationApiTests.cs` - record/local create behavior and ownership/job
  expectations.
- `tests/Api.Tests/GameServerLifecycle/LifecycleJobQueueServiceTests.cs` - queued/running/retry/stale recovery job truth.
- `tests/Api.Tests/GameServerLifecycle/LifecycleWorkerRegistryServiceTests.cs` - worker DTO credential-hash redaction.

### Server UX And Repository Surfaces
- `sources/WebApp/Pages/Servers/Create.razor` - guided creation wizard and no-intermediate-persistence behavior.
- `sources/WebApp/Pages/Servers/Details.razor` - selected-server workspace, Overview, Logs, Jobs, Cloud, and action
  surfaces.
- `sources/WebApp/Pages/Servers/Advanced.razor` - advanced cloud setup/review/provisioning surface that may still be
  involved in browser walkthroughs.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - WebApp repository facade for creation,
  workspace, lifecycle, logs, cloud, policy, cost, and teardown operations.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` - generated-client mapping and repository
  projection implementation.
- `sources/WebApp/Services/GameServerRepository/ServerWorkspaceModels.cs` - hand-written Razor-facing workspace models.
- `sources/WebApp/Services/GameServerRepository/LifecycleJobInfo.cs` - Razor-facing lifecycle job model.
- `sources/WebApp/Services/GameServerRepository/CloudRuntimeStatusInfo.cs` - Razor-facing runtime/job/progress model.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs` - lifecycle/cloud workflow
  result, next-step, cost, policy, and teardown mapping.
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs` - redaction guard for
  repository display text.

### API Lifecycle And Projection Surfaces
- `sources/Api/Controllers/GameServersController.cs` - server-scoped API routes for details, lifecycle jobs, logs,
  runtime status, cost/policy, cloud setup, and provider teardown.
- `sources/Api/DTO/LifecycleJobDto.cs` - lifecycle job fields available to clients.
- `sources/Contract/GameServer/ELifecycleJobStatus.cs` - durable lifecycle job statuses.
- `sources/Contract/GameServer/ELifecycleJobKind.cs` - lifecycle job kinds including log collection and cleanup.
- `sources/Api/Services/GameServerLifecycle/GameServerLifecycleJobService.cs` - lifecycle enqueue validation and active
  job behavior.
- `sources/Api/Services/GameServerLifecycle/LifecycleJobQueueService.cs` - claim, retry, failure, success, and stale
  recovery transitions.
- `sources/Api/HostedServices/LifecycleJobRecoveryWorker.cs` - background stale job recovery loop.
- `sources/Api/Services/GameServerLifecycle/LifecycleJobExecutor.cs` - lifecycle job execution, including log collection.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `tests/Ui.Tests/GameServerUiMockBackend.cs` already models fake game servers, lifecycle jobs, logs, cloud sessions,
  cost/policy state, cloud plans, provider teardown, and setup blockers. Phase 60 should extend it for the remaining
  Phase 59/60 state matrix instead of introducing a separate mock server.
- `tests/Ui.Tests/CloudAgentUiTests.cs` already covers create wizard modes, no intermediate persistence, record/local
  and cloud creation, workspace tabs, failed lifecycle server, logs tab, teardown, policy blockers, and stale review.
- `tests/Ui.Tests/WebAppHostFixture.cs` already runs the WebApp with Playwright and supports an externally supplied
  base URL for test reuse.
- API boundary tests under `tests/Api.Tests/WebApp` already scan source files for repository boundaries, raw transport
  exposure, generated DTO leakage, unsafe labels, and deferred live-evidence claims.
- `CloudWorkflowRedactionGuard` and existing redaction tests provide a pattern for sentinel-based unsafe display checks.

### Established Patterns
- Browser tests use xUnit plus Playwright and should assert visible text/roles/routes against a mock backend rather
  than invoking live Hetzner, Steam, Arma, bearer, cookie, or secret-bearing systems.
- Razor pages should bind hand-written repository models, while generated Kiota models stay behind repository
  implementations.
- Source-boundary tests commonly use exact forbidden markers and helper assertions; continue that pattern to avoid noisy
  broad scans.
- Provider teardown/deletion is the cost-stop boundary; runtime stop and runtime cleanup are not cost-stop evidence.
- Live UAT remains parked. Phase 60 evidence is product/UX verification under fake states, not CR-0001 live acceptance.

### Integration Points
- Extend `GameServerUiMockBackend` with explicit scenario helpers for local record, local managed install, cloud
  planned, cloud provisioned, running, failed job, stuck/recovered job, log collection, cleanup needed, teardown ready,
  cost-stopped, unknown/unavailable/unsupported/deferred, and waiting-for-worker states.
- Expand `CloudAgentUiTests` with one coherent create-to-workspace walkthrough and focused scenario checks for the full
  state matrix.
- Expand `ServerWorkspaceBoundaryTests`, `GameServerCreationBoundaryTests`, `GenericGameHostingContractBoundaryTests`,
  `CloudWorkflowRepositoryBoundaryTests`, and redaction/source-boundary tests to cover the final Phase 59/60 labels,
  repository models, and unsafe exposure markers.
- Add or update final verification evidence that explicitly distinguishes mock/browser proof from deferred live UAT.

</code_context>

<specifics>
## Specific Ideas

- The user explicitly delegated all Phase 60 gray-area choices to the agent.
- The user explicitly clarified that no migration effort is needed because the product is not deployed yet.
- Unpackaged sketches were detected under `.planning/sketches/MANIFEST.md`, but no packaged sketch-findings skill was
  available. Downstream agents should not treat sketch findings as canonical unless wrapped up separately.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 60-UX Verification, Fake States, And Guardrails*
*Context gathered: 2026-07-05*
