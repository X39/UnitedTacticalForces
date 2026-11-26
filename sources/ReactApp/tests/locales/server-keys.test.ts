import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import ts from "typescript";
import i18n, { i18nReady } from "@/app/i18n";
import de from "@/locales/de.json";
import en from "@/locales/en.json";

const english: Record<string, string> = en;
const german: Record<string, string> = de;

const actionIds = ["Start", "Stop", "Terminate", "Restart", "Install", "Update", "CollectLogs"];

/** Fleet keys that carry `_one` and `_other` forms. */
const fleetPluralKeys = [
  "Server_Fleet_ResultCount",
  "Server_Fleet_CostLine",
  "Server_Fleet_ChangedMore",
] as const;

/** Fleet keys whose values are rendered as button labels or icon-only button names. */
const fleetButtonKeys = [
  "Server_Fleet_Open",
  "Server_Fleet_Refresh",
  "Server_Fleet_SearchClear",
  "Server_Fleet_NoMatch_Clear",
  "Server_Fleet_Live_Reconnect",
] as const;

/** Every Server_ key that later plans consume and that Plan 61-05 owns. */
const requiredKeys: readonly string[] = [
  ...actionIds.flatMap((id) => [
    `Server_Action_${id}_Label`,
    `Server_Action_${id}_Dismiss`,
    `Server_Action_${id}_Pending`,
  ]),
  "Server_Action_Other_Dismiss",
  "Server_Action_Other_Pending",
  "Server_Action_ArchiveRecord_Dismiss",
  "Server_Action_InProgress",
  "Server_Confirm_Kicker",
  "Server_Confirm_Title_Start",
  "Server_Confirm_Title_Stop",
  "Server_Confirm_Title_Terminate",
  "Server_Confirm_Title_Other",
  "Server_Confirm_Title_Unknown",
  "Server_Confirm_Body_Start",
  "Server_Confirm_Body_Stop",
  "Server_Confirm_Body_Terminate",
  "Server_Confirm_Body_Other",
  "Server_Confirm_Fact_Cost",
  "Server_Confirm_Fact_Owner",
  "Server_Confirm_Fact_Stop",
  "Server_Confirm_CostHourlyStart",
  "Server_Confirm_CostMonthly",
  "Server_Confirm_CostStopHourly",
  "Server_Confirm_CostUnavailable",
  "Server_Confirm_CostKeepsRunning",
  "Server_Confirm_CostUnknown",
  "Server_Confirm_OwnerUnknown",
  "Server_Confirm_OwnerHidden",
  "Server_Confirm_StopHowStart",
  "Server_Confirm_StopHowStop",
  "Server_Confirm_StopHowStopNoMinutes",
  "Server_Confirm_ChallengeHelper",
  "Server_Confirm_ServerNote",
  "Server_Confirm_RequestError",
  "Server_Toast_StartRequested",
  "Server_Toast_ShutdownRequested",
  "Server_Toast_ActionRequested",
  "Server_Reason_Operate",
  "Server_Reason_Pending",
  "Server_Reason_LaunchDisabled",
  "Server_Reason_State",
  "Server_Reason_StateGeneric",
  "Server_Reason_Terminating",
  "Server_Reason_Paused",
  "Server_Reason_Other",
  "Server_Reason_CreatePermission",
  "Server_DeepLink_Unavailable",
  "Server_DeepLink_Unknown",
  "Server_DeepLink_Dismiss",
  "Server_Access_ForbiddenTitle",
  "Server_Access_ForbiddenBody",
  "Server_Access_EmptyTitle",
  "Server_Access_EmptyBody",
  "Server_Access_LoadError",
  "Server_Access_Retry",
  "Server_State_TechnicalStatus",
  ...fleetKeys(),
];

/** The fleet keys of the Phase 62 copy contract; plural keys are listed with both forms. */
function fleetKeys(): string[] {
  return [
    "Server_Fleet_Open",
    "Server_Fleet_Refresh",
    "Server_Fleet_SearchLabel",
    "Server_Fleet_SearchPlaceholder",
    "Server_Fleet_SearchClear",
    "Server_Fleet_FilterLabel",
    "Server_Fleet_Filter_All",
    "Server_Fleet_Filter_Mine",
    "Server_Fleet_Filter_Running",
    "Server_Fleet_Filter_Off",
    ...fleetPluralKeys.flatMap((key) => [`${key}_one`, `${key}_other`]),
    "Server_Fleet_Section_Active",
    "Server_Fleet_Section_Off",
    "Server_Fleet_Section_ActiveEmpty",
    "Server_Fleet_QueuedChange",
    "Server_Fleet_Submitted",
    "Server_Fleet_NoMatch_Title",
    "Server_Fleet_NoMatch_Body",
    "Server_Fleet_NoMatch_Clear",
    "Server_Fleet_Mine_Empty_Title",
    "Server_Fleet_Mine_Empty_Body",
    "Server_Fleet_Live_Reconnecting_Title",
    "Server_Fleet_Live_Reconnecting_Body",
    "Server_Fleet_Live_Unavailable_Title",
    "Server_Fleet_Live_Unavailable_Body",
    "Server_Fleet_Live_Reconnect",
    "Server_Fleet_Changed",
  ];
}

/** Every key of the Phase 63 copy contract (63-UI-SPEC Copywriting Contract) that Plan 63-01 adds. */
const phase63RequiredKeys: readonly string[] = [
  "Server_Workspace_Section_Overview",
  "Server_Workspace_Section_Activity",
  "Server_Workspace_Section_Settings",
  "Server_Workspace_Section_Files",
  "Server_Workspace_Section_Machine",
  "Server_Workspace_Breadcrumb_Label",
  "Server_Workspace_Details_Label",
  "Server_Workspace_Cost_Hourly",
  "Server_Workspace_Cost_Monthly",
  "Server_Workspace_Cost_Status",
  "Server_Workspace_Cost_Accrued",
  "Server_Workspace_Cost_NoEstimate",
  "Server_Workspace_Endpoint_Label",
  "Server_Workspace_Endpoint_Unavailable",
  "Server_Workspace_Endpoint_Copy",
  "Server_Workspace_Endpoint_Copied",
  "Shared_Copy_Done",
  "Shared_Copy_Failed",
  "Server_Workspace_Refresh",
  "Server_Workspace_Archived",
  "Server_Workspace_NotFound_Title",
  "Server_Workspace_NotFound_Body",
  "Server_Workspace_BackToServers",
  "Server_Workspace_Announce_JobFinished",
  "Server_Workspace_Announce_JobFailed",
  "Server_Progress_Kicker",
  "Server_Progress_StepsLabel",
  "Server_Progress_Step_Queued",
  "Server_Progress_Step_PreparingMachine",
  "Server_Progress_Step_WaitingForSoftware",
  "Server_Progress_Step_WorkingOnServer",
  "Server_Progress_Step_SavingData",
  "Server_Progress_Step_ReleasingMachine",
  "Server_Progress_Step_Finishing",
  "Server_Progress_Step_RetryWait",
  "Server_Progress_Step_Working",
  "Server_Progress_StepDone",
  "Server_Progress_StepCurrent",
  "Server_Progress_StepUpcoming",
  "Server_Progress_Started",
  "Server_Progress_Requested",
  "Server_Progress_Lag",
  "Server_Progress_MoreQueued_one",
  "Server_Progress_MoreQueued_other",
  "Server_Progress_RetryBody",
  "Server_Failure_Title",
  "Server_Failure_WhatFailed",
  "Server_Failure_FailedAt",
  "Server_Failure_NoReason",
  "Server_Failure_CostLabel",
  "Server_Failure_Cost_Live",
  "Server_Failure_Cost_Maybe",
  "Server_Failure_Cost_None",
  "Server_Failure_NextLabel",
  "Server_Failure_Next_StopBilling",
  "Server_Failure_Next_Retry",
  "Server_Failure_Next_AskOwner",
  "Server_Failure_Next_Details",
  "Server_Failure_Next_Wait",
  "Server_Failure_Retry",
  "Server_Failure_Skip",
  "Server_Failure_OpenDetails",
  "Server_Failure_Sending",
  "Server_Failure_RequestError",
  "Server_Activity_Description",
  "Server_Activity_FilterLabel",
  "Server_Activity_Filter_All",
  "Server_Activity_Filter_Jobs",
  "Server_Activity_Filter_Logs",
  "Server_Activity_Filter_Audit",
  "Server_Activity_ProblemsOnly",
  "Server_Activity_Type_Job",
  "Server_Activity_Type_Log",
  "Server_Activity_Type_Audit",
  "Server_Activity_Today",
  "Server_Activity_Yesterday",
  "Server_Activity_Actor_Platform",
  "Server_Activity_Actor_Server",
  "Server_Activity_Actor_Hidden",
  "Server_Activity_RequestedBy",
  "Server_Activity_ShowDetails",
  "Server_Activity_HideDetails",
  "Server_Activity_CopyOutput",
  "Server_Activity_OutputCopied",
  "Server_Activity_DownloadLog",
  "Server_Activity_DownloadAll",
  "Server_Activity_ShowMore",
  "Server_Activity_ShowAll",
  "Server_Activity_Empty_Title",
  "Server_Activity_Empty_Body",
  "Server_Activity_NoMatch_Title",
  "Server_Activity_NoMatch_Body",
  "Server_Activity_NoProblems",
  "Server_Activity_LoadError",
  "Server_Activity_WindowNote",
  "Server_Activity_EntryMissing",
  "Server_Activity_Kind_Custom",
  "Server_Activity_Kind_Unknown",
  "Server_Live_Connecting",
  "Server_Live_Live",
  "Server_Live_Stale",
  "Server_Live_Updated",
  "Server_Live_StaleReason_Disconnected",
  "Server_Live_StaleReason_Old",
  "Server_Live_RefreshActivity",
  "Server_Activity_Job_Queued",
  "Server_Activity_Job_Running",
  "Server_Activity_Job_RetryPending",
  "Server_Activity_Job_Succeeded",
  "Server_Activity_Job_Failed",
  "Server_Activity_Job_Skipped",
  "Server_Activity_Job_CancelledByCostStop",
  "Server_Activity_Log_Queued",
  "Server_Activity_Log_Running",
  "Server_Activity_Log_Succeeded",
  "Server_Activity_Log_Failed",
  "Server_Activity_Log_Retried",
  "Server_Activity_Log_Skipped",
  "Server_Activity_Log_CancelledByCostStop",
  "Server_Activity_Audit_DeploymentCreated",
  "Server_Activity_Audit_DeploymentResourceProvisioningRequested",
  "Server_Activity_Audit_DeploymentResourceProvisioningFailed",
  "Server_Activity_Audit_DeploymentResourceObserved",
  "Server_Activity_Audit_DeploymentResourceAdopted",
  "Server_Activity_Audit_DeploymentResourceReleaseRequested",
  "Server_Activity_Audit_DeploymentResourceReleaseFailed",
  "Server_Activity_Audit_DeploymentResourceReleased",
  "Server_Activity_Audit_DeploymentStopped",
  "Server_Activity_Audit_DeploymentTerminated",
  "Server_Activity_Audit_LifecycleJobQueued",
  "Server_Activity_Audit_LifecycleJobClaimed",
  "Server_Activity_Audit_LifecycleJobRetryPending",
  "Server_Activity_Audit_LifecycleJobFailed",
  "Server_Activity_Audit_LifecycleJobSucceeded",
  "Server_Activity_Audit_LifecycleFailed",
  "Server_Activity_Audit_WorkerProvisioned",
  "Server_Activity_Audit_WorkerRegistered",
  "Server_Activity_Audit_WorkerDegraded",
  "Server_Activity_Audit_WorkerRecovered",
  "Server_Activity_Audit_WorkerStopped",
  "Server_Activity_Audit_WorkerCommandQueued",
  "Server_Activity_Audit_WorkerCommandFailed",
  "Server_Activity_Audit_WorkerCommandSucceeded",
  "Server_Activity_Audit_ProviderReconciliationDegraded",
  "Server_Activity_Audit_Recovery",
  "Server_Activity_Audit_Other",
  "Server_Overview_DetailsTitle",
  "Server_Overview_Game",
  "Server_Overview_Provider",
  "Server_Overview_Terrain",
  "Server_Overview_Scenario",
  "Server_Overview_Updated",
  "Server_Overview_MetricsTitle",
  "Server_Overview_RecentTitle",
  "Server_Overview_ActionsTitle",
  "Server_Settings_CredentialsTitle",
  "Server_Settings_AutomationTitle",
  "Server_Settings_RecordTitle",
  "Server_Settings_RecordBody",
  "Server_Settings_DeleteError",
  "Server_Machine_MayCost",
  "Server_Machine_ExternalStorage",
  "Server_Machine_ResourceHourly",
];

/** Every `Server_Create_*` key of the Phase 64 copy contract (64-UI-SPEC Copywriting Contract, Plan 64-01). */
const phase64RequiredKeys: readonly string[] = [
  "Server_Create_About_Title",
  "Server_Create_Advanced_Title",
  "Server_Create_After_KeepOff",
  "Server_Create_After_KeepOff_Help",
  "Server_Create_After_Start",
  "Server_Create_After_Start_Help",
  "Server_Create_After_Title",
  "Server_Create_BilledTo",
  "Server_Create_BilledTo_Change",
  "Server_Create_Blocked_Checking",
  "Server_Create_Blocked_EstimateFailed",
  "Server_Create_Blocked_NoProvider",
  "Server_Create_Blocked_OptionsFailed",
  "Server_Create_Blocked_OptionsLoading",
  "Server_Create_Blocked_OwnerFailed",
  "Server_Create_Blocked_OwnerFrozen",
  "Server_Create_Blocked_Storage",
  "Server_Create_Content_Choose",
  "Server_Create_Content_Required",
  "Server_Create_Cost_Announce",
  "Server_Create_Cost_Hourly",
  "Server_Create_Cost_Monthly",
  "Server_Create_Cost_StorageValue",
  "Server_Create_Cost_Title",
  "Server_Create_Cost_Unavailable",
  "Server_Create_Cost_WhileOff",
  "Server_Create_Description",
  "Server_Create_Error",
  "Server_Create_Fit_Insufficient",
  "Server_Create_Fit_Sufficient",
  "Server_Create_Fit_Unknown",
  "Server_Create_Game_Announce",
  "Server_Create_Invalid_one",
  "Server_Create_Invalid_other",
  "Server_Create_Leave",
  "Server_Create_ModPack_Label",
  "Server_Create_Name_Hint",
  "Server_Create_Name_Required",
  "Server_Create_Name_TooLong",
  "Server_Create_Notice_Dismiss",
  "Server_Create_Notice_NotStarted",
  "Server_Create_Options_LoadFailed",
  "Server_Create_Owner_Frozen",
  "Server_Create_Owner_LoadFailed",
  "Server_Create_Owner_Loading",
  "Server_Create_Owner_Retry",
  "Server_Create_Preset_Announce",
  "Server_Create_Preset_AppliedLine",
  "Server_Create_Preset_CustomOption",
  "Server_Create_Preset_Customized",
  "Server_Create_Preset_Default",
  "Server_Create_Preset_GroupModPacks",
  "Server_Create_Preset_GroupProfiles",
  "Server_Create_Preset_Hint",
  "Server_Create_Preset_Label",
  "Server_Create_Preset_ProfileOption",
  "Server_Create_Profile_Label",
  "Server_Create_Providers_Unavailable",
  "Server_Create_Retry",
  "Server_Create_Section_Server",
  "Server_Create_Size_Hint",
  "Server_Create_Size_Label",
  "Server_Create_Size_Option",
  "Server_Create_Size_OptionNoPrice",
  "Server_Create_StorageDetails",
  "Server_Create_Storage_Label",
  "Server_Create_Submit_KeepOff",
  "Server_Create_Submit_Pending",
  "Server_Create_Submit_Start",
  "Server_Create_Summary_Configuration",
  "Server_Create_Summary_Content",
  "Server_Create_Summary_ModPack",
  "Server_Create_Summary_Provider",
  "Server_Create_Summary_Storage",
  "Server_Create_Title",
  "Server_Create_Toast_KeepOff",
  "Server_Create_Toast_Start",
];

/** Every key of the Phase 65 copy contract (65-UI-SPEC Copywriting Contract, Plan 65-02), 159 in total. */
const phase65RequiredKeys: readonly string[] = [
  "Server_Change_Bar_Label",
  "Server_Change_Bar_Status_one",
  "Server_Change_Bar_Status_other",
  "Server_Change_Review",
  "Server_Change_Discard",
  "Server_Change_Invalid_one",
  "Server_Change_Invalid_other",
  "Server_Change_PanelCount",
  "Server_Change_Field_Changed",
  "Server_Change_Field_Undo",
  "Server_Change_Field_Undo_Name",
  "Server_Change_Announce_Staged",
  "Server_Change_Announce_Empty",
  "Server_Change_ReadOnly",
  "Server_Change_ReadOnly_AccessOnly",
  "Server_Change_ReadOnly_Archived",
  "Server_Change_DiscardDialog_Title",
  "Server_Change_DiscardDialog_Body_one",
  "Server_Change_DiscardDialog_Body_other",
  "Server_Change_KeepEditing",
  "Server_Change_ReviewDialog_Description",
  "Server_Change_Effect_Off",
  "Server_Change_Effect_Running",
  "Server_Change_ReviewDialog_Body",
  "Server_Change_Group_ModPack",
  "Server_Change_Group_Profile",
  "Server_Change_Group_Settings",
  "Server_Change_Group_Credentials",
  "Server_Change_Before",
  "Server_Change_After",
  "Server_Change_Value_Inherited",
  "Server_Change_Value_SecretChanged",
  "Server_Change_Value_SecretRotated",
  "Server_Change_Value_ProfileLatest",
  "Server_Change_Value_ProfilePinned",
  "Server_Change_Item_Remove_Name",
  "Server_Change_Apply_one",
  "Server_Change_Apply_other",
  "Server_Change_Apply_Pending",
  "Server_Change_Retry_one",
  "Server_Change_Retry_other",
  "Server_Change_Blocked_Comparing",
  "Server_Change_State_Applied",
  "Server_Change_State_Failed",
  "Server_Change_State_NotTried",
  "Server_Change_Failure_Forbidden",
  "Server_Change_Failure_Conflict",
  "Server_Change_Failure_Other",
  "Server_Change_Failure_Dependency",
  "Server_Change_Results_Partial",
  "Server_Change_Toast_Applied_one",
  "Server_Change_Toast_Applied_other",
  "Server_Change_Pending_Running",
  "Server_Change_Pending_Off",
  "Server_Change_Pending_Areas",
  "Server_Change_Area_ModPack",
  "Server_Change_Area_Configuration",
  "Server_Change_Area_Credentials",
  "Server_Change_Leave_Title",
  "Server_Change_Leave_Body_one",
  "Server_Change_Leave_Body_other",
  "Server_Change_Leave_Stay",
  "Server_Change_Leave_Discard",
  "Server_Change_ModDiff_Toggle_one",
  "Server_Change_ModDiff_Toggle_other",
  "Server_Mods_Title",
  "Server_Mods_Description",
  "Server_Mods_Current",
  "Server_Mods_Unavailable",
  "Server_Mods_CurrentLine",
  "Server_Mods_CurrentNone",
  "Server_Mods_StagedLine",
  "Server_Mods_Contents_Title",
  "Server_Mods_Contents_Empty",
  "Server_Mods_Contents_Loading",
  "Server_Mods_Contents_Failed",
  "Server_Mods_Contents_Unknown",
  "Server_Mods_UnavailableList",
  "Server_Mods_UnavailableFallback",
  "Server_Mods_Diff_Added",
  "Server_Mods_Diff_Removed",
  "Server_Mods_Diff_Unchanged",
  "Server_Mods_Diff_ShowAll",
  "Server_Mods_Diff_ShowFewer",
  "Server_Mods_Diff_Failed",
  "Server_Mods_Diff_Same",
  "Server_Mods_LoadError",
  "Server_Mods_Loading",
  "Server_Mods_Empty",
  "Server_Settings_ConfigurationTitle",
  "Server_Settings_ConfigurationDescription",
  "Server_Settings_Revision_Legend",
  "Server_Settings_Revision_Latest",
  "Server_Settings_Revision_Pinned",
  "Server_Settings_Common",
  "Server_Settings_Advanced_Title",
  "Server_Settings_Advanced_Summary_one",
  "Server_Settings_Advanced_Summary_other",
  "Server_Settings_Advanced_SummaryChanged_one",
  "Server_Settings_Advanced_SummaryChanged_other",
  "Server_Settings_Target",
  "Server_Settings_StoredValue",
  "Server_Settings_ResetStaged",
  "Server_Settings_Reset_Name",
  "Server_Settings_Invalid_Required",
  "Server_Settings_Invalid_Min",
  "Server_Settings_Invalid_Max",
  "Server_Settings_Invalid_Number",
  "Server_Settings_Invalid_Decimal",
  "Server_Settings_Invalid_Choice",
  "Server_Settings_PhysicalOverride",
  "Server_Settings_EstimateSaved",
  "Server_Settings_Empty",
  "Server_Settings_LoadError",
  "Server_Settings_Retry",
  "Server_Credentials_Description",
  "Server_Credentials_Configured",
  "Server_Credentials_RequiredBeforeStart",
  "Server_Credentials_Masked",
  "Server_Credentials_OneTime_Label",
  "Server_Credentials_OneTime_Announce",
  "Server_Credentials_Copy",
  "Server_Credentials_CopiedAnnounce",
  "Server_Credentials_SetHintGenerated",
  "Server_Credentials_ShowTyped",
  "Server_Credentials_ShowTyped_Name",
  "Server_Credentials_HideTyped",
  "Server_Credentials_HideTyped_Name",
  "Server_Credentials_Invalid_Blank",
  "Server_Credentials_Invalid_TooLong",
  "Server_Credentials_Invalid_Control",
  "Server_Credentials_Invalid_Token",
  "Server_Credentials_Reveal",
  "Server_Credentials_Reveal_Name",
  "Server_Credentials_Hide",
  "Server_Credentials_Hide_Name",
  "Server_Credentials_RevealAnnounce",
  "Server_Credentials_HideAnnounce",
  "Server_Credentials_RevealError",
  "Server_Credentials_SetLabel",
  "Server_Credentials_SetHint",
  "Server_Credentials_Rotate",
  "Server_Credentials_Rotate_Name",
  "Server_Credentials_RotateStaged",
  "Server_Credentials_StagedSet",
  "Server_Credentials_RotateUndo",
  "Server_Credentials_RotateUndo_Name",
  "Server_Credentials_Loading",
  "Server_Credentials_LoadError",
  "Server_Credentials_Empty",
  "Server_Files_Footer_Immediate",
  "Server_Files_PathFallback",
  "Server_Files_PathInvalid",
  "Server_Files_NoticeDismiss",
  "Server_Files_TopLevel",
  "Server_Files_Toolbar_Label",
  "Server_Files_Upload_DropTitle",
  "Server_Files_Upload_DropBody",
  "Server_Files_Upload_RemoveNamed",
  "Server_Files_FolderAnnounce",
  "Server_Files_PathLabel",
  "Server_Files_Leave_Title",
  "Server_Files_Leave_Body",
  "Server_Files_Leave_Discard",
];

const bannedLabels = new Set(
  [
    "Cancel",
    "OK",
    "Submit",
    "Save",
    "Confirm",
    "Click here",
    "Abbrechen",
    "Speichern",
    "Bestätigen",
  ].map((label) => label.toLocaleLowerCase()),
);

/** Phase 65 keys whose values are shown as button labels and so must name their effect. */
const phase65ButtonKeys: readonly string[] = [
  "Server_Change_Review",
  "Server_Change_Discard",
  "Server_Change_KeepEditing",
  "Server_Change_Field_Undo",
  "Server_Change_Apply_one",
  "Server_Change_Apply_other",
  "Server_Change_Retry_one",
  "Server_Change_Retry_other",
  "Server_Change_Leave_Stay",
  "Server_Change_Leave_Discard",
  "Server_Mods_Diff_ShowAll",
  "Server_Mods_Diff_ShowFewer",
  "Server_Settings_Retry",
  "Server_Credentials_Reveal",
  "Server_Credentials_Hide",
  "Server_Credentials_Copy",
  "Server_Credentials_ShowTyped",
  "Server_Credentials_HideTyped",
  "Server_Credentials_Rotate",
  "Server_Credentials_RotateUndo",
  "Server_Files_NoticeDismiss",
  "Server_Files_Leave_Discard",
];

/** Keys whose values are shown as button labels and so must name their effect. */
function isButtonLabelKey(key: string): boolean {
  return (
    /^Server_Action_.+_(Label|Dismiss|Pending)$/.test(key) ||
    /^Server_Create_Submit_/.test(key) ||
    [
      "Server_Access_Retry",
      "Server_Create_Leave",
      "Server_Create_Retry",
      "Server_Create_BilledTo_Change",
      "Server_Create_Owner_Retry",
      "Server_Create_Notice_Dismiss",
      ...phase65ButtonKeys,
    ].includes(key)
  );
}

function placeholderSet(value: string): string {
  const names = new Set<string>();
  for (const match of value.matchAll(/\{\{\s*([^{}\s]+)\s*\}\}/g)) {
    const name = match[1];
    if (name !== undefined) names.add(name);
  }
  return [...names].sort().join(",");
}

const languages: ReadonlyArray<readonly [string, Record<string, string>]> = [
  ["en", english],
  ["de", german],
];

describe("Server_ locale vocabulary", () => {
  it("contains every required key with a non-empty value in both languages", () => {
    const missing = requiredKeys.filter(
      (key) => !Object.hasOwn(english, key) || !english[key]?.trim() || !german[key]?.trim(),
    );

    expect(missing).toEqual([]);
  });

  it.each(languages)("has no generic button label among the action keys in %s", (_name, source) => {
    const offending = Object.entries(source)
      .filter(([key]) => isButtonLabelKey(key))
      .filter(([, value]) => bannedLabels.has(value.trim().toLocaleLowerCase()))
      .map(([key]) => key);

    expect(offending).toEqual([]);
    expect(Object.keys(source).filter(isButtonLabelKey).length).toBeGreaterThan(20);
  });

  it.each(languages)(
    "uses the cost-stop phrase in the start how-to-stop sentence in %s",
    (_n, source) => {
      const phrase = source["Server_Action_Stop_Label"];

      expect(phrase).toBeTruthy();
      expect(source["Server_Confirm_StopHowStart"]).toContain(phrase);
    },
  );

  it("uses the exact cost-stop phrase of the specification", () => {
    expect(english["Server_Action_Stop_Label"]).toBe("Shut down and stop billing");
    expect(german["Server_Action_Stop_Label"]).toBe("Herunterfahren und Abrechnung beenden");
  });

  it.each(languages)("names every Server_ key as PascalCase segments in %s", (_name, source) => {
    const pattern = /^[A-Z][A-Za-z0-9]*(_[A-Z][A-Za-z0-9]*)*(_one|_other)?$/;
    const offending = Object.keys(source).filter(
      (key) => key.startsWith("Server_") && (!pattern.test(key) || /[.:]/.test(key)),
    );

    expect(offending).toEqual([]);
  });

  it("keeps the placeholder sets of every Server_ key equal in both languages", () => {
    const mismatched = Object.keys(english)
      .filter((key) => key.startsWith("Server_"))
      .filter((key) => placeholderSet(english[key] ?? "") !== placeholderSet(german[key] ?? ""));

    expect(mismatched).toEqual([]);
  });
});

/** Phase 63 keys that carry `_one` and `_other` forms. */
const phase63PluralKeys = ["Server_Progress_MoreQueued"] as const;

describe("Phase 63 locale vocabulary", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("contains every Phase 63 key with a non-empty value in both languages", () => {
    const missing = phase63RequiredKeys.filter(
      (key) => !Object.hasOwn(english, key) || !english[key]?.trim() || !german[key]?.trim(),
    );

    expect(phase63RequiredKeys.length).toBeGreaterThan(150);
    expect(missing).toEqual([]);
  });

  it.each(languages)(
    "has both plural forms of every Phase 63 plural key in %s",
    (_name, source) => {
      const missing = phase63PluralKeys
        .flatMap((key) => [`${key}_one`, `${key}_other`])
        .filter((key) => !source[key]?.trim());

      expect(missing).toEqual([]);
    },
  );

  it("renders the queue line plurals in English and German", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Progress_MoreQueued", { count: 1 })).toBe("1 more change is waiting.");
    expect(i18n.t("Server_Progress_MoreQueued", { count: 3 })).toBe("3 more changes are waiting.");

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Progress_MoreQueued", { count: 1 })).toBe("1 weitere Änderung wartet.");
    expect(i18n.t("Server_Progress_MoreQueued", { count: 3 })).toBe("3 weitere Änderungen warten.");
  });

  it("uses named placeholders in the sentence keys", () => {
    expect(english["Server_Activity_Job_Failed"]).toContain("{{action}}");
    expect(english["Server_Activity_RequestedBy"]).toContain("{{actor}}");
    expect(english["Server_Live_Updated"]).toContain("{{relative}}");
    expect(english["Server_Machine_ResourceHourly"]).toContain("{{amount}}");
  });
});

describe("Phase 64 locale vocabulary", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("contains every Phase 64 key with a non-empty value in both languages", () => {
    const missing = phase64RequiredKeys.filter(
      (key) => !Object.hasOwn(english, key) || !english[key]?.trim() || !german[key]?.trim(),
    );

    expect(phase64RequiredKeys.length).toBeGreaterThanOrEqual(77);
    expect(missing).toEqual([]);
  });

  it("lists every Server_Create_ key of the locale files", () => {
    const unlisted = Object.keys(english).filter(
      (key) => key.startsWith("Server_Create_") && !phase64RequiredKeys.includes(key),
    );

    expect(unlisted).toEqual([]);
  });

  it("keeps the named interpolation variables of every Server_Create_ key equal in both languages", () => {
    const mismatched = phase64RequiredKeys.filter(
      (key) => placeholderSet(english[key] ?? "") !== placeholderSet(german[key] ?? ""),
    );

    expect(mismatched).toEqual([]);
  });

  it("names the documented interpolation variables", () => {
    expect(placeholderSet(english["Server_Create_BilledTo"] ?? "")).toBe("owner");
    expect(placeholderSet(english["Server_Create_Cost_Hourly"] ?? "")).toBe("hourly");
    expect(placeholderSet(english["Server_Create_Cost_Monthly"] ?? "")).toBe("monthly");
    expect(placeholderSet(english["Server_Create_Toast_KeepOff"] ?? "")).toBe("name");
  });

  it("renders the invalid-submit summary plurals in English and German", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Create_Invalid", { count: 1 })).toBe(
      "Check the highlighted field before creating.",
    );
    expect(i18n.t("Server_Create_Invalid", { count: 3 })).toBe(
      "Check the 3 highlighted fields before creating.",
    );

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Create_Invalid", { count: 1 })).toBe(
      "Prüfe das markierte Feld, bevor du erstellst.",
    );
    expect(i18n.t("Server_Create_Invalid", { count: 3 })).toBe(
      "Prüfe die 3 markierten Felder, bevor du erstellst.",
    );
  });

  it.each(languages)(
    "resolves no create button label to a banned generic label in %s",
    (_n, source) => {
      const buttonKeys = phase64RequiredKeys.filter(isButtonLabelKey);
      const offending = buttonKeys.filter((key) =>
        bannedLabels.has((source[key] ?? "").trim().toLocaleLowerCase()),
      );

      expect(buttonKeys.length).toBeGreaterThanOrEqual(8);
      expect(offending).toEqual([]);
    },
  );
});

describe("Phase 65 locale vocabulary", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("lists exactly 164 keys", () => {
    expect(phase65RequiredKeys.length).toBe(164);
    expect(new Set(phase65RequiredKeys).size).toBe(164);
  });

  it("keeps every credentials status line within the two-line reservation", () => {
    const statusKeys = [
      "Server_Credentials_Configured",
      "React_ServerWorkspace_NotSet",
      "Server_Credentials_StagedSet",
      "Server_Credentials_RotateStaged",
    ] as const;
    const tooLong = languages.flatMap(([language, source]) =>
      statusKeys
        .map((key) => ({
          key,
          text: (source[key] ?? "").replace("{{version}}", "9999"),
        }))
        .filter(({ text }) => text.length === 0 || text.length > 40)
        .map(({ key, text }) => `${language}:${key}:${text.length}`),
    );

    expect(tooLong).toEqual([]);
  });

  it("contains every Phase 65 key with a non-empty value in both languages", () => {
    const missing = phase65RequiredKeys.filter(
      (key) => !Object.hasOwn(english, key) || !english[key]?.trim() || !german[key]?.trim(),
    );

    expect(missing).toEqual([]);
  });

  it("keeps the named interpolation variables of every Phase 65 key equal in both languages", () => {
    const mismatched = phase65RequiredKeys.filter(
      (key) => placeholderSet(english[key] ?? "") !== placeholderSet(german[key] ?? ""),
    );

    expect(mismatched).toEqual([]);
  });

  it("renders the pending changes bar sentence plurals in English and German", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Change_Bar_Status", { count: 1 })).toBe(
      "1 change staged. Nothing is saved until you apply it.",
    );
    expect(i18n.t("Server_Change_Bar_Status", { count: 3 })).toBe(
      "3 changes staged. Nothing is saved until you apply them.",
    );

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Change_Bar_Status", { count: 1 })).toBe(
      "1 Änderung vorgemerkt. Nichts wird gespeichert, bis du sie anwendest.",
    );
    expect(i18n.t("Server_Change_Bar_Status", { count: 3 })).toBe(
      "3 Änderungen vorgemerkt. Nichts wird gespeichert, bis du sie anwendest.",
    );
  });

  it.each(languages)(
    "has both plural forms of every Phase 65 plural key in %s",
    (_name, source) => {
      const bases = [
        "Server_Change_Bar_Status",
        "Server_Change_Invalid",
        "Server_Change_DiscardDialog_Body",
        "Server_Change_Apply",
        "Server_Change_Retry",
        "Server_Change_Toast_Applied",
        "Server_Change_Leave_Body",
        "Server_Settings_Advanced_Summary",
        "Server_Settings_Advanced_SummaryChanged",
      ];
      const missing = bases
        .flatMap((key) => [`${key}_one`, `${key}_other`])
        .filter((key) => !source[key]?.trim());

      expect(missing).toEqual([]);
    },
  );

  it("lists every key of the new change, mods, credentials and files areas", () => {
    const unlisted = Object.keys(english).filter(
      (key) =>
        /^Server_(Change|Mods|Credentials|Files)_/.test(key) && !phase65RequiredKeys.includes(key),
    );

    expect(unlisted).toEqual([]);
  });

  it.each(languages)(
    "resolves no Phase 65 button label to a banned generic label in %s",
    (_name, source) => {
      const buttonKeys = phase65RequiredKeys.filter(isButtonLabelKey);
      const offending = buttonKeys.filter((key) =>
        bannedLabels.has((source[key] ?? "").trim().toLocaleLowerCase()),
      );

      expect(buttonKeys.length).toBe(phase65ButtonKeys.length);
      expect(offending).toEqual([]);
    },
  );
});

/** Every key Phase 66 adds: the alert area, the accrued and alert rows of the cost strip and two shared keys. */
const phase66RequiredKeys: readonly string[] = [
  "Server_Alerts_Amounts_Add",
  "Server_Alerts_Amounts_AddedAnnounce",
  "Server_Alerts_Amounts_Counter",
  "Server_Alerts_Amounts_Description",
  "Server_Alerts_Amounts_EmptyBody",
  "Server_Alerts_Amounts_EmptyTitle",
  "Server_Alerts_Amounts_Label",
  "Server_Alerts_Amounts_Remove",
  "Server_Alerts_Amounts_RemoveLabel",
  "Server_Alerts_Amounts_RemovedAnnounce",
  "Server_Alerts_Amounts_Title",
  "Server_Alerts_Banner_Ended",
  "Server_Alerts_Banner_Older_one",
  "Server_Alerts_Banner_Older_other",
  "Server_Alerts_Banner_RowReached",
  "Server_Alerts_Banner_Title_one",
  "Server_Alerts_Banner_Title_other",
  "Server_Alerts_Channel_Discord",
  "Server_Alerts_Channel_Email",
  "Server_Alerts_Channel_InApp",
  "Server_Alerts_Channel_InAppHint",
  "Server_Alerts_Channel_Status_AlwaysOn",
  "Server_Alerts_Channel_Status_NoAddress",
  "Server_Alerts_Channel_Status_NotLinked",
  "Server_Alerts_Channel_Status_Ready",
  "Server_Alerts_Channel_Status_Unavailable",
  "Server_Alerts_Channels_Description",
  "Server_Alerts_Channels_Title",
  "Server_Alerts_Discord_Group",
  "Server_Alerts_Discord_NoneGroup",
  "Server_Alerts_Discord_NotLinked",
  "Server_Alerts_Discord_OpenSettings",
  "Server_Alerts_Discord_Self",
  "Server_Alerts_Discord_Unavailable",
  "Server_Alerts_Dismiss",
  "Server_Alerts_DismissAll",
  "Server_Alerts_DismissAllLabel",
  "Server_Alerts_DismissError",
  "Server_Alerts_DismissLabel",
  "Server_Alerts_DismissedAll_Toast_one",
  "Server_Alerts_DismissedAll_Toast_other",
  "Server_Alerts_Dismissed_Toast",
  "Server_Alerts_Email_Group",
  "Server_Alerts_Email_NoAddress",
  "Server_Alerts_Email_NoneGroup",
  "Server_Alerts_Email_NotConfigured",
  "Server_Alerts_Email_Self",
  "Server_Alerts_Invalid_Duplicate",
  "Server_Alerts_Invalid_Max",
  "Server_Alerts_Invalid_Min",
  "Server_Alerts_Invalid_Number",
  "Server_Alerts_Invalid_Precision",
  "Server_Alerts_Invalid_Required",
  "Server_Alerts_Language_Group",
  "Server_Alerts_Language_Self",
  "Server_Alerts_Leave_Body",
  "Server_Alerts_Leave_Title",
  "Server_Alerts_Menu_Badge_one",
  "Server_Alerts_Menu_Badge_other",
  "Server_Alerts_Menu_More_one",
  "Server_Alerts_Menu_More_other",
  "Server_Alerts_Menu_Reached",
  "Server_Alerts_Page_Description",
  "Server_Alerts_Page_Forbidden",
  "Server_Alerts_Page_LoadError",
  "Server_Alerts_Page_Loading",
  "Server_Alerts_Page_NoOwner",
  "Server_Alerts_Page_NotFound",
  "Server_Alerts_Page_Owner",
  "Server_Alerts_Page_Title",
  "Server_Alerts_Reload",
  "Server_Alerts_Row_Amount",
  "Server_Alerts_Save",
  "Server_Alerts_Save_Pending",
  "Server_Alerts_Status_Conflict",
  "Server_Alerts_Status_Dirty",
  "Server_Alerts_Status_Discarded",
  "Server_Alerts_Status_Invalid_one",
  "Server_Alerts_Status_Invalid_other",
  "Server_Alerts_Status_Pristine",
  "Server_Alerts_Status_SaveError",
  "Server_Alerts_Status_Saved",
  "Server_Alerts_Toast",
  "Server_Alerts_Toast_Saved",
  "Server_Workspace_Cost_AccruedFinal",
  "Server_Workspace_Cost_AccruedLoading",
  "Server_Workspace_Cost_AccruedNone",
  "Server_Workspace_Cost_AccruedUnknown",
  "Server_Workspace_Cost_AccruedReason_Rate",
  "Server_Workspace_Cost_AccruedTiny",
  "Server_Workspace_Cost_AccruedAsOf",
  "Server_Workspace_Cost_AccruedBilling_Hour",
  "Server_Workspace_Cost_AccruedBilling_HourNext",
  "Server_Workspace_Cost_AccruedBilling_Minute",
  "Server_Workspace_Cost_AccruedStopped",
  "Server_Workspace_Cost_AccruedMaybe",
  "Server_Workspace_Cost_AccruedStoppedAnnounce",
  "Server_Workspace_Cost_Alerts",
  "Server_Workspace_Cost_AlertsNone",
  "Server_Workspace_Cost_AlertsList",
  "Server_Workspace_Cost_AlertsListMore",
  "Server_Workspace_Cost_AlertsSet",
  "Server_Workspace_Cost_AlertsManage",
  "Server_Confirm_Fact_Accrued",
  "Shared_CommandCenter_CurrentArea_CostAlerts",
];

/** Phase 66 keys that carry `_one` and `_other` forms. */
const phase66PluralBases = [
  "Server_Alerts_Banner_Title",
  "Server_Alerts_Banner_Older",
  "Server_Alerts_Menu_More",
  "Server_Alerts_Menu_Badge",
  "Server_Alerts_Status_Invalid",
  "Server_Alerts_DismissedAll_Toast",
] as const;

/** Phase 66 keys whose values are shown as button labels and so must name their effect. */
const phase66ButtonKeys: readonly string[] = [
  "Server_Alerts_Dismiss",
  "Server_Alerts_DismissAll",
  "Server_Alerts_Amounts_Remove",
  "Server_Alerts_Amounts_Add",
  "Server_Alerts_Save",
  "Server_Alerts_Save_Pending",
  "Server_Alerts_Reload",
  "Server_Alerts_Discord_OpenSettings",
  "Server_Workspace_Cost_AlertsSet",
  "Server_Workspace_Cost_AlertsManage",
];

describe("Phase 66 locale vocabulary", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("lists the Phase 66 keys once each", () => {
    expect(new Set(phase66RequiredKeys).size).toBe(phase66RequiredKeys.length);
    expect(
      phase66RequiredKeys.filter((key) => key.startsWith("Server_Alerts_")).length,
    ).toBeGreaterThanOrEqual(60);
  });

  it("contains every Phase 66 key with a non-empty value in both languages", () => {
    const missing = phase66RequiredKeys.filter(
      (key) => !Object.hasOwn(english, key) || !english[key]?.trim() || !german[key]?.trim(),
    );

    expect(missing).toEqual([]);
  });

  it("lists every Server_Alerts_ and accrued strip key, so none is added unlisted", () => {
    const unlisted = Object.keys(english).filter(
      (key) =>
        /^(Server_Alerts_|Server_Workspace_Cost_(Accrued|Alerts))/.test(key) &&
        key !== "Server_Workspace_Cost_Accrued" &&
        !phase66RequiredKeys.includes(key),
    );

    expect(unlisted).toEqual([]);
  });

  it("keeps the named placeholders of every Server_Alerts_ and Server_Workspace_Cost_ key equal", () => {
    const mismatched = Object.keys(english)
      .filter((key) => /^(Server_Alerts_|Server_Workspace_Cost_)/.test(key))
      .filter((key) => placeholderSet(english[key] ?? "") !== placeholderSet(german[key] ?? ""));

    expect(mismatched).toEqual([]);
  });

  it.each(languages)(
    "has both plural forms of every Phase 66 plural key in %s",
    (_name, source) => {
      const missing = phase66PluralBases
        .flatMap((key) => [`${key}_one`, `${key}_other`])
        .filter((key) => !source[key]?.trim());

      expect(missing).toEqual([]);
    },
  );

  it("renders the alert title plurals in English and German", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Alerts_Banner_Title", { count: 1 })).toBe("Cost alert");
    expect(i18n.t("Server_Alerts_Banner_Title", { count: 3 })).toBe("3 cost alerts");

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Alerts_Banner_Title", { count: 1 })).toBe("Kostenwarnung");
    expect(i18n.t("Server_Alerts_Banner_Title", { count: 3 })).toBe("3 Kostenwarnungen");
  });

  it("renders the badge description and the invalid status plurals", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Alerts_Menu_Badge", { count: 1 })).toBe("1 server needs your attention");
    expect(i18n.t("Server_Alerts_Menu_Badge", { count: 2 })).toBe("2 servers need your attention");
    expect(i18n.t("Server_Alerts_Status_Invalid", { count: 2 })).toBe(
      "Fix the 2 highlighted amounts before saving.",
    );

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Alerts_Menu_Badge", { count: 2 })).toBe(
      "2 Server brauchen deine Aufmerksamkeit",
    );
  });

  it.each(languages)(
    "resolves no Phase 66 button label to a banned generic label in %s",
    (_name, source) => {
      const offending = phase66ButtonKeys.filter((key) =>
        bannedLabels.has((source[key] ?? "").trim().toLocaleLowerCase()),
      );
      const alertButtons = Object.keys(english).filter((key) =>
        /^Server_Alerts_(Dismiss|DismissAll|Amounts_Remove|Amounts_Add|Save|Save_Pending|Reload|Discord_OpenSettings)$/.test(
          key,
        ),
      );

      expect(offending).toEqual([]);
      expect(alertButtons.every((key) => phase66ButtonKeys.includes(key))).toBe(true);
      expect(phase66ButtonKeys.every((key) => (source[key] ?? "").trim().length > 0)).toBe(true);
    },
  );

  it("uses the carried cost-stop phrase for the banner and has no alert-specific stop label", () => {
    expect(Object.keys(english).filter((key) => /^Server_Alerts_.*Stop/.test(key))).toEqual([]);
    expect(english["Server_Action_Stop_Label"]).toBe("Shut down and stop billing");
  });
});

describe("fleet locale vocabulary", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it.each(languages)("has both plural forms of every fleet plural key in %s", (_name, source) => {
    const missing = fleetPluralKeys
      .flatMap((key) => [`${key}_one`, `${key}_other`])
      .filter((key) => !source[key]?.trim());

    expect(missing).toEqual([]);
  });

  it("renders the English result count and cost line plurals", async () => {
    await i18n.changeLanguage("en");

    expect(i18n.t("Server_Fleet_ResultCount", { count: 1, shown: 1 })).toBe("1 of 1 server shown");
    expect(i18n.t("Server_Fleet_ResultCount", { count: 5, shown: 2 })).toBe("2 of 5 servers shown");
    expect(i18n.t("Server_Fleet_CostLine", { count: 1 })).toBe(
      "1 server is costing money or may still cost money. Shut down servers you are not using.",
    );
    expect(i18n.t("Server_Fleet_CostLine", { count: 3 })).toBe(
      "3 servers are costing money or may still cost money. Shut down servers you are not using.",
    );
  });

  it("renders the German result count and cost line plurals", async () => {
    await i18n.changeLanguage("de");

    expect(i18n.t("Server_Fleet_ResultCount", { count: 1, shown: 1 })).toBe(
      "1 von 1 Server angezeigt",
    );
    expect(i18n.t("Server_Fleet_ResultCount", { count: 5, shown: 2 })).toBe(
      "2 von 5 Server angezeigt",
    );
    expect(i18n.t("Server_Fleet_CostLine", { count: 1 })).toBe(
      "1 Server verursacht Kosten oder könnte noch Kosten verursachen. Fahre Server herunter, die du nicht nutzt.",
    );
    expect(i18n.t("Server_Fleet_CostLine", { count: 3 })).toBe(
      "3 Server verursachen Kosten oder könnten noch Kosten verursachen. Fahre Server herunter, die du nicht nutzt.",
    );
  });

  it("renders the active section title with a count on a key without plural forms", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Fleet_Section_Active", { count: 3 })).toBe("Active (3)");

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Fleet_Section_Active", { count: 3 })).toBe("Aktiv (3)");
  });

  it("names the navigation item and the fleet title Servers in English and Server in German", () => {
    expect(english["Hosting_Nav_Servers"]).toBe("Servers");
    expect(english["Hosting_Servers_Title"]).toBe(english["Hosting_Nav_Servers"]);
    expect(german["Hosting_Nav_Servers"]).toBe("Server");
    expect(german["Hosting_Servers_Title"]).toBe(german["Hosting_Nav_Servers"]);
  });

  it.each([
    ["en", english, ["manage", "administer", "operate", "deploy", "provider", "billing account"]],
    ["de", german, ["verwalt", "administr", "bereitstell", "anbieter"]],
  ] as const)("keeps admin wording out of the fleet description in %s", (_name, source, terms) => {
    const description = source["Hosting_Servers_Description"]?.toLocaleLowerCase() ?? "";

    expect(description).not.toBe("");
    expect(terms.filter((term) => description.includes(term))).toEqual([]);
  });

  it.each([
    ["en", english, ["Cancel", "OK", "Submit", "Save", "Confirm", "Open", "Refresh"]],
    [
      "de",
      german,
      ["Abbrechen", "OK", "Absenden", "Speichern", "Bestätigen", "Öffnen", "Aktualisieren"],
    ],
  ] as const)("uses no bare label for a fleet button in %s", (_name, source, banned) => {
    const bare = new Set(banned.map((label) => label.toLocaleLowerCase()));
    const offending = fleetButtonKeys.filter((key) =>
      bare.has((source[key] ?? "").trim().toLocaleLowerCase()),
    );

    expect(fleetButtonKeys.every((key) => Boolean(source[key]?.trim()))).toBe(true);
    expect(offending).toEqual([]);
  });
});

/** Every key Phase 68 adds (68-UI-SPEC Copywriting Contract, Plan 68-04): panel, schedules, consent, notice, activity. */
const phase68RequiredKeys: readonly string[] = [
  "Server_Automation_Description",
  "Server_Automation_Warning_Lead_one",
  "Server_Automation_Warning_Lead_other",
  "Server_Automation_Warning_Link",
  "Server_Automation_ReadOnly",
  "Server_Automation_Unavailable",
  "Server_Automation_Status_Label",
  "Server_Automation_Status_Idle",
  "Server_Automation_Status_Max",
  "Server_Automation_Status_Next",
  "Server_Automation_Status_Off",
  "Server_Automation_Status_Idle_NotAvailable",
  "Server_Automation_Status_Idle_Armed_one",
  "Server_Automation_Status_Idle_Armed_other",
  "Server_Automation_Status_Idle_Players",
  "Server_Automation_Status_Idle_Running",
  "Server_Automation_Status_Idle_Unknown",
  "Server_Automation_Status_Max_Armed_one",
  "Server_Automation_Status_Max_Armed_other",
  "Server_Automation_Status_Max_Running",
  "Server_Automation_Status_Next_None",
  "Server_Automation_Status_Next_Value",
  "Server_Automation_Idle_Title",
  "Server_Automation_Idle_Description",
  "Server_Automation_Idle_Enable",
  "Server_Automation_Idle_EnableHint",
  "Server_Automation_Idle_NotAvailable",
  "Server_Automation_Idle_Minutes",
  "Server_Automation_Idle_MinutesHint",
  "Server_Automation_Max_Title",
  "Server_Automation_Max_Description",
  "Server_Automation_Max_Enable",
  "Server_Automation_Max_EnableHint",
  "Server_Automation_Max_Hours",
  "Server_Automation_Max_HoursHint",
  "Server_Automation_Invalid_IdleMinutes",
  "Server_Automation_Invalid_MaxHours",
  "Server_Automation_Schedules_Title",
  "Server_Automation_Schedules_Description",
  "Server_Automation_Schedules_Zone",
  "Server_Automation_Schedules_ZoneHint",
  "Server_Automation_Schedules_EmptyTitle",
  "Server_Automation_Schedules_EmptyBody",
  "Server_Automation_Rule_Title",
  "Server_Automation_Rule_Action",
  "Server_Automation_Rule_StartHelp",
  "Server_Automation_Rule_StopHelp",
  "Server_Automation_Rule_Days",
  "Server_Automation_Rule_Time",
  "Server_Automation_Rule_Summary_Daily",
  "Server_Automation_Rule_Summary_Weekdays",
  "Server_Automation_Rule_Summary_Days",
  "Server_Automation_Rule_Summary_Incomplete",
  "Server_Automation_Rule_NextRun",
  "Server_Automation_Rule_NotSaved",
  "Server_Automation_Rule_Paused",
  "Server_Automation_Rule_LastRun_None",
  "Server_Automation_Rule_LastRun_Started",
  "Server_Automation_Rule_LastRun_Stopped",
  "Server_Automation_Rule_LastRun_SkippedRunning",
  "Server_Automation_Rule_LastRun_SkippedOff",
  "Server_Automation_Rule_LastRun_SkippedCost",
  "Server_Automation_Rule_LastRun_SkippedConsent",
  "Server_Automation_Rule_LastRun_SkippedMissed",
  "Server_Automation_Rule_LastRun_SkippedBusy",
  "Server_Automation_Rule_LastRun_WaitingForStop",
  "Server_Automation_Rule_LastRun_SkippedStopUnfinished",
  "Server_Automation_Rule_LastRun_Failed",
  "Server_Automation_Rule_LastRun_Other",
  "Server_Automation_Rule_Remove",
  "Server_Automation_Rule_RemoveLabel",
  "Server_Automation_Schedules_Add",
  "Server_Automation_Schedules_Counter",
  "Server_Automation_Schedules_AddedAnnounce",
  "Server_Automation_Schedules_RemovedAnnounce",
  "Server_Automation_Invalid_Days",
  "Server_Automation_Invalid_Time",
  "Server_Automation_Invalid_Duplicate",
  "Server_Automation_Save",
  "Server_Automation_Save_Pending",
  "Server_Automation_Status_Pristine",
  "Server_Automation_Status_Dirty",
  "Server_Automation_Status_Saved",
  "Server_Automation_Status_Discarded",
  "Server_Automation_Status_Invalid_one",
  "Server_Automation_Status_Invalid_other",
  "Server_Automation_Status_SaveError",
  "Server_Automation_Status_Conflict",
  "Server_Automation_Status_Forbidden",
  "Server_Automation_Reload",
  "Server_Automation_Toast_Saved",
  "Server_Automation_Leave_Body",
  "Server_Automation_Leave_BodyBoth",
  "Server_Automation_Consent_Title",
  "Server_Automation_Consent_Body",
  "Server_Automation_Consent_Skip",
  "Server_Automation_Consent_StartsLabel",
  "Server_Automation_Consent_Starts_one",
  "Server_Automation_Consent_Starts_other",
  "Server_Automation_Consent_Confirm",
  "Server_Automation_Consent_NeededTitle",
  "Server_Automation_Consent_NeededBody",
  "Server_Automation_Consent_NeededBodyViewer",
  "Server_Automation_Consent_Reconfirm",
  "Server_Automation_Notice_Title",
  "Server_Automation_Notice_Reason_Idle_one",
  "Server_Automation_Notice_Reason_Idle_other",
  "Server_Automation_Notice_Reason_Max_one",
  "Server_Automation_Notice_Reason_Max_other",
  "Server_Automation_Notice_Reason_Schedule",
  "Server_Automation_Notice_Consequence",
  "Server_Automation_Notice_Relative",
  "Server_Automation_Notice_Change",
  "Server_Automation_Toast_Warning",
  "Server_Automation_Toast_Cancelled_Idle",
  "Server_Automation_Toast_Cancelled_Settings",
  "Server_Automation_Menu_Line",
  "Server_Automation_Menu_DismissLabel",
  "Server_Activity_Trigger_Idle_one",
  "Server_Activity_Trigger_Idle_other",
  "Server_Activity_Trigger_Max_one",
  "Server_Activity_Trigger_Max_other",
  "Server_Activity_Trigger_Schedule",
  "Server_Activity_Audit_AutomaticStopWarned",
  "Server_Activity_Audit_AutomaticStopRequested",
  "Server_Activity_Audit_AutomaticStopCancelled",
  "Server_Activity_Audit_ScheduledStartRequested",
  "Server_Activity_Audit_ScheduledRunSkipped",
  "Server_Activity_Audit_AutomationSettingsChanged",
  "Server_Activity_Audit_ScheduledStartsConfirmed",
  "Server_Overview_Automation",
  "Server_Overview_Automation_Idle_one",
  "Server_Overview_Automation_Idle_other",
  "Server_Overview_Automation_Max_one",
  "Server_Overview_Automation_Max_other",
  "Server_Overview_Automation_Next",
  "Server_Overview_Automation_Link",
];

/** Phase 68 keys that carry `_one` and `_other` forms. */
const phase68PluralBases = [
  "Server_Automation_Warning_Lead",
  "Server_Automation_Status_Idle_Armed",
  "Server_Automation_Status_Max_Armed",
  "Server_Automation_Status_Invalid",
  "Server_Automation_Consent_Starts",
  "Server_Automation_Notice_Reason_Idle",
  "Server_Automation_Notice_Reason_Max",
  "Server_Activity_Trigger_Idle",
  "Server_Activity_Trigger_Max",
  "Server_Overview_Automation_Idle",
  "Server_Overview_Automation_Max",
] as const;

/** Phase 68 keys whose values are shown as button or link labels and so must name verb plus noun. */
const phase68ButtonKeys: readonly string[] = [
  "Server_Automation_Save",
  "Server_Automation_Warning_Link",
  "Server_Automation_Rule_Remove",
  "Server_Automation_Schedules_Add",
  "Server_Automation_Reload",
  "Server_Automation_Consent_Confirm",
  "Server_Automation_Consent_Reconfirm",
  "Server_Automation_Notice_Change",
  "Server_Overview_Automation_Link",
];

/** The generic labels the copy rules forbid, in addition to the ones banned in earlier phases. */
const phase68BannedLabels = new Set(
  [
    "Submit",
    "OK",
    "Cancel",
    "Click here",
    "Save",
    "Confirm",
    "Open",
    "Absenden",
    "Abbrechen",
    "Hier klicken",
    "Speichern",
    "Bestätigen",
    "Öffnen",
  ].map((label) => label.toLocaleLowerCase()),
);

/** The seven audit kinds that Phase 68 adds to the Activity timeline. */
const phase68AuditKinds = [
  "AutomaticStopWarned",
  "AutomaticStopRequested",
  "AutomaticStopCancelled",
  "ScheduledStartRequested",
  "ScheduledRunSkipped",
  "AutomationSettingsChanged",
  "ScheduledStartsConfirmed",
] as const;

describe("Phase 68 locale vocabulary", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("lists the Phase 68 keys once each", () => {
    expect(new Set(phase68RequiredKeys).size).toBe(phase68RequiredKeys.length);
    expect(
      phase68RequiredKeys.filter((key) => key.startsWith("Server_Automation_")).length,
    ).toBeGreaterThanOrEqual(90);
  });

  it("contains every Phase 68 key with a non-empty value in both languages", () => {
    const missing = phase68RequiredKeys.filter(
      (key) => !Object.hasOwn(english, key) || !english[key]?.trim() || !german[key]?.trim(),
    );

    expect(missing).toEqual([]);
  });

  it("lists every Server_Automation_ and Server_Activity_Trigger_ key, so none is added unlisted", () => {
    const unlisted = Object.keys(english).filter(
      (key) =>
        /^(Server_Automation_|Server_Activity_Trigger_|Server_Overview_Automation)/.test(key) &&
        !phase68RequiredKeys.includes(key),
    );

    expect(unlisted).toEqual([]);
  });

  it("keeps the named placeholders of every Phase 68 key equal in both languages", () => {
    const mismatched = phase68RequiredKeys.filter(
      (key) => placeholderSet(english[key] ?? "") !== placeholderSet(german[key] ?? ""),
    );

    expect(mismatched).toEqual([]);
  });

  it.each(languages)(
    "has both plural forms of every Phase 68 plural key in %s",
    (_name, source) => {
      const missing = phase68PluralBases
        .flatMap((key) => [`${key}_one`, `${key}_other`])
        .filter((key) => !source[key]?.trim());

      expect(missing).toEqual([]);
    },
  );

  it("gives every Phase 68 plural form a count placeholder in the other form", () => {
    const missing = phase68PluralBases.filter(
      (key) => !(english[`${key}_other`] ?? "").includes("{{count}}"),
    );

    expect(missing).toEqual([]);
  });

  it("adds the seven audit kinds in both languages", () => {
    const missing = phase68AuditKinds
      .map((kind) => `Server_Activity_Audit_${kind}`)
      .filter((key) => !english[key]?.trim() || !german[key]?.trim());

    expect(phase68AuditKinds.length).toBe(7);
    expect(missing).toEqual([]);
  });

  it("renders the status and trigger plurals in English and German", async () => {
    await i18n.changeLanguage("en");
    expect(i18n.t("Server_Automation_Status_Idle_Armed", { count: 1 })).toBe(
      "After 1 minute without players.",
    );
    expect(i18n.t("Server_Automation_Status_Idle_Armed", { count: 30 })).toBe(
      "After 30 minutes without players.",
    );
    expect(i18n.t("Server_Activity_Trigger_Max", { count: 6 })).toBe(
      "Automatic: maximum runtime of 6 hours reached",
    );

    await i18n.changeLanguage("de");
    expect(i18n.t("Server_Automation_Status_Idle_Armed", { count: 1 })).toBe(
      "Nach 1 Minute ohne Spieler.",
    );
    expect(i18n.t("Server_Automation_Status_Idle_Armed", { count: 30 })).toBe(
      "Nach 30 Minuten ohne Spieler.",
    );
    expect(i18n.t("Server_Activity_Trigger_Max", { count: 6 })).toBe(
      "Automatisch: maximale Laufzeit von 6 Stunden erreicht",
    );
  });

  it("uses the app terms cost owner and cloud machine in both languages", () => {
    expect(german["Server_Automation_Consent_Body"]).toContain("Kostenträger");
    expect(german["Server_Automation_Consent_Body"]).toContain("Cloud-Maschine");
    expect(english["Server_Automation_Consent_Body"]).toContain("cost owner");
    expect(english["Server_Automation_Consent_Body"]).toContain("cloud machine");
  });

  it.each(languages)(
    "resolves no Phase 68 button label to a banned generic label in %s",
    (_name, source) => {
      const offending = phase68ButtonKeys.filter((key) =>
        phase68BannedLabels.has((source[key] ?? "").trim().toLocaleLowerCase()),
      );

      expect(phase68ButtonKeys.every((key) => (source[key] ?? "").trim().length > 0)).toBe(true);
      expect(offending).toEqual([]);
    },
  );

  it.each(languages)(
    "gives every Phase 68 button label more than one word in %s",
    (_name, source) => {
      const single = phase68ButtonKeys.filter(
        (key) => (source[key] ?? "").trim().split(/\s+/).length < 2,
      );

      expect(single).toEqual([]);
    },
  );
});

/** Labels that never name an outcome, in lowercase; the base list applies to every button label (D-11). */
const bannedButtonLabels: ReadonlySet<string> = new Set([
  "cancel",
  "abbrechen",
  "ok",
  "submit",
  "absenden",
  "confirm",
  "bestätigen",
  "yes",
  "no",
  "ja",
  "nein",
]);

/** The bare save labels, banned for the dismiss and confirm labels of dialogs and editor leave controls. */
const bannedBareSaveLabels: readonly string[] = ["save", "speichern"];

/** The close labels; they pass only as the aria-label of an icon-only dismiss or in a read-only dialog. */
const closeLabels: readonly string[] = ["close", "schließen"];

/** The JSX tags whose text child is a button label. */
const buttonTags: ReadonlySet<string> = new Set(["ActionButton", "ActionLink", "Button", "button"]);

/** One dismiss, confirm or button label key that resolves to a banned generic value. */
interface BannedLabelFinding {
  file: string;
  line: number;
  control: string;
  key: string;
  language: string;
  value: string;
}

/** The result of one source scan: the findings and how many label keys were examined. */
interface BannedLabelScan {
  findings: BannedLabelFinding[];
  examined: number;
}

/** Whether an expression is a call of the translation function `t`. */
function isTranslateCall(node: ts.Node): node is ts.CallExpression {
  if (!ts.isCallExpression(node)) return false;
  const callee = node.expression;
  return (
    (ts.isIdentifier(callee) && callee.text === "t") ||
    (ts.isPropertyAccessExpression(callee) && callee.name.text === "t")
  );
}

/** Collects the literal first arguments of every `t(...)` call inside a node. */
function translationKeys(node: ts.Node): string[] {
  const keys: string[] = [];
  const visit = (child: ts.Node): void => {
    if (isTranslateCall(child)) {
      const first = child.arguments[0];
      if (first && (ts.isStringLiteral(first) || ts.isNoSubstitutionTemplateLiteral(first))) {
        keys.push(first.text);
      }
    }
    ts.forEachChild(child, visit);
  };
  visit(node);
  return keys;
}

/**
 * Finds every key passed as `cancelLabel` or `confirmLabel` or shown as the text of a button, and reports each one
 * whose English or German value is a banned generic label.
 *
 * `Close` and `Schließen` are reported for the dismiss and confirm labels of a dialog that has an `onConfirm`
 * decision; they pass as the label of a read-only dialog and as an icon-only `aria-label`, which is never scanned.
 *
 * @param fileName The file name, used for the report.
 * @param text The TSX source text.
 * @param locales The flat locale files by language.
 * @returns The findings and the number of label keys examined.
 */
function scanBannedLabels(
  fileName: string,
  text: string,
  locales: Readonly<Record<string, Readonly<Record<string, string>>>>,
): BannedLabelScan {
  const source = ts.createSourceFile(
    fileName,
    text,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TSX,
  );
  const findings: BannedLabelFinding[] = [];
  let examined = 0;

  const check = (
    node: ts.Node,
    control: string,
    keys: string[],
    banned: ReadonlySet<string>,
  ): void => {
    const line = source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;
    for (const key of keys) {
      examined += 1;
      for (const [language, values] of Object.entries(locales)) {
        const value = values[key];
        if (value !== undefined && banned.has(value.trim().toLocaleLowerCase())) {
          findings.push({ file: fileName, line, control, key, language, value });
        }
      }
    }
  };

  const visit = (node: ts.Node): void => {
    if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      const attributes = node.attributes.properties.filter(ts.isJsxAttribute);
      const decides = attributes.some(
        (attribute) => attribute.name.getText(source) === "onConfirm",
      );
      const banned = new Set([
        ...bannedButtonLabels,
        ...bannedBareSaveLabels,
        ...(decides ? closeLabels : []),
      ]);
      for (const attribute of attributes) {
        const name = attribute.name.getText(source);
        if (name !== "cancelLabel" && name !== "confirmLabel") continue;
        const initializer = attribute.initializer;
        if (initializer && ts.isJsxExpression(initializer) && initializer.expression) {
          check(attribute, name, translationKeys(initializer.expression), banned);
        }
      }
    }
    if (ts.isPropertyAssignment(node) && ts.isIdentifier(node.name)) {
      if (node.name.text === "cancelLabel" || node.name.text === "confirmLabel") {
        check(
          node,
          node.name.text,
          translationKeys(node.initializer),
          new Set([...bannedButtonLabels, ...bannedBareSaveLabels]),
        );
      }
    }
    if (ts.isJsxElement(node) && buttonTags.has(node.openingElement.tagName.getText(source))) {
      for (const child of node.children) {
        if (ts.isJsxExpression(child) && child.expression) {
          check(
            child,
            node.openingElement.tagName.getText(source),
            translationKeys(child.expression),
            bannedButtonLabels,
          );
        }
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return { findings, examined };
}

/** Lists every `.tsx` file below a directory. */
function tsxFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) return tsxFiles(path);
    return name.endsWith(".tsx") ? [path] : [];
  });
}

describe("banned dismiss and confirm labels app-wide (D-11)", () => {
  const locales = { en: english, de: german };
  const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

  it("resolves no cancelLabel, confirmLabel or button label to a banned generic value in any component", () => {
    const scans = tsxFiles(sourceRoot).map((file) =>
      scanBannedLabels(file, readFileSync(file, "utf8"), locales),
    );
    const findings = scans
      .flatMap((scan) => scan.findings)
      .map(
        (finding) =>
          `${finding.file.slice(sourceRoot.length + 1)}:${finding.line} ${finding.control} ${finding.key} = ${finding.language}:${finding.value}`,
      );

    expect(findings).toEqual([]);
    expect(scans.reduce((sum, scan) => sum + scan.examined, 0)).toBeGreaterThan(100);
  });

  it("reports an inline sample that uses a banned value (non-vacuity)", () => {
    const sample = [
      "export const Sample = () => (",
      '  <ConfirmDialog cancelLabel={t("Sample_Cancel")} confirmLabel={t("Sample_Save")} onConfirm={go} />',
      ");",
      'export const Plain = () => <ActionButton>{t("Sample_Ok")}</ActionButton>;',
    ].join("\n");
    const scan = scanBannedLabels("sample.tsx", sample, {
      en: { Sample_Cancel: "Cancel", Sample_Save: "Save", Sample_Ok: "OK" },
      de: { Sample_Cancel: "Abbrechen", Sample_Save: "Speichern", Sample_Ok: "OK" },
    });

    expect(
      scan.findings.map((finding) => `${finding.control}:${finding.language}:${finding.value}`),
    ).toEqual([
      "cancelLabel:en:Cancel",
      "cancelLabel:de:Abbrechen",
      "confirmLabel:en:Save",
      "confirmLabel:de:Speichern",
      "ActionButton:en:OK",
      "ActionButton:de:OK",
    ]);
  });

  it("accepts Close and Schließen as an icon-only aria-label or in a read-only dialog, and bans them on a decision", () => {
    const readOnly = scanBannedLabels(
      "read-only.tsx",
      [
        'export const Icon = () => <button aria-label={t("Sample_Close")} />;',
        'export const Info = () => <ConfirmDialog confirmLabel={t("Sample_Close")} />;',
      ].join("\n"),
      { en: { Sample_Close: "Close" }, de: { Sample_Close: "Schließen" } },
    );
    const decision = scanBannedLabels(
      "decision.tsx",
      'export const Ask = () => <ConfirmDialog confirmLabel={t("Sample_Close")} onConfirm={go} />;',
      { en: { Sample_Close: "Close" }, de: { Sample_Close: "Schließen" } },
    );

    expect(readOnly.findings).toEqual([]);
    expect(decision.findings.map((finding) => finding.value)).toEqual(["Close", "Schließen"]);
  });
});
