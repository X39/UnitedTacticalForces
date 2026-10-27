"""Verify the API image's fail-closed hosting default with inert OCI payloads."""
import importlib.util
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


class ApiImagePauseTests(unittest.TestCase):
    """Exercise real OCI serialization without a runtime, network, or application build."""

    def test_api_overrides_inherited_unpaused_base_configuration(self):
        """The serialized production artifact always starts with hosting execution paused."""
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = root / '.publish/api'
            payload.mkdir(parents=True)
            (payload / 'X39.UnitedTacticalForces.Api.dll').write_bytes(b'inert fixture')
            base = SimpleNamespace(config={'architecture': 'amd64', 'os': 'linux',
                                           'config': {'Env': ['Hosting__ControlPlane__Paused=false'],
                                                       'Labels': {'org.opencontainers.image.version': '24.04'}},
                                           'rootfs': {'type': 'layers', 'diff_ids': []}}, layers=[], blobs={})
            output = root / 'api.tar'
            with patch.object(assembly, 'Base', return_value=base), \
                    patch.object(assembly, 'package_closure', return_value=[]):
                assembly.assemble('api', root, output)
            with tarfile.open(output) as archive:
                index = json.load(archive.extractfile('index.json'))
                manifest = json.load(archive.extractfile('blobs/sha256/' + index['manifests'][0]['digest'][7:]))
                config = json.load(archive.extractfile('blobs/sha256/' + manifest['config']['digest'][7:]))
            env = dict(value.split('=', 1) for value in config['config']['Env'])
            self.assertEqual(env['Hosting__ControlPlane__Paused'], 'true')
            self.assertEqual(config['config']['Entrypoint'], ['dotnet', 'X39.UnitedTacticalForces.Api.dll'])

    def test_docker_recipe_has_the_same_explicit_pause_default(self):
        """Both supported API image packaging paths declare the same operator gate."""
        recipe = (Path(__file__).parents[1] / 'Dockerfile').read_text()
        api_stage = recipe.split(' AS api\n', 1)[1].split('\nFROM ', 1)[0]
        self.assertIn('Hosting__ControlPlane__Paused=true', api_stage)


if __name__ == '__main__':
    unittest.main()
