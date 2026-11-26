import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { ApiError } from "@/api/client";
import { useDocumentTitle } from "@/app/documentTitle";
import { currentLocale, formatCurrency, formatDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinLedger } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppRouteToast } from "@/components/AppToasts";
import { TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { financeTones, toneOf } from "@/components/statusTones";
import {
  dateFilterInstant,
  kassenbuchQueryKeys,
  loadKassenbuchPage,
  type KassenbuchFilters,
} from "@/features/admin/vereinKassenbuch";
import "@/features/admin/vereinKassenbuch.css";

interface FilterForm {
  search: string;
  category: string;
  from: string;
  to: string;
}

const emptyFilters: FilterForm = { search: "", category: "", from: "", to: "" };

function queryFilters(filters: FilterForm): KassenbuchFilters {
  const from = dateFilterInstant(filters.from, false);
  const to = dateFilterInstant(filters.to, true);
  const search = filters.search.trim();
  const category = filters.category.trim();
  return {
    skip: 0,
    take: 100,
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
    ...(search ? { search } : {}),
    ...(category ? { category } : {}),
  };
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const location = useLocation();
  const authorized = canManageVereinLedger(user);
  const [formFilters, setFormFilters] = useState<FilterForm>(emptyFilters);
  const [filters, setFilters] = useState<FilterForm>(emptyFilters);
  const requestFilters = queryFilters(filters);
  const query = useQuery({
    queryKey: kassenbuchQueryKeys.list(requestFilters),
    queryFn: ({ signal }) => loadKassenbuchPage(requestFilters, signal),
    enabled: authorized,
  });
  useDocumentTitle(t("Pages_Command_Verein_KassenbuchPageTitle"));
  const savedState = location.state as {
    kassenbuchSaved?: boolean;
    kassenbuchDeleted?: boolean;
  } | null;
  useAppRouteToast(
    savedState?.kassenbuchSaved
      ? t("Pages_Command_Verein_KassenbuchToastSaved")
      : savedState?.kassenbuchDeleted
        ? t("Pages_Command_Verein_KassenbuchToastDeleted")
        : undefined,
  );

  if (!authorized)
    return (
      <PageStack>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_VereinKassenbuch")}
          title={t("Pages_Command_Verein_KassenbuchPageTitle")}
        />
        <ForbiddenState title={t("Pages_Command_Verein_KassenbuchAccessRequiredTitle")}>
          {t("Pages_Command_Verein_KassenbuchAccessRequiredDescription")}
        </ForbiddenState>
      </PageStack>
    );

  const data = query.data;
  const count = data?.totalCount ?? 0;
  const countLabel = `${count.toLocaleString(currentLocale())} ${t(
    count === 1
      ? "Pages_Command_Verein_Count_Singular_Row"
      : "Pages_Command_Verein_Count_Plural_Row",
  )}`;
  const forbidden =
    query.isError &&
    query.error instanceof ApiError &&
    (query.error.code === "unauthorized" || query.error.code === "forbidden");
  return (
    <PageStack>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinKassenbuch")}
        title={t("Pages_Command_Verein_KassenbuchPageTitle")}
        description={t("Pages_Command_Verein_KassenbuchPageDescription")}
        actions={
          <>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Verein_ActionRefresh")}
            </ActionButton>
            <ActionLink variant="ghost" to="/verein/kassenbuch">
              {t("Pages_Command_Verein_KassenbuchMemberView")}
            </ActionLink>
            <ActionLink variant="gold" to="/command/verein/kassenbuch/create">
              {t("Pages_Command_Verein_LedgerAddEntry")}
            </ActionLink>
          </>
        }
      />

      {forbidden ? (
        <ForbiddenState title={t("Pages_Command_Verein_KassenbuchAccessRequiredTitle")}>
          {t("Pages_Command_Verein_KassenbuchAccessRequiredDescription")}
        </ForbiddenState>
      ) : query.isError ? (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
        </ErrorState>
      ) : null}

      <section
        className="command-metrics verein-kassenbuch-metrics"
        aria-label={t("Pages_Command_Verein_KassenbuchMetricsAria")}
      >
        <Metric
          label={t("Pages_Command_Verein_MetricBalanceStatus")}
          value={formatCurrency(data?.balance ?? 0, data?.currency ?? "EUR")}
          detail={formatResource(t("Pages_Command_Verein_MetricBalanceDescription"), count)}
        />
        <Metric
          label={t("Pages_Command_Verein_MetricIncomeStatus")}
          value={formatCurrency(data?.incomeTotal ?? 0, data?.currency ?? "EUR")}
          detail={t("Pages_Command_Verein_MetricIncomeDescription")}
        />
        <Metric
          label={t("Pages_Command_Verein_MetricExpenseStatus")}
          value={formatCurrency(data?.expenseTotal ?? 0, data?.currency ?? "EUR")}
          detail={t("Pages_Command_Verein_MetricExpenseDescription")}
        />
        <Metric
          label={t("Pages_Command_Verein_KassenbuchMetricRowsStatus")}
          value={countLabel}
          detail={t("Pages_Command_Verein_KassenbuchMetricRowsDescription")}
        />
      </section>

      <form
        className="toolbar verein-kassenbuch-toolbar"
        aria-label={t("Pages_Command_Verein_KassenbuchToolbarAria")}
        onSubmit={(event) => {
          event.preventDefault();
          setFilters(formFilters);
        }}
      >
        <SearchField
          label={t("Pages_Command_Verein_KassenbuchSearchPlaceholder")}
          placeholder={t("Pages_Command_Verein_KassenbuchSearchPlaceholder")}
          clearLabel={t("Common_SearchClear")}
          value={formFilters.search}
          onChange={(search) => setFormFilters((current) => ({ ...current, search }))}
        />
        <TextField
          label={t("Pages_Command_Verein_LedgerFormCategory")}
          value={formFilters.category}
          maxLength={96}
          onChange={(event) =>
            setFormFilters((current) => ({ ...current, category: event.target.value }))
          }
        />
        <TextField
          label={t("Pages_Command_Verein_KassenbuchFilterFrom")}
          type="date"
          value={formFilters.from}
          onChange={(event) =>
            setFormFilters((current) => ({ ...current, from: event.target.value }))
          }
        />
        <TextField
          label={t("Pages_Command_Verein_KassenbuchFilterTo")}
          type="date"
          value={formFilters.to}
          onChange={(event) =>
            setFormFilters((current) => ({ ...current, to: event.target.value }))
          }
        />
        <ActionButton variant="ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_Verein_KassenbuchApplyFilters")}
        </ActionButton>
        <ActionButton
          variant="ghost"
          disabled={query.isFetching}
          onClick={() => {
            setFormFilters(emptyFilters);
            setFilters(emptyFilters);
          }}
        >
          {t("Pages_Command_Verein_KassenbuchClearFilters")}
        </ActionButton>
      </form>

      {query.isPending ? (
        <LoadingState label={t("Pages_Command_Verein_Loading")} />
      ) : data ? (
        <section className="command-panel verein-kassenbuch-list">
          <header className="command-panel__header verein-kassenbuch-list__header">
            <div>
              <span className="kicker">{t("Pages_Command_Verein_KassenbuchKicker")}</span>
              <h2>{t("Pages_Command_Verein_KassenbuchTitle")}</h2>
            </div>
            <StatusBadge>{countLabel}</StatusBadge>
          </header>
          {data.entries.length === 0 ? (
            <EmptyState title={t("Pages_Command_Verein_KassenbuchEmptyTitle")}>
              {t("Pages_Command_Verein_KassenbuchEmptyBody")}
            </EmptyState>
          ) : (
            <div className="command-table-scroll">
              <table className="command-table verein-kassenbuch-table">
                <thead>
                  <tr>
                    <th>{t("Pages_Command_Verein_KassenbuchHeaderDate")}</th>
                    <th>{t("Pages_Command_Verein_KassenbuchHeaderCategory")}</th>
                    <th>{t("Pages_Command_Verein_KassenbuchHeaderDescription")}</th>
                    <th>{t("Pages_Command_Verein_KassenbuchHeaderReference")}</th>
                    <th>{t("Pages_Command_Verein_KassenbuchHeaderAmount")}</th>
                    <th>{t("Pages_Command_Verein_KassenbuchHeaderActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.entries.map((entry) => (
                    <tr key={entry.primaryKey}>
                      <td>{formatDate(entry.timeStampBooked)}</td>
                      <td>{entry.category}</td>
                      <td>{entry.description}</td>
                      <td>{entry.documentNumber || entry.reference || "—"}</td>
                      <td>
                        <StatusBadge
                          tone={toneOf(
                            financeTones,
                            entry.direction === "Expense" ? "expense" : "income",
                          )}
                        >
                          {formatCurrency(
                            entry.direction === "Expense" ? -entry.amount : entry.amount,
                            entry.currency,
                          )}
                        </StatusBadge>
                      </td>
                      <td>
                        <ActionLink
                          variant="ghost"
                          to={`/command/verein/kassenbuch/${entry.primaryKey}/edit`}
                        >
                          {t("Pages_Command_Verein_KassenbuchActionEdit")}
                        </ActionLink>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : null}
    </PageStack>
  );
}
