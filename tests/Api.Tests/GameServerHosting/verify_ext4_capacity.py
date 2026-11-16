#!/usr/bin/env python3
"""Validate external-volume capacity forecasts against freshly formatted ext4 filesystems.

Run as root on Linux with mkfs.ext4, mount, and umount available. Only temporary
sparse image files are formatted; no existing block device or filesystem is used.
The caller should use a private mount namespace, for example:
    sudo unshare --mount --propagation private python3 verify_ext4_capacity.py
"""

import json
import os
from pathlib import Path
import subprocess
import tempfile


GIB = 1_073_741_824


def verify_capacity(raw_gib: int, peak_gib: int) -> None:
    """Require the forecast and measured non-reserved available bytes to fit peak demand."""
    raw_bytes = raw_gib * GIB
    peak_bytes = peak_gib * GIB
    forecast_bytes = max(0, raw_bytes - (raw_bytes + 9) // 10 - GIB)
    assert forecast_bytes >= peak_bytes, "The fixture must satisfy preflight."
    with tempfile.TemporaryDirectory(prefix="utf-ext4-capacity-") as temporary:
        root = Path(temporary)
        image = root / "volume.img"
        mount_path = root / "mounted"
        mount_path.mkdir()
        with image.open("wb") as stream:
            stream.truncate(raw_bytes)
        subprocess.run(["mkfs.ext4", "-q", "-F", "-L", "UTF_capacity", str(image)], check=True)
        subprocess.run(["mount", "-o", "loop,nosuid,nodev,noexec", str(image), str(mount_path)], check=True)
        try:
            stats = os.statvfs(mount_path)
            available_bytes = stats.f_bavail * stats.f_frsize
            assert available_bytes >= forecast_bytes, "The forecast exceeds measured ext4 capacity."
            assert available_bytes >= peak_bytes, "A preflight-approved volume cannot hold peak demand."
            assert available_bytes < raw_bytes, "A fixed raw-equals-peak request must fail its mounted gate."
            print(json.dumps({
                "raw_bytes": raw_bytes,
                "forecast_usable_bytes": forecast_bytes,
                "observed_available_bytes": available_bytes,
                "peak_bytes": peak_bytes,
                "reserved_bytes": (stats.f_bfree - stats.f_bavail) * stats.f_frsize,
            }), flush=True)
        finally:
            subprocess.run(["umount", str(mount_path)], check=True)


def main() -> None:
    """Exercise provider-minimum and representative automatically enlarged volumes."""
    if os.geteuid() != 0:
        raise SystemExit("Run with root privileges inside an existing Linux environment.")
    for raw_gib, peak_gib in [(10, 8), (13, 10), (20, 17)]:
        verify_capacity(raw_gib, peak_gib)


if __name__ == "__main__":
    main()
