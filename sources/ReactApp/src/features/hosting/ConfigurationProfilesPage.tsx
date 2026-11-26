import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { SelectField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
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
  const clearFilters = () => {
    setSearch("");
    setGame("all");
    setStatus("all");
  };
  const activeCount = items.filter((profile) => !profile.isArchived).length;
  const archivedCount = items.length - activeCount;

  return (
    <div className="next-hosting-page next-hosting-profile-library-page">
      <PageHeader
        title={t("Hosting_ConfigurationProfiles_Title")}
        description={t("Hosting_ConfigurationProfiles_Description")}
        actions={
          permissions.createConfigurationProfiles ? (
            <ActionLink variant="gold" to="/command/configuration-profiles/create">
              {t("Hosting_ConfigurationProfiles_NewProfile")}
            </ActionLink>
          ) : undefined
        }
      />
      <PageStack>
        {!permissions.accessConfigurationProfiles ? (
          <ForbiddenState title={t("Hosting_ConfigurationProfiles_ForbiddenTitle")}>
            {t("Hosting_ConfigurationProfiles_ForbiddenDescription")}
          </ForbiddenState>
        ) : authLoading || profiles.isLoading ? (
          <LoadingState label={t("Hosting_ConfigurationProfiles_Loading")} />
        ) : profiles.error ? (
          <ErrorState
            title={t("Hosting_ConfigurationProfiles_LoadError").replace(": {0}", "")}
            onRetry={() => void profiles.refetch()}
          >
            {profiles.error.message}
          </ErrorState>
        ) : items.length === 0 ? (
          <EmptyState
            title={t("Hosting_ConfigurationProfiles_NoProfilesTitle")}
            action={
              permissions.createConfigurationProfiles ? (
                <ActionLink variant="gold" to="/command/configuration-profiles/create">
                  {t("Hosting_ConfigurationProfiles_Create")}
                </ActionLink>
              ) : undefined
            }
          >
            {t("Hosting_ConfigurationProfiles_NoProfilesDescription")}
          </EmptyState>
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
                <SearchField
                  label={t("React_ConfigurationProfiles_Search")}
                  placeholder={t("React_ConfigurationProfiles_SearchPlaceholder")}
                  clearLabel={t("Common_SearchClear")}
                  value={search}
                  onChange={setSearch}
                />
                <SelectField
                  label={t("Hosting_ConfigurationProfiles_Game")}
                  value={game}
                  onChange={(event) => setGame(event.target.value)}
                  options={[
                    { value: "all", label: t("React_ConfigurationProfiles_AllGames") },
                    ...games.map((identifier) => ({
                      value: identifier,
                      label: gameLabel(identifier),
                    })),
                  ]}
                />
                <SelectField
                  label={t("React_ConfigurationProfiles_Status")}
                  value={status}
                  onChange={(event) => setStatus(event.target.value as StatusFilter)}
                  options={[
                    { value: "all", label: t("React_ConfigurationProfiles_AllStatuses") },
                    { value: "active", label: t("Hosting_ConfigurationProfiles_Active") },
                    { value: "archived", label: t("Hosting_ConfigurationProfiles_Archived") },
                  ]}
                />
              </div>
              {filtered.length === 0 ? (
                <EmptyState
                  title={t("React_ConfigurationProfiles_NoMatchesTitle")}
                  action={
                    <ActionButton variant="ghost" onClick={clearFilters}>
                      {t("Server_Fleet_NoMatch_Clear")}
                    </ActionButton>
                  }
                >
                  {t("React_ConfigurationProfiles_NoMatchesDescription")}
                </EmptyState>
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
                          {t("Hosting_ConfigurationProfiles_Revision")}{" "}
                          {profile.latestRevisionNumber}
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
      </PageStack>
    </div>
  );
}
