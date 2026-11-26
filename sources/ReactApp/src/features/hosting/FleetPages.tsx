import { Warning20Regular } from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import { currentLocale } from "@/app/format";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { Metric, PageHeader } from "@/components/primitives";
import { FleetLiveNotice } from "@/components/server/FleetLiveNotice";
import { FleetSection } from "@/components/server/FleetSection";
import { FleetServerCard } from "@/components/server/FleetServerCard";
import { FleetToolbar, type FleetResultSummary } from "@/components/server/FleetToolbar";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { AccessBoundary, EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { useAuth } from "@/auth/auth";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  countCostlyServers,
  countFilters,
  matchesFilter,
  parseFleetSearchParams,
  partitionFleet,
  searchServers,
  writeFleetSearchParams,
  type FleetFilter,
  type FleetViewParams,
} from "@/features/hosting/fleetModel";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import { deriveServerState } from "@/features/hosting/serverState";
import { useFleetAnnouncements } from "@/features/hosting/useFleetAnnouncements";
import { useFleetFocusFallback } from "@/features/hosting/useFleetFocusFallback";
import { useFleetLiveUpdates } from "@/features/hosting/useFleetLiveUpdates";
import { useFleetQuery } from "@/features/hosting/useFleetQuery";
import { useFleetSubmitted } from "@/features/hosting/useFleetSubmitted";
import type { GameServer, ServerNotification } from "@/features/hosting/types";
import "@/components/server/fleet-page.css";

export function HostingOverviewView({
  servers,
  notifications,
  canViewFinance,
  canCreate,
}: {
  servers: GameServer[];
  notifications: ServerNotification[];
  canViewFinance: boolean;
  canCreate: boolean;
}) {
  const { t } = useTranslation();
  const possibleCost = servers.filter((server) => server.deployment?.possibleCost).length;
  const pending = servers.reduce((count, server) => count + server.pendingJobCount, 0);
  const attention = notifications.filter((notification) => notification.possibleCost);
  const serversById = useMemo(
    () => new Map(servers.map((server) => [server.primaryKey, server])),
    [servers],
  );
  return (
    <>
      <section className="next-hosting-metrics" aria-label={t("Hosting_Overview_Title")}>
        <Metric label={t("Hosting_Servers_Title")} value={servers.length} />
        <Metric label={t("Hosting_Servers_PossibleCost")} value={possibleCost} />
        <Metric label={t("Hosting_Overview_PendingWork")} value={pending} />
        <Metric label={t("Hosting_Overview_CostAttention")} value={attention.length} />
      </section>

      <section className="next-hosting-dashboard-grid">
        <article className="next-hosting-panel">
          <span className="kicker">{t("Hosting_Overview_CostAttention")}</span>
          <h2>{t("Hosting_Servers_PossibleCost")}</h2>
          {attention.length === 0 ? (
            <p className="next-hosting-muted">{t("Hosting_Overview_NoCostAttention")}</p>
          ) : (
            <ul className="next-hosting-attention-list">
              {attention.map((notification) => (
                <li key={notification.serverId}>
                  <Link to={`/command/servers/${encodeURIComponent(notification.serverId)}`}>
                    <strong>{notification.title}</strong>
                  </Link>
                  <ServerStateSummary
                    derived={deriveServerState(serversById.get(notification.serverId), {
                      notification,
                    })}
                  />
                </li>
              ))}
            </ul>
          )}
        </article>

        <nav
          className="next-hosting-panel next-hosting-shortcuts"
          aria-label={t("Hosting_Overview_Title")}
        >
          <span className="kicker">{t("Hosting_Overview_Title")}</span>
          <Link to="/command/servers">{t("Hosting_Nav_Servers")}</Link>
          <Link to="/command/configuration-profiles">
            {t("Hosting_ConfigurationProfiles_Title")}
          </Link>
          {canViewFinance && <Link to="/command/hosting-finance">{t("HostingFinance_Title")}</Link>}
          {canCreate && <Link to="/command/servers/create">{t("Hosting_Servers_Create")}</Link>}
        </nav>
      </section>
    </>
  );
}

export function HostingOverviewPage() {
  const { t } = useTranslation();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const fleet = useFleetQuery(permissions.accessServers);
  const notifications = useQuery({
    queryKey: hostingQueryKeys.notifications,
    queryFn: ({ signal }) => hostingService.notifications(signal),
    refetchInterval: 10_000,
    enabled: permissions.accessServers,
  });
  return (
    <div className="next-hosting-page">
      <PageHeader
        title={t("Hosting_Overview_Title")}
        description={t("Hosting_Overview_Description")}
      />
      <AccessBoundary
        allowed={permissions.accessServers || permissions.viewFinance}
        title={t("Server_Access_ForbiddenTitle")}
        body={t("Server_Access_ForbiddenBody")}
      >
        {authLoading || fleet.isLoading ? (
          <LoadingState />
        ) : fleet.isError ? (
          <ErrorState
            onRetry={() => void Promise.all([fleet.refetch(), notifications.refetch()])}
            retryLabel={t("Server_Access_Retry")}
          >
            {t("Server_Access_LoadError")}
          </ErrorState>
        ) : (
          <HostingOverviewView
            servers={fleet.data ?? []}
            notifications={notifications.data ?? []}
            canViewFinance={permissions.viewFinance}
            canCreate={permissions.createServers}
          />
        )}
      </AccessBoundary>
    </div>
  );
}

/** Shared empty fleet used before the fleet has loaded. */
const noServers: GameServer[] = [];

/** Delay in milliseconds before typed search text is written to the URL. */
const searchDebounceMs = 250;

/** The filters whose result renders the Active section. */
const activeSectionFilters: ReadonlySet<FleetFilter> = new Set<FleetFilter>([
  "all",
  "mine",
  "running",
]);

/** The filters whose result may render the Off section. */
const offSectionFilters: ReadonlySet<FleetFilter> = new Set<FleetFilter>(["all", "mine", "off"]);

/**
 * Search text the viewer typed that may not be in the URL yet.
 *
 * `base` is the URL `q` the draft belongs to. The draft is shown only while the URL `q` still equals `base`, so a
 * navigation that changes `q` from outside (a link, the back button, a reset) replaces the field value without an
 * effect.
 */
interface SearchDraft {
  /** The text exactly as typed, including whitespace the URL drops. */
  value: string;
  /** The URL `q` the draft belongs to. */
  base: string;
}

/**
 * Normalizes search text the same way the URL parser does.
 *
 * @param value The typed search text.
 * @returns The trimmed and capped text that the URL `q` would hold.
 */
function normalizedQuery(value: string): string {
  return parseFleetSearchParams(new URLSearchParams([["q", value]])).q;
}

/**
 * The fleet page: every server the viewer may see, grouped into Active and Off, with search, counted filters, quick
 * start and stop through the shared cost confirmation, and live updates without polling.
 *
 * Search and filter live in the URL (`?q=` and `?filter=`) and are written with history replace, so reloads and
 * shared links restore the view without growing the history. Typed text filters immediately and reaches the URL
 * after a short debounce. The cost line counts the whole fleet, so no search or filter can hide a server that costs
 * money. The fleet query does not poll: updates arrive through the live fleet signal and the Refresh servers button.
 */
export function ServerFleetPage() {
  const { t } = useTranslation();
  useDocumentTitle(t("Hosting_Servers_Title"));
  const auth = useAuth();
  const permissions = getHostingPermissions(auth.user);
  const fleet = useFleetQuery(!auth.isLoading && permissions.accessServers);
  const live = useFleetLiveUpdates();
  const { submittedIds, markedAt, markSubmitted } = useFleetSubmitted(
    fleet.dataUpdatedAt,
    fleet.isError,
  );
  const announcement = useFleetAnnouncements(fleet.data, fleet.dataUpdatedAt, markedAt);

  const [searchParams, setSearchParams] = useSearchParams();
  const view = parseFleetSearchParams(searchParams);
  const [draft, setDraft] = useState<SearchDraft | null>(null);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const sectionsRef = useRef<HTMLDivElement | null>(null);
  const debounceRef = useRef<number | undefined>(undefined);
  const setSearchParamsRef = useRef(setSearchParams);

  useEffect(() => {
    setSearchParamsRef.current = setSearchParams;
  });

  useEffect(() => () => window.clearTimeout(debounceRef.current), []);

  let currentDraft = draft;
  if (
    currentDraft !== null &&
    currentDraft.base !== view.q &&
    normalizedQuery(currentDraft.value) === view.q
  ) {
    // The URL caught up with the typed text; keep the text as typed, including trailing whitespace.
    currentDraft = { value: currentDraft.value, base: view.q };
    setDraft(currentDraft);
  }
  const search =
    currentDraft !== null && currentDraft.base === view.q ? currentDraft.value : view.q;

  /** Writes view state to the URL with history replace, merged into the latest parameters. */
  function writeView(next: Partial<FleetViewParams>): void {
    setSearchParamsRef.current((current) => writeFleetSearchParams(current, next), {
      replace: true,
    });
  }

  function cancelDebounce(): void {
    window.clearTimeout(debounceRef.current);
    debounceRef.current = undefined;
  }

  function handleSearchChange(value: string): void {
    setDraft({ value, base: view.q });
    cancelDebounce();
    if (normalizedQuery(value) === "") {
      writeView({ q: "" });
      return;
    }
    debounceRef.current = window.setTimeout(() => {
      debounceRef.current = undefined;
      writeView({ q: value });
    }, searchDebounceMs);
  }

  function handleClear(): void {
    cancelDebounce();
    setDraft(null);
    writeView({ q: "", filter: "all" });
    searchInputRef.current?.focus();
  }

  const servers = fleet.data ?? noServers;
  const meId = auth.user?.primaryKey;
  const locale = currentLocale();
  const searched = searchServers(servers, search, locale);
  const counts = countFilters(searched, meId);
  const filtered = searched.filter((server) => matchesFilter(server, view.filter, meId));
  const sections = partitionFleet(filtered);
  const costly = countCostlyServers(servers);
  const searching = search.trim() !== "";
  const summary: FleetResultSummary | null =
    searching || view.filter !== "all" ? { shown: filtered.length, total: servers.length } : null;
  const numberFormat = new Intl.NumberFormat(locale);
  const showActive = activeSectionFilters.has(view.filter);
  const showOff = offSectionFilters.has(view.filter) && sections.off.length > 0;

  useFleetFocusFallback(
    sectionsRef,
    [sections.active, sections.off]
      .map((list) => list.map((server) => server.primaryKey).join(","))
      .join("|"),
  );

  const fleetEmpty = fleet.data !== undefined && fleet.data.length === 0;
  const loadError = (
    <ErrorState onRetry={() => void fleet.refetch()} retryLabel={t("Server_Access_Retry")}>
      {t("Server_Access_LoadError")}
    </ErrorState>
  );

  const renderCard = (server: GameServer) => (
    <FleetServerCard
      key={server.primaryKey}
      server={server}
      submitted={submittedIds.has(server.primaryKey)}
      onSubmitted={markSubmitted}
    />
  );

  let body: ReactNode;
  if (fleetEmpty) {
    body = permissions.createServers ? (
      <EmptyState
        title={t("Hosting_Servers_EmptyTitle")}
        action={
          <ActionLink variant="gold" to="/command/servers/create">
            {t("Hosting_Servers_Create")}
          </ActionLink>
        }
      >
        {t("Hosting_Servers_EmptyDescription")}
      </EmptyState>
    ) : (
      <EmptyState title={t("Server_Access_EmptyTitle")}>{t("Server_Access_EmptyBody")}</EmptyState>
    );
  } else {
    const mineEmpty = view.filter === "mine" && !searching;
    body = (
      <>
        {costly > 0 && (
          <p className="fleet-cost-line" role="note">
            <Warning20Regular aria-hidden="true" />
            <span className="fleet-cost-line__text">
              {t("Server_Fleet_CostLine", {
                count: costly,
                replace: { count: numberFormat.format(costly) },
              })}
            </span>
          </p>
        )}
        <FleetToolbar
          search={search}
          onSearchChange={handleSearchChange}
          searchInputRef={searchInputRef}
          filter={view.filter}
          counts={counts}
          onFilterChange={(filter) => writeView({ filter })}
          summary={summary}
        />
        <FleetLiveNotice
          status={live.status}
          epoch={live.disconnectEpoch}
          onReconnect={live.reconnectNow}
        />
        <div className="fleet-sections" ref={sectionsRef}>
          {filtered.length === 0 ? (
            <EmptyState
              title={
                mineEmpty ? t("Server_Fleet_Mine_Empty_Title") : t("Server_Fleet_NoMatch_Title")
              }
              action={
                <ActionButton variant="ghost" onClick={handleClear}>
                  {t("Server_Fleet_NoMatch_Clear")}
                </ActionButton>
              }
            >
              {mineEmpty ? t("Server_Fleet_Mine_Empty_Body") : t("Server_Fleet_NoMatch_Body")}
            </EmptyState>
          ) : (
            <>
              {showActive && (
                <FleetSection
                  title={t("Server_Fleet_Section_Active", {
                    replace: { count: numberFormat.format(sections.active.length) },
                  })}
                  emptyText={t("Server_Fleet_Section_ActiveEmpty")}
                >
                  {sections.active.map(renderCard)}
                </FleetSection>
              )}
              {showOff && (
                <FleetSection
                  title={t("Server_Fleet_Section_Off", {
                    replace: { count: numberFormat.format(sections.off.length) },
                  })}
                >
                  {sections.off.map(renderCard)}
                </FleetSection>
              )}
            </>
          )}
        </div>
      </>
    );
  }

  return (
    <div className="next-hosting-page" aria-busy={fleet.isFetching}>
      <PageHeader
        title={t("Hosting_Servers_Title")}
        description={t("Hosting_Servers_Description")}
        actions={
          permissions.accessServers ? (
            <>
              <ActionButton
                variant="ghost"
                disabled={fleet.isFetching}
                onClick={() => void fleet.refetch()}
              >
                {t("Server_Fleet_Refresh")}
              </ActionButton>
              <ActionLink variant="ghost" to="/command/servers/cost-alerts">
                {t("Server_Alerts_Page_Title")}
              </ActionLink>
              {permissions.createServers ? (
                !fleetEmpty && (
                  <ActionLink variant="gold" to="/command/servers/create">
                    {t("Hosting_Servers_Create")}
                  </ActionLink>
                )
              ) : (
                <ActionButton
                  variant="gold"
                  unavailableReason={t("Server_Reason_CreatePermission")}
                >
                  {t("Hosting_Servers_Create")}
                </ActionButton>
              )}
            </>
          ) : undefined
        }
      />
      {auth.isLoading ? (
        <LoadingState label={t("Hosting_Servers_Loading")} />
      ) : (
        <AccessBoundary
          allowed={permissions.accessServers}
          title={t("Server_Access_ForbiddenTitle")}
          body={t("Server_Access_ForbiddenBody")}
        >
          {fleet.isPending ? (
            <LoadingState label={t("Hosting_Servers_Loading")} />
          ) : fleet.isError && fleet.data === undefined ? (
            loadError
          ) : (
            <>
              {fleet.isError && loadError}
              {body}
            </>
          )}
        </AccessBoundary>
      )}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {announcement}
      </div>
    </div>
  );
}
