# Phase 62 Deferred Items

Out-of-scope discoveries logged by executors. Not fixed by the plan that found them.

## Found during 62-03

- `npm --prefix sources/ReactApp run format:check` reports three files committed by plan 62-01 that are not Prettier
  formatted at HEAD `f5a02059`: `src/features/hosting/FleetLiveUpdatesProvider.tsx`,
  `tests/hosting/fleet-fixtures.test.ts`, `tests/hosting/fleet-signal-tracer.test.tsx`. Every file touched by 62-03
  passes `prettier --check`. Fix with `npx prettier --write` on those three files in the plan that next owns them.
  status: acknowledged

## Found during 62-06

- 62-06 owned and formatted `FleetLiveUpdatesProvider.tsx` and `fleet-signal-tracer.test.tsx`. Still open:
  `tests/hosting/fleet-fixtures.test.ts` is not Prettier formatted at HEAD (not touched by 62-06).
- `src/auth/claims.ts` reports under `format:check` only because the working copy has CRLF line endings
  (`core.autocrlf=true`); its committed content passes `prettier --check`. No code change needed.
  status: acknowledged

## Resolved during 62-12

- `tests/hosting/fleet-fixtures.test.ts` formatted with Prettier (commit `033464cd`) so the phase gate `npm run ci`
  passes. `src/auth/claims.ts` working-copy line endings were normalized to LF; the committed content is unchanged.
  status: acknowledged
