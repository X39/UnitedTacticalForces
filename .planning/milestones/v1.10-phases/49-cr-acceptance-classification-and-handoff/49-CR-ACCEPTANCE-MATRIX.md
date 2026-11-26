# CR-0001 Acceptance Matrix

## Purpose

This artifact maps CR-0001 acceptance criteria, v1.10 requirements, and Phase 30 OPS/EVID gates to captured evidence and missing evidence. It is a classification record, not a live acceptance pass.

No CR acceptance pass is claimed.

No Phase 30 pass is claimed.

Provider teardown remains the cost-stop boundary.

Runtime stop and runtime cleanup do not stop provider billing.

Final provider-side billable resource count `0` was not proven by this automated run.

## Acceptance Classification Labels

| Label | Meaning |
|-------|---------|
| implementation_ready | Product implementation or evidence surface is present and guarded by automated tests/build/source checks. |
| human_needed | Operator, authenticated browser, provider, runtime, cleanup, or redaction evidence is required. |
| blocked | A criterion cannot pass until upstream human-needed evidence exists. |
| deferred | The criterion is outside the current live evidence pass or intentionally carried forward. |
| out_of_scope | The item is excluded by CR-0001 or v1.10 boundaries. |

## Overall Classification

| Item | Classification | Reason |
|------|----------------|--------|
| CR-0001 final acceptance | blocked | Live provisioning, live runtime, live provider teardown, final provider-side billable count `0`, authenticated browser walkthrough, and full evidence redaction review were not supplied. |
| Phase 30 classification | blocked-pending-operator-input | `.planning/v1.7-LIVE-UAT-EVIDENCE.md` still requires operator approval, authenticated sessions, provider readiness, billing owner, dry-run, cost, policy, reconciliation, runtime, cleanup, final billable count, and redaction evidence. |
| v1.10 milestone scope | implementation_ready | The milestone produced evidence surfaces, tests, matrix, and handoff without claiming blocked live acceptance. |

## CR-0001 Overall Acceptance Criteria

| # | Acceptance Criterion | Classification | Captured Evidence | Missing Or Blocked Evidence | Follow-Up |
|---|---------------------|----------------|-------------------|-----------------------------|-----------|
| 1 | A user can create an Arma 3 server from the frontend. | implementation_ready / human_needed | v1.8 guided entry, dry-run review, policy gate, provisioning UX, and v1.9 template/profile work provide the implementation path. Phase 45-46 preflight/provisioning evidence rows are present. | Authenticated server-owner browser walkthrough for the approved live scenario was not captured. | Operator runs authenticated `/servers` walkthrough with the selected server, billing owner, template/profile, and dry-run plan. |
| 2 | The system provisions required cloud infrastructure without manual SSH work. | implementation_ready / blocked | Provider provisioning routes, reviewed-plan gates, preflight readiness, and provisioning evidence rows are implemented and guarded. | Actual Hetzner provisioning was not executed in this automated run. | Operator executes exactly one approved provisioning path or records the live blocker. |
| 3 | The server can be configured, started, stopped, updated, and deleted from the frontend. | implementation_ready / blocked | Runtime action facade, lifecycle job visibility, runtime evidence rows, and provider teardown action rows are present. | Live worker bootstrap, Steam/Arma install/configure/start/stop/update, and live provider teardown were not executed. | Operator captures live runtime action evidence or an environment blocker, then captures provider teardown evidence. |
| 4 | The public connection details are visible in the frontend. | implementation_ready / human_needed | Provisioning/runtime evidence surfaces include cloud session endpoint host/port and selected-server visibility. | No live endpoint was created or captured. | Operator records endpoint evidence after approved provisioning. |
| 5 | Logs and lifecycle state are visible in the frontend and survive API restarts. | implementation_ready / human_needed | Durable lifecycle jobs, worker/runtime evidence rows, recent logs, and visibility panels are implemented and tested across previous milestones. | Live worker logs and restart/recovery evidence for the approved cloud session were not captured. | Operator records live worker/job/log evidence and any restart recovery observations. |
| 6 | Every billable resource has a visible owner while it exists. | implementation_ready / human_needed | Billing owner assignment, cost responsibility, provisioning owner attribution, and provider-resource evidence rows are implemented. | No live provider resources were created in this run, so live owner attribution was not observed on real billable resources. | Operator verifies owner attribution during provisioning, partial failure if any, runtime, and cleanup. |
| 7 | The owner can see estimated running cost and historical accumulated cost. | implementation_ready / human_needed | Cost estimate, running-cost, accumulated-cost, reconciliation, and cost-responsibility visibility were implemented across v1.8-v1.10. | Authenticated owner view against a live running session was not captured. | Operator captures `/servers` owner cost view for the approved session. |
| 8 | Administrators can audit ownership, lifecycle, and cleanup events. | implementation_ready / human_needed | Audit records and sanitized admin/server-owner visibility are implemented and exposed through evidence rows. | Authenticated administrator view and live audit records were not captured. | Operator captures `/servers/cloud` and `/servers` audit evidence with secrets redacted. |
| 9 | Failed or orphaned cloud resources are visible and actionable. | implementation_ready / human_needed | Partial-failure, cleanup-risk, unknown provider resource, orphan-risk, manual fallback, and remaining billable evidence rows are implemented. | Actual provider-side failure/orphan/duplicate inspection and cleanup evidence were not supplied. | Operator records provider reconciliation and cleanup evidence after provisioning/teardown or records the blocker. |

## v1.10 Requirement Classification

| Requirement | Classification | Evidence | Missing Evidence |
|-------------|----------------|----------|------------------|
| LIVE-01 | implementation_ready / human_needed | Approval package recording surface, budget/cleanup-window fields, selected server, billing owner, dry-run, policy, and preflight rows exist. | Real approver, project, budget, lifetime, cleanup window, and selected scenario evidence. |
| LIVE-02 | implementation_ready / human_needed | Authenticated admin and server-owner evidence surfaces exist and avoid session secret capture. | Real authenticated `/servers/cloud`, `/servers`, and API access evidence. |
| LIVE-03 | implementation_ready / human_needed | Provider, metadata, owner, dry-run, cost, policy, reconciliation, and readiness gates are implemented. | Live provider configuration, fresh metadata, selected readiness, and reconciliation evidence. |
| PROV-01 | implementation_ready / blocked | Reviewed provisioning path and evidence rows exist. | Actual live provisioning result, cloud session id, provider resource identifiers, endpoint, audit, and owner/cost evidence. |
| PROV-02 | implementation_ready / blocked | Partial-failure, retry, cleanup-risk, duplicate/orphan, and remaining billable evidence rows exist. | Actual failure/partial-provisioning, retry, provider inspection, and hidden-resource evidence. |
| RUNT-01 | implementation_ready / blocked | Worker bootstrap, heartbeat, capability, Steam/Arma action, log, and blocker evidence rows exist. | Actual worker bootstrap, Steam/Arma install/configure/start/stop/log evidence or explicit environment blocker. |
| RUNT-02 | implementation_ready / blocked | Admin/owner runtime visibility, cost, policy, reconciliation, owner, template/profile, audit, and redaction rows exist. | Authenticated admin and owner runtime walkthrough evidence. |
| CLN-01 | implementation_ready / blocked | Provider teardown, manual fallback, provider-side count, final billable count, unknown resource, and current job rows exist. | Actual provider teardown, manual cleanup fallback, provider-side counts, and final billable resource count `0`. |
| CLN-02 | implementation_ready / blocked | Redaction review rows and verification guard tests exist. | Full redaction review of approval, browser, provider, worker/runtime, logs, screenshots, comments, and exports. |
| ACC-01 | in_progress | This acceptance matrix maps criteria and missing evidence. | Final handoff and verification are completed in Plan 49-02. |

## Phase 30 And OPS/EVID Classification

| Gate | Classification | Current Evidence | Required Before Pass |
|------|----------------|------------------|----------------------|
| Phase 30 | blocked-pending-operator-input | v1.7 live evidence template, v1.8 handoff, and v1.10 evidence-surface artifacts exist. | Operator approval, authenticated sessions, provider readiness, billing owner, dry-run, cost, policy, reconciliation, provisioning, runtime, cleanup, final billable count `0`, and redaction evidence. |
| OPS-02 | blocked | Frontend provisioning/runtime/cleanup path and evidence surfaces are implemented. | Authenticated Aspire/API browser UAT covering live provisioning, metadata sync, plan selection, server-admin navigation, runtime install/start/stop, cleanup, and final provider billable count `0`. |
| OPS-03 | human_needed | Approval/preflight recording surfaces and readiness gates are implemented. | Completed operator approval package and ready-for-live-execution decision. |
| OPS-04 | blocked | Runtime action and evidence surfaces exist. | Live Steam/Arma runtime UAT or explicit operator/environment blocker. |
| EVID-01 | blocked | Evidence rows exist for provider status, metadata, dry-run, resources, worker, runtime, cost, policy, reconciliation, owner, audit, teardown, and cleanup. | Actual live evidence values for the approved scenario. |
| EVID-02 | human_needed | Redaction guardrails and verification tests exist. | Full evidence package redaction review for screenshots, logs, comments, and exports. |
| EVID-03 | blocked | Cleanup and provider-count evidence surfaces exist. | Automated teardown result, manual cleanup if needed, provider-side deletion confirmation, remaining billable count, and final provider billable count `0`. |

## Follow-Up Summary

| Priority | Action | Owner |
|----------|--------|-------|
| 1 | Fill approval, budget, cleanup-window, provider readiness, billing owner, dry-run, cost, policy, and reconciliation fields in `.planning/v1.7-LIVE-UAT-EVIDENCE.md`. | Operator |
| 2 | Run authenticated administrator and server-owner browser walkthroughs and record safe evidence. | Operator |
| 3 | Execute exactly one approved live provisioning path or record the live environment blocker. | Operator |
| 4 | Capture worker bootstrap, Steam/Arma runtime, endpoint, logs, cost, policy, reconciliation, owner, and audit evidence. | Operator |
| 5 | Execute provider teardown, record manual cleanup fallback if needed, and prove final provider-side billable resource count `0`. | Operator |
| 6 | Complete full redaction review before any Phase 30 or CR acceptance pass classification. | Operator / reviewer |

## Result

ACC-01 matrix mapping is present. Final ACC-01 completion depends on the Plan 49-02 handoff and verification artifact.

CR-0001 final acceptance remains blocked.
