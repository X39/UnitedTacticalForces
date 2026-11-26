import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { ApiError } from "@/api/client";
import { Breadcrumbs } from "@/components/Breadcrumbs";
import { PageStack } from "@/components/PageStack";
import { PageHeader, StatePanel } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { CostAlertSettingsPanel } from "@/features/hosting/CostAlertSettingsPanel";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import "@/features/hosting/cost-alert-settings.css";

/** A canonical or plain textual GUID, the only shape the `owner` query parameter may have. */
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** A private-use character that stands in for the owner name while the localized sentence is split around it. */
const ownerMarker = "\uE000";

/** Props of {@link OwnerMeta}. */
interface OwnerMetaProps {
  /** The localized sentence with the owner marker where the name belongs. */
  label: string;
  /** The cost owner name, which is user data. */
  name: string;
}

/**
 * Renders the "Cost owner" line with the owner name isolated in `bdi`, so a long or right-to-left name wraps and
 * never reorders the surrounding text, whatever position the language gives the name.
 *
 * @param props The sentence and the name.
 * @returns The meta line.
 */
function OwnerMeta({ label, name }: OwnerMetaProps) {
  const [before = "", after = ""] = label.split(ownerMarker);
  return (
    <span>
      {before}
      <bdi>{name}</bdi>
      {after}
    </span>
  );
}

/**
 * The member page for the cost alert settings of one cost owner.
 *
 * Without an `owner` query parameter the page edits the viewer's own personal cost owner. A malformed `owner`
 * shows the not-found panel without any request; an owner the viewer may not see shows the forbidden panel and an
 * unknown owner shows the not-found panel. A viewer without a personal cost owner sees an explaining panel. The
 * route needs server access only, so a member who pays for their own server can reach it without finance rights.
 *
 * @returns The page with a loading, error, forbidden, not-found, no-owner or loaded state.
 */
export function CostAlertsPage() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const owner = params.get("owner");
  const malformed = owner !== null && !guidPattern.test(owner);
  const queryKey = hostingQueryKeys.costAlertSettings(owner ?? "mine");
  const settings = useQuery({
    queryKey,
    enabled: !malformed,
    queryFn: ({ signal }) =>
      owner === null
        ? hostingService.personalCostAlertSettings(signal)
        : hostingService.costAlertSettings(owner, signal),
  });

  const breadcrumbs = (
    <Breadcrumbs
      label={t("Server_Workspace_Breadcrumb_Label")}
      items={[
        { label: t("Hosting_Servers_Title"), to: "/command/servers" },
        { label: t("Server_Alerts_Page_Title") },
      ]}
    />
  );

  const loadedSettings = malformed ? undefined : (settings.data ?? undefined);
  /** The description and the owner line appear once the settings are known; the h1 is always shown. */
  const headerDetails = loadedSettings
    ? {
        description: t("Server_Alerts_Page_Description"),
        meta: (
          <OwnerMeta
            label={t("Server_Alerts_Page_Owner", { name: ownerMarker })}
            name={loadedSettings.costOwnerName}
          />
        ),
      }
    : {};
  const notFound = (
    <EmptyState title={t("App_NotFound_Title")}>{t("Server_Alerts_Page_NotFound")}</EmptyState>
  );
  let body;
  if (malformed) {
    body = notFound;
  } else if (settings.isPending) {
    body = <LoadingState label={t("Server_Alerts_Page_Loading")} />;
  } else if (settings.isError && settings.data === undefined) {
    const status = settings.error instanceof ApiError ? settings.error.status : 0;
    if (status === 403) {
      body = <ForbiddenState>{t("Server_Alerts_Page_Forbidden")}</ForbiddenState>;
    } else if (status === 404) {
      body = notFound;
    } else {
      body = (
        <ErrorState onRetry={() => void settings.refetch()}>
          {t("Server_Alerts_Page_LoadError")}
        </ErrorState>
      );
    }
  } else if (settings.data === null) {
    body = <StatePanel kind="info">{t("Server_Alerts_Page_NoOwner")}</StatePanel>;
  } else {
    body = (
      <CostAlertSettingsPanel
        key={settings.data.costOwnerId}
        settings={settings.data}
        queryKey={queryKey}
      />
    );
  }

  return (
    <div className="cost-alerts-page">
      {breadcrumbs}
      <PageHeader title={t("Server_Alerts_Page_Title")} {...headerDetails} />
      <PageStack>{body}</PageStack>
    </div>
  );
}
