import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";

export function FinanceAccountPicker({
  value,
  name,
  label,
  allowAll = false,
  excludeId,
  required = false,
  onChange,
}: {
  value: string;
  name?: string;
  label: string;
  allowAll?: boolean;
  excludeId?: string;
  required?: boolean;
  onChange(value: string): void;
}) {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const accounts = useQuery({
    queryKey: financeQueryKeys.owners(search, 0, 50),
    queryFn: ({ signal }) => financeService.owners(search, 0, 50, undefined, false, signal),
  });
  return (
    <div className="finance-picker">
      <label className="field">
        <span>{t("HostingFinance_SearchAccounts")}</span>
        <input value={search} onChange={(event) => setSearch(event.target.value)} />
      </label>
      <label className="field">
        <span>{label}</span>
        <select
          name={name}
          value={value}
          required={required}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="">
            {t(allowAll ? "HostingFinance_FilterAll" : "HostingFinance_SelectAccount")}
          </option>
          {accounts.data?.owners
            .filter((owner) => owner.costOwnerId !== excludeId)
            .map((owner) => (
              <option key={owner.costOwnerId} value={owner.costOwnerId}>
                {owner.displayName}
              </option>
            ))}
        </select>
      </label>
    </div>
  );
}
