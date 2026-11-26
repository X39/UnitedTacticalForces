---
phase: 20
slug: labeling-safety-and-verification-hardening
status: draft
created: 2026-06-18
---

# Phase 20 - Research

## Codebase Findings

| Area | Finding | Implication |
|------|---------|-------------|
| Provider create models | `HetznerCloudFirewallCreateRequest`, `HetznerCloudServerCreateRequest`, `HetznerCloudFloatingIpCreateRequest`, and `HetznerCloudVolumeCreateRequest` lack label payloads. | HETZ-12 needs model and client request-body changes. |
| Provider live reads | `HetznerCloudClient` maps safe labels from live resource JSON through `MapSafeLabels`. | Provider-side labels added now will be useful to later reconciliation without persisting secrets. |
| Provisioning context | `CloudProvisioningService` has session, plan, resource, game server, billing account, and billing assignment ids at every create call. | Labels can be deterministic and avoid free-form or secret data. |
| Secret handling | `LifecycleFailureReasonSanitizer` is already used in provisioning, teardown, and reconciliation failure paths. | Tests should assert new label and failure paths continue using sanitized output. |
| Read-only reconciliation | `CloudProviderReconciliationService` only calls `GetServersAsync`, `GetFloatingIpsAsync`, `GetVolumesAsync`, and `GetFirewallsAsync`. | Add explicit guard tests so this stays true as the provider client grows. |
| Cost tests | Existing cost tests cover billable kinds, deletion timestamp cost stop, runtime-stop non-stop, source/freshness/confidence, and owner attribution snapshots. | RELY-16 can close with a coverage matrix plus any missing redaction/read-only tests. |

## Safe Label Shape

Use labels that are UTF-owned, provider-safe, stable enough for support/reconciliation, free of credentials/profile
values/raw input, and small enough for provider label limits.

| Key | Value |
|-----|-------|
| `utf.managed` | `true` |
| `utf.project` | `united-tactical-forces` |
| `utf.provider` | `hetzner` |
| `utf.session` | cloud session primary key |
| `utf.game-server` | game server primary key |
| `utf.billing-account` | billing account primary key |
| `utf.billing-assignment` | billing assignment primary key |
| `utf.inventory-record` | inventory record primary key |
| `utf.resource-kind` | resource kind enum value |

## Verification Constraints

- API/WebApp build and test commands may fail before compile with `NU1301` due denied `api.nuget.org:443` socket access.
- Parser-only tests are available as an environment sanity check but do not validate API changes.
- Static checks with `rg`, `git diff --check`, and targeted source scans remain useful when restore is unavailable.
- Live OPS-02 UAT cannot be claimed in this environment.
