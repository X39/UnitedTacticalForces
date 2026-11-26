# API Coverage — Valve Source Query (A2S) and Source RCON

> Full coverage by default. Opt-outs are explicit, reasoned decisions.

Phase 67 reads player counts from running game servers. The dedicated cloud worker queries the game process on
the same machine: Counter-Strike 2 through Source RCON `status` with A2S_INFO as the fallback
(`sources/CloudWorker/CounterStrike2RconClient.cs`, `sources/CloudWorker/A2sInfoClient.cs`), and ASKA through
A2S_INFO on port 27016. Arma 3 reads counts from its join monitor and uses neither protocol. The phase uses no
other external API. Hetzner provisioning belongs to earlier phases.

| capability | decision | reason |
|---|---|---|
| A2S_INFO request and reply (player count, max players) | INTEGRATE | |
| A2S challenge handshake (S2C_CHALLENGE resend, bounded rounds) | INTEGRATE | |
| A2S multi-packet (split) replies | OPT-OUT | not needed: an A2S_INFO reply fits one packet; a split reply reads as query-failed and never as a guessed count |
| A2S_PLAYER (per-player names and scores) | OPT-OUT | explicitly out of scope: metrics show counts only, and player names would add personal data the phase does not need |
| A2S_RULES (server cvars) | OPT-OUT | not needed: no metric uses server rules |
| RCON authentication (SERVERDATA_AUTH) | INTEGRATE | |
| RCON `status` command and parsing of the human count | INTEGRATE | |
| RCON multi-packet response assembly (bounded size and pause) | INTEGRATE | |
| RCON arbitrary command execution (admin console) | OPT-OUT | explicitly out of scope: metrics are read-only; a remote admin console would need its own feature and authorization design |
