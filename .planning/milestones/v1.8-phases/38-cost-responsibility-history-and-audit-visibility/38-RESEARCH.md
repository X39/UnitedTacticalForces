# Phase 38: Cost Responsibility, History, And Audit Visibility - Research

**Researched:** 2026-06-21
**Domain:** ASP.NET Core authorization, Blazor server-owner workflows, cloud cost attribution projections
**Confidence:** HIGH

## Summary

The codebase already has most Phase 38 data: `CloudCostVisibilityService` composes estimated, reconciled, adjusted, cleanup-risk, reconciliation, and policy-adjacent values; `GameServerBillingAssignment` stores owner intervals; `HostingAuditRecord` stores sanitized audit events; `/servers` already loads server-scoped cost and policy visibility; and `IGameServerRepository` already wraps generated Kiota calls for cost, owner history, and audit records.

The main gap is access and presentation. Server-scoped cost visibility is authorized through `sv`/server-admin access, but owner history and audit records are admin-only and hidden behind `CanManageServers`. That prevents non-admin users who can inspect a server from seeing the relevant cost responsibility history and audit context for that server.

## Findings

| Area | Existing Evidence | Phase 38 Action |
|------|-------------------|-----------------|
| Server-scoped cost visibility | `GameServersController.GetCloudCostVisibilityAsync` uses `Authorize($"{Claims.ResourceBased.Server.All}:*")`. | Preserve and reuse. |
| Billing owner history | `GetBillingOwnerHistoryAsync` returns `GameServerBillingAssignmentDto[]` but is admin-only. | Change read-only endpoint to server resource scope. |
| Audit records | `GetHostingAuditRecordsAsync` returns sanitized `HostingAuditRecordDto[]` but is admin-only. | Change read-only endpoint to server resource scope. |
| WebApp selected server details | `/servers` loads cost/policy visibility for any selected server but loads owner/audit only for `CanManageServers`. | Load and render read-only context for any selected server. |
| Generated client boundary | Existing generated request builders already exist for billing-owner history and audit-records. | Do not edit generated files. |
| Language boundary | Phase docs require internal attribution only. | Rename UI section and avoid invoice/payment wording. |

## Constraints

- Mutating owner assignment remains admin/server-admin only.
- Do not add payment processing, checkout, taxes, customer invoicing, or legal invoice artifacts.
- Do not display raw JSON payloads or secret-bearing fields.
- Do not edit `sources/WebApp/UtfApi/**` generated files.

