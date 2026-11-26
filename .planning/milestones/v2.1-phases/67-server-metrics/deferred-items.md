# Deferred items - phase 67

## Found during plan 67-15 (out of scope, pre-existing)

- `sources/ReactApp/tests/hosting/accrued-cost.test.tsx` (4 cases), `cost-alert-banner.test.tsx` (1) and `cost-confirm-dialog.test.tsx` (2) fail since 2026-11-22: the fixtures use instants on 2026-11-21, so `formatInstant` adds the short date because the instant is no longer today. The tests should build their expected text with the same helper or use fake system time.
- Several `tests/hosting/workspace-page.test.tsx` cases exceed the default 5 s timeout under a full parallel run and pass alone.
  status: acknowledged

## Found during plan 67-20 (out of scope, pre-existing)

- `sources/ReactApp/tests/e2e/hosting-fleet.spec.ts` (20 cases) and `hosting-placement.spec.ts` (1 case) fail at HEAD because their API mocks do not answer `GET /api/cost-alerts` (added to every page by Phase 66). The metrics panel is not involved. Plan 67-20 added `tests/e2e/support/costMocks.ts` (`fulfillCostRoute`) for the six specs of its list; the same two-line call fixes these two specs.
- `tests/hosting/metrics-performance.test.tsx` ("renders 2 016 connected points at 320px within 200 ms") failed once in a full parallel `npm test` run and passed 3 of 3 times alone. The 200 ms backstop is sensitive to machine load.
- The 7 date-dependent failures listed above are unchanged (accrued-cost 4, cost-alert-banner 1, cost-confirm-dialog 2).
  status: acknowledged
