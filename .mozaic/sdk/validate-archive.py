"""Additional image/config/diffID checks after mandatory Mozaic OCI validation.

The graph walk extends the reviewed Bantapop abfbfb8 preparation parser. This
module never extracts filesystem members. The independent Mozaic protocol binary
must first validate the original archive's tar envelope and complete OCI graph.
"""
import argparse
import gzip
import hashlib
import json
import re
import stat
import tarfile
from pathlib import Path

GIB = 1024**3
MANIFEST = "application/vnd.oci.image.manifest.v1+json"
INDEX = "application/vnd.oci.image.index.v1+json"
CONFIG = "application/vnd.oci.image.config.v1+json"
RAW = "application/vnd.oci.image.layer.v1.tar"
GZIP = RAW + "+gzip"
DIGEST = re.compile(r"sha256:[a-f0-9]{64}")


def digest_file(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_json(stream):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON property")
            result[key] = value
        return result
    return json.load(stream, object_pairs_hook=unique,
                     parse_constant=lambda _: (_ for _ in ()).throw(ValueError("nonfinite JSON")))


def inspect_archive(path, expected_image_id):
    """Bind a bounded single-image OCI closure and actual layer bytes to an image ID."""
    if not stat.S_ISREG(path.lstat().st_mode) or path.stat().st_size > 16 * GIB:
        raise ValueError("archive exceeds 16 GiB")
    expected = expected_image_id.removeprefix("sha256:")
    if not re.fullmatch(r"[a-f0-9]{64}", expected):
        raise ValueError("invalid inspected image ID")
    seen, blobs, members = set(), {}, {}
    index = None
    with tarfile.open(path, "r:") as archive:
        for member in archive:
            name = member.name
            if name in seen or len(seen) >= 1026 or member.pax_headers:
                raise ValueError("duplicate, excessive or extended archive member")
            seen.add(name)
            if member.isdir() and name in {"blobs", "blobs/sha256"} and member.size == 0:
                continue
            if not member.isreg() or not 0 <= member.size <= 8 * GIB:
                raise ValueError("nonregular or oversized archive member")
            stream = archive.extractfile(member)
            if name in {"index.json", "oci-layout"}:
                if member.size > 1024**2:
                    raise ValueError("oversized metadata")
                value = read_json(stream)
                if name == "index.json":
                    index = value
                elif value != {"imageLayoutVersion": "1.0.0"}:
                    raise ValueError("invalid layout version")
            elif re.fullmatch(r"blobs/sha256/[a-f0-9]{64}", name):
                actual = hashlib.file_digest(stream, "sha256").hexdigest()
                if actual != name.rsplit("/", 1)[1]:
                    raise ValueError("altered blob")
                digest = "sha256:" + actual
                blobs[digest], members[digest] = member.size, member
            else:
                raise ValueError("unsafe or unexpected member")
        if (not isinstance(index, dict) or type(index.get("schemaVersion")) is not int
                or index["schemaVersion"] != 2 or index.get("mediaType", INDEX) != INDEX
                or not isinstance(index.get("manifests"), list)
                or len(index["manifests"]) != 1 or "oci-layout" not in seen):
            raise ValueError("invalid single-image index")

        def descriptor(value, types, maximum):
            if (not isinstance(value, dict) or value.get("mediaType") not in types
                    or not isinstance(value.get("digest"), str)
                    or not DIGEST.fullmatch(value["digest"])
                    or type(value.get("size")) is not int
                    or not 0 <= value["size"] <= maximum
                    or blobs.get(value["digest"]) != value["size"]
                    or "urls" in value or "data" in value):
                raise ValueError("invalid, absent or external descriptor")
            return value["digest"]

        def metadata(digest):
            if blobs[digest] > 1024**2:
                raise ValueError("oversized image metadata")
            value = read_json(archive.extractfile(members[digest]))
            if not isinstance(value, dict):
                raise ValueError("image metadata must be an object")
            return value

        root = descriptor(index["manifests"][0], {MANIFEST}, 1024**2)
        platform = index["manifests"][0].get("platform")
        if platform is not None and (not isinstance(platform, dict)
                                     or platform.get("os") != "linux"
                                     or platform.get("architecture") != "amd64"):
            raise ValueError("index platform differs from inspected image platform")
        manifest = metadata(root)
        if (type(manifest.get("schemaVersion")) is not int or manifest["schemaVersion"] != 2
                or manifest.get("mediaType", MANIFEST) != MANIFEST
                or not isinstance(manifest.get("layers"), list)
                or not 1 <= len(manifest["layers"]) <= 512):
            raise ValueError("invalid image manifest")
        config_digest = descriptor(manifest.get("config"), {CONFIG}, 1024**2)
        if config_digest != "sha256:" + expected:
            raise ValueError("archive config differs from inspected image ID")
        references = {root, config_digest}
        layers = []
        for layer in manifest["layers"]:
            digest = descriptor(layer, {RAW, GZIP}, 8 * GIB)
            references.add(digest)
            layers.append((digest, layer["mediaType"]))
        if references != set(blobs):
            raise ValueError("unreferenced graph blob")
        config = metadata(config_digest)
        rootfs = config.get("rootfs")
        if (config.get("os") != "linux" or config.get("architecture") != "amd64"
                or not isinstance(rootfs, dict) or rootfs.get("type") != "layers"
                or not isinstance(rootfs.get("diff_ids"), list)
                or len(rootfs["diff_ids"]) != len(layers)):
            raise ValueError("unsupported image platform or rootfs")
        expanded_total = 0
        for (digest, media_type), diff_id in zip(layers, rootfs["diff_ids"]):
            if not isinstance(diff_id, str) or not DIGEST.fullmatch(diff_id):
                raise ValueError("invalid diffID")
            compressed = archive.extractfile(members[digest])
            stream = gzip.GzipFile(fileobj=compressed) if media_type == GZIP else compressed
            size, hasher = 0, hashlib.sha256()
            with stream:
                while block := stream.read(1024**2):
                    size += len(block)
                    expanded_total += len(block)
                    if (size > 8 * GIB or expanded_total > 32 * GIB
                            or size > blobs[digest] * 200 + 1024**2):
                        raise ValueError("layer expansion exceeds validation envelope")
                    hasher.update(block)
            if diff_id != "sha256:" + hasher.hexdigest():
                raise ValueError("layer bytes differ from config diffID")
    return {"manifest_digest": root, "config_digest": config_digest,
            "archive_sha256": digest_file(path), "archive_bytes": path.stat().st_size,
            "layer_count": len(layers), "expanded_layer_bytes": expanded_total}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("image_id")
    options = parser.parse_args()
    try:
        print(json.dumps(inspect_archive(options.archive, options.image_id), sort_keys=True))
    except (ValueError, OSError, tarfile.TarError, EOFError, KeyError, TypeError):
        raise SystemExit("OCI image/config/diffID validation failed.") from None
