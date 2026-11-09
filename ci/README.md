# Amsel release preflight

The ordinary deployment job obtains only the exact `applications/utf/api`, `applications/utf/web`,
and `applications/utf/cloud-worker` publications from its own `checked-artifacts` job.
It transfers the immutable OCI archives through the job's explicit CONNECT
route and strict checked-in Amsel host key. The target scope is
`c2cf220f-3b1e-4967-b413-87e9119d08bc` and the exact job grant expects
the Amsel-owned key in project secret `managed-utf-ssh`.

This client requires the UTF-specific receiver profile. The versioned stream
magic is `mozaic-utf-oci-v1` with fixed roles `api`, `web`, `worker`
in that order. The worker role names `applications/utf/cloud-worker`. The receiver must
check all three against one live Verified attempt.

`ci/oci-mode` defaults to `receive`. A successful transfer validates the
cohort without replacing the live service. Enable activation only after
the UTF controller has adopted the retained API/web containers, can make
a coherent backup, and can apply the accepted worker digest to protected
provisioning inputs. UTF starts API/web on Amsel. Its worker publication
is a dependency for remote provisioning.

Run the focused transport tests with
`python3 -m unittest discover -s ci -p 'test_*.py'`.
