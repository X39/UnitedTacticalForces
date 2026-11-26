import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { SelectField, TextField } from "@/components/FormFields";
import { financeQueryKeys, financeService } from "@/features/hosting/financeService";

/**
 * A labelled account chooser: a search field that narrows a labelled select of billing accounts.
 *
 * The select is a shared {@link SelectField}, so a `name` makes its value part of a surrounding form. Frozen accounts
 * are never offered.
 *
 * @param props.value The chosen cost owner id, or an empty string for none.
 * @param props.name Optional form field name of the select.
 * @param props.label Visible label of the select.
 * @param props.allowAll Offers "All" instead of "Select an account" as the empty choice.
 * @param props.excludeId A cost owner id that is left out of the list.
 * @param props.required Marks the select as required.
 * @param props.onChange Called with the chosen cost owner id.
 * @returns The search field and the select.
 */
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
    <>
      <TextField
        label={t("HostingFinance_SearchAccounts")}
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />
      <SelectField
        name={name}
        label={label}
        value={value}
        required={required}
        options={[
          {
            value: "",
            label: t(allowAll ? "HostingFinance_FilterAll" : "HostingFinance_SelectAccount"),
          },
          ...(accounts.data?.owners ?? [])
            .filter((owner) => owner.costOwnerId !== excludeId)
            .map((owner) => ({ value: owner.costOwnerId, label: owner.displayName })),
        ]}
        onChange={(event) => onChange(event.target.value)}
      />
    </>
  );
}
