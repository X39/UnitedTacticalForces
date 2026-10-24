"""Dependency-only native BUILD on the inspected existing rootless release VM.

Remote Podman BUILD does not propagate the required systemd runtime flag. This
helper stages a closed dependency context and uses only the proven native path.
It never provisions a cgroup, changes authentication, or copies application code.
"""
import base64
import hashlib
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import time
from pathlib import Path

PROFILES = {"api", "worker", "aska", "sdk"}
SCRIPT_FILES = {"capture-apt.sh", "sdk-dependencies.sh", "select-dotnet.sh",
                "smoke-browser.cjs", "inputs.json", "sdk.Containerfile"}
ARCHIVES = {"archives/" + name for name in
            ("api.oci.tar", "worker.oci.tar", "aska.oci.tar", "nginx.oci.tar", "SHA256SUMS")}


class OwnershipFailure(ValueError):
    """A source, host, stage or cleanup identity failed its existing fence."""


class CommandFailure(ValueError):
    """A bounded native command returned an unaccepted status."""


class FutureHeartbeat(TimeoutError):
    """The observed shared-file timestamp is ahead of the native clock."""


class StaleHeartbeat(TimeoutError):
    """The observed heartbeat exceeded the existing freshness bound."""


def failure_message(error):
    """Return only a fixed reason code; never interpolate exception data."""
    if isinstance(error, FutureHeartbeat):
        code = "HEARTBEAT_FUTURE"
    elif isinstance(error, StaleHeartbeat):
        code = "HEARTBEAT_STALE"
    elif isinstance(error, OwnershipFailure):
        code = "OWNERSHIP_FAILURE"
    elif isinstance(error, (CommandFailure, subprocess.CalledProcessError)):
        code = "COMMAND_NONZERO"
    elif isinstance(error, InterruptedError):
        code = "CANCELLED"
    elif isinstance(error, (TimeoutError, subprocess.TimeoutExpired)):
        code = "TIMEOUT"
    else:
        code = "INTERNAL_ERROR"
    return "Native preparation failed [" + code + "]; retain attempt logs/stage and inspect exact owned scope."


def required_files(profile):
    if profile not in PROFILES:
        raise ValueError("unknown dependency profile")
    if profile == "sdk":
        return SCRIPT_FILES | ARCHIVES
    return {profile + ".Containerfile", "capture-apt.sh", "runtime-dependencies.sh"}


def validate_request(value):
    if (not isinstance(value, dict) or value.get("mode") not in {"preflight", "build"}
            or not re.fullmatch(r"[a-f0-9]{32}", value.get("identity", ""))
            or not re.fullmatch(r"[a-z0-9-]+\.slice", value.get("slice", ""))
            or not re.fullmatch(r"/user\.slice/[A-Za-z0-9_./@:-]+", value.get("cgroup_path", ""))
            or any(part in {".", ".."} for part in value["cgroup_path"].split("/"))
            or not value["cgroup_path"].endswith("/" + value["slice"])
            or value.get("graph_root") != "/home/user/.local/share/containers/storage"
            or not re.fullmatch(r"/mnt/[a-z]/[^\x00\r\n]+", value.get("heartbeat", ""))
            or any(part in {".", ".."} for part in value["heartbeat"].split("/"))
            or type(value.get("deadline")) not in {int, float}
            or not time.time() < value["deadline"] <= time.time() + 7200):
        raise ValueError("invalid native preparation contract")
    if value["mode"] == "build":
        expected = required_files(value.get("profile"))
        if (not isinstance(value.get("files"), dict) or set(value["files"]) != expected
                or any(not re.fullmatch(r"[a-f0-9]{64}", digest) for digest in value["files"].values())
                or not re.fullmatch(r"/mnt/[a-z]/[^\x00\r\n]+", value.get("context", ""))
                or any(part in {".", ".."} for part in value["context"].split("/"))):
            raise ValueError("native context is not the closed reviewed dependency set")
    return value


def run(arguments, request, seconds=30, capture=True, accepted=(0,)):
    remaining = request["deadline"] - time.time()
    if remaining <= 0:
        raise TimeoutError("native preparation deadline reached")
    # The outer host timeout also bounds this helper if local filesystem I/O hangs.
    completed = subprocess.run(arguments, check=False, text=True, timeout=min(seconds, remaining),
                               stdout=subprocess.PIPE if capture else None,
                               stderr=subprocess.PIPE if capture else None)
    if completed.returncode not in accepted:
        raise CommandFailure("native preparation command failed")
    return completed.stdout


def validate_host(info, graph_root, uid, gid):
    host = info["host"]
    if (uid != 1000 or gid != 1000 or host["security"]["rootless"] is not True
            or host["os"] != "linux" or host["arch"] != "amd64"
            or host["cgroupManager"] != "systemd" or host["cgroupVersion"] != "v2"
            or info["store"]["graphRoot"] != graph_root
            or not {"cpu", "memory", "pids"}.issubset(host["cgroupControllers"])):
        raise OwnershipFailure("native engine does not match inspected rootless release identity")
    for kind in ("uidmap", "gidmap"):
        roots = [item for item in host["idMappings"][kind] if item["container_id"] == 0]
        if roots != [{"container_id": 0, "host_id": 1000, "size": 1}]:
            raise OwnershipFailure("native engine identity mapping changed")


def preflight(request):
    if sys.platform != "linux" or not hasattr(os, "waitid") or not hasattr(os, "WNOWAIT"):
        raise ValueError("native supervision requires Linux nonreaping child-exit observation")
    if signal.getsignal(signal.SIGCHLD) != signal.SIG_DFL:
        raise ValueError("native supervision requires default SIGCHLD handling to reserve owned PIDs")
    info = json.loads(run(["/usr/bin/podman", "--remote=false", "info", "--format=json"], request))
    validate_host(info, request["graph_root"], os.getuid(), os.getgid())
    if os.stat("/var/tmp").st_dev != os.stat(request["graph_root"]).st_dev:
        raise OwnershipFailure("native staging must share the monitored image-store filesystem")
    observed = run(["/usr/bin/systemctl", "--user", "show", request["slice"],
                    "--property=ControlGroup", "--value"], request).strip()
    if observed != request["cgroup_path"]:
        raise OwnershipFailure("systemd slice and inspected cgroup path differ")
    parent = Path("/sys/fs/cgroup" + observed)
    expected = {"cpu.max": "600000 100000", "memory.max": "12884901888",
                "memory.swap.max": "0", "pids.max": "512"}
    for name, value in expected.items():
        if (parent / name).read_text().strip() != value:
            raise OwnershipFailure("parent resource bounds differ from accepted six-CPU/12-GiB/512-PID contract")
    if ("populated 0" not in (parent / "cgroup.events").read_text().splitlines()
            or not {"cpu", "memory", "pids"}.issubset((parent / "cgroup.controllers").read_text().split())):
        raise OwnershipFailure("preparation parent is occupied or lacks resource controllers")
    return parent


def plain_file(path):
    for ancestor in [path, *path.parents]:
        if ancestor.is_symlink():
            raise OwnershipFailure("native source ancestry contains a symlink")
    if not stat.S_ISREG(path.lstat().st_mode):
        raise OwnershipFailure("native context input is not a regular file")


def stage_context(source, destination, files, deadline):
    """Copy only authenticated regular inputs; no tar extraction or directory copy."""
    destination.mkdir(mode=0o700)  # Exclusive: an existing attempt path is never reused.
    for name, expected in files.items():
        origin = source / name
        plain_file(origin)
        target = destination / name
        target.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        hasher = hashlib.sha256()
        with origin.open("rb") as incoming, target.open("xb") as outgoing:
            while block := incoming.read(1024**2):
                if time.time() >= deadline:
                    raise TimeoutError("native staging deadline reached")
                hasher.update(block)
                outgoing.write(block)
        if hasher.hexdigest() != expected:
            raise OwnershipFailure("staged dependency input hash differs from reviewed context")


def check_heartbeat(request):
    path = Path(request["heartbeat"])
    plain_file(path)
    age = time.time() - path.stat().st_mtime
    if age < 0:
        raise FutureHeartbeat("operator heartbeat timestamp is ahead of the native clock")
    if not age <= 45:
        raise StaleHeartbeat("operator heartbeat is stale; stop the owned attempt")


def stop_owned_scope(parent, scope):
    """Stop only the unpredictable, preclaimed attempt scope, never its parent."""
    result = subprocess.run(["/usr/bin/systemctl", "--user", "show", scope,
                             "--property=ControlGroup", "--value"],
                            capture_output=True, text=True, timeout=2, check=False)
    observed = result.stdout.strip()
    expected = str(parent / scope).removeprefix("/sys/fs/cgroup")
    if observed == expected:
        subprocess.run(["/usr/bin/systemctl", "--user", "stop", scope],
                       capture_output=True, timeout=5, check=True)
        if (parent / scope).exists():
            raise OwnershipFailure("owned scope remains after stop; retain failed attempt for inspection")
    elif observed or (parent / scope).exists():
        raise OwnershipFailure("scope identity changed; refuse broad cleanup and retain stage")


def observe_leader_exit(process):
    """Peek without reaping, reserving the owned PID/PGID until finalization."""
    observed = os.waitid(os.P_PID, process.pid, os.WEXITED | os.WNOHANG | os.WNOWAIT)
    if observed is None:
        return None
    if observed.si_code == os.CLD_EXITED:
        return observed.si_status
    if observed.si_code in (os.CLD_KILLED, os.CLD_DUMPED):
        return -observed.si_status
    raise ValueError("unexpected native leader state")


def supervise(command, request, parent, scope):
    """A silent client loss stops RUN through the attempt scope after 45 seconds."""
    check_heartbeat(request)
    process = None
    success = False
    events = (signal.SIGTERM, signal.SIGHUP, signal.SIGINT)
    previous_handlers = {event: signal.getsignal(event) for event in events}
    pending_cancel = None

    def defer_cancel(number, _frame):
        nonlocal pending_cancel
        pending_cancel = number

    try:
        # Defer Python cancellation, not the OS signal mask. Popen's POSIX
        # exec-error-pipe handshake returns only after setsid/exec, so assignment
        # establishes the owned process/group before cancellation can raise.
        # Child exec retains normal unblocked signal behavior; no preexec hook is used.
        for event in events:
            signal.signal(event, defer_cancel)
        if pending_cancel is not None:
            raise InterruptedError("native preparation cancelled before process launch")
        process = subprocess.Popen(command, start_new_session=True)
        if pending_cancel is not None:
            raise InterruptedError("native preparation cancelled during process launch")
        while (returncode := observe_leader_exit(process)) is None:
            if pending_cancel is not None:
                raise InterruptedError("native preparation cancelled during supervision")
            check_heartbeat(request)
            if time.time() >= request["deadline"] - 15:
                raise TimeoutError("native work deadline reached; preserve cleanup allowance")
            time.sleep(0.25)
        if pending_cancel is not None:
            raise InterruptedError("native preparation cancelled before completion")
        if returncode != 0:
            raise CommandFailure("native dependency build failed")
        success = True
    finally:
        # Finalize every launched group, including normal leader exit. Nonreaping
        # observation above keeps its PID reserved, preventing PGID reuse before
        # the final KILL. Every callback remains nonraising until release.
        released = process is None
        try:
            if process is not None:
                try:
                    grace_deadline = time.monotonic() + 2
                    try:
                        os.killpg(process.pid, signal.SIGTERM)
                    except ProcessLookupError:
                        pass
                    finally:
                        # Do not reap the leader during grace: even a successful
                        # leader can leave descendants in its reserved group.
                        time.sleep(max(0, grace_deadline - time.monotonic()))
                        try:
                            os.killpg(process.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                finally:
                    try:
                        stop_owned_scope(parent, scope)
                    finally:
                        process.wait(timeout=1)
                released = True
        finally:
            if released:
                for event, handler in previous_handlers.items():
                    signal.signal(event, handler)
        if success and pending_cancel is not None:
            raise InterruptedError("native preparation cancelled during completion")


def build(request):
    parent = preflight(request)
    profile, identity = request["profile"], request["identity"]
    name = "utf" + identity + "-" + profile
    scope = "mozaic-sdk-" + name + ".scope"
    state = run(["/usr/bin/systemctl", "--user", "show", scope, "--property=LoadState", "--value"], request, accepted=(0, 1)).strip()
    if state != "not-found" or (parent / scope).exists():
        raise OwnershipFailure("attempt-owned build scope is stale or already exists")
    stage = Path("/var/tmp/utf-sdk-" + identity + "-" + profile)
    stage_context(Path(request["context"]), stage, request["files"], request["deadline"])
    if (stage.stat().st_uid, stage.stat().st_gid, stat.S_IMODE(stage.stat().st_mode)) != (1000, 1000, 0o700):
        raise OwnershipFailure("native stage ownership or permissions changed")
    tag = "localhost/utf-dependencies-" + profile + ":" + identity
    # All names are generated from validated profile/GUID fields. No caller-supplied
    # Podman arguments, application files, host mounts or engine socket enter RUN.
    work_seconds = min(3600, int(request["deadline"] - time.time()) - 15)
    if work_seconds < 1:
        raise TimeoutError("insufficient native build cleanup allowance")
    command = ["/usr/bin/timeout", "--signal=TERM", "--kill-after=5", str(work_seconds),
               "/usr/bin/podman", "--remote=false", "build", "--pull=never",
               "--authfile", str(stage / "anonymous-auth.json"), "--http-proxy=false",
               "--cpu-period=100000", "--cpu-quota=600000", "--memory=12g", "--memory-swap=12g",
               "--cgroupns=host", "--cgroup-parent", request["slice"] + ":mozaic-sdk:" + name,
               "--runtime-flag=systemd-cgroup", "--layers=false", "--force-rm", "--format=oci",
               "--tag", tag, "--file", str(stage / (profile + ".Containerfile")), str(stage)]
    # COPY lists only reviewed dependency inputs. This generated empty authfile is
    # not copied by any recipe; no registry or application credential is read.
    auth = stage / "anonymous-auth.json"
    auth.write_text('{"auths":{}}')
    print(json.dumps({"stage": str(stage), "scope": scope, "parent": str(parent), "tag": tag}), flush=True)
    # Failed attempts retain their stage even after bounded own-scope cancellation.
    supervise(command, request, parent, scope)
    if (parent / scope).exists():
        stop_owned_scope(parent, scope)
        raise OwnershipFailure("native build scope remains after successful CLI exit; retain stage for inspection")
    if stage.parent != Path("/var/tmp") or stage.name != "utf-sdk-" + identity + "-" + profile or stage.is_symlink():
        raise OwnershipFailure("unexpected native cleanup path")
    shutil.rmtree(stage)


if __name__ == "__main__":
    try:
        def cancelled(_number, _frame):
            raise InterruptedError("native preparation transport or operator cancelled")
        for event in (signal.SIGTERM, signal.SIGHUP, signal.SIGINT):
            signal.signal(event, cancelled)
        request = validate_request(json.loads(base64.b64decode(sys.argv[1], validate=True)))
        if request["mode"] == "preflight":
            preflight(request)
            print("Native rootless engine, graph root and empty bounded parent verified.")
        else:
            build(request)
    except Exception as error:
        raise SystemExit(failure_message(error)) from None
