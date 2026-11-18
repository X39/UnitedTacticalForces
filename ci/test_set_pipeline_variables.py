"""The operator script that enters all pipeline variables: prompting, restrictions and secrecy (Linux, bash)."""

import base64
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "deploy" / "operator" / "set-pipeline-variables.sh"

# Stand-in for `mozaic-admin`: records its arguments and standard input, exits with STUB_EXIT.
STUB = """import os, sys
directory = os.environ["STUB_DIR"]
count = len([n for n in os.listdir(directory) if n.endswith(".args")])
with open(os.path.join(directory, "call-%d.args" % count), "w") as sink:
    sink.write("\\n".join(sys.argv[1:]))
with open(os.path.join(directory, "call-%d.stdin" % count), "wb") as sink:
    sink.write(sys.stdin.buffer.read())
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
        (self.root / "stub.py").write_text(STUB)
        self.key = self.root / "key"
        subprocess.run(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-C", "t", "-f", str(self.key)], check=True)

    def run_script(self, answers, *arguments, status="0"):
        env = {**os.environ, "UTF_VARS_SKIP_ROOT_CHECK": "1", "STUB_DIR": str(self.calls), "STUB_EXIT": status,
               "MOZAIC_ADMIN_CMD": f"{sys.executable} {self.root / 'stub.py'}"}
        text = "".join(line + "\n" for line in answers)
        return subprocess.run(["bash", str(SCRIPT), *arguments], input=text, capture_output=True, text=True, env=env)

    def answers(self, **given):
        return [given.get(name, "") for name in ORDER]

    def recorded(self):
        result = []
        for path in sorted(self.calls.glob("call-*.args")):
            arguments = path.read_text().split("\n")
            result.append((arguments, path.with_suffix(".stdin").read_bytes()))
        return result

    def test_list_names_every_variable_without_prompting(self):
        result = self.run_script([], "--list")
        self.assertEqual(result.returncode, 0)
        for name in ORDER:
            self.assertIn(name, result.stdout)

    def test_enters_the_given_variables_restricted_and_passes_values_only_on_stdin(self):
        secret, token = "pgpassword-abc123", "mzoci_client_abcdefghij"
        answers = self.answers(UTF_DEPLOY_SSH_KEY=str(self.key), POSTGRES_PASSWORD=secret, WORKER_REGISTRY_TOKEN=token)
        result = self.run_script(answers + ["y"])
        self.assertEqual(result.returncode, 0, result.stderr)
        calls = self.recorded()
        self.assertEqual(len(calls), 3)
        names = sorted(call[0][call[0].index("--name") + 1] for call in calls)
        self.assertEqual(names, ["POSTGRES_PASSWORD", "UTF_DEPLOY_SSH_KEY", "WORKER_REGISTRY_TOKEN"])
        by_name = {call[0][call[0].index("--name") + 1]: call for call in calls}
        self.assertEqual(by_name["POSTGRES_PASSWORD"][1], secret.encode())
        self.assertEqual(by_name["WORKER_REGISTRY_TOKEN"][1], token.encode())
        self.assertEqual(base64.b64decode(by_name["UTF_DEPLOY_SSH_KEY"][1]), self.key.read_bytes())
        operations = set()
        for arguments, stdin in calls:
            joined = "\n".join(arguments)
            for needle in (secret, token, stdin.decode()[:20]):
                self.assertNotIn(needle, joined)  # no value ever reaches an argument
            for flag, expected in (("--branch", "refs/heads/master"), ("--pipeline", ".mozaic/release.yml"),
                                   ("--organization-id", "01a16590-011f-7d72-91fb-61d57c148fe7"),
                                   ("--project-id", "01a166ad-9024-7b30-a9bc-cd03ac37f2e8"),
                                   ("--repository-id", "01a166ad-93f3-7fa2-a4f0-e57014752726")):
                self.assertEqual(arguments[arguments.index(flag) + 1], expected)
            self.assertIn("--value-stdin", arguments)
            operations.add(arguments[arguments.index("--operation-id") + 1])
        self.assertEqual(len(operations), 3)  # a fresh operation id per value
        combined = result.stdout + result.stderr
        for needle in (secret, token):
            self.assertNotIn(needle, combined)

    def test_cancelling_or_entering_nothing_writes_nothing(self):
        self.assertEqual(self.run_script(self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["n"]).returncode, 0)
        self.assertEqual(self.run_script(self.answers() + [""]).returncode, 0)
        self.assertEqual(self.recorded(), [])

    def test_short_values_and_bad_keys_are_refused_before_anything_is_written(self):
        short = self.run_script(self.answers(POSTGRES_PASSWORD="true") + ["y"])
        self.assertEqual(short.returncode, 1)
        self.assertIn("shorter than", short.stderr)
        bad = self.run_script(self.answers(UTF_DEPLOY_SSH_KEY=str(self.key) + ".pub") + ["y"])
        self.assertEqual(bad.returncode, 1)
        self.assertEqual(self.recorded(), [])

    def test_a_failing_tool_is_reported_by_name_and_fails_the_script(self):
        result = self.run_script(self.answers(POSTGRES_PASSWORD="pgpassword-abc123") + ["y"], status="1")
        self.assertEqual(result.returncode, 1)
        self.assertIn("FAILED: POSTGRES_PASSWORD", result.stderr)
        self.assertNotIn("pgpassword-abc123", result.stdout + result.stderr)

    def test_only_prompts_for_one_variable_and_rejects_unknown_names(self):
        result = self.run_script(["hetznertoken-xyz987", "y"], "--only", "HETZNER_TOKEN")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(self.recorded()), 1)
        self.assertEqual(self.run_script([], "--only", "NOPE").returncode, 1)


if __name__ == "__main__":
    unittest.main()
