# Dependency preparation for an ordinary UTF release job

**Execution boundary:** source review and the three small native-helper cases
below passed. Full SDK preparation and the complete `prepare.ps1` CLI have not
completed. The first full CLI attempt stopped at the polling failure recorded
below, before any dependency image was completed.
Actual dependency preparation, browser/ABI smoke and immutable transport remain
separate gates; the small public fixture does not establish those results.

This is an operator preparation tool, not an application build or a replacement
release pipeline. It prepares dependency-only OCI images on the existing disposable
release VM through the explicit `mozaic-release-2a599af` connection and matching
machine SSH identity. It never
copies the application checkout into a build context. Current pipeline files remain
unchanged. No SDK publication, runner admission or deployment is implied.

## Inputs and output

`inputs.json` records the reviewed immutable linux/amd64 image references. The API
and worker runtime and both SDK references match the existing release inputs. Node
is the reviewed 24.11.1 Bookworm image; the compiler comes from the already reviewed
Rust image. SDK 10.0.201 remains the default host. The separate 10.0.302 installation
does not change root `global.json` or the worker Dockerfile semantics.

The three prepared runtime bases contain dependencies and existing filesystem
identities only. API receives libgssapi. Worker receives i386 libraries, SteamCMD,
tini, setpriv and UID/GID 10002/10003/10001. ASKA additionally receives the existing
Wine, Xvfb, winbind and font packages. Normal authenticated apt installation runs
the distribution's maintainer hooks. SteamCMD retains the exact existing SHA256;
the preparation tool does not run its updater or download game files. Application
outputs, entrypoints, credentials and release configuration are absent.

The SDK contains both SDK installations, Node, the Rust compiler, separate vendor
Playwright 1.61.0 Chromium and 1.62.1 Chromium/Firefox/WebKit caches, and read-only
OCI archives for API, worker, ASKA and the unchanged Nginx base. These archives
contain no application output. No engine socket, OCI engine or deployment token is
provided for an eventual ordinary job.

Each invocation creates a new external output directory, a closed source context,
logs, package receipts, OCI archives and `images.json` containing actual image IDs,
manifest digests and archive hashes. No generated digest is invented or checked
into the source before a successful preparation and independent review. Keep the
complete directory as evidence. A failed run retains `PREPARATION-INCOMPLETE`;
only a completed offline browser smoke creates `PREPARATION-COMPLETE` and renames
that fence to `PREPARATION-FINISHED`.

## Operator invocation and resource gates

Use Windows PowerShell 7 with Podman and Python 3.11 or newer available. Supply
the independently built Mozaic protocol validator executable explicitly; its
reviewed SHA256 is pinned in `inputs.json`. The current local verified binary is
`A:/releases/mozaic-oci-independent-20261024/validate.exe` (SHA256
`e5efcf60b242855df46e1a3274c5d9b2e598fd0a48e9085d5ff219784b7c6d66`).
It invokes `mozaic_protocols::oci_layout::validate_oci_layout_archive` with the
protocol's default limits and `OciImageLayoutTarV1`; it accepts exactly one archive
path and returns a nonzero exit on failure. It is an external verification tool,
never an application dependency or container engine. Replacing it requires review
of its provenance and a deliberate pin update.

Before any remote mutation the tool validates the selected connection and full
engine info: Linux amd64, rootless boolean true, systemd/cgroup v2, the reviewed
CPU/memory/PID controllers, UID/GID 1000 mapping and the `user` SSH connection to
the loopback release VM's
`/run/user/1000/podman/podman.sock`. A connection name alone is insufficient.
The output parent must already exist; every ancestor of it and the checkout must
be a plain directory without a Windows reparse point. The new output is claimed
by an atomic Windows no-replace directory rename, before writing any output file.
An existing or concurrently created destination fails without overwriting it.

The operator must first provide an existing rootless systemd slice with CPU quota
six cores, MemoryMax 12 GiB, MemorySwapMax zero and TasksMax 512.
Pass its exact slice name and full
`/user.slice/.../<name>.slice` cgroup path. The tool reads that cgroup's `pids.max`
and verifies every exact limit, controller and empty-parent state. It refuses
stale scopes, occupied parents or changed bounds. It does not provision or
modify cgroups, and must never be pointed at a production host.

```powershell
./.mozaic/sdk/prepare.ps1 `
  -Connection mozaic-release-2a599af `
  -OutputDirectory A:/releases/utf-sdk-<unique-reviewed-run> `
  -CgroupParent <verified-user-slice>.slice `
  -ParentCgroupPath /user.slice/<verified-existing-path>/<verified-user-slice>.slice `
  -PythonPath <trusted-python-3.11-or-newer.exe> `
  -ProtocolValidator A:/releases/mozaic-oci-independent-20261024/validate.exe
```

By default every base must already be present at its exact digest. The explicit
`-AllowAnonymousFetch` switch permits TLS-verified pulls of only the reviewed
digest references with an empty authfile. It does not read registry credentials.
Preparation needs public network access for signed Ubuntu apt packages, npm and
the pinned vendor Playwright downloads, and IPv4 access for the exact SteamCMD
archive. It does not require this access from a future cloud job. Live apt package
resolution is deliberate operator preparation; the resulting package versions
and final OCI digests, rather than the recipe alone, identify the closure.

RUN subtrees use six CPU cores through Podman's supported CPU period/quota options
and 12 GiB memory with no additional swap allowance. The existing bounded parent
provides the PID limit: `podman build` has no per-build `--pids-limit` here. RUN
commands have explicit 30/40/60 minute timeouts. The orchestrator enforces a
120-minute shared deadline while polling engine storage and external output.
Every Podman call, including info, connection inventory, storage polling, image
inspection, probes, receipt copying and cleanup, uses the same deadline-aware
subprocess wrapper. Native helper commands are also individually deadline bound.
Metadata calls have a 30-second cap; build/pull/save calls
have a 60-minute cap; archive validators have a 10-minute cap. Output draining
has a two-second bound per pipe and process-tree kill/reap has a five-second
bound. These finite local cleanup allowances can extend beyond the work deadline.
It requires 80 GiB initial free space, retains a 32 GiB free-space floor and stops
at a monitored 64 GiB incremental store-plus-output budget. These are monitoring
guards, not an extfs project quota or a kernel-enforced disk-isolation claim.

BUILD alone uses `podman machine ssh --username user mozaic-release-2a599af` and
native `/usr/bin/podman --remote=false`. Bare and absolute remote-API build parent
arguments failed actual probes. The accepted native form is
`<slice>:mozaic-sdk:utf<run-guid>-<profile>` with
`--runtime-flag=systemd-cgroup --cgroupns=host`. Its generated `.scope` is below the
verified parent; the absolute filesystem path is used for inspection, never as a
runtime cgroup argument. The helper revalidates UID/GID, native engine and matching
graph root before each build. It copies only the per-profile reviewed file list,
with SHA256 checks, into an exclusive UID/GID 1000 mode0700 `/var/tmp/utf-sdk-*`
stage. Native staging must share the image store's monitored filesystem. Windows
paths require the existing WSL `/mnt/<drive>` mapping; UNC paths are rejected.

Native BUILD has a host timeout, its own child process group and an attempt scope.
A shared-output heartbeat is refreshed by the operator poll loop. If it is stale
for 45 seconds, a deadline expires or TERM/HUP/INT arrives, the native supervisor
terminates that process group and stops only the exact owned scope after checking
its systemd ControlGroup identity. Cleanup has finite process and systemctl caps
and sends group KILL unconditionally after the two-second TERM grace, even when
the group leader exited. This occurs before the systemd query, so a hung query
cannot skip group termination.
Cancellation is deferred and recorded while `Popen` establishes its process
object and POSIX exec/session handshake, throughout supervision and through owned
cleanup. The same nonraising callbacks remain installed for that entire lifetime;
there is no per-signal transition to raising handlers or SIG_IGN during cleanup.
The loop checks pending cancellation, including after leader exit before reporting
success. Group and exact-scope finalization run unconditionally for every launched
process, including a successful leader exit. Linux `waitid(..., WNOWAIT)` observes
that exit without reaping the leader, reserving its PID/PGID until the final group
KILL and scope-stop attempt; this prevents signaling an unrelated recycled group.
Preflight requires default SIGCHLD handling so that reservation cannot be lost to
automatic or custom-handler reaping. The leader is then reaped before restoring
prior handlers. If group/scope release cannot be verified, the attempt fails with
its fence retained and nonraising callbacks remain installed until CLI exit.
This defers Python callbacks without blocking OS signals or using a child pre-exec
hook, so the child retains normal signal behavior.
It reserves 15 seconds before the work deadline and has a 15-second host kill grace.
The Windows wrapper separately bounds local client-tree cleanup. The dated
acceptance below covers actual scope cleanup with a responsive user manager.
It does not establish cleanup of detached OCI scope processes when systemd
show/stop is unavailable, even though group KILL remains independent of that query.

`native-intent-<profile>.json` records exact stage/scope ownership before launch.
Failures retain the stage and logs for inspection even after attempted scope
cancellation. Successful builds remove only their exact owned stage after scope
removal. On a failure, verify the scope and external build-container state before
retrying. No global prune or speculative deletion of another operator's resources
occurs. RUN limits do not claim a whole-engine CPU/memory quota: host staging,
compression and export remain separate operations under deadlines and monitored
storage guards.

## Actual native-helper acceptance (2026-10-24)

The `native-build.py` snapshot with SHA256
`3955c6c1e63be83a405eef8f5096e5991c9c6edb49da2d7d660993bc7ff5e4fb`
passed these individually bounded public-fixture cases on the existing release VM:

| Case | Elapsed | Result |
| --- | ---: | --- |
| Successful small public RUN | 12.86 s | Scope and owned processes absent after success |
| TERM-resistant RUN with short deadline | 20.41 s | Nonzero exit; scope and owned processes absent |
| Windows SSH client killed, heartbeat stopped | 56.41 s | Scope and owned processes absent |

Each case had a 120-second absolute watchdog and used only the already cached
pinned Fedora 44 image, with RUN networking disabled. The actual source
`Assert-RootlessEngine` and `Assert-MachineBinding` guards validated the remote
engine and machine endpoint/identity. The unmodified native helper then validated
UID/GID 1000, its matching graph root and exact empty bounded parent; copied only
three hash-bound public-fixture files into its exclusive mode0700 stage; and
executed the real native BUILD path. RUN proved its exact generated scope,
six-CPU/12-GiB/zero-swap bounds and effective 512-PID ancestor limit. After every
case the parent was empty with unchanged limits and no matching owned processes.
Forced SSH loss did not isolate whether an SSH signal or heartbeat expiry caused
cancellation; the result does not claim a separately measured heartbeat-only gate.

Private evidence is retained at
`A:/releases/utf-native-helper-repeat-20261024-3955`, including per-case
requests, source-guard logs, RUN output, active/final cgroup observations, process
audits and results. The harness assembled the helper requests directly; the
complete `prepare.ps1` CLI, dependency recipes, image export/validation chain and
SDK browser smokes were not executed by these cases. No application code, game
files, credentials, SDK pulls or SDK builds entered the fixture.

Before these cases, the retained transient slice was active in systemd but its
empty kernel cgroup had been garbage-collected. Restarting that exact leaf
recreated the path without realizing resource-controller files, so the guards
refused to proceed. The operator then stopped only the identity/property-verified
empty leaf and invoked the original reviewed `create-slice.sh` unchanged. All
exact bounds and controller availability were reverified before any RUN. No
manual ancestor write or weaker bound was used; this fixture maintenance is not
an automatic fallback in `prepare.ps1`.

Both canceled cases left nonrunning Buildah storage-container metadata with PID0.
Process/scope cleanup therefore does not imply storage cleanup. The operator
attributed each exact container ID using its pinned base image ID, creation time
between that case's request/result, and both mounted unique fixture file hashes.
Only after rechecking PID0/storage state were those exact IDs removed. The unique
success image and hash-verified failed stages were also removed. The final audit
found no owned processes, container metadata, image tag or native stages, and the
parent was released empty with its limits preserved. The two unrelated containers
retained their IDs, PIDs, states and base images; every pre-existing image ID and
the exact public base were preserved. This was explicit, identity-verified manual
cleanup, recorded in
`owned-storage-proof.json` and `cleanup-result.json`; there is no automatic prune,
blanket retry or whole-engine resource-bound claim.

Additional POSIX regressions cover a signal arriving inside the `Popen` launch
gap. Its real POSIX regression creates the child session, queues an actual TERM
before the constructor wrapper returns, and proves the leader and TERM-resistant
descendant are reaped while child cancellation signals remain unblocked. A second
regression sends actual HUP and INT during TERM grace and at scope-query entry,
verifies cleanup continues, and checks prior handlers are restored afterward.
Another uses a successful leader with a remaining TERM-resistant descendant and
delivers HUP during handler restoration: group/scope finalization and leader
reaping must already have completed before cancellation raises. These focused
regressions complement the current-source three-case helper result above; they
do not establish full SDK preparation or unavailable-DBus scope cleanup.

## Receipts and dependency smoke

APT receipts include initial and installed package/version/architecture lists,
authenticated InRelease files, source definitions, trusted keyring hashes, exact
downloaded deb SHA256 matched against apt's authenticated package metadata,
extracted control scripts and hook hashes, package copyright records, and clean
`dpkg --audit`/`apt-get check` results. Debug gpgv verification is in the build
logs. The normal vendor package manager executes hooks; no custom deb extractor
substitutes for installation.

SteamCMD's original archive is retained with the pinned hash and installed file
hashes. Playwright receipts retain npm lockfile integrity values, the installed
dependency tree, vendor browser revision inventory, browser file hashes and
license/notice hashes. Vendor installers perform their normal browser extraction
and native apt dependency installation. Browser ZIP archives are not retained by
the vendor installer; installed browser file hashes are the recorded payload
identity. Redistributability and required notices remain an operator review gate,
particularly for SteamCMD and third-party browser components.

The dependency build probes compiler ABI and exact .NET/Node versions. Final
smoke launches both browser closures offline as UID 65532 with a read-only root,
no capabilities, no network, bounded tmpfs and explicit CPU/memory/PID limits. It
loads only a static data URL. It runs no application tests or game processes.

Raw Podman exports are retained as `<profile>.raw.oci.tar`, including Nginx and
the final SDK. They are evidence, not deliverable runtime archives. Podman's two
empty outer `blobs` directories violate the strict regular-only transport contract.
`canonicalize-archive.py` first validates every raw USTAR header, extent, checksum,
padding and terminator, then the complete graph, image identity and actual layer
diffIDs. It permits only those two known empty directories; links, other paths,
duplicate names, PAX/GNU extensions, specials and trailing archives fail closed.
There is no extraction. Bounds remain 16 GiB per archive, 8 GiB per file, 1026
regular files and 32 GiB expanded layers.

The helper exclusively creates a new external `<profile>.oci.tar` under plain
non-reparse ancestry and refuses existing destinations, including competing
writers. It writes regular-only USTAR with normalized outer metadata. All
manifest/config/layer blob bytes remain unchanged, with the same exported root,
config digest and diffIDs. Only the index reference annotation is set to
`localhost/utf-dependencies-<profile>@<exported-manifest-digest>`; an engine store
digest is not used because export can produce a different manifest digest.
Raw and canonical archive hashes, sizes, removed directories, graph inventory,
diffIDs and the producer reference are recorded in `receipts/transport-*.json`.
`verification-tools.json` binds the local canonicalizer and validator sources.
These local tools are not copied into a native build context.

Every canonical archive must pass the unchanged pinned real Mozaic protocol
validator before recording success or copying runtime archives into the SDK.
`context/archives/SHA256SUMS` covers only those validated canonical runtime
archives. The independent gate checks
the bounded tar envelope, safe members, duplicate entries, OCI schemas, descriptor
sizes, hashes and complete graph. `validate-archive.py` then independently walks
the single-image manifest/config/layer closure, requires Linux amd64, binds the
config digest to the image ID obtained from the engine, rejects external and
unreferenced blobs, and verifies every layer's actual uncompressed SHA256 against
its config diffID. Only raw and gzip layers are admitted, with 8 GiB per layer,
32 GiB total expansion and a 200:1 expansion bound plus 1 MiB slack. No archive
root filesystem is extracted. A different compression format fails closed.

Run the source-only negative fixtures on Windows before review:

```powershell
./.mozaic/sdk/test-safety.ps1
./.mozaic/sdk/test-budget.ps1
$env:UTF_OCI_VALIDATOR = 'A:/releases/mozaic-oci-independent-20261024/validate.exe'
<trusted-python.exe> -B .mozaic/sdk/test_archive.py -v
<trusted-python.exe> -B .mozaic/sdk/test_canonicalize.py -v
<trusted-python.exe> -B .mozaic/sdk/test_native.py -v
<trusted-python.exe> -B .mozaic/sdk/test_native_build.py -v
```

The Windows tests use actual junction and competing-process output claims plus
rootful/identity/platform/cgroup, hanging storage-poll/deadline and local child
process cleanup negatives. The archive tests
execute the pinned real protocol validator against synthetic raw/gzip layouts,
unsafe members, duplicates, links, malformed schemas, descriptors, absent/altered
and unreferenced blobs, then test additional image/config/diffID and expansion
failures. They perform no Podman mutation or application build.
Canonical transport tests additionally prove full blob byte preservation and
exported-root reference binding, strong raw-envelope rejection before output,
existing-file protection, a real Windows junction refusal, and exactly one winner
between two concurrent writer processes. A reduced test cap exercises the archive
size rejection without allocating a 16 GiB fixture.
The budget test extracts the actual `Assert-Budget` function and invokes it from
the real `Invoke-DeadlineProcess` polling callback with a short-lived local
PowerShell child. Only storage readings are substituted. It verifies successful
polling despite the subprocess helper's local Boolean launch state, preparation
and subprocess deadlines, the 64 GiB incremental-storage fence, the VM free-space
floor, and child cleanup on failure. Callback timestamp, baseline, output,
deadline and heartbeat references use script-owned state; storage arithmetic uses
explicit Int64 operands rather than PowerShell's Int32 overload inference.
The native tests also cover closed context selection, staged byte hashes, stale
context refusal, graph-root/identity binding, heartbeat age and exact own-scope
cancellation routing. Their supervision test uses substitutes only to check
control flow; it does not claim that real cgroup descendants have terminated.
The diagnostic fixtures additionally use actual future/stale heartbeat files,
nonzero and timed-out child commands, a source-hash refusal and malformed CLI
input. On Linux, the exact-scope cleanup callback must have run and real
supervised nonzero/deadline children must be reaped before supervision returns
or raises. That callback is substituted
in this fixture; the separate POSIX and actual RUN acceptance remain required.

The native helper reports only fixed failure codes: `HEARTBEAT_FUTURE`,
`HEARTBEAT_STALE`, `OWNERSHIP_FAILURE`, `COMMAND_NONZERO`, `TIMEOUT`, `CANCELLED`
or `INTERNAL_ERROR`. No exception text, command arguments, environment or paths
are interpolated into that diagnostic. Unknown exceptions fail closed as
`INTERNAL_ERROR`. These codes distinguish the failed guard without changing its
behavior: any future heartbeat timestamp still fails, and the existing 45-second
freshness limit, deadlines, process-group reservation and scope cleanup apply.

Heartbeat age samples the file's modification time before sampling the native
clock. A concurrent Windows refresh during the shared-filesystem `stat` can
otherwise make that timestamp appear newer than a clock sampled before `stat`.
The deterministic regression schedules a real writer inside that interval and
still requires genuine future and stale timestamps to fail; no clock-skew
tolerance is added. A bounded read-only observation during the `a1401936`
attempt captured a -0.000580-second age using the old order and a positive
0.001289-second age after the same `stat`. Evidence remains in
`A:/releases/utf-diagnostic-preparation-20261024-a1401936/live-heartbeat-observation.json`.
That observation proves the refresh window exists, not the cause of an earlier
preparation failure.

The POSIX regressions use real test-owned processes: one combines a leader exiting
on TERM, a TERM-resistant descendant and a real hung DBus-query substitute; the
second delivers TERM in the constructor/assignment gap after a real child session
exists and checks descendant cleanup and the child's unblocked signal mask; the
third sends HUP and INT during cleanup and verifies handler restoration afterward;
the fourth covers successful leader exit with a remaining descendant and late
cancellation at restoration, while checking the reserved PID during group signals.
Run it on the existing release VM under a finite outer timeout:

```powershell
podman machine ssh --username user mozaic-release-2a599af '/usr/bin/timeout --signal=TERM --kill-after=5 40 /usr/bin/python3 -B /mnt/a/dev/git/UnitedTacticalForces/.mozaic/sdk/test_posix_cancel.py -v'
```

It proves the group descendant dies even when the query times out. It runs no
Podman build, cgroup mutation or real DBus request and does not replace the
separate actual RUN/transport-loss acceptance gate.

On 2026-10-24 the existing public Bantapop API dependency raw export was tested
read-only through this transport step. Evidence is retained at
`A:/releases/utf-oci-transport-proof-6306128533e8/result.json` and its neighboring
script/logs. The raw archive (SHA256
`12d4d1eb2d6212d5ad5afa41bb1533c471633a6a9ea42a74df136d27411411c5`)
failed the pinned strict validator, while the canonical archive (SHA256
`03d0f94d8869ed07aea871490ec92e131a843f61c904b727389045b461080ede`)
passed. All four blob streams were compared byte-for-byte; the raw file's hash,
length and modification time were unchanged. Both uncompressed layer diffIDs
were verified, preserving manifest
`sha256:67f72efbaad67d0cd9c8321a723cb1ec9e29a0fdf45f21ff4522b47f4b3e85e5`
and the inspected config/image ID. The helper and strict gate completed in
4.000 seconds. This local file-transport proof did not run an engine, dependency
recipe, publication or the complete `prepare.ps1` CLI. The earlier native-helper
three-case acceptance remains separate historical evidence for its exact source.

The first full dependency-only CLI attempt used committed source
`14a0144ba6aeffbe159a80b0ea00e9336ae49fbb` on 2026-10-24. Evidence remains at
`A:/releases/utf-full-dependency-preparation-20261024-7e986b11c262`. It stopped
after 19.282 seconds during the first API APT step because PowerShell's dynamic
scope resolved the callback's start time to the subprocess helper's Boolean
`started` variable. This is an orchestration failure, not a dependency validation
result. No dependency image or archive was completed, and there was no retry.
`PREPARATION-INCOMPLETE`, the source context, failed native stage and logs remain.
The final audit found no owned scope or process, an empty parent with unchanged
CPU6/memory12GiB/swap0/PID512 limits, no added container or image, and both existing
containers and all 64 preexisting image IDs preserved. No pruning or storage
removal was needed. The local callback regression above covers the correction;
it does not substitute for a later full preparation run.

The next full dependency-only attempt used committed source
`4c18da5701a7a3a1acef8eaf17c114184016bf15`. Evidence is retained at
`A:/releases/utf-canonical-preparation-20261024-4c18da57`. It stopped after
297.218 seconds with no retry. API and worker archives passed canonical and real
protocol validation. ASKA installed its Wine/i386 dependencies and verified the
SteamCMD archive, then stopped during final receipt generation. The retained
receipt for `libpython3.12t64:amd64=3.12.3-1ubuntu0.17` has an empty authenticated
index file; the old generic native diagnostic does not prove whether its
`apt-cache` query failed or supervision interrupted it. No APT or timestamp fence
has been changed on that evidence. Nginx, the combined SDK and browser smokes did
not run. The fixed diagnostic codes above prepare a later attempt to distinguish
those outcomes; their tests do not establish the earlier failure's cause.

That attempt's final ownership audit preserved all 69 image IDs returned by the
initial image inventory and both unrelated containers, including their IDs,
base images, PIDs and states. The parent was released empty with its original
bounds, all three attempt scopes and owned processes absent, and no OOM/PID-limit
events. Two completed images, the exact PID0 failed Buildah storage container,
and its mode0700 source-only stage remain for diagnosis. Sampled combined growth
peaked at 2,361,406,500 bytes, with at least 220,129,906,688 bytes free on A:.
These are sampled monitoring results, not a hard whole-engine storage quota.

Future application work must use `utf-dotnet-201` in the root context and
`utf-dotnet-302` in an isolated worker context with its own explicit 10.0.302
`global.json`. The wrappers fail if ancestor configuration selects another SDK.
Set `PLAYWRIGHT_BROWSERS_PATH` to the corresponding `UTF_*_PLAYWRIGHT_BROWSERS_PATH`
for each suite; do not accidentally run .NET tests against React's browser build.

## Gates before pipeline conversion

This unit does not establish the future single-job 65536 MiB workspace fit, the
application's Release checks, all four final application images, production
runtime compatibility, or deployment readiness. A later reviewed conversion must
retain every existing required check and overlay only reviewed application files,
configuration, ownership and entrypoints onto these prepared runtime bases.

Private immutable publication/transport of the SDK and runtime archives remains
unresolved. Admission requires actual prepared digests, completed receipts and
smoke evidence, independent review, the runner's immutable-image policy and its
separate real bounded-workspace acceptance gate. Do not insert local tags or
unpublished digests into the active pipeline. API/database/migration/public-site
contracts do not change in this dependency-only unit.
