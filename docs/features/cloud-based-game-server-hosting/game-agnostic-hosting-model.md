# Game Agnostic Hosting Model

## Detail

As a developer, I want cloud providers and supported games to plug into separate hosting abstractions so that adding a game does not require game-specific cloud orchestration and adding a provider does not require game-specific provider code.

The control plane specifies generic server, lifecycle, storage, cost, ownership, action, and job behavior. A cloud/runtime implementation provisions and releases execution resources through provider-neutral contracts. A game implementation supplies game capabilities, installation and configuration behavior, content handling, file rules, and game-specific worker operations through separate contracts.

Game configuration uses a lightweight typed abstraction. Game implementations register stable configuration definitions and translate stored values into their game-specific files, arguments, environment, or runtime controls. The platform persists values by definition key without understanding game-specific configuration semantics.

Values may be grouped into optional reusable Configuration Profiles. Effective resolution applies game defaults, the selected immutable profile revision, sparse mutable Server-local values, and finally a physical persistent-file override at an identical generated target.

Arma 3 is the first game implementation of these abstractions. Common hosting concepts must be modeled generically. Arma 3-specific behavior must enter through the Arma 3 implementation and must not be embedded in the cloud layer, generic control plane, or generic client flow.

The goal is not to support every game immediately. The goal is to avoid designing Arma 3 as a special-case platform that cannot be extended.

The provider, installer, immutable revision, custom format, and worker acquisition boundaries are specified in
[Normalized Mod Content Workflow](normalized-mod-content-workflow.md).

A game definition may declare a deployment topology made of independently provisioned service nodes. Each node has a
stable service identifier, a bounded instance count, its own inbound network requirements, and exactly one node role is
primary. The control plane persists one dedicated worker identity and provider-resource attribution per expanded node.
Provider code receives only this generic node identity and network/storage plan; it never branches on Arma 3 or Headless
Client semantics. Commands carry a non-secret topology snapshot so game workers can configure peer addresses.

The game-action model has two required well-known actions: Start and Stop. Start supplies the game-specific preparation and start behavior used after the platform provisions the deployment's dedicated worker. Stop supplies the game-specific graceful stop behavior used by the ordinary full cost-stop lifecycle. A game can declare further custom actions, but it cannot omit these two lifecycle actions. Creating the durable server record is a separate platform operation and never invokes Start implicitly.

Actions are projected with stable identity and complete presentation/availability metadata. Apart from the semantic Start and Stop identities, grouping and ordering are supplied by the game or platform action provider for display only; the generic model does not impose a universal category taxonomy. Server-side authorization and precondition evaluation produce an enabled state and safe user-facing reason without exposing claim internals.

Game state is likewise extensible and multi-valued. A game can expose several concurrent states that do not exist for other games. The dedicated worker supplies applicable preparation/runtime states before the game server has started; after startup, the game integration reports game-server states through that worker. The generic platform does not require all games to map these into one shared runtime enum.

A game implementation can also advertise specialized Modpack artifact formats that it can import and/or export. The generic platform exposes the advertised format metadata and transports the artifact, while the game adapter owns parsing, validation, generation, and compatibility warnings. These are derived interchange/download artifacts around the generic Mod/Modpack model, not extensions to its authoritative schema. Arma 3 Launcher HTML is the first implementation; future games can supply unrelated formats through the same capability.

## Definition Of Done

- [ ] Supported games are represented by game capability metadata.
- [ ] Creation flows use game capabilities instead of Arma-specific branching wherever possible.
- [ ] Mod loading is modeled as a generic game content dependency concept.
- [ ] Playable content such as missions, worlds, saves, or operation packages is modeled through reusable Scenarios.
- [ ] The generic model supports individual mods and modpacks.
- [ ] The generic model supports Workshop-style external mods and file-upload backed mods.
- [ ] Each Mod has one source, initially Steam Workshop or file-based content.
- [ ] Each Mod has one usage role: shared, client-only, or server-only.
- [ ] A Modpack can compose Mods and other Modpacks.
- [ ] Nested Modpacks flatten recursively and remove duplicate Mods from the effective result.
- [ ] Game-specific generators transform generic modpack data into game-specific runtime output.
- [ ] Game implementations can advertise specialized Modpack import/export artifact formats.
- [ ] The generic UI/API can expose those formats without knowing their internal structure.
- [ ] Game adapters own artifact parsing, validation, generation, and compatibility warnings.
- [ ] Game-specific artifacts remain derived representations rather than authoritative generic Modpack data.
- [ ] File management is modeled as a server-scoped file tree with server-relative paths.
- [ ] Game-specific file meaning is modeled as metadata/classification over files or paths.
- [ ] Runtime actions are modeled as game lifecycle capabilities.
- [ ] Every game implementation supplies the well-known Start and Stop lifecycle actions.
- [ ] Game implementations may declare additional custom actions without redefining the platform meaning of Create, Start, or Stop.
- [ ] Projected actions carry stable identity, localized presentation, group/order, availability reason, danger/confirmation, pending status, optional duration, and cost-effect metadata.
- [ ] Action grouping is provider-supplied presentation metadata rather than a universal semantic category taxonomy.
- [ ] Action authorization remains server-side and unavailable reasons do not expose internal claim identifiers.
- [ ] Creating a server record never implicitly invokes Start.
- [ ] Server actions and states are projected by the API with game-specific extensions instead of hard-coded client logic.
- [ ] Game implementations can expose multiple concurrent game-specific states without mapping them to one universal runtime enum.
- [ ] State reporting transitions from worker-supplied preparation/runtime states to game-server states reported through the worker after startup.
- [ ] Game-specific states and actions can be localized on the API side.
- [ ] Provider provisioning can consume game-specific requirements through a generic contract.
- [ ] Game implementations can declare independently provisioned primary and auxiliary service nodes.
- [ ] Every expanded service node has its own durable worker, provisioning intent, provider attribution, command stream, and teardown path.
- [ ] Provider implementations remain unaware of game-specific meanings such as Arma 3 Headless Client.
- [ ] Game implementations provide stable typed configuration definitions through a generic contract.
- [ ] The platform stores configuration values by game-owned key without hard-coding game configuration files.
- [ ] Servers consume reusable Configuration Profiles without a separate abstract per-Server override layer.
- [ ] Definition evolution validates stored values against the current game-provided type and defaults without coercing incompatible history.
- [ ] Arma 3-specific behavior is isolated behind the Arma 3 implementation.
- [ ] Cloud/runtime implementations do not contain Arma 3-specific branching or content rules.
- [ ] The Arma 3 implementation targets the game-hosting contracts rather than a concrete cloud provider.
- [ ] A future game can reuse the same control-plane and cloud/runtime behavior by supplying another game implementation.

## Assumptions

- Game-specific implementations may still need specialized workers, scripts, validation, and configuration renderers.
- "Game agnostic" means the control plane and UI concepts are generic, not that every game has identical runtime behavior.
- Provider neutrality and game neutrality are separate concerns with separate contracts.
- Local filesystem is the first storage backend, but the model should not prevent later storage backends.
- Game-specific modpack import/export formats must live behind adapters or generators, not in the generic content model.
- The UI should not need to know game-specific action/state rules.

## Open Questions

- What is the minimal game capability schema needed for v1?
- Which content concepts need first-class entities beyond Mod and Modpack?
- Which game-specific operations must be allowed to appear in the UI without polluting the generic model?
