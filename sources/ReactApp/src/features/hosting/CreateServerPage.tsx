import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatResource } from "@/app/i18n";
import { currentLocale } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { ApiError } from "@/api/client";
import { useAuth } from "@/auth/auth";
import { PageHeader, StatePanel, useUnsavedChanges } from "@/components/primitives";
import { getHostingPermissions } from "@/features/hosting/authorization";
import { HostingError, HostingLoading } from "@/features/hosting/components";
import { useHostingCopy } from "@/features/hosting/copy";
import {
  combinedHourlyCost,
  createServerCreationSchema,
  isCostOwnerSelectionValid,
  isProviderAvailable,
  orderProviders,
  type ServerCreationFormValues,
} from "@/features/hosting/createServerModel";
import {
  formatBytes,
  isStorageSelectionValid,
  selectedStorageBytes,
  type StorageStrategy,
} from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  RuntimeProfile,
  RuntimeProvider,
  ServerCreationCostOwnerOption,
  StorageDemand,
} from "@/features/hosting/types";

const defaults: ServerCreationFormValues = {
  title: "",
  gameIdentifier: "",
  runtimeProviderIdentifier: "",
  runtimeProfileIdentifier: "",
  runtimeStorageStrategy: "IncludedDisk",
  externalStorageSizeGiB: null,
  modPackDefinitionId: null,
  terrainId: null,
  scenarioId: null,
  configurationProfileId: null,
  costOwnerId: null,
  isLaunchDisabled: false,
};

const costOwnerQueryKey = (userId: string) =>
  ["hosting", "creation", "cost-owners", userId] as const;

function errorMessage(reason: unknown, fallback: string): string {
  if (reason instanceof ApiError) return fallback;
  return reason instanceof Error && reason.message.trim() ? reason.message : fallback;
}

function number(value: number | null | undefined, maximumFractionDigits = 1): string {
  return value == null
    ? "—"
    : new Intl.NumberFormat(currentLocale(), { maximumFractionDigits }).format(value);
}

function euro(value: number | null | undefined, digits: number): string {
  return value == null
    ? "—"
    : new Intl.NumberFormat(currentLocale(), {
        style: "currency",
        currency: "EUR",
        minimumFractionDigits: digits,
        maximumFractionDigits: digits,
      }).format(value);
}

function providerStatusKey(
  status: string,
):
  | "Hosting_ProfileCatalog_ConfigurationRequiredDescription"
  | "Hosting_ProfileCatalog_UnavailableDescription"
  | "Hosting_ProfileCatalog_EmptyDescription" {
  switch (status.trim().toLocaleLowerCase()) {
    case "configuration-required":
      return "Hosting_ProfileCatalog_ConfigurationRequiredDescription";
    case "catalog-unavailable":
      return "Hosting_ProfileCatalog_UnavailableDescription";
    default:
      return "Hosting_ProfileCatalog_EmptyDescription";
  }
}

function RuntimeProfileSummary({ profile }: { profile: RuntimeProfile }) {
  const { t } = useTranslation();
  return (
    <section
      className="next-hosting-profile-summary"
      aria-label={t("Hosting_Profile_Specifications")}
    >
      <header>
        <div>
          <span>{t("Hosting_Profile_Specifications")}</span>
          <strong>{profile.displayName}</strong>
        </div>
        <code>{profile.identifier}</code>
      </header>
      {profile.description && <p>{profile.description}</p>}
      <dl className="next-hosting-facts next-hosting-facts--compact">
        <div>
          <dt>{t("Hosting_Profile_VirtualCpu")}</dt>
          <dd>{number(profile.virtualCpuCount, 0)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Profile_Memory")}</dt>
          <dd>{profile.memoryGiB == null ? "—" : `${number(profile.memoryGiB)} GiB`}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Profile_Disk")}</dt>
          <dd>{profile.diskGiB == null ? "—" : `${number(profile.diskGiB, 0)} GiB`}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Profile_HourlyEstimate")}</dt>
          <dd>{euro(profile.estimatedHourlyCostEur, 4)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Profile_MonthlyEstimate")}</dt>
          <dd>{euro(profile.estimatedMonthlyCostEur, 2)}</dd>
        </div>
      </dl>
    </section>
  );
}

function StorageDemandSummary({
  demand,
  selectedProfileIdentifier,
}: {
  demand: StorageDemand;
  selectedProfileIdentifier: string;
}) {
  const { t } = useTranslation();
  const selectedFit = demand.runtimeProfiles.find(
    (candidate) =>
      candidate.runtimeProfileIdentifier.toLocaleLowerCase() ===
      selectedProfileIdentifier.toLocaleLowerCase(),
  );
  const status = String(demand.status).toLocaleLowerCase();
  const statusLabel =
    status === "measured"
      ? t("Hosting_Create_StorageStatusMeasured")
      : status === "estimated"
        ? t("Hosting_Create_StorageStatusEstimated")
        : status === "incomplete"
          ? t("Hosting_Create_StorageStatusIncomplete")
          : t("Hosting_Create_StorageStatusUnavailable");

  return (
    <section
      className="next-hosting-panel next-hosting-storage-demand"
      aria-labelledby="demand-title"
    >
      <div className="next-hosting-section-heading">
        <span className="kicker">{t("Hosting_Create_StoragePreflight")}</span>
        <h2 id="demand-title">{t("Hosting_Create_StorageDemandTitle")}</h2>
        <p>{t("Hosting_Create_StorageDemandDescription")}</p>
      </div>
      <dl className="next-hosting-facts">
        <div>
          <dt>{t("Hosting_Create_SteadyStateUsage")}</dt>
          <dd>{formatBytes(demand.steadyStateRequiredBytes)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Create_PreparationWorkspace")}</dt>
          <dd>{formatBytes(demand.preparationWorkspaceBytes)}</dd>
        </div>
        <div>
          <dt>
            {formatResource(t("Hosting_Create_SafetyMargin"), number(demand.safetyMarginPercent))}
          </dt>
          <dd>{formatBytes(demand.safetyMarginBytes)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Create_RecommendedPeak")}</dt>
          <dd>{formatBytes(demand.peakPreparationRequiredBytes)}</dd>
        </div>
        {demand.minimumExternalCapacityGiB != null && (
          <>
            <div>
              <dt>{t("Hosting_Storage_RecommendedRawCapacity")}</dt>
              <dd>{number(demand.minimumExternalCapacityGiB, 0)} GiB</dd>
            </div>
            <div>
              <dt>{t("Hosting_Storage_RecommendedUsableCapacity")}</dt>
              <dd>{formatBytes(demand.recommendedExternalUsableBytes)}</dd>
            </div>
          </>
        )}
      </dl>
      <p className="next-hosting-muted">
        <strong>{t("Hosting_Create_Confidence")}</strong> {statusLabel}.
        {demand.measurementTimestamp && (
          <>
            {" "}
            {formatResource(
              t("Hosting_Create_LatestMeasurement"),
              new Intl.DateTimeFormat(currentLocale(), {
                dateStyle: "medium",
                timeStyle: "short",
              }).format(new Date(demand.measurementTimestamp)),
            )}
          </>
        )}
      </p>
      {demand.missingComponents.length > 0 && (
        <StatePanel kind="warning">
          {formatResource(
            t("Hosting_Create_MissingComponents"),
            demand.missingComponents.join(", "),
          )}
        </StatePanel>
      )}
      {selectedFit?.isSufficient === false && (
        <StatePanel kind="warning">{t("Hosting_Create_SelectedProfileInsufficient")}</StatePanel>
      )}
      {selectedFit?.isSufficient == null && (
        <StatePanel kind="warning">{t("Hosting_Create_SelectedProfileUnknown")}</StatePanel>
      )}
      <h3>{t("Hosting_Create_SelectedProfileCapacity")}</h3>
      {selectedFit ? (
        <article className="next-hosting-storage-fit">
          <strong>{selectedFit.displayName}</strong>
          <span>
            {t("Hosting_Create_IncludedCapacity")}: {formatBytes(selectedFit.includedCapacityBytes)}
          </span>
          <span>
            {t("Hosting_Create_UsableCapacity")}: {formatBytes(selectedFit.usableCapacityBytes)}
          </span>
          <strong>
            {selectedFit.isSufficient === true
              ? t("Hosting_Create_FitSufficient")
              : selectedFit.isSufficient === false
                ? t("Hosting_Create_FitInsufficient")
                : t("Hosting_Create_FitUnknown")}
          </strong>
        </article>
      ) : (
        <p className="next-hosting-muted">{t("Hosting_Create_ProfileCapacityUnavailable")}</p>
      )}
      <details>
        <summary>{t("Hosting_Create_ComponentBreakdown")}</summary>
        <ul className="next-hosting-component-list">
          {demand.components.map((component) => (
            <li key={component.identifier}>
              <span>
                <strong>{component.displayName}</strong> — {component.kind}
              </span>
              <span>
                {formatBytes(component.requiredBytes)} · {String(component.status)} ·{" "}
                {component.source}
              </span>
            </li>
          ))}
        </ul>
      </details>
    </section>
  );
}

export function CreateServerPage() {
  const c = useHostingCopy();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  useDocumentTitle(t("Hosting_Create_Title"));
  const schema = useMemo(
    () =>
      createServerCreationSchema({
        titleRequired: t("Hosting_Create_ValidationNameRequired"),
        titleTooLong: t("Hosting_Create_ValidationNameTooLong"),
        selectionRequired: t("Hosting_Create_ValidationSelectionRequired"),
        invalidCapacity: t("Hosting_Create_ValidationCapacity"),
        invalidIdentifier: t("Hosting_Create_ValidationIdentifier"),
      }),
    [t],
  );
  const form = useForm<ServerCreationFormValues>({
    resolver: zodResolver(schema),
    defaultValues: defaults,
  });
  const [
    gameIdentifier,
    runtimeProviderIdentifier,
    runtimeProfileIdentifier,
    storageStrategy,
    externalStorageSizeGiB,
    modPackDefinitionId,
    terrainId,
    scenarioId,
    configurationProfileId,
    costOwnerId,
  ] = useWatch({
    control: form.control,
    name: [
      "gameIdentifier",
      "runtimeProviderIdentifier",
      "runtimeProfileIdentifier",
      "runtimeStorageStrategy",
      "externalStorageSizeGiB",
      "modPackDefinitionId",
      "terrainId",
      "scenarioId",
      "configurationProfileId",
      "costOwnerId",
    ],
  });
  useUnsavedChanges(form.formState.isDirty);
  const fixedExternalGiB = externalStorageSizeGiB ?? undefined;

  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
    enabled: permissions.createServers,
    staleTime: 60_000,
  });
  const capability = capabilities.data?.find(
    (item) => item.gameIdentifier.toLocaleLowerCase() === gameIdentifier.toLocaleLowerCase(),
  );
  const availableProviders = useMemo(
    () => orderProviders((capability?.runtimeProviderProfiles ?? []).filter(isProviderAvailable)),
    [capability],
  );
  const unavailableProviders = useMemo(
    () =>
      orderProviders(
        (capability?.runtimeProviderProfiles ?? []).filter((item) => !isProviderAvailable(item)),
      ),
    [capability],
  );
  const provider = availableProviders.find(
    (item) => item.identifier.toLocaleLowerCase() === runtimeProviderIdentifier.toLocaleLowerCase(),
  );
  const profile = provider?.profiles.find(
    (item) => item.identifier.toLocaleLowerCase() === runtimeProfileIdentifier.toLocaleLowerCase(),
  );

  useEffect(() => {
    const firstCapability = capabilities.data?.[0];
    if (!firstCapability) return;
    const currentCapability = capabilities.data?.find(
      (item) => item.gameIdentifier.toLocaleLowerCase() === gameIdentifier.toLocaleLowerCase(),
    );
    const nextCapability = currentCapability ?? firstCapability;
    const nextProviders = orderProviders(
      nextCapability.runtimeProviderProfiles.filter(isProviderAvailable),
    );
    const currentProvider = nextProviders.find(
      (item) =>
        item.identifier.toLocaleLowerCase() === runtimeProviderIdentifier.toLocaleLowerCase(),
    );
    const nextProvider = currentProvider ?? nextProviders[0];
    const currentProfile = nextProvider?.profiles.find(
      (item) =>
        item.identifier.toLocaleLowerCase() === runtimeProfileIdentifier.toLocaleLowerCase(),
    );
    const nextProfile = currentProfile ?? nextProvider?.profiles[0];
    if (!currentCapability) form.setValue("gameIdentifier", nextCapability.gameIdentifier);
    if (!currentProvider)
      form.setValue("runtimeProviderIdentifier", nextProvider?.identifier ?? "");
    if (!currentProfile) form.setValue("runtimeProfileIdentifier", nextProfile?.identifier ?? "");
    if (!currentProvider) {
      form.setValue(
        "runtimeStorageStrategy",
        nextProvider?.storage?.supportsExternalBlockStorage ? "AutomaticExternal" : "IncludedDisk",
      );
      form.setValue("externalStorageSizeGiB", null);
    }
  }, [
    capabilities.data,
    form,
    gameIdentifier,
    runtimeProfileIdentifier,
    runtimeProviderIdentifier,
  ]);

  const modPacks = useQuery({
    queryKey: hostingQueryKeys.creationModPacks(gameIdentifier),
    queryFn: ({ signal }) => hostingService.creationModPacks(gameIdentifier, signal),
    enabled:
      permissions.createServers &&
      Boolean(gameIdentifier) &&
      Boolean(capability) &&
      capability?.supportsModPacks !== false,
  });
  const terrains = useQuery({
    queryKey: hostingQueryKeys.terrains(gameIdentifier),
    queryFn: ({ signal }) => hostingService.terrains(gameIdentifier, signal),
    enabled:
      permissions.createServers && Boolean(gameIdentifier) && capability?.requiresTerrain === true,
  });
  const scenarios = useQuery({
    queryKey: hostingQueryKeys.scenarios(gameIdentifier),
    queryFn: ({ signal }) => hostingService.scenarios(gameIdentifier, signal),
    enabled:
      permissions.createServers && Boolean(gameIdentifier) && capability?.requiresScenario === true,
  });
  const configurationProfiles = useQuery({
    queryKey: hostingQueryKeys.profileOptions(gameIdentifier),
    queryFn: ({ signal }) => hostingService.profileOptions(gameIdentifier, signal),
    enabled: permissions.createServers && Boolean(gameIdentifier),
  });
  const storageEstimate = useQuery({
    queryKey: hostingQueryKeys.storageEstimate(
      gameIdentifier,
      runtimeProviderIdentifier,
      modPackDefinitionId,
    ),
    queryFn: ({ signal }) =>
      hostingService.estimateStorage(
        gameIdentifier,
        runtimeProviderIdentifier,
        modPackDefinitionId,
        signal,
      ),
    enabled:
      permissions.createServers && Boolean(gameIdentifier && runtimeProviderIdentifier && profile),
  });
  const costOwners = useQuery({
    queryKey: costOwnerQueryKey(user?.primaryKey ?? "anonymous"),
    queryFn: ({ signal }) => hostingService.creationCostOwners(signal),
    enabled: permissions.createServers && Boolean(user),
    staleTime: 30_000,
  });

  const storageValid = isStorageSelectionValid(
    storageStrategy as StorageStrategy,
    profile,
    storageEstimate.data,
    provider?.storage,
    fixedExternalGiB,
  );
  const capacity = selectedStorageBytes(
    storageStrategy as StorageStrategy,
    profile,
    storageEstimate.data,
    provider?.storage,
    fixedExternalGiB,
  );
  const hourlyCost = combinedHourlyCost(profile, capacity, storageStrategy, provider?.storage);
  const currentUserOwner = costOwners.data?.find((owner) => owner.isCurrentUser);
  const costOwnerValid = isCostOwnerSelectionValid(costOwnerId, costOwners.data);
  const requiredOptionsReady =
    (capability?.supportsModPacks === false || modPacks.isSuccess) &&
    (!capability?.requiresTerrain || terrains.isSuccess) &&
    (!capability?.requiresScenario || scenarios.isSuccess) &&
    configurationProfiles.isSuccess &&
    costOwners.isSuccess;
  const canSubmit =
    Boolean(capability && provider && profile) &&
    storageValid &&
    costOwnerValid &&
    requiredOptionsReady &&
    (!capability?.requiresTerrain || terrainId !== null) &&
    (!capability?.requiresScenario || scenarioId !== null) &&
    !storageEstimate.isFetching;

  const create = useMutation({
    mutationFn: (payload: Parameters<typeof hostingService.create>[0]) =>
      hostingService.create(payload),
  });

  const changeGame = (nextGame: string) => {
    const nextCapability = capabilities.data?.find(
      (item) => item.gameIdentifier.toLocaleLowerCase() === nextGame.toLocaleLowerCase(),
    );
    const nextProvider = orderProviders(
      (nextCapability?.runtimeProviderProfiles ?? []).filter(isProviderAvailable),
    )[0];
    form.setValue("gameIdentifier", nextGame, { shouldDirty: true, shouldValidate: true });
    form.setValue("runtimeProviderIdentifier", nextProvider?.identifier ?? "", {
      shouldDirty: true,
    });
    form.setValue("runtimeProfileIdentifier", nextProvider?.profiles[0]?.identifier ?? "", {
      shouldDirty: true,
    });
    form.setValue("modPackDefinitionId", null, { shouldDirty: true });
    form.setValue("terrainId", null, { shouldDirty: true });
    form.setValue("scenarioId", null, { shouldDirty: true });
    form.setValue("configurationProfileId", null, { shouldDirty: true });
    form.setValue(
      "runtimeStorageStrategy",
      nextProvider?.storage?.supportsExternalBlockStorage ? "AutomaticExternal" : "IncludedDisk",
      { shouldDirty: true },
    );
    form.setValue("externalStorageSizeGiB", null, { shouldDirty: true });
  };

  useEffect(() => {
    if (capability?.requiresTerrain && terrainId === null && terrains.data?.length) {
      const selected =
        terrains.data.find((item) =>
          item.versions.some(
            (version) => version.sourceIdentifier === capability.defaultTerrainSourceIdentifier,
          ),
        ) ?? terrains.data[0];
      if (selected)
        form.setValue("terrainId", Number(selected.primaryKey), { shouldValidate: true });
    }
    if (capability?.requiresScenario && scenarioId === null && scenarios.data?.length) {
      const selected =
        scenarios.data.find((item) =>
          item.versions.some(
            (version) => version.sourceIdentifier === capability.defaultScenarioSourceIdentifier,
          ),
        ) ?? scenarios.data[0];
      if (selected) form.setValue("scenarioId", selected.primaryKey, { shouldValidate: true });
    }
  }, [capability, form, scenarioId, scenarios.data, terrainId, terrains.data]);

  const changeProvider = (identifier: string) => {
    const nextProvider = availableProviders.find(
      (item) => item.identifier.toLocaleLowerCase() === identifier.toLocaleLowerCase(),
    );
    form.setValue("runtimeProviderIdentifier", identifier, {
      shouldDirty: true,
      shouldValidate: true,
    });
    form.setValue("runtimeProfileIdentifier", nextProvider?.profiles[0]?.identifier ?? "", {
      shouldDirty: true,
    });
    form.setValue(
      "runtimeStorageStrategy",
      nextProvider?.storage?.supportsExternalBlockStorage ? "AutomaticExternal" : "IncludedDisk",
      { shouldDirty: true },
    );
    form.setValue("externalStorageSizeGiB", null, { shouldDirty: true });
  };

  const submit = form.handleSubmit(async (values) => {
    if (!canSubmit || create.isPending) return;
    try {
      const server = await create.mutateAsync({
        ...values,
        title: values.title.trim(),
        externalStorageSizeGiB:
          values.runtimeStorageStrategy === "FixedExternal" ? values.externalStorageSizeGiB : null,
        configurationProfileRevisionId: null,
      });
      form.reset(values);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet }),
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications }),
        queryClient.invalidateQueries({ queryKey: costOwnerQueryKey(user?.primaryKey ?? "") }),
      ]);
      navigate(`/command/servers/${server.primaryKey}`);
    } catch {
      // The normalized mutation error is rendered below and the form remains available for correction.
    }
  });

  const retryRequiredOptions = () => {
    void Promise.all([
      capability?.supportsModPacks !== false ? modPacks.refetch() : Promise.resolve(),
      capability?.requiresTerrain ? terrains.refetch() : Promise.resolve(),
      capability?.requiresScenario ? scenarios.refetch() : Promise.resolve(),
      configurationProfiles.refetch(),
    ]);
  };

  return (
    <div className="next-hosting-page">
      <PageHeader title={t("Hosting_Create_Title")} description={t("Hosting_Create_Description")} />
      <Link className="next-hosting-back-link" to="/command/servers">
        ← {t("Hosting_Common_BackToServers")}
      </Link>
      {authLoading ? (
        <HostingLoading />
      ) : !permissions.createServers ? (
        <StatePanel kind="forbidden" title={t("Hosting_Create_ForbiddenTitle")}>
          {t("Hosting_Create_ForbiddenDescription")}
        </StatePanel>
      ) : (
        <>
          {capabilities.isLoading ? (
            <StatePanel kind="loading">{t("Hosting_Create_LoadingCapabilities")}</StatePanel>
          ) : capabilities.isError ? (
            <HostingError
              message={errorMessage(capabilities.error, t("Hosting_Create_LoadError"))}
              onRetry={() => void capabilities.refetch()}
            />
          ) : !capabilities.data?.length ? (
            <StatePanel kind="empty" title={t("Hosting_Create_NoCapabilitiesTitle")}>
              {t("Hosting_Create_NoCapabilitiesDescription")}
            </StatePanel>
          ) : (
            <form
              className="next-hosting-form-grid"
              aria-busy={create.isPending}
              onSubmit={(event) => void submit(event)}
            >
              <section className="next-hosting-panel next-hosting-form">
                <div className="next-hosting-section-heading">
                  <span className="kicker">{t("Hosting_Create_RecordKicker")}</span>
                  <h2>{t("Hosting_Create_RecordTitle")}</h2>
                  <p>{t("Hosting_Create_RecordDescription")}</p>
                </div>
                <label className="field">
                  <span>{t("Hosting_Create_Name")}</span>
                  <input
                    autoComplete="off"
                    maxLength={256}
                    required
                    aria-invalid={Boolean(form.formState.errors.title)}
                    aria-describedby={form.formState.errors.title ? "server-name-error" : undefined}
                    {...form.register("title")}
                  />
                  {form.formState.errors.title && (
                    <small id="server-name-error" className="next-hosting-field-error" role="alert">
                      {form.formState.errors.title.message}
                    </small>
                  )}
                </label>
                <label className="field">
                  <span>{t("Hosting_Create_Game")}</span>
                  <select
                    value={gameIdentifier}
                    required
                    onChange={(event) => changeGame(event.target.value)}
                  >
                    {capabilities.data.map((item) => (
                      <option key={item.gameIdentifier} value={item.gameIdentifier}>
                        {item.displayName}
                      </option>
                    ))}
                  </select>
                </label>
                {capability?.requiresTerrain && (
                  <label className="field">
                    <span>{capability.terrainLabel ?? "Terrain"}</span>
                    <select
                      required
                      value={terrainId ?? ""}
                      disabled={terrains.isLoading || terrains.isError}
                      onChange={(event) =>
                        form.setValue(
                          "terrainId",
                          event.target.value ? Number(event.target.value) : null,
                          {
                            shouldDirty: true,
                            shouldValidate: true,
                          },
                        )
                      }
                    >
                      <option value="" disabled>
                        Select {(capability.terrainLabel ?? "terrain").toLowerCase()}
                      </option>
                      {terrains.data?.map((item) => (
                        <option key={item.primaryKey} value={item.primaryKey}>
                          {item.title}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                {capability?.requiresScenario && (
                  <label className="field">
                    <span>{capability.scenarioLabel ?? "Scenario"}</span>
                    <select
                      required
                      value={scenarioId ?? ""}
                      disabled={scenarios.isLoading || scenarios.isError}
                      onChange={(event) =>
                        form.setValue("scenarioId", event.target.value || null, {
                          shouldDirty: true,
                          shouldValidate: true,
                        })
                      }
                    >
                      <option value="" disabled>
                        Select {(capability.scenarioLabel ?? "scenario").toLowerCase()}
                      </option>
                      {scenarios.data?.map((item) => (
                        <option key={item.primaryKey} value={item.primaryKey}>
                          {item.title}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                {capability && capability.supportsModPacks !== false && (
                  <label className="field">
                    <span>{t("Hosting_Create_ModPack")}</span>
                    <select
                      value={modPackDefinitionId ?? ""}
                      disabled={modPacks.isLoading || modPacks.isError}
                      onChange={(event) =>
                        form.setValue(
                          "modPackDefinitionId",
                          event.target.value ? Number(event.target.value) : null,
                          { shouldDirty: true, shouldValidate: true },
                        )
                      }
                    >
                      <option value="">{t("Hosting_Create_ModPackNone")}</option>
                      {modPacks.data?.map((item) => (
                        <option key={item.primaryKey} value={item.primaryKey}>
                          {item.title}
                        </option>
                      ))}
                    </select>
                    <small>{t("Hosting_Create_ModPackHint")}</small>
                  </label>
                )}
                <label className="field">
                  <span>{t("Hosting_Create_ConfigurationProfile")}</span>
                  <select
                    value={configurationProfileId ?? ""}
                    disabled={configurationProfiles.isLoading || configurationProfiles.isError}
                    onChange={(event) =>
                      form.setValue("configurationProfileId", event.target.value || null, {
                        shouldDirty: true,
                        shouldValidate: true,
                      })
                    }
                  >
                    <option value="">{t("Hosting_Create_ConfigurationProfileDefault")}</option>
                    {configurationProfiles.data?.map((item) => (
                      <option key={item.primaryKey} value={item.primaryKey}>
                        {formatResource(
                          t("Hosting_Create_ConfigurationProfileOption"),
                          item.title,
                          item.latestRevisionNumber,
                        )}
                      </option>
                    ))}
                  </select>
                  <small>{t("Hosting_Create_ConfigurationProfileHint")}</small>
                </label>
                {((capability?.supportsModPacks !== false && modPacks.isLoading) ||
                  (capability?.requiresTerrain && terrains.isLoading) ||
                  (capability?.requiresScenario && scenarios.isLoading) ||
                  configurationProfiles.isLoading) && (
                  <StatePanel kind="loading">{t("Hosting_Create_LoadingSelections")}</StatePanel>
                )}
                {((capability?.supportsModPacks !== false && modPacks.isError) ||
                  (capability?.requiresTerrain && terrains.isError) ||
                  (capability?.requiresScenario && scenarios.isError) ||
                  configurationProfiles.isError) && (
                  <StatePanel
                    kind="error"
                    title={t("Hosting_Create_SelectionLoadErrorTitle")}
                    action={
                      <button
                        className="button button--ghost"
                        type="button"
                        onClick={retryRequiredOptions}
                      >
                        {t("Hosting_Create_Retry")}
                      </button>
                    }
                  >
                    {errorMessage(
                      terrains.error ??
                        scenarios.error ??
                        modPacks.error ??
                        configurationProfiles.error,
                      t("Hosting_Create_SelectionLoadError"),
                    )}
                  </StatePanel>
                )}
                <label className="field">
                  <span>{t("Hosting_Common_CostOwner")}</span>
                  <select
                    value={costOwnerId ?? ""}
                    disabled={!costOwners.isSuccess}
                    aria-describedby="cost-owner-hint"
                    onChange={(event) =>
                      form.setValue("costOwnerId", event.target.value || null, {
                        shouldDirty: true,
                        shouldValidate: true,
                      })
                    }
                  >
                    <option value="" disabled={currentUserOwner?.isFrozen}>
                      {currentUserOwner
                        ? formatResource(
                            t("Hosting_Create_CostOwnerSelfExisting"),
                            currentUserOwner.displayName,
                          )
                        : t("Hosting_Create_CostOwnerSelf")}
                    </option>
                    {costOwners.data
                      ?.filter((owner: ServerCreationCostOwnerOption) => !owner.isCurrentUser)
                      .map((owner) => (
                        <option
                          key={owner.costOwnerId}
                          value={owner.costOwnerId}
                          disabled={owner.isFrozen === true}
                        >
                          {owner.displayName}
                          {owner.isFrozen ? ` — ${t("Hosting_Create_CostOwnerFrozen")}` : ""}
                        </option>
                      ))}
                  </select>
                  <small id="cost-owner-hint">{t("Hosting_Create_CostOwnerHint")}</small>
                </label>
                {costOwners.isLoading && (
                  <p className="next-hosting-muted" role="status">
                    {t("Hosting_Create_CostOwnersLoading")}
                  </p>
                )}
                {costOwners.isError && (
                  <StatePanel
                    kind="warning"
                    action={
                      <button
                        className="button button--ghost"
                        type="button"
                        onClick={() => void costOwners.refetch()}
                      >
                        {t("Hosting_Create_Retry")}
                      </button>
                    }
                  >
                    {t("Hosting_Create_CostOwnerLoadWarning")}
                  </StatePanel>
                )}
                {!costOwnerValid && (
                  <p className="next-hosting-inline-error" role="alert">
                    {t("Hosting_Create_CostOwnerFrozenError")}
                  </p>
                )}
                <label className="field">
                  <span>{t("Hosting_Create_Runtime")}</span>
                  {availableProviders.length > 0 ? (
                    <select
                      value={runtimeProviderIdentifier}
                      required
                      onChange={(event) => changeProvider(event.target.value)}
                    >
                      {availableProviders.map((item) => (
                        <option key={item.identifier} value={item.identifier}>
                          {item.identifier}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <div className="next-hosting-provider-empty">
                      {t("Hosting_ProfileCatalog_NoSelectableProvider")}
                    </div>
                  )}
                  <small>{t("Hosting_Create_RuntimeHint")}</small>
                </label>
                {provider && provider.profiles.length > 0 && (
                  <>
                    <label className="field">
                      <span>{t("Hosting_Create_Profile")}</span>
                      <select
                        value={runtimeProfileIdentifier}
                        required
                        onChange={(event) =>
                          form.setValue("runtimeProfileIdentifier", event.target.value, {
                            shouldDirty: true,
                            shouldValidate: true,
                          })
                        }
                      >
                        {provider.profiles.map((item) => (
                          <option key={item.identifier} value={item.identifier}>
                            {item.displayName} — {number(item.virtualCpuCount, 0)} vCPU ·{" "}
                            {number(item.memoryGiB)} GiB · {number(item.diskGiB, 0)} GiB ·{" "}
                            {item.estimatedHourlyCostEur == null
                              ? t("Hosting_Create_PriceUnavailable")
                              : `${euro(item.estimatedHourlyCostEur, 4)} / ${t("Hosting_Create_Hour")}`}
                          </option>
                        ))}
                      </select>
                      <small>{t("Hosting_ProfileCatalog_SelectHint")}</small>
                    </label>
                    {profile && <RuntimeProfileSummary profile={profile} />}
                  </>
                )}
                {profile && provider && (
                  <section
                    className="next-hosting-storage-configuration"
                    aria-labelledby="storage-title"
                  >
                    <div className="next-hosting-section-heading">
                      <span className="kicker">{t("Hosting_Create_StorageKicker")}</span>
                      <h3 id="storage-title">
                        {formatResource(t("Hosting_Create_StorageTitle"), profile.displayName)}
                      </h3>
                      <p>{t("Hosting_Create_StorageDescription")}</p>
                    </div>
                    <label className="field">
                      <span>{t("Hosting_Create_StorageStrategy")}</span>
                      <select
                        value={storageStrategy}
                        onChange={(event) => {
                          const strategy = event.target.value as StorageStrategy;
                          form.setValue("runtimeStorageStrategy", strategy, {
                            shouldDirty: true,
                            shouldValidate: true,
                          });
                          form.setValue(
                            "externalStorageSizeGiB",
                            strategy === "FixedExternal"
                              ? (provider.storage?.minimumCapacityGiB ?? null)
                              : null,
                            { shouldDirty: true, shouldValidate: true },
                          );
                        }}
                      >
                        <option value="IncludedDisk">{t("Hosting_Create_StorageIncluded")}</option>
                        {provider.storage?.supportsExternalBlockStorage && (
                          <>
                            <option value="AutomaticExternal">
                              {t("Hosting_Create_StorageAutomatic")}
                            </option>
                            <option value="FixedExternal">
                              {t("Hosting_Create_StorageFixed")}
                            </option>
                          </>
                        )}
                      </select>
                    </label>
                    {storageStrategy === "FixedExternal" && provider.storage && (
                      <label className="field">
                        <span>{t("Hosting_Create_ExternalCapacity")}</span>
                        <input
                          type="number"
                          required
                          min={provider.storage.minimumCapacityGiB}
                          max={provider.storage.maximumCapacityGiB}
                          step={provider.storage.capacityIncrementGiB}
                          value={fixedExternalGiB ?? ""}
                          aria-invalid={Boolean(
                            storageEstimate.data && !storageValid && fixedExternalGiB,
                          )}
                          onChange={(event) =>
                            form.setValue(
                              "externalStorageSizeGiB",
                              event.target.value ? Number(event.target.value) : null,
                              { shouldDirty: true, shouldValidate: true },
                            )
                          }
                        />
                      </label>
                    )}
                    <dl className="next-hosting-facts next-hosting-facts--compact">
                      <div>
                        <dt>{t("Hosting_Create_IncludedProfileDisk")}</dt>
                        <dd>
                          {profile.diskGiB == null ? "—" : `${number(profile.diskGiB, 0)} GiB`}
                        </dd>
                      </div>
                      <div>
                        <dt>{t("Hosting_Create_EstimatedPeakRequirement")}</dt>
                        <dd>
                          {storageEstimate.isFetching
                            ? t("Hosting_Create_Calculating")
                            : formatBytes(storageEstimate.data?.peakPreparationRequiredBytes)}
                        </dd>
                      </div>
                      <div>
                        <dt>
                          {t(
                            storageStrategy === "IncludedDisk"
                              ? "Hosting_Create_SelectedDeploymentCapacity"
                              : "Hosting_Storage_SelectedRawCapacity",
                          )}
                        </dt>
                        <dd>{formatBytes(capacity)}</dd>
                      </div>
                      <div>
                        <dt>{t("Hosting_Create_CombinedPrice")}</dt>
                        <dd>
                          {hourlyCost == null
                            ? t("Hosting_Create_PriceUnavailable")
                            : formatResource(
                                t("Hosting_Create_CombinedPriceValue"),
                                euro(hourlyCost, 6),
                                euro(hourlyCost * 730, 2),
                              )}
                        </dd>
                      </div>
                    </dl>
                    <p className="next-hosting-muted">
                      {storageStrategy === "IncludedDisk"
                        ? t("Hosting_Create_FilesystemIncluded")
                        : formatResource(
                            t("Hosting_Create_FilesystemExternal"),
                            provider.storage?.deploymentMountPath ?? "—",
                          )}
                    </p>
                    {storageStrategy !== "IncludedDisk" && (
                      <StatePanel kind="warning">
                        {t("Hosting_Create_ExternalStorageWarning")}
                      </StatePanel>
                    )}
                  </section>
                )}
                {storageEstimate.isFetching && (
                  <StatePanel kind="loading">{t("Hosting_Create_EstimateLoading")}</StatePanel>
                )}
                {storageEstimate.isError && (
                  <StatePanel
                    kind="warning"
                    action={
                      <button
                        className="button button--ghost"
                        type="button"
                        onClick={() => void storageEstimate.refetch()}
                      >
                        {t("Hosting_Create_Retry")}
                      </button>
                    }
                  >
                    {formatResource(
                      t("Hosting_Create_EstimateUnavailable"),
                      errorMessage(storageEstimate.error, t("Hosting_Create_EstimateFallback")),
                    )}
                  </StatePanel>
                )}
                {storageEstimate.data && !storageValid && !storageEstimate.isFetching && (
                  <StatePanel kind="warning">{t("Hosting_Create_InvalidStorage")}</StatePanel>
                )}
                {storageEstimate.data && (
                  <StorageDemandSummary
                    demand={storageEstimate.data}
                    selectedProfileIdentifier={runtimeProfileIdentifier}
                  />
                )}
                {unavailableProviders.length > 0 && (
                  <section
                    className="next-hosting-provider-statuses"
                    aria-label={t("Hosting_Create_ProviderAvailability")}
                  >
                    {unavailableProviders.map((item: RuntimeProvider) => (
                      <article key={item.identifier}>
                        <strong>{item.identifier}</strong>
                        <span>
                          {item.availabilityStatus === "paused"
                            ? c.executionPaused
                            : formatResource(
                                t(providerStatusKey(item.availabilityStatus)),
                                item.identifier,
                              )}
                        </span>
                      </article>
                    ))}
                  </section>
                )}
                <label className="next-hosting-check">
                  <input type="checkbox" {...form.register("isLaunchDisabled")} />
                  <span>
                    <strong>{t("Hosting_Create_LaunchDisabled")}</strong>
                    <small>{t("Hosting_Create_LaunchDisabledHint")}</small>
                  </span>
                </label>
                {create.error && (
                  <p className="next-hosting-inline-error" role="alert">
                    {errorMessage(create.error, t("Hosting_Create_SaveError"))}
                  </p>
                )}
                <div className="next-hosting-form-actions">
                  <Link className="button button--ghost" to="/command/servers">
                    {t("Hosting_Common_Cancel")}
                  </Link>
                  <button
                    className="button button--primary"
                    type="submit"
                    disabled={create.isPending || !canSubmit}
                  >
                    {create.isPending ? t("Hosting_Create_Saving") : t("Hosting_Create_Submit")}
                  </button>
                </div>
              </section>
              <aside className="next-hosting-create-aside">
                <section className="next-hosting-panel next-hosting-capability-card">
                  <span className="kicker">{t("Hosting_Create_CapabilityKicker")}</span>
                  <h2>{capability?.displayName}</h2>
                  <p>{capability?.description}</p>
                  {capability && capability.actions.length > 0 && (
                    <div className="next-hosting-capability-actions">
                      {[...capability.actions]
                        .sort((left, right) => left.order - right.order)
                        .map((action) => (
                          <span key={action.id} title={action.description}>
                            {action.label}
                          </span>
                        ))}
                    </div>
                  )}
                </section>
                <section className="next-hosting-create-boundary">
                  <strong>{t("Hosting_Create_SeparateTitle")}</strong>
                  <p>{t("Hosting_Create_SeparateDescription")}</p>
                  <ol>
                    <li>{t("Hosting_Create_SeparateStepCreate")}</li>
                    <li>{t("Hosting_Create_SeparateStepReview")}</li>
                    <li>{t("Hosting_Create_SeparateStepStart")}</li>
                  </ol>
                </section>
              </aside>
            </form>
          )}
        </>
      )}
    </div>
  );
}
