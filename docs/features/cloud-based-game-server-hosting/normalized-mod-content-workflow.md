# Normalized Mod Content Workflow

## Detail

Mods and Modpacks are normalized reusable catalog resources. An uploaded HTML file is not a Modpack and is never the
authoritative representation of one. Game-specific files are import and export formats derived from the normalized
model.

The generic content model has four primary records:

- **Mod** owns its game, registered provider identifier, canonical source identifier, title, and client/server usage
  role. A game/provider/source tuple identifies one catalog Mod.
- **Mod version** is immutable and stores the provider version selector, game installer identifier, installer
  configuration, and immutable artifact metadata where applicable.
- **Modpack** owns a game and an ordered history of immutable revisions.
- **Modpack revision** contains an ordered list of direct Mods and an ordered list of nested Modpacks. A membership can
  pin a Mod version or nested Modpack revision; an omitted pin follows the current version or revision when content is
  resolved.

Creating a Modpack writes its definition and first nonempty revision atomically. Editing never mutates an existing
revision; Save Revision appends a complete replacement revision and makes it current. Membership validation rejects
inactive or missing content, cross-game references, invalid pins, duplicates, empty revisions, and nesting cycles.

Resolution walks direct Mods and nested Modpacks in declared order. A nested membership uses its pinned revision when
present and otherwise uses that Modpack's current revision. The effective result is recursively flattened and
deduplicated by Mod identity, with the first occurrence retaining its position. The same resolver supplies runtime
content; there is no Launcher-HTML fallback and an unsupported provider is an explicit incompatibility.

## Providers And Game Installers

Providers are globally registered by stable string identifiers. The current identifiers are `steam-workshop`,
`uploaded-artifact`, and `git`. The old source enum remains only for terrain and Scenario source behavior; it does not
constrain Mod providers.

A provider owns source detection, canonicalization, input validation, metadata resolution, version-selector semantics,
and its public capabilities. A game separately advertises the providers it accepts and the game-specific installers
available for each provider. The API accepts a Mod only when both registrations exist. This separation allows another
provider or installer to be added without changing Modpack storage or presenting a provider that a selected game
cannot actually install.

The current provider semantics are:

- **Steam Workshop** accepts supported Workshop URL variants or item identifiers, canonicalizes them to a Workshop item
  identity, and requires Steam metadata resolution before mutation. A Steam service outage produces a retryable
  service-unavailable response and leaves the catalog and Modpack unchanged. Workshop content floats because Steam
  normally exposes only the latest payload; the worker records the version it observes at Start.
- **Uploaded artifact** stores a bounded ZIP as immutable reusable Mod-version content, including its artifact identity,
  size, and SHA-256 checksum. It is not copied into every Server file set. The worker verifies the checksum and applies
  the archive through the selected game installer.
- **Git** accepts public, credential-free HTTPS repositories only on configured allowlisted hosts. The API removes query
  and fragment data when it constructs the canonical identity and rejects credentials, IP-literal hosts, custom ports,
  and malformed references. The worker resolves the host and rejects loopback, private, link-local, multicast, and
  other non-public destinations; disables credentials, redirects, hooks, submodules, file transport, and LFS smudging;
  performs bounded shallow acquisition; rejects links and unsafe or oversized trees; deletes `.git`; and installs only
  through a registered safe installer.

Git's default branch, named branches, and tags are floating selectors and are resolved again when a Server starts. A
full 40-character commit hash is fixed. In both cases the worker records the actual checked-out commit. Temporary
network or DNS failures are retryable; invalid sources, forbidden addresses, unsafe redirects, credentials, and
nonexistent repositories or refs are permanent validation failures.

The dedicated worker advertises its supported provider identifiers during registration. Start commands carry
normalized provider, source, usage-role, version, installer, artifact, and checksum information. Command completion
evidence carries observed provider/source/version triples, including Steam observations, Git commit hashes, and
uploaded-artifact checksums. This evidence describes what was prepared; it does not make a floating upstream source
reproducible.

## Catalog And Editing Workflow

The management workflow supports both catalog-first selection and pasted source URLs:

1. Search the paged, game-scoped Mod catalog and add existing Mods.
2. Paste one or many Steam Workshop or Git sources and request a non-mutating preview.
3. Review canonical sources, provider metadata, matching catalog entries, installer choices, version selectors,
   duplicates, warnings, and provider errors.
4. Reference catalog matches and keep genuinely new remote sources as reviewed drafts.
5. Add ordered nested Modpacks and optional pins.
6. Create the Modpack or append a revision in one explicit save operation.

Reviewed remote-source drafts can be created with the Modpack revision transaction. Uploaded artifacts differ because
their bytes must be transferred and checksummed first: the current implementation creates the reusable uploaded Mod
and immutable first version before the user saves the Modpack revision, then the editor references that catalog Mod.
The upload and later revision save are therefore not one database transaction. An abandoned editor can leave a valid,
unreferenced catalog Mod that can be reused or managed separately.

The normalized API surface is:

| Operation | Route |
| --- | --- |
| Search the game-scoped Mod catalog | `GET /game-content/mods` |
| Preview one or many provider sources | `POST /game-content/mods/source-preview` |
| Create a remote-backed Mod/version | `POST /game-content/mods` |
| Create a reusable uploaded-artifact Mod/version | `POST /game-content/mods/uploaded-artifact` |
| Append an immutable Mod version | `POST /game-content/mods/{modId}/versions` |
| List or read normalized Modpacks | `GET /game-content/mod-packs` and `GET /game-content/mod-packs/{modPackId}` |
| Atomically create a Modpack and first revision | `POST /game-content/mod-packs` |
| Atomically append a revision | `POST /game-content/mod-packs/{modPackId}/revisions` |
| Read direct or flattened membership | `GET /game-content/mod-packs/revisions/{revisionId}/members?effective={bool}` |

The editor and workspace consume these normalized routes. They display direct membership, flattened effective content,
provider/version and pin state, nested Modpacks, and custom formats instead of embedding an HTML file.

## Game-Owned Import And Export Formats

A game can register zero or more bidirectional Modpack format handlers. Each advertised format supplies a stable
identifier, label, extension, MIME type, maximum import size, import-preview behavior, and export behavior. The generic
API transports files and normalized preview data without interpreting their game-specific structure. A custom format
is a derived representation and never authoritative Modpack state.

Import is preview-only. It resolves catalog matches, proposes new Mods, removes duplicates, and reports warnings. The
result populates the ordinary Create or Update editor; only the user's later Create or Save Revision action mutates the
catalog and Modpack. Export operates on the flattened effective normalized set and returns compatibility warnings in
the response metadata.

Arma 3 registers `arma3-launcher-html`. Its importer extracts Steam Workshop entries, canonicalizes their identities,
reuses catalog matches, proposes missing entries as shared Mods, removes duplicates, and rejects malformed content. Its
exporter includes representable client/shared Workshop entries and warns about server-only or non-Workshop content it
cannot express. Launcher HTML is therefore an Arma-specific bidirectional interchange format only; it is not stored on
`ModPackRevision` and is not a generic requirement for Counter-Strike 2 or any other game.

Format routes are:

| Operation | Route |
| --- | --- |
| Preview a game-owned import | `POST /game-content/mod-packs/formats/{gameIdentifier}/{formatIdentifier}/preview` |
| List formats for a revision | `GET /game-content/mod-packs/revisions/{revisionId}/formats` |
| Download a revision format | `GET /game-content/mod-packs/revisions/{revisionId}/formats/{formatIdentifier}` |
| List formats for an Event selection | `GET /events/{eventId}/mod-pack-formats` |
| Download an Event selection format | `GET /events/{eventId}/mod-pack-formats/{formatIdentifier}` |

Management routes require authentication and their existing resource permissions. Event format discovery and download
resolve the Event's selected immutable revision. A visible Event permits anonymous access; a non-visible Event permits
only its owner, host, or a global administrator. Event participation does not grant Modpack management permission.
Games without a custom format expose the normalized list without inventing a file download.

After a format export succeeds, management and Event download endpoints persist delivery evidence containing the
immutable revision and format identifiers plus the Event and authenticated user identifiers when applicable. An
anonymous download from a public Event records no user identifier. Failed exports and forbidden or unsupported
requests record no delivery. The evidence deliberately stores neither IP addresses nor user-agent strings.

## Migration And Compatibility

`NormalizeModPackContent` is an intentionally destructive migration for legacy Arma 3 Modpacks. It clears Event and
Server references to affected revisions, removes related Scenario requirements, download metadata, resource claims,
and legacy composition links, deletes the existing Arma 3 Modpacks/revisions, and removes their orphaned backfilled
Mods. Compatible non-Arma content is retained and its legacy source identity is translated into registered provider
identifiers where possible. The migration removes the revision HTML column, standalone/composition flag, legacy
composition relation, and old Mod-version source fields.

Legacy Arma HTML data cannot be recovered after migration. Operators must export or otherwise preserve any source data
they need before applying it, then import it through the Arma Launcher HTML preview to create a normalized pack.

This change replaces public API contracts and worker protocol fields. The API, database migration, worker, Blazor
client, React client, and generated clients must be deployed as one coordinated upgrade. Old clients that post raw HTML
or call legacy preview/download/update routes and old workers that do not negotiate provider identifiers are not
compatible.

## Current Verification Limits

- Git acquisition tests use a simulated process runner and controlled address resolver. They verify command hardening,
  validation, cleanup, floating and pinned behavior, and observed commits, but they do not clone a live remote
  repository in CI.
- Uploaded artifacts are reusable catalog Mods created before Modpack revision save; upload and revision creation are
  not transactionally coupled.

## Definition Of Done

- [x] Mods use registered provider identifiers and game-specific installer bindings.
- [x] Mods and Mod versions carry normalized identity, role, selector, installer, and artifact metadata.
- [x] Modpack revisions are immutable and can contain ordered direct Mods and ordered nested Modpacks together.
- [x] Create and append operations reject empty, duplicate, cross-game, invalid-pin, and cyclic membership.
- [x] Effective membership recursively flattens nested Modpacks and deduplicates Mods.
- [x] Runtime content resolution uses normalized membership without an HTML fallback.
- [x] Steam Workshop, uploaded-artifact, and Git providers are registered.
- [x] Steam source preview requires metadata lookup and reports service unavailability without partial mutation.
- [x] Git sources are constrained to public allowlisted HTTPS and installed through hardened bounded acquisition.
- [x] Workers negotiate provider support and return observed content versions.
- [x] Uploaded artifacts are immutable reusable Mod-version content.
- [x] The API provides catalog, source-preview, Mod creation, Modpack revision, and membership routes.
- [x] Arma Launcher HTML is a game-owned bidirectional import/export format derived from normalized content.
- [x] Event format routes enforce Event visibility, including anonymous access for public Events.
- [x] Successful management and Event format downloads persist privacy-bounded delivery evidence.
- [ ] Git acquisition is covered by an opt-in live-remote integration test in addition to simulated tests.
- [ ] Uploaded-artifact creation and a later Modpack revision save have one compensating workflow for abandoned uploads.

## Assumptions

- Providers are global, but content is usable only when the selected game maps that provider to an installer.
- Arma 3 maps Workshop content to its Workshop installer and can map uploaded or Git trees to its safe content support.
- Counter-Strike 2 maps uploaded or Git content to its registered Metamod, CounterStrikeSharp, plugin, or overlay
  installers. Steam Workshop Mod content is explicitly incompatible unless CS2 later registers a supported mapping.
- Git v1 supports no private-repository credentials.
- Existing legacy Arma Modpack data is intentionally unrecoverable after migration.
