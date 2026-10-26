# Ordinary deployment client

`deploy.sh`, `invoke-protected-deploy.py`, and `ssh-connect-proxy.py` are exact
copies of the generic Mozaic client from shared commit
`bf940a629c55e3d7da27b0e352cc80879140b69f`. The shell source is named
`tools/invoke-protected-deploy.sh` upstream and is renamed here to `deploy.sh`;
its contents are unchanged.

| Local path | Git blob identity |
| --- | --- |
| `deploy.sh` | `026ad5407057199c47524aca6932cb278707f623` |
| `invoke-protected-deploy.py` | `4a0db2d3c5a4480f41161cbbe749cc90deddac1f` |
| `ssh-connect-proxy.py` | `e0909ea16421b6ff4299227e70e2e13b0923ce1e` |

The ordinary command accepts `--target`, `--manifest`, `--release-job`,
`--attempt`, `--wait`, and an optional bounded `--timeout`. It uses OpenSSH with
strict known-host validation and HTTP CONNECT through the job's explicitly
allowed proxy destination. The private invoke-only key stays in a protected file.
Runtime/evidence credentials remain on the target.

No UTF target manifest or CI invocation is enabled yet. The shared infrastructure
owner must supply the reviewed public DNS, SSH account, exact known-host bytes,
private key reference, target binding, canonical source branch and queue slot.
The deployment job must be separate from `checked-artifacts`, require it through
normal `needs`, and receive no build artifacts, caches or test-report outputs.
It resolves all four immutable publications using the exact requesting attempt.

Do not replace this client with a polling process, mutable image lookup, direct
SSH networking, or application-specific runner dispatch. Update the shared source
first when generic behavior needs to change, then synchronize these copies.
