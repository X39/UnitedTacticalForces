"""Credential-free release push: SSH client and key preparation (Linux; needs sh and ssh-keygen)."""

import base64
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CLIENT = load("stream_release", HERE / "stream_release.py")
MANIFEST_TYPE = "application/vnd.oci.image.manifest.v1+json"
CONFIG_TYPE = "application/vnd.oci.image.config.v1+json"
LAYER_TYPE = "application/vnd.oci.image.layer.v1.tar+gzip"

# Stand-in for the forced `podman load`: records stdin, then exits with the requested status.
STUB_SSH = """import os, sys
data = sys.stdin.buffer.read()
count = len([n for n in os.listdir(os.environ["STUB_DIR"]) if n.startswith("call-")])
with open(os.path.join(os.environ["STUB_DIR"], "call-%d" % count), "wb") as sink:
    sink.write(data)
sys.exit(int(os.environ.get("STUB_EXIT", "0")))
"""


def sha(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


def build_archive(seed):
    layer = (seed * 200).encode()
    config = json.dumps({"os": "linux", "architecture": "amd64", "seed": seed}).encode()
    manifest = json.dumps({
        "schemaVersion": 2, "mediaType": MANIFEST_TYPE,
        "config": {"mediaType": CONFIG_TYPE, "digest": sha(config), "size": len(config)},
        "layers": [{"mediaType": LAYER_TYPE, "digest": sha(layer), "size": len(layer)}],
    }).encode()
    index = json.dumps({"schemaVersion": 2, "manifests": [
        {"mediaType": MANIFEST_TYPE, "digest": sha(manifest), "size": len(manifest)}]}).encode()
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        for name, data in (("oci-layout", b'{"imageLayoutVersion":"1.0.0"}'), ("index.json", index),
                           ("blobs/sha256/" + sha(manifest)[7:], manifest),
                           ("blobs/sha256/" + sha(config)[7:], config),
                           ("blobs/sha256/" + sha(layer)[7:], layer)):
            item = tarfile.TarInfo(name)
            item.size = len(data)
            archive.addfile(item, io.BytesIO(data))
    return buffer.getvalue(), sha(manifest), sha(config)


class ClientTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name).resolve()
        (self.root / "images").mkdir()
        self.api, self.api_manifest, self.api_config = build_archive("a")
        self.web, self.web_manifest, self.web_config = build_archive("w")
        (self.root / "images" / "api.tar").write_bytes(self.api)
        (self.root / "images" / "web.tar").write_bytes(self.web)
        (self.root / "known_hosts").write_text("2a01:4f8:c0c:e2e::1 ssh-ed25519 AAAA\n")
        (self.root / "key").write_text("not a real key\n")
        (self.root / "key").chmod(0o600)
        self.manifest = {"version": 1, "target": "amsel", "host": "2a01:4f8:c0c:e2e::1", "port": 22,
                         "user": "utf-deploy", "key_file": str(self.root / "key"),
                         "known_hosts_file": str(self.root / "known_hosts")}

    def test_identity_is_read_from_the_archive(self):
        manifest, config = CLIENT.archive_identity(self.root / "images" / "api.tar")
        self.assertEqual((manifest, config), (self.api_manifest, self.api_config))

    def test_ssh_boundary_pins_host_key_proxy_and_runs_only_podman_load(self):
        argv = CLIENT.ssh_arguments(self.manifest, "amsel", 600)
        self.assertIn("-oStrictHostKeyChecking=yes", argv)
        self.assertIn("-oBatchMode=yes", argv)
        self.assertIn("-oClearAllForwardings=yes", argv)
        self.assertTrue(any(item.startswith("-oProxyCommand=") for item in argv))
        self.assertEqual(argv[-1], "podman load")
        for bad in ({**self.manifest, "target": "other"}, {**self.manifest, "host": "127.0.0.1"},
                    {**self.manifest, "extra": 1}, {**self.manifest, "user": "Root;x"},
                    {**self.manifest, "key_file": "relative-key"}):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                CLIENT.ssh_arguments(bad, "amsel", 600)
        (self.root / "key").chmod(0o644)
        with self.assertRaises(ValueError):
            CLIENT.ssh_arguments(self.manifest, "amsel", 600)

    def run_main(self, status="0", api="images/api.tar", limit=None):
        manifest_path = self.root / "production.json"
        manifest_path.write_text(json.dumps(self.manifest))
        calls = self.root / "calls"
        calls.mkdir(exist_ok=True)
        stub = self.root / "stub_ssh.py"
        stub.write_text(STUB_SSH)
        argv = ["stream_release.py", "--manifest", str(manifest_path), "--target", "amsel",
                "--api", api, "--web", "images/web.tar", "--timeout", "120"]
        if limit:
            argv += ["--max-archive-bytes", str(limit)]
        cwd = os.getcwd()
        os.chdir(self.root)
        self.addCleanup(os.chdir, cwd)
        with patch.dict(os.environ, {"STUB_DIR": str(calls), "STUB_EXIT": status}), \
                patch.object(CLIENT, "ssh_arguments", lambda *_: [sys.executable, str(stub)]), \
                patch.object(sys, "argv", argv):
            return CLIENT.main(), sorted(calls.iterdir())

    def test_both_archives_are_streamed_byte_exact_and_identities_recorded(self):
        status, calls = self.run_main()
        self.assertEqual(status, 0)
        self.assertEqual([path.read_bytes() for path in calls], [self.api, self.web])
        release = json.loads((self.root / "release.json").read_text())
        self.assertEqual(release["api"], {"path": "images/api.tar", "manifest": self.api_manifest,
                                          "image_id": self.api_config[7:]})
        self.assertEqual(release["web"]["image_id"], self.web_config[7:])

    def test_a_failed_load_stops_before_the_next_archive(self):
        status, calls = self.run_main(status="1")
        self.assertEqual(status, 1)
        self.assertEqual(len(calls), 1)

    def test_oversized_or_invalid_inputs_are_refused_before_any_transfer(self):
        (self.root / "images" / "bad.tar").write_bytes(b"not a tar")
        with self.assertRaises((tarfile.TarError, ValueError, OSError)):
            self.run_main(api="images/bad.tar")
        self.assertEqual(list((self.root / "calls").iterdir()), [])
        with self.assertRaises(ValueError):
            self.run_main(limit=len(self.api) + 10)
        self.assertEqual(list((self.root / "calls").iterdir()), [])


@unittest.skipUnless(subprocess.run(["sh", "-c", "command -v ssh-keygen"], capture_output=True).returncode == 0,
                     "ssh-keygen is required")
class DeployKeyTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.private, self.public = self.make_key("good")
        self.other_private, _ = self.make_key("other")

    def make_key(self, name):
        path = self.root / name
        subprocess.run(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-C", "test", "-f", str(path)], check=True)
        return path.read_text(), Path(str(path) + ".pub")

    def run_script(self, value):
        env = {**os.environ, "UTF_PRIVATE_DIR": str(self.root / "private"), "UTF_DEPLOY_PUBLIC_KEY": str(self.public)}
        env.pop("UTF_DEPLOY_SSH_KEY", None)
        if value is not None:
            env["UTF_DEPLOY_SSH_KEY"] = value
        return subprocess.run(["sh", str(HERE / "prepare-deploy-key.sh")], capture_output=True, text=True, env=env)

    def test_accepts_pem_flattened_pem_and_base64(self):
        for label, value in (("pem", self.private), ("flattened", self.private.replace("\n", " ").strip()),
                             ("base64", base64.b64encode(self.private.encode()).decode())):
            with self.subTest(label):
                result = self.run_script(value)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn("UTF deploy key: ok SHA256:", result.stdout)
                self.assertEqual(stat.S_IMODE((self.root / "private" / "deploy_key").stat().st_mode), 0o600)

    def test_refuses_missing_wrong_and_garbage_values_without_echoing_them(self):
        body = self.private.splitlines()[1]
        for label, value, reason in (("missing", None, "missing or empty"), ("empty", "", "missing or empty"),
                                     ("garbage", "%%%%", "neither PEM nor base64"),
                                     ("other key", self.other_private, "different key")):
            with self.subTest(label):
                result = self.run_script(value)
                self.assertEqual(result.returncode, 1)
                self.assertIn(reason, result.stderr)
                self.assertNotIn(body, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
