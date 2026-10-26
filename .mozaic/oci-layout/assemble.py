#!/usr/bin/env python3
"""Deterministic, unprivileged OCI assembly; no engine or target execution."""
import copy
import ctypes
import functools
import gzip
import hashlib
import io
import json
import lzma
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import sys
import tarfile
import tempfile
import urllib.request

MANIFEST = 'application/vnd.oci.image.manifest.v1+json'
INDEX = 'application/vnd.oci.image.index.v1+json'
CONFIG = 'application/vnd.oci.image.config.v1+json'
LAYER = 'application/vnd.oci.image.layer.v1.tar'
PINS = {
    'node': ('docker.io/library/node', 'fe306151aeafaf7c94e03dd3aff2435c91621f71a58bba3a09579c89d3ba730e'),
    '201': ('mcr.microsoft.com/dotnet/sdk', 'c804b88aaca8d6b76f65998427adec3101dcbbf2e52e705e2509eb81263aab0c'),
    '302': ('mcr.microsoft.com/dotnet/sdk', '3dae2f7699441af56216ff64d5c9b6dfce7cd7dc7f4f71d353d29662b10a384f'),
    'api': ('mcr.microsoft.com/dotnet/aspnet', 'f879b8a9cb3f6694b8b0c9fa530eb0c39d8920db09c05a7edbd6c4c6c104ae1b'),
    'web': ('docker.io/library/nginx', '3bcf852aed06467cf075c6105892e4d5a6ebbbafa0ce22d35062db9e90ddef4c'),
    'worker': ('mcr.microsoft.com/dotnet/runtime', 'ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe'),
}
SNAPSHOT = 'https://snapshot.ubuntu.com/ubuntu/20261024T000000Z/'
RELEASES = {
    'noble': '26758a13cecfaff9ff274d31ea9a4633674a999bd130c09f6ecd66a4b8071184',
    'noble-updates': 'b0ce6f04d05c105454c4f3b6747230e1486618f4f167a616b54b643d9a58ff06',
    'noble-security': '08ee8b6333cd5d57d555a77a58b911165742adc54240d8f2ed757fbedbc91f4a',
}
HOSTS = {'auth.docker.io', 'registry-1.docker.io', 'production.cloudfront.docker.com',
         'mcr.microsoft.com', 'mcrprod.azureedge.net', 'westeurope.data.mcr.microsoft.com', 'snapshot.ubuntu.com',
         'steamcdn-a.akamaihd.net'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


class Redirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, url):
        try:
            check_url(url)
        except ValueError as error:
            error.download_host = urllib.parse.urlsplit(url).hostname
            raise
        result = super().redirect_request(request, fp, code, message, headers, url)
        if urllib.parse.urlsplit(url).hostname != urllib.parse.urlsplit(request.full_url).hostname:
            result.remove_header('Authorization')
        return result


def check_url(url):
    parsed = urllib.parse.urlsplit(url)
    require(parsed.scheme == 'https' and parsed.hostname in HOSTS
            and not parsed.username and not parsed.password and parsed.port in (None, 443),
            'unapproved download destination')


def fetch_failure(url, error, kind='content'):
    """Describe a public transport failure without exposing URLs, headers or response bodies."""
    host = getattr(error, 'download_host', None) or urllib.parse.urlsplit(getattr(error, 'url', url)).hostname
    status = getattr(error, 'code', None)
    status = status if isinstance(status, int) else 'none'
    reason = getattr(error, 'reason', error)
    return ValueError(f'bounded public {kind} fetch failed: host={host}; status={status}; '
                      f'category={type(error).__name__}/{type(reason).__name__}')


def fetch(url, expected=None, maximum=1024 * 1024 * 1024, headers=None):
    check_url(url)
    try:
        with urllib.request.build_opener(Redirect()).open(
                urllib.request.Request(url, headers=headers or {}), timeout=300) as response:
            data = response.read(maximum + 1)
    except Exception as error:
        raise fetch_failure(url, error) from None
    require(len(data) <= maximum, 'download exceeds limit')
    if expected:
        require(sha(data) == expected.removeprefix('sha256:'), 'content digest mismatch')
    return data


def checked_path(name):
    path = PurePosixPath(name)
    require(not path.is_absolute() and '..' not in path.parts and '\\' not in name,
            'unsafe archive path')
    return str(path)


class Base:
    def __init__(self, key, work):
        repository, pin = PINS[key]
        host, repository = repository.split('/', 1)
        headers = {'Accept': ', '.join([MANIFEST, INDEX,
            'application/vnd.docker.distribution.manifest.v2+json',
            'application/vnd.docker.distribution.manifest.list.v2+json'])}
        if host == 'docker.io':
            token = json.loads(fetch('https://auth.docker.io/token?service=registry.docker.io&scope=repository:'
                                     + repository + ':pull', maximum=32768))['token']
            headers['Authorization'] = 'Bearer ' + token
            host = 'registry-1.docker.io'
        self.url = f'https://{host}/v2/{repository}/'
        self.headers = headers
        document = json.loads(fetch(self.url + 'manifests/sha256:' + pin, pin, 1024 * 1024, headers))
        if 'manifests' in document:
            choices = [entry for entry in document['manifests']
                       if entry.get('platform', {}).get('os') == 'linux'
                       and entry['platform'].get('architecture') == 'amd64'
                       and not entry['platform'].get('variant')]
            require(len(choices) == 1, 'base architecture selection is ambiguous')
            descriptor = choices[0]
            document = json.loads(self.blob(descriptor, 'manifests'))
        require(document.get('schemaVersion') == 2, 'invalid base manifest')
        self.config = json.loads(self.blob(document['config']))
        require(self.config['os'] == 'linux' and self.config['architecture'] == 'amd64', 'wrong base platform')
        self.layers = document['layers']
        require(0 < len(self.layers) <= 32, 'base layer count exceeded')
        self.blobs = {}
        self.metadata = {}
        for index, descriptor in enumerate(self.layers):
            path = work / descriptor['digest'].split(':')[1]
            check_url(self.url + 'blobs/' + descriptor['digest'])
            try:
                with urllib.request.build_opener(Redirect()).open(urllib.request.Request(
                        self.url + 'blobs/' + descriptor['digest'], headers=self.headers), timeout=300) as response, path.open('xb') as target:
                    total = 0
                    while data := response.read(64 * 1024):
                        total += len(data)
                        require(total <= descriptor['size'] <= 1024**3, 'blob exceeds descriptor limit')
                        target.write(data)
            except ValueError as error:
                if getattr(error, 'download_host', None):
                    raise fetch_failure(self.url, error, 'layer') from None
                raise
            except Exception as error:
                raise fetch_failure(self.url, error, 'layer') from None
            require(path.stat().st_size == descriptor['size'] and 'sha256:' + file_sha(path) == descriptor['digest'], 'base blob identity mismatch')
            self.blobs[descriptor['digest']] = path
            digest, expanded = hashlib.sha256(), 0
            with (gzip.open(path, 'rb') if descriptor['mediaType'].endswith(('gzip', '.gzip')) else path.open('rb')) as stream:
                while data := stream.read(64 * 1024):
                    expanded += len(data)
                    require(expanded <= 4 * 1024**3, 'base expansion limit exceeded')
                    digest.update(data)
            require('sha256:' + digest.hexdigest() == self.config['rootfs']['diff_ids'][index], 'base diff ID mismatch')
            with tarfile.open(path, mode='r|*') as archive:
                for member in archive:
                    name = checked_path(member.name)
                    if name in ('etc/passwd', 'etc/group', 'var/lib/dpkg/status', 'etc/os-release') and member.isfile():
                        self.metadata[name] = archive.extractfile(member).read()

    def blob(self, descriptor, kind='blobs'):
        require(0 < descriptor['size'] <= 1024**3, 'blob size exceeded')
        data = fetch(self.url + kind + '/' + descriptor['digest'], descriptor['digest'], headers=self.headers)
        require(len(data) == descriptor['size'], 'blob size mismatch')
        return data

    def entries(self):
        for descriptor in self.layers:
            with tarfile.open(self.blobs[descriptor['digest']], mode='r|*') as archive:
                for member in archive:
                    yield archive, member


def stage(key, prefix):
    prefix = Path(prefix)
    require(not prefix.exists(), 'toolchain prefix already exists')
    with tempfile.TemporaryDirectory(dir=prefix.parent) as temporary:
        base = Base(key, Path(temporary))
        prefix.mkdir()
        for archive, member in base.entries():
            name = checked_path(member.name)
            selected = (name in ('usr/local/bin/node', 'usr/local/bin/npm', 'usr/local/bin/npx')
                        or name.startswith('usr/local/lib/node_modules/npm/')) if key == 'node' else name.startswith('usr/share/dotnet/')
            if not selected:
                continue
            require(not any(part.startswith('.wh.') for part in PurePosixPath(name).parts), 'toolchain whiteout unsupported')
            relative = name.removeprefix('usr/local/' if key == 'node' else 'usr/share/dotnet/')
            destination = prefix / relative
            require(not destination.is_symlink() and not any(p.is_symlink() for p in destination.parents if p != prefix.parent), 'toolchain symlink ancestry')
            if member.isdir():
                destination.mkdir(parents=True, exist_ok=True)
            elif member.isfile():
                destination.parent.mkdir(parents=True, exist_ok=True)
                with archive.extractfile(member) as source, destination.open('wb') as target:
                    shutil.copyfileobj(source, target)
                destination.chmod(member.mode & 0o777)
            elif member.issym():
                target = member.linkname
                if key == 'node' and relative in ('bin/npm', 'bin/npx'):
                    require(target == '../lib/node_modules/npm/bin/' + relative.split('/')[1] + '-cli.js', 'unexpected npm link')
                    continue
                require(not target.startswith('/') and '..' not in PurePosixPath(target).parts, 'unsupported toolchain link')
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.symlink_to(target)
            else:
                raise ValueError('unsupported toolchain entry')
        if key == 'node':
            for name in ('npm', 'npx'):
                target = prefix / 'bin' / name
                if target.is_symlink():
                    target.unlink()
                target.write_text('#!/bin/sh\nprefix=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)\nexec "$prefix/bin/node" "$prefix/lib/node_modules/npm/bin/' + name + '-cli.js" "$@"\n')
                target.chmod(0o755)


def paragraphs(data):
    result = []
    for block in data.decode().split('\n\n'):
        fields = {}
        for line in block.splitlines():
            if ': ' in line and not line.startswith(' '):
                key, value = line.split(': ', 1)
                fields[key] = value
        if fields:
            result.append(fields)
    return result


def version_compare(a, b):
    def split(value):
        epoch, separator, rest = value.partition(':')
        epoch, rest = (int(epoch), rest) if separator else (0, value)
        upstream, separator, revision = rest.rpartition('-')
        return epoch, upstream if separator else rest, revision if separator else '0'
    def order(char):
        # Debian treats digits like an empty span while comparing non-digit characters.
        return (-1 if char == '~' else 0 if not char or char.isdigit()
                else ord(char) if char.isalpha() else ord(char) + 256)
    def segment(a, b):
        while a or b:
            while (a and not a[0].isdigit()) or (b and not b[0].isdigit()):
                x, y = a[:1], b[:1]
                if order(x) != order(y):
                    return (order(x) > order(y)) * 2 - 1
                a, b = a[1:] if a else '', b[1:] if b else ''
            x = re.match(r'\d*', a)[0]
            y = re.match(r'\d*', b)[0]
            if int(x or '0') != int(y or '0'):
                return (int(x or '0') > int(y or '0')) * 2 - 1
            a, b = a[len(x):], b[len(y):]
        return 0
    ae, au, ar = split(a)
    be, bu, br = split(b)
    return ((ae > be) - (ae < be)) or segment(au, bu) or segment(ar, br)


def package_closure(base, roots, architectures):
    installed = {}
    for package in paragraphs(base.metadata['var/lib/dpkg/status']):
        if package.get('Status') == 'install ok installed':
            installed[(package['Package'], package['Architecture'])] = package
    catalog = {}
    for suite, pin in RELEASES.items():
        release = fetch(SNAPSHOT + 'dists/' + suite + '/Release', pin, 4 * 1024**2).decode()
        hashes = {}
        active = False
        for line in release.splitlines():
            if line == 'SHA256:':
                active = True
            elif active and line.startswith(' '):
                checksum, size, name = line.split()
                hashes[name] = checksum
            elif active:
                break
        for component in ('main', 'universe'):
            for architecture in architectures:
                name = f'{component}/binary-{architecture}/Packages.xz'
                data = lzma.decompress(fetch(SNAPSHOT + 'dists/' + suite + '/' + name, hashes[name]))
                for package in paragraphs(data):
                    key = (package['Package'], package['Architecture'])
                    if key not in catalog or version_compare(package['Version'], catalog[key]['Version']) > 0:
                        catalog[key] = package
    selected = {}
    providers = {}
    for mapping in (installed, catalog):
        for package in mapping.values():
            for provided in package.get('Provides', '').split(','):
                name = provided.strip().split(' ')[0]
                if name:
                    providers.setdefault((name, package['Architecture']), []).append(package)
    pending = [(root, 'amd64') for root in roots]
    while pending:
        dependency, parent_architecture = pending.pop()
        alternatives = dependency.split('|')
        chosen = None
        for alternative in alternatives:
            match = re.fullmatch(r'\s*([a-z0-9+.-]+)(?::([a-z0-9-]+))?(?:\s*\((<<|<=|=|>=|>>)\s*([^ )]+)\))?\s*', alternative)
            require(match is not None, 'unsupported package dependency grammar')
            name, architecture, operator, minimum = match.groups()
            architecture = parent_architecture if architecture in (None, 'any', 'native') else architecture
            for mapping in (selected, installed, catalog):
                package = mapping.get((name, architecture)) or mapping.get((name, 'all'))
                if not package:
                    continue
                comparison = version_compare(package['Version'], minimum) if minimum else 0
                valid = not operator or {'<<': comparison < 0, '<=': comparison <= 0, '=': comparison == 0,
                                         '>=': comparison >= 0, '>>': comparison > 0}[operator]
                if valid:
                    chosen = package, mapping is catalog
                    break
            if not chosen and not operator:
                available = providers.get((name, architecture), []) + providers.get((name, 'all'), [])
                if available:
                    package = available[0]
                    chosen = package, (package['Package'], package['Architecture']) not in installed
            if chosen:
                break
        require(chosen is not None, 'package dependency unavailable: ' + dependency)
        package, new = chosen
        key = (package['Package'], package['Architecture'])
        if not new or key in selected:
            continue
        selected[key] = package
        for field in ('Pre-Depends', 'Depends'):
            if package.get(field):
                pending.extend((entry, parent_architecture if package['Architecture'] == 'all' else package['Architecture'])
                               for entry in package[field].split(','))
    return [selected[key] for key in sorted(selected)]


def deb_data(data):
    require(data.startswith(b'!<arch>\n'), 'invalid Debian archive')
    offset = 8
    while offset < len(data):
        header = data[offset:offset + 60]
        require(len(header) == 60 and header[-2:] == b'`\n', 'invalid ar header')
        size = int(header[48:58])
        name = header[:16].decode().strip().rstrip('/')
        offset += 60
        if name.startswith('data.tar'):
            return data[offset:offset + size]
        offset += size + size % 2
    raise ValueError('Debian payload missing')


class ZstdReader(io.RawIOBase):
    """Bounded streaming decoder using the admitted worker's existing libzstd."""
    class Input(ctypes.Structure):
        _fields_ = [('src', ctypes.c_void_p), ('size', ctypes.c_size_t), ('pos', ctypes.c_size_t)]

    class Output(ctypes.Structure):
        _fields_ = [('dst', ctypes.c_void_p), ('size', ctypes.c_size_t), ('pos', ctypes.c_size_t)]

    def __init__(self, data):
        super().__init__()
        self.data = data
        try:
            self.library = ctypes.CDLL('libzstd.so.1')
        except OSError:
            raise ValueError('admitted worker lacks required system libzstd') from None
        self.library.ZSTD_createDStream.restype = ctypes.c_void_p
        self.library.ZSTD_initDStream.argtypes = [ctypes.c_void_p]
        self.library.ZSTD_initDStream.restype = ctypes.c_size_t
        self.library.ZSTD_freeDStream.argtypes = [ctypes.c_void_p]
        self.library.ZSTD_freeDStream.restype = ctypes.c_size_t
        self.library.ZSTD_isError.argtypes = [ctypes.c_size_t]
        self.library.ZSTD_isError.restype = ctypes.c_uint
        self.library.ZSTD_decompressStream.argtypes = [ctypes.c_void_p, ctypes.POINTER(self.Output), ctypes.POINTER(self.Input)]
        self.library.ZSTD_decompressStream.restype = ctypes.c_size_t
        self.context = self.library.ZSTD_createDStream()
        require(self.context, 'cannot allocate bounded zstd decoder')
        require(not self.library.ZSTD_isError(self.library.ZSTD_initDStream(self.context)), 'zstd decoder initialization failed')
        self.source = self.Input(ctypes.cast(ctypes.c_char_p(self.data), ctypes.c_void_p), len(data), 0)
        self.total = 0
        self.finished = False

    def readable(self):
        return True

    def readinto(self, target):
        if self.finished or not target:
            return 0
        buffer = (ctypes.c_char * len(target)).from_buffer(target)
        output = self.Output(ctypes.cast(buffer, ctypes.c_void_p), len(target), 0)
        while output.pos == 0:
            previous = self.source.pos
            result = self.library.ZSTD_decompressStream(self.context, ctypes.byref(output), ctypes.byref(self.source))
            require(not self.library.ZSTD_isError(result), 'invalid zstd package payload')
            if result == 0:
                require(self.source.pos == self.source.size, 'trailing zstd package data')
                self.finished = True
                break
            require(output.pos or self.source.pos != previous, 'truncated zstd package payload')
        self.total += output.pos
        require(self.total <= 4 * 1024**3, 'expanded package exceeds 4 GiB limit')
        return output.pos

    def close(self):
        context = getattr(self, 'context', None)
        if context:
            self.library.ZSTD_freeDStream(context)
            self.context = None
        super().close()


class Overlay:
    def __init__(self, path):
        self.archive = tarfile.open(path, 'w', format=tarfile.PAX_FORMAT)

    def put(self, name, data=b'', mode=0o644, uid=0, gid=0, kind=tarfile.REGTYPE, link=''):
        item = tarfile.TarInfo(checked_path(name))
        item.mode, item.uid, item.gid, item.type, item.linkname = mode, uid, gid, kind, link
        item.size = len(data) if kind == tarfile.REGTYPE else 0
        item.mtime = 0
        self.archive.addfile(item, io.BytesIO(data) if item.size else None)

    def tree(self, source, destination, skip=()):
        source = Path(source)
        require(source.is_dir() and not source.is_symlink(), 'publish directory missing')
        self.put(destination, mode=0o755, kind=tarfile.DIRTYPE)
        for child in sorted(source.iterdir()):
            if child.name in skip:
                continue
            require(not child.is_symlink(), 'publish symlink forbidden')
            if child.is_dir():
                self.tree(child, destination + '/' + child.name)
            else:
                require(child.is_file() and child.stat().st_size <= 512 * 1024**2, 'invalid publish file')
                self.put(destination + '/' + child.name, child.read_bytes(), 0o755 if child.stat().st_mode & 0o111 else 0o644)

    def payload(self, data):
        source = io.BufferedReader(ZstdReader(data), 64 * 1024) if data.startswith(b'\x28\xb5\x2f\xfd') else io.BytesIO(data)
        with source, tarfile.open(fileobj=source, mode='r|*') as archive:
            for item in archive:
                name = checked_path(item.name)
                if name == '.':
                    continue
                require(item.isfile() or item.isdir() or item.issym() or item.islnk(), 'unsupported package entry')
                item.name, item.mtime, item.uname, item.gname, item.pax_headers = name, 0, '', '', {}
                self.archive.addfile(item, archive.extractfile(item) if item.isfile() else None)


def assemble(component, source, output):
    component = 'worker-aska' if component == 'aska' else component
    require(component in ('api', 'web', 'worker', 'worker-aska'), 'unknown component')
    source, output = Path(source), Path(output)
    require(not output.exists(), 'output already exists')
    with tempfile.TemporaryDirectory(dir=output.parent) as temporary:
        work = Path(temporary)
        base = Base('worker' if component.startswith('worker') else component, work)
        layer = work / 'application.tar'
        overlay = Overlay(layer)
        config = copy.deepcopy(base.config.get('config', {}))
        environment = dict(value.split('=', 1) for value in config.get('Env', []))
        config.update({'WorkingDir': '/app', 'Cmd': [], 'User': 'root'})
        roots = ['libgssapi-krb5-2'] if component == 'api' else []
        if component.startswith('worker'):
            roots = ['bash', 'ca-certificates', 'curl', 'git', 'libc6:i386', 'libgcc-s1',
                     'libgcc-s1:i386', 'libstdc++6', 'libstdc++6:i386', 'tar', 'tini', 'util-linux']
            if component == 'worker-aska':
                roots += ['fonts-liberation', 'wine', 'wine64', 'winbind', 'xauth', 'xvfb']
        if roots:
            require(base.config['config'].get('Labels', {}).get('org.opencontainers.image.version') == '24.04', 'package base is not noble')
            packages = package_closure(base, roots, ['amd64', 'i386'] if component.startswith('worker') else ['amd64'])
            for package in packages:
                overlay.payload(deb_data(fetch(SNAPSHOT + package['Filename'], package['SHA256'])))
            # Essential package manager triggers are represented directly in OCI;
            # target-maintainer scripts, chroot and ldconfig are never executed.
        if component == 'api':
            overlay.tree(source / '.publish/api', 'app')
            for directory in ('app/App_Data', 'home/app/.aspnet', 'home/app/.aspnet/DataProtection-Keys',
                              'var/lib/united-tactical-forces', 'var/lib/united-tactical-forces/hosting'):
                overlay.put(directory, mode=0o755, uid=1654, gid=1654, kind=tarfile.DIRTYPE)
            config.update({'User': '1654:1654', 'Entrypoint': ['dotnet', 'X39.UnitedTacticalForces.Api.dll'], 'ExposedPorts': {'8080/tcp': {}}})
            environment.update({'ASPNETCORE_HTTP_PORTS': '8080', 'DOTNET_EnableDiagnostics': '0'})
        elif component == 'web':
            excluded = [child.name for child in (source / '.publish/web/wwwroot').glob('appsettings*')
                        if child.name == 'appsettings.Production.json' or child.name.endswith(('.json.br', '.json.gz'))]
            overlay.tree(source / '.publish/web/wwwroot', 'usr/share/nginx/html', excluded)
            overlay.tree(source / 'sources/ReactApp/dist', 'usr/share/nginx/html/next')
            overlay.tree(source / 'sources/ReactApp/dist-root', 'usr/share/nginx/html/canary')
            overlay.put('etc/nginx/nginx.conf', (source / 'deploy/nginx.conf').read_bytes())
            overlay.put('usr/share/nginx/html/appsettings.json', (source / 'deploy/webapp.appsettings.json').read_bytes())
            for child in (source / '.publish/web/wwwroot').glob('appsettings*'):
                if child.name == 'appsettings.Production.json' or child.name.endswith(('.json.br', '.json.gz')):
                    overlay.put('usr/share/nginx/html/.wh.' + child.name)
            config.update({'User': 'nginx', 'WorkingDir': '/', 'Entrypoint': ['nginx', '-g', 'daemon off;'], 'ExposedPorts': {'8080/tcp': {}}})
        else:
            overlay.tree(source / '.publish/worker', 'app')
            overlay.put('app/docker-entrypoint.sh', (source / 'sources/CloudWorker/docker-entrypoint.sh').read_bytes(), 0o555)
            overlay.put('etc/passwd', base.metadata['etc/passwd'] + b'utfgame:x:10002:10001::/srv/united-tactical-forces/runtime:/usr/sbin/nologin\nutfsteam:x:10003:10001::/srv/united-tactical-forces/runtime:/usr/sbin/nologin\n')
            overlay.put('etc/group', base.metadata['etc/group'] + b'utfruntime:x:10001:\n')
            for directory, mode, gid in [('var/lib/united-tactical-forces', 0o755, 0), ('var/lib/united-tactical-forces/worker', 0o700, 0),
                                          ('srv/united-tactical-forces/runtime', 0o2770, 10001), ('opt/steamcmd', 0o755, 0)]:
                overlay.put(directory, mode=mode, gid=gid, kind=tarfile.DIRTYPE)
            steam = fetch('https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz',
                          'cebf0046bfd08cf45da6bc094ae47aa39ebf4155e5ede41373b579b8f1071e7c')
            with tarfile.open(fileobj=io.BytesIO(steam), mode='r:gz') as archive:
                for item in archive:
                    require(item.isfile() or item.isdir(), 'unexpected SteamCMD archive entry')
                    overlay.put('opt/steamcmd/' + checked_path(item.name), archive.extractfile(item).read() if item.isfile() else b'', item.mode, kind=item.type)
            environment.update({'UTF_WORKER_BOOTSTRAP_PATH': '/run/secrets/utf-worker-bootstrap.json',
                'UTF_WORKER_STATE_DIRECTORY': '/var/lib/united-tactical-forces/worker', 'UTF_WORKER_RUNTIME_ROOT': '/srv/united-tactical-forces/runtime',
                'UTF_STEAMCMD_PATH': '/srv/united-tactical-forces/runtime/.utf-tooling/steamcmd/steamcmd.sh',
                'UTF_STEAM_HOME': '/srv/united-tactical-forces/runtime/.utf-tooling/steam-home', 'UTF_STEAM_UID': '10003',
                'UTF_GAME_UID': '10002', 'UTF_GAME_GID': '10001', 'UTF_GAME_PRIVILEGE_DROP_PATH': '/usr/bin/setpriv'})
            if component == 'worker-aska':
                for executable in ('wine', 'wineboot'):
                    overlay.put('usr/bin/' + executable, kind=tarfile.SYMTYPE, link=executable + '-stable')
                environment.update({'UTF_ASKA_WINE_PATH': '/usr/bin/wine', 'UTF_ASKA_WINEBOOT_PATH': '/usr/bin/wineboot',
                    'UTF_ASKA_XVFB_RUN_PATH': '/usr/bin/xvfb-run', 'UTF_ASKA_SETSID_PATH': '/usr/bin/setsid'})
            config.update({'Entrypoint': ['/usr/bin/tini', '-g', '--', '/app/docker-entrypoint.sh'],
                'Volumes': {'/var/lib/united-tactical-forces/worker': {}, '/srv/united-tactical-forces/runtime': {}}})
        config['Env'] = [key + '=' + value for key, value in sorted(environment.items())]
        overlay.archive.close()
        diff_id = 'sha256:' + file_sha(layer)
        compressed = work / 'application.tar.gz'
        with layer.open('rb') as original, compressed.open('xb') as target:
            with gzip.GzipFile(filename='', mode='wb', fileobj=target, mtime=0, compresslevel=6) as stream:
                shutil.copyfileobj(original, stream, 64 * 1024)
        descriptor = {'mediaType': LAYER + '+gzip', 'digest': 'sha256:' + file_sha(compressed), 'size': compressed.stat().st_size}
        blobs = dict(base.blobs)
        blobs[descriptor['digest']] = compressed
        image_config = copy.deepcopy(base.config)
        image_config.update({'created': '1970-01-01T00:00:00Z', 'config': config})
        image_config['rootfs']['diff_ids'].append(diff_id)
        image_config.setdefault('history', []).append({'created': '1970-01-01T00:00:00Z', 'created_by': 'UTF ordinary OCI assembly'})
        def blob(value, media):
            data = json.dumps(value, separators=(',', ':'), sort_keys=True).encode()
            digest = 'sha256:' + sha(data)
            path = work / digest.split(':')[1]
            path.write_bytes(data)
            blobs[digest] = path
            return {'mediaType': media, 'digest': digest, 'size': len(data)}
        manifest = blob({'schemaVersion': 2, 'mediaType': MANIFEST, 'config': blob(image_config, CONFIG),
                         'layers': base.layers + [descriptor]}, MANIFEST)
        index = {'schemaVersion': 2, 'mediaType': INDEX, 'manifests': [manifest]}
        require(sum(path.stat().st_size + 1024 for path in blobs.values()) + 8192 <= 1024**3,
                'compressed OCI archive exceeds the 1 GiB publication limit')
        with tarfile.open(output, 'x', format=tarfile.USTAR_FORMAT) as archive:
            def add(name, data):
                item = tarfile.TarInfo(name)
                item.mode, item.size, item.mtime = 0o644, len(data), 0
                archive.addfile(item, io.BytesIO(data))
            add('oci-layout', b'{"imageLayoutVersion":"1.0.0"}')
            add('index.json', json.dumps(index, separators=(',', ':'), sort_keys=True).encode())
            for digest, path in sorted(blobs.items()):
                item = tarfile.TarInfo('blobs/sha256/' + digest.split(':')[1])
                item.mode, item.size, item.mtime = 0o644, path.stat().st_size, 0
                with path.open('rb') as stream:
                    archive.addfile(item, stream)
        verify(component, output)


def verify(component, output):
    """Inspect the serialized artifact, including ownership without host chown."""
    component = 'worker-aska' if component == 'aska' else component
    require(component in ('api', 'web', 'worker', 'worker-aska'), 'unknown component')
    with tarfile.open(output, 'r:') as archive:
        index = json.load(archive.extractfile('index.json'))
        require(len(index['manifests']) == 1, 'archive must contain one platform')
        def document(descriptor):
            data = archive.extractfile('blobs/sha256/' + descriptor['digest'].split(':')[1]).read()
            require(len(data) == descriptor['size'] and 'sha256:' + sha(data) == descriptor['digest'], 'serialized descriptor mismatch')
            return json.loads(data)
        manifest = document(index['manifests'][0])
        image = document(manifest['config'])
        config = image['config']
        require(image['architecture'] == 'amd64' and image['os'] == 'linux', 'serialized platform mismatch')
        require(config['Cmd'] == [], 'inherited command was not cleared')
        environment = dict(value.split('=', 1) for value in config['Env'])
        descriptor = manifest['layers'][-1]
        member = archive.getmember('blobs/sha256/' + descriptor['digest'].split(':')[1])
        require(member.size == descriptor['size'], 'serialized overlay size mismatch')
        members = {}
        contents = {}
        digest = hashlib.sha256()
        stream = archive.extractfile(member)
        while block := stream.read(64 * 1024):
            digest.update(block)
        require('sha256:' + digest.hexdigest() == descriptor['digest'], 'serialized overlay digest mismatch')
        with gzip.GzipFile(fileobj=archive.extractfile(member)) as expanded:
            require('sha256:' + hashlib.file_digest(expanded, 'sha256').hexdigest() == image['rootfs']['diff_ids'][-1], 'serialized diff ID mismatch')
        with tarfile.open(fileobj=archive.extractfile(member), mode='r|*') as overlay:
            for entry in overlay:
                members[entry.name] = entry
                if entry.isfile() and entry.name in ('usr/share/nginx/html/appsettings.json', 'etc/nginx/nginx.conf'):
                    contents[entry.name] = overlay.extractfile(entry).read()
        def ownership(name, uid, gid, mode):
            entry = members[name]
            require((entry.uid, entry.gid, entry.mode) == (uid, gid, mode), 'serialized ownership mismatch: ' + name)
        if component == 'api':
            require(config['User'] == '1654:1654' and config['WorkingDir'] == '/app'
                    and config['Entrypoint'] == ['dotnet', 'X39.UnitedTacticalForces.Api.dll'], 'API launch contract changed')
            require(environment['ASPNETCORE_HTTP_PORTS'] == '8080' and environment['DOTNET_EnableDiagnostics'] == '0', 'API environment changed')
            require('8080/tcp' in config['ExposedPorts'] and 'app/X39.UnitedTacticalForces.Api.dll' in members, 'API payload missing')
            for name in ('app/App_Data', 'home/app/.aspnet/DataProtection-Keys', 'var/lib/united-tactical-forces/hosting'):
                ownership(name, 1654, 1654, 0o755)
        elif component == 'web':
            require(config['User'] == 'nginx' and config['Entrypoint'] == ['nginx', '-g', 'daemon off;']
                    and '8080/tcp' in config['ExposedPorts'], 'web launch contract changed')
            require('usr/share/nginx/html/next/index.html' in members and 'usr/share/nginx/html/canary/index.html' in members, 'React assets missing')
            require('usr/share/nginx/html/appsettings.Production.json' not in members, 'stale production settings copied')
            require(not any(name.startswith('usr/share/nginx/html/appsettings') and name.endswith(('.json.gz', '.json.br')) for name in members), 'stale compressed settings copied')
            require(contents['usr/share/nginx/html/appsettings.json'] and contents['etc/nginx/nginx.conf'], 'web runtime configuration missing')
        else:
            require(config['User'] == 'root' and config['WorkingDir'] == '/app'
                    and config['Entrypoint'] == ['/usr/bin/tini', '-g', '--', '/app/docker-entrypoint.sh'], 'worker launch contract changed')
            ownership('var/lib/united-tactical-forces/worker', 0, 0, 0o700)
            ownership('srv/united-tactical-forces/runtime', 0, 10001, 0o2770)
            ownership('app/docker-entrypoint.sh', 0, 0, 0o555)
            require(environment['UTF_GAME_UID'] == '10002' and environment['UTF_STEAM_UID'] == '10003'
                    and environment['UTF_GAME_GID'] == '10001', 'worker identities changed')
            require('/srv/united-tactical-forces/runtime' in config['Volumes']
                    and '/var/lib/united-tactical-forces/worker' in config['Volumes'], 'worker volume contract changed')
            require('app/X39.UnitedTacticalForces.CloudWorker.dll' in members and 'opt/steamcmd/steamcmd.sh' in members, 'worker payload missing')
            if component == 'worker-aska':
                require(environment['UTF_ASKA_WINE_PATH'] == '/usr/bin/wine' and 'usr/bin/wine' in members
                        and 'usr/bin/wineboot' in members, 'ASKA Wine command contract missing')
    print('Verified serialized OCI launch and ownership contract: ' + component)


if __name__ == '__main__':
    try:
        command, *arguments = sys.argv[1:]
        if command == 'stage-node' and len(arguments) == 1:
            stage('node', arguments[0])
        elif command == 'stage-dotnet' and len(arguments) in (1, 2):
            stage(arguments[1] if len(arguments) == 2 else '201', arguments[0])
        elif command == 'assemble' and len(arguments) == 3:
            assemble(*arguments)
        elif command == 'verify' and len(arguments) == 2:
            verify(*arguments)
        else:
            raise ValueError('usage: stage-node PREFIX | stage-dotnet PREFIX [201|302] | assemble COMPONENT SOURCE OUTPUT')
    except Exception as error:
        print('UTF OCI assembly failed: ' + (str(error) if isinstance(error, ValueError) else type(error).__name__), file=sys.stderr)
        sys.exit(1)
