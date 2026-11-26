---
phase: 70
review: 70-REVIEW.md
titles: json
findings:
  - id: WR-01
    severity: warning
    disposition: fixed
    title: "A filter chip removes the field the member is editing, dropping focus and the rest of the typed value"
  - id: WR-02
    severity: warning
    disposition: fixed
    title: "A file dropped next to the upload drop zone navigates the browser away from the app"
  - id: WR-03
    severity: warning
    disposition: fixed
    title: "Focus is lost when a queued upload is removed from the dialog"
  - id: IN-01
    severity: info
    disposition: fixed
    title: "The disclosure chevron only rotates when motion is allowed, so reduced-motion members get no visual open state"
  - id: IN-02
    severity: info
    disposition: fixed
    title: "The configuration search lower-cases with the browser's default locale"
  - id: IN-03
    severity: info
    disposition: fixed
    title: "`keepContextFocusRef` can stay armed after a failed context switch"
  - id: IN-04
    severity: info
    disposition: skipped
    title: "The global test setup opens every Settings panel, so most hosting tests cannot catch a collapsed-state regression"
---

# Phase 70 code review disposition

One row per finding in `70-REVIEW.md`; `open` until fixed or triaged.

| Finding | Disposition | Evidence |
|---|---|---|
| WR-01 | fixed | 10d564f4 |
| WR-02 | fixed | 48114bd0 |
| WR-03 | fixed | 004c53a1 |
| IN-01 | fixed | 3079457b |
| IN-02 | fixed | b3b15cc4 |
| IN-03 | fixed | 1bfe67cc |
| IN-04 | skipped | migrating 13 test files; 70-15 S-14 audit covers the collapsed default |

Re-review (iteration 2): clean.
