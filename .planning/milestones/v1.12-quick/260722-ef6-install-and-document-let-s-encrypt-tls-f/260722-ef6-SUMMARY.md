---
quick_id: 260722-ef6
status: complete
completed: 2026-07-22
description: Install and document Let's Encrypt TLS for production sites
---

# Quick Task 260722-ef6 Summary

## Outcome

- Installed Debian's Certbot 4.0 packages and nginx integration.
- Issued separate ECDSA Let's Encrypt certificates for x39 and UTF, each covering the apex and `www` hostname.
- Enabled nginx HTTPS listeners on IPv4 and IPv6 and redirected HTTP to HTTPS.
- Enabled automatic renewal through `certbot.timer` and documented certificate operations.

## Verification

- nginx configuration validation passed and ports 80/443 listen on IPv4 and IPv6.
- All four HTTP hostnames return 301 redirects to HTTPS.
- All four public HTTPS URLs return HTTP 200 with successful certificate verification.
- Both certificates are valid through 2026-10-20.
- `certbot renew --dry-run` succeeded for both certificate lineages.

## Operations

- The ACME account has no email address; monitor `certbot.timer` and renewal results locally.
- Certificate private keys remain exclusively under `/etc/letsencrypt/` on the server.
