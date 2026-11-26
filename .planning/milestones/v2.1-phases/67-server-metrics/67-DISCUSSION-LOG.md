# Phase 67: Server Metrics - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-21
**Phase:** 67-Server Metrics
**Areas discussed:** Transport & history, Players per game, Overview display & charts, Unavailable & stale rules

---

## Transport & history

| Option | Description | Selected |
|--------|-------------|----------|
| Extend heartbeat | Optional metrics block on HTTP heartbeat | |
| Separate metrics endpoint | New HTTP endpoint | |
| WebSocket (user free text) | Persistent connection, no polling | ✓ |
| PostgreSQL table | Durable history | ✓ |
| In-memory ring buffer | Lost on restart | |
| 7 days, downsampled | Longer trends | ✓ |
| Metrics only / Metrics + heartbeat / Full channel | Scope of worker WebSocket | Full channel ✓ |
| Tiered rollups / Single resolution / You decide | Downsampling | You decide ✓ |

**User's choice:** WebSocket full channel, PostgreSQL, 7 days downsampled.
**Notes:** "We need a proper connection here between the two that is not relying on polling."

---

## Players per game

| Option | Description | Selected |
|--------|-------------|----------|
| All three where possible | Arma 3, CS2, Aska | ✓ |
| Arma 3 and CS2 only | | |
| Arma 3 only | | |

---

## Overview display & charts

| Option | Description | Selected |
|--------|-------------|----------|
| Tiles + charts | Tiles and a chart per metric, 1h/24h/7d | ✓ |
| Tiles with sparklines | | |
| Hand-built SVG / Add library / You decide | Chart implementation | You decide ✓ |

---

## Unavailable & stale rules

| Option | Description | Selected |
|--------|-------------|----------|
| Gap + reason label | Break line at gaps, plain reason | ✓ |
| Dimmed last value | | |

---

## Claude's Discretion

Downsampling tiers, chart implementation, stale threshold and wording, live browser push.

## Deferred Ideas

Disk/network metrics, per-member visibility, metric alerting.
