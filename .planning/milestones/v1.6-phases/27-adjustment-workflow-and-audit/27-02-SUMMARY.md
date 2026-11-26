# Plan 27-02 Summary: Adjustment API And Audit

## Completed

- Added adjustment DTO and create/decision payloads.
- Added DTO projection in `Transform`.
- Added admin-only `CloudBillingAdjustmentsController` endpoints for create, apply, reject, list, and get.
- Added hosting audit event kinds for adjustment created/applied/rejected.
- Added sanitized audit records for create/apply/reject transitions.
- Added invariant-culture amount formatting for persisted audit details.

## Safety Properties

- All adjustment endpoints require `Claims.Administrative.Server`.
- Secret-like reason text is sanitized before entity persistence, DTO projection, or audit persistence.
- Applying or rejecting requires pending status and records the actor/timestamp separately from creation.

## Verification

- Covered by `CloudBillingAdjustmentApiTests` and service redaction/audit tests.
