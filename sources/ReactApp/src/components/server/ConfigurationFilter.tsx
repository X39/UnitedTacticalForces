import { useEffect, useState, type JSX } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { SearchField } from "@/components/SearchField";
import type { ConfigurationFilterMode } from "@/components/server/configurationFilterRules";
import "@/components/server/configuration.css";

/** How long the announced result count waits for the last change before a screen reader reads it. */
const announceSettleMs = 300;

/** Props of {@link ConfigurationFilter}. */
export interface ConfigurationFilterProps {
  /** The search text as typed. */
  query: string;
  /** The pressed chip. */
  mode: ConfigurationFilterMode;
  /** The number of changed settings, shown in the Changed chip. */
  changedCount: number;
  /** The number of settings that need a value, shown in the Needs a value chip. */
  attentionCount: number;
  /** The number of settings the current query and chip show. */
  shownCount: number;
  /**
   * Called with the new search text on every keystroke and with an empty string when the search is cleared.
   *
   * @param query The new search text.
   */
  onQueryChange(query: string): void;
  /**
   * Called when a chip is activated.
   *
   * @param mode The activated chip.
   */
  onModeChange(mode: ConfigurationFilterMode): void;
}

/**
 * The search field, the chips All, Changed and Needs a value, and the result count of the Configuration panel.
 *
 * The component is presentational; the panel owns the query and the chip. The visible count follows every change at
 * once, while the copy a screen reader announces is a separate polite live region that settles 300ms after the last
 * change, so typing a word does not read out one count per keystroke.
 *
 * @param props The query, the pressed chip, the counts and the change callbacks.
 * @returns The toolbar.
 */
export function ConfigurationFilter({
  query,
  mode,
  changedCount,
  attentionCount,
  shownCount,
  onQueryChange,
  onModeChange,
}: ConfigurationFilterProps): JSX.Element {
  const { t } = useTranslation();
  const numberFormat = new Intl.NumberFormat(currentLocale());
  const resultText = t("Server_Settings_Filter_Result", {
    count: shownCount,
    replace: { count: numberFormat.format(shownCount) },
  });
  // The announced copy starts equal to the visible one, so mounting the toolbar announces nothing.
  const [announced, setAnnounced] = useState(resultText);
  useEffect(() => {
    const timer = window.setTimeout(() => setAnnounced(resultText), announceSettleMs);
    return () => window.clearTimeout(timer);
  }, [resultText]);

  const items: FilterChipItem<ConfigurationFilterMode>[] = [
    { id: "all", label: t("Server_Settings_Filter_All") },
    { id: "changed", label: t("Server_Settings_Filter_Changed"), count: changedCount },
    { id: "attention", label: t("Server_Settings_Filter_Attention"), count: attentionCount },
  ];

  return (
    <div className="configuration-filter">
      <SearchField
        label={t("Server_Settings_Filter_Label")}
        placeholder={t("Server_Settings_Filter_Placeholder")}
        clearLabel={t("Server_Settings_Filter_Clear")}
        value={query}
        onChange={onQueryChange}
      />
      <FilterChipGroup
        label={t("Server_Settings_Filter_GroupLabel")}
        items={items}
        value={mode}
        onChange={onModeChange}
        formatCount={(count) => numberFormat.format(count)}
      />
      <p className="configuration-filter__count" aria-hidden="true">
        {resultText}
      </p>
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {announced}
      </div>
    </div>
  );
}
