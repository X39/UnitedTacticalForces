"""Check the shared worker's serialized runtime contract using inert tar fixtures."""
import importlib.util
import io
import json
from pathlib import Path
import tarfile
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location('assembly', Path(__file__).parent / 'oci-layout/assemble.py')
assembly = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assembly)


def payload(names, compressed=False):
    """Create bounded synthetic support files; none is a runnable game or tool."""
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode='w:gz' if compressed else 'w') as archive:
        for name in names:
            member = tarfile.TarInfo(name)
            member.size, member.mode = 7, 0o755
            archive.addfile(member, io.BytesIO(b'fixture'))
    return output.getvalue()


class SharedWorkerImageTests(unittest.TestCase):
    """Keep all executor support in one artifact without installing or running games."""

    def assemble(self, root, missing=()):
        """Use the real serializer/verifier with fake base metadata and package transport."""
        source = root / 'source'
        worker = source / '.publish/worker'
        worker.mkdir(parents=True)
        (worker / 'X39.UnitedTacticalForces.CloudWorker.dll').write_bytes(b'fixture')
        entrypoint = source / 'sources/CloudWorker/docker-entrypoint.sh'
        entrypoint.parent.mkdir(parents=True)
        entrypoint.write_bytes(b'fixture')
        base = SimpleNamespace(config={'architecture': 'amd64', 'os': 'linux',
            'config': {'Labels': {'org.opencontainers.image.version': '24.04'}, 'Env': []},
            'rootfs': {'type': 'layers', 'diff_ids': []}}, layers=[], blobs={},
            metadata={'etc/passwd': b'root:x:0:0::/root:/bin/sh\n', 'etc/group': b'root:x:0:\n'})
        package = payload(name for name in ('usr/bin/xvfb-run', 'usr/bin/Xvfb', 'usr/bin/wine-stable',
                                           'usr/bin/wineboot-stable') if name not in missing)
        steam = payload(['steamcmd.sh'], compressed=True)
        output = root / 'worker.tar'
        with patch.object(assembly, 'Base', return_value=base), \
                patch.object(assembly, 'package_closure', return_value=[{'Filename': 'fixture.deb', 'SHA256': 'a' * 64}]) as closure, \
                patch.object(assembly, 'deb_data', side_effect=lambda data: data), \
                patch.object(assembly, 'fetch', side_effect=lambda url, *args: steam if 'steamcmd' in url else package):
            assembly.assemble('worker', source, output)
        self.assertTrue({'wine', 'wine64', 'winbind', 'xauth', 'xvfb', 'fonts-liberation'}
                        .issubset(closure.call_args.args[1]))
        self.assertEqual(closure.call_args.args[2], ['amd64', 'i386'])
        return output

    def test_shared_artifact_contains_wine_and_worker_dispatch_payload(self):
        """The actual serialization path produces one worker with Windows runtime support."""
        with tempfile.TemporaryDirectory() as temporary:
            output = self.assemble(Path(temporary))
            assembly.verify('worker', output)
            with tarfile.open(output) as archive:
                index = json.load(archive.extractfile('index.json'))
                manifest = json.load(archive.extractfile('blobs/sha256/' + index['manifests'][0]['digest'][7:]))
                image = json.load(archive.extractfile('blobs/sha256/' + manifest['config']['digest'][7:]))
            self.assertEqual(image['config']['Entrypoint'], ['/usr/bin/tini', '-g', '--', '/app/docker-entrypoint.sh'])
            self.assertIn('UTF_ASKA_XVFB_RUN_PATH=/usr/bin/xvfb-run', image['config']['Env'])

    def test_missing_xvfb_support_is_rejected(self):
        """Consolidation must not silently turn the shared image into the former Linux-only image."""
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, 'shared worker Wine/Xvfb command contract missing'):
                self.assemble(Path(temporary), missing=('usr/bin/xvfb-run',))

    def test_separate_aska_components_are_rejected_before_fetch(self):
        """Retired component names cannot create an accidental fourth artifact."""
        with patch.object(assembly, 'Base') as base:
            for name in ('aska', 'worker-aska'):
                with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'unknown component'):
                    assembly.assemble(name, 'unused', 'unused.tar')
            base.assert_not_called()

    def test_dockerfile_uses_the_same_runtime_support(self):
        """The development build also supplies the libraries required by the ASKA executor."""
        root = Path(__file__).resolve().parent.parent
        dockerfile = (root / 'sources/CloudWorker/Dockerfile').read_text()
        for name in ('wine', 'wine64', 'winbind', 'xauth', 'xvfb', 'fonts-liberation'):
            self.assertIn('        ' + name + ' \\', dockerfile)
        self.assertIn('UTF_ASKA_XVFB_RUN_PATH=/usr/bin/xvfb-run', dockerfile)
        self.assertFalse((root / 'sources/CloudWorker/Dockerfile.Aska').exists())


if __name__ == '__main__':
    unittest.main()
