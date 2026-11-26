# Plan 25-02 Summary: Hetzner Consumption CSV Parser And Diagnostics

## Completed

- Added the internal Hetzner billing parser contract and parse result records.
- Implemented CSV parsing with `TextFieldParser`, quoted-field handling, and header aliases for representative Hetzner export fields.
- Normalized resource kind, provider resource references, usage periods, amount, tax amount, currency, product/reference metadata, and row hashes.
- Added sanitized and bounded parser diagnostics.
- Added focused parser tests for valid rows, quoted fields, German decimal values, fallback periods, invalid rows, and secret redaction.

## Notes

- Row hashes are computed from normalized facts, not raw source row text.
- CSV parsing supports multiple known aliases instead of relying on one exact column order.
