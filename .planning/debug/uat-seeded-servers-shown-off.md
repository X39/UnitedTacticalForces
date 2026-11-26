---
status: diagnosed
trigger: "Phase 67 UAT gaps G-67-1 and G-67-3: seeded 'running' UAT servers (UAT 2 Running, UAT 10 Metrics, ...) are shown as off; only UAT 5, 7 and 9 keep their seeded look."
created: 2026-11-22T17:30:00Z
updated: 2026-11-22T17:35:00Z
goal: find_root_cause_only
audit_acknowledged:
  milestone: v2.1
  at: 2026-11-26
  status: diagnosed
---

## Current Focus

hypothesis: "CONFIRMED. The HostingCostReconciliationWorker tries to charge a provider-cost slice for every seeded PossibleCost deployment that has no current CostAuthorizationExpiresAt. The seeded UAT cost owner has Balance 0 (minus 1.30 EUR from the UAT 9 slice) and no Allowance, so AuthorizeNextSliceAsync throws HostingAffordabilityException and AutomaticCostStopService queues a Terminate (TerminatingDueToFault). The emulated provider releases the emu-server-* resources, so the deployments resolve and the servers show as off."
bug_class: "Bohrbug (deterministic: happens on every fresh UAT start, first reconciler pass)"
test: "Live UAT DB inspection (done) plus the prediction that UAT 9 is cost-stopped the same way once its pre-charged authorization (16:34:32 UTC) comes within the 30 s renewal window."
expecting: "UAT 9 deployment -> TerminatedDueToFault, FaultCode cost-owner-credit-exhausted, shortly after 16:34:02 UTC (observed 16:34:10)."
next_action: "Diagnosis complete; hand the fix (fund the UAT cost owner + recurrence test) to plan-phase --gaps."

reasoning_checkpoint:
  hypothesis: "Seeded running servers go off because the real cost reconciler cost-stops every PossibleCost deployment billed to the unfunded UAT cost owner (Balance <= 0, no Allowance, safety range 5 EUR), and the emulated provider then verifies the release so the deployment resolves."
  confirming_evidence:
    - "Live DB: UAT 2/3/4/6/10 deployments TerminatedDueToFault with FaultCode cost-owner-credit-exhausted, resolved 9 s after seeding; owner Balance -1.3250, Allowance NULL."
    - "Audit trail: six 'Confirmed deployment fault queued automatic provider cost-stop.' records at 16:19:39 for exactly UAT 10, 7, 4, 6, 3, 2, followed by verified emu-server-* releases."
    - "UAT 5 (PossibleCost=false), UAT 7 (emu-stuck- release never verifies) and UAT 9 (pre-charged until seed + 15 min) are exactly the servers not shown off."
  falsification_test: "If UAT 9 stays Running after its authorization expires while the owner is still unfunded, the hypothesis is wrong; likewise if a funded owner still saw cost-stops."
  fix_rationale: "Funding the UAT owner (or pre-authorizing every seeded PossibleCost deployment) removes the affordability failure that triggers the cost-stop, while keeping the product's fail-closed billing intact."
  blind_spots: "The funded-owner variant was not executed end to end; cost alert thresholds and finance pages that read the owner balance may need the UAT expectations adjusted."
  candidate_causes:
    - "data: the UAT cost owner is seeded with Balance 0 / no Allowance (confirmed)"
    - "config: the UAT profile unpauses the control plane and only neutralises the health reconciler, not the cost reconciler (contributing)"
    - "code: frontend state mapping or 67-21 metrics fix (eliminated)"
    - "environment: emulated provider forgetting seeded resources (eliminated; it reports emu-server-* as allocated and only releases them on request)"
  and_gate: "yes - the cost-stop needs (a) an unfunded owner AND (b) the cost reconciler running against seeded PossibleCost deployments without a current authorization; the visible 'off' additionally needs (c) the emulated provider verifying the release (which is correct behaviour). Fixing (a) is sufficient and least invasive."

## Symptoms

expected: "'UAT 10 Metrics' and 'UAT 2 Running' (and the other seeded in-flight states) keep their seeded state after `./scripts/Start-Uat.ps1 -Fresh`; only intentionally off servers (UAT 1, UAT 8) show as off."
actual: "All UAT servers except UAT 5 (failed), UAT 7 (unconfirmed stop) and UAT 9 (costing) are shown as off. UAT 10 metrics history shows a gap, so it was running at some point and then went off."
errors: "None reported in the UI."
reproduction: "./scripts/Start-Uat.ps1 -Fresh, open http://localhost:5173/next/command/servers, look at the server cards."
started: "Observed during the phase 67 UAT re-run after the 67-21 fix (2026-11-22)."

## Eliminated

## Evidence

- timestamp: 2026-11-22T17:30:00Z
  checked: "Knowledge base"
  found: ".planning/debug/knowledge-base.md does not exist; no MemPalace match attempted."
  implication: "No known-pattern candidate."

- timestamp: 2026-11-22T17:32:00Z
  checked: "sources/Api/Data/UatFleetScenarioSeeder.cs"
  found: "Scenarios: 1 no deployment; 2 Running PossibleCost=true; 3 WaitingForWorker true; 4 Stopping true; 5 Failed PossibleCost=false; 6 PreservationFailed true; 7 Stopped true (emu-stuck- resource); 8 Terminated false; 9 Running true + AccruedCost (CostAuthorizationExpiresAt = seed + 15 min, owner Balance -= 1.30); 10 Running true + Metrics (online worker + history). No other scenario sets CostAuthorizationExpiresAt or a cost reservation."
  implication: "The only deployments that keep their seeded look (5, 7, 9) are exactly the ones a cost-slice reconciler would not stop or could not finish stopping."

- timestamp: 2026-11-22T17:33:00Z
  checked: "sources/Api/Data/DebugDemoDataSeeder.cs EnsureDebugCostOwnersAsync; sources/Database/Hosting/CostOwner.cs"
  found: "The debug cost owners are created without Balance or Allowance, so Balance = 0 and Allowance = null. The UAT 9 slice then lowers the owner balance to -1.30 EUR."
  implication: "The UAT owner can never afford a new slice: Hosting:Finance:MinimumSafetyRangeEur is 5.0."

- timestamp: 2026-11-22T17:34:00Z
  checked: "HostingCostReconciler.ReconcileAsync, HostingCostSliceService.AuthorizeNextSliceAsync, AutomaticCostStopService.TriggerAsync"
  found: "The reconciler selects every unresolved PossibleCost deployment with CostAuthorizationExpiresAt null or <= now + 30 s and calls AuthorizeNextSliceAsync(enforceAffordability: !IsProvisioningBlocked). With Balance -1.30 + Allowance 0 - 0.025 < 5.0 it throws HostingAffordabilityException; the reconciler catches it and calls AutomaticCostStopService.TriggerAsync(..., 'cost-owner-credit-exhausted'), which sets Status=TerminatingDueToFault, IsProvisioningBlocked=true and queues a Terminate job with Priority int.MaxValue."
  implication: "Every seeded running/in-flight deployment with PossibleCost=true and no pre-charged slice is cost-stopped on the first reconciliation pass (30 s cadence)."

- timestamp: 2026-11-22T17:36:00Z
  checked: "HostingControlPlaneServiceCollectionExtensions + sources/AppHost/Program.cs (UAT profile)"
  found: "HostingCostReconciliationWorker (with GameServerControlPlaneWorker, ProviderResourceReconciliationWorker, ...) is registered whenever the control plane is not paused. The UAT profile sets Hosting__ControlPlane__Paused=false (since eab7b5fa, 2026-11-19). The UAT profile only swaps IDeploymentHealthReconciler for a no-op (EmulatedDeploymentHealthReconciler, documented as preventing seeded servers from being stopped as 'worker missing'); there is no equivalent guard for the cost reconciler."
  implication: "The seeded-state protection is incomplete: the health reconciler was neutralised, the cost reconciler was not."

- timestamp: 2026-11-22T16:30:00Z
  checked: "Live UAT database of the user's running session (podman container pg-database-npqfqcma, volume x39.unitedtacticalforces.apphost-40d8a6f9a5-pg-database-data), read-only psql queries"
  found: "Seeded at 16:19:32 UTC. GameServerDeployments: UAT 2, 3, 4, 6, 10 -> Status 9 TerminatedDueToFault, Stage terminated-due-to-fault, FaultCode cost-owner-credit-exhausted (UAT 6 keeps its seeded fault code via ??=), PossibleCost=false, TimeStampResolved 16:19:41. UAT 7 -> Status 8 TerminatingDueToFault, Stage emulated-unconfirmed, still PossibleCost. UAT 5 -> Failed (untouched). UAT 8 -> Terminated (untouched). UAT 9 -> Running, CostAuthorizationExpiresAt 16:34:32. Cost owner X39: Balance -1.3250, Allowance NULL, not frozen."
  implication: "Direct observation: the seeded deployments were terminated by the system 9 seconds after seeding; this is not a frontend mapping problem."

- timestamp: 2026-11-22T16:30:00Z
  checked: "HostingAuditRecords of the same session"
  found: "16:19:39 six records 'Confirmed deployment fault queued automatic provider cost-stop.' FaultCode=cost-owner-credit-exhausted for UAT 10, 7, 4, 6, 3, 2. 16:19:40-41 the Terminate jobs are claimed; emu-server-* releases are 'verified non-billable'; 'Deployment terminated after a fault' for UAT 2, 3, 4, 6, 10. UAT 10 additionally logs 'Fault diagnostics and preservation were unavailable; provider cost-stop ...'. UAT 7's emu-stuck-* release 'could not be verified' and the job is retried every 30 s (still running at 16:30). 16:20:09 a 0.0250 EUR slice is charged to UAT 7 without affordability enforcement because IsProvisioningBlocked is now true."
  implication: "Confirms the full causal chain: cost reconciler -> affordability failure -> automatic cost-stop -> emulated release -> deployment resolved -> server off. Explains exactly why 5, 7 and 9 differ from the rest."

- timestamp: 2026-11-22T17:40:00Z
  checked: "GameServerProjectionService.ActiveDeployment + sources/ReactApp/src/features/hosting/serverState.ts"
  found: "The API projects only the unresolved deployment (TimeStampResolved == null). A resolved TerminatedDueToFault deployment is therefore not sent; the React deriveServerState gets deployment = null, possibleCost = false, pendingJobCount = 0 and returns 'off'. UAT 5 (Failed, unresolved) -> attention; UAT 7 (TerminatingDueToFault, unresolved, PossibleCost) -> stopping with live cost; UAT 9 (Running) -> running."
  implication: "The frontend mapping is correct; it faithfully renders the backend state. The 'all but 5, 7 and 9 off' pattern is exactly the DB state above."

- timestamp: 2026-11-22T17:41:00Z
  checked: "Phase 67-21 fix commits 4cc52330, 431cce9f, fff84aa6, 4fb5d677"
  found: "They only touch ReactApp metricsFrames.ts, metricsModel.ts, useServerMetrics.ts, FleetLiveUpdatesProvider.tsx and their tests; none touches fleet state derivation, seeding, or the cost reconciler."
  implication: "The 67-21 fix is not the cause."

- timestamp: 2026-11-22T17:42:00Z
  checked: "Git history of the UAT harness"
  found: "eab7b5fa (2026-11-19) added the emulated Hetzner UAT profile with Hosting__ControlPlane__Paused=false and swapped only the health reconciler. 8f3b38b9 (2026-11-21) added UAT 9 and its -1.30 EUR charge. 6169302e (2026-11-22) added UAT 10. The debug cost owner never had credit, so the cost-stop was latent since eab7b5fa; UAT 9 only survives because it was pre-charged. tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs asserts owner.Balance == -1.3000m after seeding, i.e. it pins the unfundable state."
  implication: "This is a UAT seed/harness defect present since the UAT profile was created, made visible in phase 67 because UAT 10 must stay running for the metrics UAT."

- timestamp: 2026-11-22T17:43:00Z
  checked: "ServerMetricBuckets / ServerMetricSnapshots of UAT 10 in the live DB; UatWorkerChannelEmulator + options"
  found: "Seeded buckets end at 16:19:15 (15 s), 16:18 (60 s), 16:10 (300 s); zero live snapshots were ever stored. The emulator waits StartDelay 5 s after host start, then looks for an unresolved UAT 10 deployment; the cost reconciler's first pass (16:19:39) and the Terminate job (resolved 16:19:41) finish before that, so the emulator logs 'found no running ... server' and never starts."
  implication: "The metrics gap reported in test 2 (no data after the seed instant, server shown off) is the same root cause, not a metrics-pipeline bug."

- timestamp: 2026-11-22T16:34:11Z
  checked: "Falsification test: UAT 9 in the live DB after its pre-charged authorization (16:34:32) entered the 30 s renewal window"
  found: "UAT 9 deployment -> Status 9 TerminatedDueToFault, Stage terminated-due-to-fault, FaultCode cost-owner-credit-exhausted, TimeStampResolved 16:34:10.32 UTC (the reconciler pass at about 16:34:09)."
  implication: "Prediction confirmed. UAT 9 is not an exception, only delayed by its 15-minute pre-charge; the user saw it as on because they looked within 15 minutes of startup. With an unfunded owner, every seeded PossibleCost server is cost-stopped."

## Resolution

root_cause: "UAT seed/harness defect: the UAT profile runs the real HostingCostReconciliationWorker (control plane unpaused), but UatFleetScenarioSeeder bills every seeded server to the debug cost owner, which DebugDemoDataSeeder creates with Balance 0 and no Allowance (and UAT 9 lowers it to -1.30 EUR). On its first pass (~7 s after startup) the reconciler cannot fund a slice for any PossibleCost deployment without a pre-charged authorization, throws HostingAffordabilityException and queues an automatic cost-stop (cost-owner-credit-exhausted). The emulated provider verifies the emu-server-* releases at once, the deployments resolve as TerminatedDueToFault, the API stops projecting them, and the React fleet shows the servers as off. UAT 5 (PossibleCost=false) and UAT 7 (emu-stuck-* release never verifies, so it is stuck in TerminatingDueToFault instead of its seeded Stopped state) do not end up off; UAT 9 survives only until its pre-charged authorization nears expiry (seed + ~14.5 min) and is then cost-stopped the same way."
fix: "Not applied (goal: find_root_cause_only)."
verification: ""
files_changed: []
oracle_type: ""

suggested_fix_direction:
  - "Fund the UAT cost owner in UatFleetScenarioSeeder (seed-time Balance or Allowance comfortably above MinimumSafetyRangeEur 5.00 EUR plus several hours of slices for all PossibleCost scenarios, e.g. a credit ledger entry so the balance stays auditable), applied idempotently, so the real cost reconciler keeps charging slices instead of cost-stopping. Keep UAT 9's -1.30 EUR slice; adjust UatFleetScenarioSeederTests.CostingScenario_ChargesOneBackdatedSliceAgainstTheOwnerBalance, which currently pins Balance == -1.3000m."
  - "Optionally give each seeded running/in-flight PossibleCost scenario a current authorization (CostAuthorizationExpiresAt) like UAT 9, so the first reconciler pass is not a cliff."
  - "Add a recurrence guard: a test that seeds the UAT scenarios and runs HostingCostReconciler.ReconcileAsync once, asserting no automatic cost-stop is queued and UAT 2/3/4/6/7/10 keep their seeded status."
  - "Do not neutralise the cost reconciler in the UAT profile (unlike the health reconciler): the cost-stop path is product behaviour UAT should still be able to exercise; the defect is the unfunded seed owner, not the reconciler."
