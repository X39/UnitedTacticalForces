# Deferred items found by plan 69-24

Out of scope for plan 69-24 (files owned by other plans); not fixed here.

| Item | Where | Evidence | Likely owner |
|------|-------|----------|--------------|
| The fleet page document title reads "Server fleet", but `hosting-fleet.spec.ts` ("the Command Center nav and the fleet page read Servers", chromium) expects a title matching `/Servers/`. | `tests/e2e/hosting-fleet.spec.ts:566`, route title of `/command/servers` | Fails at HEAD and after this plan; this plan touched no title or route file. | 69-09 (hosting core) or 69-26 (fleet slice) |
| The profile menu trigger keeps a focus outline that is not gold-bright (`rgb(232, 220, 178)`). | `ProfileMenu.css`, `button.account-profile__trigger`, 41 discovery runs in the phone projects | Re-run of the discovery pass on this branch. | 69-08 (already listed in the 69-06 findings table) |
| The public page editor has no visible `h1`. | `PublicPageEditorPage.tsx` | Re-run of the discovery pass on this branch (6 runs). | 69-18 (already listed in the 69-06 findings table) |
| The stacked dialog actions use `column-reverse`: the confirm action is first visually and the dismiss control first in tab order, so a keyboard user moves upward through the stack. D-10 fixes the visual order (confirm first) and initial focus on dismiss, so the shell was left as designed. | `src/components/shared-primitives.css` `.dialog__actions` at or below 520px | Observed while writing `dialog-geometry.spec.ts`. | Milestone verification (visual and keyboard judgment) |
