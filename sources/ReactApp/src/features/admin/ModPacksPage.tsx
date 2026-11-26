import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageModPacks } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
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
type ModPackOwnership = "all-owners" | "my-mod-packs";

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

/**
 * The mod pack administration list: a metric strip, a search and filter toolbar and a paged ledger of mod packs.
 *
 * The page header title is the navigation label of the page. Loading, forbidden and error states render below the
 * same header; stale rows stay visible under a failed refetch.
 */
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
  const selectOwnership = (value: ModPackOwnership) => {
    const next = value === "my-mod-packs";
    if (next === myModPacksOnly) return;
    resetWindow();
    setMyModPacksOnly(next);
  };

  if (!authorized)
    return (
      <section>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_ModPacks")}
          title={t("Shared_CommandCenter_Nav_ModPacks")}
        />
        <PageStack>
          <ForbiddenState title={t("Auto_ModPackAdministratorAccessRequired_95eb8250")}>
            {t("Auto_ModPackControlIsVisibleOnlyToAuthorizedAsset_16976818")}
          </ForbiddenState>
        </PageStack>
      </section>
    );

  const filterChips: FilterChipItem<ModPackFilter>[] = (
    Object.keys(filterLabels) as ModPackFilter[]
  ).map((id) => ({ id, label: t(filterLabels[id]), count: counts[id] }));
  const ownershipChips: FilterChipItem<ModPackOwnership>[] = [
    { id: "all-owners", label: t("Auto_AllOwners_5f198db2") },
    { id: "my-mod-packs", label: t("Auto_MyModPacks_a70e2739") },
  ];
  const failedWithoutData = query.isError && !query.data;

  return (
    <section>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_ModPacks")}
        title={t("Shared_CommandCenter_Nav_ModPacks")}
        description={t("ModPacks_LedgerDescription")}
        actions={
          <>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Common_Refresh")}
            </ActionButton>
            <ActionLink variant="gold" to="/command/mod-packs/create">
              {t("Auto_CreateModPack_04e0630f")}
            </ActionLink>
          </>
        }
      />

      <PageStack>
        {query.isError && (
          <ErrorState onRetry={() => void query.refetch()}>
            {t("Pages_Data_ModPacks_All_LoadError")}
          </ErrorState>
        )}

        <section
          className="command-metrics modpacks-metrics"
          aria-label={t("Auto_ModPackSummary_4a23e0b2")}
        >
          <ActionButton
            variant="ghost"
            className="modpacks-metric"
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
          </ActionButton>
          <ActionButton
            variant="ghost"
            className="modpacks-metric"
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
          </ActionButton>
        </section>

        <section
          className="toolbar modpacks-toolbar"
          aria-label={t("Auto_ModPackFilters_52e85bf3")}
        >
          <SearchField
            label={t("Auto_SearchModPacks_5fce5f83")}
            placeholder={t("Auto_SearchModPacks_5fce5f83")}
            clearLabel={t("Common_SearchClear")}
            value={search}
            onChange={(value) => {
              resetWindow();
              setSearch(value);
            }}
          />
          <FilterChipGroup<ModPackFilter>
            label={t("Auto_ModPackFilters_52e85bf3")}
            items={filterChips}
            value={filter}
            onChange={setFilter}
          />
          <FilterChipGroup<ModPackOwnership>
            label={t("Auto_AllOwners_5f198db2")}
            items={ownershipChips}
            value={myModPacksOnly ? "my-mod-packs" : "all-owners"}
            onChange={selectOwnership}
          />
        </section>

        {query.isPending ? (
          <LoadingState label={t("Auto_LoadingModPackControl_4b9cc997")} />
        ) : (
          !failedWithoutData && (
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
                <EmptyState title={t("Pages_ModPacks_EmptyTitle")}>
                  {t("Pages_ModPacks_EmptyBody")}
                </EmptyState>
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
                        const name = title(definition, t("Auto_UntitledModPack_68e62d9a"));
                        return (
                          <tr
                            key={id ?? `${definition.title}-${definition.timeStampCreated}`}
                            className={id == null ? undefined : "command-table__interactive"}
                            tabIndex={id == null ? undefined : 0}
                            onClick={() => id != null && navigate(`/command/mod-packs/${id}`)}
                            onKeyDown={(event) => {
                              if (id == null || (event.key !== "Enter" && event.key !== " "))
                                return;
                              event.preventDefault();
                              navigate(`/command/mod-packs/${id}`);
                            }}
                          >
                            <td>
                              <strong>{name}</strong>
                              <span>{id ?? "—"}</span>
                            </td>
                            <td>
                              <StatusBadge tone="info">{definition.gameIdentifier}</StatusBadge>
                            </td>
                            <td>
                              <strong>{definition.owner?.nickname || "—"}</strong>
                              <span>
                                {(definition.isActive ?? true) ? t("Active") : t("Inactive")}
                              </span>
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
                              {formatDateTime(
                                latest?.timeStampCreated ?? definition.timeStampCreated,
                              )}
                            </td>
                            <td>
                              {id != null && (
                                <ActionLink
                                  variant="ghost"
                                  to={`/command/mod-packs/${id}`}
                                  aria-label={`${t("Open")} ${name}`}
                                  onClick={(event) => event.stopPropagation()}
                                >
                                  {t("Open")}
                                </ActionLink>
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
                    : formatResource(
                        t("Pages_Data_ModPacks_All_WindowLoadedAll"),
                        definitions.length,
                      )}
                </span>
                <ActionButton
                  variant="ghost"
                  disabled={
                    !query.data || query.data.count <= definitions.length || query.isFetching
                  }
                  onClick={() => setTake((current) => current + windowSize)}
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
