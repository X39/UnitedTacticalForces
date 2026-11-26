---
phase: 38
slug: cost-responsibility-history-and-audit-visibility
status: draft
shadcn_initialized: false
preset: none
created: 2026-06-21
---

# Phase 38 - UI Design Contract

> Visual and interaction contract for cost responsibility, history, and audit visibility.

## Design System

| Property | Value |
|----------|-------|
| Component library | Microsoft Fluent UI Blazor 4.11.8 |
| Icon library | Microsoft Fluent UI Icons through existing `FluentButton` icon patterns |
| Page surface | Existing `/servers` owner workflow and `/servers/cloud` admin fleet console |
| Typography and spacing | Reuse `.detail-section`, `.meta-grid`, `.data-table`, `.status-badge`, `.muted`, and `.risk-note` |

## Scope Contract

| Surface | Contract |
|---------|----------|
| `/servers` selected server details | Show cost responsibility as internal attribution for the selected server, including active/current owner context, historical owner intervals, estimated/reconciled/adjusted labels, cleanup risk, policy context, and sanitized audit events. |
| `/servers/cloud` admin console | May retain fleet-level cost, reconciliation, policy, and provider metadata visibility. It must not become the only place to inspect a server's cost responsibility history. |
| Authorization | A user who can inspect a server through `sv`/server-admin claims can inspect read-only cost responsibility context for that same server. Mutating billing owner assignment remains admin/server-admin controlled. |
| Billing language | Use "cost responsibility", "owner assignment", "provider allocated", "adjusted", and "internal attribution". Do not use payment, checkout, customer invoice, taxes, legal invoice, or charge collection language. |
| Redaction | Render only DTO/facade fields already sanitized by the API/repository boundary. Do not display raw JSON details, provider tokens, worker credentials, Steam credentials, authorization headers, raw provider payloads, or exception bodies. |

## Interaction Contract

| Area | Required Behavior |
|------|-------------------|
| Cost responsibility summary | Show estimated runtime cost, accumulated cost, provider allocated cost, adjusted cost, billing reconciliation status, billable resources, cleanup risk, and effective policy where available. |
| Owner history | Show all historical owner assignment intervals for the selected server, including assigned/unassigned timestamps and reasons. This remains visible after transfers. |
| Audit context | Show sanitized hosting audit records related to the selected server so lifecycle, policy, cost, cleanup, and owner events can be inspected together. |
| Empty states | If no history or audit records exist, show a quiet empty row/message rather than hiding the section. |
| Cleanup risk | Keep the existing warning that provider teardown is the cost-stop boundary and runtime stop/cleanup/local deletion do not stop provider billing. |

## Visual Contract

| Element | Treatment |
|---------|-----------|
| Cost responsibility heading | Use a `detail-section` heading named `Cost responsibility history` to distinguish from payment/billing workflow terminology. |
| Internal attribution note | Use existing muted/risk-note styling and concise copy explaining the section is internal attribution, not invoicing or checkout. |
| History table | Dense `data-table` with owner, assignment id, assigned, unassigned, status, and reason columns. |
| Audit table | Dense `data-table` with event, server, job, cost owner, worker, summary, and time columns. |
| Status | Use existing `status-badge` classes; do not introduce new palettes. |

