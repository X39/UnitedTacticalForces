# Phase 62: Fleet & Quick Actions - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-18
**Phase:** 62-Fleet & Quick Actions
**Areas discussed:** Member entry point, Card content & 320px, Search/filter/grouping, Quick start/stop behavior

---

## Member entry point

| Option | Description | Selected |
|--------|-------------|----------|
| Community header 'Servers' link | Top-level public link to /command/servers | |
| Keep only inside Command Center | Relabel nav item; enter via Command Center | ✓ (via free text) |
| Both | Header link plus relabelled item | |

**User's choice:** Free text: members with sv or sv:view must be able to enter the command center via the current path. Command Center and the public-facing page are two different things; most members will not have Command Center access, and those who do get a distinctive, less vibrant, more focused interface. Command Center is by design a separate UI concept.
**Notes:** Shell framing not changed. Label: "Servers / Server".

## Card content & 320px

**User's choice:** "You decide" for card facts, small-screen layout, and summary strip.
**Notes:** Recommendations adopted (minimal card, single-column cards, filter counts).

## Search, filter, grouping

| Question | Selected |
|----------|----------|
| Mine = server owner or cost owner | ✓ |
| URL state: search params, replace history | ✓ |
| Grouping: Active then Off sections, expanded | ✓ |
| Search over name, game, owner | ✓ |

## Quick start/stop behavior

| Question | Selected |
|----------|----------|
| Primary: Start when off, cost-stop when running | ✓ |
| Terminate: workspace only | ✓ |
| Feedback: toast + refetch + pending state | ✓ |
| Updates | Free text: use a WebSocket connection to receive information rather than active polling |

**Follow-up (push scope):** Fleet-change signal in this phase ✓; no fallback (user); signals from lifecycle executor/job transitions ✓.

## Claude's Discretion

- Card layout details, section ordering, reconnect/backoff mechanics and disconnected indicator, attention/unconfirmed primary action mapping.

## Deferred Ideas

- Workspace live updates and replacing other pollers (Phase 63), full-state socket payloads, shared backplane, terminate from fleet.
