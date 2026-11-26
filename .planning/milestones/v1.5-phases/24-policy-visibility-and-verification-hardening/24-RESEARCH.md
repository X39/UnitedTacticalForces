# Phase 24 Research: Policy Visibility And Verification Hardening

## Existing Behavior

Policy configuration and dry evaluation endpoints are grouped under `cloud-spending-policies`. The controller already returns individual DTOs for policies, decisions through dry evaluations, approval requests, and automatic action attempts. Approval and automatic action services provide filtered retrieval by status.

Cloud cost visibility is already composed from session status, latest cost summaries, and reconciliation findings. The admin projection lives under `cloud-inventory/cost-visibility`; server-scoped projection lives under `game-servers/{gameServerId}/cloud-cost-visibility`.

The WebApp uses Kiota-generated request builders under `sources/WebApp/UtfApi` and wraps them with repository models in `sources/WebApp/Services`. The admin cloud page and selected-server details already consume generated cloud inventory/game-server endpoints and should be extended instead of adding browser-side HTTP calls.

## Implementation Direction

Add a dedicated policy visibility service and DTO set that composes:

- effective policies by billing account/provider
- threshold usage from current cost visibility
- pending approval requests and recent denied/approved requests
- recent policy decisions, especially warnings, approvals, denials, stale-data risk, cleanup risk, and automatic-action-threshold hits
- automatic action attempt history and retry/failure status
- operator next-step flags such as `HasPendingApprovals`, `HasDeniedActions`, `HasAutomaticActionsPending`, and `HasCleanupRisk`

Expose two read projections:

- admin aggregate: all policy rows, billing-account scoped summaries, recent decisions, approvals, and automatic actions
- server scoped: only policy state related to an authorized game server/session/billing assignment

Refresh the generated WebApp API client after the API surface exists, then add repository facade models/mappers:

- `ICloudAdminRepository.GetPolicyVisibilityAsync`
- `IGameServerRepository.GetCloudPolicyVisibilityAsync`

Update UI surfaces:

- `/servers/cloud`: summary cards and a Policy tab for effective policy, threshold usage, pending approvals, recent decisions, and automatic actions.
- `/servers`: selected-server cloud runtime section adds relevant warnings, approval/denial reasons, automatic shutdown status, and retry/cleanup guidance for the selected server.

## Safety Notes

- Visibility must be descriptive and scoped. It should not allow server owners to inspect unrelated billing accounts, other servers' policy decisions, or global admin queues.
- DTOs may expose sanitized summaries and reason codes, but not raw exception text beyond existing sanitized fields.
- "Cleanup runtime" and "Delete provider resources" must remain clearly distinct.

## Test Direction

Focused tests should cover:

- admin policy visibility includes effective policy, cost usage, pending approvals, denied decisions, and automatic action attempts
- server-scoped policy visibility filters unrelated decisions, approvals, and automatic actions
- missing game server returns 404
- authorization attributes remain admin/resource scoped
- DTOs do not expose sensitive property names
- WebApp repositories use generated clients and no raw browser HTTP bridge is added
