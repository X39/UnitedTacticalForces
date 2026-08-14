import { ArrowLeft20Regular, ArrowSync20Regular, Money24Regular } from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import { type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatCurrency, formatDate } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { Metric, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { canReadVereinLedger } from "./memberAccess";
import { eventsMemberQueryKeys, eventsMemberService } from "./services";
import type { KassenbuchEntry } from "./types";

function signedAmount(entry: KassenbuchEntry): number {
  return entry.direction === "Expense" ? -entry.amount : entry.amount;
}

export function VereinLedgerPage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const canRead = canReadVereinLedger(auth.user);
  const heroImage = useSiteImage(
    "kassenbuch-hero",
    "/media/generated/verein-finance-ledger-hero.jpg",
  );
  const query = useQuery({
    queryKey: eventsMemberQueryKeys.vereinLedger,
    queryFn: ({ signal }) => eventsMemberService.vereinLedger(signal),
    enabled: canRead,
    retry: false,
  });
  useDocumentTitle(t("Pages_Unit_Kassenbuch_Index_PageTitle"));

  if (auth.isLoading) {
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Pages_Unit_Kassenbuch_Index_Loading")}</StatePanel>
      </div>
    );
  }

  if (!auth.isAuthenticated) {
    const provider = auth.providers.steam ? "steam" : auth.providers.discord ? "discord" : null;
    return (
      <div className="narrow-page">
        <StatePanel
          kind="forbidden"
          title={t("Pages_Unit_Kassenbuch_Index_SignInRequiredTitle")}
          action={
            provider ? (
              <button
                className="button button--gold"
                type="button"
                onClick={() => auth.login(provider)}
              >
                {t("Pages_Unit_Kassenbuch_Index_SignIn")}
              </button>
            ) : undefined
          }
        >
          {t("Pages_Unit_Kassenbuch_Index_SignInRequiredDescription")}
        </StatePanel>
      </div>
    );
  }

  if (!canRead) {
    return (
      <div className="narrow-page">
        <StatePanel
          kind="forbidden"
          title={t("Pages_Unit_Kassenbuch_Index_MembershipRequiredTitle")}
        >
          {t("Pages_Unit_Kassenbuch_Index_MembershipRequiredDescription")}
        </StatePanel>
      </div>
    );
  }

  return (
    <div
      className="page-wrap em-ledger-page"
      style={{ "--em-ledger-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
    >
      <section className="em-ledger-hero">
        <PageHeader
          kicker={t("Pages_Unit_Kassenbuch_Index_Kicker")}
          title={t("Pages_Unit_Kassenbuch_Index_Title")}
          description={t("Pages_Unit_Kassenbuch_Index_Intro")}
          actions={
            <Link className="button button--glass" to="/verein">
              <ArrowLeft20Regular /> {t("Pages_Unit_Kassenbuch_Index_BackToUnit")}
            </Link>
          }
        />
      </section>

      {query.isLoading && (
        <StatePanel kind="loading">{t("Pages_Unit_Kassenbuch_Index_Loading")}</StatePanel>
      )}
      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void query.refetch()}
            >
              <ArrowSync20Regular /> {t("React_UI_Retry")}
            </button>
          }
        >
          {t("Pages_Unit_Kassenbuch_Index_Error_LoadFailed")}
        </StatePanel>
      )}
      {query.data && (
        <>
          <section
            className="metrics-grid em-ledger-metrics"
            aria-label={t("Pages_Unit_Kassenbuch_Index_TotalsAria")}
          >
            <Metric
              label={t("Pages_Unit_Kassenbuch_Index_Income")}
              value={formatCurrency(query.data.incomeTotal, query.data.currency)}
              detail={formatResource(
                t("Pages_Unit_Kassenbuch_Index_BookingsDescription"),
                query.data.totalCount,
              )}
            />
            <Metric
              label={t("Pages_Unit_Kassenbuch_Index_Expense")}
              value={formatCurrency(query.data.expenseTotal, query.data.currency)}
              detail={t("Pages_Unit_Kassenbuch_Index_ExpensesDescription")}
            />
            <Metric
              label={t("Pages_Unit_Kassenbuch_Index_Balance")}
              value={formatCurrency(query.data.balance, query.data.currency)}
              detail={t("Pages_Unit_Kassenbuch_Index_BalanceDescription")}
            />
          </section>

          {query.data.entries.length === 0 ? (
            <StatePanel kind="empty" title={t("Pages_Unit_Kassenbuch_Index_NoEntriesTitle")}>
              {t("Pages_Unit_Kassenbuch_Index_NoEntriesDescription")}
            </StatePanel>
          ) : (
            <section className="content-section em-panel em-ledger-panel">
              <PageHeader
                kicker={t("Pages_Unit_Kassenbuch_Index_LedgerKicker")}
                title={t("Pages_Unit_Kassenbuch_Index_LedgerTitle")}
                actions={<Money24Regular />}
              />
              <div className="em-table-scroll">
                <table>
                  <thead>
                    <tr>
                      <th>{t("Pages_Unit_Kassenbuch_Index_TableDate")}</th>
                      <th>{t("Pages_Unit_Kassenbuch_Index_TableCategory")}</th>
                      <th>{t("Pages_Unit_Kassenbuch_Index_TableDescription")}</th>
                      <th>{t("Pages_Unit_Kassenbuch_Index_TableReference")}</th>
                      <th>{t("Pages_Unit_Kassenbuch_Index_TableAmount")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {query.data.entries.map((entry) => (
                      <tr key={entry.primaryKey}>
                        <td>{formatDate(entry.timeStampBooked)}</td>
                        <td>
                          <StatusBadge tone={entry.direction === "Expense" ? "danger" : "success"}>
                            {entry.category}
                          </StatusBadge>
                        </td>
                        <td>{entry.description}</td>
                        <td>{entry.documentNumber || entry.reference || "—"}</td>
                        <td>{formatCurrency(signedAmount(entry), entry.currency)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </>
      )}
    </div>
  );
}
