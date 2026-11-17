import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
import uuid

import oci_download as download


def digest(data):
    return "sha256:" + hashlib.sha256(data).hexdigest()


class ExportTest(unittest.TestCase):
    def setUp(self):
        self.origin = "https://registry.example.test"
        self.attempt = str(uuid.uuid4())
        self.responses = {}
        self.credential = {"origin": self.origin, "attempt_id": self.attempt,
                           "release_job": "checked-artifacts", "images": []}
        producer = str(uuid.uuid4())
        self.workspace = Path(self.enterContext(tempfile.TemporaryDirectory()))
        for role in download.ROLES:
            config = json.dumps({"os": "linux", "architecture": "amd64", "role": role}).encode()
            layer = role.encode()
            manifest = json.dumps({
                "schemaVersion": 2, "mediaType": download.MANIFEST,
                "config": {"mediaType": download.CONFIG, "size": len(config), "digest": digest(config)},
                "layers": [{"mediaType": download.LAYER, "size": len(layer), "digest": digest(layer)}],
            }).encode()
            repository = download.REPOSITORIES[role]
            self.credential["images"].append({"repository": repository, "digest": digest(manifest), "token": role,
                "producer_attempt_id": producer, "publication_id": str(uuid.uuid4())})
            self.responses[f"{self.origin}/v2/{repository}/manifests/{digest(manifest)}"] = (role, manifest)
            for blob in (config, layer):
                self.responses[f"{self.origin}/v2/{repository}/blobs/{digest(blob)}"] = (role, blob)

    def open(self, request, timeout):
        self.assertEqual(timeout, 30)
        role, content = self.responses[request.full_url]
        self.assertEqual(request.get_header("Authorization"), "Bearer " + role)
        return io.BytesIO(content)

    def export(self, budget=50000):
        return download.export_release(self.credential, self.origin, self.attempt,
                                       self.workspace, budget, self)

    def test_exports_exact_digest_named_manifest_and_blobs(self):
        manifest = json.loads(self.export().read_bytes())
        for image in self.credential["images"]:
            role = next(role for role, path in download.REPOSITORIES.items() if path == image["repository"])
            self.assertEqual(manifest[role]["digest"], image["digest"])
            with tarfile.open(self.workspace / manifest[role]["path"]) as archive:
                for name in archive.getnames():
                    if name.startswith("blobs/sha256/"):
                        self.assertEqual(digest(archive.extractfile(name).read()), "sha256:" + name.rsplit("/", 1)[1])

    def test_refuses_tampered_blob_and_removes_incomplete_archives(self):
        path = next(path for path, (_, content) in self.responses.items()
                    if "/api/blobs/" in path and content == b"api")
        role, data = self.responses[path]
        self.responses[path] = (role, b"x" * len(data))
        with self.assertRaisesRegex(ValueError, "blob digest mismatch"):
            self.export()
        self.assertEqual(list(self.workspace.iterdir()), [])

    def test_bounds_bytes_and_rejects_wrong_scope(self):
        with self.assertRaisesRegex(ValueError, "byte limit"):
            self.export(20000)
        self.assertEqual(list(self.workspace.iterdir()), [])
        self.credential["images"][0]["repository"] = "applications/other/api"
        with self.assertRaisesRegex(ValueError, "Invalid OCI reader scope"):
            self.export()


    def test_refuses_worker_scope_or_a_second_producer(self):
        images = self.credential["images"]
        images.append(dict(images[0], repository="applications/utf/cloud-worker"))
        with self.assertRaisesRegex(ValueError, "API and web reader scopes"):
            self.export()
        images.pop()
        images[1]["producer_attempt_id"] = str(uuid.uuid4())
        with self.assertRaisesRegex(ValueError, "one exact producer"):
            self.export()
        self.assertEqual(list(self.workspace.iterdir()), [])


if __name__ == "__main__":
    unittest.main()
