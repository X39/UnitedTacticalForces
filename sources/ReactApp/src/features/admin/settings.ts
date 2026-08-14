import { getApiClient } from "@/api/client";
import { apiPath } from "@/api/contract";
import type { components } from "@/generated/api";

export type SettingsSnapshot = components["schemas"]["CommandCenterSettingsSnapshotDto"];
export type SettingsFeature = components["schemas"]["CommandCenterSettingsFeatureDto"];

export async function loadSettingsSnapshot(signal?: AbortSignal): Promise<SettingsSnapshot> {
  return (
    (await getApiClient().request<SettingsSnapshot>(apiPath("/command-center/settings-snapshot"), {
      signal,
    })) ?? {}
  );
}
