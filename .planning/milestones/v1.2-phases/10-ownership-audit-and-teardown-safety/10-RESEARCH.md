# Phase 10 Research: Ownership, Audit, And Teardown Safety

## Scope

Phase 10 extends the Phase 9 provisioning path with explicit teardown and cleanup semantics. It should not introduce cloud worker bootstrap or Arma 3 cloud runtime execution; those remain Phase 11 and later.

## Existing Implementation Surface

- `CloudProvisioningService` creates Hetzner firewall, server, floating IP, and volume resources from a valid dry-run plan. It saves after each successful provider mutation so partial provider state is durable.
- `CloudResourceInventoryRecord` and `CloudGameServerSession` already carry provider identifiers, provider resource ids, state, game-server ownership snapshots, and billing assignment snapshots.
- `HostingAuditService` sanitizes summaries and details before persisting audit records.
- `CloudInventoryService.TransitionResourceStateAsync` supports cleanup-needed to orphan-candidate transitions and already preserves billing snapshots.
- `CloudInventoryController` exposes admin-only cloud inventory and provisioning endpoints.

## Provider Teardown Notes

- Hetzner documents that many cloud resources continue to incur costs while they exist, including servers, floating IPs, and volumes. Deletion, not power-off, is the cost-stop boundary.
- Hetzner cancellation documentation also notes that volumes must be disconnected before they can be deleted.
- The existing provider boundary intentionally allows only verify/get/create methods. Phase 10 should widen that boundary explicitly to teardown-only methods instead of adding generic update/power operations.

## Implementation Implications

- Add explicit provider methods for deleting server, floating IP, firewall, and volume resources. Add a volume detach method because attached volumes cannot be deleted safely.
- Persist a deletion timestamp on sessions and resource inventory records so cost-stop time is durable and distinguishable from cleanup-needed or orphan-candidate states.
- Keep provider ids after deletion for auditability; use state and deletion timestamp, not null provider ids, to decide whether cost has stopped.
- Teardown should be best-effort across known provider ids. A single failed delete must leave that resource cleanup-needed, keep its owner snapshot, and record a sanitized failure audit.
- Resource deletion order should detach/delete volumes first, delete the server, then delete floating IP and firewall records. This keeps volumes deletable and removes firewall server references before firewall cleanup.
- Orphan marking can reuse `TransitionResourceStateAsync`, but the audit kind should distinguish orphan marking from ordinary state changes.

## Test Strategy

- Add fake HTTP tests for delete/detach provider endpoints and ensure no token material appears in failure messages.
- Add service tests for successful teardown, teardown failure with remaining cleanup-needed billable resources, owner snapshot preservation on partial provisioning failure, and orphan marking audit.
- Add controller reflection or direct invocation tests for the admin-only teardown endpoint.

## Sources

- Hetzner Cloud API reference: https://docs.hetzner.cloud/reference/cloud
- Hetzner cancellation documentation: https://docs.hetzner.com/general/billing-and-account-management/cancellation/cancellations-hetzner-console/
