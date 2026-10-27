"""Verify the pinned Node distribution's integrity and extraction boundary."""
import hashlib
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import MagicMock, patch


spec = importlib.util.spec_from_file_location('assembly', Path(__file__).parent / 'oci-layout/assemble.py')
assembly = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assembly)


def archive(extra=()):
    """Create a minimal distribution with the real layout and npm link convention."""
    buffer = io.BytesIO()
    entries = [('bin/node', b'node', None), ('lib/node_modules/npm/bin/npm-cli.js', b'npm', None),
               ('lib/node_modules/npm/bin/npx-cli.js', b'npx', None),
               ('bin/npm', None, '../lib/node_modules/npm/bin/npm-cli.js'),
               ('bin/npx', None, '../lib/node_modules/npm/bin/npx-cli.js'),
               ('include/node/unused.h', b'unused', None), *extra]
    with tarfile.open(fileobj=buffer, mode='w:xz') as output:
        for name, data, link in entries:
            member = tarfile.TarInfo(assembly.NODE_ARCHIVE + '/' + name)
            member.mode = 0o755
            if link is not None:
                member.type, member.linkname = tarfile.SYMTYPE, link
            else:
                member.size = len(data)
            output.addfile(member, None if data is None else io.BytesIO(data))
    return buffer.getvalue()


class NodeDistributionTests(unittest.TestCase):
    def stage(self, data, prefix, digest=None):
        """Exercise the real size/hash checks with an inert HTTPS response."""
        response = MagicMock()
        response.__enter__.return_value = response
        response.read.return_value = data
        opener = MagicMock()
        opener.open.return_value = response
        with patch.object(assembly, 'NODE_ARCHIVE_SIZE', len(data)), \
                patch.object(assembly, 'NODE_ARCHIVE_SHA256', digest or hashlib.sha256(data).hexdigest()), \
                patch.object(assembly.urllib.request, 'build_opener', return_value=opener), \
                patch.object(assembly, 'Base', side_effect=AssertionError('No Node container download')):
            assembly.stage('node', prefix)
        self.assertEqual(opener.open.call_args.args[0].full_url,
                         'https://nodejs.org/dist/v24.11.1/node-v24.11.1-linux-x64.tar.xz')
        self.assertEqual(response.read.call_args.args[0], len(data) + 1)

    def test_extracts_only_tools_with_relocatable_npm_wrappers(self):
        with tempfile.TemporaryDirectory() as temporary:
            prefix = Path(temporary) / 'node'
            self.stage(archive(), prefix)
            self.assertEqual((prefix / 'bin/node').read_bytes(), b'node')
            self.assertFalse((prefix / 'include').exists())
            for name in ('npm', 'npx'):
                self.assertFalse((prefix / 'bin' / name).is_symlink())
                self.assertIn(f'/{name}-cli.js', (prefix / 'bin' / name).read_text())

    def test_corrupt_archive_fails_before_creating_toolchain(self):
        with tempfile.TemporaryDirectory() as temporary:
            prefix = Path(temporary) / 'node'
            with self.assertRaisesRegex(ValueError, 'content digest mismatch'):
                self.stage(archive(), prefix, '0' * 64)
            self.assertFalse(prefix.exists())

    def test_traversal_fails_without_partial_toolchain(self):
        with tempfile.TemporaryDirectory() as temporary:
            prefix = Path(temporary) / 'node'
            with self.assertRaisesRegex(ValueError, 'unsafe archive path'):
                self.stage(archive([('../escape', b'unsafe', None)]), prefix)
            self.assertEqual(list(Path(temporary).iterdir()), [])

    def test_selected_symlink_escape_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            prefix = Path(temporary) / 'node'
            with self.assertRaisesRegex(ValueError, 'unsupported Node archive entry'):
                self.stage(archive([('lib/node_modules/npm/escape', None, '../../../../escape')]), prefix)
            self.assertFalse(prefix.exists())

    def test_duplicate_selected_entry_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            prefix = Path(temporary) / 'node'
            with self.assertRaisesRegex(ValueError, 'duplicate Node archive entry'):
                self.stage(archive([('bin/node', b'replacement', None)]), prefix)
            self.assertFalse(prefix.exists())


if __name__ == '__main__':
    unittest.main()
