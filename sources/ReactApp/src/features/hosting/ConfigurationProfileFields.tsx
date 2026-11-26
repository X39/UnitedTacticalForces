import {
  configurationContribution,
  configurationContributionField,
} from "./implementations/registry";
import { useEffect, useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton } from "@/components/ActionControl";
import { CheckboxField, SelectField, TextAreaField, TextField } from "@/components/FormFields";
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

function configurationValidationMessage(
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
  description,
  value,
  required,
  error,
  onChange,
}: {
  id: string;
  label: string;
  description: ReactNode;
  value: string;
  required: boolean;
  error: string | undefined;
  onChange(value: string): void;
}) {
  const { t } = useTranslation();
  const input = useRef<HTMLInputElement>(null);
  const isUnset = value === "";

  useEffect(() => {
    if (input.current) input.current.indeterminate = isUnset;
  }, [isUnset]);

  return (
    <CheckboxField
      ref={input}
      id={id}
      label={
        isUnset
          ? t("Hosting_ConfigurationProfiles_NotSet")
          : value === "true"
            ? t("Hosting_ConfigurationProfiles_Enabled")
            : t("Hosting_ConfigurationProfiles_Disabled")
      }
      hint={description}
      error={error}
      required={required}
      checked={value === "true"}
      aria-label={label}
      aria-checked={isUnset ? "mixed" : value === "true"}
      onChange={(event) => onChange(isUnset ? "false" : event.target.checked ? "true" : "false")}
    />
  );
}

export function ConfigurationProfileFields({
  gameIdentifier,
  definitions,
  values,
  issues,
  configuredSensitiveKeys = [],
  clearedKeys = [],
  onChange,
  onClear,
}: {
  gameIdentifier?: string;
  definitions: ConfigurationDefinition[];
  values: Record<string, string>;
  issues: ConfigurationValidationIssue[];
  configuredSensitiveKeys?: string[];
  clearedKeys?: string[];
  onChange(key: string, value: string): void;
  onClear?(key: string): void;
}) {
  const { t } = useTranslation();
  const contribution = configurationContribution(gameIdentifier);
  const targets = Object.entries(
    definitions.reduce<Record<string, ConfigurationDefinition[]>>((result, definition) => {
      (result[
        definition.isControlPlaneOnly
          ? t("React_Placement_ControlPlane")
          : definition.targetPath || t("Hosting_ConfigurationProfiles_GeneratedFile")
      ] ??= []).push(definition);
      return result;
    }, {}),
  );
  const issueByKey = new Map(issues.map((issue) => [issue.key, issue]));

  return (
    <div className="next-hosting-configuration-groups">
      {targets.map(([targetPath, targetDefinitions]) => (
        <section className="next-hosting-configuration-target" key={targetPath}>
          <header>
            <span>
              {t(
                targetDefinitions.every((definition) => definition.isControlPlaneOnly)
                  ? "React_Placement_ControlPlane"
                  : "Hosting_ConfigurationProfiles_GeneratedFile",
              )}
            </span>
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
                  const ContributionField = configurationContributionField(
                    contribution,
                    definition.key,
                  );
                  const choices = definition.choices ?? [];
                  const sensitiveConfigured =
                    definition.sensitive &&
                    configuredSensitiveKeys.includes(definition.key) &&
                    !clearedKeys.includes(definition.key);
                  const describedBy = issue ? `${descriptionId} ${errorId}` : descriptionId;
                  const errorMessage = issue ? configurationValidationMessage(issue, t) : undefined;
                  const description = (
                    <>
                      {definition.description}
                      {definition.sensitive && (
                        <>
                          {" "}
                          <strong>{t("Hosting_ConfigurationProfiles_Sensitive")}</strong>
                        </>
                      )}
                      {sensitiveConfigured && ` ${t("React_ConfigurationProfiles_Configured")}.`}
                    </>
                  );
                  const clearControl = !definition.required &&
                    (value !== "" || sensitiveConfigured) && (
                      <div className="next-hosting-configuration-field-meta">
                        <ActionButton
                          variant="ghost"
                          size="compact"
                          onClick={() =>
                            onClear ? onClear(definition.key) : onChange(definition.key, "")
                          }
                        >
                          {t("Hosting_ConfigurationProfiles_ClearValue")}
                        </ActionButton>
                      </div>
                    );

                  if (ContributionField)
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
                        <ContributionField
                          allowUnset
                          id={id}
                          value={value}
                          required={definition.required}
                          invalid={Boolean(issue)}
                          describedBy={describedBy}
                          onChange={(next) => onChange(definition.key, next)}
                        />
                        <div className="next-hosting-configuration-field-meta">
                          <small id={descriptionId}>
                            {definition.description}
                            {sensitiveConfigured &&
                              ` ${t("React_ConfigurationProfiles_Configured")}.`}
                          </small>
                        </div>
                        {clearControl}
                        {errorMessage && (
                          <small className="next-hosting-field-error" id={errorId} role="alert">
                            {errorMessage}
                          </small>
                        )}
                      </div>
                    );

                  return (
                    <div className="next-hosting-configuration-field" key={definition.key}>
                      {kind === "boolean" ? (
                        <ConfigurationBooleanField
                          id={id}
                          label={definition.displayName}
                          description={description}
                          value={value}
                          required={definition.required}
                          error={errorMessage}
                          onChange={(nextValue) => onChange(definition.key, nextValue)}
                        />
                      ) : choices.length > 0 || (definition.allowedValues?.length ?? 0) > 0 ? (
                        <SelectField
                          id={id}
                          label={definition.displayName}
                          hint={description}
                          error={errorMessage}
                          required={definition.required}
                          value={value}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                          options={[
                            ...(definition.required
                              ? []
                              : [{ value: "", label: t("Hosting_ConfigurationProfiles_NotSet") }]),
                            ...(choices.length > 0
                              ? choices.map((option) => {
                                  const optionValue =
                                    typeof option.value === "string"
                                      ? option.value
                                      : JSON.stringify(option.value);
                                  return { value: optionValue, label: option.label };
                                })
                              : (definition.allowedValues ?? []).map((option) => ({
                                  value: option,
                                  label: option,
                                }))),
                          ]}
                        />
                      ) : kind === "stringarray" ? (
                        <TextAreaField
                          id={id}
                          label={definition.displayName}
                          hint={description}
                          error={errorMessage}
                          required={definition.required}
                          rows={3}
                          value={value}
                          placeholder={t("Hosting_ConfigurationProfiles_EnterOnePerLine")}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        />
                      ) : (
                        <TextField
                          id={id}
                          label={definition.displayName}
                          hint={description}
                          error={errorMessage}
                          required={definition.required}
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
                      {clearControl}
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
