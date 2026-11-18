#!/usr/bin/env python3
"""Back up, activate and verify a release on the target, in the same job as the build and the push.

Runs over the same ordinary SSH login as ci/stream_release.py (organization-wide key, unprivileged deploy user,
host key pinned). Every remote command is a fixed string built here from the build's own image IDs and revision;
nothing is sent to be executed that the repository does not contain. Steps, in order:

1. both pushed images exist in the deploy user's podman;
2. database backup (`pg_dump`) with the existing backup role, verified non-empty, last ten kept;
3. the fixed local tags `localhost/utf/{api,web}:release` move to the new image IDs (old ones become `:previous`);
4. the retired migration-managed service and its containers are stopped if still active (first release only);
5. the two static units are enabled and restarted; they run exactly those tags;
6. the running containers must use the new image IDs, then the public site must serve this revision;
7. the retired migration units are disabled so they cannot fight the new units after a reboot;
8. on any failure after step 3 the previous images are restored, or, on the very first release when none exist,
   the retired service is started again; the job then fails.
"""

import argparse
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request

HERE = Path(__file__).resolve().parent
IMAGE_ID = re.compile(r"[0-9a-f]{64}")
REVISION = re.compile(r"[0-9a-f]{40}")
UNITS = "utf-api.service utf-web.service"


def load_client():
    spec = importlib.util.spec_from_file_location("stream_release", HERE / "stream_release.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def commands(api_id, web_id, revision):
    """The complete, fixed set of remote commands."""
    config = '"$HOME/.config/mozaic-deploy/targets/utf"'
    return {
        "images": f"podman image exists {api_id} && podman image exists {web_id}",
        "backup": (
            'set -eu; umask 077; d="$HOME/backups"; mkdir -p "$d"; '
            f't="$d/utf-{revision}.dump"; c={config}; '
            'PGSERVICEFILE="$c/database.service" PGPASSFILE="$c/database.pgpass" '
            'pg_dump "service=utf" -Fc --no-owner -f "$t.part"; test -s "$t.part"; mv "$t.part" "$t"; '
            'ls -1t "$d"/utf-*.dump | tail -n +11 | xargs -r rm -f; ls -l "$t"'),
        "tag": (
            "set -eu; for r in api web; do if podman image exists localhost/utf/$r:release; then "
            "podman tag localhost/utf/$r:release localhost/utf/$r:previous; fi; done; "
            f"podman tag {api_id} localhost/utf/api:release; podman tag {web_id} localhost/utf/web:release"),
        "stop_old": (
            "set -eu; if systemctl --user is-active --quiet mozaic-utf-ordinary.service; then "
            "systemctl --user stop mozaic-utf-ordinary.service; fi; "
            "podman stop --ignore --time 30 mozaic-app-utf-web mozaic-app-utf-api"),
        "switch": f"set -eu; systemctl --user enable {UNITS}; systemctl --user restart {UNITS}",
        "retire_old": "systemctl --user disable mozaic-utf-ordinary.service mozaic-migration@utf.service",
        "probe": (
            f"systemctl --user is-active {UNITS} >/dev/null && "
            "echo \"$(podman inspect utf-api --format '{{.Image}}') $(podman inspect utf-web --format '{{.Image}}')\""),
        "rollback": (
            "set -eu; if podman image exists localhost/utf/api:previous && podman image exists localhost/utf/web:previous; then "
            "for r in api web; do podman tag localhost/utf/$r:previous localhost/utf/$r:release; done; "
            f"systemctl --user restart {UNITS}; else systemctl --user stop {UNITS}; "
            "systemctl --user start mozaic-utf-ordinary.service; fi"),
    }


def fetch(url, timeout=20):
    """(status, body) without raising on HTTP error statuses."""
    request = urllib.request.Request(url, headers={"User-Agent": "utf-release-check"})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return response.status, response.read(65536)
    except urllib.error.HTTPError as error:
        return error.code, b""


def public_check(site, revision, fetcher=fetch):
    """Return an error string, or None when the site serves this revision and the API answers."""
    status, body = fetcher(site + "/revision.json")
    try:
        served = json.loads(body).get("revision", "") if status == 200 else ""
    except (ValueError, AttributeError):
        served = ""
    if not served or not served.endswith(revision):
        return "the public site does not serve this revision yet"
    status, _ = fetcher(site + "/api/game-content/mods")
    if status not in (200, 401):
        return f"the public API answered HTTP {status}"
    return None


def activate(run, api_id, web_id, revision, site, wait, fetcher=fetch, sleep=time.sleep, clock=time.monotonic):
    """Drive the steps with run(command) -> (exit code, stdout). Returns the process exit status."""
    fixed = commands(api_id, web_id, revision)
    for name in ("images", "backup", "tag"):
        code, output = run(fixed[name])
        if output:
            print(output.strip())
        if code:
            print(f"UTF activate: step '{name}' failed with exit code {code}", file=sys.stderr)
            if name == "tag":
                # Part of the tags may have moved; restoring is idempotent and safe.
                run(fixed["rollback"])
            return 1
        print(f"UTF activate: {name} ok")

    def fail(reason):
        print(f"UTF activate: {reason}; restoring the previous images", file=sys.stderr)
        code, _ = run(fixed["rollback"])
        print("UTF activate: previous images restored" if code == 0 else "UTF activate: ROLLBACK FAILED", file=sys.stderr)
        return 1

    for name in ("stop_old", "switch"):
        code, output = run(fixed[name])
        if code:
            return fail(f"step '{name}' failed with exit code {code}")
        print(f"UTF activate: {name} ok")
    deadline = clock() + wait
    last = "the containers did not start"
    while clock() < deadline:
        code, output = run(fixed["probe"])
        running = output.replace("sha256:", "").split()
        if code == 0 and running == [api_id, web_id]:
            problem = public_check(site, revision, fetcher)
            if problem is None:
                print(f"UTF activate: running image ids match; {site} serves revision {revision}")
                code, _ = run(fixed["retire_old"])
                print("UTF activate: retired migration units disabled" if code == 0
                      else "UTF activate: could not disable the retired migration units (not fatal)")
                return 0
            last = problem
        elif code == 0:
            last = "the containers run different images than the ones pushed"
        sleep(5)
    return fail(last)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--target", required=True)
    parser.add_argument("--release", type=Path, default=Path("release.json"))
    parser.add_argument("--revision", required=True, help="the built source revision (MOZAIC_REVISION)")
    parser.add_argument("--site", required=True)
    parser.add_argument("--wait", type=int, default=300, help="seconds to wait for the new release to serve")
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()
    revision = args.revision.removeprefix("git:")
    release = json.loads(args.release.read_text(encoding="ascii"))
    api_id, web_id = release["api"]["image_id"], release["web"]["image_id"]
    if not (IMAGE_ID.fullmatch(api_id) and IMAGE_ID.fullmatch(web_id) and REVISION.fullmatch(revision)
            and re.fullmatch(r"https://[a-z0-9.-]+", args.site)):
        raise ValueError("invalid release inputs")
    client = load_client()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))

    def run(command):
        argv = client.ssh_arguments(manifest, args.target, args.timeout, command)
        result = subprocess.run(argv, capture_output=True, text=True, timeout=args.timeout, check=False)
        if result.returncode and result.stderr:
            print("host said: " + result.stderr.strip()[-400:], file=sys.stderr)
        return result.returncode, result.stdout

    return activate(run, api_id, web_id, revision, args.site.rstrip("/"), args.wait)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, TypeError, KeyError, OSError, subprocess.TimeoutExpired):
        print("UTF activate failed before it could run.", file=sys.stderr)
        sys.exit(1)
