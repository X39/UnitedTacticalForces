import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canCreateTerrains, canManageTerrains } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CommandListToolbar } from "@/components/CommandListToolbar";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { PageStack } from "@/components/PageStack";
import { PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import {
  formatTerrainTitle,
  loadTerrainWindow,
  terrainHasImage,
  terrainMatchesImageFilter,
  terrainQueryKeys,
} from "@/features/admin/terrains";
import "@/features/admin/terrains.css";

const windowSize = 80;
type TerrainFilter = "all" | "missing-image";

/**
 * The terrain administration list: a metric strip, a search and filter toolbar and a paged ledger of terrains.
 *
 * The page header title is the navigation label of the page. Forbidden, loading and error states render below
 * the same header, and stale rows stay visible under a failed refetch.
 */
export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const canManage = canManageTerrains(user);
  const canCreate = canCreateTerrains(user);
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [take, setTake] = useState(windowSize);
  const [filter, setFilter] = useState<TerrainFilter>("all");
  const query = useQuery({
    queryKey: terrainQueryKeys.list(search, take),
    queryFn: ({ signal }) => loadTerrainWindow(search, take, signal),
    enabled: canManage,
  });
  const terrains = useMemo(() => query.data?.terrains ?? [], [query.data?.terrains]);
  const visibleTerrains = useMemo(
    () =>
      terrains
        .filter((terrain) => terrainMatchesImageFilter(terrain, filter))
        .sort((left, right) => left.title.localeCompare(right.title)),
    [filter, terrains],
  );
  const missingImageCount = terrains.filter((terrain) => !terrainHasImage(terrain)).length;
  const readyImageCount = terrains.length - missingImageCount;
  const canLoadMore = (query.data?.totalCount ?? 0) > terrains.length;
  const apiDenied =
    query.error instanceof ApiError &&
    (query.error.code === "unauthorized" || query.error.code === "forbidden");
  const applySearch = (event: FormEvent) => {
    event.preventDefault();
    setTake(windowSize);
    setSearch(searchDraft.trim());
  };

  if (!canManage)
    return (
      <section>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_Terrains")}
          title={t("Shared_CommandCenter_Nav_Terrains")}
        />
        <PageStack>
          <ForbiddenState title={t("Pages_Data_Terrains_All_AccessDeniedTitle")}>
            {t("Pages_Data_Terrains_All_AccessDeniedBody")}
          </ForbiddenState>
        </PageStack>
      </section>
    );

  const filterChips: FilterChipItem<TerrainFilter>[] = [
    { id: "all", label: t("Pages_Data_Terrains_All_FilterAll") },
    { id: "missing-image", label: t("Pages_Data_Terrains_All_FilterMissingImage") },
  ];
  const failedWithoutData = query.isError && !query.data;

  return (
    <section>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Terrains")}
        title={t("Shared_CommandCenter_Nav_Terrains")}
        description={t("Pages_Data_Terrains_All_PageDescription")}
        actions={
          <>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Data_Terrains_All_ActionRefresh")}
            </ActionButton>
            {canCreate && (
              <ActionLink variant="gold" to="/command/terrains/create">
                {t("Pages_Data_Terrains_All_ActionCreate")}
              </ActionLink>
            )}
          </>
        }
      />

      <PageStack>
        <section
          className="command-metrics terrain-metrics"
          aria-label={t("Pages_Data_Terrains_All_MetricsAria")}
        >
          <ActionButton
            variant="ghost"
            className="terrain-metric"
            aria-pressed={filter === "all"}
            onClick={() => setFilter("all")}
          >
            <span>{t("Pages_Data_Terrains_All_StatusTerrains")}</span>
            <strong>
              {query.data
                ? formatResource(t("Pages_Data_Terrains_All_CountLoaded"), query.data.totalCount)
                : t("Pages_Data_Terrains_All_CountLoading")}
            </strong>
            <small>
              {formatResource(t("Pages_Data_Terrains_All_MetricAllDescription"), terrains.length)}
            </small>
          </ActionButton>
          <div className="terrain-metric">
            <span>{t("Pages_Data_Terrains_All_StatusImagery")}</span>
            <strong>
              {formatResource(t("Pages_Data_Terrains_All_ValueReadyImageCount"), readyImageCount)}
            </strong>
            <small>
              {formatResource(
                t("Pages_Data_Terrains_All_ReadyImageMetricDescription"),
                readyImageCount,
              )}
            </small>
          </div>
          <ActionButton
            variant="ghost"
            className="terrain-metric"
            aria-pressed={filter === "missing-image"}
            onClick={() => setFilter("missing-image")}
          >
            <span>{t("Pages_Data_Terrains_All_StatusImagery")}</span>
            <strong>
              {formatResource(
                t("Pages_Data_Terrains_All_ValueMissingImageCount"),
                missingImageCount,
              )}
            </strong>
            <small>{t("Pages_Data_Terrains_All_MissingImageMetricDescription")}</small>
          </ActionButton>
        </section>

        <CommandListToolbar
          className="terrain-toolbar"
          ariaLabel={t("Pages_Data_Terrains_All_FilterBarAria")}
          onSubmit={applySearch}
          searchLabel={t("Pages_Data_Terrains_All_SearchPlaceholder")}
          searchValue={searchDraft}
          onSearchChange={setSearchDraft}
          submitLabel={t("Search")}
          submitDisabled={query.isFetching}
        >
          <FilterChipGroup<TerrainFilter>
            label={t("Pages_Data_Terrains_All_FilterBarAria")}
            items={filterChips}
            value={filter}
            onChange={setFilter}
          />
        </CommandListToolbar>

        {query.isError &&
          (apiDenied ? (
            <ForbiddenState title={t("Pages_Data_Terrains_All_AccessDeniedTitle")}>
              {t("Pages_Data_Terrains_All_AccessDeniedBody")}
            </ForbiddenState>
          ) : (
            <ErrorState onRetry={() => void query.refetch()}>
              {t("Auto_TerrainDataCouldNotBeLoadedCheckTheAPI_ff13b797")}
            </ErrorState>
          ))}
        {query.isPending ? (
          <LoadingState label={t("Pages_Data_Terrains_All_LoadingState")} />
        ) : (
          !failedWithoutData && (
            <section className="command-panel terrain-ledger">
              <header className="command-panel__header terrain-ledger__header">
                <div>
                  <span className="kicker">{t("Pages_Data_Terrains_All_Kicker")}</span>
                  <h2>
                    {filter === "missing-image"
                      ? t("Pages_Data_Terrains_All_TitleMissingImage")
                      : t("Pages_Data_Terrains_All_TitleAll")}
                  </h2>
                </div>
                <StatusBadge>
                  {formatResource(
                    t("Pages_Data_Terrains_All_VisibleCount"),
                    visibleTerrains.length,
                  )}
                </StatusBadge>
              </header>
              {visibleTerrains.length === 0 ? (
                <EmptyState title={t("Pages_Data_Terrains_All_EmptyTitle")}>
                  {t("Pages_Data_Terrains_All_EmptyBody")}
                </EmptyState>
              ) : (
                <div className="command-table-scroll">
                  <table className="command-table terrain-table">
                    <caption className="sr-only">{t("Shared_CommandCenter_Nav_Terrains")}</caption>
                    <thead>
                      <tr>
                        <th scope="col">{t("Pages_Data_Terrains_All_TableHeaderPreview")}</th>
                        <th scope="col">{t("Pages_Data_Terrains_All_TableHeaderTerrain")}</th>
                        <th scope="col">{t("Hosting_Create_Game")}</th>
                        <th scope="col">{t("Pages_Data_Terrains_All_TableHeaderKey")}</th>
                        <th scope="col">{t("Pages_Data_Terrains_All_TableHeaderActions")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleTerrains.map((terrain) => {
                        const name = formatTerrainTitle(
                          terrain,
                          t("Auto_UntitledTerrain_6aba91cc"),
                        );
                        return (
                          <tr key={terrain.primaryKey}>
                            <td>
                              <div
                                className={`terrain-thumbnail${terrainHasImage(terrain) ? "" : " is-empty"}`}
                              >
                                {terrainHasImage(terrain) ? (
                                  <img
                                    src={resolveImageUrl(terrain.imagePath)}
                                    alt={`${name}: ${t("Pages_Data_Terrains_All_StatusImagery")}`}
                                    loading="lazy"
                                  />
                                ) : (
                                  <span>{t("Pages_Data_Terrains_All_NoImage")}</span>
                                )}
                              </div>
                            </td>
                            <th scope="row">
                              <strong>{name}</strong>
                              <span>{t("Pages_Data_Terrains_All_SubtitleTerrainRecord")}</span>
                            </th>
                            <td>{terrain.gameIdentifier || "—"}</td>
                            <td>{terrain.primaryKey}</td>
                            <td>
                              <ActionLink
                                variant="ghost"
                                size="compact"
                                to={`/command/terrains/${terrain.primaryKey}`}
                                aria-label={`${t("Open")} ${name}`}
                              >
                                {t("Open")}
                              </ActionLink>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
              <footer className="terrain-ledger__footer">
                <span>
                  {formatResource(
                    canLoadMore
                      ? t("Pages_Data_Terrains_All_WindowLoadedPartial")
                      : t("Pages_Data_Terrains_All_WindowLoadedAll"),
                    terrains.length,
                    query.data?.totalCount ?? terrains.length,
                  )}
                </span>
                <ActionButton
                  variant="ghost"
                  disabled={!canLoadMore || query.isFetching}
                  onClick={() => setTake((value) => value + windowSize)}
                >
                  {query.isFetching ? t("Pages_Common_LoadingMore") : t("Pages_Common_LoadMore")}
                </ActionButton>
              </footer>
            </section>
          )
        )}
      </PageStack>
    </section>
  );
}
