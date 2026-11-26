# Phase 48 Research

## Objective

Identify what already supports provider teardown, cleanup, final billable-count evidence, and redaction review without inventing live provider facts.

## Findings

### R-48-01: The selected-server provider teardown panel already exists

`sources/WebApp/Pages/Servers/Index.razor` renders `Provider teardown` when a selected cloud session id is available. The panel already displays selected session id, remaining billable resources, cleanup state, current job, cost-stop warning copy, a teardown button, and a billable-resource refresh button.

### R-48-02: Provider teardown actions are repository-backed

Provider teardown flows through `ConfirmTeardownProviderResourcesAsync`, `ExecuteCloudWorkflowActionAsync`, and `TeardownCloudSessionResultAsync`. The Razor page does not need raw HTTP or generated client builders.

### R-48-03: Existing workflow/cost/status models expose safe cleanup evidence

Existing helpers already derive cleanup risk, cleanup-needed count, remaining billable count, unknown provider resource count, orphan risk, retry evidence, audit records, reconciliation summaries, and redaction-blocked state from safe WebApp facade models.

### R-48-04: Final billable-resource closure is a provider-side live evidence boundary

Automated UI/source checks can prove rows, gates, and redaction boundaries. They cannot prove a Hetzner account has zero remaining billable resources without live provider inspection or operator-supplied evidence.

### R-48-05: Resource-kind counts need stable display labels

The workflow/status resource projections include kind labels. Phase 48 can expose counts for servers, floating IPs, volumes, firewalls, and total provider-side resources using safe resource summaries, while classifying absent live evidence as human-needed.

### R-48-06: Redaction review already has guard patterns

Phase 46/47 tests use forbidden marker arrays and verification-artifact scans. Phase 48 should extend the same guard pattern for cleanup evidence and avoid raw secret-bearing transport terms in UI and verification artifacts.

## Implications

- Add teardown evidence rows and tests rather than live provider teardown execution.
- Preserve existing teardown action gates and repository boundaries.
- Record automated UI/source evidence separately from live provider-side cleanup evidence.
