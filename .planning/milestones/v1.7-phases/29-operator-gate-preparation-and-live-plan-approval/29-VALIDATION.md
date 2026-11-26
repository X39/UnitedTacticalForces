---
phase: 29
slug: operator-gate-preparation-and-live-plan-approval
status: draft
nyquist_compliant: false
wave_0_complete: true
created: 2026-06-18
---

# Phase 29 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | PowerShell file and content checks |
| **Config file** | none |
| **Quick run command** | `Test-Path .planning/v1.7-LIVE-UAT-EVIDENCE.md` |
| **Full suite command** | `Select-String -Path .planning/v1.7-LIVE-UAT-EVIDENCE.md -Pattern "Approval Record","Forbidden Evidence Content","Final Classification"` |
| **Estimated runtime** | < 5 seconds |

## Sampling Rate

- **After every task commit:** Run the relevant file existence or heading check for the edited evidence artifact.
- **After every plan wave:** Run the full content check against `.planning/v1.7-LIVE-UAT-EVIDENCE.md`.
- **Before verification:** Confirm operator-dependent fields are either filled with safe values or explicitly marked `pending-operator-input`.
- **Max feedback latency:** < 5 seconds for automated checks.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 29-01-01 | 01 | 1 | OPS-03 | T-29-01 | Evidence artifact excludes raw secrets and includes approval fields. | file-content | `Select-String -Path .planning/v1.7-LIVE-UAT-EVIDENCE.md -Pattern "Approval Record","Token reference","Forbidden Evidence Content"` | pending | pending |
| 29-01-02 | 01 | 1 | OPS-03 | T-29-02 | Runbook points operators to v1.7 evidence before provider mutation. | file-content | `Select-String -Path docs/operations/hetzner-live-uat.md -Pattern "v1.7-LIVE-UAT-EVIDENCE"` | pending | pending |
| 29-02-01 | 02 | 2 | OPS-03 | T-29-03 | Auth evidence records roles and URLs, not cookies or headers. | manual | Manual browser review | pending | pending |
| 29-03-01 | 03 | 3 | OPS-03 | T-29-04 | Provider readiness records token reference, metadata timestamp, owner, plan, cost, and approval status. | manual | Manual API/WebApp/provider review | pending | pending |

## Wave 0 Requirements

Existing PowerShell and planning-file checks cover this documentation phase. No new test framework is required.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Authenticated administrator and server-owner access | OPS-03 | Requires real Steam/Discord sessions. | Sign in as each role and record safe URLs, visible state, and role name without cookies, headers, or bearer material. |
| Provider configuration and metadata freshness | OPS-03 | Requires operator-controlled provider token reference and current metadata. | Verify provider status and metadata sync in the API/WebApp, recording token reference and timestamps only. |
| Approval record completion | OPS-03 | Requires explicit human approval for billable resources. | Fill approver, project, shape, budget, lifetime, cleanup window, game server, billing account, and dry-run plan IDs before Phase 30. |

## Validation Sign-Off

- [ ] All tasks have file-content or manual verification.
- [ ] Manual-only items are explicit and cannot be replaced by autonomous evidence.
- [ ] Evidence artifact records pending fields instead of fabricating values.
- [ ] No raw provider, Steam, worker, auth header, bearer token, or cookie material is stored.

**Approval:** pending
