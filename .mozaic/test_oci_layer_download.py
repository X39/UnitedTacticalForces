"""Verify public layer failure diagnostics without network or image execution."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import MagicMock, patch
import urllib.error


specification = importlib.util.spec_from_file_location('assembly', Path(__file__).parent / 'oci-layout/assemble.py')
assembly = importlib.util.module_from_spec(specification)
specification.loader.exec_module(assembly)


class LayerDownloadTests(unittest.TestCase):
    """Preserve bounds and secret scrubbing while identifying a failed pinned layer."""

    def setUp(self):
        """Use an inert authenticated request and one public pinned descriptor."""
        self.base = object.__new__(assembly.Base)
        self.base.url = 'https://registry-1.docker.io/v2/library/node/'
        self.base.headers = {'Authorization': 'Bearer credential-sentinel'}
        self.descriptor = {'digest': 'sha256:' + 'a' * 64, 'size': 131072}

    def response(self, reads):
        """Supply a response whose URL contains deliberately sensitive query text."""
        response = MagicMock()
        response.__enter__.return_value = response
        response.geturl.return_value = 'https://production.cloudfront.docker.com/layer?signed=query-sentinel'
        response.read.side_effect = reads
        return response

    def test_open_timeout_reports_public_descriptor_and_no_unobserved_response(self):
        """Opening cannot distinguish a connect timeout from a header timeout."""
        opener = MagicMock()
        opener.open.side_effect = urllib.error.URLError(TimeoutError('credential-sentinel query-sentinel'))
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(assembly.urllib.request, 'build_opener', return_value=opener):
            path = Path(directory) / 'layer'
            with self.assertRaises(ValueError) as caught:
                self.base.download_layer(self.descriptor, path)
            self.assertFalse(path.exists())
        message = str(caught.exception)
        self.assertIn('category=URLError/TimeoutError', message)
        self.assertIn('digest=' + self.descriptor['digest'], message)
        self.assertIn('expected_bytes=131072; written_bytes=0; stage=connect/headers', message)
        self.assertNotIn('response_host=', message)
        for forbidden in ('credential-sentinel', 'query-sentinel', 'https://', '/v2/', 'Authorization'):
            self.assertNotIn(forbidden, message)
        self.assertEqual(opener.open.call_args.kwargs['timeout'], 300)

    def test_body_timeout_reports_written_bytes_and_observed_hostname_only(self):
        """A later stalled read retains the byte count of the actual partial file."""
        response = self.response([b'x' * 65536, TimeoutError('query-sentinel')])
        opener = MagicMock()
        opener.open.return_value = response
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(assembly.urllib.request, 'build_opener', return_value=opener):
            path = Path(directory) / 'layer'
            with self.assertRaises(ValueError) as caught:
                self.base.download_layer(self.descriptor, path)
            self.assertEqual(path.stat().st_size, 65536)
        message = str(caught.exception)
        self.assertIn('category=TimeoutError/TimeoutError', message)
        self.assertIn('written_bytes=65536; stage=body', message)
        self.assertIn('response_host=production.cloudfront.docker.com', message)
        for forbidden in ('query-sentinel', '?', 'credential-sentinel', 'https://'):
            self.assertNotIn(forbidden, message)
        self.assertEqual(response.read.call_args.args, (65536,))

    def test_success_preserves_exact_bytes(self):
        """Diagnostics do not change the streamed payload."""
        response = self.response([b'x' * 65536, b'y' * 65536, b''])
        opener = MagicMock()
        opener.open.return_value = response
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(assembly.urllib.request, 'build_opener', return_value=opener):
            path = Path(directory) / 'layer'
            self.base.download_layer(self.descriptor, path)
            self.assertEqual(path.read_bytes(), b'x' * 65536 + b'y' * 65536)

    def test_descriptor_limit_failure_is_not_reclassified(self):
        """The existing size assertion fails before oversized content is written."""
        response = self.response([b'x' * 65536])
        opener = MagicMock()
        opener.open.return_value = response
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(assembly.urllib.request, 'build_opener', return_value=opener):
            path = Path(directory) / 'layer'
            with self.assertRaisesRegex(ValueError, '^blob exceeds descriptor limit$'):
                self.base.download_layer(dict(self.descriptor, size=1), path)
            self.assertEqual(path.stat().st_size, 0)

    def test_unsafe_redirect_failure_retains_existing_scrubbed_category(self):
        """Only the rejected hostname accompanies public descriptor metadata."""
        error = ValueError('query-sentinel')
        error.download_host = 'unapproved.example'
        opener = MagicMock()
        opener.open.side_effect = error
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(assembly.urllib.request, 'build_opener', return_value=opener):
            with self.assertRaises(ValueError) as caught:
                self.base.download_layer(self.descriptor, Path(directory) / 'layer')
        self.assertIn('host=unapproved.example', str(caught.exception))
        self.assertIn('category=ValueError/ValueError', str(caught.exception))
        self.assertNotIn('query-sentinel', str(caught.exception))


if __name__ == '__main__':
    unittest.main()
