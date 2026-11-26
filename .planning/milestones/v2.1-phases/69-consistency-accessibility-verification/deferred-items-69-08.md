# Deferred items found by plan 69-08

Out of scope for the shell plan; recorded for the owning plan or the closure plan.

1. `tests/e2e/hosting-fleet.spec.ts:558` ("the Command Center nav and the fleet page read Servers", chromium, 390x844)
   expects the page title to match `/Servers/` but the layout title is "Server fleet"
   (`Shared_CommandCenter_CurrentArea_ServerFleet`). The layout title code is unchanged by 69-08; the expectation
   predates the Wave 1 label work. Owner: the hosting-core plan (69-09) or whoever owns that spec.
2. The sign-in page's "no login method" state has no title and body key pair, so it cannot use `EmptyState`
   (title and body are both required, D-03). It stays a `<p role="status">` with `Authentication_NoMethodsAvailable`.
   Needed keys: an empty title and a body that names the next step (for example "Ask an administrator to enable a
   sign-in provider").
3. Keys without a reader after this plan: `Shared_CommunityDialog_Cancel` (the community sign-in dialog now reads
   `Shared_CommunityDialog_Login_Dismiss`). Retire it in 69-33.
4. Legacy rules in `styles.css` that lost their last user in the shell: `.language-select` and its media rules,
   `.command-overview-page`, `.account-profile__button*` (the last lived in `ProfileMenu.css` and is already gone).
   `.icon-button` still has a reader in `GalleryAssetPicker.tsx`. Retire the rest in the closure plan, which owns
   `styles.css`.
