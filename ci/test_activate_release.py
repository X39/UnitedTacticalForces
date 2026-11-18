"""Backup, activation and verification steps (no network, no host)."""

import importlib.util
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("activate_release", HERE / "activate_release.py")
ACTIVATE = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ACTIVATE)

API, WEB, OLD = "a" * 64, "b" * 64, "c" * 64
REVISION = "1234567890abcdef1234567890abcdef12345678"
SITE = "https://example.test"


class FakeHost:
    """Answers the fixed commands; failures and the running images are configurable."""

    def __init__(self, fail=(), running=None):
        self.fail, self.calls = set(fail), []
        self.running = running or f"sha256:{API} {WEB}"
        self.rolled_back = False

    def __call__(self, command):
        name = next((key for key, value in ACTIVATE.commands(API, WEB, REVISION).items() if value == command), None)
        self.calls.append(name)
        if name == "rollback":
            self.rolled_back = True
        if name in self.fail:
            return 1, ""
        return 0, self.running if name == "probe" else ""


def site(revision=REVISION, api=401):
    def fetcher(url, timeout=20):
        if url.endswith("/revision.json"):
            return 200, json.dumps({"revision": "git:" + revision}).encode()
        return api, b""
    return fetcher


class Clock:
    def __init__(self):
        self.now = 0.0

    def __call__(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds


def activate(host, fetcher):
    clock = Clock()
    return ACTIVATE.activate(host, API, WEB, REVISION, SITE, 60, fetcher=fetcher, sleep=clock.sleep, clock=clock)


class ActivateTests(unittest.TestCase):
    def test_success_runs_every_step_in_order_and_never_rolls_back(self):
        host = FakeHost()
        self.assertEqual(activate(host, site()), 0)
        self.assertEqual(host.calls, ["images", "backup", "tag", "stop_old", "switch", "probe", "retire_old"])
        self.assertFalse(host.rolled_back)

    def test_a_failed_backup_stops_before_anything_changes(self):
        host = FakeHost(fail={"backup"})
        self.assertEqual(activate(host, site()), 1)
        self.assertEqual(host.calls, ["images", "backup"])

    def test_missing_images_stop_before_the_backup(self):
        host = FakeHost(fail={"images"})
        self.assertEqual(activate(host, site()), 1)
        self.assertEqual(host.calls, ["images"])

    def test_a_failed_switch_restores_the_previous_images(self):
        host = FakeHost(fail={"switch"})
        self.assertEqual(activate(host, site()), 1)
        self.assertTrue(host.rolled_back)

    def test_a_failed_stop_of_the_retired_service_rolls_back_before_starting_anything(self):
        host = FakeHost(fail={"stop_old"})
        self.assertEqual(activate(host, site()), 1)
        self.assertEqual(host.calls, ["images", "backup", "tag", "stop_old", "rollback"])

    def test_a_failed_retirement_of_old_units_does_not_fail_the_release(self):
        host = FakeHost(fail={"retire_old"})
        self.assertEqual(activate(host, site()), 0)
        self.assertFalse(host.rolled_back)

    def test_rollback_falls_back_to_the_retired_service_when_no_previous_image_exists(self):
        rollback = ACTIVATE.commands(API, WEB, REVISION)["rollback"]
        self.assertIn("mozaic-utf-ordinary.service", rollback)
        self.assertIn("podman image exists localhost/utf/api:previous", rollback)

    def test_containers_on_other_images_fail_and_roll_back(self):
        host = FakeHost(running=f"{OLD} {WEB}")
        self.assertEqual(activate(host, site()), 1)
        self.assertTrue(host.rolled_back)

    def test_a_site_serving_another_revision_fails_and_rolls_back(self):
        host = FakeHost()
        self.assertEqual(activate(host, site(revision="f" * 40)), 1)
        self.assertTrue(host.rolled_back)

    def test_a_broken_public_api_fails_and_rolls_back(self):
        host = FakeHost()
        self.assertEqual(activate(host, site(api=502)), 1)
        self.assertTrue(host.rolled_back)

    def test_the_commands_hold_only_validated_inputs(self):
        for command in ACTIVATE.commands(API, WEB, REVISION).values():
            self.assertNotIn("\n", command)
        self.assertIn(f"utf-{REVISION}.dump", ACTIVATE.commands(API, WEB, REVISION)["backup"])
        self.assertTrue(ACTIVATE.IMAGE_ID.fullmatch(API) and not ACTIVATE.IMAGE_ID.fullmatch("a;rm -rf /"))
        self.assertTrue(ACTIVATE.REVISION.fullmatch(REVISION) and not ACTIVATE.REVISION.fullmatch("abc; id"))

    def test_the_public_check_needs_a_json_revision_not_the_single_page_fallback(self):
        html = lambda url, timeout=20: (200, b"<!doctype html><title>UTF</title>")
        self.assertIsNotNone(ACTIVATE.public_check(SITE, REVISION, html))
        self.assertIsNone(ACTIVATE.public_check(SITE, REVISION, site()))


if __name__ == "__main__":
    unittest.main()
