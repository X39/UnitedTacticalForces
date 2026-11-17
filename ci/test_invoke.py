"""Invoke-only SSH parsing and proxy tests; live SSH/Podman acceptance is separate."""

import importlib.util
import hashlib
import io
import json
import os
from pathlib import Path
import stat
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import uuid


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# Target-side parsing remains owned by the installed stateful actor.
CLIENT = load("invoke-protected-deploy")
PROXY = load("ssh-connect-proxy")
TRANSFER = load("oci_transfer")
ATTEMPT = str(uuid.uuid4())
REQUEST = "deploy --attempt " + ATTEMPT + " --release-job make_artifacts --wait"


class InvokeBoundaryTests(unittest.TestCase):
    def test_hosts_accept_canonical_public_addresses_and_dns_only(self):
        for module in (CLIENT, PROXY):
            for host in ("target.example.test", "2a01:4f8:c0c:e2e::1", "8.8.8.8"):
                with self.subTest(module=module.__name__, host=host):
                    self.assertEqual(module.validate_host(host), host)
            for host in ("127.0.0.1", "10.0.0.1", "192.168.1.1", "169.254.169.254",
                         "100.64.0.1", "192.0.0.9", "192.88.99.1", "192.0.2.1", "198.51.100.1", "203.0.113.1",
                         "224.0.0.1", "240.0.0.1", "255.255.255.255", "0.0.0.0",
                         "::1", "::", "fc00::1", "fe80::1", "ff02::1", "2001:db8::1",
                         "::ffff:8.8.8.8", "::ffff:808:808", "64:ff9b::808:808",
                         "2001::1", "2002:808:808::1", "3fff::1",
                         "2A01:4F8:C0C:E2E::1", "2a01:04f8:c0c:e2e::1",
                         "[2a01:4f8:c0c:e2e::1]", "2a01:4f8:c0c:e2e::1%eth0",
                         "127.1", "010.000.000.001", "host..test", "-host.test"):
                with self.subTest(module=module.__name__, host=host), self.assertRaises(ValueError):
                    module.validate_host(host)

    def test_client_pins_host_key_uses_proxy_and_does_not_send_local_paths(self):
        manifest = {"version": 1, "target": "amsel", "host": "target.example.test",
                    "port": 22, "user": "deploy", "key_file": str(Path("private-key").absolute()),
                    "known_hosts_file": str(Path("known-hosts").absolute())}
        metadata = Mock(st_mode=stat.S_IFREG | 0o600, st_size=100)
        with patch.object(Path, "stat", return_value=metadata), patch.object(Path, "is_symlink", return_value=False):
            argv = CLIENT.ssh_arguments(manifest, "amsel", ATTEMPT, "make_artifacts", 600)
            self.assertIn("-oStrictHostKeyChecking=yes", argv)
            self.assertTrue(any(arg.startswith("-oProxyCommand=") for arg in argv))
            self.assertEqual(argv[-1], REQUEST + " --timeout 600")
            release = "b" * 64
            receive = CLIENT.ssh_arguments(manifest, "amsel", ATTEMPT, "make_artifacts", 600, release)
            self.assertEqual(receive[:-1], argv[:-1])
            self.assertEqual(receive[-1], f"receive-oci-v1 --attempt {ATTEMPT} --release-job make_artifacts --release {release} --timeout 600")
            activate = CLIENT.ssh_arguments(manifest, "amsel", ATTEMPT, "make_artifacts", 600, release, activate=True)
            self.assertEqual(activate[:-1], argv[:-1])
            self.assertEqual(activate[-1], receive[-1].replace("receive-oci-v1", "activate-oci-v1", 1))
            owner_receive = CLIENT.ssh_arguments(
                manifest, "amsel", ATTEMPT, "make_artifacts", 600, release, owner_key=True)
            self.assertEqual(owner_receive[:-1], argv[:-1])
            self.assertEqual(owner_receive[-1], receive[-1].replace("receive-oci-v1", "receive-owner-oci-v1", 1))
            with self.assertRaises(ValueError):
                CLIENT.ssh_arguments(manifest, "amsel", ATTEMPT, "make_artifacts", 600, "bad")
            for host in ("2a01:4f8:c0c:e2e::1", "8.8.8.8"):
                literal_argv = CLIENT.ssh_arguments(dict(manifest, host=host), "amsel", ATTEMPT, "make_artifacts", 600)
                self.assertEqual(literal_argv[-2], host)
                self.assertTrue(any(arg.startswith("-oProxyCommand=") and host in arg for arg in literal_argv))
            for change in ({"host": "-oProxyCommand=evil"}, {"host": "host;id"},
                           {"user": "-root"}, {"target": "other"}, {"port": True},
                           {"credential": "/private/credential.json"}):
                with self.subTest(change=change), self.assertRaises(ValueError):
                    CLIENT.ssh_arguments(dict(manifest, **change), "amsel", ATTEMPT, "make_artifacts", 600)
        with patch.object(Path, "stat", return_value=Mock(st_mode=stat.S_IFREG | 0o644, st_size=100)), \
                patch.object(Path, "is_symlink", return_value=False):
            with self.assertRaises(ValueError):
                CLIENT.ssh_arguments(manifest, "amsel", ATTEMPT, "make_artifacts", 600)

    def test_connect_uses_exact_authority_and_preserves_ssh_bytes(self):
        connection = Mock()
        connection.recv.side_effect = [bytes([value]) for value in b"HTTP/1.1 200 Connection established\r\n\r\n"]
        with patch.object(PROXY.socket, "create_connection", return_value=connection) as create:
            self.assertIs(PROXY.connect("http://attempt-proxy:3128", "target.example.test", 22), connection)
            create.assert_called_once_with(("attempt-proxy", 3128), timeout=20)
            connection.sendall.assert_called_once_with(b"CONNECT target.example.test:22 HTTP/1.1\r\nHost: target.example.test:22\r\n\r\n")
        for proxy, host, port in (("", "target.example.test", 22),
                                  ("https://proxy:3128", "target.example.test", 22),
                                  ("http://user:secret@proxy:3128", "target.example.test", 22),
                                  ("http://proxy:3128", "host\r\nInjected: value", 22),
                                  ("http://proxy:3128", "target.example.test", 0)):
            with self.subTest(proxy=proxy, host=host, port=port), self.assertRaises(ValueError):
                PROXY.connect(proxy, host, port)

    @unittest.skipUnless(os.name == "posix", "OCI transfer uses the Linux runner pipe contract")
    def test_receive_frames_exact_order_and_rejects_escape_and_oversize(self):
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Path(temporary)
            files = {role: role.encode("ascii") * 3 for role in TRANSFER.ROLES}
            receipts = {}
            for role, data in reversed(tuple(files.items())):
                archive = workspace / f"{role}.tar"
                archive.write_bytes(data)
                receipts[role] = {"path": archive.name, "digest": "sha256:" + hashlib.sha256(data).hexdigest()}
            manifest = workspace / "release.json"
            manifest.write_text(json.dumps(receipts), encoding="utf-8")
            size = sum(map(len, files.values()))
            with TRANSFER.open_release(manifest, workspace, size) as (reference, archives):
                digest_set = "".join(f"{role}={receipts[role]['digest']}\n" for role in files)
                digest_set += "retained_worker=sha256:2adb5fdefde4e5f1410c2cc2d87eac08a7d7238a59f8d741d84494c42a7c5be3\n"
                self.assertEqual(reference, hashlib.sha256(digest_set.encode("ascii")).hexdigest())
                data = b"mozaic-utf-local-oci-v1\n" + struct.pack("!QQ", *map(len, files.values())) + b"".join(files.values())
                command = [sys.executable, "-c", f"import sys; sys.exit(sys.stdin.buffer.read() != {data!r})"]
                self.assertEqual(TRANSFER.send_release(command, archives, 5), 0)
            with self.assertRaises(ValueError), TRANSFER.open_release(manifest, workspace, size - 1):
                self.fail("Accepted oversized release")
            with TRANSFER.open_release(manifest, workspace, size) as (_, archives):
                owner_data = b"MOZAIC-ATTEMPT-V1 mzdeploy_fixture_token_01\n" + data
                command = [sys.executable, "-c", f"import sys; sys.exit(sys.stdin.buffer.read() != {owner_data!r})"]
                self.assertEqual(TRANSFER.send_release(command, archives, 5, evidence="mzdeploy_fixture_token_01"), 0)
            with patch.object(TRANSFER.subprocess, "Popen") as launch:
                with self.assertRaises(ValueError):
                    TRANSFER.send_release(command, archives, 5, evidence="bad\ninjected")
                launch.assert_not_called()
            receipts["web"]["path"] = sys.executable
            manifest.write_text(json.dumps(receipts), encoding="utf-8")
            with self.assertRaises(ValueError), TRANSFER.open_release(manifest, workspace, size):
                self.fail("Accepted archive outside the workspace")

    @unittest.skipUnless(os.name == "posix", "OCI transfer uses the Linux runner pipe contract")
    def test_receive_timeout_stops_an_unresponsive_consumer(self):
        command = [sys.executable, "-c", "import time; time.sleep(60)"]
        with self.assertRaises(subprocess.TimeoutExpired):
            TRANSFER.send_release(command, [(io.BytesIO(b"image"), 5) for _ in TRANSFER.ROLES], 0.1)

    def test_connect_public_ipv6_authority_is_bracketed_and_ipv4_is_bare(self):
        for host, authority in (("2a01:4f8:c0c:e2e::1", "[2a01:4f8:c0c:e2e::1]:22"),
                                ("8.8.8.8", "8.8.8.8:22")):
            connection = Mock()
            connection.recv.side_effect = [bytes([value]) for value in b"HTTP/1.1 200 OK\r\n\r\n"]
            with self.subTest(host=host), patch.object(PROXY.socket, "create_connection", return_value=connection):
                self.assertIs(PROXY.connect("http://attempt-proxy:3128", host, 22), connection)
                connection.sendall.assert_called_once_with(("CONNECT " + authority + " HTTP/1.1\r\nHost: "
                                                           + authority + "\r\n\r\n").encode("ascii"))

    def test_connect_rejection_truncation_and_oversized_headers_fail_closed(self):
        for response in (b"HTTP/1.1 403 Forbidden\r\n\r\n", b"", b"x" * 8200):
            connection = Mock()
            connection.recv.side_effect = [bytes([value]) for value in response] + [b""]
            with patch.object(PROXY.socket, "create_connection", return_value=connection):
                with self.assertRaises(ValueError):
                    PROXY.connect("http://proxy:3128", "target.example.test", 22)
            connection.close.assert_called_once()


if __name__ == "__main__":
    unittest.main()
