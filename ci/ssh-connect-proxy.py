#!/usr/bin/env python3
"""Tunnel OpenSSH over the attempt's explicitly authorized HTTP CONNECT proxy."""

import os
import re
import select
import socket
import sys
import urllib.parse


def connect(proxy_url, host, port):
    proxy = urllib.parse.urlsplit(proxy_url)
    if (proxy.scheme != "http" or not proxy.hostname or not proxy.port
            or proxy.username or proxy.password or proxy.path not in ("", "/")
            or proxy.query or proxy.fragment
            or not re.fullmatch(r"[a-z0-9][a-z0-9.-]{0,252}", host)
            or not 1 <= port <= 65535):
        raise ValueError("Invalid CONNECT boundary")
    connection = socket.create_connection((proxy.hostname, proxy.port), timeout=20)
    try:
        authority = host + ":" + str(port)
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
