# Phase 14 Research

## Existing Local Arma 3 Flow

- Install/update:
  - Server app: Steam app `233780`.
  - Workshop app: Steam app `107410`.
  - Server install path: `{installRoot}/233780/{gameServerId:00000000}/server-instance`.
  - Workshop content path: `{gameServerPath}/mods/{workshopId}/depots/107410/{depotVersion}`.
  - Linux launch later uses lowercased content directories/symlinks.

- Configuration:
  - `host` realm feeds launch arguments only and is not written to config files.
  - `server.cfg` and `basic.cfg` use `key = value;` with nested `class` blocks for slash-separated paths.
  - `beserver.cfg` uses BattlEye `key value` lines.
  - Existing password/string conversion quotes values for Arma config files.

## Phase 14 Snapshot Needs

- Current worker claim snapshot has job/game/session/config data.
- Worker install/update also needs active or selected mod-pack revision data, or at least parsed workshop item IDs.
- Worker should not need `ApiDbContext`, generated WebApp clients, or API project references.

## Test Strategy

- Unit-test API snapshot enrichment with SQLite-backed dispatch tests.
- Unit-test worker command construction with a fake command runner.
- Unit-test config rendering and path validation with temporary directories/files.
- Build `sources/CloudWorker/CloudWorker.csproj` and full solution after implementation.

## Deferred

- Starting/stopping/recovering native `arma3server_x64` process remains Phase 15.
- Live SteamCMD/DepotDownloader execution remains operator/environment gated.
