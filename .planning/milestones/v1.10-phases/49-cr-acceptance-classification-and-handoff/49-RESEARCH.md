# Phase 49 Research

## Objective

Identify the evidence sources and classification rules needed to close v1.10 without fabricating CR-0001 or Phase 30 pass status.

## Findings

### R-49-01: CR-0001 has nine overall acceptance criteria

The change request requires frontend creation, non-SSH cloud provisioning, frontend lifecycle operations, visible public connection details, durable logs/lifecycle state, billable-resource owner visibility, owner cost visibility, administrator audit visibility, and actionable failed/orphaned resources.

### R-49-02: v1.10 evidence is intentionally split between implementation proof and live/operator proof

Phase 45 verified approval/preflight surfaces and API gates. Phase 46 verified provisioning evidence surfaces. Phase 47 verified runtime evidence surfaces. Phase 48 verified teardown and redaction evidence surfaces. None of those automated runs executed a full live Hetzner provisioning/runtime/cleanup pass.

### R-49-03: Phase 30 remains the operator-controlled live evidence gate

`.planning/v1.7-LIVE-UAT-EVIDENCE.md` and the Phase 39 handoff both keep Phase 30 blocked until operator approval, authenticated sessions, provider readiness, billing owner, dry-run, cost, policy, reconciliation, runtime, cleanup, final provider-side billable count `0`, and redaction evidence exist.

### R-49-04: Requirements statuses must distinguish shipped implementation from acceptance pass

LIVE-01 through LIVE-03 have enough implementation evidence to be marked complete in the requirements traceability table, but PROV, RUNT, and CLN criteria still require live/operator evidence before they can be accepted. ACC-01 can be completed by producing an honest classification matrix and handoff even if many mapped criteria are blocked or human-needed.

### R-49-05: Existing tests can guard the final docs

`VerificationHandoffBoundaryTests` already reads planning artifacts and rejects common false live-pass claims. Phase 49 can extend it for the new acceptance matrix and handoff artifacts without adding production code.

## Implications

- Phase 49 should create docs and tests, not production behavior.
- The acceptance matrix can complete ACC-01 while keeping CR-0001 final acceptance blocked.
- The final handoff should recommend milestone closure only as an evidence-classification milestone, not as proof of final live acceptance.
