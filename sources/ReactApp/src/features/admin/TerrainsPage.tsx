import { useQuery } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canCreateTerrains, canManageTerrains } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  formatTerrainTitle,
  loadTerrainWindow,
  terrainHasImage,
  terrainMatchesImageFilter,
  terrainQueryKeys,
} from "@/features/admin/terrains";

const windowSize = 80;
type TerrainFilter = "all" | "missing-image";

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
      <StatePanel kind="forbidden" title={t("Pages_Data_Terrains_All_AccessDeniedTitle")}>
        {t("Pages_Data_Terrains_All_AccessDeniedBody")}
      </StatePanel>
    );

  return (
    <section className="terrains-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Terrains")}
        title={t("Pages_Terrains_AllTerrains")}
        description={t("Pages_Data_Terrains_All_PageDescription")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Data_Terrains_All_ActionRefresh")}
            </button>
            {canCreate && (
              <Link className="button button--primary" to="/command/terrains/create">
                {t("Pages_Data_Terrains_All_ActionCreate")}
              </Link>
            )}
          </>
        }
      />

      <section
        className="command-metrics terrain-metrics"
        aria-label={t("Pages_Data_Terrains_All_MetricsAria")}
      >
        <button
          type="button"
          className={filter === "all" ? "active" : undefined}
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
        </button>
        <div>
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
        <button
          type="button"
          className={filter === "missing-image" ? "active" : undefined}
          aria-pressed={filter === "missing-image"}
          onClick={() => setFilter("missing-image")}
        >
          <span>{t("Pages_Data_Terrains_All_StatusImagery")}</span>
          <strong>
            {formatResource(t("Pages_Data_Terrains_All_ValueMissingImageCount"), missingImageCount)}
          </strong>
          <small>{t("Pages_Data_Terrains_All_MissingImageMetricDescription")}</small>
        </button>
      </section>

      <form
        className="toolbar terrain-toolbar"
        aria-label={t("Pages_Data_Terrains_All_FilterBarAria")}
        onSubmit={applySearch}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Data_Terrains_All_SearchPlaceholder")}</span>
          <input
            value={searchDraft}
            placeholder={t("Pages_Data_Terrains_All_SearchPlaceholder")}
            onChange={(event) => setSearchDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit" disabled={query.isFetching}>
          {t("Search")}
        </button>
        <div className="filter-chips">
          <button
            type="button"
            className={filter === "all" ? "active" : undefined}
            aria-pressed={filter === "all"}
            onClick={() => setFilter("all")}
          >
            {t("Pages_Data_Terrains_All_FilterAll")}
          </button>
          <button
            type="button"
            className={filter === "missing-image" ? "active" : undefined}
            aria-pressed={filter === "missing-image"}
            onClick={() => setFilter("missing-image")}
          >
            {t("Pages_Data_Terrains_All_FilterMissingImage")}
          </button>
        </div>
      </form>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Data_Terrains_All_LoadingState")}</StatePanel>
      ) : query.isError ? (
        <StatePanel
          kind={apiDenied ? "forbidden" : "error"}
          {...(apiDenied ? { title: t("Pages_Data_Terrains_All_AccessDeniedTitle") } : {})}
          action={
            apiDenied ? undefined : (
              <button
                className="button button--ghost"
                type="button"
                onClick={() => query.refetch()}
              >
                {t("React_UI_Retry")}
              </button>
            )
          }
        >
          {apiDenied
            ? t("Pages_Data_Terrains_All_AccessDeniedBody")
            : t("Auto_TerrainDataCouldNotBeLoadedCheckTheAPI_ff13b797")}
        </StatePanel>
      ) : (
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
              {formatResource(t("Pages_Data_Terrains_All_VisibleCount"), visibleTerrains.length)}
            </StatusBadge>
          </header>
          {visibleTerrains.length === 0 ? (
            <StatePanel kind="empty">{t("Pages_Data_Terrains_All_NoTerrainsMatch")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table terrain-table">
                <caption className="sr-only">{t("Pages_Terrains_AllTerrains")}</caption>
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
                  {visibleTerrains.map((terrain) => (
                    <tr key={terrain.primaryKey}>
                      <td>
                        <div
                          className={`terrain-thumbnail${terrainHasImage(terrain) ? "" : " is-empty"}`}
                        >
                          {terrainHasImage(terrain) ? (
                            <img
                              src={resolveImageUrl(terrain.imagePath)}
                              alt={`${formatTerrainTitle(terrain, t("Auto_UntitledTerrain_6aba91cc"))}: ${t("Pages_Data_Terrains_All_StatusImagery")}`}
                              loading="lazy"
                            />
                          ) : (
                            <span>{t("Pages_Data_Terrains_All_NoImage")}</span>
                          )}
                        </div>
                      </td>
                      <th scope="row">
                        <strong>
                          {formatTerrainTitle(terrain, t("Auto_UntitledTerrain_6aba91cc"))}
                        </strong>
                        <span>{t("Pages_Data_Terrains_All_SubtitleTerrainRecord")}</span>
                      </th>
                      <td>{terrain.gameIdentifier || "—"}</td>
                      <td>{terrain.primaryKey}</td>
                      <td>
                        <Link
                          className="button button--ghost button--small"
                          to={`/command/terrains/${terrain.primaryKey}`}
                        >
                          {t("Open")}
                        </Link>
                      </td>
                    </tr>
                  ))}
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
                query.data.totalCount,
              )}
            </span>
            <button
              className="button button--ghost"
              type="button"
              disabled={!canLoadMore || query.isFetching}
              onClick={() => setTake((value) => value + windowSize)}
            >
              {query.isFetching ? t("Pages_Common_LoadingMore") : t("Pages_Common_LoadMore")}
            </button>
          </footer>
        </section>
      )}
    </section>
  );
}
