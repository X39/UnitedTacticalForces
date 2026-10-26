# Ordinary deployment client

`deploy.sh`, `invoke-protected-deploy.py`, and `ssh-connect-proxy.py` are exact
copies of the generic Mozaic client from shared commit
`56832f9b9cd7c3bbc3c0125246a42a010e5ed870`. The shell source is named
`tools/invoke-protected-deploy.sh` upstream and is renamed here to `deploy.sh`;
its contents are unchanged.

| Local path | Git blob identity |
| --- | --- |
| `deploy.sh` | `026ad5407057199c47524aca6932cb278707f623` |
| `invoke-protected-deploy.py` | `5b6144ddd3af2d9d087717b7efdb28995681977e` |
| `ssh-connect-proxy.py` | `1ff86fd3f278d168e654ae8f57e18a9e9f1b6fda` |

The ordinary command accepts `--target`, `--manifest`, `--release-job`,
`--attempt`, `--wait`, and an optional bounded `--timeout`. It uses OpenSSH with
strict known-host validation and HTTP CONNECT through the job's explicitly
allowed proxy destination. The private invoke-only key stays in a protected file.
Runtime/evidence credentials remain on the target.

No UTF target manifest or CI invocation is enabled yet. The shared infrastructure
owner must supply the reviewed public endpoint, SSH account, exact known-host bytes,
private key reference, target binding, canonical source branch and queue slot.
The deployment job must be separate from `checked-artifacts`, require it through
normal `needs`, and receive no build artifacts, caches or test-report outputs.
It resolves all four immutable publications using the exact requesting attempt.

Do not replace this client with a polling process, mutable image lookup, direct
SSH networking, or application-specific runner dispatch. Update the shared source
first when generic behavior needs to change, then synchronize these copies.
