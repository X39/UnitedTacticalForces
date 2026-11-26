import { settingsPanelStorageKey } from "@/components/server/useSettingsPanelState";

/** The anchor ids of every panel of the Settings section, in page order. */
export const allSettingsPanelIds: readonly string[] = [
  "server-mods",
  "server-configuration",
  "server-credentials",
  "server-automation",
  "server-access",
  "server-cost-owner",
  "server-record",
];

/**
 * Stores which Settings panels are open before a render, as a member's earlier visit of the browser session would.
 *
 * @param ids The anchor ids to open; every panel of the section when omitted.
 */
export function seedSettingsPanels(ids: readonly string[] = allSettingsPanelIds): void {
  sessionStorage.setItem(settingsPanelStorageKey, JSON.stringify(ids));
}

/** Clears the stored open state, so the next render shows the collapsed first-visit default. */
export function clearSettingsPanels(): void {
  sessionStorage.removeItem(settingsPanelStorageKey);
}
