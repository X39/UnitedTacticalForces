# Phase 14: Arma 3 Install And Configuration Execution - Context

**Gathered:** 2026-06-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement worker-side Arma 3 install/update and configuration rendering for cloud workers using the Phase 13
credential-authenticated job protocol. The worker should execute from API-provided snapshots and local worker
tool configuration, without direct database access and without exposing Steam/provider/worker credential material.

</domain>

<decisions>
## Implementation Decisions

### Snapshot Data
- Extend the worker claim snapshot with active/selected mod-pack revision metadata and parsed Steam workshop item IDs.
- Parse mod-pack HTML on the API side where `Arma3ModPackParser` already exists, so the worker does not need API or
  database dependencies.
- Keep sensitive configuration values masked in worker snapshots. The worker renders non-sensitive entries and skips
  masked sensitive placeholders rather than leaking secrets through DTOs.

### Worker Tool Configuration
- Resolve SteamCMD, DepotDownloader, Steam username/password, and install root from worker-local runtime options
  (CLI/env), not from API snapshots.
- Validate paths, executable existence, install-root write access, and Steam credential presence before file mutations.
- Never include Steam credentials or full secret-bearing command lines in progress, logs, failure reasons, or tests.

### Worker Execution
- Implement Arma 3 `Install`, `Update`, and `Configure` job handling in the worker. Process start/stop/recovery remains
  Phase 15.
- Use an injectable command runner abstraction so tests can verify command construction and filesystem output without
  launching Steam tooling.
- Preserve Linux mod layout behavior by downloading each workshop item under the game-server workspace and creating
  lowercased content copies/symlinks suitable for later launch command construction.

### Local Compatibility
- Do not alter the local API-owned `Arma3GameServerController` behavior in this phase except for worker snapshot data.
- Existing local lifecycle execution remains the compatibility baseline.

</decisions>

<code_context>
## Existing Code Insights

- `Arma3GameServerController` already defines configuration realms: `host`, `server.cfg`, `basic.cfg`, and `beserver.cfg`.
- Local config rendering writes nested class blocks for slash-separated paths, converts string/password values to Arma
  strings, and writes BattlEye entries as `key value`.
- `SteamGameServerControllerBase` uses `+force_install_dir`, `+login`, `+app_update`, and `+quit` for SteamCMD server
  installs and uses DepotDownloader for workshop items.
- Local workshop parsing depends on `Arma3ModPackParser.FromHtml` and Steam URLs under
  `https://steamcommunity.com/sharedfiles/filedetails/?id=`.
- Phase 13 worker runtime already claims one job and can report progress/failure; it needs a real execution dispatcher.

</code_context>

<risks>
## Risks

- Real Steam tooling cannot be exercised in automated tests without credentials and network access; verification must
  use fake command runners and path assertions.
- Sensitive server configuration remains masked by design. A future worker-secret channel may be needed if operators
  require cloud-rendered RCon/server passwords.
- DepotDownloader output layouts can vary. The implementation should be tolerant of existing lowercased directories and
  should not depend on live output in tests.

</risks>
