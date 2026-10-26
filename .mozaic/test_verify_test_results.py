"""Regression checks for fail-closed release test evidence validation."""

from contextlib import redirect_stdout
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    'release_results', Path(__file__).with_name('verify-test-results.py'))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ReleaseResultTests(unittest.TestCase):
    """Exercise empty, missing, skipped, failed and valid VSTest evidence."""

    def write_report(self, directory, assembly='Ui.Tests', outcome='Passed', results=True):
        """Write a minimal namespaced report using VSTest's assembly and result fields."""
        result = ('<Results><UnitTestResult testId="case-1" outcome="' + outcome
                  + '" /></Results>') if results else '<Results />'
        Path(directory, 'report.trx').write_text(
            '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
            '<TestDefinitions><UnitTest id="case-1"><TestMethod codeBase="C:\\tests\\'
            + assembly + '.dll" /></UnitTest></TestDefinitions>' + result + '</TestRun>',
            encoding='utf-8')

    def test_empty_directory_fails(self):
        """A successful test command without reports must not authorize publication."""
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                MODULE.verify(directory, ['Ui.Tests'])

    def test_zero_executed_tests_fails(self):
        """Test discovery without execution is insufficient evidence."""
        with tempfile.TemporaryDirectory() as directory:
            self.write_report(directory, results=False)
            with self.assertRaises(ValueError):
                MODULE.verify(directory, ['Ui.Tests'])

    def test_another_assembly_does_not_satisfy_required_suite(self):
        """Other passing projects cannot hide an empty browser suite."""
        with tempfile.TemporaryDirectory() as directory:
            self.write_report(directory, assembly='Api.Tests')
            with self.assertRaises(ValueError):
                MODULE.verify(directory, ['Ui.Tests', 'Api.Tests'])

    def test_failed_and_skipped_tests_fail(self):
        """Neither failed tests nor skipped tests count as release acceptance."""
        for outcome in ['Failed', 'NotExecuted']:
            with self.subTest(outcome=outcome), tempfile.TemporaryDirectory() as directory:
                self.write_report(directory, outcome=outcome)
                with self.assertRaises(ValueError):
                    MODULE.verify(directory, ['Ui.Tests'])

    def test_executed_passing_tests_succeed(self):
        """The assembly name is case insensitive across Windows and Linux reports."""
        with tempfile.TemporaryDirectory() as directory:
            self.write_report(directory)
            with redirect_stdout(io.StringIO()) as output:
                MODULE.verify(directory, ['ui.tests'])
            self.assertIn('ui.tests=1', output.getvalue())


if __name__ == '__main__':
    unittest.main()
