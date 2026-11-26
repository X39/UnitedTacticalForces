# Phase 63 Deferred Items

- 63-01: `npm --prefix sources/ReactApp run format:check` reports `tests/hosting/arma-placement.test.tsx` and
  `tests/hosting/fleet-fixtures.test.ts` (pre-existing at HEAD eab7b5fa, not touched by Phase 63). Out of scope; fix
  with `prettier --write` in a dedicated chore commit before the CI gate runs.
- 63-10: `sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx` still renders a hard-coded `/h` suffix in the
  runtime profile option text (`{formatMoney(profile.estimatedHourlyCostEur)}/h`). The plan limited that file to the
  external storage note key swap, so it was left. Replace it with `Server_Machine_ResourceHourly` in a later plan.
- 63-10: `tests/hosting/activity-section.test.tsx` "shows 50 entries first and Show more activity adds 50..." exceeded
  the 5 s default timeout once in a full `tests/hosting tests/styles` run and passes in isolation (36/36). Not touched
  by 63-10; consider a higher timeout for that test.
- 63-10: the locale key `React_ServerWorkspace_ExternalStorageDeleted` is now unused (replaced by
  `Server_Machine_ExternalStorage`); remove it from both locale files when the old copy is retired.
- 63-11: `tests/hosting/activity-section.test.tsx` "shows 50 entries first and Show more activity adds 50 and focuses
  the first new one" now times out (5.5 s against the 5 s default) in every full `npm test` run and passes in isolation
  (36/36). It is outside plan 63-11's file list and was not edited. Give the test a higher timeout or make its render
  cheaper; until then `npm test` exits 1 on this single test.
- 63-11: `format:check` also lists `tests/hosting/fleet-page.test.tsx` (not touched by Phase 63 plans), next to the two
  files from 63-01.
- 63-11: legacy `.next-hosting-job-list`, `.next-hosting-job` and related styles in `hosting.css` are probably unused
  now that the page no longer renders the job list; verify and remove in a later plan.
- 63-12: resolved from the list above: the `activity-section.test.tsx` paging test now has a 30 s timeout (commit
  640999eb) and `npm run ci` exits 0. The three `format:check` files were only CRLF checkout artifacts on this Windows
  working tree (prettier rewrites them to LF and git sees no content change); they are clean in a Linux CI checkout.
- 63-12: `.next-hosting-job*` selectors in `hosting.css` are confirmed unreferenced by any `.tsx` or `.ts` file (8 CSS
  references, 0 source references). Remove them in a later styles plan; deleting was outside the 63-12 file list.
- 63-12: `tests/e2e/hosting-fleet.spec.ts` "the Command Center nav and the fleet page read Servers" is flaky (6 of 8
  runs with `--repeat-each=8`, about 1 in 5 in the four-spec run): `toHaveTitle(/Servers/)` sees "Server fleet".
  `FleetPages.tsx` and `CommandCenterLayout.tsx` both call `useDocumentTitle`, so the title that wins depends on
  effect and mount order. Phase 62 code, not touched by Phase 63; fix the title ownership (one owner) or relax the
  assertion in a Phase 62 follow-up.
- 63-12: German section links wrap onto a third row at 320 px when Barlow Condensed does not load (the page takes it
  from Google Fonts, there is no self-hosted fallback, and the system fallback font is much wider). The e2e asserts the
  two-row limit only while the real font is active and records a `label-font-fallback` annotation otherwise. Self-host
  the font or tighten the link padding at 520 px and below to make the limit hold without network access.
  status: acknowledged
