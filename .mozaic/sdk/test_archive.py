"""Synthetic archive contracts; requires the real pinned Mozaic protocol validator."""
import gzip
import hashlib
import importlib.util
import io
import json
import os
import subprocess
import tarfile
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("utf_archive", ROOT / "validate-archive.py")
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


def encode(value):
    return json.dumps(value, separators=(",", ":")).encode()


def digest(value):
    return "sha256:" + hashlib.sha256(value).hexdigest()


def fixture(compressed=False, wrong_diff=False, platform="amd64", payload=None):
    if payload is None:
        stream = io.BytesIO()
        with tarfile.open(fileobj=stream, mode="w", format=tarfile.USTAR_FORMAT) as archive:
            info = tarfile.TarInfo("dependency-probe")
            info.size = 5
            archive.addfile(info, io.BytesIO(b"probe"))
        payload = stream.getvalue()
    layer = gzip.compress(payload, mtime=0) if compressed else payload
    config = encode({"os": "linux", "architecture": platform,
                     "rootfs": {"type": "layers", "diff_ids": ["sha256:" + "f" * 64 if wrong_diff else digest(payload)]}})
    manifest = encode({"schemaVersion": 2, "mediaType": validator.MANIFEST,
                       "config": {"mediaType": validator.CONFIG, "digest": digest(config), "size": len(config)},
                       "layers": [{"mediaType": validator.GZIP if compressed else validator.RAW,
                                   "digest": digest(layer), "size": len(layer)}]})
    index = {"schemaVersion": 2, "manifests": [{"mediaType": validator.MANIFEST,
             "digest": digest(manifest), "size": len(manifest)}]}
    members = [("oci-layout", encode({"imageLayoutVersion": "1.0.0"})), ("index.json", encode(index))]
    members += [("blobs/sha256/" + digest(value)[7:], value) for value in (config, manifest, layer)]
    return members, digest(config), index


class ArchiveContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.binary = Path(os.environ["UTF_OCI_VALIDATOR"])
        inputs = json.loads((ROOT / "inputs.json").read_text())
        if validator.digest_file(cls.binary) != inputs["protocol_validator_sha256"]:
            raise AssertionError("real protocol validator SHA mismatch")

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="utf-archive-contract-")
        self.addCleanup(self.temporary.cleanup)
        self.path = Path(self.temporary.name) / "fixture.oci.tar"

    def write(self, members, special=None):
        with tarfile.open(self.path, "w", format=tarfile.USTAR_FORMAT) as archive:
            for name, value in members:
                info = tarfile.TarInfo(name)
                info.size = len(value)
                archive.addfile(info, io.BytesIO(value))
            if special:
                archive.addfile(special)

    def protocol(self):
        return subprocess.run([str(self.binary), str(self.path)], capture_output=True, timeout=10).returncode

    def test_real_protocol_and_image_binding_accept_raw_and_gzip(self):
        for compressed in (False, True):
            with self.subTest(compressed=compressed):
                members, image_id, _ = fixture(compressed=compressed)
                self.write(members)
                self.assertEqual(self.protocol(), 0)
                result = validator.inspect_archive(self.path, image_id)
                self.assertEqual(result["config_digest"], image_id)
                self.assertEqual(result["layer_count"], 1)

    def test_real_protocol_rejects_unsafe_duplicate_and_link_members(self):
        members, _, _ = fixture()
        for bad in ([*members, members[1]], [*members, ("../escape", b"x")]):
            self.write(bad)
            self.assertNotEqual(self.protocol(), 0)
        link = tarfile.TarInfo("extra-link")
        link.type, link.linkname = tarfile.SYMTYPE, "index.json"
        self.write(members, link)
        self.assertNotEqual(self.protocol(), 0)

    def test_real_protocol_rejects_schema_media_size_and_missing_blob(self):
        members, _, index = fixture()
        for field, value in (("schemaVersion", 99), ("schemaVersion", True)):
            changed = dict(index)
            changed[field] = value
            self.write([(name, encode(changed) if name == "index.json" else data) for name, data in members])
            self.assertNotEqual(self.protocol(), 0)
        for field, value in (("size", -1), ("size", index["manifests"][0]["size"] + 1), ("mediaType", "text/plain")):
            changed = json.loads(encode(index))
            changed["manifests"][0][field] = value
            self.write([(name, encode(changed) if name == "index.json" else data) for name, data in members])
            self.assertNotEqual(self.protocol(), 0)
        self.write(members[:-1])
        self.assertNotEqual(self.protocol(), 0)

    def test_real_protocol_rejects_altered_blob_and_unreferenced_blob(self):
        members, _, _ = fixture()
        altered = list(members)
        altered[-1] = (altered[-1][0], b"changed layer")
        self.write(altered)
        self.assertNotEqual(self.protocol(), 0)
        self.write([*members, ("blobs/sha256/" + hashlib.sha256(b"unused").hexdigest(), b"unused")])
        self.assertNotEqual(self.protocol(), 0)

    def test_config_image_identity_and_platform_are_enforced(self):
        members, image_id, index = fixture()
        self.write(members)
        with self.assertRaises(ValueError):
            validator.inspect_archive(self.path, "sha256:" + "a" * 64)
        index["manifests"][0]["platform"] = {"os": "linux", "architecture": "arm64"}
        self.write([(name, encode(index) if name == "index.json" else data) for name, data in members])
        with self.assertRaisesRegex(ValueError, "platform"):
            validator.inspect_archive(self.path, image_id)
        members, image_id, _ = fixture(platform="arm64")
        self.write(members)
        with self.assertRaises(ValueError):
            validator.inspect_archive(self.path, image_id)

    def test_diffid_mismatch_is_caught_after_protocol_success(self):
        for compressed in (False, True):
            members, image_id, _ = fixture(compressed=compressed, wrong_diff=True)
            self.write(members)
            self.assertEqual(self.protocol(), 0)
            with self.assertRaisesRegex(ValueError, "diffID"):
                validator.inspect_archive(self.path, image_id)

    def test_compressed_layer_expansion_is_bounded(self):
        members, image_id, _ = fixture(compressed=True, payload=b"x" * (3 * 1024**2))
        self.write(members)
        with self.assertRaisesRegex(ValueError, "expansion"):
            validator.inspect_archive(self.path, image_id)


if __name__ == "__main__":
    unittest.main()
