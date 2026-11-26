import { ArrowLeft20Regular, Money24Regular } from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import { type CSSProperties, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatCurrency, formatDate } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { PageStack } from "@/components/PageStack";
import { Metric, PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
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

  // The one page h1 stays in place for every state; the shared state view or the ledger follows below it.
  const frame = (body: ReactNode) => (
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
            <ActionLink variant="glass" to="/verein">
              <ArrowLeft20Regular aria-hidden="true" />{" "}
              {t("Pages_Unit_Kassenbuch_Index_BackToUnit")}
            </ActionLink>
          }
        />
      </section>
      <PageStack>{body}</PageStack>
    </div>
  );

  if (auth.isLoading) {
    return frame(<LoadingState label={t("Pages_Unit_Kassenbuch_Index_Loading")} />);
  }

  if (!auth.isAuthenticated) {
    const provider = auth.providers.steam ? "steam" : auth.providers.discord ? "discord" : null;
    return frame(
      <ForbiddenState
        title={t("Pages_Unit_Kassenbuch_Index_SignInRequiredTitle")}
        action={
          provider && (
            <ActionButton variant="gold" onClick={() => auth.login(provider)}>
              {t("Pages_Unit_Kassenbuch_Index_SignIn")}
            </ActionButton>
          )
        }
      >
        <p>{t("Pages_Unit_Kassenbuch_Index_SignInRequiredDescription")}</p>
      </ForbiddenState>,
    );
  }

  if (!canRead) {
    return frame(
      <ForbiddenState title={t("Pages_Unit_Kassenbuch_Index_MembershipRequiredTitle")}>
        {t("Pages_Unit_Kassenbuch_Index_MembershipRequiredDescription")}
      </ForbiddenState>,
    );
  }

  return frame(
    <>
      {query.isLoading && <LoadingState label={t("Pages_Unit_Kassenbuch_Index_Loading")} />}
      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>
          {t("Pages_Unit_Kassenbuch_Index_Error_LoadFailed")}
        </ErrorState>
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
            <EmptyState
              title={t("Pages_Unit_Kassenbuch_Index_NoEntriesTitle")}
              action={
                <ActionButton variant="ghost" onClick={() => void query.refetch()}>
                  {t("Common_Refresh")}
                </ActionButton>
              }
            >
              {t("Pages_Unit_Kassenbuch_Index_NoEntriesDescription")}
            </EmptyState>
          ) : (
            <section className="content-section em-panel em-ledger-panel">
              <PageHeader
                headingLevel={2}
                kicker={t("Pages_Unit_Kassenbuch_Index_LedgerKicker")}
                title={t("Pages_Unit_Kassenbuch_Index_LedgerTitle")}
                actions={<Money24Regular aria-hidden="true" />}
              />
              <div className="em-table-scroll">
                <table>
                  <caption className="sr-only">
                    {t("Pages_Unit_Kassenbuch_Index_LedgerTitle")}
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("Pages_Unit_Kassenbuch_Index_TableDate")}</th>
                      <th scope="col">{t("Pages_Unit_Kassenbuch_Index_TableCategory")}</th>
                      <th scope="col">{t("Pages_Unit_Kassenbuch_Index_TableDescription")}</th>
                      <th scope="col">{t("Pages_Unit_Kassenbuch_Index_TableReference")}</th>
                      <th scope="col">{t("Pages_Unit_Kassenbuch_Index_TableAmount")}</th>
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
    </>,
  );
}
