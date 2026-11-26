import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { SelectField, TextField } from "@/components/FormFields";
import { StatePanel } from "@/components/primitives";
import { LoadingState } from "@/components/StateViews";
import { formatBytes, formatMoney } from "@/features/hosting/model";
import { automaticExternalStorageGiB } from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";

/** The localized label of each storage strategy; the same texts the strategy select offers. */
const storageStrategyKeys = {
  IncludedDisk: "React_ServerWorkspace_IncludedDiskOnly",
  AutomaticExternal: "React_ServerWorkspace_AutomaticExternal",
  FixedExternal: "React_ServerWorkspace_FixedExternal",
} as const satisfies Record<GameServer["runtimeStorageStrategy"], string>;

export function ServerRuntimePanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
  });
  const demand = useQuery({
    queryKey: hostingQueryKeys.storageDemand(server.primaryKey),
    queryFn: ({ signal }) => hostingService.storageDemand(server.primaryKey, signal),
  });
  const capability = capabilities.data?.find(
    (item) => item.gameIdentifier.toLowerCase() === server.gameIdentifier.toLowerCase(),
  );
  const terrains = useQuery({
    queryKey: hostingQueryKeys.terrains(server.gameIdentifier),
    queryFn: ({ signal }) => hostingService.terrains(server.gameIdentifier, signal),
    enabled: capability?.requiresTerrain === true,
  });
  const scenarios = useQuery({
    queryKey: hostingQueryKeys.scenarios(server.gameIdentifier),
    queryFn: ({ signal }) => hostingService.scenarios(server.gameIdentifier, signal),
    enabled: capability?.requiresScenario === true,
  });
  const [profileId, setProfileId] = useState(server.runtimeProfileIdentifier);
  const [strategy, setStrategy] = useState(server.runtimeStorageStrategy);
  const [capacity, setCapacity] = useState<number | null>(server.externalStorageSizeGiB);
  const [terrainId, setTerrainId] = useState<number | null>(server.selectedTerrainId);
  const [scenarioId, setScenarioId] = useState<string | null>(server.selectedScenarioId);
  const provider = useMemo(
    () =>
      capabilities.data
        ?.find(
          (capability) =>
            capability.gameIdentifier.toLowerCase() === server.gameIdentifier.toLowerCase(),
        )
        ?.runtimeProviderProfiles.find(
          (candidate) =>
            candidate.identifier.toLowerCase() === server.runtimeProviderIdentifier.toLowerCase(),
        ),
    [capabilities.data, server.gameIdentifier, server.runtimeProviderIdentifier],
  );
  const selectedProfile = provider?.profiles.find((profile) => profile.identifier === profileId);
  const storage = provider?.storage;
  const automaticCapacity = automaticExternalStorageGiB(demand.data, storage);
  const actualCapacity = strategy === "AutomaticExternal" ? (automaticCapacity ?? null) : capacity;
  const storageValid =
    strategy === "IncludedDisk" ||
    Boolean(
      storage?.supportsExternalBlockStorage &&
      actualCapacity !== null &&
      actualCapacity >= storage.minimumCapacityGiB &&
      actualCapacity <= storage.maximumCapacityGiB &&
      actualCapacity % storage.capacityIncrementGiB === 0 &&
      Number.isSafeInteger(actualCapacity) &&
      demand.data?.minimumExternalCapacityGiB != null &&
      actualCapacity >= demand.data.minimumExternalCapacityGiB,
    );
  const refresh = async () => {
    await client.cancelQueries({ queryKey: hostingQueryKeys.storageDemand(server.primaryKey) });
    await client.invalidateQueries({ queryKey: hostingQueryKeys.server(server.primaryKey) });
  };
  const profileMutation = useMutation({
    mutationFn: () => hostingService.changeRuntimeProfile(server.primaryKey, profileId),
    onSuccess: refresh,
  });
  const storageMutation = useMutation({
    mutationFn: () =>
      hostingService.changeRuntimeStorage(
        server.primaryKey,
        strategy,
        strategy === "FixedExternal" ? capacity : null,
      ),
    onSuccess: refresh,
  });
  const contentMutation = useMutation({
    mutationFn: () =>
      hostingService.changeContentSelection(server.primaryKey, terrainId, scenarioId),
    onSuccess: refresh,
  });
  const error =
    capabilities.error?.message ??
    demand.error?.message ??
    profileMutation.error?.message ??
    storageMutation.error?.message ??
    contentMutation.error?.message;

  return (
    <div className="next-hosting-runtime-stack">
      {error && (
        <p role="alert" className="next-hosting-inline-error">
          {error}
        </p>
      )}
      <section className="next-hosting-panel">
        <header className="next-hosting-section-heading">
          <div>
            <span className="kicker">{t("Hosting_Detail_CloudProfileKicker")}</span>
            <h2>{t("Hosting_Detail_CloudProfileTitle")}</h2>
          </div>
        </header>
        {capabilities.isLoading ? (
          <LoadingState label={t("React_ServerWorkspace_LoadingRuntimeProfiles")} />
        ) : !provider ? (
          <StatePanel kind="warning">
            {t("React_ServerWorkspace_RuntimeCatalogUnavailable")}
          </StatePanel>
        ) : (
          <>
            <SelectField
              label={t("React_ServerWorkspace_ProfileNextDeployment")}
              disabled={!server.canModify || server.isArchived || profileMutation.isPending}
              value={profileId}
              onChange={(event) => setProfileId(event.target.value)}
              options={provider.profiles.map((profile) => ({
                value: profile.identifier,
                label: `${profile.displayName} · ${profile.virtualCpuCount ?? "—"} vCPU · ${
                  profile.memoryGiB ?? "—"
                } GiB · ${formatMoney(profile.estimatedHourlyCostEur)}/h`,
              }))}
            />
            {selectedProfile && (
              <dl className="next-hosting-facts">
                <div>
                  <dt>{t("Hosting_Detail_Compute")}</dt>
                  <dd>
                    {`${selectedProfile.virtualCpuCount ?? "—"} vCPU · ${
                      selectedProfile.memoryGiB ?? "—"
                    } GiB RAM`}
                  </dd>
                </div>
                <div>
                  <dt>{t("React_ServerWorkspace_IncludedDisk")}</dt>
                  <dd>{selectedProfile.diskGiB ?? "—"} GiB</dd>
                </div>
                <div>
                  <dt>{t("React_ServerWorkspace_HourlyEstimate")}</dt>
                  <dd>{formatMoney(selectedProfile.estimatedHourlyCostEur)}</dd>
                </div>
                <div>
                  <dt>{t("React_ServerWorkspace_MonthlyEstimate")}</dt>
                  <dd>{formatMoney(selectedProfile.estimatedMonthlyCostEur)}</dd>
                </div>
              </dl>
            )}
            {server.deployment && (
              <StatePanel kind="info">{t("React_ServerWorkspace_ProfileAppliesNext")}</StatePanel>
            )}
            {server.canModify && (
              <div className="next-hosting-form-actions">
                <ActionButton
                  variant="primary"
                  disabled={
                    profileId === server.runtimeProfileIdentifier || profileMutation.isPending
                  }
                  onClick={() => profileMutation.mutate()}
                >
                  {t("Hosting_Detail_SaveCloudProfile")}
                </ActionButton>
              </div>
            )}
          </>
        )}
      </section>
      {storage && (
        <section className="next-hosting-panel">
          <header className="next-hosting-section-heading">
            <div>
              <span className="kicker">{t("React_ServerWorkspace_StorageKicker")}</span>
              <h2>{t("React_ServerWorkspace_StorageConfigurationTitle")}</h2>
            </div>
          </header>
          <SelectField
            label={t("React_ServerWorkspace_StorageStrategy")}
            disabled={
              !server.canModify ||
              server.isArchived ||
              Boolean(server.deployment) ||
              storageMutation.isPending
            }
            value={strategy}
            onChange={(event) =>
              setStrategy(event.target.value as GameServer["runtimeStorageStrategy"])
            }
            options={[
              { value: "IncludedDisk", label: t("React_ServerWorkspace_IncludedDiskOnly") },
              ...(storage.supportsExternalBlockStorage
                ? [
                    {
                      value: "AutomaticExternal",
                      label: t("React_ServerWorkspace_AutomaticExternal"),
                    },
                    { value: "FixedExternal", label: t("React_ServerWorkspace_FixedExternal") },
                  ]
                : []),
            ]}
          />
          {strategy === "FixedExternal" && (
            <TextField
              label={t("React_ServerWorkspace_ExternalCapacity")}
              type="number"
              min={storage.minimumCapacityGiB}
              max={storage.maximumCapacityGiB}
              step={storage.capacityIncrementGiB}
              value={capacity ?? ""}
              onChange={(event) =>
                setCapacity(event.target.value ? Number(event.target.value) : null)
              }
            />
          )}
          <dl className="next-hosting-facts">
            <div>
              <dt>{t("React_ServerWorkspace_PeakRequirement")}</dt>
              <dd>{formatBytes(demand.data?.peakPreparationRequiredBytes)}</dd>
            </div>
            {strategy !== "IncludedDisk" && (
              <div>
                <dt>{t("Hosting_Storage_RecommendedUsableCapacity")}</dt>
                <dd>{formatBytes(demand.data?.recommendedExternalUsableBytes)}</dd>
              </div>
            )}
            <div>
              <dt>
                {t(
                  strategy === "IncludedDisk"
                    ? "React_ServerWorkspace_SelectedCapacity"
                    : "Hosting_Storage_SelectedRawCapacity",
                )}
              </dt>
              <dd>
                {strategy === "IncludedDisk"
                  ? formatBytes(selectedProfile?.usableStorageBytes)
                  : actualCapacity === null
                    ? "—"
                    : `${actualCapacity} GiB`}
              </dd>
            </div>
            <div>
              <dt>{t("React_ServerWorkspace_MountPath")}</dt>
              <dd>
                <code>{storage.deploymentMountPath}</code>
              </dd>
            </div>
            <div>
              <dt>{t("React_ServerWorkspace_Strategy")}</dt>
              <dd>{t(storageStrategyKeys[strategy])}</dd>
            </div>
          </dl>
          {strategy !== "IncludedDisk" && (
            <StatePanel kind="warning">{t("Server_Machine_ExternalStorage")}</StatePanel>
          )}
          {server.deployment && (
            <StatePanel kind="info">{t("React_ServerWorkspace_StorageAppliesNext")}</StatePanel>
          )}
          {storage.readinessFailure && (
            <StatePanel kind="warning">{storage.readinessFailure}</StatePanel>
          )}
          {!storageValid && (
            <StatePanel kind="error">{t("React_ServerWorkspace_InvalidCapacity")}</StatePanel>
          )}
          {server.canModify && (
            <div className="next-hosting-form-actions">
              <ActionButton
                variant="primary"
                disabled={
                  Boolean(server.deployment) ||
                  !storageValid ||
                  storageMutation.isPending ||
                  (strategy === server.runtimeStorageStrategy &&
                    capacity === server.externalStorageSizeGiB)
                }
                onClick={() => storageMutation.mutate()}
              >
                {t("React_ServerWorkspace_SaveStorage")}
              </ActionButton>
            </div>
          )}
        </section>
      )}
      {(capability?.requiresTerrain || capability?.requiresScenario) && (
        <section className="next-hosting-panel">
          <header className="next-hosting-section-heading">
            <div>
              <span className="kicker">{t("React_ServerWorkspace_ContentKicker")}</span>
              <h2>{t("React_ServerWorkspace_ContentTitle")}</h2>
            </div>
          </header>
          {capability.requiresTerrain && (
            <SelectField
              label={capability.terrainLabel ?? t("Server_Overview_Terrain")}
              value={terrainId ?? ""}
              disabled={!server.canModify || terrains.isLoading}
              onChange={(event) =>
                setTerrainId(event.target.value ? Number(event.target.value) : null)
              }
              options={(terrains.data ?? []).map((item) => ({
                value: String(item.primaryKey),
                label: item.title,
              }))}
            />
          )}
          {capability.requiresScenario && (
            <SelectField
              label={capability.scenarioLabel ?? t("Server_Overview_Scenario")}
              value={scenarioId ?? ""}
              disabled={!server.canModify || scenarios.isLoading}
              onChange={(event) => setScenarioId(event.target.value || null)}
              options={(scenarios.data ?? []).map((item) => ({
                value: String(item.primaryKey),
                label: item.title,
              }))}
            />
          )}
          <p className="next-hosting-muted">{t("React_ServerWorkspace_ContentFollowsLatest")}</p>
          {server.canModify && (
            <div className="next-hosting-form-actions">
              <ActionButton
                variant="primary"
                disabled={
                  contentMutation.isPending ||
                  (terrainId === server.selectedTerrainId &&
                    scenarioId === server.selectedScenarioId)
                }
                onClick={() => contentMutation.mutate()}
              >
                {t("React_ServerWorkspace_SaveContentSelection")}
              </ActionButton>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
