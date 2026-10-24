"""Actual heartbeat/command diagnostics; these fixtures do not build an image."""
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
spec = importlib.util.spec_from_file_location("utf_native_diagnostics", ROOT / "native-build.py")
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


class NativeDiagnostics(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="utf-native-diagnostics-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.heartbeat = self.root / "heartbeat"
        self.heartbeat.write_text("fixture")

    def assert_code(self, error, code):
        message = native.failure_message(error)
        self.assertEqual(message, "Native preparation failed [" + code
                         + "]; retain attempt logs/stage and inspect exact owned scope.")
        self.assertNotIn("private-canary", message)

    def test_actual_future_and_stale_file_timestamps_have_distinct_codes(self):
        value = {"heartbeat": str(self.heartbeat)}
        native.check_heartbeat(value)
        for delta, kind, code in ((60, native.FutureHeartbeat, "HEARTBEAT_FUTURE"),
                                  (-60, native.StaleHeartbeat, "HEARTBEAT_STALE")):
            stamp = time.time() + delta
            os.utime(self.heartbeat, (stamp, stamp))
            with self.assertRaises(kind) as result:
                native.check_heartbeat(value)
            self.assert_code(result.exception, code)

    def test_real_nonzero_command_is_sanitized(self):
        with self.assertRaises(native.CommandFailure) as result:
            native.run([sys.executable, "-c", "import sys; print('private-canary'); sys.exit(7)"],
                       {"deadline": time.time() + 5})
        self.assert_code(result.exception, "COMMAND_NONZERO")

    def test_real_command_timeout_and_elapsed_deadline_are_sanitized(self):
        with self.assertRaises(subprocess.TimeoutExpired) as result:
            native.run([sys.executable, "-c", "import time; time.sleep(20)"],
                       {"deadline": time.time() + 5}, seconds=0.2)
        self.assert_code(result.exception, "TIMEOUT")
        with self.assertRaises(TimeoutError) as result:
            native.run(["private-canary-not-executed"], {"deadline": time.time() - 1})
        self.assert_code(result.exception, "TIMEOUT")

    def test_real_source_ownership_failure_and_unknown_error_are_sanitized(self):
        with self.assertRaises(native.OwnershipFailure) as result:
            native.stage_context(self.root, self.root / "stage", {"heartbeat": "0" * 64}, time.time() + 5)
        self.assert_code(result.exception, "OWNERSHIP_FAILURE")
        self.assert_code(RuntimeError("private-canary argv env /private/path"), "INTERNAL_ERROR")
        self.assert_code(InterruptedError("private-canary"), "CANCELLED")
        self.assert_code(subprocess.CalledProcessError(7, ["private-canary"],
                                                       output="private-canary", stderr="private-canary"),
                         "COMMAND_NONZERO")

    def test_cli_unknown_error_does_not_print_exception_or_arguments(self):
        result = subprocess.run([sys.executable, "-B", str(ROOT / "native-build.py"), "private-canary"],
                                capture_output=True, text=True, timeout=5)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")
        self.assertEqual(result.stderr.strip(), native.failure_message(RuntimeError()))

    @unittest.skipUnless(sys.platform == "linux", "requires real Linux nonreaping process supervision")
    def test_real_supervised_nonzero_and_deadline_reap_leader_and_release_scope(self):
        for command, remaining, kind, code in (
                ([sys.executable, "-c", "raise SystemExit(7)"], 60, native.CommandFailure, "COMMAND_NONZERO"),
                ([sys.executable, "-c", "import time; time.sleep(30)"], 15.5, TimeoutError, "TIMEOUT")):
            with self.subTest(code=code):
                children = []
                original = subprocess.Popen

                def launch(*args, **kwargs):
                    child = original(*args, **kwargs)
                    children.append(child)
                    return child

                with (patch.object(native.subprocess, "Popen", side_effect=launch),
                      patch.object(native, "stop_owned_scope") as cleanup):
                    with self.assertRaises(kind) as result:
                        native.supervise(command, {"heartbeat": str(self.heartbeat),
                                                  "deadline": time.time() + remaining},
                                         self.root, "fixture.scope")
                self.assert_code(result.exception, code)
                cleanup.assert_called_once_with(self.root, "fixture.scope")
                self.assertIsNotNone(children[0].returncode)
                with self.assertRaises(ProcessLookupError):
                    os.kill(children[0].pid, 0)


if __name__ == "__main__":
    unittest.main()
