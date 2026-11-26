---
phase: 39
slug: verification-and-phase-30-handoff
status: draft
created: 2026-06-21
---

# Phase 39 - UI Design Contract

> Phase 39 does not add product UI. The user-facing artifact is documentation that should be easy for an operator to
> scan before a separate live UAT milestone.

## Scope Contract

| Surface | Contract |
|---------|----------|
| Evidence index | Separate implementation/build/test evidence from live/browser/provider/runtime evidence. |
| Phase 30 handoff | List operator prerequisites and pending fields without implying that Phase 30 is passed. |
| Language | Use "implementation evidence", "dry-run", "component/API/repository/source-boundary", and "deferred live UAT". |
| Forbidden claims | Do not say live Hetzner, Steam, Arma, authenticated browser, provider mutation, or final provider cleanup passed. |
| Redaction | Do not record tokens, cookies, credentials, authorization headers, bearer values, raw provider payloads, screenshots, or secret-bearing logs. |

## Information Design

| Section | Required Behavior |
|---------|-------------------|
| Evidence matrix | Map each VERIFY-01 behavior to tests/artifacts and result classification. |
| Evidence classification | Use distinct labels for `automated`, `component`, `API`, `repository`, `source-boundary`, `build`, `deferred-live`, and `operator-required`. |
| Handoff checklist | Include approval, budget, cleanup window, authenticated sessions, provider readiness, Steam environment, dry-run/cost/policy/reconciliation evidence, and final provider-side billable resource count `0`. |
| Stop conditions | Clearly state that no live mutation should run until required operator fields are recorded. |

