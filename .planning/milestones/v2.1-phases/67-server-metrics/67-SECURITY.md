---
phase: "67"
slug: "server-metrics"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-22"
---

# Phase 67 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Worker machine to API channel | Untrusted network peer holding a deployment-scoped credential opens a long-lived WebSocket and sends frames | Bearer credential, heartbeat/claim/report/metrics frames |
| API channel to worker | Command payloads and wake frames leave the control plane | Command payloads that may carry secrets |
| Worker sample to control-plane storage | Worker-supplied numbers become persisted history and shown values | Metric values and reason codes |
| Browser principal to metrics reads | Members read current and historical metrics of a server | Current readings, history series |
| Control plane to browsers | Metrics frames pushed over the update stream | Per-server metrics frames |
| Game process to worker | Loopback A2S/RCON replies and RPT log lines from user-configured games and mods | Player counts, RCON password (CS2) |
| Development harness to production configuration | UAT emulator must never activate in a deployment | Emulated worker credential |
| Dependency manifest to build | A package could enter through package.json or csproj | Third-party code |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-67-01 | Spoofing | channel upgrade | high | mitigate | `LifecycleWorkersController.ChannelAsync` authenticates before `AcceptWebSocketAsync` (401); handler closes 1008 on credential loss; per-frame re-auth | closed |
| T-67-02 | Information disclosure | command frames and logs | high | mitigate | Handler logs ids/type/outcome only; `HostingFailureSanitizer.Sanitize(msg, 512)`; generic Unavailable ack; credential header-only | closed |
| T-67-03 | Denial of service | long-lived endpoint | high | mitigate | Token bucket (1008), 256 KiB cap, send-timeout abort, newest-wins registry, removal in `finally`, keep-alive timeout | closed |
| T-67-04 | Tampering | cross-deployment frames | medium | mitigate | `payload.DeploymentId != request.Worker.DeploymentId` rejection in heartbeat/claim/report routes; `WorkerChannelRouteTests` | closed |
| T-67-05 | Elevation of privilege | duplicated protocol rules | medium | mitigate | Routes only call `IWorkerProtocolService`; `CanExecuteCommand` guards in `WorkerProtocolService.cs` | closed |
| T-67-06 | Repudiation | lost supersession on socket | medium | mitigate | Conflict mapped to Superseded ack; `ReportRunningAfterCostStopSupersededTheCommand_IsAckedSuperseded` | closed |
| T-67-07 | Tampering | metrics ingest attribution | high | mitigate | `MetricsFrameRoute` builds `ServerMetricSampleSource` from re-authenticated identity; mismatch rejected; non-primary not stored | closed |
| T-67-08 | Repudiation | fake values hiding outage | medium | mitigate | Value-or-reason validation, closed reason sets, ranges, skew/stale limits, receipt-time bucketing | closed |
| T-67-09 | Information disclosure | current readings read model | medium | mitigate | `Server.View` permission + existence check before read; 404 (`ServerMetricsService.GetCurrentAsync`) | closed |
| T-67-10 | Information disclosure | CS2 RCON password/status | high | mitigate | Password held in-memory only, cleared on stop, never logged/persisted; tests in CS2 executor suite | closed |
| T-67-11 | Denial of service | malformed game replies | medium | mitigate | `A2sInfoClient`: 3 rounds max, timeout, single-packet check, loopback only | closed |
| T-67-12 | Information disclosure | worker credential/frame handling | high | mitigate | Credential only via Authorization header; `ValidateTransport` enforces https except loopback/explicit opt-in; console-capture test | closed |
| T-67-13 | Spoofing | rejected/revoked credential | medium | mitigate | 401/1008 with unchanged credential faults client; rotation saved before reconnect | closed |
| T-67-14 | Denial of service | hostile server stream | medium | mitigate | 4 MiB inbound cap, 10 s send, 30 s request timeout, 15 s max backoff | closed |
| T-67-15 | Repudiation | paid process after cost-stop | high | mitigate | `WorkerChannelSignalInterceptor` wakes on status changes; heartbeat safety net; `CanExecuteCommand` Conflict | closed |
| T-67-16 | Denial of service | signals blocking saves | medium | mitigate | Unbounded channel, background drain, try/catch in interceptor (see advisory) | closed |
| T-67-17 | Spoofing | metrics frames | high | mitigate | `AuthenticateAsync` per Metrics frame; attribution from identity; mismatch rejected | closed |
| T-67-18 | Information disclosure | browser frames and REST reads | medium | mitigate | Audience resolved per publish, fail closed; `[Authorize(Server.View)]` + service 404 | closed |
| T-67-19 | Tampering | metrics side door over HTTP | medium | mitigate | `WorkerMetricsTransportGuardTests` reflection guard | closed |
| T-67-20 | Denial of service | unbounded history growth | low | mitigate | `ServerMetricsPruneWorker` tier retention every 10 min; idempotent | closed |
| T-67-21 | Spoofing | forged UTFPL lines | medium | mitigate | `Arma3PlayerEvidence.Parse`: nonce, advancing sequence, 256-char cap, quote balance, NonBacktracking regex, 0..1024 | closed |
| T-67-22 | Denial of service | RPT log growth | low | mitigate | Emit on change / every 30 s; 64 KiB tail read | closed |
| T-67-23 | Repudiation | paid process after cost-stop (worker) | high | mitigate | Wake-triggered Running probe, compensation, journal entry in `CloudWorkerRuntime` | closed |
| T-67-24 | Denial of service | missed heartbeats during long commands | high | mitigate | Independent heartbeat loop; 15 s backoff < 60 s stale threshold | closed |
| T-67-25 | Spoofing | stale/rejected credential | medium | mitigate | Rotation saved before reconnect; auth fault ends worker; expiry check | closed |
| T-67-26 | Tampering | metrics frame parsing | medium | mitigate | Strict `parseMetricsFrame`; newest asOf wins; writes own server key only | closed |
| T-67-27 | Information disclosure | raw codes/errors in UI | low | mitigate | `reasonKey()` fixed keys with Unknown fallback; fixed error copy | closed |
| T-67-28 | Elevation of privilege | emulator outside UAT | high | mitigate | `UatWorkerChannelEmulator.AddTo` requires Development + `Testing:UatScenarios:Enabled`; paused skip; registration test | closed |
| T-67-29 | Information disclosure | emulator credential | low | mitigate | Per-run in-memory credential, never logged, normal auth path | closed |
| T-67-30 | Denial of service | very large series | low | mitigate | One path per segment per layer; 2 016-point performance test | closed |
| T-67-31 | Repudiation | silent e2e passes | medium | mitigate | All 7 phase-touched hosting e2e specs assert `unexpected` toEqual([]); axe helpers assert `results.passes.length > 0` (`hosting-metrics.spec.ts:222`, `hosting-a11y.spec.ts:195`) | closed |
| T-67-32 | Repudiation | parity regressions | low | mitigate | `phase67Offenders` in `tests/hosting/parity-matrix.test.ts:181` with negative samples (lines 381-393) | closed |
| T-67-33 | Information disclosure | channel logs | medium | mitigate | Registry logs ids/type/exception type; recording-logger test | closed |
| T-67-34 | Repudiation | stale reading shown as current (API) | medium | mitigate | Single availability precedence `Gate()`; exhaustive property test | closed |
| T-67-35 | Information disclosure | history read model | medium | mitigate | View + existence check, 404; closed range parser (1h/24h/7d) | closed |
| T-67-36 | Denial of service | huge RCON replies | medium | mitigate | 64 KiB cap, read pause, regex timeout, bounded quantifiers, 0..1024 | closed |
| T-67-37 | Repudiation | stale reading displayed (browser) | low | mitigate | `isReadingStale` with `performance.now` and `useNow` re-aging | closed |
| T-67-38 | Tampering | extreme history values | low | mitigate | Fixed y domain clamp; `buildSegments` sole gap decision; no interpolation | closed |
| T-67-SC | Tampering | npm/pip/cargo installs | high | accept (67-01..18, 20) / mitigate (67-19) | No manifest changed since phase start; `metrics-source-scan.test.ts` guards package.json and chart imports | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-67-01 | T-67-SC | No dependencies were installed in plans 67-01..18 and 67-20; diff from phase start shows no csproj, package.json, lock, props, Dockerfile or requirements change; plan 67-19 adds a source-scan guard | Plans 67-01..20 | 2026-11-22 |

---

## Advisory Notes (non-blocking)

- T-67-16: `WorkerChannelSignalInterceptor.cs` logs `LogWarning(ex, ...)` without ids on three catch paths; only status/id data flows there, so no secret exposure. Optional: log ids plus `ex.GetType().Name`.
- T-67-12: insecure ws/http is allowed only for loopback or the pre-existing explicit `UTF_WORKER_ALLOW_INSECURE_HTTP` opt-in.
- T-67-03: `WorkerChannelRegistry` is process-local (documented single-API-instance limitation).

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-22 | 39 | 39 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-22
