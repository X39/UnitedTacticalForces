#!/usr/bin/env python3
"""Require executed, passing TRX results for every named release test assembly."""

from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def verify(directory, assemblies):
    """Reject missing, empty, skipped or failed assembly results before packaging."""
    counts = {assembly.casefold(): 0 for assembly in assemblies}
    for path in Path(directory).glob('*.trx'):
        root = ET.parse(path).getroot()
        namespace = {'t': root.tag.removesuffix('TestRun').strip('{}')}
        definitions = {}
        for definition in root.findall('t:TestDefinitions/t:UnitTest', namespace):
            method = definition.find('t:TestMethod', namespace)
            if method is None:
                continue
            code_base = method.get('codeBase', definition.get('storage', ''))
            assembly = code_base.replace('\\', '/').rsplit('/', 1)[-1].removesuffix('.dll').casefold()
            if assembly in counts:
                definitions[definition.get('id')] = assembly
        for result in root.findall('t:Results/t:UnitTestResult', namespace):
            assembly = definitions.get(result.get('testId'))
            if assembly is None:
                continue
            if result.get('outcome') != 'Passed':
                raise ValueError('Required test did not pass in ' + assembly)
            counts[assembly] += 1
    missing = [assembly for assembly, count in counts.items() if count == 0]
    if missing:
        raise ValueError('No passing tests executed for: ' + ', '.join(missing))
    for assembly, count in counts.items():
        print(f'Executed required tests: {assembly}={count}')


if __name__ == '__main__':
    try:
        if len(sys.argv) < 3:
            raise ValueError('Usage: verify-test-results.py DIRECTORY ASSEMBLY [ASSEMBLY ...]')
        verify(sys.argv[1], sys.argv[2:])
    except (ValueError, OSError, ET.ParseError) as error:
        print('Release test evidence failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
