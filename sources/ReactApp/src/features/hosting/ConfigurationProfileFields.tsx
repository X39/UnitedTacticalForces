import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import type { ConfigurationValidationIssue } from "@/features/hosting/model";
import type { ConfigurationDefinition } from "@/features/hosting/types";

type Translate = (key: TranslationKey) => string;

const validationKeys: Record<ConfigurationValidationIssue["code"], TranslationKey> = {
  required: "Hosting_ConfigurationProfiles_ValidationRequired",
  integer: "Hosting_ConfigurationProfiles_ValidationInteger",
  number: "Hosting_ConfigurationProfiles_ValidationNumber",
  minimum: "Hosting_ConfigurationProfiles_ValidationMinimum",
  maximum: "Hosting_ConfigurationProfiles_ValidationMaximum",
  selection: "Hosting_ConfigurationProfiles_ValidationSelection",
};

export function configurationValidationMessage(
  issue: ConfigurationValidationIssue,
  translate: Translate,
): string {
  return formatResource(
    translate(validationKeys[issue.code]),
    issue.displayName,
    issue.limit ?? "",
  );
}

export function gameLabel(identifier: string): string {
  switch (identifier.toLowerCase()) {
    case "arma3":
      return "Arma 3";
    case "aska":
      return "ASKA";
    case "cs2":
      return "Counter-Strike 2";
    default:
      return identifier;
  }
}

function ConfigurationBooleanField({
  id,
  label,
  value,
  required,
  invalid,
  describedBy,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  required: boolean;
  invalid: boolean;
  describedBy: string;
  onChange(value: string): void;
}) {
  const { t } = useTranslation();
  const input = useRef<HTMLInputElement>(null);
  const isUnset = value === "";

  useEffect(() => {
    if (input.current) input.current.indeterminate = isUnset;
  }, [isUnset]);

  return (
    <label className="next-hosting-configuration-toggle" htmlFor={id}>
      <input
        ref={input}
        id={id}
        type="checkbox"
        required={required}
        checked={value === "true"}
        aria-label={label}
        aria-checked={isUnset ? "mixed" : value === "true"}
        aria-invalid={invalid}
        aria-describedby={describedBy}
        onChange={(event) => onChange(isUnset ? "false" : event.target.checked ? "true" : "false")}
      />
      <span>
        {isUnset
          ? t("Hosting_ConfigurationProfiles_NotSet")
          : value === "true"
            ? t("Hosting_ConfigurationProfiles_Enabled")
            : t("Hosting_ConfigurationProfiles_Disabled")}
      </span>
    </label>
  );
}

export function ConfigurationProfileFields({
  definitions,
  values,
  issues,
  configuredSensitiveKeys = [],
  clearedKeys = [],
  onChange,
  onClear,
}: {
  definitions: ConfigurationDefinition[];
  values: Record<string, string>;
  issues: ConfigurationValidationIssue[];
  configuredSensitiveKeys?: string[];
  clearedKeys?: string[];
  onChange(key: string, value: string): void;
  onClear?(key: string): void;
}) {
  const { t } = useTranslation();
  const targets = Object.entries(
    definitions.reduce<Record<string, ConfigurationDefinition[]>>((result, definition) => {
      (result[definition.targetPath || t("Hosting_ConfigurationProfiles_GeneratedFile")] ??=
        []).push(definition);
      return result;
    }, {}),
  );
  const issueByKey = new Map(issues.map((issue) => [issue.key, issue]));

  return (
    <div className="next-hosting-configuration-groups">
      {targets.map(([targetPath, targetDefinitions]) => (
        <section className="next-hosting-configuration-target" key={targetPath}>
          <header>
            <span>{t("Hosting_ConfigurationProfiles_GeneratedFile")}</span>
            <code>{targetPath}</code>
          </header>
          {Object.entries(
            targetDefinitions.reduce<Record<string, ConfigurationDefinition[]>>(
              (result, definition) => {
                (result[definition.group || t("Hosting_ConfigurationProfiles_General")] ??=
                  []).push(definition);
                return result;
              },
              {},
            ),
          ).map(([group, items]) => (
            <fieldset key={group}>
              <legend>{group}</legend>
              {items
                .toSorted((left, right) => left.order - right.order)
                .map((definition) => {
                  const id = `configuration-${definition.key.replace(/[^a-z0-9_-]/gi, "-")}`;
                  const descriptionId = `${id}-description`;
                  const errorId = `${id}-error`;
                  const value = values[definition.key] ?? "";
                  const issue = issueByKey.get(definition.key);
                  const kind = definition.kind.toLowerCase();
                  const choices = definition.choices ?? [];
                  const sensitiveConfigured =
                    definition.sensitive &&
                    configuredSensitiveKeys.includes(definition.key) &&
                    !clearedKeys.includes(definition.key);
                  const describedBy = issue ? `${descriptionId} ${errorId}` : descriptionId;
                  const common = {
                    id,
                    required: definition.required,
                    "aria-invalid": Boolean(issue),
                    "aria-describedby": describedBy,
                  } as const;

                  return (
                    <div className="field next-hosting-configuration-field" key={definition.key}>
                      <div className="next-hosting-configuration-field-heading">
                        <label htmlFor={id}>{definition.displayName}</label>
                        <span>
                          {definition.required && (
                            <strong>{t("Hosting_ConfigurationProfiles_Required")}</strong>
                          )}
                          {definition.sensitive && (
                            <strong>{t("Hosting_ConfigurationProfiles_Sensitive")}</strong>
                          )}
                        </span>
                      </div>
                      {kind === "boolean" ? (
                        <ConfigurationBooleanField
                          id={id}
                          label={definition.displayName}
                          value={value}
                          required={definition.required}
                          invalid={Boolean(issue)}
                          describedBy={describedBy}
                          onChange={(nextValue) => onChange(definition.key, nextValue)}
                        />
                      ) : choices.length > 0 || (definition.allowedValues?.length ?? 0) > 0 ? (
                        <select
                          {...common}
                          value={value}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        >
                          {!definition.required && (
                            <option value="">{t("Hosting_ConfigurationProfiles_NotSet")}</option>
                          )}
                          {choices.map((option) => {
                            const optionValue =
                              typeof option.value === "string"
                                ? option.value
                                : JSON.stringify(option.value);
                            return (
                              <option
                                key={optionValue}
                                value={optionValue}
                                title={option.description ?? undefined}
                              >
                                {option.label}
                              </option>
                            );
                          })}
                          {choices.length === 0 &&
                            (definition.allowedValues ?? []).map((option) => (
                              <option key={option} value={option}>
                                {option}
                              </option>
                            ))}
                        </select>
                      ) : kind === "stringarray" ? (
                        <textarea
                          {...common}
                          rows={3}
                          value={value}
                          placeholder={t("Hosting_ConfigurationProfiles_EnterOnePerLine")}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        />
                      ) : (
                        <input
                          {...common}
                          type={
                            definition.sensitive
                              ? "password"
                              : kind === "integer" || kind === "number" || kind === "decimal"
                                ? "number"
                                : "text"
                          }
                          step={
                            kind === "integer"
                              ? 1
                              : kind === "number" || kind === "decimal"
                                ? "any"
                                : undefined
                          }
                          min={definition.minimum ?? undefined}
                          max={definition.maximum ?? undefined}
                          value={value}
                          autoComplete={definition.sensitive ? "new-password" : "off"}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        />
                      )}
                      <div className="next-hosting-configuration-field-meta">
                        <small id={descriptionId}>
                          {definition.description}
                          {sensitiveConfigured &&
                            ` ${t("React_ConfigurationProfiles_Configured")}.`}
                        </small>
                        {!definition.required && (value !== "" || sensitiveConfigured) && (
                          <button
                            className="button button--ghost button--small"
                            type="button"
                            onClick={() =>
                              onClear ? onClear(definition.key) : onChange(definition.key, "")
                            }
                          >
                            {t("Hosting_ConfigurationProfiles_ClearValue")}
                          </button>
                        )}
                      </div>
                      {issue && (
                        <small className="next-hosting-field-error" id={errorId} role="alert">
                          {configurationValidationMessage(issue, t)}
                        </small>
                      )}
                    </div>
                  );
                })}
            </fieldset>
          ))}
        </section>
      ))}
    </div>
  );
}
