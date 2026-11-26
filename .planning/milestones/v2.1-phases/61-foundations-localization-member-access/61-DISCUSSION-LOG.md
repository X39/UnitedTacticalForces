# Phase 61: Foundations, Localization & Member Access - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-18
**Phase:** 61-Foundations, Localization & Member Access
**Areas discussed:** none interactively (defaults accepted)

---

## Gray area selection

| Option | Description | Selected |
|--------|-------------|----------|
| Locale seeding | Translate referenced identical keys vs allowlist | |
| ACC-01 proof | How to prove bare sv / sv:view end to end | |
| Parity matrix | Row granularity, sign-off, CI enforcement | |
| Rollout & cleanup | copy.ts retirement, fixtures, one cut vs shim | |

**User's choice:** Free text: "The ui spec phase told me to execute discuss". No areas selected.

## Proceed mode

| Option | Description | Selected |
|--------|-------------|----------|
| Write context with defaults | Lock UI-SPEC decisions, apply recommended defaults for the 4 areas | ✓ |
| Discuss all four areas | Few questions each | |
| Discuss only a few | User names areas | |

**User's choice:** Write context with defaults.
**Notes:** UI-SPEC already locked state model, dialog, tokens, copy, locale tooling. Defaults applied: translate all
referenced identical keys; Api.Tests integration plus React check for ACC-01; row-per-capability matrix enforced in CI;
single-cut replacement of ActionConfirmation/ServerStatus.

## Claude's Discretion

Icon glyph per state, module layout under `src/components/server/`, axe test runner, check-script mechanics.

## Deferred Ideas

Fleet grid overflow (62), stable reason codes (63), global `--quiet` fix and identical-value failure (69), Playwright
mobile baselines (69), create-and-start confirmation (64).
