import { Add16Regular } from "@fluentui/react-icons";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useId, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { SelectField, SwitchField, TextField } from "@/components/FormFields";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { AutomationStatusList } from "@/components/server/AutomationStatusList";
import "@/components/server/automation.css";
import { ScheduleRuleFields } from "@/components/server/ScheduleRuleFields";
import {
  ScheduledStartConsentDialog,
  ScheduledStartsPausedNotice,
} from "@/components/server/ScheduledStartConsentDialog";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import "@/components/server/sections.css";
import {
  automationSaveFailure,
  useSaveAutomationSettings,
} from "@/features/hosting/useAutomationSettings";
import {
  draftFromSaved,
  isAutomationDraftDirty,
  needsStartConsent,
  sortRulesForDisplay,
  timeZoneOptions,
  toAutomationUpdate,
  validateAutomationDraft,
  type AutomationDraft,
  type AutomationUpdateInput,
  type NumberError,
  type RuleDraft,
} from "@/features/hosting/automationModel";
import { hostingQueryKeys } from "@/features/hosting/services";
import type {
  GameServer,
  ServerAutomation,
  ServerAutomationSettings,
} from "@/features/hosting/types";

/** The id of the panel; the deep link `?tab=settings#server-automation` targets it. */
const panelId = "server-automation";

/** The longest idle time text accepted, enough for the largest limit. */
const idleMaxLength = 4;

/** The longest runtime hours text accepted, enough for the largest limit. */
const hoursMaxLength = 3;

/** Source of draft rule keys. Module scoped so keys stay unique across renders without reading a ref in render. */
let keySequence = 0;

/**
 * Hands out the next local key of a draft rule.
 *
 * @returns A key no other rule of this page has had.
 */
function nextKey(): string {
  keySequence += 1;
  return `rule-${keySequence}`;
}

/**
 * Builds the draft a panel starts from, with the saved rules in display order (first day, then time).
 *
 * @param settings The saved settings.
 * @param browserZone The viewer's browser time zone, used while no zone was saved.
 * @returns The editable draft.
 */
function freshDraft(settings: ServerAutomationSettings, browserZone: string): AutomationDraft {
  const draft = draftFromSaved(settings, browserZone, nextKey);
  return { ...draft, rules: sortRulesForDisplay(draft.rules) };
}

/**
 * The time zones the runtime lists.
 *
 * @returns The IANA zone ids, or an empty list when the runtime cannot list them.
 */
function supportedTimeZones(): string[] {
  return typeof Intl.supportedValuesOf === "function" ? Intl.supportedValuesOf("timeZone") : [];
}

/** A new rule: start the server, no day and no time chosen yet. */
function blankRule(): RuleDraft {
  return { key: nextKey(), id: null, action: "Start", days: [], time: "" };
}

/** Where focus goes once the rule list changed and rendered: the first radio of a rule, or the Add button. */
type FocusTarget = { kind: "rule"; key: string } | { kind: "add" };

/**
 * The time zone the viewer's browser resolves.
 *
 * @returns The IANA zone id, or `UTC` when the runtime cannot tell.
 */
function browserTimeZone(): string {
  return new Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
}

/** Props of {@link AutomationPanel}. */
export interface AutomationPanelProps {
  /** The loaded server; the panel edits a draft copy of its saved automation settings. */
  server: GameServer;
  /**
   * Reports whether the panel holds unsaved changes. Called when the flag changes and with false when the panel goes
   * away, so the host's leave guard never keeps a stale flag.
   *
   * @param dirty True while the draft differs from the saved settings.
   */
  onDirtyChange: (dirty: boolean) => void;
  /**
   * Hands the host a callback that puts the draft back to the saved settings. The host calls it when the viewer
   * confirms leaving with unsaved changes.
   *
   * @param discard The callback that resets the draft.
   */
  registerDiscard: (discard: () => void) => void;
}

/**
 * How many automations the saved settings switch on: the idle shutdown, the maximum runtime and every weekly rule
 * (a rule has no enabled flag, so every saved rule counts).
 *
 * @param settings The saved settings.
 * @returns The number of active automations.
 */
function activeAutomationCount(settings: ServerAutomationSettings): number {
  return (
    (settings.idleShutdownEnabled ? 1 : 0) +
    (settings.maxRuntimeEnabled ? 1 : 0) +
    settings.schedules.length
  );
}

/**
 * The Automation panel of the Settings section: the idle shutdown and the maximum runtime of one server.
 *
 * The panel is a collapsible {@link SettingsPanel} with the id `server-automation`; the deep link
 * `?tab=settings#server-automation` opens it, scrolls it below the sticky header and focuses its header button. The
 * live summary reads from the saved settings and the badge reads "Unsaved" while the draft is dirty; a dirty draft also
 * keeps Collapse all from closing the panel. Without an `automation` block (an older API or a failed read) it shows
 * only its heading and the unavailable state, never a form with invented defaults.
 *
 * @param props The server and the two callbacks that connect the panel to the leave guard of the Settings section.
 * @returns The automation panel.
 */
export function AutomationPanel({ server, onDirtyChange, registerDiscard }: AutomationPanelProps) {
  const { t } = useTranslation();
  const automation = server.automation ?? null;
  // The panel needs the flag itself for its badge and its pin, next to handing it to the host.
  const [dirty, setDirty] = useState(false);
  const reportDirty = useCallback(
    (value: boolean) => {
      setDirty(value);
      onDirtyChange(value);
    },
    [onDirtyChange],
  );
  const summary =
    automation === null
      ? undefined
      : activeAutomationCount(automation.settings) === 0
        ? t("Server_Settings_Summary_Automation_Off")
        : t("Server_Settings_Summary_Automation_Active", {
            count: activeAutomationCount(automation.settings),
          });

  return (
    <SettingsPanel
      id={panelId}
      title={t("Server_Settings_AutomationTitle")}
      summary={summary}
      badge={
        dirty ? (
          <StatusBadge tone="info">{t("Server_Settings_Panel_Unsaved")}</StatusBadge>
        ) : undefined
      }
      stagedKeys={dirty ? [panelId] : undefined}
    >
      <div className="automation-panel" data-slot="automation">
        {automation === null ? (
          <StatePanel kind="info">{t("Server_Automation_Unavailable")}</StatePanel>
        ) : (
          <AutomationBody
            server={server}
            automation={automation}
            onDirtyChange={reportDirty}
            registerDiscard={registerDiscard}
          />
        )}
      </div>
    </SettingsPanel>
  );
}

/** Props of the internal {@link AutomationBody}. */
interface AutomationBodyProps extends AutomationPanelProps {
  /** The automation block of the server; never null here. */
  automation: ServerAutomation;
}

/** What the status line reports besides the pristine and dirty states. */
type Outcome = "none" | "saved" | "discarded" | "invalid" | "saveError" | "conflict" | "forbidden";

/** What the consent dialog saves: the draft, or the saved settings again to confirm paused scheduled starts. */
type ConsentMode = "draft" | "reconfirm";

/** The status copy of each failed save, shown inside the consent dialog when the save was started from it. */
const saveErrorKeys: Readonly<Partial<Record<Outcome, TranslationKey>>> = {
  saveError: "Server_Automation_Status_SaveError",
  conflict: "Server_Automation_Status_Conflict",
  forbidden: "Server_Automation_Status_Forbidden",
};

/** Which number fields were left once, which is when their validation message may first show. */
interface Touched {
  /** The idle minutes field. */
  idle: boolean;
  /** The maximum runtime hours field. */
  hours: boolean;
}

/**
 * The content of the panel for a server that has an automation block: lead, notices, the two settings and the save
 * row.
 *
 * The draft lives in React state only. Saving sends the version the settings were loaded with and replaces the
 * cached settings with the saved ones. A stale save keeps the draft and offers Reload settings. A viewer who may see
 * but not manage the settings (or any viewer of an archived server) gets disabled controls, one notice and no save
 * row. While the form is pristine the draft follows settings that change elsewhere; a dirty draft is kept.
 *
 * @param props The server, its automation block and the leave guard callbacks.
 * @returns The panel content.
 */
function AutomationBody({
  server,
  automation,
  onDirtyChange,
  registerDiscard,
}: AutomationBodyProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { showToast } = useAppToast();
  const save = useSaveAutomationSettings(server.primaryKey);
  const baseId = useId();
  const noticeId = `${baseId}-notice`;
  const idleEnableId = `${baseId}-idle-enable`;
  const saveButton = useRef<HTMLButtonElement>(null);
  const idleField = useRef<HTMLInputElement>(null);
  const hoursField = useRef<HTMLInputElement>(null);
  const addButton = useRef<HTMLButtonElement>(null);
  const ruleList = useRef<HTMLUListElement>(null);
  const pendingFocus = useRef<FocusTarget | null>(null);
  const zone = browserTimeZone();
  const locale = currentLocale();
  const counterId = `${baseId}-counter`;

  const saved = automation.settings;
  const { limits } = automation;
  const idleAvailable = automation.idleShutdown.available;
  const archived = server.isArchived;
  const editable = server.canManageAutomation === true && !archived;

  const [draft, setDraft] = useState<AutomationDraft>(() => freshDraft(saved, zone));
  const [baseline, setBaseline] = useState(saved);
  const [touched, setTouched] = useState<Touched>({ idle: false, hours: false });
  const [saveAttempted, setSaveAttempted] = useState(false);
  const [outcome, setOutcome] = useState<Outcome>("none");
  const [reloading, setReloading] = useState(false);
  const [consentMode, setConsentMode] = useState<ConsentMode | null>(null);
  const consentWasOpen = useRef(false);
  const [announcement, setAnnouncement] = useState({ text: "", count: 0 });

  const dirty = editable && isAutomationDraftDirty(draft, saved, zone);

  // The draft follows settings that change elsewhere only while it is pristine; the baseline is the saved state the
  // draft was last synchronized with. State adjusted during render, not an effect, so no stale frame is painted.
  if (baseline !== saved) {
    const pristine = !isAutomationDraftDirty(draft, baseline, zone);
    setBaseline(saved);
    if (pristine) setDraft(freshDraft(saved, zone));
  }

  const validation = validateAutomationDraft(draft, limits, idleAvailable);
  const { invalidCount } = validation;
  const busy = save.isPending;
  const atLimit = draft.rules.length >= limits.maxSchedules;
  const scheduleZone = saved.timeZone ?? "UTC";
  const consentNeeded = automation.startConsent.state === "Needed";

  // The saved zone and the draft zone are always options, even when the runtime does not list them.
  const zoneChoices = useMemo(
    () =>
      timeZoneOptions(
        [...supportedTimeZones(), ...(saved.timeZone === null ? [] : [saved.timeZone])],
        draft.timeZone,
      ),
    [saved.timeZone, draft.timeZone],
  );
  const zoneOptions = [
    { value: zoneChoices.utc, label: zoneChoices.utc, lang: "en" },
    ...zoneChoices.groups.map((group) => ({
      group: group.region,
      options: group.zones.map((choice) => ({ value: choice.id, label: choice.label, lang: "en" })),
    })),
  ];

  useEffect(() => {
    onDirtyChange(dirty);
  }, [dirty, onDirtyChange]);
  useEffect(() => () => onDirtyChange(false), [onDirtyChange]);

  // Declining or finishing the consent dialog returns focus to Save, also where the browser does not restore it.
  useEffect(() => {
    const open = consentMode !== null;
    if (consentWasOpen.current && !open) saveButton.current?.focus();
    consentWasOpen.current = open;
  }, [consentMode]);

  // Moves focus to the target an add or a remove left behind, once the new list is in the DOM.
  useEffect(() => {
    const target = pendingFocus.current;
    if (target === null) return;
    pendingFocus.current = null;
    if (target.kind === "add") {
      addButton.current?.focus();
      return;
    }
    ruleList.current
      ?.querySelector<HTMLElement>(`[data-rule-key="${target.key}"] input[type="radio"]`)
      ?.focus();
  });

  useEffect(() => {
    registerDiscard(() => {
      setDraft(freshDraft(saved, zone));
      setTouched({ idle: false, hours: false });
      setSaveAttempted(false);
      setOutcome("none");
    });
  }, [registerDiscard, saved, zone]);

  /** Clears the messages an edit makes outdated; an invalid count and a conflict stay until resolved. */
  const edited = () => {
    setOutcome((current) =>
      current === "saved" || current === "discarded" || current === "saveError" ? "none" : current,
    );
  };

  const change = (patch: Partial<AutomationDraft>) => {
    if (busy) return;
    edited();
    setDraft((current) => ({ ...current, ...patch }));
  };

  /** Tells assistive technology what an add or a remove did; the counter makes a repeat read again. */
  const announce = (text: string) => {
    setAnnouncement((current) => ({ text, count: current.count + 1 }));
  };

  const changeRule = (rule: RuleDraft) => {
    if (busy) return;
    edited();
    setDraft((current) => ({
      ...current,
      rules: current.rules.map((candidate) => (candidate.key === rule.key ? rule : candidate)),
    }));
  };

  const addRule = () => {
    if (busy || !editable || atLimit) return;
    edited();
    const rule = blankRule();
    pendingFocus.current = { kind: "rule", key: rule.key };
    setDraft((current) => ({ ...current, rules: [...current.rules, rule] }));
    announce(t("Server_Automation_Schedules_AddedAnnounce"));
  };

  const removeRule = (index: number) => {
    if (busy || !editable) return;
    edited();
    const neighbour = draft.rules[index + 1] ?? draft.rules[index - 1];
    pendingFocus.current =
      neighbour === undefined ? { kind: "add" } : { kind: "rule", key: neighbour.key };
    setDraft((current) => ({
      ...current,
      rules: current.rules.filter((_, position) => position !== index),
    }));
    announce(t("Server_Automation_Schedules_RemovedAnnounce"));
  };

  /** Focuses the first invalid field in page order: the numbers, then per rule the time and the days. */
  const focusFirstInvalid = () => {
    if (validation.idleMinutes !== null) {
      idleField.current?.focus();
      return;
    }
    if (validation.maxHours !== null) {
      hoursField.current?.focus();
      return;
    }
    const index = validation.rules.findIndex((rule) => rule.days || rule.time || rule.duplicate);
    const errors = validation.rules[index];
    if (errors === undefined) return;
    const field = errors.time || errors.duplicate ? "time" : "days";
    ruleList.current?.children[index]
      ?.querySelector<HTMLElement>(`[data-field="${field}"]`)
      ?.focus();
  };

  const numberMessage = (
    error: NumberError | null,
    touchedField: boolean,
    kind: "idle" | "hours",
  ) => {
    if (error === null || !(touchedField || saveAttempted)) return undefined;
    return kind === "idle"
      ? t("Server_Automation_Invalid_IdleMinutes", {
          min: limits.idleMinutesMin,
          max: limits.idleMinutesMax,
        })
      : t("Server_Automation_Invalid_MaxHours", {
          min: limits.maxRuntimeHoursMin,
          max: limits.maxRuntimeHoursMax,
        });
  };

  const touch = (field: keyof Touched) => () => {
    setTouched((current) => ({ ...current, [field]: true }));
  };

  const trySave = () => {
    if (busy) return;
    setSaveAttempted(true);
    if (invalidCount > 0) {
      setOutcome("invalid");
      focusFirstInvalid();
      return;
    }
    if (needsStartConsent(draft, saved)) {
      setConsentMode("draft");
      return;
    }
    send(toAutomationUpdate(draft, saved, false, idleAvailable), true);
  };

  /**
   * Sends one settings update and handles its outcome.
   *
   * @param update The body to send.
   * @param replaceDraft True when the draft was what got saved, so it is replaced by the saved settings. A
   *   reconfirmation saves the saved settings only; the draft stays and follows the saved settings while pristine.
   */
  const send = (update: AutomationUpdateInput, replaceDraft: boolean) => {
    setOutcome("none");
    save.mutate(update, {
      onSuccess: (result) => {
        if (replaceDraft) {
          setDraft(freshDraft(result.settings, zone));
          setBaseline(result.settings);
          setTouched({ idle: false, hours: false });
          setSaveAttempted(false);
        }
        setConsentMode(null);
        setOutcome("saved");
        showToast(t("Server_Automation_Toast_Saved"), "success");
      },
      onError: (error) => {
        const failure = automationSaveFailure(error);
        setOutcome(
          failure === "conflict" ? "conflict" : failure === "forbidden" ? "forbidden" : "saveError",
        );
        // A rejected right is confirmed by the server: the refetch turns the panel read-only.
        if (failure === "forbidden") {
          void queryClient.invalidateQueries({
            queryKey: hostingQueryKeys.server(server.primaryKey),
            exact: true,
          });
        }
      },
    });
  };

  const confirmConsent = () => {
    if (busy || consentMode === null) return;
    if (consentMode === "reconfirm") {
      send(toAutomationUpdate(freshDraft(saved, zone), saved, true, idleAvailable), false);
    } else {
      send(toAutomationUpdate(draft, saved, true, idleAvailable), true);
    }
  };

  const dismissConsent = () => {
    if (busy) return;
    setConsentMode(null);
  };

  const reconfirm = () => {
    if (busy) return;
    setOutcome("none");
    setConsentMode("reconfirm");
  };

  const discard = () => {
    if (busy) return;
    setDraft(freshDraft(saved, zone));
    setTouched({ idle: false, hours: false });
    setSaveAttempted(false);
    setOutcome("discarded");
    saveButton.current?.focus();
  };

  const reload = async () => {
    if (busy || reloading) return;
    setReloading(true);
    const queryKey = hostingQueryKeys.server(server.primaryKey);
    await queryClient.refetchQueries({ queryKey, exact: true });
    setReloading(false);
    const latest = queryClient.getQueryData<GameServer | null>(queryKey)?.automation;
    if (queryClient.getQueryState(queryKey)?.status === "error" || !latest) return;
    setDraft(freshDraft(latest.settings, zone));
    setBaseline(latest.settings);
    setTouched({ idle: false, hours: false });
    setSaveAttempted(false);
    setOutcome("none");
  };

  const dialogOpen = consentMode !== null;
  const dialogError = dialogOpen ? (saveErrorKeys[outcome] ?? null) : null;
  const startRuleCount =
    consentMode === "reconfirm"
      ? saved.schedules.filter((rule) => rule.action !== "Stop").length
      : draft.rules.filter((rule) => rule.action === "Start").length;
  const showPausedNotice = consentNeeded && saved.schedules.some((rule) => rule.action !== "Stop");
  const alertText = dialogOpen
    ? ""
    : outcome === "invalid" && invalidCount > 0
      ? t("Server_Automation_Status_Invalid", { count: invalidCount })
      : outcome === "saveError"
        ? t("Server_Automation_Status_SaveError")
        : outcome === "conflict"
          ? t("Server_Automation_Status_Conflict")
          : outcome === "forbidden"
            ? t("Server_Automation_Status_Forbidden")
            : "";
  const statusText =
    alertText !== ""
      ? ""
      : outcome === "saved"
        ? t("Server_Automation_Status_Saved")
        : outcome === "discarded"
          ? t("Server_Automation_Status_Discarded")
          : dirty
            ? t("Server_Automation_Status_Dirty")
            : t("Server_Automation_Status_Pristine");

  const noticeText = archived
    ? t("Server_Change_ReadOnly_Archived")
    : !editable
      ? t("Server_Automation_ReadOnly")
      : null;
  const describedBy = noticeText === null ? undefined : noticeId;
  const idleEnabled = editable && idleAvailable;
  const idleNumberDisabled = !idleEnabled || !draft.idleShutdownEnabled;
  const hoursNumberDisabled = !editable || !draft.maxRuntimeEnabled;
  // Without the player signal the structural reason (the checkbox hint) also describes the minutes field.
  const idleDescribedBy = idleAvailable
    ? describedBy
    : [`${idleEnableId}-hint`, describedBy].filter(Boolean).join(" ");

  return (
    <>
      <div className="automation-panel__lead">
        <p className="workspace-panel__muted">{t("Server_Automation_Description")}</p>
        <p className="workspace-panel__muted">
          {t("Server_Automation_Warning_Lead", { count: limits.warningLeadMinutes })}
        </p>
        {server.canManageCostAlerts === true && (
          <ActionLink
            variant="ghost"
            size="compact"
            to={`/command/servers/cost-alerts?owner=${server.costOwnerId}`}
          >
            {t("Server_Automation_Warning_Link")}
          </ActionLink>
        )}
      </div>
      {noticeText !== null && (
        <div id={noticeId} className="automation-panel__notice">
          <StatePanel kind="info">{noticeText}</StatePanel>
        </div>
      )}
      {showPausedNotice && (
        <ScheduledStartsPausedNotice canManage={editable} onReconfirm={reconfirm} />
      )}
      <AutomationStatusList automation={automation} locale={currentLocale()} />
      <div
        role="group"
        aria-labelledby={`${baseId}-idle-title`}
        className="automation-panel__subsection"
      >
        <h3 id={`${baseId}-idle-title`}>{t("Server_Automation_Idle_Title")}</h3>
        <p className="automation-panel__description">{t("Server_Automation_Idle_Description")}</p>
        <SwitchField
          id={idleEnableId}
          label={t("Server_Automation_Idle_Enable")}
          onLabel={t("React_ServerWorkspace_Enabled")}
          offLabel={t("React_ServerWorkspace_Disabled")}
          hint={
            idleAvailable
              ? t("Server_Automation_Idle_EnableHint")
              : t("Server_Automation_Idle_NotAvailable")
          }
          checked={draft.idleShutdownEnabled}
          disabled={!idleEnabled}
          aria-disabled={busy ? true : undefined}
          aria-describedby={describedBy}
          onChange={(event) => change({ idleShutdownEnabled: event.target.checked })}
        />
        <div className="automation-panel__field">
          <TextField
            ref={idleField}
            label={t("Server_Automation_Idle_Minutes")}
            hint={t("Server_Automation_Idle_MinutesHint", {
              min: limits.idleMinutesMin,
              max: limits.idleMinutesMax,
            })}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            maxLength={idleMaxLength}
            value={draft.idleMinutes}
            disabled={idleNumberDisabled}
            readOnly={busy}
            aria-disabled={busy ? true : undefined}
            aria-describedby={idleDescribedBy}
            error={numberMessage(validation.idleMinutes, touched.idle, "idle")}
            onChange={(event) => change({ idleMinutes: event.target.value })}
            onBlur={touch("idle")}
          />
        </div>
      </div>
      <div
        role="group"
        aria-labelledby={`${baseId}-max-title`}
        className="automation-panel__subsection"
      >
        <h3 id={`${baseId}-max-title`}>{t("Server_Automation_Max_Title")}</h3>
        <p className="automation-panel__description">{t("Server_Automation_Max_Description")}</p>
        <SwitchField
          label={t("Server_Automation_Max_Enable")}
          onLabel={t("React_ServerWorkspace_Enabled")}
          offLabel={t("React_ServerWorkspace_Disabled")}
          hint={t("Server_Automation_Max_EnableHint")}
          checked={draft.maxRuntimeEnabled}
          disabled={!editable}
          aria-disabled={busy ? true : undefined}
          aria-describedby={describedBy}
          onChange={(event) => change({ maxRuntimeEnabled: event.target.checked })}
        />
        <div className="automation-panel__field">
          <TextField
            ref={hoursField}
            label={t("Server_Automation_Max_Hours")}
            hint={t("Server_Automation_Max_HoursHint", {
              min: limits.maxRuntimeHoursMin,
              max: limits.maxRuntimeHoursMax,
            })}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            maxLength={hoursMaxLength}
            value={draft.maxHours}
            disabled={hoursNumberDisabled}
            readOnly={busy}
            aria-disabled={busy ? true : undefined}
            aria-describedby={describedBy}
            error={numberMessage(validation.maxHours, touched.hours, "hours")}
            onChange={(event) => change({ maxHours: event.target.value })}
            onBlur={touch("hours")}
          />
        </div>
      </div>
      <div
        role="group"
        aria-labelledby={`${baseId}-schedules-title`}
        className="automation-panel__subsection"
      >
        <h3 id={`${baseId}-schedules-title`}>{t("Server_Automation_Schedules_Title")}</h3>
        <p className="automation-panel__description">
          {t("Server_Automation_Schedules_Description")}
        </p>
        <SelectField
          label={t("Server_Automation_Schedules_Zone")}
          hint={t("Server_Automation_Schedules_ZoneHint")}
          value={draft.timeZone}
          options={zoneOptions}
          disabled={!editable}
          aria-disabled={busy ? true : undefined}
          aria-describedby={describedBy}
          onChange={(event) => change({ timeZone: event.target.value })}
        />
        {draft.rules.length === 0 && (
          <StatePanel kind="info" title={t("Server_Automation_Schedules_EmptyTitle")}>
            {t("Server_Automation_Schedules_EmptyBody")}
          </StatePanel>
        )}
        {draft.rules.length > 0 && (
          <ul ref={ruleList} className="automation-panel__rules">
            {draft.rules.map((rule, index) => (
              <li key={rule.key} data-rule-key={rule.key}>
                <ScheduleRuleFields
                  index={index}
                  rule={rule}
                  saved={
                    rule.id === null
                      ? null
                      : (saved.schedules.find((candidate) => candidate.id === rule.id) ?? null)
                  }
                  consentNeeded={consentNeeded}
                  errors={validation.rules[index] ?? { days: false, time: false, duplicate: false }}
                  showErrors={saveAttempted}
                  disabled={!editable}
                  saving={busy}
                  timeZone={scheduleZone}
                  locale={locale}
                  onChange={changeRule}
                  onRemove={() => removeRule(index)}
                />
              </li>
            ))}
          </ul>
        )}
        <div className="automation-panel__add">
          <ActionButton
            ref={addButton}
            variant="ghost"
            disabled={!editable}
            aria-disabled={atLimit || busy ? true : undefined}
            aria-describedby={atLimit ? counterId : undefined}
            onClick={addRule}
          >
            <Add16Regular aria-hidden="true" />
            {t("Server_Automation_Schedules_Add")}
          </ActionButton>
          <p id={counterId} className="automation-panel__counter">
            {t("Server_Automation_Schedules_Counter", {
              count: draft.rules.length,
              max: limits.maxSchedules,
            })}
          </p>
        </div>
      </div>
      {editable && (
        <div className="automation-panel__save">
          <div className="automation-panel__actions">
            <ActionButton
              ref={saveButton}
              variant="primary"
              aria-disabled={busy ? true : undefined}
              onClick={trySave}
            >
              {busy ? t("Server_Automation_Save_Pending") : t("Server_Automation_Save")}
            </ActionButton>
            <ActionButton
              variant="ghost"
              aria-disabled={busy || !dirty ? true : undefined}
              onClick={discard}
            >
              {t("Server_Change_Discard")}
            </ActionButton>
          </div>
          <div className="automation-panel__status">
            <p role="status">{statusText}</p>
            {alertText !== "" && <p role="alert">{alertText}</p>}
            {outcome === "conflict" && (
              <ActionButton
                variant="ghost"
                size="compact"
                aria-disabled={busy || reloading ? true : undefined}
                onClick={() => void reload()}
              >
                {t("Server_Automation_Reload")}
              </ActionButton>
            )}
          </div>
        </div>
      )}
      {dialogOpen && (
        <ScheduledStartConsentDialog
          open
          server={server}
          startRuleCount={startRuleCount}
          busy={busy}
          errorKey={dialogError}
          onConfirm={confirmConsent}
          onDismiss={dismissConsent}
        />
      )}
      <div className="sr-only" aria-live="polite">
        <span key={announcement.count}>{announcement.text}</span>
      </div>
    </>
  );
}
