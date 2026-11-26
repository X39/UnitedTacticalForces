# Phase 60: UX Verification, Fake States, And Guardrails - Research

**Phase:** 60 - UX Verification, Fake States, And Guardrails
**Status:** Ready for planning
**Generated:** 2026-07-05

## Research Complete

Phase 60 is a verification and guardrail phase over the server creation and selected-server workspace work completed in
Phases 55-59. It should not introduce live provider mutation, new hosting products, payment behavior, or deployed-user
migration paths. The useful work is to make fake states explicit, prove the intended user journey in the browser, and
close source/browser redaction boundaries with precise sentinel checks.

## Scope Interpretation

The phase must satisfy VER-01 through VER-04:

- VER-01: preserve automated proof that the generic hosting contract and Arma 3 compatibility stay generic and do not
  regress into new Arma-3-only WebApp repository boundaries.
- VER-02: cover the full creation/workspace fake state matrix: local record, local managed install, cloud planned,
  cloud provisioned, running, failed job, stuck/recovered job, log collection, cleanup needed, and provider teardown
  ready scenarios.
- VER-03: use browser-level verification with the mock backend to prove the redesigned server path is understandable
  without live Hetzner, Steam, Arma, bearer, cookie, or secret material.
- VER-04: confirm redaction and repository boundaries across API DTOs, WebApp repository models, rendered DOM,
  diagnostics, browser evidence, and final artifacts.

## Existing Implementation Findings

### Browser Test Infrastructure

- `tests/Ui.Tests/WebAppHostFixture.cs` starts the WebApp test host and supports `UTF_UI_TEST_BASE_URL` for reuse
  against an externally supplied WebApp.
- `tests/Ui.Tests/GameServerUiMockBackend.cs` owns the Playwright route-level fake API. It already handles game
  hosting capabilities, creation options, readiness, local/cloud server creation, logs, lifecycle jobs, cloud runtime,
  cost, policy, dry-run review, provider teardown, and admin inventory routes.
- `tests/Ui.Tests/CloudAgentUiTests.cs` already proves creation choices, no intermediate wizard persistence, local
  managed creation, cloud creation workspace handoff, workspace tabs, logs, teardown, policy blockers, job status
  rows, unknown/deferred states, and selected-server diagnostics.
- Phase 59 added several required states, but the current matrix is implicit in individual tests. Phase 60 should make
  the matrix explicit so future UX changes cannot silently drop a state.

### API And Source Boundary Tests

- `tests/Api.Tests/WebApp/GenericGameHostingContractBoundaryTests.cs` protects generic capability projection and
  generated-client/repository boundaries.
- `tests/Api.Tests/WebApp/GameServerCreationBoundaryTests.cs` protects the creation wizard non-persistence boundary
  and no Arma-specific public marker behavior.
- `tests/Api.Tests/WebApp/ServerWorkspaceBoundaryTests.cs` protects selected-server workspace labels, no user-facing
  `Collect logs`, provider teardown cost-stop wording, diagnostics redaction, target section routing, and no `n/a`
  operational surfaces.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` protects repository action mapping, policy/teardown
  boundaries, live-evidence classification, and redaction guard behavior.
- `tests/Api.Tests/WebApp/ProvisioningRuntimeTeardownUxBoundaryTests.cs`,
  `tests/Api.Tests/WebApp/UserFacingErrorBoundaryTests.cs`, and lifecycle worker tests are useful for exact unsafe
  marker scans.

### UI Surface

- `sources/WebApp/Pages/Servers/Create.razor` and `.css` own the wizard, choice cards, validation list, and final
  submit behavior.
- `sources/WebApp/Pages/Servers/Details.razor` and `.css` own the selected-server workspace with Overview, Runtime,
  Logs, Jobs, Cloud, Configuration, Files, Ownership, Audit, and Danger tabs.
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` maps generated Kiota transport models
  into hand-written Razor-facing workspace models and ranks primary/secondary actions.
- `ServerWorkspaceModels.cs`, `LifecycleJobInfo.cs`, `CloudRuntimeStatusInfo.cs`, and
  `CloudWorkflowResultMapper.cs` are the main hand-written surfaces that browser tests should indirectly exercise.

## Planning Recommendations

### Plan Shape

Use the roadmap split:

1. Fake state catalog and test harness.
2. Browser walkthrough and responsive visual verification.
3. Redaction, boundary scans, final milestone verification, and handoff.

### Implementation Approach

- Introduce a single catalog of fake server workspace scenarios inside the UI test project. It should carry the expected
  visible labels, primary action, target section, forbidden action text, and mock/live evidence wording for every Phase
  60 state.
- Extend `GameServerUiMockBackend` through existing `MockServerState` helpers rather than creating a second fake API.
  Existing tests already depend on this backend and request-count tracking.
- Keep fake provider/runtime values visibly fake. Avoid real-looking Hetzner ids, cookies, bearer strings, real
  usernames, real project names, or secret-like tokens in fixtures and evidence.
- Add browser assertions for visible meaning, not just screenshots. Assertions should cover text, role, route, enabled
  state, action target, and request counts.
- Use Playwright screenshots only as supporting evidence for responsive/no-overlap checks. Pass/fail should still be
  deterministic through DOM assertions and image artifact existence when screenshots are saved.
- Expand source boundary tests using exact unsafe sentinel values and forbidden field names. Avoid broad scans for
  words like `authorization` because safe CORS or explanatory code may legitimately mention them.
- Final evidence should be a dedicated Phase 60 verification/handoff Markdown file that names mock/fake evidence and
  keeps v1.11 live evidence parked.

## Validation Architecture

Automated validation should combine:

- API/source tests for generic hosting capability projection, creation wizard non-persistence, repository model
  boundaries, no generated DTO binding in Razor, no raw HTTP bridge usage, and no Arma-3-only WebApp repository
  methods.
- UI mock tests for local record, local managed install, cloud planned, cloud provisioned, running, failed job,
  stuck/recovered job, log collection, cleanup needed, provider teardown ready, unknown, unavailable, unsupported,
  waiting-for-worker, stale, failed, deferred, and unavailable-until-setup-completes states.
- Browser walkthrough tests proving creation-to-workspace handoff, tab inspection, primary action meaning, logs/jobs
  troubleshooting, safe operation routing, and mock/live evidence language.
- Responsive browser checks at desktop and mobile widths that assert no visible state/action labels overlap or disappear
  and that screenshots are captured as supporting artifacts.
- Redaction scans across WebApp source, API/source tests, UI mock backend payloads, rendered DOM, screenshot/evidence
  artifacts, and final handoff Markdown for exact unsafe sentinel values and forbidden raw field names.

Recommended commands:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GenericGameHostingContractBoundaryTests|FullyQualifiedName~GameServerCreationBoundaryTests|FullyQualifiedName~ServerWorkspaceBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests"`
- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ProvisioningRuntimeTeardownUxBoundaryTests|FullyQualifiedName~UserFacingErrorBoundaryTests|FullyQualifiedName~GameServerCapabilityContractTests|FullyQualifiedName~LifecycleWorkerRegistryServiceTests"`
- `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter "FullyQualifiedName~CloudAgentUiTests"`
- `dotnet build X39.UnitedTacticalForces.slnx`
- `dotnet test X39.UnitedTacticalForces.slnx`

## Risks And Constraints

- Do not mutate live Hetzner, Steam, Arma, provider teardown, or authenticated operator evidence.
- Do not update v1.11 Phase 51-54 live evidence, Phase 30 acceptance, OPS-02 live evidence, final billable resource
  count evidence, or CR-0001 acceptance classification.
- Do not preserve stale mock payload compatibility when the current intended model differs; update fake states to the
  Phase 59 contracts.
- Do not leak provider tokens, Steam credentials, worker credentials, worker credential hashes, authorization headers,
  bearer tokens, cookies, raw payload fields, generated DTOs, Kiota transport models, or `AdditionalData` into UI
  surfaces or evidence.
- Do not let screenshot artifacts become the only pass/fail proof. Keep stable text/role/route/action assertions.

## Research Complete

Planner can proceed using this research, `60-CONTEXT.md`, `60-UI-SPEC.md`, `60-VALIDATION.md`, and
`60-PATTERNS.md`.
