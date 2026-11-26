import { Delete16Regular } from "@fluentui/react-icons";
import { useId, useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CheckboxField, FieldError, RadioGroupField, TextField } from "@/components/FormFields";
import "@/components/server/automation.css";
import {
  formatWallTime,
  lastRunKey,
  ruleDiffersFromSaved,
  ruleSummaryKey,
  weekdayLabels,
  type LastRunKey,
  type RuleDraft,
  type RuleErrors,
  type ScheduleAction,
} from "@/features/hosting/automationModel";
import type { ServerSchedule } from "@/features/hosting/types";

/** The sentence of every last-run outcome the client knows; an unknown outcome maps to the fixed `Other` sentence. */
const lastRunSentences: Readonly<Record<LastRunKey, TranslationKey>> = {
  None: "Server_Automation_Rule_LastRun_None",
  Started: "Server_Automation_Rule_LastRun_Started",
  Stopped: "Server_Automation_Rule_LastRun_Stopped",
  SkippedRunning: "Server_Automation_Rule_LastRun_SkippedRunning",
  SkippedOff: "Server_Automation_Rule_LastRun_SkippedOff",
  SkippedCost: "Server_Automation_Rule_LastRun_SkippedCost",
  SkippedConsent: "Server_Automation_Rule_LastRun_SkippedConsent",
  SkippedMissed: "Server_Automation_Rule_LastRun_SkippedMissed",
  SkippedBusy: "Server_Automation_Rule_LastRun_SkippedBusy",
  SkippedStopUnfinished: "Server_Automation_Rule_LastRun_SkippedStopUnfinished",
  WaitingForStop: "Server_Automation_Rule_LastRun_WaitingForStop",
  Failed: "Server_Automation_Rule_LastRun_Failed",
  Other: "Server_Automation_Rule_LastRun_Other",
};

/** A valid wall-clock time as the native time input writes it. */
const clockPattern = /^([01]\d|2[0-3]):([0-5]\d)$/;

/** The format of a run instant: short weekday, day, month and time. */
const runInstantFormat: Intl.DateTimeFormatOptions = {
  weekday: "short",
  day: "numeric",
  month: "short",
  hour: "numeric",
  minute: "2-digit",
};

/**
 * Formats a wall-clock time of a rule the way the locale writes a time of day.
 *
 * The text is the time the viewer typed, not an instant: it is formatted in UTC from the same hours and minutes, so
 * no time zone shifts it.
 *
 * @param time The time as `HH:mm`.
 * @param locale The locale of the text.
 * @returns The localized time, or an empty text when the time is not a valid `HH:mm`.
 */
function formatClock(time: string, locale: string): string {
  const match = clockPattern.exec(time);
  if (match === null) return "";
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  return new Intl.DateTimeFormat(locale, { timeStyle: "short", timeZone: "UTC" }).format(
    new Date(Date.UTC(2024, 0, 1, hours, minutes)),
  );
}

/** Props of {@link ScheduleRuleFields}. */
export interface ScheduleRuleFieldsProps {
  /** The zero-based position of the rule in the list; the heading shows it one-based. */
  index: number;
  /** The draft rule. */
  rule: RuleDraft;
  /** The saved rule with the same id, or null for a new rule. The run lines describe it, never the draft. */
  saved: ServerSchedule | null;
  /** True while the scheduled-start consent is needed; start rules then show as paused. */
  consentNeeded: boolean;
  /** The validation verdict of this rule. */
  errors: RuleErrors;
  /** True once a save was attempted, which is when the messages show. */
  showErrors: boolean;
  /** True when the viewer cannot change the rule: every control is natively disabled. */
  disabled: boolean;
  /** True while a save is in flight: text inputs are read-only and the change handlers of the panel ignore input. */
  saving: boolean;
  /** The IANA time zone the saved run instants are shown in. */
  timeZone: string;
  /** The locale of weekday names and times, for example `en-US`. */
  locale: string;
  /**
   * Reports the edited rule.
   *
   * @param rule The rule with the change applied.
   */
  onChange: (rule: RuleDraft) => void;
  /** Reports that the viewer removed the rule. */
  onRemove: () => void;
}

/**
 * One weekly start or stop rule: the action, the time, the weekdays, a summary sentence and the run lines.
 *
 * The block is a labelled group named by its heading row. The summary describes the draft and updates as the viewer
 * edits; the next-run and last-run lines describe the saved rule and never the draft, so an edited rule reads "Not
 * saved yet." for its next run. Next and last runs are instants the control plane sent; nothing is calculated here.
 * An outcome code the client does not know shows the fixed "Last run: {time}." sentence and never API text.
 *
 * @param props The rule, the saved rule behind it, its validation and the callbacks.
 * @returns The rule block.
 */
export function ScheduleRuleFields({
  index,
  rule,
  saved,
  consentNeeded,
  errors,
  showErrors,
  disabled,
  saving,
  timeZone,
  locale,
  onChange,
  onRemove,
}: ScheduleRuleFieldsProps) {
  const { t } = useTranslation();
  const baseId = useId();
  const titleId = `${baseId}-title`;
  const daysErrorId = `${baseId}-days-error`;
  const number = index + 1;
  const shortDays = useMemo(() => weekdayLabels(locale, "short"), [locale]);
  const longDays = useMemo(() => weekdayLabels(locale, "long"), [locale]);

  const toggleDay = (day: number, checked: boolean) => {
    const days = checked
      ? rule.days.includes(day)
        ? rule.days
        : [...rule.days, day]
      : rule.days.filter((candidate) => candidate !== day);
    onChange({ ...rule, days });
  };

  const time = formatClock(rule.time, locale);
  const summaryKind = ruleSummaryKey(rule.days, rule.time);
  const summary = (() => {
    if (summaryKind === "Incomplete" || time === "") {
      return t("Server_Automation_Rule_Summary_Incomplete");
    }
    switch (summaryKind) {
      case "Daily":
        return t("Server_Automation_Rule_Summary_Daily", { time });
      case "Weekdays":
        return t("Server_Automation_Rule_Summary_Weekdays", { time });
      case "Days": {
        const chosen = new Set(rule.days);
        const names = longDays.filter(({ day }) => chosen.has(day)).map(({ label }) => label);
        const days = new Intl.ListFormat(locale, { type: "conjunction", style: "long" }).format(
          names,
        );
        return t("Server_Automation_Rule_Summary_Days", { days, time });
      }
    }
  })();

  const nextRun = (() => {
    if (ruleDiffersFromSaved(rule, saved)) return t("Server_Automation_Rule_NotSaved");
    if (consentNeeded && rule.action === "Start") return t("Server_Automation_Rule_Paused");
    const when = saved?.nextRunAt
      ? formatWallTime(saved.nextRunAt, timeZone, locale, runInstantFormat)
      : "";
    return t("Server_Automation_Rule_NextRun", { when: when === "" ? "-" : when });
  })();

  const lastRun = saved?.lastRun ?? null;
  const lastKey = lastRunKey(lastRun);
  const lastRunText = t(lastRunSentences[lastKey], {
    time: lastRun === null ? "" : formatWallTime(lastRun.at, timeZone, locale, runInstantFormat),
  });

  const timeError = !showErrors
    ? undefined
    : errors.time
      ? t("Server_Automation_Invalid_Time")
      : errors.duplicate
        ? t("Server_Automation_Invalid_Duplicate")
        : undefined;
  const daysError = showErrors && errors.days;

  return (
    <div role="group" aria-labelledby={titleId} className="schedule-rule">
      <div className="schedule-rule__heading">
        <p id={titleId} className="schedule-rule__title">
          {t("Server_Automation_Rule_Title", { index: number })}
        </p>
        <ActionButton
          variant="ghost"
          size="compact"
          aria-label={t("Server_Automation_Rule_RemoveLabel", { index: number })}
          disabled={disabled}
          aria-disabled={saving ? true : undefined}
          onClick={onRemove}
        >
          <Delete16Regular aria-hidden="true" />
          {t("Server_Automation_Rule_Remove")}
        </ActionButton>
      </div>
      <div className="schedule-rule__row">
        <RadioGroupField
          legend={t("Server_Automation_Rule_Action")}
          name={`${baseId}-action`}
          value={rule.action}
          disabled={disabled}
          options={[
            {
              value: "Start",
              label: t("Server_Action_Start_Label"),
              help: t("Server_Automation_Rule_StartHelp"),
            },
            {
              value: "Stop",
              label: t("Server_Action_Stop_Label"),
              help: t("Server_Automation_Rule_StopHelp"),
            },
          ]}
          onChange={(value) => onChange({ ...rule, action: value as ScheduleAction })}
        />
        <div className="schedule-rule__time">
          <TextField
            label={t("Server_Automation_Rule_Time")}
            type="time"
            step={60}
            data-field="time"
            value={rule.time}
            disabled={disabled}
            readOnly={saving}
            aria-disabled={saving ? true : undefined}
            error={timeError}
            onChange={(event) => onChange({ ...rule, time: event.target.value })}
          />
        </div>
      </div>
      <fieldset
        className="schedule-rule__days"
        aria-describedby={daysError ? daysErrorId : undefined}
      >
        <legend className="form-field__legend">{t("Server_Automation_Rule_Days")}</legend>
        <div className="schedule-rule__day-grid">
          {shortDays.map(({ day, label }) => (
            <CheckboxField
              key={day}
              variant="chip"
              label={label}
              data-field="days"
              checked={rule.days.includes(day)}
              disabled={disabled}
              aria-disabled={saving ? true : undefined}
              onChange={(event) => toggleDay(day, event.target.checked)}
            />
          ))}
        </div>
        {daysError && <FieldError id={daysErrorId} message={t("Server_Automation_Invalid_Days")} />}
      </fieldset>
      <p className="schedule-rule__summary">{summary}</p>
      <div className="schedule-rule__runs">
        <p className="schedule-rule__run">{nextRun}</p>
        <div className="schedule-rule__last">
          <p className="schedule-rule__run">{lastRunText}</p>
          {lastKey === "Failed" && (
            <ActionLink variant="ghost" size="compact" to={{ search: "?tab=activity" }}>
              {t("Server_Failure_OpenDetails")}
            </ActionLink>
          )}
        </div>
      </div>
    </div>
  );
}
