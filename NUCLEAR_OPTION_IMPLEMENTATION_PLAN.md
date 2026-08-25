# Nuclear Option dedicated-server support

## Outcome

Add Nuclear Option as a fully supported cloud game-server implementation without creating a parallel hosting path.
The implementation must use the existing persistent control plane, dedicated external worker, provider-neutral runtime
contracts, configuration profiles, normalized content model, preservation workflow, secrets, authorization, audit,
storage estimation, and generic WebApp screens.

Full support means:

- create and operate a Nuclear Option server through the existing server UI;
- install and update the official Linux dedicated server through anonymous SteamCMD;
- start, observe, restart, gracefully stop, preserve, and tear down a deployment safely;
- configure every documented `DedicatedServerConfig.json` setting exposed by the public server;
- select built-in, uploaded, and Steam Workshop missions, including mission rotation;
- manage join passwords and other sensitive values without exposing them to the browser, logs, or command output;
- collect redacted server logs and report runtime/storage state;
- use the official remote-command protocol for status, mission control, messaging, kicking, and bans;
- make every mutating operator command authorized, durable, redeliverable, and auditable;
- retain provider cleanup and cost-stop guarantees if the game or worker is unhealthy.

Authoritative external references:

- [Shockfront Studios dedicated-server guide](https://github.com/Shockfront-Studios/Nuclear-Option-Server-Tools/blob/main/DedicatedServerGuide.md)
- [Shockfront Studios remote-command protocol](https://github.com/Shockfront-Studios/Nuclear-Option-Server-Tools/blob/main/ServerCommands/Readme.md)

The researched public runtime values are Steam application `3930080`, Workshop application `2168680`, Linux
executable `NuclearOptionServer.x86_64`, UDP game port `7777`, UDP query port `7778`, and optional internal TCP
remote-command port `7779`.

## Architectural decisions

1. Use `nuclear-option` as the stable game identifier. Do not use the Steam app id as a domain identity.
2. Keep the existing single dedicated worker node. Nuclear Option does not require a secondary service topology.
3. Install the runtime at command execution time with anonymous SteamCMD. Do not embed game files in the worker image.
4. Publish only UDP `7777` and `7778`. The unauthenticated remote-command listener must remain container-internal and
   must never appear in provider firewall or Docker-publish rules.
5. Treat a Scenario as a Nuclear Option mission. Its source maps as follows:
   - `BuiltIn` -> the server's `BuiltIn` mission group;
   - `SteamWorkshop` -> the `Workshop` group and Workshop item id;
   - `UploadedArtifact` -> a validated `User` mission installed under the configured mission directory.
6. Store ordered mission rotation separately from scalar configuration values. Do not serialize an arbitrary JSON blob
   through a string configuration field. The rotation must reference immutable Scenario versions.
7. Keep lifecycle operations in the existing lifecycle job system. Add a provider-neutral active-deployment command
   path for live queries and mutations instead of adding Nuclear Option endpoints or enum members for individual game
   commands.
8. Live mutations are durable worker commands with persisted input, authorization, audit, redelivery, and bounded
   output. Read-only live queries may use the same short-lived durable command mechanism so results and failures remain
   observable after an API restart.
9. Continue to use process signals for the terminal stop fallback. The official remote-command list has no shutdown
   command, so SIGTERM behavior must be proven by an integration test before it is classified as graceful.
10. Preserve the source-compatible behavior of Arma 3 and Counter-Strike 2 while extracting shared worker primitives.

## Phase 0: executable and container qualification

This phase is a release gate, not a disposable manual experiment.

- Add an opt-in integration fixture or script that downloads app `3930080` anonymously into an isolated directory.
- Run the Linux executable inside the production worker image with its read-only root filesystem, restricted game uid,
  mounted runtime/state paths, `no-new-privileges`, memory limit, and normal Docker bridge networking.
- Record and add only the required native runtime packages to `sources/CloudWorker/Dockerfile`.
- Set `HOME`, XDG paths, Steam library paths, log path, configuration path, and mission path under the deployment mount;
  prove the game does not require writes to the container root or worker credential directory.
- Verify UDP `7777` gameplay and UDP `7778` Steam query behavior through the same Docker port publishing and Hetzner
  firewall plan used in production.
- Verify anonymous Workshop mission acquisition, including update behavior and the exact on-disk paths.
- Verify the startup readiness log marker and immediate-exit behavior.
- Verify SIGTERM with an active mission and players absent. Define the bounded wait and force-kill fallback from the
  observed behavior.
- Measure clean install size, update peak size, idle memory, representative mission memory, CPU behavior, startup time,
  and log growth. Use the results for storage demand, action durations, and recommended runtime profiles.
- Capture a representative default `DedicatedServerConfig.json` and `RunServer.sh` as test fixtures only when their
  redistribution terms permit it; otherwise construct minimal fixtures from the documented schema.

Exit criteria:

- the official server reaches the documented waiting-for-players state inside the hardened worker container;
- a client can discover and join it through the provisioned ports;
- clean shutdown or the required fallback is known and testable;
- required writable and preservable paths and realistic resource bounds are documented in tests/configuration.

## Phase 1: consolidate reusable worker primitives

Refactor only behavior already duplicated by Arma 3 and Counter-Strike 2, then use it for Nuclear Option.

- Extract a `SteamCmdApplicationInstaller` that owns safe runscript construction, anonymous/authenticated login,
  application/workshop ids, `validate`, build-id extraction, restricted Steam identity, cancellation, secret redaction,
  and storage measurements.
- Extract an attributed process controller that owns executable containment, exact server/deployment identity,
  PID/start-time verification, durable identity files, restricted launch, signal/force-stop, and verified exit.
- Keep game adapters responsible for executable arguments, readiness policy, optional graceful-control request, and
  game-specific error text.
- Extract shared bounded log collection and measurement helpers without weakening symlink/reparse-point protections.
- Replace the hard-coded worker heartbeat list with `WorkerGameExecutorRegistry.GameIdentifiers`, so registration and
  advertised support cannot drift apart.
- Move per-game options behind a keyed game-runtime options model instead of continuing to add properties to the
  Arma-centric `CloudWorkerOptions`. Preserve current environment variables as compatibility aliases.
- Add regression tests proving the refactor does not change Arma 3 or CS2 install, identity, redelivery, stop,
  preservation, secret, and measurement behavior.

Exit criteria:

- all existing worker tests pass unchanged in behavior;
- the executor registry is the single source of truth for supported games;
- Nuclear Option can be implemented without copying a complete CS2 executor/process controller.

## Phase 2: register the game and configuration schema

### Game definition

Add `NuclearOptionGameServerDefinitionRegistration` under `sources/Api/Services/GameServerDefinition`:

- display name `Nuclear Option` and identifier `nuclear-option`;
- lifecycle and maintenance actions matching the existing Arma 3/CS2 set;
- UDP `7777-7778` inbound requirement and UDP `7777` primary connection;
- runtime installation for Steam app `3930080`, with the measured installed/peak size and no Steam credentials;
- one primary worker node;
- Scenario label `Mission`, with Scenario required;
- built-in, Steam Workshop, and uploaded-artifact Scenario sources;
- optional mod-pack support only if the qualification phase proves a safe server-side mod installation contract;
- join-password secret marked sensitive, worker-delivered, and configuration-injected;
- file capabilities and preservation seeds for config, ban lists, user missions, and any other qualified persistent data;
- logs readable but not writable through the file-management API.

### Typed configuration

Add `NuclearOptionConfigurationImplementation` to `sources/GameServerImplementations` and register it in
`GameConfigurationImplementations`.

- Render deterministic UTF-8 JSON with `System.Text.Json`; never concatenate JSON fragments.
- Flatten documented nested fields into typed definitions while preserving their JSON structure in the renderer.
- Cover at least: server name, maximum players, hidden flag, modded flag, port overrides, no-player stop time,
  post-mission delay, rotation type, error-kick policy, error-kick immunity files, ban-list files, and all vote-kick
  fields documented by Shockfront Studios.
- Derive game/query ports from the fixed network plan rather than accepting arbitrary profile values in v1.
- Keep the join password in `GameServerSecret`, not a configuration-profile revision.
- Validate numeric ranges, path arrays, enum selections, strings, duplicate paths, and all relative/absolute-path policy
  at the API boundary and again in the worker.
- Add a cross-field validation hook to `IGameConfigurationImplementation` for constraints that cannot be checked one
  field at a time. Apply it generically when profiles are created and when effective configuration is resolved.

### Content seeds and validation

- Extend `GameContentSeeder` with idempotent Nuclear Option built-in missions, beginning with the official documented
  defaults `Escalation` and `Terminal Control`.
- Add game-owned Scenario-version validation rather than another identifier comparison in `GameContentController`.
- Validate built-in names against registered content, Workshop ids as positive unsigned integers, and uploaded mission
  artifact/checksum metadata.
- Add an explicit Scenario-source capability list; do not continue using `TerrainSourceKinds` to validate Scenarios.
- Default new servers to a seeded mission while allowing an ordered rotation to replace the single default.

Exit criteria:

- Nuclear Option appears automatically in the generic creation/configuration UI;
- configuration profiles round-trip and render a server-accepted JSON file;
- invalid source, path, password, port, nested setting, and cross-game selections fail before provisioning.

## Phase 3: model ordered mission rotation

Add a provider-neutral ordered Scenario rotation associated with a game server or immutable configuration revision.
Prefer server-owned rotation with version pins so operators can change it independently of reusable scalar profiles.

- Persist rotation entries with order, Scenario id, pinned Scenario-version id, and maximum mission time.
- Add uniqueness/order and same-game invariants and an EF migration.
- Include the resolved immutable rotation in deployment snapshots and `WorkerCommandPayload` so retries and restarts do
  not observe later database edits.
- Preserve the existing singular selected Scenario as the creation default and compatibility projection; materialize it
  as a one-entry rotation when no explicit rotation exists.
- Add authorized API operations to read and replace rotation atomically.
- Extend the generic server details UI with reorder, add, remove, version pin, and duration controls.
- For Workshop entries, render the Workshop item id directly and let the official server perform its documented
  automatic download/update. Snapshot the requested identity even though the external content is mutable.
- For uploaded entries, stage the immutable artifact through existing worker storage transfer, validate checksum and
  archive structure, and install exactly `<MissionDirectory>/<name>/<name>.json` without traversal, links, collisions,
  or case ambiguity.

Exit criteria:

- ordered built-in, Workshop, mixed, and uploaded rotations survive API/worker restarts;
- deployment retries use the original snapshot;
- invalid or missing artifacts prevent start without allocating an untracked process.

## Phase 4: implement the Nuclear Option worker adapter

Add game-specific paths, payload validation, configuration rendering, process policy, remote-command client, and
executor under `sources/CloudWorker`.

- Register `nuclear-option` in `WorkerGameExecutorRegistry`; advertise it through the registry-derived heartbeat.
- Install/update app `3930080` anonymously with validation and record appmanifest build id and measured bytes.
- Validate deployment/server/game/action attribution, graceful-stop bounds, network ports, configuration keys,
  rotation entries, content installers, artifacts, and checksums before mutation.
- Reset the persistent overlay, render configuration into a restricted temporary file, atomically publish it, apply the
  preserved snapshot, stage uploaded missions, and then launch.
- Launch the executable directly with the qualified official arguments, explicit configuration path, log path, frame
  limit/headless arguments, and internal remote-command port.
- Persist and reconcile exact process identity using the shared attributed process controller.
- Define readiness from process liveness plus the qualified log marker; do not report `Running` solely because a PID
  exists.
- Implement start/install/update/restart/stop/fault-stop/terminate/collect-logs with the same compensation,
  redelivery, preservation, storage-capacity, and bounded fault-evidence behavior as existing games.
- Use the remote client for graceful in-process operations where documented. Use SIGTERM and then verified force-kill
  for stop/restart because the protocol currently has no shutdown operation.
- Ensure all command errors and diagnostic output are bounded and redacted.

Exit criteria:

- every lifecycle action is idempotent under command redelivery;
- a worker/API restart reconciles a running Nuclear Option process without duplicate launch;
- stop publishes selected preservation data before provider teardown;
- fault stop remains bounded and cannot hold cloud resources beyond control-plane policy.

## Phase 5: add generic live server control

### Contracts and persistence

- Introduce game-defined control-operation metadata separate from lifecycle `GameServerActionDefinition`: stable id,
  label/group, read-only or mutating classification, required server state, typed input schema, typed bounded result
  schema, timeout, confirmation policy, and required server permission.
- Add a persisted JSON request snapshot to the durable job/command path and migrate the database. Inputs must be
  allowlisted and validated before persistence; arbitrary command names or argument arrays are forbidden.
- Enable `ELifecycleJobKind.Custom` through a generic executor branch without adding an enum member per game command.
- Preserve sequencing with lifecycle jobs so control commands cannot race stop, restart, update, or deployment
  replacement.
- Persist bounded/redacted results and write audit events for every mutation. Query results containing Steam ids must
  follow existing user-disclosure rules and must not be copied into broad logs.

### Nuclear Option control adapter

Implement the official four-byte little-endian length-prefixed UTF-8 JSON request and status/length/JSON response
protocol with connect/read/write timeouts and strict maximum frame sizes.

Expose typed operations for:

- status: `get-server-id`, `get-player-list`, `get-mission`, `get-mission-time`, and `get-mission-rotation`;
- messaging: `send-chat-message`;
- mission control: `set-time-remaining`, `set-next-mission`, `clear-next-mission`, `set-mission-rotation`, and
  `reload-config`;
- moderation: `kick-player`, `unkick-player`, `clear-kicked-players`, `banlist-reload`, `banlist-add`,
  `banlist-remove`, and `banlist-clear`.

`update-ready` is an implementation detail for update coordination and must not be projected as a user operation.

### API and UI

- Add generic endpoints to list available live controls, enqueue one with typed parameters, and retrieve its result.
- Reuse server-scoped authorization. Separate view/status permission from operate and moderation mutations where the
  claims model requires finer control.
- Add generic server-details panels for status, mission control, messaging, connected players, and bans. Render controls
  from server capability metadata; do not branch the page on `nuclear-option` except for truly bespoke result views.
- Require confirmation for disruptive mission/ban-list mutations and show pending/result/failure state using existing
  job and update-stream mechanisms.

Exit criteria:

- the remote TCP port is absent from the provider firewall and Docker-published port list;
- malformed/oversized/truncated frames fail closed;
- commands cannot be forged by changing a payload after enqueue;
- unauthorized users cannot query player identities or invoke mutations;
- every mutation remains auditable across API restart and worker redelivery.

## Phase 6: storage, files, preservation, and secrets

- Use qualification measurements to set runtime installed bytes and peak preparation bytes.
- Include Workshop cache and uploaded mission staging in storage-demand estimates without double counting preserved
  content.
- Define file capabilities for generated config, ban files, user mission directory, server logs, and any supported mod
  directory. Generated sensitive configuration must not become browser-readable.
- Seed preservation for ban lists and uploaded/user missions. Preserve generated configuration only if necessary;
  authoritative profile/secret data should normally regenerate it.
- Keep Workshop downloads recreatable rather than preserving their cache unless testing demonstrates availability or
  update behavior requires it.
- Inject the join password only while rendering in the worker, atomically restrict the file, redact it from logs and
  diagnostics, and delete temporary material.
- Confirm the game process cannot read worker credentials, bootstrap material, provider tokens, or Steam credentials.

Exit criteria:

- capacity checks reject undersized deployments before SteamCMD or artifact extraction;
- preservation restore reproduces bans and uploaded missions after a fresh deployment;
- secret scanning tests find no join password in API responses, audits, logs, journals, or diagnostic output.

## Phase 7: verification matrix

### Unit and contract tests

- game-definition catalog, network plan, primary connection, action set, files, preservation seeds, secrets, and
  runtime installation metadata;
- deterministic configuration rendering, defaults, every documented setting, cross-field validation, password
  injection, and unknown-key rejection;
- content source validation and idempotent built-in seeding;
- ordered rotation persistence, version pinning, snapshotting, same-game checks, ordering, and atomic replacement;
- SteamCMD runscript safety, executable/path containment, storage measurements, and build-id parsing;
- process identity, readiness, immediate exit, SIGTERM, force-stop verification, and stale PID rejection;
- remote protocol framing, partial reads/writes, status codes, timeouts, maximum lengths, invalid JSON, and result
  redaction;
- executor action matrix, redelivery, compensation, maintenance-while-running rejection, preservation, and fault stop;
- authorization and audit coverage for live queries and mutations;
- provider firewall and cloud-init tests proving only UDP `7777-7778` are published.

### Integration tests

- API -> durable job -> worker claim -> Nuclear Option executor -> report for every lifecycle action;
- API restart while a lifecycle or live-control command is pending/running;
- worker restart while the game remains running and while a command result awaits acknowledgement;
- built-in, Workshop, uploaded, and mixed rotation starts;
- preservation to API-host storage and restore into a new deployment;
- Steam query and real client discovery/join through Docker bridge networking;
- container hardening and secret-isolation assertions.

### Hetzner acceptance test

- provision a real attributed deployment from a clean account/project state;
- discover and join the server, exercise every projected control, restart the API and worker, then stop;
- verify provider-wide discovery finds the resources throughout the run;
- verify server, Primary IP, volume where applicable, and firewall reach non-billable/deleted states;
- verify cost owner, ledger slices, lifecycle history, worker command history, and audit records remain complete.

## Delivery order

1. Container qualification and measurements.
2. Shared worker primitive extraction with Arma 3/CS2 regression coverage.
3. Nuclear Option definition, typed configuration, secrets, content validation, and seeds.
4. Ordered Scenario rotation and immutable deployment snapshot.
5. Worker lifecycle implementation and local container integration tests.
6. Generic live-control command contracts, persistence, authorization, API, and update-stream projection.
7. Nuclear Option remote protocol and full control surface.
8. Generic UI additions, files/preservation polish, and end-to-end tests.
9. Real Hetzner acceptance, operational documentation, and release enablement.

Phases 3 and the persistence design for Phase 5 can proceed after the Phase 1 interfaces stabilize. Do not enable the
game in production capability responses until lifecycle, preservation, firewall, redelivery, and real-provider cleanup
acceptance tests pass.

## Definition of done

- Nuclear Option has no API-hosted game process and no game-specific provider logic.
- All functionality is reachable through registered provider-neutral definitions and worker adapters.
- All documented configuration and remote commands are either supported or explicitly documented as intentionally
  internal (`update-ready`).
- Lifecycle and mutating control operations are durable, idempotent, authorized, audited, and restart-safe.
- One external worker owns one active deployment and connects outbound to the API.
- Runtime and Workshop acquisition require no client-exposed credentials.
- Secrets and player-sensitive data are not exposed through frontend payloads or unredacted logs.
- Stop and fault-stop preserve selected state and end with verified provider cleanup.
- Arma 3 and CS2 retain their behavior after the shared abstractions are introduced.
- `dotnet build X39.UnitedTacticalForces.slnx` and the full test suite pass.
- A clean real Hetzner deployment has completed create, operate, recovery, stop, and verified non-billable teardown.
