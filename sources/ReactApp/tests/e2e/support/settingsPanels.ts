import type { Page } from "@playwright/test";

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

/** The sessionStorage key the Settings section keeps its open panel ids under. */
const storageKey = "utf.settings.open";

/**
 * Registers an init script that opens Settings panels before the app first reads its open state.
 *
 * The script writes only when the key is absent, so a reload inside a test keeps the member's own open state, and it
 * stores nothing but the array of anchor ids.
 *
 * @param page The page to seed; call before the first navigation.
 * @param ids The anchor ids to open; every panel of the section when omitted, an empty array for the collapsed default.
 */
export async function seedSettingsPanels(
  page: Page,
  ids?: readonly string[] | undefined,
): Promise<void> {
  await page.addInitScript(
    ({ key, value }) => {
      if (sessionStorage.getItem(key) === null) {
        sessionStorage.setItem(key, value);
      }
    },
    { key: storageKey, value: JSON.stringify(ids ?? allSettingsPanelIds) },
  );
}
