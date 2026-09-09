import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageModPacks } from "@/auth/claims";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import {
  latestActiveRevision,
  modPackMatchesFilter,
  modPackQueryKeys,
  modPackService,
  type FullModPackDefinition,
} from "@/features/admin/modPacks";
import "@/features/admin/modPacks.css";

const windowSize = 80;
type ModPackFilter = "all" | "no-revisions";

const filterLabels: Record<ModPackFilter, TranslationKey> = {
  all: "All",
  "no-revisions": "Auto_MissingRevisions_fd474e59",
};

const listTitles: Record<ModPackFilter, TranslationKey> = {
  all: "Pages_Data_ModPacks_All_TitleAll",
  "no-revisions": "Pages_Data_ModPacks_All_TitleNoRevisions",
};

function title(definition: FullModPackDefinition, fallback: string): string {
  return definition.title?.trim() || fallback;
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const authorized = canManageModPacks(user);
  const [filter, setFilter] = useState<ModPackFilter>("all");
  const [search, setSearch] = useState("");
  const [myModPacksOnly, setMyModPacksOnly] = useState(false);
  const [take, setTake] = useState(windowSize);
  const options = {
    skip: 0,
    take,
    myModPacksOnly,
    ...(search.trim() ? { search: search.trim() } : {}),
  };
  const query = useQuery({
    queryKey: modPackQueryKeys.list(options),
    queryFn: ({ signal }) => modPackService.snapshot(options, signal),
    enabled: authorized,
    placeholderData: keepPreviousData,
  });
  const definitions = useMemo(() => query.data?.definitions ?? [], [query.data?.definitions]);
  const counts = useMemo(
    () => ({
      all: definitions.length,
      "no-revisions": definitions.filter((definition) => definition.modPackRevisions.length === 0)
        .length,
    }),
    [definitions],
  );
  const visible = useMemo(
    () =>
      definitions
        .filter((definition) => modPackMatchesFilter(definition, filter))
        .sort(
          (left, right) =>
            Number(right.isActive ?? true) - Number(left.isActive ?? true) ||
            (left.title ?? "").localeCompare(right.title ?? ""),
        ),
    [definitions, filter],
  );
  const resetWindow = () => setTake(windowSize);
  const selectOwnership = (value: boolean) => {
    if (value === myModPacksOnly) return;
    resetWindow();
    setMyModPacksOnly(value);
  };

  if (!authorized)
    return (
      <StatePanel kind="forbidden" title={t("Auto_ModPackAdministratorAccessRequired_95eb8250")}>
        {t("Auto_ModPackControlIsVisibleOnlyToAuthorizedAsset_16976818")}
      </StatePanel>
    );

  return (
    <section className="modpacks-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_ModPacks")}
        title={t("Pages_ModPacks_AllModPacks")}
        description={t("ModPacks_LedgerDescription")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Common_Refresh")}
            </button>
            <Link className="button button--primary" to="/command/mod-packs/create">
              {t("Auto_CreateModPack_04e0630f")}
            </Link>
          </>
        }
      />

      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void query.refetch()}
            >
              {t("Common_Refresh")}
            </button>
          }
        >
          {t("Pages_Data_ModPacks_All_LoadError")}
        </StatePanel>
      )}

      <section
        className="command-metrics modpacks-metrics"
        aria-label={t("Auto_ModPackSummary_4a23e0b2")}
      >
        <button
          type="button"
          className={filter === "all" ? "active" : undefined}
          aria-pressed={filter === "all"}
          onClick={() => setFilter("all")}
        >
          <Metric
            label={t("Pages_Command_Assets_StatusModPacks")}
            value={
              query.data
                ? formatResource(t("Pages_Data_ModPacks_All_CountLoaded"), query.data.count)
                : t("Pages_Data_ModPacks_All_CountLoading")
            }
            detail={formatResource(t("Pages_Data_ModPacks_All_MetricActiveWindow"), counts.all)}
          />
        </button>
        <button
          type="button"
          className={filter === "no-revisions" ? "active" : undefined}
          aria-pressed={filter === "no-revisions"}
          onClick={() => setFilter("no-revisions")}
        >
          <Metric
            label={t("Revision")}
            value={formatResource(
              t("Pages_Data_ModPacks_All_ValueMissingRevisionCount"),
              counts["no-revisions"],
            )}
            detail={t("Auto_ReturnedPackagesMissingRevisionMetadata_d9a1fd57")}
          />
        </button>
      </section>

      <section className="toolbar modpacks-toolbar" aria-label={t("Auto_ModPackFilters_52e85bf3")}>
        <label className="search-field">
          <span className="sr-only">{t("Auto_SearchModPacks_5fce5f83")}</span>
          <input
            value={search}
            placeholder={t("Auto_SearchModPacks_5fce5f83")}
            onChange={(event) => {
              resetWindow();
              setSearch(event.target.value);
            }}
          />
        </label>
        <div className="filter-chips">
          {(Object.keys(filterLabels) as ModPackFilter[]).map((candidate) => (
            <button
              key={candidate}
              type="button"
              className={filter === candidate ? "active" : undefined}
              aria-pressed={filter === candidate}
              onClick={() => setFilter(candidate)}
            >
              {t(filterLabels[candidate])} <span>{counts[candidate]}</span>
            </button>
          ))}
          <button
            type="button"
            className={!myModPacksOnly ? "active" : undefined}
            aria-pressed={!myModPacksOnly}
            onClick={() => selectOwnership(false)}
          >
            {t("Auto_AllOwners_5f198db2")}
          </button>
          <button
            type="button"
            className={myModPacksOnly ? "active" : undefined}
            aria-pressed={myModPacksOnly}
            onClick={() => selectOwnership(true)}
          >
            {t("Auto_MyModPacks_a70e2739")}
          </button>
        </div>
      </section>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Auto_LoadingModPackControl_4b9cc997")}</StatePanel>
      ) : (
        <section className="command-panel modpacks-ledger">
          <header className="command-panel__header modpacks-ledger__header">
            <div>
              <span className="kicker">{t("Auto_PackageLedger_7b725d5e")}</span>
              <h2>{t(listTitles[filter])}</h2>
            </div>
            <StatusBadge>
              {formatResource(t("Pages_Data_ModPacks_All_VisibleCount"), visible.length)}
            </StatusBadge>
          </header>
          {visible.length === 0 ? (
            <StatePanel>{t("Auto_NoModPacksMatchTheCurrentFilters_33565b66")}</StatePanel>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table modpacks-table">
                <thead>
                  <tr>
                    <th>{t("Package")}</th>
                    <th>{t("React_Terrains_GameLabel")}</th>
                    <th>{t("Owner")}</th>
                    <th>{t("Revisions")}</th>
                    <th>{t("Updated")}</th>
                    <th>{t("Action")}</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((definition) => {
                    const latest = latestActiveRevision(definition);
                    const id = definition.primaryKey;
                    return (
                      <tr
                        key={id ?? `${definition.title}-${definition.timeStampCreated}`}
                        className={id == null ? undefined : "command-table__interactive"}
                        tabIndex={id == null ? undefined : 0}
                        onClick={() => id != null && navigate(`/command/mod-packs/${id}`)}
                        onKeyDown={(event) => {
                          if (id == null || (event.key !== "Enter" && event.key !== " ")) return;
                          event.preventDefault();
                          navigate(`/command/mod-packs/${id}`);
                        }}
                      >
                        <td>
                          <strong>{title(definition, t("Auto_UntitledModPack_68e62d9a"))}</strong>
                          <span>{id ?? "—"}</span>
                        </td>
                        <td>
                          <StatusBadge tone="info">{definition.gameIdentifier}</StatusBadge>
                        </td>
                        <td>
                          <strong>{definition.owner?.nickname || "—"}</strong>
                          <span>{(definition.isActive ?? true) ? t("Active") : t("Inactive")}</span>
                        </td>
                        <td>
                          <strong>
                            {formatResource(
                              t("Pages_Data_ModPacks_All_RevisionCount"),
                              definition.modPackRevisions.length,
                            )}
                          </strong>
                          <span>
                            {latest?.primaryKey == null
                              ? "—"
                              : formatResource(
                                  t("Pages_Command_Assets_LatestRevisionLabel"),
                                  latest.primaryKey,
                                )}
                          </span>
                        </td>
                        <td>
                          {formatDateTime(latest?.timeStampCreated ?? definition.timeStampCreated)}
                        </td>
                        <td>
                          {id != null && (
                            <Link
                              className="button button--ghost"
                              to={`/command/mod-packs/${id}`}
                              onClick={(event) => event.stopPropagation()}
                            >
                              {t("Open")}
                            </Link>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
          <footer className="modpacks-ledger__footer">
            <span>
              {query.data && query.data.count > definitions.length
                ? formatResource(
                    t("Pages_Data_ModPacks_All_WindowLoadedPartial"),
                    definitions.length,
                    query.data.count,
                  )
                : formatResource(t("Pages_Data_ModPacks_All_WindowLoadedAll"), definitions.length)}
            </span>
            <button
              className="button button--ghost"
              type="button"
              disabled={!query.data || query.data.count <= definitions.length || query.isFetching}
              onClick={() => setTake((current) => current + windowSize)}
            >
              {query.isFetching ? t("Pages_Common_LoadingMore") : t("Pages_Common_LoadMore")}
            </button>
          </footer>
        </section>
      )}
    </section>
  );
}
