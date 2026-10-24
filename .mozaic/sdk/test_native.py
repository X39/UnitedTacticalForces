"""Native preparation source contracts; real cgroup/transport acceptance is separate."""
import hashlib
import importlib.util
import json
import os
import subprocess
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("utf_native", ROOT / "native-build.py")
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


def request():
    return {"mode": "build", "identity": "a" * 32, "slice": "utf-preparation.slice",
            "cgroup_path": "/user.slice/user-1000.slice/utf-preparation.slice",
            "graph_root": "/home/user/.local/share/containers/storage",
            "heartbeat": "/mnt/a/releases/exclusive/native-heartbeat",
            "deadline": time.time() + 120, "profile": "worker", "context": "/mnt/a/releases/exclusive/context",
            "files": {name: hashlib.sha256(b"dependency").hexdigest() for name in native.required_files("worker")}}


class NativeContracts(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="utf-native-contract-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def test_closed_profiles_reject_application_or_missing_inputs(self):
        native.validate_request(request())
        for name in ("sources/Api/Api.csproj", "credentials.json", "../app", "Dockerfile"):
            value = request()
            value["files"][name] = "a" * 64
            with self.assertRaises(ValueError):
                native.validate_request(value)
        value = request()
        value["files"].pop("capture-apt.sh")
        with self.assertRaises(ValueError):
            native.validate_request(value)

    def test_request_identity_paths_deadline_and_scope_fields_fail_closed(self):
        for key, value in (("identity", "../escape"), ("slice", "system.slice:bad:scope"),
                           ("cgroup_path", "/user.slice/../utf-preparation.slice"),
                           ("graph_root", "/var/lib/containers/storage"), ("profile", "app"),
                           ("context", "/mnt/a/../repository"), ("context", "/home/user/repository"),
                           ("heartbeat", "/mnt/a/../heartbeat"), ("deadline", time.time() - 1)):
            with self.subTest(key=key, value=value):
                changed = request()
                changed[key] = value
                with self.assertRaises(ValueError):
                    native.validate_request(changed)

    def test_staging_copies_only_reviewed_files_and_binds_bytes(self):
        source, target = self.root / "source", self.root / "stage"
        source.mkdir()
        value = request()
        for name in value["files"]:
            (source / name).write_bytes(b"dependency")
        (source / "credentials.json").write_bytes(b"must not copy")
        native.stage_context(source, target, value["files"], time.time() + 5)
        self.assertEqual({path.name for path in target.iterdir()}, set(value["files"]))
        with self.assertRaises(FileExistsError):
            native.stage_context(source, target, value["files"], time.time() + 5)
        wrong = dict(value["files"])
        wrong["capture-apt.sh"] = "f" * 64
        with self.assertRaises(ValueError):
            native.stage_context(source, self.root / "bad-stage", wrong, time.time() + 5)

    def test_native_graph_root_and_identity_are_exact(self):
        info = {"store": {"graphRoot": request()["graph_root"]}, "host": {
            "security": {"rootless": True}, "os": "linux", "arch": "amd64", "cgroupManager": "systemd",
            "cgroupVersion": "v2", "cgroupControllers": ["cpu", "memory", "pids"], "idMappings": {
                kind: [{"container_id": 0, "host_id": 1000, "size": 1}] for kind in ("uidmap", "gidmap")}}}
        native.validate_host(info, request()["graph_root"], 1000, 1000)
        for uid, gid in ((0, 1000), (1000, 0)):
            with self.assertRaises(ValueError):
                native.validate_host(info, request()["graph_root"], uid, gid)
        with self.assertRaises(ValueError):
            native.validate_host(info, "/different/rootless/store", 1000, 1000)
        info["host"]["security"]["rootless"] = False
        with self.assertRaises(ValueError):
            native.validate_host(info, request()["graph_root"], 1000, 1000)

    def test_real_heartbeat_age_is_checked(self):
        heartbeat = self.root / "heartbeat"
        heartbeat.write_text("operator alive")
        value = {"heartbeat": str(heartbeat)}
        native.check_heartbeat(value)
        old = time.time() - 60
        os.utime(heartbeat, (old, old))
        with self.assertRaises(TimeoutError):
            native.check_heartbeat(value)

    def test_scope_cleanup_never_stops_parent_or_substituted_scope(self):
        parent = Path("/sys/fs/cgroup/user.slice/utf-preparation.slice")
        scope = "mozaic-sdk-utf" + "a" * 32 + "-worker.scope"
        good = subprocess.CompletedProcess([], 0, str(parent / scope).removeprefix("/sys/fs/cgroup") + "\n", "")
        with patch.object(native.subprocess, "run", return_value=good) as execute:
            native.stop_owned_scope(parent, scope)
            self.assertEqual(execute.call_args_list[-1].args[0], ["/usr/bin/systemctl", "--user", "stop", scope])
        for path in (str(parent).removeprefix("/sys/fs/cgroup"), "/user.slice/other.scope"):
            bad = subprocess.CompletedProcess([], 0, path, "")
            with patch.object(native.subprocess, "run", return_value=bad) as execute:
                with self.assertRaises(ValueError):
                    native.stop_owned_scope(parent, scope)
                self.assertEqual(execute.call_count, 1)

    def test_stale_transport_supervision_stops_process_group_and_own_scope(self):
        child = MagicMock(pid=12345)
        child.poll.return_value = None
        with (patch.object(native, "check_heartbeat", side_effect=[None, TimeoutError("stale")]),
              patch.object(native.subprocess, "Popen", return_value=child) as start,
              patch.object(native.os, "killpg", create=True) as kill,
              patch.object(native, "stop_owned_scope") as stop,
              patch.object(native.signal, "signal"),
              patch.object(native.signal, "SIGKILL", 9, create=True),
              patch.object(native.signal, "SIGHUP", 1, create=True)):
            with self.assertRaises(TimeoutError):
                native.supervise(["dependency-probe"], request(), Path("/sys/fs/cgroup/parent"), "owned.scope")
            self.assertTrue(start.call_args.kwargs["start_new_session"])
            self.assertEqual([call.args for call in kill.call_args_list],
                             [(12345, native.signal.SIGTERM), (12345, native.signal.SIGKILL)])
            stop.assert_called_once_with(Path("/sys/fs/cgroup/parent"), "owned.scope")
        # This checks routing only. The real process-tree/scope termination gate
        # must still run against the accepted release VM before SDK preparation.


if __name__ == "__main__":
    unittest.main()
