import type { CostAlert, CostAlertSettings } from "@/features/hosting/types";

/**
 * Builds a cost alert as the API sends it. Every call returns a fresh object.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns A fully populated alert for a 5 euro threshold crossed at 5.02 euros.
 */
export function costAlert(overrides: Partial<CostAlert> = {}): CostAlert {
  return {
    id: "a1000000-0000-4000-8000-000000000001",
    serverId: "f1ee7000-0000-4000-8000-000000000001",
    serverName: "Fleet Server 1",
    costOwnerId: "c0000000-0000-4000-8000-000000000001",
    thresholdAmount: 5,
    accruedAmount: 5.02,
    currency: "EUR",
    createdAt: "2026-11-21T14:05:00Z",
    ...overrides,
  };
}

/**
 * Builds an automatic shutdown warning as the API sends it. Every call returns a fresh object.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns An AutoStop alert announcing an idle shutdown after 30 minutes, with no threshold or accrued amount.
 */
export function autoStopAlert(overrides: Partial<CostAlert> = {}): CostAlert {
  return costAlert({
    id: "a2000000-0000-4000-8000-000000000001",
    thresholdAmount: 0,
    accruedAmount: 0,
    kind: "AutoStop",
    stopsAt: "2026-11-21T14:35:00Z",
    stopReason: "Idle",
    stopLimit: 30,
    ...overrides,
  });
}

/**
 * Builds cost alert settings as the API sends them. Every call returns a fresh object.
 *
 * @param overrides Fields that replace the defaults; nothing else changes.
 * @returns Settings of a personal cost owner the viewer may manage, with two amounts and both channels on.
 */
export function costAlertSettings(overrides: Partial<CostAlertSettings> = {}): CostAlertSettings {
  return {
    costOwnerId: "c0000000-0000-4000-8000-000000000001",
    costOwnerName: "Operations",
    revision: 1,
    thresholds: [5, 10],
    discordEnabled: true,
    emailEnabled: true,
    currency: "EUR",
    maxThresholds: 10,
    maxThresholdAmount: 10000,
    canManage: true,
    audience: "Self",
    discord: "Deliverable",
    email: "Deliverable",
    messageLanguage: "en",
    ...overrides,
  };
}
