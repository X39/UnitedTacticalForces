# Plan 25-03 Summary: Admin Import API, Duplicate Handling, And Verification

## Completed

- Added import batch and line-item DTOs plus an import creation payload that carries CSV content without persisting it.
- Added `CloudBillingImportService` for source hashing, parser orchestration, persistence, duplicate handling, and audit records.
- Added admin-only `CloudBillingImportsController` endpoints for importing Hetzner consumption CSVs and reading batches/line items.
- Added DTO transforms and route constants.
- Added service/controller tests for persistence, restart-safe reload, duplicate reject/ignore/supersede behavior, failed imports, supersede failure safety, authorization attributes, read endpoints, and DTO field-name redaction.

## Notes

- Duplicate imports are rejected unless explicitly ignored or superseded.
- A failed attempted supersede no longer marks the prior imported batch as superseded.
- Hosting audit records summarize import outcomes without raw CSV content.
