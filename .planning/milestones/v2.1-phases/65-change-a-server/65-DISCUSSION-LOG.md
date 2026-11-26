# Phase 65: Change a Server - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.

**Date:** 2026-11-20
**Phase:** 65-change-a-server
**Areas discussed:** Staging model, Mod picker scope, Settings and secrets UX, Take-effect and leave guard

## Staging model
**User's choice:** Mods + settings staged; files immediate; draft in memory only; review UI and partial failure: you decide.

## Mod picker scope
**User's choice:** You decide (both). **Notes:** API changes are fine; remove "protect the API" wording from docs (done).

## Settings and secrets UX
**User's choice:** API declares Common/Advanced; masked secrets with explicit reveal.

## Take-effect and leave guard
**User's choice:** API pending-restart signal + banner; guard covers everything; files `?path=` mechanics: you decide.
**Notes:** URLs need not be stable; remove such constraints from docs (done).

## Claude's Discretion
Review UI placement, partial failure semantics, mod picker details, path history mode.

## Deferred Ideas
FUT-01, FUT-04.
