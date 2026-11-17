"""Export the exact published images using a deployment job's scoped OCI readers."""

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import sys
import tarfile
import tempfile
import urllib.request
import uuid


ROLES = ("api", "web")
REPOSITORIES = {"api": "applications/utf/api", "web": "applications/utf/web"}
MANIFEST = "application/vnd.oci.image.manifest.v1+json"
CONFIG = "application/vnd.oci.image.config.v1+json"
LAYER = "application/vnd.oci.image.layer.v1.tar"
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
METADATA_LIMIT = 1024 * 1024


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *_args, **_kwargs):
        raise ValueError("OCI redirects are forbidden")


def canonical_json(value):
    return json.dumps(value, separators=(",", ":"), sort_keys=True).encode("ascii")


def read_credential(path):
    flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK
    with os.fdopen(os.open(path, flags), "rb") as source:
        info = os.fstat(source.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_mode & 0o077:
            raise ValueError("OCI reader file must be private")
        data = source.read(65537)
    if len(data) > 65536:
        raise ValueError("OCI reader file is too large")
    return json.loads(data)


def descriptor(value, media_types):
    if (not isinstance(value, dict) or value.get("mediaType") not in media_types
            or not isinstance(value.get("digest"), str) or not DIGEST.fullmatch(value["digest"])
            or type(value.get("size")) is not int or value["size"] <= 0
            or "urls" in value or "data" in value):
        raise ValueError("Invalid OCI descriptor")
    return value["digest"], value["size"]


def request(opener, origin, repository, kind, digest, token, accept):
    return opener.open(urllib.request.Request(
        f"{origin}/v2/{repository}/{kind}/{digest}",
        headers={"Authorization": "Bearer " + token, "Accept": accept, "Accept-Encoding": "identity"},
    ), timeout=30)


def add(archive, name, size, stream):
    item = tarfile.TarInfo(name)
    item.size = size
    item.mode = 0o600
    archive.addfile(item, stream)


class HashedReader:
    def __init__(self, source):
        self.source = source
        self.hash = hashlib.sha256()

    def read(self, size):
        data = self.source.read(size)
        self.hash.update(data)
        return data


def export_image(opener, origin, image, archive_path, budget):
    repo, digest, token = (image[name] for name in ("repository", "digest", "token"))
    with request(opener, origin, repo, "manifests", digest, token, MANIFEST) as response:
        manifest = response.read(METADATA_LIMIT + 1)
    if (not manifest or len(manifest) > METADATA_LIMIT
            or "sha256:" + hashlib.sha256(manifest).hexdigest() != digest):
        raise ValueError("OCI publication manifest digest mismatch")
    value = json.loads(manifest)
    if (value.get("schemaVersion") != 2 or value.get("mediaType", MANIFEST) != MANIFEST
            or not isinstance(value.get("layers"), list) or len(value["layers"]) > 128):
        raise ValueError("Unsupported OCI manifest")
    config_digest, config_size = descriptor(value.get("config"), {CONFIG})
    if config_size > METADATA_LIMIT:
        raise ValueError("OCI config exceeds its byte limit")
    blobs = {config_digest: config_size}
    for layer in value["layers"]:
        key, size = descriptor(layer, {LAYER, LAYER + "+gzip"})
        if blobs.setdefault(key, size) != size:
            raise ValueError("Conflicting OCI blob descriptors")
    if digest in blobs or len(manifest) + sum(blobs.values()) > budget:
        raise ValueError("OCI release exceeds its byte limit")

    index = canonical_json({"schemaVersion": 2, "manifests": [
        {"mediaType": MANIFEST, "digest": digest, "size": len(manifest)}
    ]})
    layout = b'{"imageLayoutVersion":"1.0.0"}'
    entry_sizes = [len(index), len(layout), len(manifest), *blobs.values()]
    tar_bytes = sum(512 + (size + 511) // 512 * 512 for size in entry_sizes) + 1024
    if (tar_bytes + 10239) // 10240 * 10240 > budget:
        raise ValueError("OCI archive exceeds its byte limit")
    with archive_path.open("xb") as destination, tarfile.open(fileobj=destination, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        for name, data in [("oci-layout", layout), ("index.json", index), ("blobs/sha256/" + digest[7:], manifest)]:
            add(archive, name, len(data), io.BytesIO(data))
        for key, size in blobs.items():
            with request(opener, origin, repo, "blobs", key, token, "application/octet-stream") as response:
                if key == config_digest:
                    config = response.read(config_size + 1)
                    if (len(config) != config_size
                            or "sha256:" + hashlib.sha256(config).hexdigest() != config_digest):
                        raise ValueError("OCI publication config digest mismatch")
                    platform = json.loads(config)
                    if platform.get("os") != "linux" or platform.get("architecture") != "amd64":
                        raise ValueError("OCI publication platform mismatch")
                    add(archive, "blobs/sha256/" + key[7:], size, io.BytesIO(config))
                    continue
                reader = HashedReader(response)
                add(archive, "blobs/sha256/" + key[7:], size, reader)
                if response.read(1) or "sha256:" + reader.hash.hexdigest() != key:
                    raise ValueError("OCI publication blob digest mismatch")
    return archive_path.stat().st_size


def export_release(credential, origin, attempt, workspace, budget, opener=None):
    if (credential.get("origin") != origin or credential.get("attempt_id") != attempt
            or not re.fullmatch(r"https://[a-z0-9.-]+", origin)
            or str(uuid.UUID(attempt)) != attempt or budget <= 0
            or credential.get("release_job") != "checked-artifacts"):
        raise ValueError("OCI reader scope does not match this job")
    images = credential["images"]
    if not isinstance(images, list) or len(images) != len(ROLES):
        raise ValueError("UTF delivery requires exactly the API and web reader scopes")
    by_repository, producers, publications = {}, set(), set()
    for image in images:
        repository, digest, token = (image[name] for name in ("repository", "digest", "token"))
        if (repository not in REPOSITORIES.values() or repository in by_repository
                or not isinstance(digest, str) or not DIGEST.fullmatch(digest)
                or not isinstance(token, str) or not re.fullmatch(r"[A-Za-z0-9._~-]{1,16384}", token)):
            raise ValueError("Invalid OCI reader scope")
        producer, publication = (image[name] for name in ("producer_attempt_id", "publication_id"))
        if (str(uuid.UUID(producer)) != producer or producer == attempt
                or str(uuid.UUID(publication)) != publication):
            raise ValueError("Invalid OCI publication identity")
        producers.add(producer)
        publications.add(publication)
        by_repository[repository] = image
    if len(producers) != 1 or len(publications) != len(ROLES):
        raise ValueError("UTF images require one exact producer and distinct publications")
    opener = opener or urllib.request.build_opener(NoRedirect())
    output = Path(tempfile.mkdtemp(prefix=".release-oci-", dir=workspace))
    result = {}
    try:
        for role in ROLES:
            image = by_repository[REPOSITORIES[role]]
            archive = output / f"{role}.tar"
            budget -= export_image(opener, origin, image, archive, budget)
            result[role] = {"path": str(archive.relative_to(workspace)), "digest": image["digest"]}
        manifest = output / "release.json"
        manifest.write_bytes(canonical_json(result))
        return manifest
    except (OSError, ValueError, TypeError, KeyError, tarfile.TarError):
        for path in output.iterdir():
            path.unlink()
        output.rmdir()
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--credential", required=True, type=Path)
    parser.add_argument("--origin", required=True)
    parser.add_argument("--attempt", required=True)
    parser.add_argument("--max-archive-bytes", required=True, type=int)
    args = parser.parse_args()
    print(export_release(read_credential(args.credential), args.origin,
                         args.attempt, Path.cwd(), args.max_archive_bytes))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, TypeError, KeyError, tarfile.TarError):
        print("Exact OCI release export failed.", file=sys.stderr)
        sys.exit(1)
