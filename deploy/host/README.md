# Amsel: what the release pipeline needs on the host

The release job logs in to Amsel as the unprivileged, rootless `utf-deploy` user with one organization-wide SSH
key and runs fixed commands from this repository (`ci/stream_release.py`, `ci/activate_release.py`). The host never
pulls from a registry and holds no registry credential.

## One-time host setup

1. **Key.** The private key is the organization secret variable `AMSEL_DEPLOY_SSH_KEY` (trusted branch only). The
   public half is `deploy/amsel-ci.pub`; it is authorized with `restrict` (no forwarding, PTY or agent) in the file
   sshd reads for `utf-deploy`. Ask sshd where that is: `sshd -T -C user=utf-deploy,host=x,addr=127.0.0.1 | grep authorizedkeysfile`
   (on Amsel a `Match User` block points it to `/etc/ssh/mozaic-authorized-keys/utf-owner-deployment`, not `~/.ssh`).
   The host key the job pins is `deploy/known_hosts`.
2. **Units.** Copy `deploy/host/utf-api.service` and `utf-web.service` to `~utf-deploy/.config/systemd/user/` and run
   `systemctl --user daemon-reload` (done once on 2026-11-18). They run exactly the tags `localhost/utf/api:release` and
   `localhost/utf/web:release` with the limits, network, ports, volumes and `api.env` the containers had before. Nothing
   else is done by hand: the first pipeline release stops the retired migration-managed service
   (`mozaic-utf-ordinary.service` and the `mozaic-app-utf-*` containers), enables and starts these units and disables the
   retired units; if that release fails the job starts the retired service again.
3. **Backups** go to `~utf-deploy/backups/utf-<revision>.dump` (last ten kept), taken by the pipeline with the
   database service file and password file already in `~/.config/mozaic-deploy/targets/utf/`.

## Rollback

The previous images keep the tag `:previous`. The job restores them by itself when a check fails. By hand:

```sh
for r in api web; do podman tag localhost/utf/$r:previous localhost/utf/$r:release; done
systemctl --user restart utf-api.service utf-web.service
```

A database restore is `pg_restore` of the matching `~/backups/utf-<revision>.dump`.

## Image identity

`podman load` verifies layers by digest and the image ID is the config digest. The job computes that ID from the
archive before sending, records it in `release.json` (also printed as `image id`), and later steps, the running-image
check included, refer to that ID, so a different image under the same name cannot be started by mistake.
