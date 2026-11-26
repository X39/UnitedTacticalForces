# Phase 50: Operator Approval, Authenticated Access, And Preflight Readiness - Research

**Researched:** 2026-06-22
**Domain:** Operator live-evidence preflight, authenticated access evidence, provider readiness classification
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-01: Use `.planning/v1.7-LIVE-UAT-EVIDENCE.md` plus the existing live-preflight WebApp/API evidence rows as the source of truth, updating only safe reference values and blocker classifications.
- D-02: Record Hetzner project and token reference only; never record raw token material or secret-bearing provider output.
- D-03: Treat budget ceiling, maximum lifetime, cleanup window, and selected resource shape as required before live mutation can be classified ready.
- D-04: If any required operator field remains unavailable, preserve `blocked-pending-operator-input` with exact blocker rows instead of claiming readiness.
- D-05: Verify `/servers/cloud`, `/servers`, and required protected API surfaces for administrator and server-owner roles.
- D-06: Record only route, role/scope, timestamp, visible state summary, and sanitized screenshot or note references as browser evidence.
- D-07: Record explicit blockers per role and surface when authenticated sessions are unavailable.
- D-08: Reject cookies, authorization headers, bearer values, OAuth material, session ids, and raw browser storage entirely.
- D-09: Treat refreshed Phase 40 readiness as supporting evidence only; it is not authorization by itself.
- D-10: Require provider configuration, metadata freshness, active billing owner, valid current dry-run, template/profile usability, cost budget, policy state or approval, reconciliation state, and redaction readiness before mutation can proceed.
- D-11: When all gates pass, classify the package as `ready-for-live-execution` and leave actual provisioning to Phase 51 only.
- D-12: When gates do not pass, keep `blocked-pending-operator-input` or a concrete blocker classification with follow-up actions, without changing CR acceptance.

### the agent's Discretion
The agent may choose the safest artifact structure, helper naming, and verification approach consistent with the existing v1.10 evidence surfaces, repository facades, boundary tests, and redaction constraints.

### Deferred Ideas (OUT OF SCOPE)
Live provisioning, cloud worker runtime execution, provider teardown, final provider-side billable count `0`, and CR-0001 pass classification are deferred to later v1.11 phases and must not be completed in Phase 50.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PREP-01 | Operator can complete the live approval package with approver, timestamp, Hetzner project, token reference, resource shape, budget ceiling, maximum lifetime, cleanup window, selected game server, billing account, template/profile snapshot, dry-run plan, and policy approval identifier when required. | Existing `.planning/v1.7-LIVE-UAT-EVIDENCE.md` approval table is the durable evidence checklist; Phase 45 live-preflight rows are implementation-ready but require operator values. |
| PREP-02 | Operator can verify authenticated administrator and server-owner access to `/servers/cloud`, `/servers`, and required API surfaces without storing cookies, authorization headers, bearer tokens, OAuth material, or session secrets. | Existing Phase 45 access evidence records route/scope/outcome/timestamp only; actual authenticated sessions are operator-controlled and unavailable to autonomous execution unless supplied. |
| PREP-03 | Operator can verify provider configuration, provider metadata freshness, active billing owner, current dry-run plan, usable template/profile snapshots, cost budget, policy gate, and reconciliation state before live provider mutation. | Phase 40/45 preflight services and UI expose readiness rows; Phase 50 must classify them from actual evidence or leave explicit blockers. |
</phase_requirements>

## Summary

Phase 50 should not add broad product code. The live-preflight persistence, controller gate, repository facade, and `/servers` UI already exist from Phase 40 and Phase 45. The current problem is evidence truth: `.planning/v1.7-LIVE-UAT-EVIDENCE.md` still contains required `pending-operator-input` values, and the Phase 49 handoff states CR-0001 remains blocked until an operator supplies live approval, authenticated browser, provider, runtime, teardown, and redaction evidence.

The safe autonomous path is to complete the Phase 50 evidence pass with exact classifications. If the operator has not supplied the live approval package, authenticated sessions, provider readiness, billing owner, dry-run, policy, reconciliation, and redaction facts, the phase should update artifacts to say why live execution remains blocked. It must not infer readiness from automated tests or from the existence of Phase 40 readiness rows.

**Primary recommendation:** Use the v1.7 evidence file and Phase 49 handoff as the authoritative checklist, update only secret-safe planning evidence/classification rows, and create a Phase 50 verification artifact that either marks `ready-for-live-execution` or preserves concrete blockers.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Approval package evidence | Planning evidence artifact | WebApp/API live-preflight records | Phase 50 must classify operator evidence; existing app surfaces are supporting evidence, not the only source of truth. |
| Authenticated access evidence | Operator/browser evidence | WebApp repository/API access evidence rows | Only a real authenticated session can prove access; stored evidence must be route/scope/outcome only. |
| Provider/dry-run/policy/reconciliation readiness | API control plane and planning evidence | WebApp display | The API owns durable readiness state; Phase 50 records whether current facts exist and are fresh enough. |
| Redaction readiness | Planning evidence artifact | Source/evidence scans | No committed evidence may expose provider, Steam, worker, bearer, cookie, or raw secret-bearing material. |
| Provider mutation authorization | Later Phase 51 | API preflight gate | Phase 50 can produce readiness classification; it must not execute provider mutation. |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / ASP.NET Core | 9.0 project target | Existing API, tests, and build verification | Repository standard from project files and AGENTS.md. |
| Microsoft Fluent UI Blazor | 4.11.8 | Existing `/servers` operational UI components | Existing WebApp standard; no UI library change needed. |
| Microsoft Kiota | 1.17.2 | Generated API client boundary | Existing WebApp repository facade pattern. |
| xUnit | 2.9.3 | Existing test runner | Current API/WebApp boundary and parser tests use xUnit. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ripgrep | installed in environment | Source/evidence marker scans | Redaction and no-pass-claim checks. |
| GSD planning artifacts | current project setup | Evidence and phase lifecycle state | Phase 50 is evidence closure, not new infrastructure. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Updating `.planning/v1.7-LIVE-UAT-EVIDENCE.md` | A separate Phase 50-only evidence artifact | Would fragment the handoff source of truth and violate D-01. |
| Recording blocked evidence | Pausing until operator secrets/sessions exist | Blocking is truthful and lets the milestone classify unavailable operator inputs without fabricating a pass. |
| New UI/code changes | Existing live-preflight surfaces | Existing code already exposes the needed operator workflow; Phase 50 should only add code if execution discovers a concrete gap. |

## Package Legitimacy Audit

No external packages are installed or recommended for Phase 50.

## Architecture Patterns

### System Architecture Diagram

```text
Operator facts or unavailable prerequisites
        |
        v
.planning/v1.7-LIVE-UAT-EVIDENCE.md  <--- supporting rows from existing live-preflight UI/API
        |
        v
Phase 50 verification classification
        |
        +--> ready-for-live-execution -> Phase 51 may run exactly one approved provisioning path
        |
        +--> blocked-pending-operator-input -> Phase 51 records safe provisioning blocker, no mutation
```

### Recommended Project Structure

```text
.planning/
|-- v1.7-LIVE-UAT-EVIDENCE.md
|-- phases/
|   `-- 50-operator-approval-authenticated-access-and-preflight-readine/
|       |-- 50-CONTEXT.md
|       |-- 50-UI-SPEC.md
|       |-- 50-RESEARCH.md
|       |-- 50-VALIDATION.md
|       |-- 50-01-PLAN.md
|       |-- 50-02-PLAN.md
|       |-- 50-03-PLAN.md
|       `-- 50-VERIFICATION.md
```

### Pattern 1: Evidence-First Classification

**What:** Update the evidence artifact from actual operator facts or explicit blockers.
**When to use:** Any live UAT/preflight item that cannot be safely proven by source code or automated tests.
**Example:** `.planning/v1.7-LIVE-UAT-EVIDENCE.md` already lists `pending-operator-input` for approver, provider, access, dry-run, cost, policy, and reconciliation fields.

### Pattern 2: Existing Repository-Backed Evidence Surfaces

**What:** Treat `CloudLivePreflightService`, `GameServersController`, and `IGameServerRepository` as existing support surfaces.
**When to use:** If Phase 50 discovers a missing display/recording detail that blocks evidence capture.
**Example:** Phase 45 already verified `RecordCloudLivePreflightApprovalAsync`, `RecordCloudLivePreflightAccessEvidenceAsync`, and `RefreshCloudLivePreflightAsync`.

### Anti-Patterns to Avoid

- Claiming `ready-for-live-execution` while any required operator field is unavailable.
- Treating Phase 40 readiness as authorization by itself.
- Recording cookies, headers, bearer values, provider tokens, Steam credentials, worker secrets, worker hashes, raw browser storage, or raw provider payloads.
- Running live provisioning, runtime execution, or provider teardown in Phase 50.
- Editing generated Kiota files for evidence-only closure.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Live-preflight persistence | New evidence database model | Existing `CloudLivePreflightRecord` and `.planning/v1.7-LIVE-UAT-EVIDENCE.md` | Existing model already captures approval/access/readiness; planning artifact remains the handoff source. |
| Browser evidence capture | Session dump or raw storage copy | Route/scope/outcome/timestamp/note references | Security boundary forbids session material. |
| Provider readiness validation | Manual provider mutation probe | Existing preflight/reconciliation rows plus operator evidence | Provider calls can create cost or expose secrets if misused. |
| Final acceptance classification | Direct CR pass claim | Phase 54 acceptance matrix | Later phase owns CR-0001 classification from captured evidence. |

## Common Pitfalls

### Pitfall 1: Automated Tests Masquerading As Live Evidence
**What goes wrong:** Source/build tests pass and the phase claims live browser/provider readiness.
**Why it happens:** Existing Phase 45 UI/API surfaces are implementation-ready, but live operator evidence is absent.
**How to avoid:** Verification must separate automated guardrails from operator facts and classify missing facts as blocked or human-needed.
**Warning signs:** Phrases such as `live Hetzner passed`, `authenticated browser passed`, or `final billable count 0 verified` without operator evidence.

### Pitfall 2: Secret-Bearing Evidence Leakage
**What goes wrong:** Token values, cookies, browser storage, headers, or raw provider output are copied into planning files.
**Why it happens:** Live UAT evidence often comes from screenshots, browser devtools, provider consoles, or logs.
**How to avoid:** Store references, ids, counts, timestamps, and sanitized summaries only; run forbidden-marker scans before verification.
**Warning signs:** Terms like `Bearer`, `Authorization`, `cookie`, `ProviderTokenValue`, `SteamPassword`, `WorkerCredentialHash`, `RawPayload`.

### Pitfall 3: Runtime Cleanup Confused With Cost Stop
**What goes wrong:** A runtime stop/cleanup result is treated as provider billing stopped.
**Why it happens:** Earlier runtime actions are visible in the same server workflow.
**How to avoid:** Keep provider teardown and final billable count in Phases 53/54; Phase 50 only verifies preflight readiness.
**Warning signs:** Phase 50 evidence referencing teardown success or final provider-side count as passed.

## Code Examples

Existing evidence rows and tests already provide the implementation pattern:

- `.planning/v1.7-LIVE-UAT-EVIDENCE.md` - approval/access/provider/dry-run/readiness/redaction tables.
- `sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs` - durable approval, access, readiness, forbidden-term checks.
- `sources/Api/Controllers/GameServersController.cs` - provider provisioning guard through `ValidateReadyForProvisioningAsync`.
- `sources/WebApp/Pages/Servers/Index.razor` - `Live preflight` selected-server panel.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - repository and redaction boundary tests.
- `tests/Api.Tests/WebApp/VerificationHandoffBoundaryTests.cs` - no-pass-claim and handoff marker tests.

## Assumption Ledger

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | No authenticated administrator/server-owner sessions are available to this autonomous agent unless the user provides them. | Environment Availability | If sessions are available through a tool not surfaced here, Phase 50 could capture stronger evidence. |
| A2 | No live Hetzner project/token reference, selected server, billing account, dry-run id, policy approval, or reconciliation id was supplied in this chat. | Environment Availability | If these values exist locally but were not surfaced, Phase 50 may classify as blocked instead of ready. |

## Open Questions (RESOLVED)

1. **Should Phase 50 run provider mutation?** RESOLVED: No. D-11 leaves provisioning to Phase 51 only.
2. **Can missing operator inputs still complete Phase 50?** RESOLVED: Yes. D-04 and D-12 require explicit blocker classification rather than pass claims.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | Build/test verification if source changes occur | yes | net9.0 projects present | Use focused source scans when no source changes occur. |
| Authenticated administrator session | PREP-02 live browser evidence | no | not provided | Record administrator access blocker. |
| Authenticated server-owner session | PREP-02 live browser evidence | no | not provided | Record owner access blocker. |
| Hetzner project and token reference | PREP-01/PREP-03 provider readiness | no | not provided | Record provider approval/configuration blocker; never record raw token values. |
| Selected live server, billing account, dry-run plan, policy/reconciliation ids | PREP-01/PREP-03 selected scenario readiness | no | not provided | Record selected-scenario blocker rows. |

**Missing dependencies with no fallback:**
- Operator-supplied authenticated sessions.
- Operator-approved Hetzner project/token reference/resource shape/budget/lifetime/cleanup window.
- Selected game server, billing account, dry-run plan, policy, and reconciliation evidence.

**Missing dependencies with fallback:**
- None for pass classification. The only safe fallback is explicit blocker classification.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 plus source scans |
| Config file | `tests/Api.Tests/Api.Tests.csproj` and `X39.UnitedTacticalForces.slnx` |
| Quick run command | `rg -n "pending-operator-input|blocked-pending-operator-input|ready-for-live-execution" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine` |
| Full suite command | `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| PREP-01 | Approval package either has safe required values or exact blockers. | source/evidence scan | `rg -n "Approval status|Approver|Approved project|Budget ceiling|Maximum lifetime|Cleanup window|blocked-pending-operator-input" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine` | yes |
| PREP-02 | Authenticated admin/owner access evidence is recorded or blocked without session material. | source/evidence scan | `rg -n "Authenticated access blocker|Administrator cloud operations access|Server-owner selected server access|cookies|Authorization headers|bearer" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine` | yes |
| PREP-03 | Provider/owner/dry-run/template/cost/policy/reconciliation readiness is recorded or blocked before mutation. | source/evidence scan | `rg -n "Provider readiness blocker|Dry-run readiness blocker|Policy|Reconciliation|Redaction Review|Phase 40" .planning/v1.7-LIVE-UAT-EVIDENCE.md .planning/phases/50-operator-approval-authenticated-access-and-preflight-readine` | yes |

### Sampling Rate
- **Per task commit:** Run the relevant `rg` scan for that plan's evidence section.
- **Per wave merge:** Run all source/evidence scans in `50-VALIDATION.md`.
- **Phase gate:** Run final no-pass-claim and forbidden-marker scans before writing `50-VERIFICATION.md`.

### Wave 0 Gaps
- None. Existing planning artifact and source scan infrastructure cover Phase 50.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes | Evidence records role/scope/outcome only; actual auth remains ASP.NET/API-controlled. |
| V3 Session Management | yes | Session material is forbidden in evidence. |
| V4 Access Control | yes | API/controller authorization remains authoritative for protected surfaces. |
| V5 Input Validation | yes | Existing preflight service rejects forbidden terms; evidence scans catch planning leaks. |
| V6 Cryptography | no | No cryptographic implementation changes. |

### Known Threat Patterns for Phase 50

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secret leakage through evidence artifacts | Information Disclosure | Allowlisted evidence fields and forbidden-marker scans. |
| False live readiness claim | Tampering / Repudiation | Explicit classification table and no-pass-claim scans. |
| Unauthorized provider mutation | Elevation of Privilege | No provider mutation tasks in Phase 50; API preflight gate remains authoritative. |

## Sources

### Primary (HIGH confidence)
- `.planning/ROADMAP.md` - Phase 50 scope, success criteria, and boundaries.
- `.planning/REQUIREMENTS.md` - PREP-01 through PREP-03.
- `.planning/v1.7-LIVE-UAT-EVIDENCE.md` - authoritative live UAT evidence artifact.
- `.planning/milestones/v1.10-phases/49-cr-acceptance-classification-and-handoff/49-HANDOFF.md` - final blocker handoff.
- `sources/Api/Services/CloudPreflight/CloudLivePreflightService.cs` - existing preflight persistence and redaction service.
- `sources/Api/Controllers/GameServersController.cs` - existing provider mutation gate.
- `sources/WebApp/Pages/Servers/Index.razor` - existing `/servers` live-preflight UI.
- `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` and `VerificationHandoffBoundaryTests.cs` - existing boundary/no-pass-claim test patterns.

### Secondary (MEDIUM confidence)
- `.planning/codebase/*.md` - generated codebase maps from earlier GSD analysis.
- `.planning/milestones/v1.10-phases/45-*` through `49-*` - archived implementation and evidence classification context.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - project files and AGENTS.md define .NET 9, Blazor, Fluent UI, Kiota, xUnit.
- Architecture: HIGH - Phase 40/45/49 artifacts and source files define the existing evidence and preflight gates.
- Pitfalls: HIGH - risks are explicitly documented in v1.7 evidence and v1.10 handoff.

**Research date:** 2026-06-22
**Valid until:** 2026-07-22 for artifact structure; operator evidence availability must be rechecked every run.
