---
phase: 66
review: 66-REVIEW.md
titles: json
findings:
  - id: WR-01
    severity: warning
    disposition: open
    title: "A concurrent partial dismissal turns Dismiss all into a 500, although the end state is reachable"
  - id: WR-02
    severity: warning
    disposition: open
    title: "The client can send more than 50 ids, after which Dismiss all fails with 400 on every attempt"
  - id: WR-03
    severity: warning
    disposition: open
    title: "A failed first load of the alerts is not retried when the socket opens"
  - id: IN-01
    severity: info
    disposition: open
    title: "Success side effects run inside the `try`, so a failing toast reports a failed dismissal"
  - id: IN-02
    severity: info
    disposition: open
    title: "`BillingUnitSecondsOf` is duplicated, and the DTO documentation omits the new reason"
  - id: IN-03
    severity: info
    disposition: open
    title: "The unavailable reason `billing-unit` is silent end to end"
  - id: IN-04
    severity: info
    disposition: open
    title: "The clock pin fixes the date but not the time zone"
  - id: IN-05
    severity: info
    disposition: open
    title: "The billing anchor is the first charged slice, but the UI states the next-hour instant as fact"
  - id: IN-06
    severity: info
    disposition: open
    title: "New or changed handwritten members lack XML documentation"
open: 9
total: 9
recorded: 2026-11-22T18:23:39.762Z
---

# Phase 66: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| WR-01 | warning | open | - |
| WR-02 | warning | open | - |
| WR-03 | warning | open | - |
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| IN-04 | info | open | - |
| IN-05 | info | open | - |
| IN-06 | info | open | - |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
