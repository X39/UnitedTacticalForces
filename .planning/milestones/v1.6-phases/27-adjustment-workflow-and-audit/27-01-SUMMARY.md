# Plan 27-01 Summary: Adjustment Schema And Core Service

## Completed

- Added `ECloudBillingAdjustmentStatus` and the durable `CloudBillingAdjustment` entity.
- Registered adjustment persistence through `ApiDbContext` with restricted relationships to match records, import batches, import line items, inventory/session/game-server scope, billing accounts, billing assignments, contributions, and actors.
- Added an EF migration for `CloudBillingAdjustments`.
- Added a filtered unique index that allows only one non-rejected adjustment per match record.
- Added `ICloudBillingAdjustmentService` and `CloudBillingAdjustmentService`.

## Safety Properties

- Adjustments snapshot match-record scope into an immutable overlay and do not mutate raw imports, match records, or cost estimates.
- Adjustment creation requires an allocated owner scope: billing account, billing assignment, allocation interval, and matching currency.
- Historical owner attribution is copied from the match record so later billing transfers do not rewrite adjustment ownership.
- Duplicate pending/applied adjustments are rejected at service level and protected by the database index.

## Verification

- Covered by `CloudBillingAdjustmentServiceTests`.
