import { Warning20Regular } from "@fluentui/react-icons";
import type { ComponentType } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { ActionButton } from "@/components/ActionControl";
import {
  SelectField,
  SwitchField,
  TextAreaField,
  TextField,
  type SelectOption,
} from "@/components/FormFields";
import { StatusBadge } from "@/components/primitives";
import { ApiText } from "@/components/server/ApiText";
import "@/components/server/configuration.css";
import type { ConfigurationContributionProps } from "@/features/hosting/implementations/ConfigurationContribution";
import type { GameConfigurationEntry } from "@/features/hosting/types";

/** Props of {@link ConfigurationEntryField}. */
export interface ConfigurationEntryFieldProps {
  /** The configuration entry the field edits. */
  entry: GameConfigurationEntry;
  /** The raw value the control shows: the staged value when one exists, otherwise the saved value. */
  value: string;
  /** Whether a value is staged for this entry. */
  staged: boolean;
  /** Whether a reset to the inherited value is staged for this entry. */
  resetStaged: boolean;
  /** The validation message to show, or undefined while the value is acceptable or not yet checked. */
  error?: string | undefined;
  /** Whether the control cannot be changed, for a reader or while an apply runs. */
  disabled: boolean;
  /**
   * Called with the raw text of a changed value. It is never called for a sensitive or redacted entry.
   *
   * @param value The new raw value.
   */
  onChange(value: string): void;
  /** Called when the control loses focus; the panel uses it to start showing the field error. */
  onBlur(): void;
  /** Removes the staged value or the staged reset of this entry. */
  onUndo(): void;
  /** Stages a reset to the inherited value. Omit it to hide the reset control, for example for a reader. */
  onReset?: (() => void) | undefined;
  /** The game contribution that renders the control of this entry, when the game provides one. */
  contribution?: ComponentType<ConfigurationContributionProps> | undefined;
}

/**
 * Builds the id of the control of an entry from its key.
 *
 * @param key The configuration entry key.
 * @returns A DOM-safe control id.
 */
function controlId(key: string): string {
  return `server-configuration-${key.replace(/[^a-z0-9]/gi, "-")}`;
}

/**
 * Builds the options of a selection entry from its typed choices, or from its allowed values when it has none.
 * Choice values are compared and submitted as strings, never used as object keys.
 *
 * @param entry The selection entry.
 * @param notSetLabel The label of the empty option, or undefined when the entry is required.
 * @returns The options in display order; the labels come from the API and are marked as English.
 */
function selectionOptions(
  entry: GameConfigurationEntry,
  notSetLabel: string | undefined,
): SelectOption[] {
  const choices = entry.choices?.length
    ? entry.choices.map((choice) => ({ value: String(choice.value), label: choice.label }))
    : entry.allowedValues.map((value) => ({ value, label: value }));
  const options: SelectOption[] = choices.map((choice) => ({ ...choice, lang: "en" }));
  return notSetLabel === undefined ? options : [{ value: "", label: notSetLabel }, ...options];
}

/**
 * One configuration entry of a game server: label, source line, the control for its kind, hint, warning and the
 * staging affordances.
 *
 * The control is chosen generically from the entry: the game contribution first, then boolean (a switch), selection, string
 * array, number and finally text. The control is built from the shared form fields, so label, hint and error wiring
 * follow the shared rules. Option names, descriptions and warnings come from the API and render as plain text inside
 * {@link ApiText}. A staged entry shows a Changed badge with Undo change; a staged reset says that the inherited
 * value will be used. A sensitive or redacted entry is shown read-only: its value is never loaded and it can never be
 * staged, because secrets are managed in the Credentials panel only.
 *
 * @param props The entry, its values and the staging callbacks.
 */
export function ConfigurationEntryField({
  entry,
  value,
  staged,
  resetStaged,
  error,
  disabled,
  onChange,
  onBlur,
  onUndo,
  onReset,
  contribution: Contribution,
}: ConfigurationEntryFieldProps) {
  const { t } = useTranslation();
  const id = controlId(entry.key);
  const kind = entry.kind.toLowerCase();
  const protectedEntry = entry.sensitive || entry.isRedacted;
  const hasDescription = entry.description.trim().length > 0;
  const source = formatResource(t("React_ServerWorkspace_FromSource"), entry.source);
  const hint = (
    <>
      <span className="configuration-field__source">{source}</span>
      {hasDescription && (
        <>
          {" "}
          <ApiText>{entry.description}</ApiText>
        </>
      )}
      {protectedEntry && ` ${t("Server_Settings_StoredValue")}`}
    </>
  );
  const label = <ApiText>{entry.displayName}</ApiText>;
  const changed = staged || resetStaged;
  // The slot exists whenever the member may modify the entry, so staging only fills it and never adds a row (D-11).
  const actionable = !protectedEntry && onReset !== undefined;
  const stateSlot = protectedEntry ? undefined : (
    <span
      className={
        actionable
          ? "configuration-field__state configuration-field__state--actionable"
          : "configuration-field__state"
      }
    >
      {changed && <StatusBadge tone="info">{t("Server_Change_Field_Changed")}</StatusBadge>}
      {resetStaged && (
        <span className="configuration-field__staged">{t("Server_Settings_ResetStaged")}</span>
      )}
      {changed && (
        <ActionButton
          variant="ghost"
          size="compact"
          disabled={disabled}
          aria-label={t("Server_Change_Field_Undo_Name", { name: entry.displayName })}
          onClick={onUndo}
        >
          {t("Server_Change_Field_Undo")}
        </ActionButton>
      )}
      {!resetStaged && onReset && entry.source === "Server" && (
        <ActionButton
          variant="ghost"
          size="compact"
          disabled={disabled}
          aria-label={t("Server_Settings_Reset_Name", { name: entry.displayName })}
          onClick={onReset}
        >
          {t("React_ServerWorkspace_ResetInherited")}
        </ActionButton>
      )}
    </span>
  );
  const common = {
    label,
    hint,
    error,
    id,
    disabled,
    "aria-required": entry.required ? (true as const) : undefined,
    onBlur,
    labelEnd: stateSlot,
  };
  const numeric = kind === "integer" || kind === "number" || kind === "decimal";

  let control;
  if (protectedEntry) {
    control = (
      <TextField
        label={label}
        hint={hint}
        id={id}
        type="password"
        autoComplete="new-password"
        disabled
        value=""
        placeholder={t("Server_Settings_StoredValue")}
        onChange={() => undefined}
      />
    );
  } else if (Contribution) {
    const hintId = `${id}-hint`;
    const errorId = `${id}-error`;
    control = (
      <div className="form-field">
        <div className="form-field__head">
          <label className="form-field__label" htmlFor={id}>
            {label}
          </label>
          {stateSlot}
        </div>
        <p className="form-field__hint" id={hintId}>
          {hint}
        </p>
        <Contribution
          id={id}
          value={value}
          disabled={disabled}
          required={entry.required}
          // A server setting is never left unset by the game contribution; only profile editing allows that.
          allowUnset={false}
          invalid={error !== undefined}
          describedBy={error !== undefined ? `${hintId} ${errorId}` : hintId}
          onChange={onChange}
        />
        {error !== undefined && (
          <p className="form-field__error" id={errorId} role="alert">
            <Warning20Regular className="form-field__error-icon" aria-hidden="true" />
            <span>{error}</span>
          </p>
        )}
      </div>
    );
  } else if (kind === "boolean") {
    // A boolean has no empty state, so the switch is never marked required; it stages only "true" or "false".
    control = (
      <SwitchField
        label={label}
        hint={hint}
        error={error}
        id={id}
        disabled={disabled}
        onBlur={onBlur}
        labelEnd={stateSlot}
        checked={value === "true"}
        onChange={(event) => onChange(event.target.checked ? "true" : "false")}
        onLabel={t("React_ServerWorkspace_Enabled")}
        offLabel={t("React_ServerWorkspace_Disabled")}
      />
    );
  } else if (kind === "selection") {
    control = (
      <SelectField
        {...common}
        value={value}
        options={selectionOptions(
          entry,
          entry.required ? undefined : t("React_ServerWorkspace_NotSet"),
        )}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  } else if (kind === "stringarray") {
    control = (
      <TextAreaField
        {...common}
        rows={3}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  } else {
    control = (
      <TextField
        {...common}
        type={numeric ? "number" : "text"}
        min={numeric ? (entry.minimum ?? undefined) : undefined}
        max={numeric ? (entry.maximum ?? undefined) : undefined}
        step={numeric ? (kind === "integer" ? 1 : "any") : undefined}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }

  return (
    <div className="configuration-field">
      {control}
      {entry.warning && (
        <p className="configuration-field__warning">
          <Warning20Regular className="configuration-field__warning-icon" aria-hidden="true" />
          <ApiText>{entry.warning}</ApiText>
        </p>
      )}
    </div>
  );
}
