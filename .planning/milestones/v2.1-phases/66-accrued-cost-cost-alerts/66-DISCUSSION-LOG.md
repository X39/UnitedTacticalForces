# Phase 66: Accrued Cost & Cost Alerts - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-11-21
**Phase:** 66-accrued-cost-cost-alerts
**Areas discussed:** Accrued cost basis, Threshold configuration, In-app alert surface, Delivery & durability

---

## Accrued cost basis

| Option | Selected |
|--------|----------|
| Ledger slices + running slice | ✓ |
| Rate x elapsed | |
| Ledger only | |

**Live update:** user chose WebSocket push (REST API must also exist); avoid polling wherever possible.

---

## Threshold configuration

- Scope: per cost owner ✓ (alternatives: per server, global config)
- Form: several absolute EUR amounts ✓ (alternatives: one amount, percentage of allowance)
- Defaults: none, alerts only once configured ✓ (alternative: shipped defaults)

---

## In-app alert surface

- Model: persisted alert record in ProfileMenu + workspace banner ✓ (alternative: derived, not stored)
- Lifetime: until dismissed ✓ (alternative: until session ends)
- Settings UI: React cost-owner view ✓ (alternative: inside workspace cost strip)

---

## Delivery & durability

- Email: provider-neutral IEmailSender with SMTP ✓
- Recipient: User.EMail or other mail source; only one mail sent, app-local preferred; user locale recorded
- Durability: persisted delivery record per alert and channel with bounded retry ✓
- Shared owners: resolve via owner type, researcher confirms ✓

---

## Claude's Discretion

Push cadence and message shape, table design, retry policy, copy, SMTP library.

## Deferred Ideas

Broader poll removal; percentage and per-server thresholds.
