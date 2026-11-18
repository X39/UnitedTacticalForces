"""The local operator script that enters all pipeline variables over SSH: prompting, restrictions, secrecy."""

import base64
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "deploy" / "operator" / "set-pipeline-variables.sh"

# Stand-in for `ssh`: answers the few preflight commands and records everything else with its stdin.
STUB = """#!{python}
import json, os, sys
directory = os.environ["STUB_DIR"]
command = sys.argv[-1]
stdin = sys.stdin.buffer.read()
count = len([n for n in os.listdir(directory) if n.endswith(".json")])
with open(os.path.join(directory, "call-%03d.json" % count), "w") as sink:
    json.dump({{"argv": sys.argv[1:], "command": command, "stdin": stdin.hex()}}, sink)
if command == "id -u":
    print(os.environ.get("STUB_UID", "0"))
elif command.startswith("podman ps"):
    print(os.environ.get("STUB_IMAGES", ""))
elif command.startswith("test -r"):
    sys.exit(int(os.environ.get("STUB_CA", "1")))
elif "mktemp -d" in command:
    print("/var/tmp/mozaic-varset.abc123")
elif "pipeline-variable-set" in command:
    sys.exit(int(os.environ.get("STUB_EXIT", "0")))
"""
ORDER = ["UTF_DEPLOY_SSH_KEY", "POSTGRES_PASSWORD", "STEAM_API_KEY", "DISCORD_CLIENT_ID", "DISCORD_CLIENT_SECRET",
         "DISCORD_APPLICATION_ID", "DISCORD_PUBLIC_KEY", "DISCORD_BOT_TOKEN", "TEAMSPEAK_PASSWORD",
         "TEAMSPEAK_CHANNEL_PASSWORD", "HETZNER_TOKEN", "WORKER_REGISTRY_TOKEN"]


@unittest.skipUnless(subprocess.run(["sh", "-c", "command -v bash ssh-keygen"], capture_output=True).returncode == 0,
                     "bash and ssh-keygen are required")
class OperatorScriptTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.calls = self.root / "calls"
        self.calls.mkdir()
        self.ssh = self.root / "ssh"
        self.ssh.write_text(STUB.format(python=sys.executable))
        self.ssh.chmod(self.ssh.stat().st_mode | stat.S_IXUSR)
        self.key = self.root / "key"
        subprocess.run(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-C", "t", "-f", str(self.key)], check=True)

    def run_script(self, answers, *arguments, **extra):
        env = {**os.environ, "STUB_DIR": str(self.calls), "UTF_SSH": str(self.ssh),
               "UTF_OPERATOR_SSH": "root@amsel", "MOZAIC_ADMIN_CMD": "mozaic-admin"}
        for name in [n for n in env if n.startswith("STUB_") and n != "STUB_DIR"]:
            env.pop(name)
        env.update({key: str(value) for key, value in extra.items() if value is not None})
        for key, value in extra.items():
            if value is None:
                env.pop(key, None)
        text = "".join(line + "\n" for line in answers)
        return subprocess.run(["bash", str(SCRIPT), *arguments], input=text, capture_output=True, text=True, env=env)

    def answers(self, **given):
        return [given.get(name, "") for name in ORDER]

    def all_calls(self):
        return [json.loads(path.read_text()) for path in sorted(self.calls.glob("call-*.json"))]

    def writes(self):
        return [call for call in self.all_calls() if "pipeline-variable-set" in call["command"]]

    @staticmethod
    def flag(command, name):
        words = command.split()
        return words[words.index(name) + 1]

    def test_list_names_every_variable_without_prompting_or_ssh(self):
        result = self.run_script([], "--list")
        self.assertEqual(result.returncode, 0)
        for name in ORDER:
            self.assertIn(name, result.stdout)
        self.assertEqual(self.all_calls(), [])

    def test_enters_the_given_variables_restricted_and_passes_values_only_on_stdin(self):
        secret, token = "pgpassword-abc123", "mzoci_client_abcdefghij"
        answers = self.answers(UTF_DEPLOY_SSH_KEY=str(self.key), POSTGRES_PASSWORD=secret, WORKER_REGISTRY_TOKEN=token)
        result = self.run_script(answers + ["y"])
        self.assertEqual(result.returncode, 0, result.stderr)
        writes = self.writes()
        self.assertEqual(len(writes), 3)
        by_name = {self.flag(call["command"], "--name"): call for call in writes}
        self.assertEqual(sorted(by_name), ["POSTGRES_PASSWORD", "UTF_DEPLOY_SSH_KEY", "WORKER_REGISTRY_TOKEN"])
        self.assertEqual(bytes.fromhex(by_name["POSTGRES_PASSWORD"]["stdin"]), secret.encode())
        self.assertEqual(bytes.fromhex(by_name["WORKER_REGISTRY_TOKEN"]["stdin"]), token.encode())
        self.assertEqual(base64.b64decode(bytes.fromhex(by_name["UTF_DEPLOY_SSH_KEY"]["stdin"])), self.key.read_bytes())
        operations = set()
        for call in writes:
            command, stdin = call["command"], bytes.fromhex(call["stdin"]).decode()
            self.assertEqual(call["argv"][:3], ["-T", "-o", "BatchMode=yes"])
            self.assertEqual(call["argv"][3], "root@amsel")
            for needle in (secret, token, stdin[:20]):
                self.assertNotIn(needle, command)  # no value ever reaches the remote command line
            for flag, expected in (("--branch", "refs/heads/master"), ("--pipeline", ".mozaic/release.yml"),
                                   ("--organization-id", "01a16590-011f-7d72-91fb-61d57c148fe7"),
                                   ("--project-id", "01a166ad-9024-7b30-a9bc-cd03ac37f2e8"),
                                   ("--repository-id", "01a166ad-93f3-7fa2-a4f0-e57014752726")):
                self.assertEqual(self.flag(command, flag), expected)
            self.assertIn("--value-stdin", command)
            operations.add(self.flag(command, "--operation-id"))
        self.assertEqual(len(operations), 3)  # a fresh operation id per value
        for needle in (secret, token):
            self.assertNotIn(needle, result.stdout + result.stderr)

    def test_cancelling_or_entering_nothing_writes_nothing_and_never_connects(self):
        self.assertEqual(self.run_script(self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["n"]).returncode, 0)
        self.assertEqual(self.run_script(self.answers() + [""]).returncode, 0)
        self.assertEqual(self.all_calls(), [])

    def test_short_values_and_bad_keys_are_refused_before_any_connection(self):
        short = self.run_script(self.answers(POSTGRES_PASSWORD="true") + ["y"])
        self.assertEqual(short.returncode, 1)
        self.assertIn("shorter than", short.stderr)
        bad = self.run_script(self.answers(UTF_DEPLOY_SSH_KEY=str(self.key) + ".pub") + ["y"])
        self.assertEqual(bad.returncode, 1)
        self.assertEqual(self.all_calls(), [])

    def test_a_failing_tool_is_reported_by_name_and_fails_the_script(self):
        result = self.run_script(self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["y"], STUB_EXIT=1)
        self.assertEqual(result.returncode, 1)
        self.assertIn("FAILED: POSTGRES_PASSWORD", result.stderr)
        self.assertNotIn("pgpassword-abc123", result.stdout + result.stderr)

    def test_only_prompts_for_one_variable_and_rejects_unknown_names(self):
        result = self.run_script(["hetznertoken-xyz987", "y"], "--only", "HETZNER_TOKEN")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(self.writes()), 1)
        self.assertEqual(self.run_script([], "--only", "NOPE").returncode, 1)

    def test_a_missing_target_or_a_non_root_login_stops_before_writing(self):
        answers = self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["y"]
        missing = self.run_script(answers, UTF_OPERATOR_SSH=None)
        self.assertEqual(missing.returncode, 1)
        self.assertIn("UTF_OPERATOR_SSH", missing.stderr)
        unprivileged = self.run_script(answers, STUB_UID=1000)
        self.assertEqual(unprivileged.returncode, 1)
        self.assertIn("not root", unprivileged.stderr)
        self.assertEqual(self.writes(), [])

    def test_the_admin_binary_is_uploaded_for_the_run_mounted_and_removed_again(self):
        binary = self.root / "mozaic-admin"
        binary.write_bytes(b"\x7fELF-not-a-real-binary")
        result = self.run_script(self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["y"], MOZAIC_ADMIN_CMD=None,
                                 MOZAIC_ADMIN_BIN=binary, MOZAIC_IMAGE="localhost/mozaic-runtime@sha256:abc")
        self.assertEqual(result.returncode, 0, result.stderr)
        calls = self.all_calls()
        upload = next(call for call in calls if call["command"].startswith("cat >"))
        self.assertEqual(bytes.fromhex(upload["stdin"]), binary.read_bytes())
        self.assertIn("/var/tmp/mozaic-varset.abc123/mozaic-admin", upload["command"])
        command = self.writes()[0]["command"]
        self.assertIn("--volume /var/tmp/mozaic-varset.abc123/mozaic-admin:/opt/mozaic-varset/mozaic-admin:ro", command)
        self.assertIn("--entrypoint /opt/mozaic-varset/mozaic-admin localhost/mozaic-runtime@sha256:abc", command)
        for hardening in ("--cap-drop all", "--security-opt no-new-privileges", "--read-only"):
            self.assertIn(hardening, command)
        self.assertNotIn("postgres-tls", command)
        self.assertTrue(calls[-1]["command"].startswith("rm -f /var/tmp/mozaic-varset.abc123/mozaic-admin"))

    def test_the_runtime_image_is_detected_and_ambiguity_is_refused(self):
        answers = self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["y"]
        one = self.run_script(answers, MOZAIC_ADMIN_CMD=None, STUB_CA=0,
                              STUB_IMAGES="localhost/mozaic-runtime@sha256:abc\nregistry/other:1")
        self.assertEqual(one.returncode, 0, one.stderr)
        command = self.writes()[0]["command"]
        self.assertIn("--entrypoint /opt/mozaic/bin/mozaic-admin localhost/mozaic-runtime@sha256:abc", command)
        self.assertIn("/etc/mozaic/postgres-tls/ca.crt:/run/mozaic-postgres-ca/ca.crt:ro", command)
        for path in self.calls.glob("call-*.json"):
            path.unlink()
        two = self.run_script(answers, MOZAIC_ADMIN_CMD=None,
                              STUB_IMAGES="localhost/mozaic-runtime@sha256:aaa\nlocalhost/mozaic-runtime@sha256:bbb")
        self.assertEqual(two.returncode, 1)
        self.assertIn("MOZAIC_IMAGE", two.stderr)
        self.assertEqual(self.writes(), [])


if __name__ == "__main__":
    unittest.main()
