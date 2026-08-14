import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { StatePanel } from "@/components/primitives";
import {
  isSelectableStandalone,
  latestActiveRevision,
  modPackQueryKeys,
  modPackService,
  type FullModPackDefinition,
} from "@/features/admin/modPacks";

export function ModPackStandaloneSelector({
  gameIdentifier,
  selected,
  onChange,
}: {
  gameIdentifier: string;
  selected: FullModPackDefinition[];
  onChange(value: FullModPackDefinition[]): void;
}) {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: modPackQueryKeys.selector(gameIdentifier, search.trim()),
    queryFn: ({ signal }) =>
      modPackService.list(
        {
          skip: 0,
          take: 100,
          myModPacksOnly: false,
          gameIdentifier,
          ...(search.trim() ? { search: search.trim() } : {}),
        },
        signal,
      ),
    enabled: Boolean(gameIdentifier),
  });
  const selectedIds = new Set(selected.map((definition) => definition.primaryKey));
  const options = (query.data ?? []).filter(isSelectableStandalone);
  const toggle = (definition: FullModPackDefinition, checked: boolean) => {
    if (checked) {
      if (selected.length >= 100 || selectedIds.has(definition.primaryKey)) return;
      onChange([...selected, definition]);
      return;
    }
    onChange(selected.filter((item) => item.primaryKey !== definition.primaryKey));
  };

  return (
    <div className="modpack-selector">
      <label className="field">
        <span>{t("Auto_StandaloneModPacks_34afa7f0")}</span>
        <input
          value={search}
          placeholder={t("Auto_SearchStandaloneModPacks_888f2c1f")}
          onChange={(event) => setSearch(event.target.value)}
        />
      </label>
      {query.isPending ? (
        <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
      ) : query.isError ? (
        <StatePanel kind="error">{t("Pages_Data_ModPacks_All_LoadError")}</StatePanel>
      ) : options.length === 0 ? (
        <StatePanel>{t("Auto_NoModPacksMatchTheCurrentFilters_33565b66")}</StatePanel>
      ) : (
        <div
          className="modpack-selector__options"
          role="group"
          aria-label={t("Auto_StandaloneModPacks_34afa7f0")}
        >
          {options.map((definition) => {
            const id = definition.primaryKey;
            const revision = latestActiveRevision(definition);
            const checked = selectedIds.has(id);
            return (
              <label key={id ?? definition.title} className="modpack-selector__option">
                <input
                  type="checkbox"
                  checked={checked}
                  disabled={id == null || (!checked && selected.length >= 100)}
                  onChange={(event) => toggle(definition, event.target.checked)}
                />
                <span>
                  <strong>{definition.title || t("Auto_UntitledModPack_68e62d9a")}</strong>
                  <small>
                    #{id ?? "—"} · {t("Revision")} #{revision?.primaryKey ?? "—"} ·{" "}
                    {formatDateTime(revision?.timeStampCreated)}
                  </small>
                </span>
              </label>
            );
          })}
        </div>
      )}
      <small>
        {selected.length === 1
          ? t("Auto_1StandalonePackSelected_d8f0dc9c")
          : formatResource(t("Auto_StandalonePacksSelected_e437ccf0"), selected.length)}
      </small>
    </div>
  );
}
