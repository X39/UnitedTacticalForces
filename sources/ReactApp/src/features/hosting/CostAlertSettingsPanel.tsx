import { Add16Regular, Info16Regular } from "@fluentui/react-icons";
import { useMutation, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { SwitchField, TextField } from "@/components/FormFields";
import { ConfirmDialog, StatePanel, StatusBadge } from "@/components/primitives";
import "@/components/server/sections.css";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import {
  channelHintKey,
  channelStatus,
  formatAlertAmount,
  isAlertDraftDirty,
  normalizeAlertAmounts,
  validateAlertDraft,
  type AlertChannelRow,
  type AlertChannelAvailability,
  type AlertRowError,
} from "@/features/hosting/alertSettingsModel";
import {
  CostAlertSettingsConflictError,
  hostingQueryKeys,
  hostingService,
} from "@/features/hosting/services";
import type { CostAlertSettings, CostAlertSettingsUpdate } from "@/features/hosting/types";

/** The longest text an amount field accepts, enough for the largest amount with two decimals. */
const amountMaxLength = 12;

/** Source of row ids. Module scoped so ids stay unique across renders without reading a ref during render. */
let rowSequence = 0;

/**
 * Hands out the next stable row id.
 *
 * @returns An id no other row of this page has had.
 */
function nextRowId(): number {
  rowSequence += 1;
  return rowSequence;
}

/** Props of {@link CostAlertSettingsPanel}. */
export interface CostAlertSettingsPanelProps {
  /** The loaded settings of the cost owner; the panel edits a draft copy of them. */
  settings: CostAlertSettings;
  /** The query key the settings were loaded under; a successful save writes the saved settings into it. */
  queryKey: QueryKey;
  /** Level of the two panel headings; use 3 when the host page already has an h2 section around the panel. */
  headingLevel?: 2 | 3;
}

/** One amount row of the draft. The id is stable for the life of the row, so focus and keys survive removals. */
interface DraftRow {
  /** Stable identity of the row; never reused. */
  id: number;
  /** The text as typed. */
  text: string;
  /** Whether the field was left once, which is when its validation message may first show. */
  touched: boolean;
}

/** Where focus goes after a render: a row, the Add button, or nowhere. */
type FocusTarget = number | "add" | null;

/**
 * What the status line reports besides the pristine and dirty states.
 *
 * `invalid`, `saveError` and `conflict` are alerts; the others are polite status messages.
 */
type Outcome = "none" | "saved" | "discarded" | "invalid" | "saveError" | "conflict";

/**
 * The decimal separator the viewer's language writes.
 *
 * @param language The active i18n language.
 * @returns A comma for German, a point otherwise.
 */
function decimalSeparator(language: string | undefined): "." | "," {
  return language?.startsWith("de") ? "," : ".";
}

/**
 * The editor of one cost owner's alert amounts and channels with its save row.
 *
 * The amounts and the channels are two raised cards (the shared `workspace-panel` surface), each opening with its
 * heading and a muted description; the save row follows below the cards.
 *
 * The draft lives in React state only. Saving sends the revision the settings were loaded with, the amounts, both
 * channel flags and the language the viewer uses now, and replaces the cached settings with the saved ones. A
 * stale save keeps the draft and offers Reload settings; leaving with unsaved changes asks first. A viewer who may
 * see but not manage the settings gets disabled controls, one notice and no Save or Discard. While the form is
 * pristine the draft follows settings that change elsewhere; a dirty draft is kept. The channels panel lists the
 * always-on in-app row and the Discord and email rows, each with a status badge and one sentence on whether it can
 * deliver now for the owner alone or for the group of managers; an undeliverable channel stays selectable. A
 * language line closes the channels card: it names the language messages are written in for a personal owner, or
 * says each person receives their own.
 *
 * @param props The loaded settings, their query key and the heading level.
 * @returns The amounts panel, the channels panel, the save row and the leave dialog.
 */
export function CostAlertSettingsPanel({
  settings,
  queryKey,
  headingLevel = 2,
}: CostAlertSettingsPanelProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const { showToast } = useAppToast();
  const Heading = headingLevel === 2 ? "h2" : "h3";
  const separator = decimalSeparator(i18n.resolvedLanguage);
  const toRows = (values: readonly number[]): DraftRow[] =>
    values.map((value) => ({
      id: nextRowId(),
      text: formatAlertAmount(value, separator),
      touched: false,
    }));
  const [rows, setRows] = useState<DraftRow[]>(() => toRows(settings.thresholds));
  const [discordEnabled, setDiscordEnabled] = useState(settings.discordEnabled);
  const [emailEnabled, setEmailEnabled] = useState(settings.emailEnabled);
  const [baseline, setBaseline] = useState(settings);
  const [saveAttempted, setSaveAttempted] = useState(false);
  const [outcome, setOutcome] = useState<Outcome>("none");
  const [reloading, setReloading] = useState(false);
  const [announcement, setAnnouncement] = useState("");
  const inputs = useRef(new Map<number, HTMLInputElement>());
  const addButton = useRef<HTMLButtonElement>(null);
  const saveButton = useRef<HTMLButtonElement>(null);
  const focusTarget = useRef<FocusTarget>(null);
  const baseId = useId();
  const counterId = `${baseId}-counter`;
  const canManage = settings.canManage;

  const draft = { rows: rows.map((row) => row.text), discordEnabled, emailEnabled };
  const dirty = canManage && isAlertDraftDirty(draft, settings);
  const hasBlankRows = rows.length !== settings.thresholds.length;

  // The draft follows settings that change elsewhere only while it is pristine; the baseline is the saved state the
  // draft was last synchronized with. State adjusted during render, not an effect, so no stale frame is painted.
  if (baseline !== settings) {
    const pristine =
      !isAlertDraftDirty(draft, baseline) && rows.length === baseline.thresholds.length;
    setBaseline(settings);
    if (pristine) {
      setRows(toRows(settings.thresholds));
      setDiscordEnabled(settings.discordEnabled);
      setEmailEnabled(settings.emailEnabled);
    }
  }

  useEffect(() => {
    const target = focusTarget.current;
    if (target === null) return;
    focusTarget.current = null;
    (target === "add" ? addButton.current : inputs.current.get(target))?.focus();
  }, [rows]);

  const validation = validateAlertDraft(draft.rows, settings);
  const invalidCount = validation.errors.filter((error) => error !== null).length;
  const atLimit = rows.length >= settings.maxThresholds;
  const guard = useLeaveGuard({ dirty });

  const save = useMutation({
    mutationFn: (update: CostAlertSettingsUpdate) =>
      hostingService.updateCostAlertSettings(settings.costOwnerId, update),
    onSuccess: (result) => {
      queryClient.setQueryData(queryKey, result);
      void queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet });
      setRows(toRows(result.thresholds));
      setDiscordEnabled(result.discordEnabled);
      setEmailEnabled(result.emailEnabled);
      setSaveAttempted(false);
      setOutcome("saved");
      showToast(t("Server_Alerts_Toast_Saved"), "success");
    },
    onError: (error) => {
      setOutcome(error instanceof CostAlertSettingsConflictError ? "conflict" : "saveError");
    },
  });
  const busy = save.isPending;

  const invalidMessage = (error: AlertRowError): string => {
    switch (error) {
      case "Required":
        return t("Server_Alerts_Invalid_Required");
      case "Number":
        return t("Server_Alerts_Invalid_Number");
      case "Precision":
        return t("Server_Alerts_Invalid_Precision");
      case "Min":
        return t("Server_Alerts_Invalid_Min");
      case "Max":
        return t("Server_Alerts_Invalid_Max", { max: settings.maxThresholdAmount });
      case "Duplicate":
        return t("Server_Alerts_Invalid_Duplicate");
    }
  };

  /** Clears the messages an edit makes outdated; an invalid count and a conflict stay until resolved. */
  const edited = () => {
    setOutcome((current) =>
      current === "saved" || current === "discarded" || current === "saveError" ? "none" : current,
    );
  };

  const changeRow = (id: number, change: Partial<DraftRow>) => {
    if (busy) return;
    edited();
    setRows((current) => current.map((row) => (row.id === id ? { ...row, ...change } : row)));
  };

  const addRow = () => {
    if (busy || atLimit) return;
    const id = nextRowId();
    focusTarget.current = id;
    edited();
    setRows([...rows, { id, text: "", touched: false }]);
    setAnnouncement(t("Server_Alerts_Amounts_AddedAnnounce"));
  };

  const removeRow = (index: number) => {
    if (busy) return;
    const neighbour = rows[index + 1] ?? rows[index - 1];
    focusTarget.current = neighbour ? neighbour.id : "add";
    edited();
    setRows(rows.filter((_, at) => at !== index));
    setAnnouncement(t("Server_Alerts_Amounts_RemovedAnnounce"));
  };

  const trySave = () => {
    if (busy) return;
    setSaveAttempted(true);
    if (!validation.valid) {
      setOutcome("invalid");
      const first = validation.errors.findIndex((error) => error !== null);
      const row = rows[first];
      if (row) inputs.current.get(row.id)?.focus();
      return;
    }
    setOutcome("none");
    save.mutate({
      revision: settings.revision,
      thresholds: normalizeAlertAmounts(draft.rows),
      discordEnabled,
      emailEnabled,
      language: i18n.resolvedLanguage?.startsWith("de") ? "de" : "en",
    });
  };

  /** Puts the draft back to the given saved settings. */
  const resetDraft = (to: CostAlertSettings) => {
    setRows(toRows(to.thresholds));
    setDiscordEnabled(to.discordEnabled);
    setEmailEnabled(to.emailEnabled);
    setSaveAttempted(false);
  };

  const discard = () => {
    if (busy) return;
    resetDraft(settings);
    setOutcome("discarded");
    saveButton.current?.focus();
  };

  const reload = async () => {
    if (busy || reloading) return;
    setReloading(true);
    await queryClient.refetchQueries({ queryKey, exact: true });
    setReloading(false);
    const latest = queryClient.getQueryData<CostAlertSettings | null>(queryKey);
    if (queryClient.getQueryState(queryKey)?.status === "error" || !latest) return;
    resetDraft(latest);
    setBaseline(latest);
    setOutcome("none");
  };

  /** Enter inside a field submits the form natively; it must never save. */
  const ignoreSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
  };

  const alertText =
    outcome === "invalid" && invalidCount > 0
      ? t("Server_Alerts_Status_Invalid", { count: invalidCount })
      : outcome === "saveError"
        ? t("Server_Alerts_Status_SaveError")
        : outcome === "conflict"
          ? t("Server_Alerts_Status_Conflict")
          : "";
  const statusText =
    alertText !== ""
      ? ""
      : outcome === "saved"
        ? t("Server_Alerts_Status_Saved")
        : outcome === "discarded"
          ? t("Server_Alerts_Status_Discarded")
          : dirty
            ? t("Server_Alerts_Status_Dirty")
            : t("Server_Alerts_Status_Pristine");

  const languageLine =
    settings.audience === "Group"
      ? t("Server_Alerts_Language_Group")
      : t("Server_Alerts_Language_Self", {
          language: new Intl.DisplayNames(currentLocale(), { type: "language" }).of(
            settings.messageLanguage ?? (i18n.resolvedLanguage?.startsWith("de") ? "de" : "en"),
          ),
        });

  /**
   * The hint of one channel row: the status badge first, then the sentence. The badge sits inside the hint, so it is
   * announced as part of the checkbox description and aligns with the text.
   */
  const channelHint = (
    channel: AlertChannelRow,
    sentence: ReactNode,
    availability?: AlertChannelAvailability,
  ) => {
    const status = channelStatus(channel, availability);
    return (
      <>
        <StatusBadge tone={status.tone} className="cost-alert-settings__badge">
          {t(status.key)}
        </StatusBadge>{" "}
        {sentence}
      </>
    );
  };

  return (
    <>
      {!canManage && <StatePanel kind="info">{t("Server_Change_ReadOnly")}</StatePanel>}
      <form className="cost-alert-settings" onSubmit={ignoreSubmit} noValidate>
        <section
          className="workspace-panel cost-alert-settings__panel"
          aria-labelledby={`${baseId}-amounts`}
        >
          <Heading id={`${baseId}-amounts`}>{t("Server_Alerts_Amounts_Title")}</Heading>
          <p className="workspace-panel__muted">{t("Server_Alerts_Amounts_Description")}</p>
          {rows.length === 0 && (
            <StatePanel kind="info" title={t("Server_Alerts_Amounts_EmptyTitle")}>
              {t("Server_Alerts_Amounts_EmptyBody")}
            </StatePanel>
          )}
          {rows.map((row, index) => {
            const error = validation.errors[index] ?? null;
            const visible = error !== null && (row.touched || saveAttempted);
            return (
              <div key={row.id} className="cost-alert-settings__row">
                <TextField
                  ref={(element) => {
                    if (element) inputs.current.set(row.id, element);
                    else inputs.current.delete(row.id);
                  }}
                  label={t("Server_Alerts_Amounts_Label", {
                    index: index + 1,
                    currency: settings.currency,
                  })}
                  type="text"
                  inputMode="decimal"
                  autoComplete="off"
                  spellCheck={false}
                  maxLength={amountMaxLength}
                  value={row.text}
                  disabled={!canManage}
                  readOnly={busy}
                  aria-disabled={busy ? true : undefined}
                  error={visible ? invalidMessage(error) : undefined}
                  onChange={(event) => changeRow(row.id, { text: event.target.value })}
                  onBlur={() => changeRow(row.id, { touched: true })}
                  trailing={
                    <ActionButton
                      variant="ghost"
                      size="compact"
                      disabled={!canManage}
                      aria-disabled={busy ? true : undefined}
                      aria-label={t("Server_Alerts_Amounts_RemoveLabel", { index: index + 1 })}
                      onClick={() => removeRow(index)}
                    >
                      {t("Server_Alerts_Amounts_Remove")}
                    </ActionButton>
                  }
                />
              </div>
            );
          })}
          <div className="cost-alert-settings__add">
            <ActionButton
              ref={addButton}
              variant="ghost"
              disabled={!canManage}
              aria-disabled={atLimit || busy ? true : undefined}
              aria-describedby={atLimit ? counterId : undefined}
              onClick={addRow}
            >
              <Add16Regular aria-hidden="true" />
              {t("Server_Alerts_Amounts_Add")}
            </ActionButton>
            <p id={counterId} className="cost-alert-settings__counter">
              {t("Server_Alerts_Amounts_Counter", {
                count: rows.length,
                max: settings.maxThresholds,
              })}
            </p>
          </div>
          <div className="sr-only" aria-live="polite" aria-atomic="true">
            {announcement}
          </div>
        </section>
        <section
          className="workspace-panel cost-alert-settings__panel"
          aria-labelledby={`${baseId}-channels`}
        >
          <Heading id={`${baseId}-channels`}>{t("Server_Alerts_Channels_Title")}</Heading>
          <p className="workspace-panel__muted">{t("Server_Alerts_Channels_Description")}</p>
          <ul className="cost-alert-settings__channels">
            <li>
              <SwitchField
                label={t("Server_Alerts_Channel_InApp")}
                onLabel={t("React_ServerWorkspace_Enabled")}
                offLabel={t("React_ServerWorkspace_Disabled")}
                hint={channelHint("inApp", t("Server_Alerts_Channel_InAppHint"))}
                checked
                disabled
                readOnly
              />
            </li>
            <li>
              <SwitchField
                label={t("Server_Alerts_Channel_Discord")}
                onLabel={t("React_ServerWorkspace_Enabled")}
                offLabel={t("React_ServerWorkspace_Disabled")}
                hint={channelHint(
                  "discord",
                  <>
                    {t(channelHintKey("discord", settings.discord, settings.audience))}
                    {settings.discord === "NotLinked" && settings.audience === "Self" && (
                      <span className="cost-alert-settings__hint-link">
                        <ActionLink variant="ghost" size="compact" to="/settings">
                          {t("Server_Alerts_Discord_OpenSettings")}
                        </ActionLink>
                      </span>
                    )}
                  </>,
                  settings.discord,
                )}
                checked={discordEnabled}
                disabled={!canManage}
                aria-disabled={busy ? true : undefined}
                onChange={(event) => {
                  if (busy) return;
                  edited();
                  setDiscordEnabled(event.target.checked);
                }}
              />
            </li>
            <li>
              <SwitchField
                label={t("Server_Alerts_Channel_Email")}
                onLabel={t("React_ServerWorkspace_Enabled")}
                offLabel={t("React_ServerWorkspace_Disabled")}
                hint={channelHint(
                  "email",
                  t(channelHintKey("email", settings.email, settings.audience)),
                  settings.email,
                )}
                checked={emailEnabled}
                disabled={!canManage}
                aria-disabled={busy ? true : undefined}
                onChange={(event) => {
                  if (busy) return;
                  edited();
                  setEmailEnabled(event.target.checked);
                }}
              />
            </li>
          </ul>
          <p className="cost-alert-settings__language">
            <Info16Regular aria-hidden="true" />
            <span>{languageLine}</span>
          </p>
        </section>
        {canManage && (
          <div className="cost-alert-settings__save">
            <div className="cost-alert-settings__actions">
              <ActionButton
                ref={saveButton}
                variant="gold"
                aria-disabled={busy ? true : undefined}
                onClick={trySave}
              >
                {busy ? t("Server_Alerts_Save_Pending") : t("Server_Alerts_Save")}
              </ActionButton>
              <ActionButton
                variant="ghost"
                aria-disabled={busy || !(dirty || hasBlankRows) ? true : undefined}
                onClick={discard}
              >
                {t("Server_Change_Discard")}
              </ActionButton>
            </div>
            <div className="cost-alert-settings__status">
              <p role="status">{statusText}</p>
              {alertText !== "" && <p role="alert">{alertText}</p>}
              {outcome === "conflict" && (
                <ActionButton
                  variant="ghost"
                  size="compact"
                  aria-disabled={busy || reloading ? true : undefined}
                  onClick={() => void reload()}
                >
                  {t("Server_Alerts_Reload")}
                </ActionButton>
              )}
            </div>
          </div>
        )}
      </form>
      <ConfirmDialog
        open={guard.blocked}
        title={t("Server_Alerts_Leave_Title")}
        description={t("Server_Alerts_Leave_Body")}
        cancelLabel={t("Server_Change_Leave_Stay")}
        confirmLabel={t("Server_Change_Leave_Discard")}
        initialFocus="dismiss"
        onCancel={guard.stay}
        onConfirm={guard.proceed}
      />
    </>
  );
}
