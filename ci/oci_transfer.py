"""Stream the fixed UTF OCI release to an SSH receiver."""

import contextlib
import hashlib
import json
import os
from pathlib import Path
import re
import selectors
import stat
import struct
import subprocess
import time


ROLES = ("api", "web")
MAGIC = b"mozaic-utf-local-oci-v1\n"
RETAINED_WORKER = "sha256:2adb5fdefde4e5f1410c2cc2d87eac08a7d7238a59f8d741d84494c42a7c5be3"
CHUNK = 1024 * 1024


@contextlib.contextmanager
def open_release(manifest, workspace, max_bytes):
    """Resolve a bounded local archive set and its independently checked reference."""
    workspace = workspace.resolve(strict=True)
    flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK
    if not manifest.resolve(strict=True).is_relative_to(workspace):
        raise ValueError("OCI transfer manifest must stay within the workspace")
    with os.fdopen(os.open(manifest, flags), "rb") as source:
        if not stat.S_ISREG(os.fstat(source.fileno()).st_mode):
            raise ValueError("Invalid OCI transfer manifest")
        contents = source.read(65537)
    if len(contents) > 65536 or max_bytes <= 0:
        raise ValueError("Invalid OCI transfer manifest")
    values = json.loads(contents)
    if not isinstance(values, dict) or set(values) != set(ROLES):
        raise ValueError("UTF transfer requires exactly API and web")
    digests, archives = [], []
    with contextlib.ExitStack() as files:
        for role in ROLES:
            item = values[role]
            if (not isinstance(item, dict) or set(item) != {"path", "digest"}
                    or not isinstance(item["path"], str) or not isinstance(item["digest"], str)
                    or not re.fullmatch(r"sha256:[0-9a-f]{64}", item["digest"])):
                raise ValueError("Invalid OCI component receipt")
            path = workspace / item["path"]
            if not path.resolve(strict=True).is_relative_to(workspace):
                raise ValueError("OCI archive must stay within the workspace")
            archive = files.enter_context(os.fdopen(os.open(path, flags), "rb"))
            metadata = os.fstat(archive.fileno())
            if not stat.S_ISREG(metadata.st_mode) or not 0 < metadata.st_size <= max_bytes:
                raise ValueError("OCI transfer exceeds its archive limit")
            max_bytes -= metadata.st_size
            archives.append((archive, metadata.st_size))
            digests.append(f"{role}={item['digest']}\n")
        digests.append(f"retained_worker={RETAINED_WORKER}\n")
        reference = hashlib.sha256("".join(digests).encode("ascii")).hexdigest()
        yield reference, archives


def release_chunks(archives, prefix):
    if prefix:
        yield prefix
    yield MAGIC + struct.pack("!QQ", *(size for _, size in archives))
    for archive, size in archives:
        remaining = size
        while remaining:
            chunk = archive.read(min(remaining, CHUNK))
            if not chunk:
                raise ValueError("OCI archive was truncated")
            remaining -= len(chunk)
            yield chunk
        if archive.read(1):
            raise ValueError("OCI archive changed during transfer")


def send_release(command, archives, timeout, *, evidence=None):
    if len(archives) != len(ROLES):
        raise ValueError("UTF transfer requires exactly API and web")
    prefix = b""
    if evidence is not None:
        if not isinstance(evidence, str) or not re.fullmatch(r"mzdeploy_[A-Za-z0-9_-]{16,471}", evidence):
            raise ValueError("Invalid attempt evidence")
        prefix = f"MOZAIC-ATTEMPT-V1 {evidence}\n".encode("ascii")
        if len(prefix) > 512:
            raise ValueError("Invalid attempt evidence")
    deadline = time.monotonic() + timeout
    with subprocess.Popen(command, stdin=subprocess.PIPE, bufsize=0) as process:
        try:
            with process.stdin as sink, selectors.DefaultSelector() as selector:
                descriptor = sink.fileno()
                os.set_blocking(descriptor, False)
                selector.register(descriptor, selectors.EVENT_WRITE)
                for chunk in release_chunks(archives, prefix):
                    pending = memoryview(chunk)
                    while pending:
                        remaining = deadline - time.monotonic()
                        if remaining <= 0 or not selector.select(remaining):
                            raise subprocess.TimeoutExpired("ssh OCI transfer", timeout)
                        try:
                            pending = pending[os.write(descriptor, pending):]
                        except BlockingIOError:
                            continue
            return process.wait(timeout=max(0, deadline - time.monotonic()))
        except (ValueError, OSError, subprocess.TimeoutExpired):
            process.kill()
            raise
