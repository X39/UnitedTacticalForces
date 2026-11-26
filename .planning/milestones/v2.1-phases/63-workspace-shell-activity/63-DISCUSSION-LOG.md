# Phase 63: Workspace Shell & Activity - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-19
**Phase:** 63-workspace-shell-activity
**Areas discussed:** Section structure, Header & cost strip, Progress & failure, Activity timeline

---

## Section structure
- Sections: Regroup to 6 (selected; clarified to 5: Overview, Activity, Settings, Files, Machine) / Keep all 9 + Activity / Regroup to 5.
- Mobile nav: scrollable strip / wrap / select menu → **You decide**.
- Old ?tab=: map and rewrite / map without rewriting → **"We do not need backward compatibility"** (amends WSP-02).

## Header & cost strip
- Header: **Compact sticky** / full sticky / non-sticky.
- Accrued slot: hidden when empty / **visible "not yet available"**.

## Progress & failure
- Live updates: **Reuse fleet socket + slow poll while job active** / socket only / keep 5s polling.
- Progress strip: named stages / stage text only → **You decide**.
- Failure next steps (multi): **Retry/skip, Shut down and stop billing, Open activity details**.

## Activity timeline
- Order/filter: **Newest first + type filter** / no filters / grouped by job.
- Raw logs: expand in place / separate view → **You decide**.
- Freshness: age text + refresh / **Live badge**.

## Claude's Discretion
320/390 nav mechanism; progress stages; raw-log access; breadcrumbs and empty slots.

## Deferred Ideas
Grouping entries under jobs; breadcrumb and empty-slot visuals.
