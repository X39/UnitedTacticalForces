import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { StatePanel, useUnsavedChanges } from "@/components/primitives";
import {
  configurationContribution,
  configurationContributionField,
} from "./implementations/registry";
import { PlacementEstimate } from "./PlacementEstimate";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { ConfigurationValue, GameConfigurationEntry } from "@/features/hosting/types";
import {
  configurationInputValue,
  parentServerPath,
  parseConfigurationEntryValue,
  validateConfigurationEntry,
} from "@/features/hosting/workspaceModel";

export function ServerConfigurationPanel({
  serverId,
  gameIdentifier,
  canModify,
}: {
  serverId: string;
  gameIdentifier?: string;
  canModify: boolean;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const configuration = useQuery({
    queryKey: hostingQueryKeys.configuration(serverId),
    queryFn: ({ signal }) => hostingService.configuration(serverId, signal),
  });
  const contribution = configurationContribution(gameIdentifier);
  const demand = useQuery({
    queryKey: hostingQueryKeys.storageDemand(serverId),
    queryFn: ({ signal }) => hostingService.storageDemand(serverId, signal),
    enabled: contribution?.showPlacementEstimate === true,
  });
  const options = useQuery({
    queryKey: hostingQueryKeys.configurationOptions(serverId),
    queryFn: ({ signal }) => hostingService.serverConfigurationProfileOptions(serverId, signal),
    enabled: canModify,
  });
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [overriddenTargets, setOverriddenTargets] = useState<string[]>([]);
  const [validationError, setValidationError] = useState<string>();
  const dirty = Object.keys(draft).length > 0;
  useUnsavedChanges(dirty);

  useEffect(() => {
    const entries = configuration.data?.entries ?? [];
    const targets = [
      ...new Set(
        entries
          .filter((entry) => !entry.isControlPlaneOnly && entry.targetPath.trim())
          .map((entry) => entry.targetPath),
      ),
    ];
    if (targets.length === 0) return;
    let active = true;
    void Promise.all(
      targets.map(async (target) => {
        const files = await hostingService.files(serverId, "persistent", parentServerPath(target));
        return files.some(
          (file) =>
            file.path.toLowerCase() === target.toLowerCase() &&
            (file.overridesGenerated || file.configurationStatus === "Conflict"),
        )
          ? target
          : undefined;
      }),
    )
      .then((values) => {
        if (active) setOverriddenTargets(values.filter((value): value is string => Boolean(value)));
      })
      .catch(() => {
        if (active) setOverriddenTargets([]);
      });
    return () => {
      active = false;
    };
  }, [configuration.data?.entries, serverId]);

  const refresh = async () => {
    await client.cancelQueries({ queryKey: hostingQueryKeys.storageDemand(serverId) });
    await client.invalidateQueries({ queryKey: hostingQueryKeys.fleet });
  };
  const update = useMutation({
    mutationFn: ({ values, resetKeys }: { values: ConfigurationValue[]; resetKeys: string[] }) =>
      hostingService.updateConfiguration(serverId, values, resetKeys),
    onMutate: () => client.cancelQueries({ queryKey: hostingQueryKeys.storageDemand(serverId) }),
    onSuccess: async (_, variables) => {
      setDraft((current) =>
        Object.fromEntries(
          Object.entries(current).filter(
            ([key]) =>
              !variables.resetKeys.includes(key) &&
              !variables.values.some((value) => value.key === key),
          ),
        ),
      );
      await refresh();
    },
  });
  const profile = useMutation({
    mutationFn: ({
      profileId,
      revisionId,
    }: {
      profileId: string | null;
      revisionId: string | null;
    }) => hostingService.selectConfigurationProfile(serverId, profileId, revisionId),
    onMutate: () => client.cancelQueries({ queryKey: hostingQueryKeys.storageDemand(serverId) }),
    onSuccess: refresh,
  });

  const grouped = useMemo(() => {
    const targets = new Map<string, Map<string, GameConfigurationEntry[]>>();
    for (const entry of configuration.data?.entries ?? []) {
      const targetPath = entry.isControlPlaneOnly ? "" : entry.targetPath;
      const groups = targets.get(targetPath) ?? new Map<string, GameConfigurationEntry[]>();
      const values = groups.get(entry.group) ?? [];
      values.push(entry);
      groups.set(entry.group, values);
      targets.set(targetPath, groups);
    }
    return targets;
  }, [configuration.data?.entries]);

  if (configuration.isLoading)
    return (
      <StatePanel kind="loading">{t("React_ServerWorkspace_LoadingConfiguration")}</StatePanel>
    );
  if (configuration.isError)
    return <StatePanel kind="error">{configuration.error.message}</StatePanel>;
  if (!configuration.data) return null;
  const error = validationError ?? update.error?.message ?? profile.error?.message;
  return (
    <section className="next-hosting-panel next-hosting-configuration-panel">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("React_ServerWorkspace_EffectiveConfiguration")}</span>
          <h2>{t("React_ServerWorkspace_StructuredSettings")}</h2>
        </div>
      </header>
      <p>{t("React_ServerWorkspace_StructuredDescription")}</p>
      {error && (
        <p role="alert" className="next-hosting-inline-error">
          {error}
        </p>
      )}
      <label className="field">
        <span>{t("React_ServerWorkspace_ConfigurationProfile")}</span>
        <select
          aria-label={t("React_ServerWorkspace_ConfigurationProfile")}
          disabled={!canModify || update.isPending || profile.isPending}
          value={configuration.data.profileId ?? ""}
          onChange={(event) =>
            profile.mutate({ profileId: event.target.value || null, revisionId: null })
          }
        >
          <option value="">{t("React_ServerWorkspace_GameDefaultsOnly")}</option>
          {(options.data ?? []).map((option) => (
            <option key={option.primaryKey} value={option.primaryKey}>
              {option.title}
            </option>
          ))}
        </select>
      </label>
      {configuration.data.profileId && (
        <div className="next-hosting-form-actions">
          <button
            type="button"
            className="button button--ghost"
            disabled={!canModify || update.isPending || profile.isPending}
            onClick={() =>
              profile.mutate({ profileId: configuration.data.profileId, revisionId: null })
            }
          >
            {t("React_ServerWorkspace_FollowLatest")}
          </button>
          <button
            type="button"
            className="button button--ghost"
            disabled={
              !canModify ||
              !configuration.data.profileRevisionId ||
              update.isPending ||
              profile.isPending
            }
            onClick={() =>
              profile.mutate({
                profileId: configuration.data.profileId,
                revisionId: configuration.data.profileRevisionId,
              })
            }
          >
            {formatResource(
              t("React_ServerWorkspace_PinRevision"),
              configuration.data.profileRevisionNumber ?? "",
            )}
          </button>
        </div>
      )}
      {!configuration.data.isComplete && (
        <StatePanel kind="warning">{t("React_ServerWorkspace_ConfigurationIncomplete")}</StatePanel>
      )}
      {contribution?.showPlacementEstimate && (
        <PlacementEstimate
          estimate={demand.data?.placementEstimate}
          dirty={dirty}
          pending={
            update.isPending || profile.isPending || demand.isFetching || configuration.isFetching
          }
          unavailable={demand.isError || update.isError || profile.isError}
        />
      )}
      <div className="next-hosting-configuration-groups">
        {[...grouped].map(([target, groups]) => (
          <section key={target} className="next-hosting-configuration-target">
            <header>
              <span>
                {t(
                  target ? "React_ServerWorkspace_GeneratedTarget" : "React_Placement_ControlPlane",
                )}
              </span>
              <code>{target}</code>
            </header>
            {overriddenTargets.includes(target) ? (
              <StatePanel kind="warning">{t("React_ServerWorkspace_PhysicalOverride")}</StatePanel>
            ) : (
              [...groups].map(([group, entries]) => (
                <fieldset key={group}>
                  <legend>{group}</legend>
                  {entries
                    .sort((left, right) => left.order - right.order)
                    .map((entry) => {
                      const value = draft[entry.key] ?? configurationInputValue(entry);
                      const inputId = `server-configuration-${entry.key.replace(/[^a-z0-9]/gi, "-")}`;
                      const setValue = (next: string) =>
                        setDraft((current) => ({ ...current, [entry.key]: next }));
                      const ContributionField = configurationContributionField(
                        contribution,
                        entry.key,
                      );
                      return (
                        <div className="field" key={entry.key}>
                          <div className="next-hosting-configuration-field-heading">
                            <label htmlFor={inputId}>{entry.displayName}</label>
                            <span>
                              {formatResource(t("React_ServerWorkspace_FromSource"), entry.source)}
                            </span>
                          </div>
                          {ContributionField ? (
                            <ContributionField
                              id={inputId}
                              value={value}
                              required={entry.required}
                              disabled={!canModify || update.isPending || profile.isPending}
                              onChange={setValue}
                            />
                          ) : entry.kind.toLowerCase() === "boolean" ? (
                            <select
                              id={inputId}
                              disabled={!canModify || update.isPending || profile.isPending}
                              value={value || "false"}
                              onChange={(event) => setValue(event.target.value)}
                            >
                              <option value="true">{t("React_ServerWorkspace_Enabled")}</option>
                              <option value="false">{t("React_ServerWorkspace_Disabled")}</option>
                            </select>
                          ) : entry.kind.toLowerCase() === "selection" ? (
                            <select
                              id={inputId}
                              disabled={!canModify || update.isPending || profile.isPending}
                              value={value}
                              onChange={(event) => setValue(event.target.value)}
                            >
                              {!entry.required && (
                                <option value="">{t("React_ServerWorkspace_NotSet")}</option>
                              )}
                              {(entry.choices?.length
                                ? entry.choices
                                : entry.allowedValues.map((value) => ({ value, label: value }))
                              ).map((option) => (
                                <option key={String(option.value)} value={String(option.value)}>
                                  {option.label}
                                </option>
                              ))}
                            </select>
                          ) : entry.kind.toLowerCase() === "stringarray" ? (
                            <textarea
                              id={inputId}
                              disabled={!canModify || update.isPending || profile.isPending}
                              rows={3}
                              value={value}
                              onChange={(event) => setValue(event.target.value)}
                            />
                          ) : (
                            <input
                              id={inputId}
                              disabled={!canModify || update.isPending || profile.isPending}
                              type={
                                entry.sensitive
                                  ? "password"
                                  : ["integer", "number", "decimal"].includes(
                                        entry.kind.toLowerCase(),
                                      )
                                    ? "number"
                                    : "text"
                              }
                              min={entry.minimum ?? undefined}
                              max={entry.maximum ?? undefined}
                              step={entry.kind.toLowerCase() === "integer" ? 1 : undefined}
                              placeholder={
                                entry.isRedacted
                                  ? t("React_ServerWorkspace_StoredReplacement")
                                  : undefined
                              }
                              value={value}
                              onChange={(event) => setValue(event.target.value)}
                            />
                          )}
                          <small>{entry.description}</small>
                          {entry.warning && (
                            <small className="next-hosting-field-error">{entry.warning}</small>
                          )}
                          {canModify && entry.source === "Server" && (
                            <button
                              type="button"
                              className="button button--ghost"
                              disabled={update.isPending || profile.isPending}
                              onClick={() => update.mutate({ values: [], resetKeys: [entry.key] })}
                            >
                              {t("React_ServerWorkspace_ResetInherited")}
                            </button>
                          )}
                        </div>
                      );
                    })}
                </fieldset>
              ))
            )}
          </section>
        ))}
      </div>
      {canModify && (
        <div className="next-hosting-configuration-savebar">
          <span>
            {dirty
              ? formatResource(
                  t("React_ServerWorkspace_UnsavedOverrides"),
                  Object.keys(draft).length,
                )
              : t("React_ServerWorkspace_NoUnsavedOverrides")}
          </span>
          <button
            type="button"
            className="button button--primary"
            disabled={!dirty || update.isPending || profile.isPending}
            onClick={() => {
              const entries = configuration.data.entries.filter(
                (entry) => draft[entry.key] !== undefined,
              );
              const issue = entries
                .map((entry) => validateConfigurationEntry(entry, draft[entry.key] ?? ""))
                .find(Boolean);
              if (issue) {
                setValidationError(t("React_ServerWorkspace_ConfigurationInvalid"));
                return;
              }
              setValidationError(undefined);
              update.mutate({
                values: entries.map((entry) => ({
                  key: entry.key,
                  value: parseConfigurationEntryValue(entry, draft[entry.key] ?? ""),
                })),
                resetKeys: [],
              });
            }}
          >
            {update.isPending
              ? t("React_ServerWorkspace_Saving")
              : t("React_ServerWorkspace_SaveOverrides")}
          </button>
        </div>
      )}
    </section>
  );
}
