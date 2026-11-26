# Phase 45 Research: Approval, Authenticated Access, And Live Preflight Evidence

<user_constraints>

## User And Milestone Constraints

- Phase 45 must prove live provider mutation is approved, budgeted, authenticated, and safe to attempt.
- Phase 45 must not mutate provider resources.
- Phase 45 owns requirements `LIVE-01`, `LIVE-02`, and `LIVE-03`.
- Approval/access/provider evidence must not store cookies, authorization headers, bearer tokens, OAuth material, provider token values, Steam credentials, worker secrets, or raw secret-bearing browser/provider material.
- The existing game-server and cloud-workflow abstractions stay in place.
- Existing local Aspire/PostgreSQL/API/WebApp development must keep working.
- Discuss was skipped by `workflow.skip_discuss`; implementation details are at agent discretion and must follow ROADMAP, REQUIREMENTS, UI-SPEC, and codebase conventions.

</user_constraints>

## Phase Requirements

| Requirement | Phase 45 Interpretation |
|-------------|-------------------------|
| LIVE-01 | Operator can record a fresh approval package with approver, timestamp, provider project, provider token reference, resource shape, budget ceiling, maximum lifetime, cleanup window, selected game server, billing account, template/profile snapshot, dry-run plan, and policy approval id when required. |
| LIVE-02 | Operator can verify authenticated administrator and server-owner access to `/servers/cloud`, `/servers`, and required API surfaces without storing browser/session secret material. |
| LIVE-03 | Operator can verify provider configuration, provider metadata freshness, exactly one active billing owner, current valid dry-run plan, usable template/profile snapshots, cost within budget, resolved policy gates, and reconciliation with no unknown billable blockers before mutation. |

## Summary

Phase 40 already implemented the durable API/service/backend guardrail for cloud live preflight records. Phase 45 should not rebuild that service. The remaining work is to make the parked `/servers` preflight panel an operator workflow that can record approval and access evidence, refresh readiness, display the selected scenario details, and extend boundary tests so the UI cannot expose secret-bearing fields or bypass the repository-safe preflight facade.

No external package, provider SDK, or registry change is needed.

## Findings

| ID | Finding | Evidence | Impact |
|----|---------|----------|--------|
| R-45-01 | `CloudLivePreflightService` already persists approval packages, access evidence, readiness records, audit rows, and forbidden-term checks. | `sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs` | Reuse the service through existing WebApp repository methods; avoid a duplicate evidence model. |
| R-45-02 | Live provisioning already calls `ValidateReadyForProvisioningAsync` before spending-policy gate evaluation/provider mutation when the preflight service is available. | `sources/Api/Controllers/GameServersController.cs` | UI work can rely on API enforcement; Phase 45 must keep provisioning unavailable until refreshed readiness is verified. |
| R-45-03 | The WebApp repository already exposes `GetCloudLivePreflightAsync`, `RecordCloudLivePreflightApprovalAsync`, `RecordCloudLivePreflightAccessEvidenceAsync`, and `RefreshCloudLivePreflightAsync`. | `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`, `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs` | Implement UI actions through the facade, not generated clients or raw HTTP. |
| R-45-04 | The `/servers` page already has a `Live preflight` section but the approval button is hard-disabled and no access-evidence recording actions exist. | `sources/WebApp/Pages/Servers/Index.razor` | Phase 45 can be a focused UI/state/test change rather than a new page. |
| R-45-05 | Dry-run review data already includes selected/active owner ids, provider identifier, plan id, hourly cost/currency, metadata timestamp/freshness, planned resources, and template/profile selection context. | `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudDryRunPlanReviewModels.cs` | Approval payload can be prefilled from the current reviewed dry-run and active owner. |
| R-45-06 | Existing boundary tests inspect WebApp source for repository-safe usage, exact action labels, and forbidden field exposure. | `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` | Extend these tests to lock the new Phase 45 operator actions and redaction boundaries. |
| R-45-07 | Focused API tests already cover preflight persistence, routes, forbidden session material rejection, readiness blockers, and provisioning-block behavior. | `tests/Api.Tests/CloudPreflight/*.cs` | Prefer adding WebApp boundary tests unless execution uncovers an API behavior gap. |
| R-45-08 | Installed SDKs include .NET 9.0.100, 9.0.200, and 9.0.308; installed runtimes include Microsoft.NETCore.App and Microsoft.AspNetCore.App 9.0.x. | `dotnet --list-sdks`, `dotnet --list-runtimes` | Verification can run the repo's net9.0 build/test commands locally. |
| R-45-09 | No `.codex/skills/` or `.agents/skills/` project-local skill directory is present. | `Get-ChildItem .codex/skills,.agents/skills` | No additional project-local skill rules apply. |

## Architecture Direction

1. Keep the implementation inside the existing `/servers` selected-server details workflow and `IGameServerRepository`.
2. Do not edit `sources/WebApp/UtfApi/**`; repository-local `CloudLivePreflightCommandSender` and transport models already provide the generated-client boundary.
3. Build approval payloads from selected server, active billing owner, current dry-run review, current policy gate/approval context, and operator-entered approval fields.
4. Record authenticated administrator and owner access as route/scope/outcome/note/timestamp only. Never capture or persist cookies, bearer values, OAuth material, headers, screenshots, or raw browser evidence.
5. Use readiness refresh as the Phase 40 evidence update. Treat `OverallReadiness.Verified` as necessary but not sufficient by itself for provider mutation; approval/access evidence and API guardrails remain authoritative.
6. Use source-boundary tests for UI/repository redaction and targeted `CloudLivePreflight` tests for backend regressions.

## UI Direction

The approved UI contract is `45-UI-SPEC.md`. Use Microsoft Fluent UI Blazor and the existing dense operational `/servers` style. Required visible labels include `Record preflight approval`, `Refresh preflight evidence`, `Approval package`, `Authenticated admin`, `Authenticated owner`, `Provider configuration`, `Provider metadata`, `Billing owner`, `Dry-run plan`, `Template/profile`, `Cost budget`, `Policy state`, `Reconciliation state`, `Phase 40 readiness`, and `Redaction check`.

The Phase 45 panel should not be a landing page and should not add provider mutation actions. Provisioning buttons remain governed by the existing policy/preflight gates.

## Validation Architecture

Validation should combine automated tests and source checks:

- API/service regression tests: `CloudLivePreflightRecordTests`, `CloudLivePreflightApiTests`, and `CloudLivePreflightReadinessTests`.
- WebApp source-boundary tests: `CloudWorkflowRepositoryBoundaryTests`.
- Build checks: `sources/WebApp/WebApp.csproj`, `tests/Api.Tests/Api.Tests.csproj`, and `X39.UnitedTacticalForces.slnx`.
- Source redaction scans over `/servers`, preflight repository support files, Phase 45 summaries, and verification artifacts.

If actual operator/browser/provider credentials or live sessions are unavailable, verification must classify those portions as `human_needed` or blocked. Automated tests cannot truthfully prove live operator approval or authenticated browser access.

## Security Domain

| Boundary | Risk | Direction |
|----------|------|-----------|
| Operator-entered approval fields to API | Secret-bearing token values could be typed into safe fields. | UI labels must request provider access reference/secret key name only; backend forbidden-term checks remain authoritative. |
| Browser access evidence to API | Session material could be copied into note/outcome fields. | Use fixed safe route/scope/outcome defaults and optional bounded note; tests scan for forbidden field names and terms. |
| WebApp display to operator | Backend DTO contains a token reference but display surfaces must not expose provider token values or raw payloads. | Show safe ids, budget, cleanup, status, summary, blockers, and template/profile names only. |
| Readiness to provisioning | UI could enable provisioning before readiness refresh. | Keep API preflight validation; plan 03 verifies UI messaging and boundary tests, but API remains final gate. |
| Package supply chain | New dependencies can expand attack surface. | No package install planned. |

## Sources

- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/phases/45-approval-authenticated-access-and-live-preflight-evidence/45-CONTEXT.md`
- `.planning/phases/45-approval-authenticated-access-and-live-preflight-evidence/45-UI-SPEC.md`
- `sources/Api/Controllers/GameServersController.cs`
- `sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs`
- `sources/WebApp/Pages/Servers/Index.razor`
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudLivePreflightModels.cs`
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudDryRunPlanReviewModels.cs`
- `tests/Api.Tests/CloudPreflight/*.cs`
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs`

