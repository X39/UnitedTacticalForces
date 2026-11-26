---
created: 2026-11-19T11:51:13.236Z
title: Review WebSocket update-stream mechanism
area: api
severity: minor
files:
  - sources/Api/Controllers/UpdateStreamController.cs
  - sources/Api/Services/UpdateStreamService/UpdateStreamService.cs
  - sources/ReactApp/src/app/updateStream.ts
  - sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs
audit_acknowledged:
  milestone: v2.1
  at: 2026-11-26
---

## Problem

Each WebSocket connection subscribes to exactly one path (`events/{id}`, `game-servers/fleet`) through
`/update-stream/subscribe?path=...`, and `UpdateStreamService` stores subscribers as `(path, socket)` pairs. The
broker cannot filter by recipient, so claim-scoped delivery has to be encoded in the path. Phase 62 gap G-62-2
(fleet signal disclosed servers a viewer cannot see) is being closed with per-user paths
(`game-servers/fleet/{userId}`) and publish-time audience resolution as a stop-gap. The product owner wants the
whole mechanism reviewed, with a single active connection per client as the expected target.

Related open findings from 62-REVIEW: IN-11 (no WebSocket `Origin` check) and the missing per-send timeout.

## Solution

TBD. Evaluate a single multiplexed connection per client with subscribe/unsubscribe messages, per-topic claim
checks at subscribe and publish time, an `Origin` check, and a per-send timeout. This changes shared
infrastructure, so AGENTS.md requires an explicit architectural justification and user request before work starts.
