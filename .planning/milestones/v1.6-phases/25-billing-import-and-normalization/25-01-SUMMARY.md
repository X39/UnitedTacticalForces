# Plan 25-01 Summary: Billing Import Schema, Source Metadata, And Idempotency

## Completed

- Added `ECloudBillingImportBatchStatus` as the stable contract for imported, failed, ignored duplicate, and superseded import batches.
- Added durable Hosting entities for billing import batches and normalized provider line items.
- Registered `CloudBillingImportBatches` and `CloudBillingImportLineItems` in `ApiDbContext`.
- Added restricted EF relationships for importer, duplicate/supersede links, and batch-to-line-item ownership.
- Added database-backed active duplicate prevention with a unique provider/source-hash index filtered to imported batches.
- Added migration `20260618162647_AddCloudBillingImports`.

## Notes

- Raw CSV content is intentionally absent from the schema.
- Line items store normalized provider facts, bounded metadata, row numbers, and row hashes for downstream matching.
