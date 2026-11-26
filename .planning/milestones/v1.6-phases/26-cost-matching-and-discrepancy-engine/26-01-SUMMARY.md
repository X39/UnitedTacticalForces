# Plan 26-01 Summary: Matching Model, Exact Provider Matching, And Idempotency

## Completed

- Added match run, match method, and review status contract enums.
- Added durable `CloudBillingMatchRun` and `CloudBillingMatchRecord` Hosting entities.
- Registered match runs/records in `ApiDbContext` with restricted relationships and a unique import-batch plus algorithm-version idempotency index.
- Added migration `20260618165049_AddCloudBillingMatches`.
- Added `ICloudBillingMatchingService` and `CloudBillingMatchingService`.
- Implemented exact provider matching by provider identifier, resource kind, and provider resource id.
- Persisted unmatched and ambiguous rows as review-visible records.
- Returned existing run records for repeated matching of the same import batch and algorithm version.

## Notes

- The matching service does not mutate provider state.
- Failed prior runs are returned idempotently rather than creating duplicate run rows.
