"""Raw envelope rejection, exclusive transport writes and real protocol acceptance."""
import concurrent.futures
import importlib.util
import io
import json
import os
import subprocess
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

import test_archive

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("utf_canonical", ROOT / "canonicalize-archive.py")
transport = importlib.util.module_from_spec(spec)
spec.loader.exec_module(transport)
validator = transport.validator


class CanonicalTransport(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.binary = Path(os.environ["UTF_OCI_VALIDATOR"])
        inputs = json.loads((ROOT / "inputs.json").read_text())
        if validator.digest_file(cls.binary) != inputs["protocol_validator_sha256"]:
            raise AssertionError("real protocol validator SHA mismatch")

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="utf-transport-contract-")
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.raw = self.directory / "raw.tar"
        self.output = self.directory / "canonical.tar"
        self.members, self.image, _ = test_archive.fixture(compressed=True)
        self.write()

    def write(self, members=None, directories=("blobs", "blobs/sha256"), special=None, format=tarfile.USTAR_FORMAT):
        with tarfile.open(self.raw, "w", format=format) as archive:
            for name in directories:
                info = tarfile.TarInfo(name)
                info.type = tarfile.DIRTYPE
                archive.addfile(info)
            for name, value in self.members if members is None else members:
                info = tarfile.TarInfo(name)
                info.size = len(value)
                archive.addfile(info, io.BytesIO(value))
            if special is not None:
                archive.addfile(special)

    def convert(self):
        return transport.canonicalize(self.raw, self.output, self.image, "localhost/utf-dependencies-api")

    def reject(self):
        with self.assertRaises((ValueError, tarfile.TarError, EOFError)):
            self.convert()
        self.assertFalse(self.output.exists())

    def test_preserves_all_blobs_and_binds_reference_to_exported_root(self):
        raw_hash = validator.digest_file(self.raw)
        self.assertNotEqual(subprocess.run([str(self.binary), str(self.raw)], capture_output=True, timeout=10).returncode, 0)
        result = self.convert()
        self.assertEqual(subprocess.run([str(self.binary), str(self.output)], capture_output=True, timeout=10).returncode, 0)
        self.assertEqual(validator.digest_file(self.raw), raw_hash)
        original = validator.inspect_archive(self.raw, self.image, allow_directories=True)
        for key in ("manifest_digest", "config_digest", "diff_ids", "blobs"):
            self.assertEqual(result[key], original[key])
        self.assertNotEqual(result["manifest_digest"], self.image)
        with tarfile.open(self.output) as archive:
            self.assertTrue(all(member.isreg() for member in archive))
            for name, value in self.members:
                if name != "index.json":
                    self.assertEqual(archive.extractfile(name).read(), value)
            index = json.load(archive.extractfile("index.json"))
            self.assertEqual(index["manifests"][0]["annotations"]["org.opencontainers.image.ref.name"],
                             "localhost/utf-dependencies-api@" + result["manifest_digest"])
        self.assertEqual(result["removed_outer_directories"], ["blobs", "blobs/sha256"])

    def test_wrong_image_diffid_and_unreferenced_graph_rejected_before_write(self):
        self.image = "a" * 64
        self.reject()
        self.members, self.image, _ = test_archive.fixture(wrong_diff=True)
        self.write()
        self.reject()
        self.members, self.image, _ = test_archive.fixture()
        self.write([*self.members, ("blobs/sha256/" + "b" * 64, b"unused")])
        self.reject()

    def test_raw_schema_descriptor_size_hash_and_missing_blob_rejected(self):
        original = list(self.members)
        for field, value in (("schemaVersion", True), ("schemaVersion", 99), ("mediaType", "text/plain")):
            index = json.loads(original[1][1])
            index[field] = value
            self.write([(name, test_archive.encode(index) if name == "index.json" else data) for name, data in original])
            self.reject()
        for field, value in (("size", -1), ("size", 99999), ("mediaType", "text/plain"),
                             ("urls", ["https://example.invalid/foreign"]), ("annotations", {"bad": 1})):
            index = json.loads(original[1][1])
            index["manifests"][0][field] = value
            self.write([(name, test_archive.encode(index) if name == "index.json" else data) for name, data in original])
            self.reject()
        self.write(original[:-1])
        self.reject()
        self.write([*original[:-1], (original[-1][0], b"altered")])
        self.reject()

    def test_unexpected_directory_duplicate_and_alias_rejected(self):
        for directories in (("unexpected",), ("blobs", "blobs/"), ("blobs////",), ("../blobs",)):
            with self.subTest(directories=directories):
                self.write(directories=directories)
                self.reject()
        self.write([*self.members, self.members[0]])
        self.reject()

    def test_links_pax_gnu_and_special_members_rejected(self):
        for kind in (tarfile.SYMTYPE, tarfile.LNKTYPE, tarfile.FIFOTYPE, tarfile.CHRTYPE,
                     tarfile.XHDTYPE, tarfile.XGLTYPE, tarfile.GNUTYPE_LONGNAME):
            info = tarfile.TarInfo("bad")
            info.type = kind
            self.write(special=info)
            self.reject()
        self.write(format=tarfile.GNU_FORMAT)
        self.reject()
        info = tarfile.TarInfo("bad")
        info.pax_headers = {"comment": "extended"}
        self.write(special=info, format=tarfile.PAX_FORMAT)
        self.reject()

    def test_unsafe_paths_and_corrupt_envelope_rejected(self):
        for name in ("../index.json", "/index.json", "blobs/../index.json", "blobs/sha256/" + "A" * 64):
            self.write([*self.members, (name, b"x")])
            self.reject()
        self.write()
        original = self.raw.read_bytes()
        for changed in (original[:-513], original + b"x" * 512, original + original):
            self.raw.write_bytes(changed)
            self.reject()
        corrupt = bytearray(original)
        corrupt[148] ^= 1
        self.raw.write_bytes(corrupt)
        self.reject()
        self.write(directories=())
        corrupt = bytearray(self.raw.read_bytes())
        corrupt[512 + len(self.members[0][1])] = 1
        self.raw.write_bytes(corrupt)
        self.reject()

    def test_count_file_and_archive_size_limits_rejected(self):
        self.write([*self.members, *[("blobs/sha256/" + format(i, "064x"), b"") for i in range(1027)]])
        self.reject()
        self.write()
        header = tarfile.TarInfo("blobs/sha256/" + "a" * 64)
        encoded = bytearray(header.tobuf(format=tarfile.USTAR_FORMAT))
        encoded[124:136] = b"100000000001"
        encoded[148:156] = b"        "
        encoded[148:156] = (format(sum(encoded), "06o") + "\x00 ").encode("ascii")
        with self.raw.open("r+b") as stream:
            stream.write(encoded)
        self.reject()
        self.write()
        self.assertEqual(validator.MAX_ARCHIVE, 16 * 1024**3)
        # A smaller injected cap exercises rejection without allocating 16 GiB.
        with patch.object(validator, "MAX_ARCHIVE", 1024):
            self.reject()

    def test_existing_output_same_path_and_checkout_rejected_without_overwrite(self):
        self.output.write_bytes(b"sentinel")
        with self.assertRaises(FileExistsError):
            self.convert()
        self.assertEqual(self.output.read_bytes(), b"sentinel")
        with self.assertRaises(FileExistsError):
            transport.canonicalize(self.raw, self.raw, self.image, "localhost/utf-dependencies-api")
        with self.assertRaises(ValueError):
            transport.canonicalize(self.raw, ROOT / "must-not-create.tar", self.image, "localhost/utf-dependencies-api")
        self.assertFalse((ROOT / "must-not-create.tar").exists())

    def test_competing_processes_have_exactly_one_exclusive_winner(self):
        command = [sys.executable, "-B", str(ROOT / "canonicalize-archive.py"), str(self.raw),
                   str(self.output), self.image, "localhost/utf-dependencies-api"]
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            results = list(executor.map(lambda _: subprocess.run(command, capture_output=True, timeout=20).returncode, range(2)))
        self.assertEqual(sorted(results), [0, 1])
        validator.inspect_archive(self.output, self.image)

    def test_invalid_producer_repository_rejected(self):
        for repository in ("localhost/utf-dependencies-api:tag", "localhost/utf-dependencies-api@sha256:" + "a" * 64,
                           "example.org/foreign", "../escape"):
            with self.assertRaises(ValueError):
                transport.canonicalize(self.raw, self.output, self.image, repository)
        self.assertFalse(self.output.exists())

    @unittest.skipUnless(os.name == "nt", "actual Windows junction contract")
    def test_actual_windows_junction_output_ancestry_rejected(self):
        target = self.directory / "real"
        target.mkdir()
        junction = self.directory / "junction"
        result = subprocess.run(["cmd.exe", "/c", "mklink", "/J", str(junction), str(target)], capture_output=True, timeout=10)
        self.assertEqual(result.returncode, 0, result.stderr)
        try:
            with self.assertRaises(ValueError):
                transport.canonicalize(self.raw, junction / "escape.tar", self.image, "localhost/utf-dependencies-api")
            self.assertFalse((target / "escape.tar").exists())
        finally:
            junction.rmdir()


if __name__ == "__main__":
    unittest.main()
