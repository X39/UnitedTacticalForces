"""Real POSIX group regression; no engine, cgroups, DBus or application execution."""
import importlib.util
import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("utf_native", ROOT / "native-build.py")
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)

LEADER = """
import json, os, signal, sys, time
signal.signal(signal.SIGTERM, lambda *_: sys.exit(0))
child = os.fork()
if child == 0:
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    with open(sys.argv[1], 'x') as ready:
        json.dump({'child': os.getpid(), 'leader': os.getppid(), 'group': os.getpgrp()}, ready)
    while True:
        time.sleep(0.05)
else:
    while True:
        time.sleep(0.05)
"""


class PosixCancellation(unittest.TestCase):
    def test_exited_leader_resistant_descendant_and_hung_show(self):
        if sys.platform != "linux" or not hasattr(os, "fork"):
            self.fail("This real regression requires Linux/POSIX; run the documented release-VM command.")
        with tempfile.TemporaryDirectory(prefix="utf-owned-cancel-") as directory:
            ready = Path(directory) / "owned-child.json"
            original_handlers = {event: signal.getsignal(event) for event in
                                 (signal.SIGTERM, signal.SIGHUP, signal.SIGINT)}
            real_run = subprocess.run
            checks, groups, queries = 0, [], []
            real_killpg = os.killpg

            def stale_after_ready(_request):
                nonlocal checks
                checks += 1
                if checks == 1:
                    return
                deadline = time.monotonic() + 3
                while not ready.exists():
                    if time.monotonic() >= deadline:
                        raise AssertionError("Test-owned descendant failed to become ready")
                    time.sleep(0.01)
                owned = json.loads(ready.read_text())
                self.assertEqual(owned["leader"], owned["group"])
                raise TimeoutError("simulate transport heartbeat loss")

            def hung_show(command, **options):
                self.assertEqual(command[:3], ["/usr/bin/systemctl", "--user", "show"])
                queries.append(command)
                # A real hung subprocess with the production two-second bound,
                # without contacting or changing the real user DBus manager.
                return real_run([sys.executable, "-c", "import time; time.sleep(60)"], **options)

            def record_kill(group, event):
                groups.append((group, event))
                return real_killpg(group, event)

            try:
                started = time.monotonic()
                with (patch.object(native, "check_heartbeat", side_effect=stale_after_ready),
                      patch.object(native.subprocess, "run", side_effect=hung_show),
                      patch.object(native.os, "killpg", side_effect=record_kill)):
                    with self.assertRaises(subprocess.TimeoutExpired):
                        native.supervise([sys.executable, "-c", LEADER, str(ready)],
                                         {"deadline": time.time() + 12},
                                         Path("/sys/fs/cgroup/utf-test-not-created"), "owned-test.scope")
                self.assertLess(time.monotonic() - started, 10)
                owned = json.loads(ready.read_text())
                self.assertEqual(groups, [(owned["group"], signal.SIGTERM), (owned["group"], signal.SIGKILL)])
                self.assertEqual(len(queries), 1)
                # Leader was reaped by supervise; the orphan is reaped by init.
                deadline = time.monotonic() + 2
                while Path(f"/proc/{owned['child']}").exists() and time.monotonic() < deadline:
                    time.sleep(0.02)
                self.assertFalse(Path(f"/proc/{owned['child']}").exists(), "TERM-resistant descendant survived")
                self.assertFalse(Path(f"/proc/{owned['leader']}").exists(), "Leader was not reaped")
            finally:
                if ready.exists():
                    # Only the verified process group created by this fixture.
                    owned = json.loads(ready.read_text())
                    if owned["group"] == owned["leader"] and owned["group"] != os.getpgrp():
                        try:
                            real_killpg(owned["group"], signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                for event, handler in original_handlers.items():
                    signal.signal(event, handler)


if __name__ == "__main__":
    unittest.main()
