# Phase 47 Validation Plan

## Requirement Coverage

| Requirement | Automated Evidence | Human Boundary |
|-------------|--------------------|----------------|
| RUNT-01 | UI/source tests prove worker bootstrap, worker registration, heartbeat, capability/action labels, lifecycle job, progress, recent log, failure, and blocker evidence rows exist. Existing worker tests prove worker bootstrap and Arma lifecycle executors handle credentials/log redaction in isolated tests. | Actual live worker registration, heartbeat, Steam/Arma install/configure/start/stop/log execution, and environment blocker classification require a provisioned live session or operator evidence. |
| RUNT-02 | UI/source tests prove authenticated visibility labels and runtime context rows exist for admin/server-owner views with cost, policy, reconciliation, owner, template/profile, audit/log context, cleanup separation, and redaction checks. | Actual authenticated browser screenshots/API observations require real sessions or operator-supplied evidence. |

## Automated Verification

Run after executable slices:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests|FullyQualifiedName~CloudWorker|FullyQualifiedName~CloudWorkerBootstrap" --no-restore --verbosity minimal`
- `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

If `--no-restore` fails because restore has not been run in the current workspace, run the same command without `--no-restore` and record that in the summary.

## Source Checks

- `rg -n "GetCloudRuntimeEvidenceRows|Worker bootstrap|Worker registration|Worker heartbeat|Worker capabilities|Install/configure|Runtime start|Runtime stop|Runtime logs|Runtime blocker|Redaction check" sources/WebApp/Pages/Servers/Index.razor tests/Api.Tests/WebApp`
- `rg -n "QueueCloudLifecycleJobAsync|RunCloudLifecycleActionAsync|CleanupCloudRuntimeResultAsync|CanRunCloudRuntimeAction|Collect logs|Cleanup runtime" sources/WebApp/Pages/Servers/Index.razor sources/WebApp/Services/GameServerRepository tests/Api.Tests/WebApp`
- `rg -n "AuthorizationHeader|BearerToken|SteamPassword|SteamApiKey|ProviderToken|ProviderTokenValue|WorkerCredential|WorkerCredentialHash|RawJson|RawPayload|ExceptionBody|ResponseHeaders" sources/WebApp/Pages/Servers/Index.razor tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs .planning/phases/47-live-runtime-and-visibility-evidence`

## Human Verification Boundary

Classify these as `human_needed` unless supplied during execution:

- Actual provisioned live cloud session and endpoint.
- Actual worker bootstrap consumption, registration, heartbeat freshness, and capabilities for that session.
- Actual Steam/Arma install, configure, start, stop, and log collection.
- Actual authenticated administrator view and authenticated server-owner view.
- Actual runtime log evidence, screenshots, and exported artifacts after redaction review.

Do not mark live runtime, provider teardown, final billable-resource closure, Phase 30, or CR acceptance as passed from automated tests alone.
