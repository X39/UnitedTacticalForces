# Arma admission restoration and recovery

The Arma worker reconciles `headlessClients[]` and `localClient[]` immediately before launching the primary process,
after persistent configuration restoration and content installation. These directives are worker-owned: stale and
duplicate declarations are replaced, including when the current client list is empty. Other restored settings and
comments are preserved. Ambiguous managed syntax, unresolved preprocessor directives, and invalid endpoint addresses
fail preparation before launch rather than retaining uncertain authorization.

Admission uses the immutable command topology. A colocated HC endpoint uses `127.0.0.1` for both the allowlist and
connection target; only loopback addresses receive `localClient[]` privileges. Workload-to-host assignment and grouped
process supervision belong to the generic supervision implementation. Auxiliary workers do not restore or publish
primary-owned persistent data, including during Restart.

Before launching, the worker atomically records a non-secret topology fingerprint outside persistent game storage.
An existing process can be adopted during Start or command redelivery only when its applied topology matches. Missing
state remains compatible with historical empty-topology primary commands; otherwise unknown or changed topology
requires an explicit Restart. A new authorized Restart applies its new endpoints after stopping the old process.
Redelivery never substitutes current provider addresses into an already delivered command. Configuration is not
rewritten beneath a running process and presented as if the engine had reloaded it.

## Engine-confirmed Headless Client readiness

Every primary launch automatically packages the embedded `Arma3JoinAddonAssets` into
`server-instance/@utf_hc_join/addons/utf_hc_join.pbo` and supplies it through `-serverMod`. The addon also writes a
nonce-bound `UTFPL` record with the human player count (headless clients and the server excluded) on change and at
least every 30 seconds; the worker treats it as fresh for 90 seconds and reports it as the Arma 3 player observation.
Readiness evidence below applies only when a primary has an HC in its frozen workload plan.
This server-only addon requires Arma 3 2.06 or later. It does not change the user's modpack, disable signature checks,
require a client addon, or transfer AI. The selected mission must still support a playable HC slot and AI delegation.

The addon initializes through `CfgFunctions`, observes user connection/state/disconnection events, and reconciles
`allUsers` every two seconds. Readiness requires exactly one engine-classified HC named `UTFHC1`, network owner above
two, and client state exactly ten (ready to play). Ordinary players, login/lobby states, duplicate matches and
debriefing do not satisfy the check. Mission initialization/end invalidates old evidence. The sequence is retained
across missions in `uiNamespace` and incremented without scheduled-script interruption.

Each primary preparation creates a new random, non-secret launch marker. Bounded `diag_log` records carry this marker,
sequence, owner and readiness. Linux stdout/stderr are appended to the workload's `arma3-runtime.log` through inherited
file descriptors, opened after privilege dropping; capture survives worker restarts. Windows reads bounded RPT tails
under the controlled profile directory. Complete records with no timestamp, short timestamps, or full date/time
timestamps are recognized. Truncated lines, prior launch markers and replayed sequences cannot establish freshness.
Diagnostic collection continues to redact sensitive content through the existing log collection path.
The native output file remains deployment-local and append-only; bounded reads do not rotate or cap its disk usage.

Primary Start deliberately completes after process launch so the control plane can start a separate HC. Only after
all workload launch commands finish does the API send the durable `verify-readiness` command to the primary worker.
This order applies to both placements, Restart and running maintenance. Stopped maintenance does not require a join.
The optional readiness action is generic game-definition metadata; all parsing, instrumentation and HC semantics stay
in the Arma worker. No shared worker contract or provider-specific path was added.

Verification requires a new positive record after the verification baseline and the same observed primary process
incarnation throughout the wait. The default deadline is 180 seconds, configurable with
`UTF_ARMA3_HC_JOIN_TIMEOUT_SECONDS` (positive integer, capped at 900). Failure uses normal lifecycle fault teardown.
Restart must reinstall instrumentation before an older, uninstrumented process can be verified.

Successful verification persists its primary PID and creation time outside preserved game files. Heartbeats then
require advancing positive evidence within `UTF_ARMA3_HC_EVIDENCE_GRACE_SECONDS` (default 30, capped at 300).
Worker reconstruction starts one bounded grace period and requires a new sequence to renew it. Expiry reports the
primary workload as `Failed` while retaining its real PID; existing required-workload health reconciliation performs
cost-stop and diagnostic collection. New launch preparation disarms the old observation before a replacement process
starts. Brief mission transitions have the same grace; longer transitions may intentionally fail readiness.

This is trusted game-runtime telemetry, not authentication against hostile mission/addon code, which shares the SQF
and diagnostic environment. The marker is not a credential. No native extension, inbound port, remote execution
permission or API/worker secret is exposed to the addon. Join detection does not prove AI ownership or workload offload.

Automated tests cover packaging, parser bounds, fresh/stale evidence, process incarnation changes, cancellation,
recovery and lifecycle failure cleanup. Actual addon execution, native Linux capture and both placements still require
the separately authorized live acceptance run; unit tests do not claim engine validation.

Implementation references: [Functions library](https://community.bistudio.com/wiki/CfgFunctions),
[getUserInfo](https://community.bistudio.com/wiki/getUserInfo),
[client states](https://community.bistudio.com/wiki/getClientState),
[mission handlers](https://community.bistudio.com/wiki/Arma_3:_Mission_Event_Handlers),
[Linux RPT output](https://community.bistudio.com/wiki/arma.RPT), and
[PBO format](https://community.bistudio.com/wiki/PBO).

## Colocated process supervision

A same-host placement keeps one outbound worker and assigns both `game-server/0` and `headless-client/0` to it.
Generic assignments and process observations travel through the existing command and heartbeat APIs. Shared contracts
contain no Arma launch arguments or join semantics. The API persists observations and cannot substitute a primary PID
for a missing companion on a multi-workload host. Historical single-workload workers retain singular-PID compatibility.

The worker prepares one shared installation with the union of selected content while both processes are stopped, then
publishes its completed preparation phase atomically. Each launch uses role-filtered mod arguments, independent process
identity, and separate mutable profiles and logs. The primary launches first; the HC follows using loopback. Successful
launch-command completion means both processes are observed running; the subsequent readiness command must confirm the join.

Stop attempts both processes in dependent-first order and publishes preservation only after both exits are verified.
Restart and maintenance stop all consumers before changing shared files. Maintenance of a stopped group does not start it.
Partial startup failure compensates both processes. Worker recovery resumes durable command phases and verifies recorded
incarnations instead of restarting a surviving primary merely because HC startup was interrupted. An uncertain spawn
retains its launch intent and fails closed; provider teardown remains the independent cleanup authority.

Heartbeat observations do not race group operations when stopping an orphaned dependent. Idle primary loss stops the HC;
other required-process loss feeds the existing control-plane fault/cost-stop policy without an autonomous restart loop.
Process gaps during authorized restart or maintenance are expected, while stale/offline workers remain faults.
