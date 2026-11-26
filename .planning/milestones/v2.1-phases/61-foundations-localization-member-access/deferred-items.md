# Phase 61 Deferred Items

Out-of-scope discoveries logged during execution; not fixed by the plan that found them.

## Found during 61-07

- `sources/ReactApp/tests/hosting/arma-placement.test.tsx` fails `npm --prefix sources/ReactApp run format:check`
  (Prettier). The file was last changed by `2965563a` and is untouched by Phase 61 plans. `lint`, `typecheck` and
  `test` are green; only `format:check` reports this one file. Fix with `prettier --write` in a separate change.
  status: acknowledged
