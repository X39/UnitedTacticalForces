# Phase 58 — UI Review

**Audited:** 2026-07-28
**Baseline:** approved `58-UI-SPEC.md`
**Screenshots:** not captured (no dev server responded on ports 3000, 5173, or 8080); code-only audit

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 1/4 | German localization is missing 69 of the 107 keys used by the page, so keys or fallback English leak into the UI. |
| 2. Visuals | 1/4 | The contracted tabbed workspace and primary next-action focal area are absent; the page is a long stack of equally weighted panels. |
| 3. Color | 2/4 | Semantic tokens are used, but many ad-hoc translucent colors and repeated gold accents weaken the specified 60/30/10 hierarchy. |
| 4. Typography | 1/4 | The stylesheet uses many undeclared sizes and weights, including 10–12px text and weights 800–900. |
| 5. Spacing | 2/4 | Responsive grids exist, but extensive arbitrary rem values and `clamp()` declarations violate the no-exceptions 4px spacing scale. |
| 6. Experience Design | 1/4 | Critical workflows are absent or blocked: no Logs tab, incomplete file browsing, no disk usage, no modpack controls, and profile editing disappears after deployment. |

**Overall: 8/24**

---

## Top 3 Priority Fixes

1. **Restore the unified, task-oriented workspace and game controls** — operators cannot reach the contracted Overview/Runtime/Cloud/Logs/Ownership/Files/Jobs/Audit experiences or configure Arma 3 essentials — reinstate section navigation, a state-ranked primary action, readable Logs tab, and game-capability-driven controls including modpack selection.
2. **Replace the file panel with a real two-context file explorer plus storage telemetry** — users cannot browse live/game-default files like Windows Explorer or verify whether the target disk can hold an install/upload — expose both persistent and live roots in one tree/list explorer with breadcrumbs, sorting, file-type icons, selection, and total/used/free capacity for the actual deployment disk.
3. **Repair localization and mutable runtime configuration** — German users see fallback text/keys and an active deployment cannot change profile — translate all 69 missing German keys, localize generated/status/fallback copy, and provide an explicit safe profile-change workflow (stop/redeploy confirmation when hot change is impossible) rather than hiding the editor.

---

## Detailed Findings

### Pillar 1: Copywriting (1/4)

- **BLOCKER — localization is incomplete.** `Details.razor` references 107 localization keys; 69 are absent from `Language.de.resx`. Missing strings cover loading/errors, file browsing, actions, jobs, deployment facts, deletion, and preservation. Examples are used at `Details.razor:19-29`, `Details.razor:286-293`, `Details.razor:386-397`, and `Details.razor:478-591`. This directly substantiates the reported broken localization.
- **WARNING — implementation values are exposed as UI copy.** Deployment status/stage and provider state are emitted verbatim at `Details.razor:145-149`, `Details.razor:188-190`, operation status/provider identifiers at `Details.razor:242-258`, and job action/kind/status/stage at `Details.razor:407-408`. These bypass task-oriented localized language.
- **WARNING — hard-coded English remains inside an otherwise localized page.** `Diagnostic reference:` is literal at `Details.razor:421`; `Price unavailable` and the hourly/monthly pricing sentence are literal at `Details.razor:1312-1317`.
- **BLOCKER — required copy states are absent.** The contract's `No server activity yet` heading/body and problem-plus-next-step error pattern do not appear. The operations empty state is a single localized paragraph at `Details.razor:227-230`, while broad exceptions are reduced to generic `UserFacingError` fallbacks at `Details.razor:792-795` and `Details.razor:900-903`.

### Pillar 2: Visuals (1/4)

- **BLOCKER — the contracted workspace hierarchy is missing.** There is no navigation for `Overview`, `Runtime`, `Cloud`, `Logs`, `Ownership`, `Files`, `Jobs`, or `Audit`; the only tab semantics on the entire page are the file-context control at `Details.razor:483-492`. Record, deployment, activities, every action, jobs, files, and preservation are rendered sequentially from `Details.razor:74-669`.
- **BLOCKER — no primary next-action focal point exists.** Every server action is rendered into equal cards in an auto-fit grid at `Details.razor:284-351`; CSS gives all actions the same two-column structure at `Details.razor.css:118-133`. This contradicts the required single state-driven primary area plus compact secondary row and makes urgent cost-stop action compete with routine actions.
- **WARNING — the activities layout is fragile.** Activity title and timestamp share a non-wrapping flex row (`Details.razor.css:339-348`), while metadata adds several provider fields (`Details.razor:241-275`). Long localized summaries, timestamps, resource identifiers, and trace IDs will compete for width; the mobile breakpoint only changes the section heading and job header, not `.hosting-operation-title` (`Details.razor.css:585-603`).
- **WARNING — file glyphs are not Fluent UI icons.** Folder/file markers are literal symbol characters at `Details.razor:536`, producing weaker recognition and inconsistent rendering compared with the specified Fluent icon system.

### Pillar 3: Color (2/4)

- **WARNING — core semantic tokens are respected for text, stroke, warning, danger, and selected file context** (`Details.razor.css:1-2`, `68-98`, `459-462`). This prevents a score of 1.
- **WARNING — the page introduces at least 13 ad-hoc `rgba(...)` declarations** for pills, cards, alternating activity rows, icon circles, and danger surfaces (`Details.razor.css:18`, `126`, `136`, `173`, `193-205`, `260`, `279-297`, `422`, `442`, `493`, `543`). These bypass the contract's existing custom-property palette.
- **WARNING — accent is not reserved to the declared 10% roles.** Gold is applied to every action-group heading, every action icon, command expanders, progress bars, and the active file context (`Details.razor.css:110-116`, `181-185`, `397-413`, `459-462`), diluting the prominence of the primary action—which is itself absent.

### Pillar 4: Typography (1/4)

- **BLOCKER — the declared four-role type system is not followed.** The stylesheet uses at least 12 distinct rem sizes: `0.62`, `0.68`, `0.7`, `0.72`, `0.74`, `0.75`, `0.76`, `0.78`, `0.8`, `0.82`, `0.84`, and `0.92rem` (for example `Details.razor.css:68-84`, `110-114`, `151-184`, `294-305`, `350-370`, `390-425`, `476-479`, `521-525`, `551-554`). The contract allows 12px labels, 14px body, 20px headings, and 24px display.
- **BLOCKER — very small text harms legibility.** Activity icons use `0.62rem` (`Details.razor.css:294-306`) and action metadata uses `0.68rem` (`Details.razor.css:172-179`), both below the 12px label role under the normal 16px root.
- **WARNING — weight usage is inconsistent and outside contract.** Labels and decorative text use weights `800`, `850`, and `900` (`Details.razor.css:68-72`, `172-185`, `227-232`, `294-305`, `408-413`, `476-480`), whereas the contract declares 400, 600, and 700.

### Pillar 5: Spacing (2/4)

- **WARNING — responsive collapse exists.** Two-column overview, mutation, and preservation grids collapse at 900px, and dense controls collapse at 680px (`Details.razor.css:577-603`, `615-619`).
- **WARNING — the no-exceptions spacing scale is not followed.** Examples include `0.45rem`, `0.65rem`, `0.8rem`, `0.85rem`, `1.1rem`, and `1.35rem` gaps/padding (`Details.razor.css:8-21`, `30-39`, `100-132`), none of which reliably map to the declared 4/8/16/24/32/48px scale.
- **WARNING — viewport-dependent spacing violates the fixed token contract.** Page and panel padding use `clamp(1rem, 2.5vw, 2rem)` and `clamp(1rem, 2vw, 1.35rem)` at `Details.razor.css:5` and `Details.razor.css:38`, generating arbitrary intermediate values.
- **WARNING — the long unsectioned page compounds spacing problems.** All major workflows are separated only by the page's 16px grid gap (`Details.razor.css:1-5`), rather than tab breaks or the specified 32–48px major-region rhythm.

### Pillar 6: Experience Design (1/4)

- **BLOCKER — the dedicated Logs experience is absent.** `Details.razor` has no Logs tab, log list, log download, or retry state. This is a direct regression from the Phase 58 contract and summaries and prevents log reading in the selected-server workspace.
- **BLOCKER — storage is not an Explorer-like unified experience.** The UI renders one flat list with a manually editable path and parent/open buttons (`Details.razor:483-560`). Although `EHostingFileContext` defines `Persistent` and `Live` (`EHostingFileContext.cs:3-7`), the UI renders only the Persistent context (`Details.razor:483-492`). Live/game-default files are therefore absent rather than navigable alongside persistent data. There is no tree, breadcrumb segments, column headers, sorting controls, multi-selection, rename/move, or familiar file-type icons.
- **BLOCKER — disk capacity cannot be checked.** `RuntimeProfileInfo` carries only provisioned `DiskGiB` (`RuntimeProfileInfo.cs:3-12`), but `ProfileOption` omits it (`Details.razor:1307-1310`), and no used/free disk telemetry exists on the page. File entries expose individual sizes only (`Details.razor:531-537`), so operators cannot validate install/upload headroom on the actual disk.
- **BLOCKER — runtime profile changes are hidden for deployed servers.** The editor is rendered only when `_server.Deployment is null` (`Details.razor:109-128`). There is no explanation or safe stop/redeploy path when a deployment exists, making the reported “cannot be changed” behavior expected for active servers rather than recoverable UX.
- **BLOCKER — Arma 3 controls are incomplete.** Actions come from generic server actions (`Details.razor:291-351`), but no modpack selector or repository call appears anywhere in the page. The Arma definition offers start/stop/restart/install/update/log actions but no modpack configuration action (`Arma3GameServerDefinitionRegistration.cs:17-77`). An Arma operator cannot select the community modpack needed for launch.
- **BLOCKER — action discovery violates the interaction contract.** Instead of state-relevant actions, all returned actions—including disabled ones—remain visible (`Details.razor:291-351`). This creates the disabled command wall the contract explicitly prohibits.
- **WARNING — some baseline state handling is present.** Page/file loading states, operation errors, empty file/job/activity states, disabled mutation controls, and destructive confirmations exist (`Details.razor:16-31`, `65-72`, `227-230`, `354-379`, `395-397`, `494-524`, `946-952`, `1126-1132`). These safeguards do not compensate for missing task completion paths.

---

## Files Audited

- `.planning/phases/58-unified-server-workspace/58-UI-SPEC.md`
- `.planning/phases/58-unified-server-workspace/58-CONTEXT.md`
- `.planning/phases/58-unified-server-workspace/58-01-PLAN.md` through `58-04-PLAN.md`
- `.planning/phases/58-unified-server-workspace/58-01-SUMMARY.md` through `58-04-SUMMARY.md`
- `.planning/phases/58-unified-server-workspace/58-VERIFICATION.md`
- `sources/WebApp/Pages/Servers/Details.razor`
- `sources/WebApp/Pages/Servers/Details.razor.css`
- `sources/WebApp/Properties/Language.resx`
- `sources/WebApp/Properties/Language.de.resx`
- `sources/WebApp/Services/GameServerRepository/EHostingFileContext.cs`
- `sources/WebApp/Services/GameServerRepository/RuntimeProfileInfo.cs`
- `sources/WebApp/Services/GameServerRepository/GameServerInfo.cs`
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`
- `sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs`
- `sources/Api/Controllers/GameServerFilesController.cs`
- `sources/Api/Controllers/GameServersController.cs`
- `sources/Api/Services/GameServerDefinition/Arma3GameServerDefinitionRegistration.cs`
- `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs`

Registry audit skipped: `components.json` is absent and the UI spec declares no third-party registry blocks.
