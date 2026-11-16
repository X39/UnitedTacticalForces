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

Process outcomes do not prove that an HC has joined or occupied a mission slot. Runtime join detection and live-engine
connectivity evidence are not acceptance gates for this ticket. A later manual run should verify separate-host and
same-host connectivity, password/mod compatibility, mission HC slot occupation, restoration with stale addresses,
and reconnection after an authorized restart. If that run exposes a connectivity problem, add an Arma-specific probe
based on verified engine evidence without introducing Arma fields into generic workload contracts.
