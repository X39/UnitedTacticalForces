import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";
import { SearchField } from "@/components/SearchField";
import { fleetFilters, type FleetFilter } from "@/features/hosting/fleetModel";
import "@/components/server/fleet-toolbar.css";

/** Visible label key of each fleet filter chip. */
const filterLabelKeys: Record<FleetFilter, TranslationKey> = {
  all: "Server_Fleet_Filter_All",
  mine: "Server_Fleet_Filter_Mine",
  running: "Server_Fleet_Filter_Running",
  off: "Server_Fleet_Filter_Off",
};

/** The numbers of the result summary shown while a search or filter is active. */
export interface FleetResultSummary {
  /** Number of servers shown after search and filter. */
  shown: number;
  /** Number of servers in the whole fleet. */
  total: number;
}

/** Props of {@link FleetToolbar}. */
export interface FleetToolbarProps {
  /** The current search text. */
  search: string;
  /**
   * Called with the new search text on every keystroke and with an empty string when the search is cleared.
   *
   * @param value The new search text.
   */
  onSearchChange(value: string): void;
  /** Optional ref that receives the search input so the page can move focus to it. */
  searchInputRef?: RefObject<HTMLInputElement | null>;
  /** The active filter. */
  filter: FleetFilter;
  /** Number of servers each filter matches over the whole fleet. */
  counts: Record<FleetFilter, number>;
  /**
   * Called when a filter chip is activated.
   *
   * @param filter The activated filter.
   */
  onFilterChange(filter: FleetFilter): void;
  /** The result summary, or null while neither a search nor a filter other than All is active. */
  summary: FleetResultSummary | null;
}

/**
 * The fleet search field, the counted filter chips and the result summary.
 *
 * The component is purely presentational; the page owns the search, filter and URL state. The result summary
 * container is always rendered as a polite live region so screen readers announce changes, and it carries text only
 * while a summary is supplied.
 */
export function FleetToolbar({
  search,
  onSearchChange,
  searchInputRef,
  filter,
  counts,
  onFilterChange,
  summary,
}: FleetToolbarProps) {
  const { t } = useTranslation();
  const numberFormat = new Intl.NumberFormat(currentLocale());
  const items: FilterChipItem<FleetFilter>[] = fleetFilters.map((id) => ({
    id,
    label: t(filterLabelKeys[id]),
    count: counts[id],
  }));

  return (
    <div className="fleet-toolbar">
      <SearchField
        label={t("Server_Fleet_SearchLabel")}
        placeholder={t("Server_Fleet_SearchPlaceholder")}
        clearLabel={t("Server_Fleet_SearchClear")}
        value={search}
        onChange={onSearchChange}
        {...(searchInputRef ? { inputRef: searchInputRef } : {})}
      />
      <FilterChipGroup
        label={t("Server_Fleet_FilterLabel")}
        items={items}
        value={filter}
        onChange={onFilterChange}
        formatCount={(count) => numberFormat.format(count)}
      />
      <div className="fleet-toolbar__summary" aria-live="polite" aria-atomic="true">
        {summary &&
          t("Server_Fleet_ResultCount", {
            count: summary.total,
            replace: {
              count: numberFormat.format(summary.total),
              shown: numberFormat.format(summary.shown),
            },
          })}
      </div>
    </div>
  );
}
