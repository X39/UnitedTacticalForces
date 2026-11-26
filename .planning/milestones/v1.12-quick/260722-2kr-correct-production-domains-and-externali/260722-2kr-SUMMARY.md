---
quick_id: 260722-2kr
status: complete
completed: 2026-07-22
description: Correct production domains and externalize UTF server configuration
---

# Quick Task 260722-2kr Summary

## Outcome

- Corrected host nginx routing to `x39.dev`, `www.x39.dev`, `unitedtacticalforces.de`, and `www.unitedtacticalforces.de`.
- Updated UTF's root-only API environment to use `https://unitedtacticalforces.de/` and added the supported optional integration keys without changing its generated database connection.
- Added a host-managed, browser-safe WebApp settings file and mounted it read-only into `utf-web`.
- Corrected the UTF deployment guide and the x39 website README.

## Verification

- nginx syntax validation passed and all affected systemd services are active.
- All four nginx host-header routes return HTTP 200.
- Both public DNS zones resolve all four names to `2a01:4f8:1c1c:6beb::1` through a public resolver.
- The API environment remains mode `0600`; public WebApp settings are mode `0644` and contain no secrets.
- UTF serves the mounted WebApp configuration with the relative `/api/` base URL.

## Follow-up

- Populate only the integrations that will be enabled, then restart `utf-api`.
- Install end-to-end TLS before enabling Steam or Discord authentication.
