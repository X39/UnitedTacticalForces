---
phase: 31
status: complete
created: 2026-06-19
requirements:
  - VAL-01
  - VAL-02
  - VAL-03
---

# Phase 31 Validation Backfill Index

## Summary

Phase 31 backfilled or classified validation metadata without changing shipped requirement truth.

| Scope | Phases | Result |
|-------|--------|--------|
| v1.6 | 25-28 | `VALIDATION.md` artifacts added and classified as scoped delivery-verified. |
| v1.3 | 13-16 | `VALIDATION.md` artifacts added and classified as partial because live/browser/provider UAT remains operator-gated. |
| v1.0 | 02-04 | Reconstructed `VALIDATION.md` artifacts added from milestone audit evidence because archived phase directories were not retained. |
| v1.7 | 29-30 | Live UAT evidence remains blocked/deferred until operator approval and credentials exist. |

## Evidence Taxonomy

| Evidence Type | Meaning |
|---------------|---------|
| Automated verification | Tests, builds, static checks, and artifact checks run by agents. |
| Authenticated browser UAT | Administrator/server-owner browser verification with no cookies, headers, or bearer material recorded. |
| Live provider UAT | Hetzner create/delete or provider reconciliation evidence using approved token references only. |
| Live Steam/Arma UAT | SteamCMD/DepotDownloader/native Arma execution with operator-supplied credentials and cleanup window. |
| Redaction checks | Evidence review for provider, Steam, worker, auth, and secret-bearing material. |
| Cleanup evidence | Provider-side deletion confirmation and final billable resource count. |
| Deferred operator gate | A required manual/live check that is not claimed as passed. |
| Blocker | A required condition that prevents live mutation or requirement pass status. |

## Requirement Disposition

| Requirement | Status | Evidence |
|-------------|--------|----------|
| VAL-01 | complete | v1.6 Phase 25-28 validation files exist. |
| VAL-02 | complete | v1.3 Phase 13-16 and v1.0 Phase 02-04 validation files or classifications exist. |
| VAL-03 | complete | This index distinguishes automated verification, browser UAT, provider UAT, Steam/Arma UAT, redaction, cleanup, deferred gates, and blockers. |
| OPS-02 | deferred | Phase 30 cannot pass until live UAT records final provider-side billable resource count `0`. |
| OPS-03 | human_needed | Phase 29 evidence artifact exists but operator approval/preflight fields remain pending. |
| OPS-04 | deferred | Live Steam/Arma runtime UAT remains operator-gated. |

## Notes

The current operator instruction defers UAT until the whole change request is implemented across all milestones. This backfill therefore improves validation metadata while keeping live/operator requirements incomplete.
