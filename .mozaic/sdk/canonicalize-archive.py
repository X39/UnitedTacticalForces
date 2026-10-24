"""Canonicalize a verified raw OCI export without changing any graph blob.

This local transport step does not extract members or replace the independent
Mozaic protocol gate. Only known empty outer directories and outer tar metadata
are removed; the index reference names the producer repository at the exported
manifest digest, which can differ from Podman's stored manifest digest.
"""
import argparse
import importlib.util
import io
import json
import os
import re
import stat
import tarfile
from pathlib import Path

SOURCE = Path(__file__).absolute().parent
spec = importlib.util.spec_from_file_location("utf_transport_validator", SOURCE / "validate-archive.py")
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


def output_guard(path):
    """Require a new file beneath existing non-reparse ancestry outside checkout."""
    path = Path(os.path.abspath(path))
    repository = SOURCE.parent.parent
    same_drive = path.drive.lower() == repository.drive.lower()
    if same_drive and os.path.commonpath([os.path.normcase(path), os.path.normcase(repository)]) == os.path.normcase(repository):
        raise ValueError("transport output must be outside the checkout")
    for parent in (path, *path.parents):
        try:
            metadata = parent.lstat()
        except FileNotFoundError:
            if parent != path:
                raise ValueError("output parent must already exist") from None
            continue
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, "st_file_attributes", 0) & 0x400:
            raise ValueError("reparse transport output ancestry")
        if parent == path:
            raise FileExistsError("transport output already exists")
        if not stat.S_ISDIR(metadata.st_mode):
            raise ValueError("output ancestry must be directories")
    if path.parent.resolve() != path.parent:
        raise ValueError("output parent resolves elsewhere")
    return path


def canonicalize(raw, output, image_id, repository):
    """Validate first, exclusively write bounded USTAR, then verify preservation."""
    if not re.fullmatch(r"localhost/utf-dependencies-(api|worker|aska|sdk|nginx)", repository):
        raise ValueError("unexpected producer repository")
    output = output_guard(output)
    raw = Path(raw)
    before = raw.stat()
    original = validator.inspect_archive(raw, image_id, allow_directories=True)
    reference = repository + "@" + original["manifest_digest"]
    with tarfile.open(raw, "r:") as source:
        members = {member.name: member for member in source if member.isreg()}
        index = validator.read_json(source.extractfile(members["index.json"]))
        index["manifests"][0].setdefault("annotations", {})["org.opencontainers.image.ref.name"] = reference
        encoded = json.dumps(index, separators=(",", ":"), sort_keys=True).encode("utf-8")
        if len(encoded) > 1024**2:
            raise ValueError("canonical index exceeds metadata bound")
        lengths = [len(encoded) if name == "index.json" else member.size for name, member in members.items()]
        length = sum(512 + ((size + 511) // 512) * 512 for size in lengths) + 1024
        if ((length + 10239) // 10240) * 10240 > validator.MAX_ARCHIVE:
            raise ValueError("canonical archive exceeds transport bound")
        # CreateNew/O_EXCL refuses existing files even when another writer wins
        # after output_guard. No file is extracted and raw is never opened writable.
        with output.open("xb") as destination:
            with tarfile.open(fileobj=destination, mode="w|", format=tarfile.USTAR_FORMAT) as canonical:
                for name in sorted(members):
                    member = members[name]
                    info = tarfile.TarInfo(name)
                    info.mode = 0o644
                    info.size = len(encoded) if name == "index.json" else member.size
                    with (io.BytesIO(encoded) if name == "index.json" else source.extractfile(member)) as content:
                        canonical.addfile(info, content)
    after = raw.stat()
    if ((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
            != (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
            or validator.digest_file(raw) != original["archive_sha256"]):
        raise ValueError("raw export changed during canonicalization")
    checked = validator.inspect_archive(output, image_id)
    for key in ("manifest_digest", "config_digest", "diff_ids", "blobs"):
        if checked[key] != original[key]:
            raise ValueError("canonicalization changed the image graph")
    return {**checked, "producer_reference": reference,
            "raw_archive": raw.name, "raw_archive_sha256": original["archive_sha256"],
            "raw_archive_bytes": original["archive_bytes"], "canonical_archive": output.name,
            "removed_outer_directories": original["outer_directories"],
            "preserved": ["all_blob_bytes", "manifest_digest", "config_digest", "diff_ids"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("raw", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("image_id")
    parser.add_argument("repository")
    options = parser.parse_args()
    try:
        print(json.dumps(canonicalize(options.raw, options.output, options.image_id, options.repository), sort_keys=True))
    except (ValueError, OSError, tarfile.TarError, EOFError, KeyError, TypeError):
        raise SystemExit("OCI transport canonicalization failed; retain the incomplete output for inspection.") from None
