import type { GameConfigurationEntry } from "@/features/hosting/types";

/**
 * Builds a complete configuration entry with neutral defaults.
 *
 * Every Phase 65 test builds its entries through this factory so a later change of the entry shape only has to
 * update this one place.
 *
 * @param overrides Fields that replace the defaults; the key is required and doubles as the default display name.
 * @returns A full configuration entry.
 */
export function configurationEntry(
  overrides: Partial<GameConfigurationEntry> & { key: string },
): GameConfigurationEntry {
  return {
    targetPath: "server/server.cfg",
    group: "General",
    displayName: overrides.key,
    description: "",
    kind: "String",
    required: false,
    sensitive: false,
    isControlPlaneOnly: false,
    tier: "Advanced",
    order: 10,
    minimum: null,
    maximum: null,
    allowedValues: [],
    choices: [],
    value: null,
    source: "Default",
    isCompatible: true,
    isRedacted: false,
    warning: null,
    ...overrides,
  };
}
