import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageSettings } from "@/auth/claims";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  loadSettingsSnapshot,
  type SettingsFeature,
  type SettingsSnapshot,
} from "@/features/admin/settings";

export interface NormalizedSettingsSnapshot {
  apiBaseUrlConfigured: boolean;
  clientBaseUrlConfigured: boolean;
  basePathConfigured: boolean;
  autoVerifyNewUsers: boolean;
  features: Required<SettingsFeature>[];
}

export function normalizeSettingsSnapshot(
  snapshot: SettingsSnapshot | undefined,
): NormalizedSettingsSnapshot {
  return {
    apiBaseUrlConfigured: Boolean(snapshot?.apiBaseUrlConfigured),
    clientBaseUrlConfigured: Boolean(snapshot?.clientBaseUrlConfigured),
    basePathConfigured: Boolean(snapshot?.basePathConfigured),
    autoVerifyNewUsers: Boolean(snapshot?.autoVerifyNewUsers),
    features: (snapshot?.features ?? []).map((feature) => ({
      area: feature.area ?? "",
      detail: feature.detail ?? "",
      isEnabled: Boolean(feature.isEnabled),
      isConfigured: Boolean(feature.isConfigured),
    })),
  };
}

export function settingsFeatureTone(
  feature: Pick<Required<SettingsFeature>, "isEnabled" | "isConfigured">,
): "neutral" | "success" | "warning" {
  if (!feature.isEnabled) return "neutral";
  return feature.isConfigured ? "success" : "warning";
}

export function settingsFeatureDetailKey(area: string): TranslationKey | undefined {
  const keys: Record<string, TranslationKey> = {
    Steam: "Pages_Command_Settings_Feature_Steam_Detail",
    "Discord OAuth": "Pages_Command_Settings_Feature_DiscordOAuth_Detail",
    "Discord Bot": "Pages_Command_Settings_Feature_DiscordBot_Detail",
    TeamSpeak: "Pages_Command_Settings_Feature_TeamSpeak_Detail",
  };
  return keys[area];
}

export function Component() {
  const { t, i18n } = useTranslation();
  const locale = i18n.resolvedLanguage?.startsWith("de") ? "de-DE" : "en-US";
  const { user } = useAuth();
  const authorized = canManageSettings(user);
  const query = useQuery({
    queryKey: ["command-center", "settings-snapshot"],
    queryFn: async ({ signal }) => normalizeSettingsSnapshot(await loadSettingsSnapshot(signal)),
    enabled: authorized,
  });
  const snapshot = query.data;
  const orderedFeatures = useMemo(
    () =>
      [...(snapshot?.features ?? [])].sort((left, right) =>
        left.area.localeCompare(right.area, locale),
      ),
    [locale, snapshot?.features],
  );
  const [selectedArea, setSelectedArea] = useState<string>();
  const selectedFeature =
    orderedFeatures.find((feature) => feature.area === selectedArea) ??
    orderedFeatures.find((feature) => feature.isEnabled && !feature.isConfigured) ??
    orderedFeatures[0];
  const configuredFeatureCount =
    snapshot?.features.filter((feature) => feature.isConfigured).length ?? 0;
  const unconfiguredEnabledFeatureCount =
    snapshot?.features.filter((feature) => feature.isEnabled && !feature.isConfigured).length ?? 0;
  const featureDetail = (feature: Required<SettingsFeature>) => {
    const key = settingsFeatureDetailKey(feature.area);
    return key ? t(key) : feature.detail;
  };

  if (!authorized)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Settings_AccessRequiredTitle")}>
        {t("Pages_Command_Settings_AccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="command-settings-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Settings")}
        title={t("Pages_Command_Settings_HeaderTitle")}
        description={t("Pages_Command_Settings_HeaderDescription")}
        actions={
          <button
            className="button button--ghost"
            type="button"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            {t("Pages_Command_Settings_ActionRefresh")}
          </button>
        }
      />

      {query.isPending && (
        <StatePanel kind="loading">{t("Pages_Command_Settings_Loading")}</StatePanel>
      )}
      {query.isError && (
        <StatePanel
          kind="error"
          title={t("Pages_Command_Settings_LoadError")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Settings_ActionRefresh")}
            </button>
          }
        >
          {query.error.message}
        </StatePanel>
      )}

      {snapshot && (
        <>
          <section
            className="command-metrics"
            aria-label={t("Pages_Command_Settings_MetricsAriaLabel")}
          >
            <Metric
              label={t("Pages_Command_Settings_MetricReadinessStatus")}
              value={`${new Intl.NumberFormat(locale).format(configuredFeatureCount)} ${t(
                configuredFeatureCount === 1
                  ? "Pages_Command_Settings_Count_Singular_ConfiguredFeature"
                  : "Pages_Command_Settings_Count_Plural_ConfiguredFeature",
              )}`}
              detail={formatResource(
                t("Pages_Command_Settings_MetricReadinessDescription"),
                unconfiguredEnabledFeatureCount,
              )}
            />
            <Metric
              label={t("Pages_Command_Settings_MetricRoutingStatus")}
              value={formatResource(
                t("Pages_Command_Settings_RoutingSummary"),
                [
                  snapshot.apiBaseUrlConfigured,
                  snapshot.clientBaseUrlConfigured,
                  snapshot.basePathConfigured,
                ].filter(Boolean).length,
              )}
              detail={t("Pages_Command_Settings_MetricRoutingDescription")}
            />
            <Metric
              label={t("Pages_Command_Settings_MetricUsersStatus")}
              value={t(
                snapshot.autoVerifyNewUsers
                  ? "Pages_Command_Settings_AutoVerifyEnabled"
                  : "Pages_Command_Settings_AutoVerifyManual",
              )}
              detail={t("Pages_Command_Settings_MetricUsersDescription")}
            />
          </section>

          <div className="command-settings-workspace">
            <section className="command-panel">
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Settings_ReadinessKicker")}</span>
                <h2>{t("Pages_Command_Settings_FeatureRowsTitle")}</h2>
                <StatusBadge>
                  {new Intl.NumberFormat(locale).format(orderedFeatures.length)}{" "}
                  {t(
                    orderedFeatures.length === 1
                      ? "Pages_Command_Settings_Count_Singular_Row"
                      : "Pages_Command_Settings_Count_Plural_Row",
                  )}
                </StatusBadge>
              </header>
              {orderedFeatures.length === 0 ? (
                <StatePanel>{t("Pages_Command_Settings_NoRowsReturned")}</StatePanel>
              ) : (
                <div className="command-table-scroll">
                  <table className="command-table">
                    <thead>
                      <tr>
                        <th>{t("Pages_Command_Settings_HeaderFeature")}</th>
                        <th>{t("Pages_Command_Settings_HeaderStatus")}</th>
                        <th>{t("Pages_Command_Settings_HeaderEnabled")}</th>
                        <th>{t("Pages_Command_Settings_HeaderConfigured")}</th>
                        <th>{t("Pages_Command_Settings_HeaderDetail")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {orderedFeatures.map((feature) => (
                        <tr
                          key={feature.area}
                          className={
                            feature.area === selectedFeature?.area ? "is-selected" : undefined
                          }
                          tabIndex={0}
                          aria-selected={feature.area === selectedFeature?.area}
                          onClick={() => setSelectedArea(feature.area)}
                          onKeyDown={(event) => {
                            if (event.key !== "Enter" && event.key !== " ") return;
                            event.preventDefault();
                            setSelectedArea(feature.area);
                          }}
                        >
                          <th scope="row">
                            <strong>{feature.area}</strong>
                            <span>{featureDetail(feature)}</span>
                          </th>
                          <td>
                            <StatusBadge tone={settingsFeatureTone(feature)}>
                              {t(
                                !feature.isEnabled
                                  ? "Pages_Command_Settings_StatusDisabled"
                                  : feature.isConfigured
                                    ? "Pages_Command_Settings_StatusReady"
                                    : "Pages_Command_Settings_StatusNeedsConfig",
                              )}
                            </StatusBadge>
                          </td>
                          <td>
                            {t(
                              feature.isEnabled
                                ? "Pages_Command_Settings_ValueEnabled"
                                : "Pages_Command_Settings_ValueDisabled",
                            )}
                          </td>
                          <td>
                            {t(
                              feature.isConfigured
                                ? "Pages_Command_Settings_ValueConfigured"
                                : "Pages_Command_Settings_ValueMissing",
                            )}
                          </td>
                          <td>{featureDetail(feature)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            <aside
              className="command-panel command-settings-detail"
              aria-label={t("Auto_SelectedSettingsDetail_9a11f6a5")}
            >
              {!selectedFeature ? (
                <StatePanel>{t("Pages_Command_Settings_NoFeatureSelected")}</StatePanel>
              ) : (
                <>
                  <header className="command-panel__header">
                    <span className="kicker">
                      {t("Pages_Command_Settings_SelectedSettingKicker")}
                    </span>
                    <h2>{selectedFeature.area}</h2>
                    <StatusBadge tone={settingsFeatureTone(selectedFeature)}>
                      {t(
                        !selectedFeature.isEnabled
                          ? "Pages_Command_Settings_StatusDisabled"
                          : selectedFeature.isConfigured
                            ? "Pages_Command_Settings_StatusReady"
                            : "Pages_Command_Settings_StatusNeedsConfig",
                      )}
                    </StatusBadge>
                  </header>
                  <dl className="command-detail-list">
                    <dt>{t("Pages_Command_Settings_DetailEnabled")}</dt>
                    <dd>
                      {t(
                        selectedFeature.isEnabled
                          ? "Pages_Command_Settings_ValueYes"
                          : "Pages_Command_Settings_ValueNo",
                      )}
                    </dd>
                    <dt>{t("Pages_Command_Settings_DetailConfigured")}</dt>
                    <dd>
                      {t(
                        selectedFeature.isConfigured
                          ? "Pages_Command_Settings_ValueYes"
                          : "Pages_Command_Settings_ValueNo",
                      )}
                    </dd>
                    <dt>{t("Pages_Command_Settings_DetailLabel")}</dt>
                    <dd>{featureDetail(selectedFeature)}</dd>
                  </dl>
                  <section className="command-redaction-note">
                    <h3>{t("Pages_Command_Settings_RedactionPolicyTitle")}</h3>
                    <p>{t("Pages_Command_Settings_RedactionPolicyBody")}</p>
                  </section>
                </>
              )}
            </aside>
          </div>
        </>
      )}
    </section>
  );
}
