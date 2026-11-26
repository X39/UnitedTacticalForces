---
phase: 39-verification-and-phase-30-handoff
artifact: phase-30-handoff
status: blocked-pending-operator-input
created: 2026-06-21
source_template: .planning/v1.7-LIVE-UAT-EVIDENCE.md
---

# Phase 30 Live UAT Handoff

This handoff preserves the remaining live evidence path after v1.8 implementation. It does not authorize live provider
mutation and does not mark Phase 30 as passed.

## Required Operator Prerequisites

| Prerequisite | Required Evidence | Status |
|--------------|-------------------|--------|
| approval | Named operator approval for the exact project, token reference, game server, resource shape, maximum lifetime, and cleanup plan. | pending-operator-input |
| budget | Budget ceiling and maximum lifetime for the live test resources. | pending-operator-input |
| cleanup window | Time window and responsible operator for teardown plus manual cleanup fallback. | pending-operator-input |
| authenticated sessions | Administrator and server-scoped user browser sessions for `/servers/cloud` and `/servers`. | pending-operator-input |
| provider readiness | Hetzner provider enabled, verified, metadata synchronized, and token reference available server-side without exposing token values. | pending-operator-input |
| Steam environment | SteamCMD/DepotDownloader/native Arma runtime environment ready without exposing Steam credentials. | pending-operator-input |
| billing owner | Selected game server has exactly one active billing owner assignment and matching billing account. | pending-operator-input |
| dry-run plan | Valid reviewed dry-run plan for the selected server, owner assignment, provider metadata, and approved resource shape. | pending-operator-input |
| cost evidence | Current hourly/monthly estimate, policy status, reconciliation status, and cleanup risk recorded before live mutation. | pending-operator-input |
| final provider-side billable resource count `0` | After teardown, external provider-side count for billable resources is verified as zero. | pending-phase-30 |

## Required Phase 30 Evidence Sections

| Evidence Section | Required Contents | Status |
|------------------|-------------------|--------|
| Approval Record | Approver, timestamp, project, token reference, resource shape, budget, maximum lifetime, cleanup window, game server id, billing account id, dry-run plan id, policy approval request id if required. | pending-operator-input |
| Environment And Authenticated Access | Aspire/API/WebApp URLs, admin access, server-owner access, no cookies or auth headers recorded. | pending-operator-input |
| Provider And Metadata Preflight | Provider enabled/verified, token reference only, metadata timestamp/freshness, dry-run metadata alignment. | pending-operator-input |
| Game Server Billing Owner And Dry-Run Plan | Active owner assignment, valid dry-run plan, resource shape, cost estimate, policy status, reconciliation status. | pending-operator-input |
| Live Execution Evidence | Cloud session id, provider resource ids, endpoint, audit records, failure evidence if any. | pending-phase-30 |
| Runtime Evidence | Worker registration/heartbeat/capabilities and Steam/Arma install/configure/start/stop/log evidence. | pending-phase-30 |
| Cleanup Evidence | Teardown result, manual cleanup if any, provider-side server/floating IP/volume/firewall counts, final provider-side billable resource count `0`. | pending-phase-30 |
| Redaction Review | Evidence checked for tokens, cookies, credentials, auth headers, bearer values, provider secret payloads, bootstrap credentials, worker hashes, and Steam secrets. | pending-phase-30 |

## Stop Conditions

Do not run live provisioning, provider teardown, Steam/Arma runtime UAT, or provider-side cleanup verification until:

- Required operator prerequisites have no `pending-operator-input` values.
- Provider and metadata readiness is recorded using token references only.
- The selected server has a valid owner, valid dry-run plan, current cost evidence, policy status, and reconciliation status.
- A cleanup window and manual fallback owner are recorded.
- Evidence redaction rules are accepted before screenshots/logs are stored.

## Deferred Classification

Current classification: `blocked-pending-operator-input`.

Phase 30 can move to `ready-for-live-execution` only in a separate operator-controlled evidence pass. v1.8 implementation
completion does not change this classification.

