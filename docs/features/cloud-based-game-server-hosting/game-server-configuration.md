# Game Server Configuration

## Detail

Game-server configuration is stored through a game-neutral configuration abstraction rather than treating every game's generated files or launch arguments as the platform data model.

Each game implementation provides configuration definitions. A definition has a stable game-owned key, a lightweight value type, display and validation metadata, and enough game-specific mapping information for the implementation to apply the value. A key can identify both the target and setting, for example `server.cfg@port`, so games with several configuration files or configuration surfaces do not need platform-specific columns.

The generic value system supports primitive configuration shapes such as strings, integers, booleans, numbers, arrays, and other lightweight primitive compositions needed by game implementations. The platform stores values as individual database-backed records associated with their definition keys. It does not need to understand the meaning of an Arma 3 setting or encode one game's configuration-file schema.

At Start, the game implementation receives the resolved typed values and translates them into the configuration files, launch arguments, environment, or supported runtime controls required by that game. Generated output is an implementation result; the authoritative product configuration remains the abstract definitions and stored values.

Generated configuration is a baseline rather than the final runtime filesystem. Start preparation runs in this order: game installation/preparation, Mod preparation and generated Mod output, Scenario artifact staging into the game-specific path, Configuration Profile rendering, and finally persistent expected-file synchronization immediately before the worker invokes the game's Start action. If persistent expected data contains a file at the same path as any prepared or generated output, the persistent file wins. The UI identifies generated targets as file-overridden where applicable. Removing the persistent override allows the selected preparation layer to provide the effective file again on the next Start.

Configuration definitions identify sensitive values such as join passwords, administrator passwords, RCON credentials, or other secrets. Sensitive values are not exposed to unauthorized frontend clients or unredacted logs. Their rendered output is available only within the authorized worker/runtime path that needs it.

Configuration resolves through four explicit layers: game-definition defaults, the selected Configuration Profile revision, sparse mutable Server-local values, and a physical persistent file at the generated target. Profiles are optional reusable baselines. Existing and new Servers may remain profile-less, in which case defaults and Server-local values still resolve normally. Server-local changes and resets are atomic and audited without recording values.

Configuration Profiles are versioned through immutable revisions. A Server either follows the latest profile revision or pins one explicit revision. The effective revision is resolved when Start prepares the Server. Editing a profile creates a new revision; it does not mutate an existing revision or change the setup selected by a pinned Server.

Configuration definitions can evolve with game implementation updates. If neither the profile nor Server-local layer supplies an optional definition, rendering uses the definition default. If a required definition has no compatible resolved value, Start and generated-file download are blocked, while the virtual target remains visible as incomplete. Values for definitions that no longer exist are retained in immutable revision history but ignored during rendering.

Persistent browsing is an effective projection. It merges the physical tree with virtual directories and generated targets declared by the game implementation. A generated-only target appears once and opens structured editing. A physical collision appears once as the effective persistent override and hides structured editing for that target on the Server surface. Removing the physical file reveals the generated target immediately. An existing physical directory at a generated-file target is a blocking conflict; new conflicting directory writes are rejected.

If a stored value no longer matches the current definition type, the stored value is ignored rather than coerced or reinterpreted. A compatible definition default is used when available; otherwise the required-value rule applies. The Configuration Profile editor shows an inline warning at the mismatched setting so an editor can supply a valid value in a new revision. The incompatible historical value remains available for audit/history but is never rendered under the current definition.

## Definition Of Done

- [ ] Each game implementation can provide game-specific configuration definitions through a game-neutral contract.
- [ ] Every definition has a stable game-owned configuration key.
- [ ] A configuration key can identify a target configuration surface and setting, such as `server.cfg@port`.
- [ ] Definitions declare a lightweight game-neutral value type.
- [ ] Supported values include strings, integers, booleans, numbers, arrays, and other required primitive compositions.
- [ ] Configuration values are stored as individual database-backed records associated with definition keys.
- [ ] The generic platform does not hard-code Arma 3 configuration fields or file formats.
- [ ] Game implementations validate and translate resolved values into their required files, arguments, environment, or runtime controls.
- [ ] Generated files are outputs of the abstract configuration rather than the authoritative configuration model.
- [ ] Persistent expected-file synchronization runs after Configuration Profile rendering and before the game Start action.
- [ ] Scenario artifacts are staged after Mod preparation and before Configuration Profile rendering.
- [ ] The strict preparation order is game preparation, Mod preparation, Scenario staging, Configuration Profile rendering, final persistent expected-file synchronization, and game Start.
- [ ] A persistent expected file overrides profile-generated output at the same path.
- [ ] The UI identifies generated targets that are overridden by persistent files.
- [ ] Removing the persistent override restores profile-generated output on the next Start.
- [ ] Definitions can classify sensitive values.
- [ ] Sensitive values are excluded from unauthorized frontend responses and unredacted logs.
- [ ] Only the authorized worker/runtime path receives sensitive rendered configuration needed to operate the game.
- [ ] Configuration Profiles exist independently of one Server and can be reused.
- [ ] A Server references a Configuration Profile.
- [ ] Configuration Profile revisions are immutable.
- [ ] A Server can follow the latest Configuration Profile revision or pin one explicit revision.
- [ ] Start resolves the effective Configuration Profile revision.
- [ ] Editing a profile creates a new revision rather than mutating an existing revision.
- [ ] A pinned Server remains on its selected revision when newer profile revisions are created.
- [ ] Missing optional values use their definition defaults.
- [ ] A required definition without a compatible value or usable default blocks Start.
- [ ] Values for removed definitions remain in revision history but are ignored during rendering.
- [ ] Stored values incompatible with the current definition type are ignored rather than coerced.
- [ ] A compatible default is used when an incompatible stored value is ignored.
- [ ] The profile editor shows an inline warning at each incompatible stored value.
- [ ] Correcting an incompatible value creates a new profile revision.
- [ ] Incompatible historical values are retained but never rendered under the current definition.
- [ ] Effective values resolve as defaults, selected profile revision, sparse Server-local values, then physical persistent-file override.
- [ ] Profiles are optional and existing Servers are not backfilled.
- [ ] Server-local values are sparse, mutable, validated atomically, resettable, and audited without value disclosure.
- [ ] Persistent browsing mixes physical entries with game-declared virtual directories and generated targets without duplicates.
- [ ] Generated targets remain visible when incomplete; Start and content download return a validation failure.
- [ ] A physical generated-target collision is exposed as one override entry and hides Server-surface structured editing until deleted.

## Assumptions

- Configuration definition registration belongs to the game implementation.
- Definition keys remain stable enough to identify stored values across normal application upgrades.
- A stable key may retain an incompatible historical value; the current definition controls validation and rendering.
- Database persistence stores abstract values, not only generated configuration-file text.
- Games can have multiple configuration files and other configuration targets.
- The value abstraction remains lightweight and is not intended to model arbitrary executable code or a complete programming language.
- Configuration Profiles are optional reusable abstract baselines; Servers may add sparse local values.
- Latest-versus-pinned Configuration Profile selection follows the same general consumption semantics as other reusable versioned setup inputs.
- Persistent expected files are the final effective-file override layer rather than another typed configuration-value layer.
