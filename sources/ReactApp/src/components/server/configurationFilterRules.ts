import type { ServerChangeDraft } from "@/features/hosting/serverChangeDraft";
import type { GameConfigurationEntry } from "@/features/hosting/types";
import { configurationInputValue } from "@/features/hosting/workspaceModel";

/**
 * The chip that is pressed in the Configuration filter.
 *
 * - `all` keeps every entry that matches the query.
 * - `changed` keeps staged, reset-staged and profile-covered entries.
 * - `attention` keeps the entries that still need a value from the member.
 */
export type ConfigurationFilterMode = "all" | "changed" | "attention";

/** The part of the staged-change draft the filter reads. */
export type ConfigurationFilterDraft = Pick<ServerChangeDraft, "values" | "resets" | "profile">;

/**
 * Whether an entry is required, can be typed and has no value yet.
 *
 * @param entry The entry to inspect.
 * @param value The raw value the member currently sees for it.
 * @returns True when the entry still needs a value from the member.
 */
export function isRequiredEmpty(entry: GameConfigurationEntry, value: string): boolean {
  return (
    entry.required &&
    !entry.sensitive &&
    !entry.isRedacted &&
    entry.kind.toLowerCase() !== "boolean" &&
    value.trim().length === 0
  );
}

/**
 * Normalizes a text for the search: trimmed, lower-cased independent of the browser locale and without diacritics.
 *
 * @param text The text to normalize.
 * @returns The normalized text, which is empty for a blank text.
 */
function normalize(text: string): string {
  return text.trim().toLowerCase().normalize("NFD").replace(/\p{M}/gu, "");
}

/**
 * Whether a filter is active, which is the case for a query with text or for a chip other than All.
 *
 * @param query The search text as typed.
 * @param mode The pressed chip.
 * @returns True when the list must be filtered and the Common and Advanced split must not be shown.
 */
export function isFilterActive(query: string, mode: ConfigurationFilterMode): boolean {
  return normalize(query) !== "" || mode !== "all";
}

/**
 * Whether the search text matches an entry.
 *
 * The match is a substring of the display name, the group name or the description, and the description counts only
 * when the API supplies one. The value of an entry is never searched, so a sensitive or redacted value can never be
 * found through the search or inferred from it.
 *
 * @param entry The entry to test.
 * @param normalizedQuery The normalized, non-empty search text.
 * @returns True when the entry matches.
 */
function matchesQuery(entry: GameConfigurationEntry, normalizedQuery: string): boolean {
  if (normalize(entry.displayName).includes(normalizedQuery)) return true;
  if (normalize(entry.group).includes(normalizedQuery)) return true;
  const description = entry.description?.trim() ?? "";
  return description !== "" && normalize(description).includes(normalizedQuery);
}

/**
 * Whether an entry counts as changed.
 *
 * An entry is changed when a value or a reset is staged for it, or when a profile change is staged and the entry's
 * value is inherited (its source is not `Server`), because the new profile can change what it resolves to.
 *
 * @param entry The entry to test.
 * @param draft The staged changes.
 * @returns True when the entry is changed.
 */
function isChanged(entry: GameConfigurationEntry, draft: ConfigurationFilterDraft): boolean {
  if (draft.values.has(entry.key) || draft.resets.has(entry.key)) return true;
  return draft.profile !== null && entry.source !== "Server";
}

/**
 * Whether an entry needs a value from the member, judged on the value the member currently sees.
 *
 * @param entry The entry to test.
 * @param draft The staged changes.
 * @returns True when the entry is required, typed and empty.
 */
function needsValue(entry: GameConfigurationEntry, draft: ConfigurationFilterDraft): boolean {
  return isRequiredEmpty(entry, draft.values.get(entry.key) ?? configurationInputValue(entry));
}

/**
 * Filters the configuration entries by the search text and the pressed chip.
 *
 * The result keeps the order of the input and never mutates it. A blank query matches every entry; the chip then
 * decides alone.
 *
 * @param entries The entries in API order.
 * @param query The search text as typed.
 * @param mode The pressed chip.
 * @param draft The staged changes, which decide the Changed and Needs a value chips.
 * @param stickyKeys Keys that keep passing the chip test even when the entry no longer satisfies it, so that a row the
 * member is editing does not vanish under the cursor. The query still applies to them.
 * @returns The entries that match both the query and the chip.
 */
export function filterEntries(
  entries: readonly GameConfigurationEntry[],
  query: string,
  mode: ConfigurationFilterMode,
  draft: ConfigurationFilterDraft,
  stickyKeys?: ReadonlySet<string>,
): GameConfigurationEntry[] {
  const normalizedQuery = normalize(query);
  return entries.filter((entry) => {
    if (normalizedQuery !== "" && !matchesQuery(entry, normalizedQuery)) return false;
    if (mode === "all") return true;
    if (stickyKeys?.has(entry.key)) return true;
    return mode === "changed" ? isChanged(entry, draft) : needsValue(entry, draft);
  });
}

/**
 * The keys of the entries that satisfy a chip right now, whatever the query is.
 *
 * @param entries The entries in API order.
 * @param mode The pressed chip.
 * @param draft The staged changes.
 * @returns The keys behind the chip, which is empty for the All chip.
 */
export function chipKeys(
  entries: readonly GameConfigurationEntry[],
  mode: ConfigurationFilterMode,
  draft: ConfigurationFilterDraft,
): string[] {
  if (mode === "all") return [];
  return filterEntries(entries, "", mode, draft).map((entry) => entry.key);
}

/**
 * Counts the entries behind the Changed and the Needs a value chip over the whole list, whatever the query is.
 *
 * @param entries The entries in API order.
 * @param draft The staged changes.
 * @returns The number of changed entries and the number of entries that need a value.
 */
export function countEntries(
  entries: readonly GameConfigurationEntry[],
  draft: ConfigurationFilterDraft,
): { changed: number; attention: number } {
  return {
    changed: filterEntries(entries, "", "changed", draft).length,
    attention: filterEntries(entries, "", "attention", draft).length,
  };
}
