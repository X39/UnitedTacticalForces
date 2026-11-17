"""Opt-in Linux GNU-timeout proof using trusted local Node/timeout inputs."""
import importlib.util
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("utf_smoke_native_boundary", ROOT / "native-build.py")
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


@unittest.skipUnless(sys.platform == "linux" and os.environ.get("UTF_NODE_BINARY"),
                     "requires explicit trusted Linux Node input")
class SmokeProcessBoundary(unittest.TestCase):
    def test_gnu_kill_after_stops_a_term_resistant_blocked_node_event_loop(self):
        node = str(Path(os.environ["UTF_NODE_BINARY"]).resolve(strict=True))
        timeout = str(Path(os.environ.get("UTF_TIMEOUT_BINARY", "/usr/bin/timeout")).resolve(strict=True))
        version = subprocess.run([timeout, "--version"], capture_output=True, text=True, timeout=5, check=True)
        self.assertIn("GNU coreutils", version.stdout)
        with tempfile.TemporaryDirectory(prefix="utf-smoke-boundary-") as directory:
            root = Path(directory)
            heartbeat = root / "heartbeat"
            heartbeat.write_text("test-owned supervision")
            pid_file = root / "node.pid"
            command = [timeout, "--signal=TERM", "--kill-after=0.2", "0.3", node,
                       str(ROOT / "test-smoke-browser.cjs"), "--fixture", "blocked-event-loop", str(pid_file)]
            started = time.monotonic()
            with patch.object(native, "stop_owned_scope") as cleanup:
                with self.assertRaises(native.CommandFailure):
                    native.supervise(command, {"heartbeat": str(heartbeat), "deadline": time.time() + 20},
                                     root, "test-owned.scope")
            cleanup.assert_called_once_with(root, "test-owned.scope")
            self.assertLess(time.monotonic() - started, 8)
            self.assertTrue(pid_file.is_file())
            pid = int(pid_file.read_text())
            limit = time.monotonic() + 3
            while Path(f"/proc/{pid}").exists() and time.monotonic() < limit:
                time.sleep(0.02)
            self.assertFalse(Path(f"/proc/{pid}").exists(), "GNU-timeout-owned Node process remains")
        # Scope routing is substituted above. Container PID-namespace cleanup of
        # real browser descendants requires the separate reviewed engine gate.


if __name__ == "__main__":
    unittest.main()
