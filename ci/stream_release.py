#!/usr/bin/env python3
"""Push the exact checked API and web OCI archives into podman on the target over SSH.

The target runs no custom code: its authorized_keys entry for the CI key forces the single command
`podman load`, which only imports an image archive. Each archive is streamed as stdin of one SSH
connection. No registry credential exists on either side and the host never pulls. The only secret is the
SSH key written by ci/prepare-deploy-key.sh from a trusted-branch pipeline variable.

The image identity is fixed by content: podman stores layers by digest and the image ID is the config
digest, which this script computes from the archive before sending and records in release.json.
"""

import argparse
import importlib.util
import json
from pathlib import Path
import re
import shlex
import stat
import subprocess
import sys
import tarfile

DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
HERE = Path(__file__).resolve().parent
# Safe even if the host entry were not forced: it can only import an image archive.
REMOTE_COMMAND = "podman load"


def load_proxy():
    spec = importlib.util.spec_from_file_location("ssh_connect_proxy", HERE / "ssh-connect-proxy.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def archive_identity(path):
    """Return (manifest digest, config digest) of a single-image OCI layout archive."""
    with tarfile.open(path, "r:") as archive:
        def member_json(name):
            member = archive.getmember(name)
            if not member.isreg() or member.size > 1024 * 1024:
                raise ValueError("Invalid OCI metadata")
            return json.loads(archive.extractfile(member).read())

        manifests = member_json("index.json").get("manifests")
        if not isinstance(manifests, list) or len(manifests) != 1:
            raise ValueError("OCI archive must hold exactly one manifest")
        manifest_digest = manifests[0].get("digest")
        if not isinstance(manifest_digest, str) or not DIGEST.fullmatch(manifest_digest):
            raise ValueError("Invalid OCI manifest digest")
        config_digest = member_json("blobs/sha256/" + manifest_digest[7:]).get("config", {}).get("digest")
        if not isinstance(config_digest, str) or not DIGEST.fullmatch(config_digest):
            raise ValueError("Invalid OCI config digest")
    return manifest_digest, config_digest


def ssh_arguments(manifest, target, timeout):
    keys = {"version", "target", "host", "port", "user", "key_file", "known_hosts_file"}
    if set(manifest) != keys or manifest["version"] != 1 or manifest["target"] != target:
        raise ValueError("Invalid target manifest")
    proxy = load_proxy()
    host = proxy.validate_host(manifest["host"])
    if (not re.fullmatch(r"[a-z_][a-z0-9_-]{0,31}", manifest["user"]) or type(manifest["port"]) is not int
            or not 1 <= manifest["port"] <= 65535 or not 30 <= timeout <= 3600):
        raise ValueError("Invalid SSH boundary")
    files = []
    for field in ("key_file", "known_hosts_file"):
        path = Path(manifest[field])
        if not path.is_absolute() or path.is_symlink():
            raise ValueError("SSH files require absolute regular paths")
        metadata = path.stat()
        if not stat.S_ISREG(metadata.st_mode) or not 0 < metadata.st_size <= 65536:
            raise ValueError("Invalid SSH file")
        if field == "key_file" and metadata.st_mode & 0o077:
            raise ValueError("SSH key must be private")
        files.append(str(path))
    proxy_command = " ".join(shlex.quote(part) for part in [
        sys.executable, "-I", str(HERE / "ssh-connect-proxy.py"), host, str(manifest["port"])])
    return ["ssh", "-F", "/dev/null", "-T", "-oBatchMode=yes", "-oIdentitiesOnly=yes",
            "-oStrictHostKeyChecking=yes", "-oGlobalKnownHostsFile=/dev/null",
            "-oUserKnownHostsFile=" + files[1], "-oClearAllForwardings=yes",
            "-oConnectTimeout=20", "-oConnectionAttempts=1", "-oServerAliveInterval=15",
            "-oServerAliveCountMax=4", "-i", files[0], "-p", str(manifest["port"]),
            "-oProxyCommand=" + proxy_command, "-l", manifest["user"], host, REMOTE_COMMAND]


def push(argv, archive, size_limit, timeout):
    size = archive.stat().st_size
    if not 0 < size <= size_limit:
        raise ValueError("OCI archive exceeds its size limit")
    with archive.open("rb") as source:
        result = subprocess.run(argv, stdin=source, timeout=timeout, check=False)
    return result.returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--target", required=True)
    parser.add_argument("--api", required=True, type=Path)
    parser.add_argument("--web", required=True, type=Path)
    parser.add_argument("--max-archive-bytes", type=int, default=1 << 30)
    parser.add_argument("--timeout", type=int, default=1200)
    parser.add_argument("--release", type=Path, default=Path("release.json"),
                        help="Where to record the pushed identities")
    args = parser.parse_args()
    if args.manifest.stat().st_size > 65536:
        raise ValueError("Manifest is too large")
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    workspace = Path.cwd().resolve(strict=True)
    release, paths = {}, {}
    for role, path in (("api", args.api), ("web", args.web)):
        resolved = path.resolve(strict=True)
        manifest_digest, config_digest = archive_identity(resolved)
        paths[role] = resolved
        release[role] = {"path": str(resolved.relative_to(workspace)), "manifest": manifest_digest,
                         "image_id": config_digest[7:]}
    args.release.write_bytes(json.dumps(release, separators=(",", ":"), sort_keys=True).encode("ascii"))
    argv = ssh_arguments(manifest, args.target, args.timeout)
    budget = args.max_archive_bytes
    for role in ("api", "web"):
        budget -= paths[role].stat().st_size
        if budget < 0:
            raise ValueError("OCI archives exceed their combined size limit")
    for role in ("api", "web"):
        status = push(argv, paths[role], args.max_archive_bytes, args.timeout)
        if status:
            print(f"UTF push failed for {role} with SSH exit code {status}", file=sys.stderr)
            return max(1, status)
        print(f"UTF push: {role} loaded, image id {release[role]['image_id']}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, TypeError, KeyError, OSError, tarfile.TarError, subprocess.TimeoutExpired):
        print("UTF push failed before or during transfer.", file=sys.stderr)
        sys.exit(1)
