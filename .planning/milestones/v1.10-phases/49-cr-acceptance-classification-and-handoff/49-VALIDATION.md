# Phase 49 Validation Plan

## Requirement Coverage

| Requirement | Automated Evidence | Human Boundary |
|-------------|--------------------|----------------|
| ACC-01 | Acceptance matrix and handoff artifacts map CR-0001, Phase 30, OPS/EVID, and v1.10 requirements to captured evidence, blockers, and follow-up actions. Tests guard required markers and false pass claims. | Operator must supply or approve live provider, runtime, cleanup, final billable-resource, authenticated browser, and full redaction evidence before CR acceptance can pass. |

## Automated Verification

Run after executable slices:

- `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~VerificationHandoffBoundaryTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --no-restore --verbosity minimal`
- `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`

If `--no-restore` fails because restore has not been run in the current workspace, run the same command without `--no-restore` and record that in the summary.

## Source Checks

- `rg -n "CR-0001 Acceptance Matrix|Phase 30 classification|blocked-pending-operator-input|No CR acceptance pass is claimed|Provider teardown remains the cost-stop boundary" .planning/phases/49-cr-acceptance-classification-and-handoff tests/Api.Tests/WebApp/VerificationHandoffBoundaryTests.cs`
- `rg -n "Phase 30 live UAT passed|live Hetzner UAT passed|live Steam UAT passed|live Arma UAT passed|authenticated browser UAT passed|provider mutation verified live|final provider-side billable resource count 0 verified|live UAT complete|Current classification: \`ready-for-live-execution\`|CR-0001 accepted|CR acceptance passed" .planning/phases/49-cr-acceptance-classification-and-handoff/49-CR-ACCEPTANCE-MATRIX.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-HANDOFF.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-VERIFICATION.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-01-SUMMARY.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-02-SUMMARY.md`
- `rg -n "AuthorizationHeader|BearerToken|SteamPassword|SteamApiKey|ProviderToken|ProviderTokenValue|WorkerCredential|WorkerCredentialHash|RawJson|RawPayload|ExceptionBody|ResponseHeaders" .planning/phases/49-cr-acceptance-classification-and-handoff/49-CR-ACCEPTANCE-MATRIX.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-HANDOFF.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-VERIFICATION.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-01-SUMMARY.md .planning/phases/49-cr-acceptance-classification-and-handoff/49-02-SUMMARY.md tests/Api.Tests/WebApp/VerificationHandoffBoundaryTests.cs`

## Human Verification Boundary

Classify these as `human_needed` or `blocked` unless supplied during execution:

- Authenticated administrator and server-owner browser walkthrough evidence.
- Actual live Hetzner provisioning evidence.
- Actual cloud worker bootstrap and Steam/Arma runtime evidence.
- Actual provider teardown and manual cleanup fallback evidence.
- Actual provider-side final billable resource count `0`.
- Full evidence-package redaction review.

Do not mark Phase 30, OPS-02, OPS-04, EVID-01, EVID-02, EVID-03, or CR-0001 final acceptance as passed from automated checks alone.
