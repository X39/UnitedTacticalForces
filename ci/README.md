# Release push client

`stream_release.py` pushes the exact API and web OCI layout archives built by the release job to the
production host, one SSH connection per archive. The job runs it **before** the registry publication steps (Mozaic
requires publications to be the last steps of a job) and after the archives are assembled and checked.
No registry credential exists on either side; the host never pulls.

- `prepare-deploy-key.sh` runs first in the job, before the long build. It writes the SSH key from the
  trusted-branch secret variable `UTF_DEPLOY_SSH_KEY` (OpenSSH PEM, or base64 of it on one line), checks
  that it matches the committed `deploy/utf-deploy.pub`, and prints only the fingerprint.
- `stream_release.py` records the image identities (manifest digest and image ID) in `release.json`, builds a hardened SSH command
  (strict pinned host key from `deploy/known_hosts`, batch mode, no forwarding, HTTP CONNECT through the
  job's allowed proxy) and streams each archive as stdin of one connection that runs `podman load`.
- The host runs no custom code: the CI key is limited to the forced command `podman load`; see `deploy/host/README.md`.

The target is described by `deploy/production.json` (Amsel, account `utf-deploy`). Its `key_file` is the
workspace path written by `prepare-deploy-key.sh`.

## Retired pieces

`deploy.sh`, `invoke-protected-deploy.py`, `deliver-release.sh`, `oci-mode` and `oci_download.py`
implemented the earlier flow that obtained images with a per-attempt OCI reader credential and relied on
Mozaic attempt evidence. Mozaic removed both (`secret_files`, `deployment_oci_read` and the evidence
endpoints), so they cannot run on the current platform. They are kept only until the new flow has been
accepted on the host and are not referenced by any pipeline.

Run the focused tests with `python3 -m unittest discover -s ci -p 'test_*.py'` on Linux.
