# Amsel release preflight

The ordinary deployment job obtains only the exact `applications/utf/api`
and `applications/utf/web` publications from its own `checked-artifacts` job.
It transfers the immutable OCI archives through the job's explicit CONNECT
route and strict checked-in Amsel host key. The target scope is
`c2cf220f-3b1e-4967-b413-87e9119d08bc` and the exact job grant expects
the Amsel-owned key in project secret `managed-utf-ssh`.

This client requires UTF receiver profile 12. The stream starts with
`mozaic-utf-local-oci-v1` and LF, followed by two unsigned 64-bit big-endian
lengths and the API and web archives in order. The receiver verifies both
publications against one live Verified attempt. The release reference also
binds the retained worker digest from the existing deployment. It is never
downloaded by this job or included in the SSH stream.

`ci/oci-mode` defaults to `receive`. A successful transfer validates the
cohort without replacing the live service. Enable activation only after
the UTF controller has adopted the retained API/web containers, can make
a coherent backup, and has pinned the reviewed activation revision and the
existing version 6 provisioning inputs. The separately built worker stays
available for a later, independently approved worker update.

Run the focused transport tests with
`python3 -m unittest discover -s ci -p 'test_*.py'`.
