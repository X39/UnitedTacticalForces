# Amsel: what the release push needs on the host

Nothing is installed and nothing listens. The release job pushes the exact API and web image archives into
the `utf-deploy` user's rootless Podman with one SSH connection per archive, and the only thing the CI key
may run on the host is `podman load`. There is no script, daemon, socket or registry credential on Amsel,
and the host never pulls.

## The single host change (owner action)

From your own PowerShell, `deploy\operator\Install-DeployKey.ps1` makes this change for you: it connects to
the host itself, shows what it will do, asks for confirmation, appends the line below, and then proves from
your computer that the key can only run `podman load`. `-CheckOnly` shows the podman path, the deploy user's
home and the existing key fingerprints without changing anything. The line it writes is:

One line in `utf-deploy`'s `~/.ssh/authorized_keys`, replacing the retired receiver's entry. The public key
is committed as `deploy/utf-deploy.pub`; the private key exists only as the trusted-branch secret pipeline
variable `UTF_DEPLOY_SSH_KEY`:

```text
restrict,command="/usr/bin/podman load" <contents of deploy/utf-deploy.pub>
```

- `restrict` disables forwarding, PTY, agent and X11. `command=` makes `podman load` the only possible
  command, whatever the client asks for. With that key an attacker could import an image archive into this
  user's local storage; they could not start a container, get a shell or read anything.
- Use the real path from `command -v podman` as `utf-deploy`.
- Check the line with `ssh-keygen -lf ~/.ssh/authorized_keys`.
- The host key the job pins is `deploy/known_hosts`.

## Checks by hand before the first release

As `utf-deploy` on the host:

```sh
command -v podman && podman --version
df -h "$(podman info --format '{{.Store.GraphRoot}}')"      # room for the API and web images
podman load < some-small-oci-or-docker-archive.tar            # prints "Loaded image: ..." and exits 0
```

## Image identity

`podman load` verifies layers by digest, and the image ID is the config digest. The pipeline computes that
ID from the archive before sending and records it in `release.json` (printed in the job log as
`image id`). Later steps, including the separately approved activation, refer to that ID, so a different
image under the same name cannot be started by mistake. Loading does not start, stop or replace anything.
