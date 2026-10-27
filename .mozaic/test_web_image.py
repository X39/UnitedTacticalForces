"""Verify Blazor configuration packaging without a build, runtime, or network."""
import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('assembly', Path(__file__).parent / 'oci-layout/assemble.py')
assembly = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assembly)
REPOSITORY = Path(__file__).parents[1]


class WebImageConfigurationTests(unittest.TestCase):
    """Keep boot-declared production JSON while preserving runtime base settings."""

    def package(self, production):
        """Serialize inert web assets through the real assembler and verifier."""
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = root / '.publish/web/wwwroot'
            payload.mkdir(parents=True)
            (payload / 'index.html').write_text('<html></html>')
            (payload / 'appsettings.json').write_text('{"Api":{"BaseUrl":"https://build.invalid/"}}')
            if production is not None:
                (payload / 'appsettings.Production.json').write_text(production)
            for name in ('appsettings.json.gz', 'appsettings.Production.json.br'):
                (payload / name).write_bytes(b'stale compressed settings')
            for name in ('dist', 'dist-root'):
                assets = root / 'sources/ReactApp' / name
                assets.mkdir(parents=True)
                (assets / 'index.html').write_text('<html></html>')
            (root / 'deploy').mkdir()
            for name in ('nginx.conf', 'webapp.appsettings.json'):
                (root / 'deploy' / name).write_bytes((REPOSITORY / 'deploy' / name).read_bytes())
            base = SimpleNamespace(config={'architecture': 'amd64', 'os': 'linux', 'config': {},
                                           'rootfs': {'type': 'layers', 'diff_ids': []}}, layers=[], blobs={})
            with patch.object(assembly, 'Base', return_value=base), \
                    patch.object(assembly, 'fetch', side_effect=AssertionError('network forbidden')):
                assembly.assemble('web', root, root / 'web.tar')

    def test_production_override_preserves_runtime_configuration(self):
        """The source override is empty and survives serialized image verification."""
        production = (REPOSITORY / 'sources/WebApp/wwwroot/appsettings.Production.json').read_text()
        self.assertEqual(json.loads(production), {})
        self.package(production)

    def test_missing_production_configuration_is_rejected(self):
        """A missing boot-declared JSON asset cannot pass image verification."""
        with self.assertRaisesRegex(ValueError, 'Blazor production settings missing'):
            self.package(None)

    def test_stale_production_override_is_rejected(self):
        """A stale publish cannot override the runtime-mounted API URL."""
        with self.assertRaisesRegex(ValueError, 'preserve runtime base configuration'):
            self.package('{"Api":{"BaseUrl":"/api/"}}')

    def test_docker_preserves_production_json_and_removes_sidecars(self):
        """Docker packaging follows the same configuration contract as OCI assembly."""
        recipe = (REPOSITORY / 'Dockerfile').read_text().split(' AS web\n', 1)[1]
        self.assertIn('COPY --from=web-publish /out/web/wwwroot/ /usr/share/nginx/html/', recipe)
        self.assertIn('COPY deploy/webapp.appsettings.json /usr/share/nginx/html/appsettings.json', recipe)
        cleanup = recipe.split('RUN rm -f', 1)[1].split('\n\n', 1)[0]
        self.assertNotIn('/appsettings.Production.json', cleanup)
        self.assertIn('/appsettings*.json.br', cleanup)
        self.assertIn('/appsettings*.json.gz', cleanup)


if __name__ == '__main__':
    unittest.main()
