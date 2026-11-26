---
phase: 65-change-a-server
reviewed: 2026-11-21T00:00:00Z
depth: standard
files_reviewed: 30
files_reviewed_list:
  - sources/Api/Controllers/GameServerSecretsController.cs
  - sources/Api/DTO/GameServerSecretStatusDto.cs
  - sources/Api/DTO/GameServerSecretValueDto.cs
  - sources/Api/Services/GameServerDefinition/Arma3GameServerDefinitionRegistration.cs
  - sources/Api/Services/GameServerDefinition/CounterStrike2GameServerDefinitionRegistration.cs
  - sources/Api/Services/GameServerDefinition/EGameSecretValueFormat.cs
  - sources/Api/Services/GameServerDefinition/GameSecretDefinition.cs
  - sources/Api/Services/GameServerSecrets/GameServerSecretService.cs
  - sources/Api/Services/GameServerSecrets/IGameServerSecretService.cs
  - sources/ReactApp/src/components/server/credentials.css
  - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
  - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
  - sources/ReactApp/src/features/hosting/serverChangeDraft.ts
  - sources/ReactApp/src/features/hosting/services.ts
  - sources/ReactApp/src/features/hosting/types.ts
  - sources/ReactApp/src/features/hosting/useServerChangeApply.ts
  - sources/ReactApp/src/features/hosting/useServerChangeDraft.tsx
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts
  - sources/ReactApp/tests/hosting/contract-shape.test.ts
  - sources/ReactApp/tests/hosting/credentials-panel.test.tsx
  - sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx
  - sources/ReactApp/tests/hosting/server-change-draft.test.ts
  - sources/ReactApp/tests/locales/server-keys.test.ts
  - tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs
  - tests/Api.Tests/GameServerHosting/GameServerSecretsControllerTests.cs
  - tests/Api.Tests/GameServerHosting/TestGameServerSecretService.cs
findings:
  critical: 0
  warning: 4
  info: 6
  total: 10
status: issues_found
---

# Phase 65: Code Review Report (gap-closure plans 65-16 to 65-19)

**Reviewed:** 2026-11-21
**Depth:** standard
**Files Reviewed:** 28 source and test files (generated `api.ts` and `utf-api.json` excluded as instructed; the
`.planning/` parity matrix change is out of scope)
**Status:** issues_found

## Summary

The credential-value hygiene is largely sound. I traced the plaintext through every layer and found no path where a
value is logged, cached, stored or echoed:

- API: the rotate and reveal responses carry `Cache-Control: no-store`. No HTTP logging middleware exists. The audit
  summary and details carry only key and version. Validation messages are fixed strings and never include the value.
- React: the reveal mutation uses `gcTime: 0` and `reset()`. The apply mutation resolves value-free, because the rotate
  executor hands the value to provider state through a callback. Mutations do not retry (`mutations: { retry: false }`
  in `App.tsx`), so a rotation is never replayed. Announcements name the credential only. Typed values live only in
  reducer state and the input is `type=password` with `autoComplete="new-password"`.
- The Arma 3 `server.cfg` renderer doubles quotes, and the CS2 `Quote` helper escapes quotes and backslashes. Token
  format (BattlEye `beserver.cfg`, CS2 RCON) is the only format with unquoted usage.

The defects are in the authorization model of the rotate response, the data-loss behaviour of the shown-once value, the
lack of any strength floor on chosen admin passwords, and the AGENTS.md documentation rule.

## Warnings

### WR-01: Rotate returns plaintext to manage-only callers and ignores the `Revealable` flag

**File:** `sources/Api/Services/GameServerSecrets/GameServerSecretService.cs:123-148` (and
`sources/Api/Controllers/GameServerSecretsController.cs:112-124`)
**Issue:** `RotateAsync` returns the generated value to any `ManageSecrets` caller without consulting
`secretDefinition.Revealable`, which `RevealAsync` enforces (line 113). The catalog already lets a game declare a
generated credential as non-revealable (`Generated: true, Revealable: false` is representable), and rotating such a
credential would now hand its plaintext to a caller who has neither `RevealSecrets` nor a revealable credential. Today
no registered credential has that combination, so this is latent. It also means `RevealSecrets` no longer separates
duties for generated credentials: a manage-only member can rotate to read a value, or choose one through the new PUT
path.
**Fix:** Return the value only when the definition allows it, and make that an enforced contract:
```csharp
if (!secretDefinition.Revealable)
    throw new HostingValidationException("This secret cannot be rotated because its value is never shown.");
```
Alternatively return `204` for non-revealable credentials. Document the manage-only exposure for revealable ones as an
accepted decision, and add a test for a generated non-revealable definition.

### WR-02: The shown-once value is destroyed silently on navigation, reload or a lost response

**File:** `sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx` (leave guard keyed on `count`), 
`sources/ReactApp/src/features/hosting/useServerChangeDraft.tsx:102-106`
**Issue:** `RotateAsync` retires the previous credential and commits the new one before the response reaches the
client. The new value is kept only in provider state. The unsaved-changes guard (D-10) is driven by the staged draft
count, which is already zero once the apply has removed the rotation. Leaving the Settings section, reloading, or the
tab crashing therefore drops the only copy with no warning. A member without `canRevealSecrets` cannot retrieve it and
must rotate again, which invalidates the credential a second time. The same applies when the response is lost after the
server committed: the rotation shows as failed and stays staged, while the credential has already changed.
**Fix:** Count a pending one-time value as guarded state (extend the leave guard and `beforeunload` while
`oneTimeValues.size > 0`), or tell the member in the one-time label that leaving discards it (the existing copy says
"shown only this once" but does not guard). Consider making the rotate endpoint idempotent per client-supplied request id
so a lost response can be replayed without a second rotation.

### WR-03: Chosen admin and RCON passwords have no minimum length or strength rule

**File:** `sources/Api/Services/GameServerSecrets/GameServerSecretService.cs:84, 96, 299-306`;
`sources/ReactApp/src/features/hosting/serverChangeDraft.ts:550-566`
**Issue:** The new chosen-value path accepts any non-blank value of one character or more for Arma 3 admin, server
command and BattlEye RCON passwords and for the CS2 RCON password. Those are internet-reachable administrative
credentials, previously platform-generated 64-character hex values. A one-character password is accepted by both the API
and the client mirror. The CloudWorker diagnostic redaction (`RememberDiagnosticSecret`) also keeps every remembered
secret and replaces occurrences in diagnostics, so a very short chosen value corrupts or over-redacts log output.
**Fix:** Add a per-credential `MinimumLength` (for example 8 for RCON and admin credentials) to `GameSecretDefinition`,
enforce it in `ValidateValue` with a fixed message, project it in the status DTO, and mirror it in
`credentialValueIssue`. At minimum, enforce a floor in the worker redaction (ignore secrets shorter than 4 characters
rather than corrupting logs).

### WR-04: Handwritten C# members touched by this work lack the XML documentation AGENTS.md requires

**File:** `sources/Api/Services/GameServerSecrets/GameServerSecretService.cs:31, 39, 105, 151, 166, 172, 178, 274`
**Issue:** AGENTS.md requires meaningful XML documentation on every C# member. The interface members were documented in
this work, but the implementation members changed here (`EnsureGeneratedSecrets`, `GetStatusesAsync`, `RevealAsync`,
`ResolveRequiredAsync`, `CreateProtectedSnapshotAsync`, `UnprotectSnapshot`, `MigrateLegacyConfigurationSecretsAsync`) have
no `<inheritdoc />`, and the private `AddVersion` (whose return type this work changed) has no documentation. The
`GameServerSecretsController.GetAsync` and others are documented, but `GameServerSecretService` is inconsistent with the
interface it implements.
**Fix:** Add `/// <inheritdoc />` to each implemented member, and a `<summary>`, `<param>` and `<returns>` block to
`AddVersion` stating that it returns the new version number.

## Info

### IN-01: `SingleLine` accepts Unicode line and paragraph separators

**File:** `sources/Api/Services/GameServerSecrets/GameServerSecretService.cs:301`;
`sources/ReactApp/src/features/hosting/serverChangeDraft.ts:559-562`
**Issue:** The `SingleLine` rule uses `char.IsControl`, which does not cover U+2028 (line separator) and U+2029
(paragraph separator). The enum documents the format as one that "fits on a single configuration line". The Arma 3
renderer does not treat those characters as line breaks, so there is no injection today, but the stated contract is
broader than the check. The client mirror has the same gap.
**Fix:** Also reject `char.GetUnicodeCategory(c)` of `LineSeparator` or `ParagraphSeparator` on both sides, or narrow the
documentation.

### IN-02: Error visibility flag `blurred` is never reset

**File:** `sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx:126, 277`
**Issue:** After the member blurs the input once, `blurred` stays true for the row's lifetime. After an apply or Undo
empties the input, the next typed value shows its validation error from the first keystroke.
**Fix:** Reset `blurred` when the staged `set` entry leaves the draft, using the same during-render adjustment already
used for `showTyped`.

### IN-03: Client-side `!generated ||` duplicates the API projection

**File:** `sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx:61-63`;
`sources/Api/Services/GameServerSecrets/GameServerSecretService.cs:67`
**Category:** necessity
**Issue:** The API already projects `AcceptsChosenValue = !Generated || AcceptsChosenValue`, so `secret.acceptsChosenValue`
alone is the complete answer. The client repeats the `!secret.generated ||` half, which cannot change the result and
would hide a future API change. The `GameSecretDefinition` documentation also says the flag is "ignored" for
non-generated secrets while the DTO documentation says "may supply", so the two meanings differ.
**Fix:** Use `secret.acceptsChosenValue` directly in `acceptsMemberValue` and align the two doc comments.

### IN-04: Locale keys are out of alphabetical order

**File:** `sources/ReactApp/src/locales/en.json:3205-3219` (and the matching `de.json` block)
**Issue:** `Server_Credentials_StagedSet` sits between `RotateStaged` and `RotateUndo`, and the `HideTyped` and
`Invalid_*` keys follow `ShowTyped`, while the rest of the block is sorted. This makes merges and review harder.
**Fix:** Sort the new keys into the existing order (or let the locale tooling sort the file).

### IN-05: Fixed-delay wait in the browser test

**File:** `sources/ReactApp/tests/e2e/hosting-files-secrets.spec.ts:605`
**Issue:** `page.waitForTimeout(750)` is a time-based wait and a common source of flakiness in the credential layout
and no-trace checks.
**Fix:** Wait on an observable condition (for example the toggle becoming disabled or a request being idle) with
`expect.poll` or a locator assertion.

### IN-06: Formatting typo in a service test

**File:** `tests/Api.Tests/GameServerHosting/GameServerSecretServiceTests.cs` (`SetAsync_ChosenValueForGeneratedSecret_StoresUserProvidedVersion`)
**Issue:** `var resolved =await service...` is missing a space and also repeats the `, CancellationToken.None` argument
style used nowhere else in the file.
**Fix:** Format as `var resolved = await service.ResolveRequiredAsync(seed.Server.PrimaryKey);`.

---

_Reviewed: 2026-11-21_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
