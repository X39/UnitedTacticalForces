import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { FinanceProtocol } from "@/features/hosting/FinanceProtocol";
import { getFinancePermissions } from "@/features/hosting/financeAuthorization";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";
import { formatDate } from "@/features/hosting/model";

export function HostingFinanceSettingsPage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const permissions = getFinancePermissions(auth.user);
  const queryClient = useQueryClient();
  const policy = useQuery({
    queryKey: financeQueryKeys.policy,
    queryFn: ({ signal }) => financeService.policy(signal),
    enabled: permissions.view,
  });
  const save = useMutation({
    mutationFn: ({ reserve, slice }: { reserve: number; slice: number }) =>
      financeService.updatePolicy(reserve, slice),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: financeQueryKeys.policy });
    },
  });

  if (auth.isLoading || policy.isLoading)
    return <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>;
  if (!permissions.view)
    return <StatePanel kind="forbidden">{t("HostingFinance_NoAccessMessage")}</StatePanel>;
  return (
    <div className="finance-page">
      <ActionLink variant="ghost" size="compact" to="/command/hosting-finance">
        ← {t("HostingFinance_BackToAccounts")}
      </ActionLink>
      <PageHeader
        kicker={t("HostingFinance_Global")}
        title={t("HostingFinance_GlobalSettings")}
        description={t("HostingFinance_GlobalDescription")}
      />
      {policy.isError && <StatePanel kind="error">{t("HostingFinance_LoadError")}</StatePanel>}
      {policy.data && (
        <section className="finance-policy">
          <header className="finance-section-heading">
            <div>
              <h2>{t("HostingFinance_PolicyTitle")}</h2>
              <p>{t("HostingFinance_PolicyDescription")}</p>
            </div>
            <StatusBadge tone="info">
              {policy.data.timeStampUpdated
                ? t("HostingFinance_PolicyUpdated", {
                    date: formatDate(policy.data.timeStampUpdated),
                  })
                : t("HostingFinance_NotAvailable")}
            </StatusBadge>
          </header>
          <form
            className="finance-policy__form"
            onSubmit={(event: FormEvent<HTMLFormElement>) => {
              event.preventDefault();
              const data = new FormData(event.currentTarget);
              save.mutate({
                reserve: Number(data.get("reserve")),
                slice: Number(data.get("slice")),
              });
            }}
          >
            <label className="field">
              <span>{t("HostingFinance_MinimumReserve")}</span>
              <input
                name="reserve"
                type="number"
                min="0"
                step="0.01"
                defaultValue={policy.data.minimumSafetyRangeEur}
                disabled={!permissions.managePolicy}
              />
            </label>
            <label className="field">
              <span>{t("HostingFinance_ReservationSlice")}</span>
              <input
                name="slice"
                type="number"
                min="1"
                max="1440"
                step="1"
                defaultValue={policy.data.reservationSliceMinutes}
                disabled={!permissions.managePolicy}
              />
            </label>
            {permissions.managePolicy ? (
              <ActionButton variant="primary" type="submit" disabled={save.isPending}>
                {save.isPending ? t("HostingFinance_Saving") : t("HostingFinance_SavePolicy")}
              </ActionButton>
            ) : (
              <StatusBadge>{t("HostingFinance_ReadOnly")}</StatusBadge>
            )}
          </form>
        </section>
      )}
      <FinanceProtocol showAccount canReverse={permissions.correctLedger} />
    </div>
  );
}
