# Server Command Center Redesign Concepts - 2026-06-23

## Contract Basis

This redesign follows `.planning/ui-reviews/2026-06-22-cinematic-dual-shell-ui-contract.md`.

The server area stays in the Command Center shell and uses the accepted dark/gold cinematic operations UI. Dense operational data sits on solid charcoal panels, gold is reserved for selection and primary intent, and destructive/cost-changing actions move behind explicit review surfaces.

## Information Architecture

The current single `/command/servers` page mixes fleet scanning, create flows, runtime control, cloud planning, logs, files, configuration, ownership, audit, and danger actions. The redesign splits that into addressable surfaces:

- `/command/servers` - fleet hub, attention metrics, search, table, and links into server workspaces.
- `/command/servers/create` - local server creation and billing-owner creation.
- `/command/servers/{id}` - selected server overview.
- `/command/servers/{id}/runtime` - start, stop, restart, install, update, log collection, log download, and log clearing.
- `/command/servers/{id}/cloud` - cloud runtime visibility, policy/cost summary, dry-run planning entry, and link to global cloud operations.
- `/command/servers/{id}/configuration` - title, selected mod pack, and controller configuration editor.
- `/command/servers/{id}/files` - server folder browser, uploads, and file deletion.
- `/command/servers/{id}/jobs` - durable lifecycle job history.
- `/command/servers/{id}/ownership` - active billing owner, assignment history, and owner transfer.
- `/command/servers/{id}/audit` - hosting audit records for this server.
- `/command/servers/{id}/danger` - server deletion and runtime cleanup.

The legacy `/servers` page remains available as a fallback while the split routes take over the command-center entry point.

## UX Concepts

### Fleet Hub

Attention-first dashboard with four metrics: servers, active jobs, stale workers, and cleanup risk. The main table is for scanning and selection, not editing. Runtime state, owner, latest job, and route actions are visible without row-icon overload.

### Server Workspace

Every server route shares a compact selected-server header and tabs. The operator always sees runtime status, owner, controller, and version before acting.

### Runtime

Low-risk lifecycle actions are direct buttons with disabled states. Logs are visible on the same page, with download and clear actions separated.

### Cloud

Cloud planning is a server-specific workflow. It exposes active owner assignment, template/runtime selectors, provider shape fields, dry-run generation, runtime/cost/policy evidence, and a direct hand-off to Cloud Operations.

### Configuration And Files

The old edit and file features return as separate pages instead of expansion rows. Configuration groups are editable with inline save feedback. Files are grouped by server folder with upload/delete controls.

### Ownership And Danger

Billing ownership becomes an explicit page with current owner, transfer reason, and history. Destructive actions are not row icons; they live in the Danger Zone with typed confirmation.

