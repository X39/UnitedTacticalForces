---
phase: 20
slug: labeling-safety-and-verification-hardening
status: draft
created: 2026-06-18
---

# Phase 20 - Context

## Goal

Improve future Hetzner reconciliation with safe UTF-owned provider labels and close v1.4 with focused verification
around secret redaction, cost math, provider failures, authorization-sensitive boundaries, and read-only reconciliation.

## Requirements

| ID | Requirement | Phase Scope |
|----|-------------|-------------|
| HETZ-12 | Newly provisioned Hetzner resources carry UTF-owned labels or equivalent safe metadata that improves future reconciliation without exposing credentials. | Covered by create-payload labels for server, floating IP, volume, and firewall resources. |
| RELY-15 | Provider tokens, Steam credentials, worker credentials, bootstrap credentials, bearer headers, and secret-bearing user data are not exposed through cost snapshots, reconciliation findings, audits, DTOs, generated clients, or logs. | Covered by redaction tests, DTO/static scans, and provider-boundary label filtering. |
| RELY-16 | Tests cover cost math, deletion-as-cost-stop behavior, ownership transfer attribution, provider list pagination/failures, reconciliation finding types, authorization boundaries, and secret redaction. | Covered by existing tests plus explicit coverage matrix and gap-closing guard tests. |

## Explicit Non-Goals

- No automatic deletion of unknown provider resources during reconciliation.
- No provider invoice import, invoice-grade actual cost, adjusted-cost entry, or spending policy.
- No live Hetzner UAT claim without operator-provided token, budget, approval, cleanup window, and final cleanup evidence.
- No authenticated browser UAT claim without operator-provided admin/owner sessions and representative cloud data.
- No schema changes unless label metadata needs to be persisted locally; the first choice is provider create-payload labels.

## Existing Local Facts

- `HetznerCloudClient` already maps safe labels from live servers, floating IPs, volumes, and firewalls while filtering
  sensitive label keys and values.
- `HetznerCloudProvisioningModels` currently has create records for server, floating IP, volume, and firewall requests,
  but those records do not yet carry labels.
- `CloudProvisioningService` already has the correlation facts needed for labels: session id, game server id, billing
  account id, billing assignment id, inventory record id, provider identifier, and resource kind.
- Cost snapshot tests already cover billable resource selection, provider deletion as the cost-stop timestamp, runtime
  stop not ending cost, freshness/confidence, and owner attribution snapshots.
- Reconciliation tests already cover run persistence, finding types, provider read failure sanitization, and fake client
  methods that throw on create/delete/detach.
- Build and API test execution are currently blocked by NuGet socket-denied `NU1301`; verification must distinguish
  restore/network failures from code/test failures.

## Design Boundaries

- Labels must use only UTF-owned names and non-secret scalar values.
- Labels must not include usernames, provider tokens, Steam credentials, bootstrap credentials, bearer headers, raw
  user-data, or free-form operator input.
- Reconciliation remains read-only: it may list provider resources and persist run/finding records, but must not invoke
  provider create, delete, detach, attach, power, or firewall mutation methods.
- Cost output remains estimated/reconciled/adjusted in naming and copy; v1.4 does not claim invoice-grade actual cost.
- Verification artifacts must state that OPS-02 remains operator-gated until live credentials, budget, approval, and
  cleanup proof exist.

## Plan Split

1. `20-01` adds safe UTF provider labels to Hetzner create requests and provisioning tests.
2. `20-02` adds explicit redaction and read-only safety guards.
3. `20-03` records the final coverage matrix, operator-gated UAT stance, and v1.4 closeout verification.
