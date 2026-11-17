# Feature requests

## Game-controlled execution strategies and on-demand dependencies

- [ ] Rework the worker to support game-selected execution strategies, including direct installation with setup
      scripts and container-based game execution.

### Motivation

Let each game controller choose how its multiplayer server is prepared and run. Direct installation should remain
available, with explicit dependency setup and readiness checks. Games should also be able to use a suitable Docker
or OCI container image instead of installing the server directly into the worker environment. The long-term goal
is flexibility as games have different packaging and operational requirements.

Keep the standard cloud worker image lean. Bundling Wine and Xvfb into the shared worker makes every game
deployment carry the approximately 439 MB image, even when that game does not need those dependencies.
Install additional dependencies on the dedicated deployment worker only when the selected game requires them.

For example, selecting ASKA should install Wine, Xvfb, and their required supporting packages during deployment
setup. Selecting Arma 3 or Counter-Strike 2 should not install ASKA's dependencies.

DayZ is a candidate for evaluating a community-maintained server container, as suggested in this request. Select
and validate the actual image, its maintenance, licensing, persistence, and operating requirements during
implementation; this request does not endorse a particular image.

### Requested behavior

- Let each game controller/runtime implementation declare a supported execution strategy and a trusted, versioned
  preparation plan. Keep the design extensible beyond the first two strategies.
- Support direct installation with game-specific setup scripts or dependency plans, and container-based execution
  that pulls and runs a selected game server image on the deployment machine.
- Validate prerequisites and game readiness explicitly for either strategy, reporting failures before startup is
  considered successful.
- Keep only common worker prerequisites in the standard image; move ASKA-specific packages out of that image.
- Run the selected game's setup on its dedicated deployment worker before game installation or process startup.
- Make setup repeatable and idempotent so command redelivery and worker restarts can safely resume it.
- Record setup progress, the completed setup version, and failures durably. Expose actionable status through the
  existing lifecycle flow and prevent startup until the required setup succeeds.
- Bound setup execution with cancellation and timeouts. Use elevated privileges only for dependency installation,
  and retain the existing unprivileged game-process execution boundary.
- Keep credentials out of scripts, persisted command output, frontend responses, and unredacted logs.
- For container execution, define immutable image selection, ports, persistent volumes, configuration, resource
  limits, health checks, updates, and cleanup. Pull only the selected game's image when needed.
- Apply the existing lifecycle operations, logging, cancellation, recovery, and data preservation rules to both
  strategies. Game-specific process and container details stay behind the game runtime boundary.

### Architecture

Game-specific setup and execution strategy selection belong to the game runtime implementation. Exactly one
dedicated external worker remains responsible for each active deployment, whether it manages a directly installed
game process or a separate game container on that deployment machine. A game container does not replace the
worker's durable command handling or outbound authenticated connection to the API.

Determine how the worker securely controls the deployment machine's container runtime during implementation;
do not assume that running a game container requires privileged nested Docker or unrestricted host access.
The API remains the persistent control plane and may expose safe setup metadata and lifecycle status. It must
not execute setup scripts or game processes. Provider provisioning remains independent of game-specific setup.
Scripts must come from trusted application code; this request does not introduce arbitrary user-supplied shell
execution or a production local runtime provider in Aspire.

### Acceptance criteria

- [ ] The standard worker image excludes Wine, Xvfb, and other dependencies required only by ASKA.
- [ ] Measure and record the standard image size reduction against the current bundled image.
- [ ] A new ASKA deployment installs its declared dependencies and then installs and starts the game successfully.
- [ ] Arma 3 and Counter-Strike 2 deployments do not install ASKA-specific dependencies.
- [ ] A game can declare additional setup without adding its dependencies to the shared worker image.
- [ ] Demonstrate both direct installation with setup steps and container-based execution through the same worker
      lifecycle, with the strategy selected by the game implementation.
- [ ] Evaluate DayZ as a container-based example and document the chosen image and runtime requirements if adopted.
- [ ] Container execution preserves configured game data, exposes only required ports, uses bounded resources,
      and cleans up runtime resources according to the requested lifecycle action.
- [ ] Tests cover prerequisite failures blocking startup, successful preparation, repeated delivery, process and
      container recovery after restart, and cleanup without unintended data loss.
- [ ] Setup logs and API-visible status contain no credentials or other secrets.
- [ ] Document dependency versions, supported deployment environments, and how setup changes apply to existing
      deployments.

Status: feature request only; execution strategies, setup script support, and the image reduction are not
implemented by this document.
