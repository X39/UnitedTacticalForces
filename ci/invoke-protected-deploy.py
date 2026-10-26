#!/usr/bin/env python3
"""Invoke a target-bound SSH deployment command from any ordinary CI run step."""

import argparse
import json
from pathlib import Path
import re
import ipaddress
import shlex
import stat
import subprocess
import sys
import uuid


def validate_host(host):
    """Accept DNS names or canonical unicast addresses in the public egress subset."""
    if not isinstance(host, str) or not host or len(host) > 253 or "%" in host:
        raise ValueError("Invalid endpoint host")
    try:
        address = ipaddress.ip_address(host)
    except ValueError:
        if (not re.fullmatch(r"[a-z0-9.-]+", host)
                or re.fullmatch(r"[0-9.]+", host)
                or any(not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?", label)
                       for label in host.split("."))):
            raise ValueError("Invalid endpoint host") from None
        return host
    if (str(address) != host or not address.is_global or address.is_multicast
            or address.is_reserved or getattr(address, "ipv4_mapped", None) is not None):
        raise ValueError("Endpoint address must be canonical public unicast")
    if address.version == 6:
        if (address not in ipaddress.ip_network("2000::/3")
                or any(address in ipaddress.ip_network(network) for network in
                       ("2001::/23", "2001:db8::/32", "2002::/16", "3fff::/20"))):
            raise ValueError("Endpoint address is outside public egress subset")
    if address.version == 4 and any(address in ipaddress.ip_network(network) for network in
            ("0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16",
             "172.16.0.0/12", "192.0.0.0/24", "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16",
             "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/3")):
        raise ValueError("Endpoint address is outside public egress subset")
    return host


def ssh_arguments(manifest, target, attempt, release_job, timeout):
    if (set(manifest) != {"version", "target", "host", "port", "user", "key_file", "known_hosts_file"}
            or type(manifest["version"]) is not int or manifest["version"] != 1
            or manifest["target"] != target):
        raise ValueError("Invalid target manifest")
    validate_host(manifest["host"])
    if (not re.fullmatch(r"[a-z_][a-z0-9_-]{0,31}", manifest["user"])
            or type(manifest["port"]) is not int or not 1 <= manifest["port"] <= 65535
            or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", release_job)
            or not 30 <= timeout <= 1800):
        raise ValueError("Invalid SSH boundary")
    paths = []
    for field in ("key_file", "known_hosts_file"):
        path = Path(manifest[field])
        if not path.is_absolute() or path.is_symlink():
            raise ValueError("SSH files require absolute regular paths")
        metadata = path.stat()
        if not stat.S_ISREG(metadata.st_mode) or not 0 < metadata.st_size <= 65536:
            raise ValueError("Invalid SSH file")
        if field == "key_file" and metadata.st_mode & 0o077:
            raise ValueError("SSH invocation key must be private")
        paths.append(str(path))
    command = "deploy --attempt " + str(uuid.UUID(attempt)) + " --release-job " + release_job
    command += " --wait --timeout " + str(timeout)
    proxy = " ".join(shlex.quote(part) for part in [sys.executable, "-I",
                     str(Path(__file__).resolve().with_name("ssh-connect-proxy.py")),
                     manifest["host"], str(manifest["port"])])
    return ["ssh", "-F", "/dev/null", "-T", "-oBatchMode=yes", "-oIdentitiesOnly=yes",
            "-oStrictHostKeyChecking=yes", "-oGlobalKnownHostsFile=/dev/null",
            "-oUserKnownHostsFile=" + paths[1], "-oClearAllForwardings=yes",
            "-oConnectTimeout=20", "-oConnectionAttempts=1", "-oServerAliveInterval=15",
            "-oServerAliveCountMax=2", "-i", paths[0], "-p", str(manifest["port"]),
            "-oProxyCommand=" + proxy,
            "-l", manifest["user"], manifest["host"], command]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--target", required=True)
    parser.add_argument("--attempt", required=True)
    parser.add_argument("--release-job", required=True)
    parser.add_argument("--wait", required=True, action="store_true")
    parser.add_argument("--timeout", default=600, type=int)
    args = parser.parse_args()
    if args.manifest.stat().st_size > 65536:
        raise ValueError("Manifest is too large")
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    command = ssh_arguments(manifest, args.target, args.attempt, args.release_job, args.timeout)
    result = subprocess.run(command, stdin=subprocess.DEVNULL, check=False, timeout=args.timeout + 30)
    return result.returncode if result.returncode > 0 else (1 if result.returncode else 0)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, TypeError, KeyError, OSError, subprocess.TimeoutExpired):
        print("Deployment invocation failed.", file=sys.stderr)
        sys.exit(1)
