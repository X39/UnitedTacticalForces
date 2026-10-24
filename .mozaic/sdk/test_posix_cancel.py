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
        json.dump({'child': os.getpid(), 'leader': os.getppid(), 'group': os.getpgrp(),
                   'blocked': [int(value) for value in signal.pthread_sigmask(signal.SIG_BLOCK, [])]}, ready)
    while True:
        time.sleep(0.05)
else:
    while True:
        time.sleep(0.05)
"""


class PosixCancellation(unittest.TestCase):
    def test_successful_leader_late_restore_cancel_still_finalizes_descendant(self):
        if sys.platform != "linux" or not hasattr(os, "fork"):
            self.fail("This real regression requires Linux/POSIX.")
        with tempfile.TemporaryDirectory(prefix="utf-owned-completion-gap-") as directory:
            ready = Path(directory) / "owned-child.json"
            events = (signal.SIGTERM, signal.SIGHUP, signal.SIGINT)
            handlers = {event: signal.getsignal(event) for event in events}
            real_signal, real_killpg, real_popen = signal.signal, os.killpg, subprocess.Popen
            launched, observed, killed = [], [], []
            stopped, delivered = False, False
            leader = LEADER.replace(
                "else:\n    while True:\n        time.sleep(0.05)",
                "else:\n    while not os.path.exists(sys.argv[1]):\n        time.sleep(0.01)\n    time.sleep(0.05)\n    sys.exit(0)")

            def raising_handler(_number, _frame):
                raise InterruptedError("previous handler restored")

            def launch(*arguments, **options):
                child = real_popen(*arguments, **options)
                launched.append(child)
                return child

            real_observe = native.observe_leader_exit

            def observe(process):
                value = real_observe(process)
                if value is not None:
                    observed.append(value)
                    owned = json.loads(ready.read_text())
                    self.assertTrue(Path(f"/proc/{owned['child']}").exists(), "Completion fixture did not leave a descendant")
                    self.assertIsNone(process.returncode, "Leader was reaped before owned group finalization")
                    self.assertTrue(Path(f"/proc/{process.pid}").exists(), "Owned PID reservation disappeared")
                return value

            def kill(group, event):
                # The actual leader must still reserve its PID at both signals.
                self.assertTrue(Path(f"/proc/{group}").exists(), "Group signal risked a recycled PID")
                killed.append(event)
                return real_killpg(group, event)

            def stop(_parent, _scope):
                nonlocal stopped
                stopped = True

            def restore(event, handler):
                nonlocal delivered
                previous = real_signal(event, handler)
                if handler is raising_handler and not delivered:
                    self.assertTrue(stopped, "Late cancellation reached restore before scope finalization")
                    self.assertEqual(killed, [signal.SIGTERM, signal.SIGKILL])
                    self.assertEqual(launched[0].returncode, 0)
                    # HUP still has its nonraising callback while the first
                    # previous TERM handler is restored: exact completion race.
                    delivered = True
                    os.kill(os.getpid(), signal.SIGHUP)
                return previous

            try:
                for event in events:
                    real_signal(event, raising_handler)
                with (patch.object(native, "check_heartbeat"),
                      patch.object(native.subprocess, "Popen", side_effect=launch),
                      patch.object(native, "observe_leader_exit", side_effect=observe),
                      patch.object(native.os, "killpg", side_effect=kill),
                      patch.object(native, "stop_owned_scope", side_effect=stop),
                      patch.object(native.signal, "signal", side_effect=restore)):
                    with self.assertRaises(InterruptedError):
                        native.supervise([sys.executable, "-c", leader, str(ready)],
                                         {"deadline": time.time() + 30},
                                         Path("/sys/fs/cgroup/utf-test-not-created"), "owned-late-completion.scope")
                self.assertEqual(observed, [0])
                self.assertTrue(delivered)
                owned = json.loads(ready.read_text())
                deadline = time.monotonic() + 2
                while Path(f"/proc/{owned['child']}").exists() and time.monotonic() < deadline:
                    time.sleep(0.02)
                self.assertFalse(Path(f"/proc/{owned['child']}").exists(), "Successful leader left its descendant")
                self.assertFalse(Path(f"/proc/{owned['leader']}").exists(), "Successful leader was not reaped")
                for event in events:
                    self.assertIs(signal.getsignal(event), raising_handler)
            finally:
                for child in launched:
                    if child.pid != os.getpgrp():
                        try:
                            real_killpg(child.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        child.wait(timeout=1)
                for event, handler in handlers.items():
                    real_signal(event, handler)

    def test_second_hup_and_int_cannot_interrupt_cleanup_or_early_restore(self):
        if sys.platform != "linux" or not hasattr(os, "fork"):
            self.fail("This real regression requires Linux/POSIX.")
        with tempfile.TemporaryDirectory(prefix="utf-owned-second-signal-") as directory:
            ready = Path(directory) / "owned-child.json"
            events = (signal.SIGTERM, signal.SIGHUP, signal.SIGINT)
            handlers = {event: signal.getsignal(event) for event in events}
            real_signal, real_killpg = signal.signal, os.killpg
            delivered, killed = [], []
            checks, stopped = 0, False

            def raising_handler(_number, _frame):
                raise InterruptedError("a prior raising handler interrupted owned cleanup")

            def second_signals():
                for event in (signal.SIGHUP, signal.SIGINT):
                    delivered.append(event)
                    os.kill(os.getpid(), event)

            def install(event, handler):
                if handler is raising_handler and not stopped:
                    self.fail("Prior raising handler restored before owned cleanup completed")
                previous = real_signal(event, handler)
                # Reproduce the old transition gap if SIG_IGN switching returns.
                # The corrected implementation has no such transition; its same
                # real signals are delivered by the TERM-grace hook below.
                if event == signal.SIGTERM and handler == signal.SIG_IGN and ready.exists():
                    second_signals()
                return previous

            def term_grace(group, event):
                killed.append(event)
                result = real_killpg(group, event)
                if event == signal.SIGTERM and not delivered:
                    second_signals()
                return result

            def heartbeat(_request):
                nonlocal checks
                checks += 1
                if checks == 1:
                    return
                deadline = time.monotonic() + 3
                while not ready.exists():
                    if time.monotonic() >= deadline:
                        raise AssertionError("Second-signal fixture failed to become ready")
                    time.sleep(0.01)
                raise TimeoutError("initial transport cancellation")

            def stop(_parent, _scope):
                nonlocal stopped
                # Signals arriving as scope querying starts must also remain
                # nonraising; no actual DBus request or cgroup is used here.
                second_signals()
                stopped = True

            try:
                for event in events:
                    real_signal(event, raising_handler)
                with (patch.object(native, "check_heartbeat", side_effect=heartbeat),
                      patch.object(native.signal, "signal", side_effect=install),
                      patch.object(native.os, "killpg", side_effect=term_grace),
                      patch.object(native, "stop_owned_scope", side_effect=stop)):
                    with self.assertRaises(TimeoutError):
                        native.supervise([sys.executable, "-c", LEADER, str(ready)],
                                         {"deadline": time.time() + 12},
                                         Path("/sys/fs/cgroup/utf-test-not-created"), "owned-second-signal.scope")
                self.assertEqual(delivered, [signal.SIGHUP, signal.SIGINT] * 2)
                self.assertEqual(killed, [signal.SIGTERM, signal.SIGKILL])
                self.assertTrue(stopped)
                for event in events:
                    self.assertIs(signal.getsignal(event), raising_handler)
                owned = json.loads(ready.read_text())
                deadline = time.monotonic() + 2
                while Path(f"/proc/{owned['child']}").exists() and time.monotonic() < deadline:
                    time.sleep(0.02)
                self.assertFalse(Path(f"/proc/{owned['child']}").exists(), "Second signal leaked descendant")
                self.assertFalse(Path(f"/proc/{owned['leader']}").exists(), "Second signal leaked leader")
            finally:
                if ready.exists():
                    owned = json.loads(ready.read_text())
                    if owned["group"] == owned["leader"] and owned["group"] != os.getpgrp():
                        try:
                            real_killpg(owned["group"], signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                for event, handler in handlers.items():
                    real_signal(event, handler)

    def test_term_inside_popen_return_gap_reaps_real_child_group(self):
        if sys.platform != "linux" or not hasattr(os, "fork"):
            self.fail("This real regression requires Linux/POSIX.")
        with tempfile.TemporaryDirectory(prefix="utf-owned-launch-gap-") as directory:
            ready = Path(directory) / "owned-child.json"
            events = (signal.SIGTERM, signal.SIGHUP, signal.SIGINT)
            handlers = {event: signal.getsignal(event) for event in events}
            real_popen, real_killpg = subprocess.Popen, os.killpg
            launched = []

            def cancel(_number, _frame):
                raise InterruptedError("signal delivered to supervisor")

            def launch_then_signal(*arguments, **options):
                child = real_popen(*arguments, **options)
                launched.append(child)
                deadline = time.monotonic() + 3
                while not ready.exists():
                    if time.monotonic() >= deadline:
                        raise AssertionError("Launch-gap fixture failed to become ready")
                    time.sleep(0.01)
                owned = json.loads(ready.read_text())
                self.assertEqual(child.pid, owned["group"])
                self.assertEqual(os.getpgid(child.pid), child.pid)
                self.assertFalse(set(owned["blocked"]) & set(events), "Child inherited a blocked cancellation signal")
                # Deterministic real signal after child/session creation but
                # before the constructor wrapper returns to assign process.
                os.kill(os.getpid(), signal.SIGTERM)
                return child

            try:
                for event in events:
                    signal.signal(event, cancel)
                with (patch.object(native, "check_heartbeat"),
                      patch.object(native.subprocess, "Popen", side_effect=launch_then_signal),
                      patch.object(native, "stop_owned_scope") as stop):
                    with self.assertRaises(InterruptedError):
                        native.supervise([sys.executable, "-c", LEADER, str(ready)],
                                         {"deadline": time.time() + 12},
                                         Path("/sys/fs/cgroup/utf-test-not-created"), "owned-launch-test.scope")
                    stop.assert_called_once()
                self.assertEqual(len(launched), 1)
                self.assertEqual(launched[0].returncode, 0, "Leader did not receive normal unblocked TERM")
                owned = json.loads(ready.read_text())
                deadline = time.monotonic() + 2
                while Path(f"/proc/{owned['child']}").exists() and time.monotonic() < deadline:
                    time.sleep(0.02)
                self.assertFalse(Path(f"/proc/{owned['child']}").exists(), "Launch-gap descendant survived")
                self.assertFalse(Path(f"/proc/{owned['leader']}").exists(), "Launch-gap leader was not reaped")
            finally:
                for child in launched:
                    if child.pid != os.getpgrp():
                        try:
                            real_killpg(child.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        child.wait(timeout=1)
                for event, handler in handlers.items():
                    signal.signal(event, handler)

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
