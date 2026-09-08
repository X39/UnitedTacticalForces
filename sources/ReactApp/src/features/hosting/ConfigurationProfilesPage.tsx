import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useAuth } from "@/auth/auth";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { getHostingPermissions } from "@/features/hosting/authorization";
import { gameLabel } from "@/features/hosting/ConfigurationProfileFields";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";

type StatusFilter = "all" | "active" | "archived";

export function ConfigurationProfilesPage() {
  const { t } = useTranslation();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const profiles = useQuery({
    queryKey: hostingQueryKeys.profiles,
    queryFn: ({ signal }) => hostingService.profiles(signal),
    enabled: permissions.accessConfigurationProfiles,
  });
  const [search, setSearch] = useState("");
  const [game, setGame] = useState("all");
  const [status, setStatus] = useState<StatusFilter>("all");
  const items = useMemo(() => profiles.data ?? [], [profiles.data]);
  const games = useMemo(
    () => [...new Set(items.map((profile) => profile.gameIdentifier))].toSorted(),
    [items],
  );
  const filtered = useMemo(() => {
    const term = search.trim().toLocaleLowerCase();
    return items.filter(
      (profile) =>
        (!term ||
          profile.title.toLocaleLowerCase().includes(term) ||
          gameLabel(profile.gameIdentifier).toLocaleLowerCase().includes(term)) &&
        (game === "all" || profile.gameIdentifier === game) &&
        (status === "all" || (status === "archived") === profile.isArchived),
    );
  }, [game, items, search, status]);
  const activeCount = items.filter((profile) => !profile.isArchived).length;
  const archivedCount = items.length - activeCount;

  return (
    <div className="next-hosting-page next-hosting-profile-library-page">
      <PageHeader
        title={t("Hosting_ConfigurationProfiles_Title")}
        description={t("Hosting_ConfigurationProfiles_Description")}
        actions={
          permissions.createConfigurationProfiles ? (
            <Link className="button button--gold" to="/command/configuration-profiles/create">
              {t("Hosting_ConfigurationProfiles_NewProfile")}
            </Link>
          ) : undefined
        }
      />
      {!permissions.accessConfigurationProfiles ? (
        <StatePanel kind="forbidden" title={t("Hosting_ConfigurationProfiles_ForbiddenTitle")}>
          {t("Hosting_ConfigurationProfiles_ForbiddenDescription")}
        </StatePanel>
      ) : authLoading || profiles.isLoading ? (
        <StatePanel kind="loading">{t("Hosting_ConfigurationProfiles_Loading")}</StatePanel>
      ) : profiles.error ? (
        <StatePanel
          kind="error"
          title={t("Hosting_ConfigurationProfiles_LoadError").replace(": {0}", "")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void profiles.refetch()}
            >
              {t("Hosting_ConfigurationProfiles_Retry")}
            </button>
          }
        >
          {profiles.error.message}
        </StatePanel>
      ) : items.length === 0 ? (
        <StatePanel
          kind="empty"
          title={t("Hosting_ConfigurationProfiles_NoProfilesTitle")}
          action={
            permissions.createConfigurationProfiles ? (
              <Link className="button button--gold" to="/command/configuration-profiles/create">
                {t("Hosting_ConfigurationProfiles_Create")}
              </Link>
            ) : undefined
          }
        >
          {t("Hosting_ConfigurationProfiles_NoProfilesDescription")}
        </StatePanel>
      ) : (
        <>
          <section
            className="next-hosting-profile-library-metrics"
            aria-label={t("React_ConfigurationProfiles_LibrarySummary")}
          >
            <Metric label={t("React_ConfigurationProfiles_TotalProfiles")} value={items.length} />
            <Metric label={t("React_ConfigurationProfiles_ActiveProfiles")} value={activeCount} />
            <Metric
              label={t("React_ConfigurationProfiles_ArchivedProfiles")}
              value={archivedCount}
            />
          </section>
          <section className="next-hosting-panel next-hosting-profile-catalog">
            <div className="next-hosting-profile-filters">
              <label className="field next-hosting-profile-search" htmlFor="profile-search">
                <span>{t("React_ConfigurationProfiles_Search")}</span>
                <input
                  id="profile-search"
                  aria-label={t("React_ConfigurationProfiles_Search")}
                  type="search"
                  value={search}
                  placeholder={t("React_ConfigurationProfiles_SearchPlaceholder")}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
              <label className="field" htmlFor="profile-game-filter">
                <span>{t("Hosting_ConfigurationProfiles_Game")}</span>
                <select
                  id="profile-game-filter"
                  aria-label={t("Hosting_ConfigurationProfiles_Game")}
                  value={game}
                  onChange={(event) => setGame(event.target.value)}
                >
                  <option value="all">{t("React_ConfigurationProfiles_AllGames")}</option>
                  {games.map((identifier) => (
                    <option key={identifier} value={identifier}>
                      {gameLabel(identifier)}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field" htmlFor="profile-status-filter">
                <span>{t("React_ConfigurationProfiles_Status")}</span>
                <select
                  id="profile-status-filter"
                  aria-label={t("React_ConfigurationProfiles_Status")}
                  value={status}
                  onChange={(event) => setStatus(event.target.value as StatusFilter)}
                >
                  <option value="all">{t("React_ConfigurationProfiles_AllStatuses")}</option>
                  <option value="active">{t("Hosting_ConfigurationProfiles_Active")}</option>
                  <option value="archived">{t("Hosting_ConfigurationProfiles_Archived")}</option>
                </select>
              </label>
            </div>
            {filtered.length === 0 ? (
              <StatePanel kind="empty" title={t("React_ConfigurationProfiles_NoMatchesTitle")}>
                {t("React_ConfigurationProfiles_NoMatchesDescription")}
              </StatePanel>
            ) : (
              <div className="next-hosting-profile-card-grid">
                {filtered.map((profile) => (
                  <article className="next-hosting-profile-card" key={profile.primaryKey}>
                    <header>
                      <span className="next-hosting-profile-game-mark" aria-hidden="true">
                        {gameLabel(profile.gameIdentifier).slice(0, 2).toUpperCase()}
                      </span>
                      <StatusBadge tone={profile.isArchived ? "warning" : "success"}>
                        {profile.isArchived
                          ? t("Hosting_ConfigurationProfiles_Archived")
                          : t("Hosting_ConfigurationProfiles_Active")}
                      </StatusBadge>
                    </header>
                    <div>
                      <span className="kicker">{gameLabel(profile.gameIdentifier)}</span>
                      <h2>{profile.title}</h2>
                    </div>
                    <footer>
                      <span>
                        {t("Hosting_ConfigurationProfiles_Revision")} {profile.latestRevisionNumber}
                      </span>
                      <Link to={`/command/configuration-profiles/${profile.primaryKey}`}>
                        {t("React_ConfigurationProfiles_OpenProfile")}
                      </Link>
                    </footer>
                  </article>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}
