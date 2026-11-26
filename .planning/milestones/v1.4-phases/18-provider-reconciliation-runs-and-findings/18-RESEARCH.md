---
phase: 18
slug: provider-reconciliation-runs-and-findings
status: draft
created: 2026-06-18
---

# Phase 18 - Research

## Provider API Surface

The existing Hetzner client already uses these local conventions:

- GET metadata calls use `GetPagedAsync<TResponse, TModel>` with `PageSize = 50`.
- Paged responses expose `Meta.Pagination.NextPage`.
- Request creation centralizes bearer-token handling through `BuildGetRequest`.
- Failure details pass through `LifecycleFailureReasonSanitizer`.
- Existing mutation methods are explicit methods such as create, delete, and detach; reconciliation must not call them.

Official reference used for endpoint naming: https://docs.hetzner.cloud/reference/cloud

The implementation can rely on the same endpoint shapes already used by the local client:

- `servers`
- `floating_ips`
- `volumes`
- `firewalls`

## Live Resource Model Needs

Reconciliation needs a bounded normalized resource record rather than raw provider JSON:

- Provider identifier: always `hetzner`.
- Provider resource id: string form of Hetzner numeric id.
- Resource kind: contract `ECloudResourceKind`.
- Display name or description: safe text only.
- Provider status/state: safe provider state string.
- Created timestamp: optional, when provider returns it.
- Attached server id: optional for IPs, volumes, and firewall applications.
- Safe labels: optional dictionary copied only as plain provider labels, never token/user-data/config payloads.
- Detail fields such as public IP or device path may be useful, but must remain bounded and secret-safe.

## Comparison Rules

Candidate inventory records:

- `ProviderIdentifier == HetznerProviderConfiguration.HetznerProviderIdentifier`.
- `ProviderResourceId` is not null or empty for provider-backed records.
- `TimeStampDeleted` is null for current provider existence checks unless specifically checking deleted inventory against live resources.

Finding categories:

- Unknown provider resource: live provider resource has no matching inventory record.
- Missing provider resource: active inventory record has provider id/kind/resource id but the provider list does not contain it.
- State mismatch: provider state and inventory state disagree materially, including inventory deleted while provider resource is still live.
- Cleanup-needed billable resource: provider-backed server, IP, or volume remains live while inventory state is `CleanupNeeded` or `OrphanCandidate`, or a live unknown billable resource is found.

Billable resource kinds follow Phase 17 cost rules:

- Server, IP address, and volume are billable while they exist.
- Firewall and session records are not cost-contribution resources.
- Provider deletion, not runtime stop or powered-off runtime state, remains the cost-stop boundary.

## Failure Handling

- Provider list failure should persist a failed run with sanitized category/summary details.
- Partial provider data should not produce completed findings unless all required resource lists finish successfully.
- Audit details should include run id, provider, actor id, live counts, inventory counts, finding counts, and a sanitized failure category/summary if failed.

## Authorization Boundary

Phase 18 exposes an administrator trigger so HETZ-08 is real, but keeps broader UI visibility for Phase 19.

- Add endpoints to the existing cloud inventory admin surface or a sibling cloud reconciliation controller.
- Require the same server-administration policy used by current cloud inventory admin endpoints.
- Controller tests should check authorization attributes by reflection, matching existing `CloudInventoryController_AdminEndpointsRequireServerClaim` style.

