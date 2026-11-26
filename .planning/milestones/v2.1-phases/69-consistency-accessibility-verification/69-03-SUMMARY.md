---
phase: 69-consistency-accessibility-verification
plan: 03
subsystem: ui
tags: [react, i18n, state-views, badge-tones, navigation, localization]

requires:
  - phase: 61-design-system
    provides: "StatePanel, StatusBadge, ActionButton and the Common_LoadingData text the shared views build on"
provides:
  - "LoadingState, EmptyState, ErrorState, ForbiddenState and AccessBoundary over StatePanel (src/components/StateViews.tsx)"
  - "Closed five-tone badge lexicon with toneOf and a neutral fallback (src/components/statusTones.ts)"
  - "Closed sentence-case navigation label list in English and German with a source-scan test"
  - "142 new en/de keys for Wave 2: dismiss labels, hard-coded text replacements, hosting overview copy, empty-state titles and bodies, named Files row actions"
affects: [69-04, 69-05, Wave 2 conversion plans, 69-33]

plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: 99252c12e9e3c96c9b3f906a8873bcfce827c86e

actuals:
  tokens: 13800
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Shared state views over StatePanel with required empty title and body, generic Common_State_ keys elsewhere"
    - "Badge tone lookup through one ReadonlyMap per domain and toneOf, unknown key to neutral"
    - "Closed navigation label contract in tests/support/navLabelContract.ts scanned against the two navigation source files"

key-files:
  created:
    - sources/ReactApp/src/components/StateViews.tsx
    - sources/ReactApp/src/components/statusTones.ts
    - sources/ReactApp/tests/state-views.test.tsx
    - sources/ReactApp/tests/status-tones.test.ts
    - sources/ReactApp/tests/support/navLabelContract.ts
    - sources/ReactApp/tests/locales/nav-labels.test.ts
    - .planning/phases/69-consistency-accessibility-verification/deferred-items-69-03.md
  modified:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/features/admin/VereinSettingsPage.tsx
    - sources/ReactApp/tests/admin-verein-settings.test.tsx

key-decisions:
  - "Four lexicon-driven tone changes are recorded below; every other mapping reproduces the replaced page-local function."
  - "Nine generic cancel sites beyond the plan's list received dismiss keys because D-11 bans the generic label in every dialog."
  - "Empty-state keys under Server_Mods_ and Server_Credentials_ were renamed to Server_ModPack_ and Server_Secrets_ because tests/locales/server-keys.test.ts closes those two prefixes."
  - "Terrain and scenario fallback labels reuse Server_Overview_Terrain and Server_Overview_Scenario instead of new keys."

patterns-established:
  - "Dismiss key: {Area}_{Dialog}_Dismiss with the D-11 pattern (Keep X, Don't verb X, Leave without saving, Discard changes)"
  - "Empty state: {Area}_EmptyTitle plus {Area}_EmptyBody, or NoSelectionTitle plus NoSelectionBody for a detail pane"

requirements-completed: [CON-02, CON-03, L10N-03]

coverage:
  - id: D1
    description: "Shared LoadingState, EmptyState, ErrorState, ForbiddenState and AccessBoundary render with the right live region, copy and retry in English and German"
    requirement: CON-02
    verification:
      - kind: unit
        ref: "tests/state-views.test.tsx"
        status: pass
    human_judgment: false
  - id: D2
    description: "Closed tone lexicon with a neutral fallback for unknown keys, one map per domain"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/status-tones.test.ts"
        status: pass
    human_judgment: false
  - id: D3
    description: "Navigation labels equal the closed sentence-case list in both languages and a source scan fails on a label key outside the list"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/locales/nav-labels.test.ts"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/public-shell.spec.ts (28 passed)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Every string Wave 2 consumes exists in en.json and de.json with distinct German text"
    requirement: L10N-03
    verification:
      - kind: other
        ref: "npm run locales:check (3980 keys, 0 failures, 0 warnings)"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 03: Shared States, Tone Lexicon, Closed Nav Labels and Wave 2 Strings Summary

**Five shared state views over StatePanel, a closed five-tone badge lexicon with a neutral fallback, a test-enforced sentence-case navigation list, and 142 English and German keys that let every Wave 2 plan convert pages without touching the locale files.**

## Performance

- **Duration:** about 35 min
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files created:** 7 (including this plan's deferred-items file); modified: 4

## Accomplishments

- `StateViews.tsx`: `LoadingState` (status, spinner, "Loading..."), `EmptyState` (required title and body, optional single action), `ErrorState` (alert, generic title and body, ghost "Try again" bound to `onRetry`, title, body and retry overrides), `ForbiddenState` and `AccessBoundary` (presentation only, documented as such; the test asserts no request is made).
- `statusTones.ts`: `StatusTone`, `toneOf`, and one `ReadonlyMap` per domain. A type-level equality assertion in the test keeps `StatusTone` identical to the `StatusBadge` tone union without editing `primitives.tsx`.
- Navigation: seven values changed to the UI-SPEC list, `tests/support/navLabelContract.ts` holds the closed list including group labels and the three recorded h1 exceptions (Overview, Contribution settings, Public images), and `tests/locales/nav-labels.test.ts` scans `commandNavigation.tsx` and `CommunityLayout.tsx` and fails on a label key outside the list.
- Locale files: 5 `Common_State_` keys plus 142 consistency keys, all in sorted positions, German distinct from English everywhere.

## Task Commits

1. **Task 1 (tracer): shared state views and their generic keys** - `37d417c4` (feat)
2. **Task 2: closed tone lexicon** - `32bb1793` (feat)
3. **Task 3a: navigation labels aligned with the closed list** - `082d30ad` (feat)
4. **Task 3b: strings for the consistency pass** - `99252c12` (feat)

The plan metadata commit follows (this file and `deferred-items-69-03.md`). Commits were created unsigned because gpg signing timed out, as the owner's standing instruction allows.

## Tone lexicon notes (Wave 2 reads this)

Map keys per domain, to be passed to `toneOf(map, key)`:

| Map | Keys and tones |
|-----|----------------|
| `accountTones` | verified success, unverified neutral, banned danger, deleted danger (`memberStatus` values) |
| `membershipTones` | Active success, Suspended warning, Ended neutral; no formal membership (undefined) resolves to neutral |
| `membershipRequestTones` | Submitted warning, AcceptedForContact info, Approved success, Rejected danger, Withdrawn neutral |
| `attendanceTones` | Accepted success, Maybe warning, Rejected danger, Unassigned neutral (`eventAttendance` values) |
| `operationTones` | ready success, needs-signups warning (`eventReadiness` values) |
| `eventTones` | ready success, low-slots warning, hidden neutral, archived neutral |
| `slotTones` | hidden neutral, assigned success, self-assignable info, unassigned warning |
| `reportTones` | published success, scheduled info, draft neutral, deleted danger |
| `settingTones` | off neutral, configured success, missing-configuration warning |
| `financeTones` | active success, frozen warning, income success, expense danger |
| `jobTones` | Queued/Running/WaitingForWorker info, RetryPending warning, Succeeded success, Failed danger, Skipped/CancelledByCostStop neutral |

Changed mappings (the UI-SPEC meaning table names the value as neutral):

| Value | Replaced function | Was | Now |
|-------|-------------------|-----|-----|
| unverified account | `MembersPage.statusTone`, `MemberDetailPage.statusTone` | warning | neutral ("Not verified") |
| no formal membership | `MemberDetailPage.membershipTone` | info | neutral ("Not a member") |
| hidden event | `EventAdminPage` state table | info | neutral ("Hidden") |
| draft field report | `FieldReportsPage.stateTone` | warning | neutral ("Draft") |

Unchanged against the replaced function: attendance, operation readiness, slot status, setting status and membership request status. `membershipRequestTones` is one export more than the plan's interface list names; the plan asked for the request statuses to be covered and no listed map fits them.

## Key map

Grouped by origin. Keys marked "reuse" need no new key; the Wave 2 plan switches the call site to the existing key. The "Replaces" column names the generic or literal text the new key retires (the retired key is deleted in 69-33, not here).

### Reuse of existing keys

| File and line | Replaces | Existing key to use | EN | DE |
|---------------|----------|---------------------|----|----|
| `features/hosting/components.tsx:43` | copy.ts `loading` | `Common_LoadingData` (LoadingState default) | Loading... | Lade... |
| `features/hosting/FleetPages.tsx:62` | copy.ts `totalServers` | `Hosting_Servers_Title` | Servers | Server |
| `features/hosting/FleetPages.tsx:63, 71` | copy.ts `possibleCost` | `Hosting_Servers_PossibleCost` | Possible cost | Mögliche Kosten |
| `features/hosting/FleetPages.tsx:94` | copy.ts `fleet` | `Shared_CommandCenter_CurrentArea_ServerFleet` | Server fleet | Serverflotte |
| `features/hosting/FleetPages.tsx:95` | copy.ts `configurationProfiles` | `Hosting_ConfigurationProfiles_Title` | Configuration profiles | Konfigurationsprofile |
| `features/hosting/FleetPages.tsx:96` | copy.ts `finance` | `HostingFinance_Title` | Hosting finance | Hosting-Finanzen |
| `features/hosting/FleetPages.tsx:97` | copy.ts `createServer` | `Hosting_Servers_Create` | Create server | Server erstellen |
| `features/hosting/ServerRuntimePanel.tsx:309`, `CreateServerPage.tsx:156` | literal `"Terrain"` fallback | `Server_Overview_Terrain` | Terrain | Karte |
| `features/hosting/ServerRuntimePanel.tsx:327`, `CreateServerPage.tsx:156` | literal `"Scenario"` fallback | `Server_Overview_Scenario` | Scenario | Szenario |
| `features/admin/EventCreatePage.tsx:390`, `EventEditPage.tsx:540` | literal `label="Scenario"` | `Pages_Events_Create_Step_Scenario_Label` | Scenario | Szenario |
| `features/admin/PublicPagesPage.tsx:123` | empty body | `Pages_Command_Content_Pages_NoRows` (names the next step; title is new) | | |
| `features/hosting/sections/ActivitySection.tsx:241` | empty body in the no-problems branch | `Server_Activity_NoProblems`, action `Server_Activity_ShowAll` | | |
| `features/wiki/WikiPage.tsx:735, 798` | empty body | `Pages_Wiki_Index_SelectPageForRevisions`, `Pages_Wiki_Index_SelectPageForDetails` (title is new) | | |
| `features/wiki/WikiPage.tsx:397` (no pages yet) | empty title | `Pages_Wiki_Index_NoPagesYet` (body is new) | | |

### New keys

| File and line | Replaces | New key | EN | DE |
|---------------|----------|---------|----|----|
| `features/admin/MemberDetailPage.tsx:540` | Common_Cancel | `Pages_Command_Member_ProfileReset_Dismiss` | Discard profile changes | Profiländerungen verwerfen |
| `features/admin/MemberDetailPage.tsx:631` | Common_Cancel | `Pages_Command_Member_MembershipReset_Dismiss` | Discard membership changes | Mitgliedschaftsänderungen verwerfen |
| `features/admin/MemberDetailPage.tsx:717 (ban branch)` | Common_Cancel | `Pages_Command_Member_ConfirmBan_Dismiss` | Don't ban member | Mitglied nicht sperren |
| `features/admin/MemberDetailPage.tsx:717 (end membership branch)` | Common_Cancel | `Pages_Command_Member_ConfirmEndMembership_Dismiss` | Keep membership | Mitgliedschaft behalten |
| `features/admin/PublicPageEditorPage.tsx:488, 540` | Common_Cancel | `Pages_Command_Content_PageEditorLeave_Dismiss` | Leave without saving | Ohne Speichern verlassen |
| `features/admin/RolesPage.tsx:285` | Common_Cancel | `Pages_Command_Roles_CreateRole_Dismiss` | Don't create role | Rolle nicht erstellen |
| `features/admin/RoleDetailPage.tsx:598` | Common_Cancel | `Pages_Command_Roles_DeleteRole_Dismiss` | Keep role | Rolle behalten |
| `features/admin/TerrainDetailPage.tsx:346` | Common_Cancel | `Pages_Data_Terrains_All_ConfirmDeleteTerrain_Dismiss` | Keep terrain active | Karte aktiv lassen |
| `features/admin/VereinDocumentEditorPage.tsx:325` | Common_Cancel | `Pages_Command_Verein_DocumentEditorLeave_Dismiss` | Leave without saving | Ohne Speichern verlassen |
| `features/admin/VereinKassenbuchEditorPage.tsx:327` | Common_Cancel | `Pages_Command_Verein_LedgerEditorLeave_Dismiss` | Leave without saving | Ohne Speichern verlassen |
| `features/events-member/AccountSettingsPage.tsx:496` | Common_Cancel | `Pages_Settings_AccountSettings_DeleteAccount_Confirm_Dismiss` | Keep account | Konto behalten |
| `features/events-member/EventDetailPage.tsx:626` | Common_Cancel | `Dialogs_ConfirmSlotLoss_Dismiss` | Keep slot | Platz behalten |
| `features/hosting/ConfigurationProfileWorkspacePage.tsx:400` | Hosting_Common_Cancel | `Hosting_ConfigurationProfiles_Archive_Dismiss` | Keep profile active | Profil aktiv lassen |
| `features/hosting/FinanceDialog.tsx:44 via HostingFinanceAccountPage (payment)` | Common_Cancel | `HostingFinance_Payment_Dismiss` | Don't record payment | Zahlung nicht erfassen |
| `FinanceDialog via HostingFinanceAccountPage (refund)` | Common_Cancel | `HostingFinance_Refund_Dismiss` | Don't record refund | Erstattung nicht erfassen |
| `FinanceDialog via HostingFinanceAccountPage (allowance)` | Common_Cancel | `HostingFinance_Allowance_Dismiss` | Keep current allowance | Aktuellen Verfügungsrahmen behalten |
| `FinanceDialog via HostingFinanceAccountPage (correction)` | Common_Cancel | `HostingFinance_Correction_Dismiss` | Keep cost owner | Kostenträger behalten |
| `FinanceDialog via HostingFinanceAccountPage (rename)` | Common_Cancel | `HostingFinance_Rename_Dismiss` | Keep account name | Kontonamen behalten |
| `FinanceDialog via HostingFinanceAccountPage (freeze)` | Common_Cancel | `HostingFinance_Freeze_Dismiss` | Don't freeze account | Konto nicht sperren |
| `FinanceDialog via HostingFinanceAccountPage (unfreeze)` | Common_Cancel | `HostingFinance_Unfreeze_Dismiss` | Keep account frozen | Konto gesperrt lassen |
| `FinanceDialog via HostingFinanceAccountPage (reversal)` | Common_Cancel | `HostingFinance_Reversal_Dismiss` | Don't reverse entry | Buchung nicht stornieren |
| `FinanceDialog via HostingFinancePage.tsx:226` | Common_Cancel | `HostingFinance_AddAccount_Dismiss` | Don't add account | Konto nicht hinzufügen |
| `features/hosting/ServerFilesPanel.tsx:988` | Common_Cancel | `React_ServerFiles_NewFolder_Dismiss` | Don't create folder | Ordner nicht erstellen |
| `features/hosting/ServerFilesPanel.tsx:1126` | Common_Cancel | `React_ServerFiles_Upload_Dismiss` | Don't upload files | Dateien nicht hochladen |
| `features/hosting/ServerFilesPanel.tsx:1176` | Common_Cancel | `React_ServerFiles_DeleteFile_Dismiss` | Keep file | Datei behalten |
| `features/admin/EventEditPage.tsx:834` | Cancel (text key) | `React_EventAuthoring_RemoveObjective_Dismiss` | Keep objective | Ziel behalten |
| `features/admin/EventSlotPlanner.tsx:395` | Cancel (text key) | `React_EventAuthoring_RemoveSlot_Dismiss` | Keep slot | Platz behalten |
| `features/admin/ModPackWorkspacePage.tsx:381` | Cancel (text key) | `React_ModPackWorkspace_Deactivate_Dismiss` | Keep mod pack active | Mod-Paket aktiv lassen |
| `features/admin/FieldReportEditorPage.tsx:644` | Pages_Command_FieldReports_Edit_ButtonCancel | `Pages_Command_FieldReports_Edit_DeleteReport_Dismiss` | Keep field report | Feldbericht behalten |
| `features/wiki/WikiPage.tsx:630` | Pages_Wiki_Index_Cancel | `Pages_Wiki_Index_ArchivePage_Dismiss` | Keep page active | Seite aktiv lassen |
| `features/wiki/WikiPage.tsx:972` | Pages_Wiki_Index_Cancel | `Pages_Wiki_Index_EditorLeave_Dismiss` | Leave without saving | Ohne Speichern verlassen |
| `components/CommunityLayout.tsx:219` | Shared_CommunityDialog_Cancel | `Shared_CommunityDialog_Login_Dismiss` | Don't sign in | Nicht anmelden |
| `features/admin/GalleryAssetPicker.tsx:238` | Components_GalleryImagePicker_DialogCancel | `Components_GalleryImagePicker_Dismiss` | Don't choose an image | Kein Bild auswählen |
| `features/admin/VereinKassenbuchEditorPage.tsx:409` | Pages_Command_Verein_KassenbuchDeleteCancel | `Pages_Command_Verein_KassenbuchDelete_Dismiss` | Keep ledger entry | Kassenbucheintrag behalten |
| `features/hosting/ServerRuntimePanel.tsx:303` | literal "Content" | `React_ServerWorkspace_ContentKicker` | Content | Inhalt |
| `features/hosting/ServerRuntimePanel.tsx:304` | literal "Terrain and scenario" | `React_ServerWorkspace_ContentTitle` | Terrain and scenario | Karte und Szenario |
| `features/hosting/ServerRuntimePanel.tsx:341` | literal sentence | `React_ServerWorkspace_ContentFollowsLatest` | Unpinned selections follow the latest compatible version. | Nicht festgelegte Auswahlen folgen der neuesten kompatiblen Version. |
| `features/hosting/ServerRuntimePanel.tsx:355` | literal button text | `React_ServerWorkspace_SaveContentSelection` | Save content selection | Inhaltsauswahl speichern |
| `features/admin/EventCreatePage.tsx:391, features/admin/EventEditPage.tsx:541` | literal placeholder | `React_EventAuthoring_ScenarioSearchPlaceholder` | Search scenarios | Szenarien suchen |
| `features/admin/ModPackWorkspacePage.tsx:248` | t("Content") (no such key) | `React_ModPackWorkspace_Tab_Content` | Content | Inhalt |
| `features/admin/ModPackWorkspacePage.tsx:248 (target danger)` | t("Danger") (no such key) | `React_ModPackWorkspace_Tab_Danger` | Danger zone | Gefahrenbereich |
| `features/hosting/FleetPages.tsx:61, 92, 93, 117` | copy.ts commandTitle | `Hosting_Overview_Title` | Command overview | Kommandoübersicht |
| `features/hosting/FleetPages.tsx:117` | copy.ts commandDescription | `Hosting_Overview_Description` | Live hosting posture, cost exposure, and operator shortcuts. | Aktuelle Hosting-Lage, Kostenrisiken und Operator-Kurzbefehle. |
| `features/hosting/FleetPages.tsx:64` | copy.ts pendingJobs | `Hosting_Overview_PendingWork` | Pending work | Offene Arbeit |
| `features/hosting/FleetPages.tsx:65, 70` | copy.ts attention | `Hosting_Overview_CostAttention` | Cost attention | Kostenhinweise |
| `features/hosting/FleetPages.tsx:73` | copy.ts noAttention | `Hosting_Overview_NoCostAttention` | No authorized server currently reports possible provider cost. | Kein autorisierter Server meldet derzeit mögliche Providerkosten. |
| `features/hosting/components.tsx:101` | copy.ts noActions | `Hosting_Detail_NoActions` | No lifecycle action is currently available. | Derzeit ist keine Lebenszyklusaktion verfügbar. |
| `features/hosting/ServerFilesPanel.tsx inline row open action` | visible label plus generic name | `React_ServerFiles_OpenNamed` | Open {{name}} | {{name}} öffnen |
| `features/hosting/ServerFilesPanel.tsx inline row download action` | visible label plus generic name | `React_ServerFiles_DownloadNamed` | Download {{name}} | {{name}} herunterladen |
| `features/hosting/ServerFilesPanel.tsx inline row delete action` | visible label plus generic name | `React_ServerFiles_DeleteNamed` | Delete {{name}} | {{name}} löschen |
| `components/server/ModPackPanel.tsx:266` | kind=empty without title (body Server_Mods_Empty kept only if it names the next step) | `Server_ModPack_EmptyTitle` | No mod packs for this game | Keine Mod-Pakete für dieses Spiel |
| `components/server/ModPackPanel.tsx:266` | body that names the next step | `Server_ModPack_EmptyBody` | Ask an administrator to add a mod pack for this game. | Bitte eine Administration, ein Mod-Paket für dieses Spiel anzulegen. |
| `features/admin/ClaimsPage.tsx:257` | Pages_Command_Claims_NoClaims (title) | `Pages_Command_Claims_EmptyTitle` | No claims match | Keine passenden Claims |
| `features/admin/ClaimsPage.tsx:257` | Pages_Command_Claims_NoClaims (body) | `Pages_Command_Claims_EmptyBody` | Clear the search or filters to see all claims. | Lösche die Suche oder die Filter, um alle Claims zu sehen. |
| `features/admin/ClaimsPage.tsx:335` | Pages_Command_Claims_SelectClaim (title) | `Pages_Command_Claims_NoSelectionTitle` | No claim selected | Kein Claim ausgewählt |
| `features/admin/ClaimsPage.tsx:335` | Pages_Command_Claims_SelectClaim (body) | `Pages_Command_Claims_NoSelectionBody` | Select a claim from the list to see how it is assigned. | Wähle einen Claim aus der Liste, um zu sehen, wie er zugewiesen ist. |
| `features/admin/EventAdminPage.tsx:286` | Pages_Events_All_NoEventsMatch (title) | `Pages_Events_All_EmptyTitle` | No events match | Keine passenden Veranstaltungen |
| `features/admin/EventAdminPage.tsx:286` | Pages_Events_All_NoEventsMatch (body) | `Pages_Events_All_EmptyBody` | Clear the search or filters to see all events. | Lösche die Suche oder die Filter, um alle Veranstaltungen zu sehen. |
| `features/admin/FieldReportsPage.tsx:256` | Pages_Command_FieldReports_NoReports (title) | `Pages_Command_FieldReports_EmptyTitle` | No field reports match | Keine passenden Feldberichte |
| `features/admin/FieldReportsPage.tsx:256` | Pages_Command_FieldReports_NoReports (body) | `Pages_Command_FieldReports_EmptyBody` | Clear the search or filters, or create a field report. | Lösche die Suche oder die Filter oder erstelle einen Feldbericht. |
| `features/admin/GalleryAssetPicker.tsx:168` | Components_GalleryImagePicker_NoResults (title) | `Components_GalleryImagePicker_EmptyTitle` | No images match | Keine passenden Bilder |
| `features/admin/GalleryAssetPicker.tsx:168` | Components_GalleryImagePicker_NoResults (body) | `Components_GalleryImagePicker_EmptyBody` | Try a different search term. | Versuche einen anderen Suchbegriff. |
| `features/admin/MediaGalleryPage.tsx:351` | Pages_Command_Gallery_Empty (title) | `Pages_Command_Gallery_EmptyTitle` | No gallery images match | Keine passenden Galeriebilder |
| `features/admin/MediaGalleryPage.tsx:351` | Pages_Command_Gallery_Empty (body) | `Pages_Command_Gallery_EmptyBody` | Clear the filters to see all images. | Lösche die Filter, um alle Bilder zu sehen. |
| `features/admin/MediaGalleryPage.tsx:415` | Pages_Command_Gallery_NoSelection (title) | `Pages_Command_Gallery_NoSelectionTitle` | No image selected | Kein Bild ausgewählt |
| `features/admin/MediaGalleryPage.tsx:415` | Pages_Command_Gallery_NoSelection (body) | `Pages_Command_Gallery_NoSelectionBody` | Select an image to see its metadata, tags and usage. | Wähle ein Bild, um Metadaten, Tags und Verwendung zu sehen. |
| `features/admin/MembersPage.tsx:354` | Pages_Command_Members_NoRows (title) | `Pages_Command_Members_EmptyTitle` | No members match | Keine passenden Mitglieder |
| `features/admin/MembersPage.tsx:354` | Pages_Command_Members_NoRows (body) | `Pages_Command_Members_EmptyBody` | Clear the search or filters to see all members. | Lösche die Suche oder die Filter, um alle Mitglieder zu sehen. |
| `features/admin/ModPackCreatePage.tsx:102` | t("No registered game supports mod packs.") (no such key) | `Pages_ModPacks_Create_NoGameTitle` | No game supports mod packs | Kein Spiel unterstützt Mod-Pakete |
| `features/admin/ModPackCreatePage.tsx:102` | body that names the next step | `Pages_ModPacks_Create_NoGameBody` | Add a game that supports mod packs, then come back. | Füge ein Spiel hinzu, das Mod-Pakete unterstützt, und komm dann zurück. |
| `features/admin/ModPackWorkspacePage.tsx:258` | Auto_ModPackNotFound_7d5fe348 (title) | `Pages_ModPacks_Workspace_NotFoundTitle` | Mod pack not found | Mod-Paket nicht gefunden |
| `features/admin/ModPackWorkspacePage.tsx:258` | body that names the next step | `Pages_ModPacks_Workspace_NotFoundBody` | It may have been deleted. Go back to the list of mod packs. | Es wurde möglicherweise gelöscht. Geh zurück zur Liste der Mod-Pakete. |
| `features/admin/ModPackWorkspacePage.tsx:474` | t("No mods are resolved for this revision.") (no such key) | `Pages_ModPacks_Workspace_NoModsTitle` | No mods in this revision | Keine Mods in dieser Revision |
| `features/admin/ModPackWorkspacePage.tsx:474` | body that names the next step | `Pages_ModPacks_Workspace_NoModsBody` | Edit the mod pack to add mods. | Bearbeite das Mod-Paket, um Mods hinzuzufügen. |
| `features/admin/ModPackWorkspacePage.tsx:512` | t("No nested mod packs.") (no such key) | `Pages_ModPacks_Workspace_NoNestedTitle` | No nested mod packs | Keine verschachtelten Mod-Pakete |
| `features/admin/ModPackWorkspacePage.tsx:512` | body that names the next step | `Pages_ModPacks_Workspace_NoNestedBody` | Add a nested mod pack in the editor to include its mods. | Füge im Editor ein verschachteltes Mod-Paket hinzu, um dessen Mods einzubinden. |
| `features/admin/ModPackWorkspacePage.tsx:553` | t("This game has no custom mod-pack file format...") (no such key) | `Pages_ModPacks_Workspace_NoFormatTitle` | No custom file format | Kein eigenes Dateiformat |
| `features/admin/ModPackWorkspacePage.tsx:553` | same sentence as the text-key literal | `Pages_ModPacks_Workspace_NoFormatBody` | This game has no custom mod-pack file format. Use the normalized list above. | Dieses Spiel hat kein eigenes Dateiformat für Mod-Pakete. Nutze die normalisierte Liste oben. |
| `features/admin/ModPacksPage.tsx:226` | Auto_NoModPacksMatchTheCurrentFilters_33565b66 (title) | `Pages_ModPacks_EmptyTitle` | No mod packs match | Keine passenden Mod-Pakete |
| `features/admin/ModPacksPage.tsx:226` | Auto_NoModPacksMatchTheCurrentFilters_33565b66 (body) | `Pages_ModPacks_EmptyBody` | Clear the search or filters to see all mod packs. | Lösche die Suche oder die Filter, um alle Mod-Pakete zu sehen. |
| `features/admin/PublicPagesPage.tsx:123` | title; body reuses Pages_Command_Content_Pages_NoRows (it names the next step) | `Pages_Command_Content_Pages_EmptyTitle` | No public pages yet | Noch keine öffentlichen Seiten |
| `features/admin/RoleDetailPage.tsx:331` | Pages_Command_Roles_NoClaimsMatch (title) | `Pages_Command_Roles_ClaimsEmptyTitle` | No claims match | Keine passenden Claims |
| `features/admin/RoleDetailPage.tsx:331` | Pages_Command_Roles_NoClaimsMatch (body) | `Pages_Command_Roles_ClaimsEmptyBody` | Clear the search to see all claims. | Lösche die Suche, um alle Claims zu sehen. |
| `features/admin/RoleDetailPage.tsx:508 (assigned view)` | Pages_Command_Roles_NoAssignedMembers (title) | `Pages_Command_Roles_NoAssignedMembersTitle` | No members assigned | Keine Mitglieder zugewiesen |
| `features/admin/RoleDetailPage.tsx:508 (assigned view)` | Pages_Command_Roles_NoAssignedMembers (body) | `Pages_Command_Roles_NoAssignedMembersBody` | Switch to available members to assign someone to this role. | Wechsle zu den verfügbaren Mitgliedern, um jemanden dieser Rolle zuzuweisen. |
| `features/admin/RoleDetailPage.tsx:508 (available view)` | Pages_Command_Roles_NoAvailableMembers (title) | `Pages_Command_Roles_NoAvailableMembersTitle` | No available members match | Keine passenden verfügbaren Mitglieder |
| `features/admin/RoleDetailPage.tsx:508 (available view)` | Pages_Command_Roles_NoAvailableMembers (body) | `Pages_Command_Roles_NoAvailableMembersBody` | Clear the search to see everyone who can be assigned. | Lösche die Suche, um alle Mitglieder zu sehen, die zugewiesen werden können. |
| `features/admin/RolesPage.tsx:318` | Pages_Command_Roles_NoRolesMatch (title) | `Pages_Command_Roles_EmptyTitle` | No roles match | Keine passenden Rollen |
| `features/admin/RolesPage.tsx:318` | Pages_Command_Roles_NoRolesMatch (body) | `Pages_Command_Roles_EmptyBody` | Clear the search or filters, or create a role. | Lösche die Suche oder die Filter oder erstelle eine Rolle. |
| `features/admin/SettingsPage.tsx:191` | Pages_Command_Settings_NoRowsReturned (title) | `Pages_Command_Settings_NoRowsTitle` | No settings to show | Keine Einstellungen zum Anzeigen |
| `features/admin/SettingsPage.tsx:191` | Pages_Command_Settings_NoRowsReturned (body) | `Pages_Command_Settings_NoRowsBody` | Refresh the page. If this stays empty, check the API configuration. | Aktualisiere die Seite. Bleibt die Liste leer, prüfe die API-Konfiguration. |
| `features/admin/SettingsPage.tsx:263` | Pages_Command_Settings_NoFeatureSelected (title) | `Pages_Command_Settings_NoSelectionTitle` | No setting selected | Keine Einstellung ausgewählt |
| `features/admin/SettingsPage.tsx:263` | Pages_Command_Settings_NoFeatureSelected (body) | `Pages_Command_Settings_NoSelectionBody` | Select a settings row to see its redacted readiness detail. | Wähle eine Zeile, um die geschwärzten Details zur Bereitschaft zu sehen. |
| `features/admin/TerrainsPage.tsx:202` | Pages_Data_Terrains_All_NoTerrainsMatch (title) | `Pages_Data_Terrains_All_EmptyTitle` | No terrains match | Keine passenden Karten |
| `features/admin/TerrainsPage.tsx:202` | Pages_Data_Terrains_All_NoTerrainsMatch (body) | `Pages_Data_Terrains_All_EmptyBody` | Clear the filter to see all terrains. | Lösche den Filter, um alle Karten zu sehen. |
| `features/admin/VereinDocumentsPage.tsx:192` | Pages_Command_Verein_DocumentsNoRows (title) | `Pages_Command_Verein_DocumentsEmptyTitle` | No documents match | Keine passenden Dokumente |
| `features/admin/VereinDocumentsPage.tsx:192` | Pages_Command_Verein_DocumentsNoRows (body) | `Pages_Command_Verein_DocumentsEmptyBody` | Clear the filter to see all documents. | Lösche den Filter, um alle Dokumente zu sehen. |
| `features/admin/VereinKassenbuchPage.tsx:237` | Pages_Command_Verein_KassenbuchNoRows (title) | `Pages_Command_Verein_KassenbuchEmptyTitle` | No ledger entries match | Keine passenden Kassenbucheinträge |
| `features/admin/VereinKassenbuchPage.tsx:237` | Pages_Command_Verein_KassenbuchNoRows (body) | `Pages_Command_Verein_KassenbuchEmptyBody` | Clear the filters or add a ledger entry. | Lösche die Filter oder füge einen Kassenbucheintrag hinzu. |
| `features/admin/VereinMembershipRequestsPage.tsx:368` | Pages_Command_Verein_MembershipRequests_NoRows (title) | `Pages_Command_Verein_MembershipRequests_EmptyTitle` | No membership requests match | Keine passenden Mitgliedsanträge |
| `features/admin/VereinMembershipRequestsPage.tsx:368` | Pages_Command_Verein_MembershipRequests_NoRows (body) | `Pages_Command_Verein_MembershipRequests_EmptyBody` | Clear the search or filters to see all requests. | Lösche die Suche oder die Filter, um alle Anträge zu sehen. |
| `features/events-member/EventDetailPage.tsx:345` | Auto_NoFullBriefingHasBeenPostedForThisOperation_574adb11 (title) | `Pages_Events_Detail_NoBriefingTitle` | No briefing yet | Noch kein Briefing |
| `features/events-member/EventDetailPage.tsx:345` | body that names the next step | `Pages_Events_Detail_NoBriefingBody` | The organizers post the full briefing before the operation starts. | Die Organisatoren veröffentlichen das vollständige Briefing vor Beginn der Operation. |
| `features/events-member/EventDetailPage.tsx:364` | Auto_NoSlotsConfigured_d0093e9c (title) | `Pages_Events_Detail_NoSlotsTitle` | No slots yet | Noch keine Plätze |
| `features/events-member/EventDetailPage.tsx:364` | body that names the next step | `Pages_Events_Detail_NoSlotsBody` | Slots appear here once the organizers configure them. | Plätze erscheinen hier, sobald die Organisatoren sie einrichten. |
| `features/hosting/FinanceProtocol.tsx:147` | HostingFinance_NoLedgerEntries (title) | `HostingFinance_NoLedgerEntriesTitle` | No ledger entries yet | Noch keine Buchungen |
| `features/hosting/FinanceProtocol.tsx:147` | HostingFinance_NoLedgerEntries (body) | `HostingFinance_NoLedgerEntriesBody` | Entries appear after the first payment, allowance or hosting cost. | Buchungen erscheinen nach der ersten Zahlung, dem ersten Verfügungsrahmen oder den ersten Hosting-Kosten. |
| `features/hosting/HostingAuditFeed.tsx:344` | Hosting_Detail_OperationsEmpty (title) | `Hosting_Detail_OperationsEmptyTitle` | No activity recorded | Keine Aktivität aufgezeichnet |
| `features/hosting/HostingAuditFeed.tsx:344` | Hosting_Detail_OperationsEmpty (body) | `Hosting_Detail_OperationsEmptyBody` | Activity appears here after the first action on this server. | Aktivität erscheint hier nach der ersten Aktion auf diesem Server. |
| `features/hosting/HostingFinancePage.tsx:149` | HostingFinance_NoOwners (title) | `HostingFinance_NoOwnersTitle` | No accounts match | Keine passenden Konten |
| `features/hosting/HostingFinancePage.tsx:149` | HostingFinance_NoOwners (body) | `HostingFinance_NoOwnersBody` | Try a different search. | Versuche eine andere Suche. |
| `features/hosting/ServerConfigurationPanel.tsx:387` | Server_Settings_Empty (title) | `Server_Settings_EmptyTitle` | No settings to change | Keine Einstellungen zum Ändern |
| `features/hosting/ServerConfigurationPanel.tsx:387` | body that names the next step | `Server_Settings_EmptyBody` | Settings appear here when the game supports them. | Einstellungen erscheinen hier, sobald das Spiel sie unterstützt. |
| `features/hosting/ServerFilesPanel.tsx:799` | Hosting_Detail_FilesEmpty (title) | `Hosting_Detail_FilesEmptyTitle` | This folder is empty | Dieser Ordner ist leer |
| `features/hosting/ServerFilesPanel.tsx:799` | Hosting_Detail_FilesEmpty (body) | `Hosting_Detail_FilesEmptyBody` | Upload files to add them to this folder. | Lade Dateien hoch, um sie diesem Ordner hinzuzufügen. |
| `features/hosting/ServerFilesPanel.tsx:808` | React_ServerFiles_NoSearchResults (title) | `React_ServerFiles_NoSearchResultsTitle` | No files match | Keine passenden Dateien |
| `features/hosting/ServerFilesPanel.tsx:808` | React_ServerFiles_NoSearchResults (body) | `React_ServerFiles_NoSearchResultsBody` | Clear the search to see everything in this folder. | Lösche die Suche, um alles in diesem Ordner zu sehen. |
| `features/hosting/ServerSecretsPanel.tsx:477` | Server_Credentials_Empty (title) | `Server_Secrets_EmptyTitle` | No credentials to manage | Keine Zugangsdaten zu verwalten |
| `features/hosting/ServerSecretsPanel.tsx:477` | Server_Credentials_Empty (body) | `Server_Secrets_EmptyBody` | Credentials appear here when the game needs them. | Zugangsdaten erscheinen hier, sobald das Spiel sie benötigt. |
| `features/hosting/sections/ActivitySection.tsx:241` | noProblems branch without title; body reuses Server_Activity_NoProblems, action Server_Activity_ShowAll | `Server_Activity_NoProblems_Title` | No problems | Keine Probleme |
| `features/hosting/sections/CloudMachinePanel.tsx:28` | Hosting_Detail_NoDeployment (title) | `Hosting_Detail_NoDeploymentTitle` | No active deployment | Keine aktive Bereitstellung |
| `features/hosting/sections/CloudMachinePanel.tsx:28` | Hosting_Detail_NoDeployment (body) | `Hosting_Detail_NoDeploymentBody` | The record and its data stay available. Start the server to create a machine. | Der Datensatz und seine Daten bleiben erhalten. Starte den Server, um eine Maschine zu erstellen. |
| `features/wiki/WikiPage.tsx:397 (no pages yet; title reuses Pages_Wiki_Index_NoPagesYet)` | body that names the next step | `Pages_Wiki_Index_NoPagesYetBody` | Create the first page to start the wiki. | Erstelle die erste Seite, um das Wiki zu starten. |
| `features/wiki/WikiPage.tsx:397 (search active)` | Pages_Wiki_Index_NoPagesMatchSearch (title) | `Pages_Wiki_Index_NoPagesMatchSearchTitle` | No pages match | Keine passenden Seiten |
| `features/wiki/WikiPage.tsx:397 (search active)` | Pages_Wiki_Index_NoPagesMatchSearch (body) | `Pages_Wiki_Index_NoPagesMatchSearchBody` | Try a different search term. | Versuche einen anderen Suchbegriff. |
| `features/wiki/WikiPage.tsx:735, 798` | title; bodies reuse Pages_Wiki_Index_SelectPageForRevisions and _SelectPageForDetails | `Pages_Wiki_Index_NoPageSelectedTitle` | No page selected | Keine Seite ausgewählt |
| `features/wiki/WikiPage.tsx:750` | Pages_Wiki_Index_NoRevisionsForPage (title) | `Pages_Wiki_Index_NoRevisionsTitle` | No revisions yet | Noch keine Revisionen |
| `features/wiki/WikiPage.tsx:750` | Pages_Wiki_Index_NoRevisionsForPage (body) | `Pages_Wiki_Index_NoRevisionsBody` | Revisions appear after the page is saved. | Revisionen erscheinen, sobald die Seite gespeichert wurde. |
| `features/wiki/WikiPage.tsx:881` | Pages_Wiki_Index_Error_EditorNotOpen (title) | `Pages_Wiki_Index_EditorClosedTitle` | No article open | Kein Artikel geöffnet |
| `features/wiki/WikiPage.tsx:881` | Pages_Wiki_Index_Error_EditorNotOpen (body) | `Pages_Wiki_Index_EditorClosedBody` | Open a page and choose Edit to start the editor. | Öffne eine Seite und wähle Bearbeiten, um den Editor zu starten. |
| `pages/FieldReportsPage.tsx:133 (category filter)` | Pages_Public_Media_NoReportsMatchCategory (title) | `Pages_Public_Media_NoMatchTitle` | No field reports in this category | Keine Feldberichte in dieser Kategorie |
| `pages/FieldReportsPage.tsx:133 (category filter)` | Pages_Public_Media_NoReportsMatchCategory (body) | `Pages_Public_Media_NoMatchBody` | Choose another category to see more reports. | Wähle eine andere Kategorie, um mehr Berichte zu sehen. |
| `pages/FieldReportsPage.tsx:133 (none published)` | Pages_Public_Media_NoReportsPublished (title) | `Pages_Public_Media_NoReportsTitle` | No field reports yet | Noch keine Feldberichte |
| `pages/FieldReportsPage.tsx:133 (none published)` | Pages_Public_Media_NoReportsPublished (body) | `Pages_Public_Media_NoReportsBody` | Published field reports appear here. | Veröffentlichte Feldberichte erscheinen hier. |
| `pages/HomePage.tsx:192 (empty branch), 286` | Pages_Public_Home_UpcomingSummaryEmpty (title) | `Pages_Public_Home_UpcomingEmptyTitle` | No upcoming operations | Keine anstehenden Operationen |
| `pages/HomePage.tsx:192 (empty branch), 286` | Pages_Public_Home_UpcomingSummaryEmpty (body) | `Pages_Public_Home_UpcomingEmptyBody` | Operations appear here once they are published. | Operationen erscheinen hier, sobald sie veröffentlicht sind. |
| `pages/HomePage.tsx:370` | Pages_Public_Home_NoFieldReportsPublished (title) | `Pages_Public_Home_NoFieldReportsTitle` | No field reports yet | Noch keine Feldberichte |
| `pages/HomePage.tsx:370` | Pages_Public_Home_NoFieldReportsPublished (body) | `Pages_Public_Home_NoFieldReportsBody` | Published field reports appear here. | Veröffentlichte Feldberichte erscheinen hier. |
| `pages/UpcomingEventsPage.tsx:529` | Pages_Events_Upcoming_NoScheduledInMonth (title) | `Pages_Events_Upcoming_NoScheduledTitle` | No operations scheduled in {0} | In {0} sind keine Operationen geplant |
| `pages/UpcomingEventsPage.tsx:529` | Pages_Events_Upcoming_NoScheduledInMonth (body) | `Pages_Events_Upcoming_NoScheduledBody` | Choose another month to see more operations. | Wähle einen anderen Monat, um mehr Operationen zu sehen. |
| `pages/VereinPages.tsx:293` | Pages_Unit_Satzung_Index_NotFoundDescription (title) | `Pages_Unit_Satzung_Index_EmptyTitle` | No Satzung published yet | Noch keine Satzung veröffentlicht |
| `pages/VereinPages.tsx:293` | Pages_Unit_Satzung_Index_NotFoundDescription (body) | `Pages_Unit_Satzung_Index_EmptyBody` | Check back after the Verein publishes its Satzung. | Schau wieder vorbei, sobald der Verein seine Satzung veröffentlicht hat. |

## Decisions Made

- Tone changes for the four values above follow the UI-SPEC lexicon examples ("Not verified", "Not a member", "Hidden", "Draft" are all listed as neutral).
- Dismiss labels for sites where the button resets or leaves an edited form use "Discard ... changes" or "Leave without saving"; confirmation dialogs use "Keep X" for destructive actions, "Keep X active" for archive or deactivate, and "Don't verb X" for create, send and upload.
- Finance dialog dismiss keys are one per action because the dialog is one component with seven effects; the Wave 2 plan adds a `dismissLabel` prop to `FinanceDialog`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Generic cancel labels the plan list did not name**
- **Found during:** Task 3 (enumerating dismiss sites)
- **Issue:** Nine more dialog or editor leave controls use a generic Cancel label under other keys: `t("Cancel")` (EventEditPage, EventSlotPlanner, ModPackWorkspacePage), `Pages_Command_FieldReports_Edit_ButtonCancel`, `Pages_Command_Verein_KassenbuchDeleteCancel`, `Pages_Wiki_Index_Cancel` (two sites), `Shared_CommunityDialog_Cancel` and `Components_GalleryImagePicker_DialogCancel`. D-11 bans the generic label for every dialog button, so Wave 2 would have no key to convert them to.
- **Fix:** Added a `_Dismiss` key for each (rows marked `Cancel (text key)` or naming the old key in the key map). Total new `_Dismiss` keys: 34 against the acceptance floor of 17.
- **Files modified:** `sources/ReactApp/src/locales/en.json`, `de.json`
- **Commit:** `99252c12`

**2. [Rule 1 - Bug] New keys collided with a closed key list of Phase 65**
- **Found during:** Task 3 (full unit suite run)
- **Issue:** `tests/locales/server-keys.test.ts` closes the `Server_Mods_`, `Server_Credentials_`, `Server_Change_` and `Server_Files_` prefixes; my first empty-state keys under `Server_Mods_` and `Server_Credentials_` failed it.
- **Fix:** Renamed to `Server_ModPack_EmptyTitle`, `Server_ModPack_EmptyBody`, `Server_Secrets_EmptyTitle`, `Server_Secrets_EmptyBody` before committing.
- **Commit:** `99252c12`

**3. [Plan premise differed from code] VereinSettingsPage h1 already had its own long-form key**
- **Found during:** Task 3
- **Issue:** The h1 reads `Pages_Command_Verein_SettingsPageTitle` ("Verein contribution settings"), not the navigation key, so shortening the navigation label would not have changed it.
- **Fix:** Followed the plan as written: added `Pages_Command_VereinSettings_PageTitle` with the long form and pointed the h1 at it (acceptance criterion). The old key now has no reader and is left for 69-33 to retire. Added one test in `tests/admin-verein-settings.test.tsx` (a file outside the plan's `files_modified`) that asserts the long h1 and the short kicker.
- **Commit:** `082d30ad`

**4. [Scope] Extra tone export**
- `membershipRequestTones` was added beside the ten listed maps (see the tone notes).

### Deferred (out of scope, logged)

`deferred-items-69-03.md` records 65 mod pack admin `t("English sentence")` lookups with no locale entry and six error or forbidden `StatePanel`s with a dynamic `kind` that Wave 2 converts to `ErrorState` and `ForbiddenState`.

## Authentication Gates

None.

## Issues Encountered

- The worktree has no `node_modules`; a directory junction to the main checkout's `node_modules` (git-ignored) was created so the tests could run. Nothing was installed.
- `npm run test:e2e` runs `build:assets`, which regenerated `src/generated/api.ts` with a line-ending diff; that file was restored with `git checkout --` and is not part of any commit.
- The full unit suite showed two timing-sensitive failures under load (`metrics-performance`, `admin-membership-requests`); both pass when run alone and neither touches files of this plan.

## Verification

- `npx vitest run tests/state-views.test.tsx tests/status-tones.test.ts tests/locales tests/admin-verein-settings.test.tsx`: passed.
- Full `npx vitest run` before the key rename: 2719 passed, 4 failed in 3 files. Two files (3 tests) were the load-sensitive timing tests named above and pass alone; the third, `server-keys.test.ts`, was fixed by the key rename (deviation 2). A rerun of `tests/locales`, `admin-membership-requests`, `metrics-performance` and `admin-verein-settings` after the rename: 135 passed.
- `npm run locales:check`: 3980 English and 3980 German keys, 0 failures, 0 warnings.
- `npm run lint` and `npm run typecheck` (whole project, after the last commit): both exit 0.
- `npm run test:e2e -- tests/e2e/public-shell.spec.ts --project=chromium`: 28 passed.

## Known Stubs

None. The components and maps are complete; the keys are consumed by Wave 2 plans, which is by design.

## Threat Flags

None. `AccessBoundary` adds no network surface; T-69-03 is mitigated by its JSDoc and by a test that asserts no request is made when access is denied.

## Next Phase Readiness

Wave 2 plans can import `LoadingState`, `EmptyState`, `ErrorState`, `ForbiddenState`, `AccessBoundary` and the tone maps, and read every string they need from the key map above.

## Self-Check: PASSED

- Created files exist: StateViews.tsx, statusTones.ts, state-views.test.tsx, status-tones.test.ts, navLabelContract.ts, nav-labels.test.ts.
- Task commits are ancestors of HEAD: 37d417c4, 32bb1793, 082d30ad, 99252c12.
