"""Check selective browser extraction without downloading or executing image content."""
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest


def load(name, relative):
    """Load a sibling helper without invoking its command-line entry point."""
    specification = importlib.util.spec_from_file_location(name, Path(__file__).parent / relative)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


browser = load('browser', 'stage-browser-deps.py')
browser.assembly = load('assembly', 'oci-layout/assemble.py')


class BrowserExtractionTests(unittest.TestCase):
    """Keep selected writes confined while ignoring unrelated image entries."""

    def test_unselected_systemd_name_is_ignored(self):
        """The pinned Ubuntu layer contains a literal escaped systemd unit name."""
        member = tarfile.TarInfo(r'usr/lib/systemd/system/system-systemd\x2dcryptsetup.slice')
        browser.extract(None, member, Path('unused'))

    def test_selected_unsafe_paths_are_rejected(self):
        """Selected traversal and backslash paths must fail before any file access."""
        for name in ('ms-playwright/../escape', r'ms-playwright/unsafe\name'):
            with self.subTest(name=name), self.assertRaises(ValueError):
                browser.extract(None, tarfile.TarInfo(name), Path('unused'))

    def test_selected_link_escape_is_rejected(self):
        """A browser symlink cannot resolve outside the extraction prefix."""
        with tempfile.TemporaryDirectory() as directory:
            member = tarfile.TarInfo('ms-playwright/link')
            member.type, member.linkname = tarfile.SYMTYPE, '../../escape'
            with self.assertRaises(ValueError):
                browser.extract(None, member, Path(directory))

    def test_selected_file_is_extracted(self):
        """A regular browser entry still writes only its expected content."""
        class Archive:
            """Supply inert bytes for a selected regular archive entry."""

            def extractfile(self, member):
                """Return the selected test entry's bytes."""
                return io.BytesIO(b'browser')

        with tempfile.TemporaryDirectory() as directory:
            member = tarfile.TarInfo('ms-playwright/browser')
            member.size, member.mode = 7, 0o755
            browser.extract(Archive(), member, Path(directory))
            self.assertEqual((Path(directory) / member.name).read_bytes(), b'browser')


if __name__ == '__main__':
    unittest.main()
