---
phase: 18
slug: provider-reconciliation-runs-and-findings
status: draft
created: 2026-06-18
---

# Phase 18 - Context

## Goal

Add read-only Hetzner reconciliation that compares live provider resources against persisted inventory and records durable findings without deleting provider resources automatically.

## Requirements

| ID | Requirement | Phase Scope |
|----|-------------|-------------|
| HETZ-08 | Administrator can reconcile known inventory records against real Hetzner resources and identify orphans. | Covered by an admin-only API trigger backed by a durable service. |
| HETZ-09 | The Hetzner provider boundary can read live servers, floating IPs, volumes, and firewalls through paged, secret-safe provider calls. | Covered by provider list methods and fake HTTP tests. |
| HETZ-10 | Reconciliation runs are persisted with status, start/end timestamps, provider resource counts, finding counts, and sanitized failure details. | Covered by run entity, migration, service status transitions, and failure tests. |
| HETZ-11 | Reconciliation findings identify missing provider resources, unknown provider resources, state mismatches, and cleanup-needed billable resources without deleting provider resources automatically. | Covered by finding entity, comparison logic, and no-mutation tests. |

## Explicit Non-Goals

- No automatic provider delete, detach, power action, firewall mutation, or cleanup execution.
- No Hetzner labels are added to newly provisioned resources in this phase; that is HETZ-12 in Phase 20.
- No WebApp reconciliation UI or generated WebApp repository facade; Phase 19 owns cost and reconciliation visibility surfaces.
- No invoice-grade actual cost. Reconciliation is about resource existence and state, not provider billing totals.
- No live Hetzner UAT is claimed without operator approval, project token, budget, and cleanup window.

## Existing Local Facts

- `IHetznerCloudClient` currently has metadata read methods and provisioning/teardown mutation methods, but no live list methods for existing provider resources.
- `HetznerCloudClient` already has a paged GET helper, bearer-token request creation, JSON send helpers, and sanitized failure handling.
- Inventory records are persisted in `CloudResourceInventoryRecord` with provider id, provider resource id, kind, state, session, game server, billing account, and billing assignment references.
- `CloudInventoryService` already treats `CleanupNeeded` and `OrphanCandidate` as teardown-relevant states and appends hosting audit records for inventory transitions.
- `CloudRuntimeCostSnapshotService` already records cost assumptions and audit details without storing credentials.
- Current tests use fake HTTP handlers and SQLite-backed EF contexts for API service coverage.

## Design Boundaries

- The provider list boundary must return normalized, secret-safe live-resource models. Raw provider payloads and bearer headers must never be persisted.
- The reconciliation service must compare live resources to inventory records by `(ProviderIdentifier, ResourceKind, ProviderResourceId)` and preserve known UTF attribution only when it can be proven from inventory.
- Unknown provider resources must be recorded as safe orphan findings with nullable session, game server, billing, and owner references.
- Failed runs must persist as failed with sanitized provider failure details and without deleting partial run history.
- Runs and findings are immutable history records; a later run may produce the same finding again, but one run must not duplicate equivalent findings internally.

