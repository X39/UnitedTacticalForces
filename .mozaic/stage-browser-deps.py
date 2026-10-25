#!/usr/bin/env python3
"""Stream verified Jammy browser content into scratch, preserving host glibc."""
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import shutil
import sys
import tarfile
import tempfile
import urllib.request

PINS = {
    '1.61.0': '19298da4a542f9823673f35f64690518abae7cb07ec925fcf4383b89e2766341',
    '1.62.1': '75d2d72c89da49d06aa7f6e623987392019aebbc52c09fd8cd6e2c94c7c7d71d',
}
BASE = 'https://mcr.microsoft.com/v2/playwright/'
GLIBC = re.compile(r'^(ld-linux|ld-[0-9]|libstdc\+\+[-.]|libgcc_s[-.]|lib(c|m|mvec|dl|pthread|rt|resolv|util|anl|'
                   r'BrokenLocale|thread_db|nss_[^.]+)([-.]|$))')


def canonical(name):
    """Normalize the image's merged-/usr paths inside scratch."""
    name = assembly.checked_path(name)
    return 'usr/' + name if name.startswith('lib/') else name


def selected(name):
    """Select browser executables, dependency libraries and supporting data."""
    filename = PurePosixPath(name).name
    return not GLIBC.match(filename) and (
        name.startswith(('ms-playwright/', 'usr/share/fonts/', 'etc/fonts/',
                         'usr/share/fontconfig/', 'usr/share/glib-2.0/schemas/',
                         'usr/share/mime/', 'usr/share/X11/xkb/'))
        or (name.startswith(('usr/lib/x86_64-linux-gnu/', 'usr/libexec/'))
            and ('.so' in filename or filename == 'gst-plugin-scanner')))


def destination(root, name):
    """Reject writes through links or outside the private prefix."""
    result = root / name
    assembly.require(result.is_relative_to(root) and not any(
        parent.is_symlink() for parent in result.parents if parent != root.parent),
        'browser symlink ancestry')
    return result


def remove(path):
    """Remove only content already extracted in the private prefix."""
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


def extract(archive, member, root):
    """Apply selected overlay entries, relocating links into scratch."""
    name = canonical(member.name)
    path = PurePosixPath(name)
    if path.name.startswith('.wh.'):
        if path.name == '.wh..wh..opq':
            parent = destination(root, str(path.parent))
            if parent.is_dir():
                for child in parent.iterdir():
                    remove(child)
        else:
            deleted = str(path.parent / path.name[4:])
            if selected(deleted):
                remove(destination(root, deleted))
        return
    if not selected(name) or member.isdir():
        return
    target = destination(root, name)
    target.parent.mkdir(parents=True, exist_ok=True)
    remove(target)
    if member.isfile():
        assembly.require(member.size <= 1024**3, 'browser file exceeds limit')
        with archive.extractfile(member) as source, target.open('wb') as output:
            shutil.copyfileobj(source, output, 1024 * 1024)
        target.chmod(member.mode & 0o777)
    elif member.issym() or member.islnk():
        linked = canonical(member.linkname.lstrip('/')) if (
            member.islnk() or member.linkname.startswith('/')) else canonical(
                os.path.normpath(str(path.parent / member.linkname)))
        if selected(linked):
            target.symlink_to(os.path.relpath(root / linked, target.parent))
    else:
        raise ValueError('unsupported selected browser entry')


class HashedReader:
    """Bound and hash uncompressed bytes while tar consumes the layer."""

    def __init__(self, source):
        self.source, self.digest, self.count = source, hashlib.sha256(), 0

    def read(self, size):
        data = self.source.read(size)
        self.count += len(data)
        assembly.require(self.count <= 5 * 1024**3, 'browser expansion exceeds limit')
        self.digest.update(data)
        return data


def stage(version, root):
    """Verify immutable metadata and stream digest-checked layers to disk."""
    assembly.require(version in PINS and not root.exists(), 'invalid browser stage request')
    headers = {'Accept': 'application/vnd.oci.image.manifest.v1+json, '
               'application/vnd.docker.distribution.manifest.v2+json'}
    manifest = json.loads(assembly.fetch(BASE + 'manifests/sha256:' + PINS[version],
                                        PINS[version], 1024**2, headers))
    descriptor = manifest['config']
    config = json.loads(assembly.fetch(BASE + 'blobs/' + descriptor['digest'],
                                      descriptor['digest'], 1024**2))
    assembly.require(config['os'] == 'linux' and config['architecture'] == 'amd64'
                     and config['config']['Labels']['org.opencontainers.image.version'] == '22.04',
                     'browser dependencies must be linux/amd64 Jammy')
    layers, diff_ids = manifest['layers'], config['rootfs']['diff_ids']
    assembly.require(0 < len(layers) == len(diff_ids) <= 16, 'invalid browser layer count')
    root.mkdir(parents=True)
    with tempfile.TemporaryDirectory(dir=root.parent) as temporary:
        for index, descriptor in enumerate(layers):
            assembly.require(0 < descriptor['size'] <= 1024**3
                             and descriptor['mediaType'].endswith(('gzip', '.gzip')),
                             'unsupported browser layer')
            layer, digest, count = Path(temporary) / 'layer.gz', hashlib.sha256(), 0
            request = urllib.request.Request(BASE + 'blobs/' + descriptor['digest'])
            with urllib.request.build_opener(assembly.Redirect()).open(request, timeout=300) as source:
                with layer.open('wb') as output:
                    while block := source.read(1024 * 1024):
                        count += len(block)
                        assembly.require(count <= descriptor['size'], 'browser layer exceeds size')
                        digest.update(block)
                        output.write(block)
            assembly.require(count == descriptor['size'] and 'sha256:' + digest.hexdigest()
                             == descriptor['digest'], 'browser layer digest mismatch')
            with gzip.open(layer, 'rb') as source:
                reader = HashedReader(source)
                with tarfile.open(fileobj=reader, mode='r|') as archive:
                    for member in archive:
                        extract(archive, member, root)
                while reader.read(1024 * 1024):
                    pass
                assembly.require('sha256:' + reader.digest.hexdigest() == diff_ids[index],
                                 'browser diff ID mismatch')
            layer.unlink()
    configure(root)


def configure(root):
    """Expose staged libraries and data while keeping the host loader and libc."""
    libraries = sorted({str(path.parent) for path in (root / 'usr/lib').rglob('*')
                        if '.so' in path.name and not GLIBC.match(path.name)})
    assembly.require(libraries and (root / 'ms-playwright').is_dir(), 'browser content missing')
    fonts = root / 'fonts.conf'
    fonts.write_text('<?xml version="1.0"?><fontconfig><dir>'
                     + str(root / 'usr/share/fonts') + '</dir><cachedir>'
                     + str(root / 'font-cache') + '</cachedir></fontconfig>\n')
    variables = {
        'UTF_DOTNET_PLAYWRIGHT_BROWSERS_PATH': str(root / 'ms-playwright'),
        'UTF_REACT_PLAYWRIGHT_BROWSERS_PATH': str(root / 'ms-playwright'),
        'FONTCONFIG_FILE': str(fonts), 'FONTCONFIG_PATH': str(root / 'etc/fonts'),
        'GSETTINGS_SCHEMA_DIR': str(root / 'usr/share/glib-2.0/schemas'),
        'GST_PLUGIN_PATH': str(root / 'usr/lib/x86_64-linux-gnu/gstreamer-1.0'),
        'GST_PLUGIN_SYSTEM_PATH': str(root / 'usr/lib/x86_64-linux-gnu/gstreamer-1.0'),
        'GST_REGISTRY': str(root / 'gstreamer-registry.bin'),
        'GIO_EXTRA_MODULES': str(root / 'usr/lib/x86_64-linux-gnu/gio/modules'),
        'XKB_CONFIG_ROOT': str(root / 'usr/share/X11/xkb'),
    }
    scanners = list(root.rglob('gst-plugin-scanner'))
    if scanners:
        assembly.require(len(scanners) == 1, 'ambiguous GStreamer scanner')
        variables['GST_PLUGIN_SCANNER'] = str(scanners[0])
    environment = ''.join('export ' + key + '=' + shlex.quote(value) + '\n'
                          for key, value in variables.items())
    environment += 'export LD_LIBRARY_PATH=' + shlex.quote(':'.join(libraries)) \
                   + '${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}\n'
    (root / 'environment.sh').write_text(environment)


if __name__ == '__main__':
    specification = importlib.util.spec_from_file_location(
        'utf_assembly', Path(__file__).parent / 'oci-layout' / 'assemble.py')
    assembly = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(assembly)
    try:
        assembly.require(len(sys.argv) == 3, 'usage: stage-browser-deps.py VERSION PREFIX')
        prefix = Path(sys.argv[2]).absolute()
        assembly.require(prefix.is_relative_to(Path('/workspace')) and prefix != Path('/workspace'),
                         'browser prefix must be inside workspace')
        stage(sys.argv[1], prefix)
    except Exception as error:
        # Network errors may embed signed redirect URLs; keep them out of logs.
        print('Browser dependency staging failed: ' + (str(error) if isinstance(error, ValueError)
              else 'bounded content operation failed'), file=sys.stderr)
        sys.exit(1)
