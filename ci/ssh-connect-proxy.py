#!/usr/bin/env python3
"""Tunnel OpenSSH over the attempt's explicitly authorized HTTP CONNECT proxy."""

import os
import re
import ipaddress
import select
import socket
import sys
import urllib.parse


def validate_host(host):
    """Accept DNS names or canonical unicast addresses in the public egress subset."""
    if not isinstance(host, str) or not host or len(host) > 253 or "%" in host:
        raise ValueError("Invalid endpoint host")
    try:
        address = ipaddress.ip_address(host)
    except ValueError:
        if (not re.fullmatch(r"[a-z0-9.-]+", host)
                or re.fullmatch(r"[0-9.]+", host)
                or any(not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?", label)
                       for label in host.split("."))):
            raise ValueError("Invalid endpoint host") from None
        return host
    if (str(address) != host or not address.is_global or address.is_multicast
            or address.is_reserved or getattr(address, "ipv4_mapped", None) is not None):
        raise ValueError("Endpoint address must be canonical public unicast")
    if address.version == 6:
        if (address not in ipaddress.ip_network("2000::/3")
                or any(address in ipaddress.ip_network(network) for network in
                       ("2001::/23", "2001:db8::/32", "2002::/16", "3fff::/20"))):
            raise ValueError("Endpoint address is outside public egress subset")
    if address.version == 4 and any(address in ipaddress.ip_network(network) for network in
            ("0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16",
             "172.16.0.0/12", "192.0.0.0/24", "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16",
             "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/3")):
        raise ValueError("Endpoint address is outside public egress subset")
    return host


def connect(proxy_url, host, port):
    validate_host(host)
    proxy = urllib.parse.urlsplit(proxy_url)
    if (proxy.scheme != "http" or not proxy.hostname or not proxy.port
            or proxy.username or proxy.password or proxy.path not in ("", "/")
            or proxy.query or proxy.fragment
            or not 1 <= port <= 65535):
        raise ValueError("Invalid CONNECT boundary")
    connection = socket.create_connection((proxy.hostname, proxy.port), timeout=20)
    try:
        authority = ("[" + host + "]" if ":" in host else host) + ":" + str(port)
        connection.sendall(("CONNECT " + authority + " HTTP/1.1\r\nHost: " + authority
                            + "\r\n\r\n").encode("ascii"))
        # Read only headers: no SSH bytes may be consumed and discarded.
        header = bytearray()
        while not header.endswith(b"\r\n\r\n"):
            part = connection.recv(1)
            if not part or len(header) >= 8192:
                raise ValueError("Invalid CONNECT response")
            header.extend(part)
        if not re.fullmatch(rb"HTTP/1\.[01] 200(?: [^\r\n]*)?", header.split(b"\r\n", 1)[0]):
            raise ValueError("CONNECT was rejected")
        return connection
    except BaseException:
        connection.close()
        raise


def main():
    if len(sys.argv) != 3:
        raise ValueError("Invalid CONNECT arguments")
    with connect(os.environ.get("HTTP_PROXY", ""), sys.argv[1], int(sys.argv[2])) as connection:
        while True:
            ready, _, _ = select.select([connection, sys.stdin.buffer], [], [], 60)
            if connection in ready:
                data = connection.recv(65536)
                if not data:
                    return
                sys.stdout.buffer.write(data)
                sys.stdout.buffer.flush()
            if sys.stdin.buffer in ready:
                data = os.read(sys.stdin.fileno(), 65536)
                if not data:
                    return
                connection.sendall(data)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError):
        print("SSH CONNECT failed.", file=sys.stderr)
        sys.exit(1)
