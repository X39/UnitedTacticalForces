import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { flushSync } from "react-dom";
import { useForm, useWatch, type Resolver } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { ActionLink } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { Breadcrumbs } from "@/components/Breadcrumbs";
import { SelectField, TextField } from "@/components/FormFields";
import { PageHeader, useUnsavedChanges } from "@/components/primitives";
import { AfterCreatingChoice, type AfterCreating } from "@/components/server/AfterCreatingChoice";
import { AdvancedOptions, type OptionListState } from "@/components/server/AdvancedOptions";
import { ApiText } from "@/components/server/ApiText";
import { CostOwnerLine, costOwnerDisplayName } from "@/components/server/CostOwnerLine";
import { CreateSummary } from "@/components/server/CreateSummary";
import { PresetField } from "@/components/server/PresetField";
import { SizeField } from "@/components/server/SizeField";
import { StorageOptions } from "@/components/server/StorageOptions";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import "@/components/server/create.css";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  createFieldOrder,
  advancedCreateFields,
  createServerCreationSchema,
  deriveCreateReadiness,
  estimateCreateCost,
  invalidCreateFields,
  isProviderAvailable,
  isStorageMisfit,
  orderProviders,
  presetSelectValue,
  projectCreatePresets,
  resolveCreateDefaults,
  type CreateField,
  type CreatePreset,
  type ServerCreationFormValues,
} from "@/features/hosting/createServerModel";
import { createStartState } from "@/features/hosting/createStartIntent";
import { formatBytes, formatHourlyRate, selectedStorageBytes } from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { ServerCapability } from "@/features/hosting/types";

/** The form values before the defaults are resolved: every field empty or off. */
const emptyValues: ServerCreationFormValues = {
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

/**
 * Builds the query key of the cost owner choices of one member.
 *
 * @param userId The signed-in user's id.
 * @returns The query key.
 */
const costOwnerQueryKey = (userId: string) =>
  ["hosting", "creation", "cost-owners", userId] as const;

/**
 * Finds the capability of a game identifier, ignoring case.
 *
 * @param capabilities The loaded capabilities, if any.
 * @param gameIdentifier The identifier to look up.
 * @returns The matching capability, or undefined.
 */
function findCapability(
  capabilities: readonly ServerCapability[] | undefined,
  gameIdentifier: string,
): ServerCapability | undefined {
  return capabilities?.find(
    (item) => item.gameIdentifier.toLocaleLowerCase() === gameIdentifier.toLocaleLowerCase(),
  );
}

/**
 * Reads the state of an option list query for the advanced options.
 *
 * @param query The query of one option list.
 * @returns The data, whether the first load is running and whether it failed without data.
 */
function optionList<T>(query: {
  data: T[] | undefined;
  isLoading: boolean;
  isError: boolean;
}): OptionListState<T> {
  return {
    data: query.data,
    loading: query.isLoading,
    failed: query.isError && query.data === undefined,
  };
}

/**
 * The guided create page: name a server, check the size and the live cost, and either keep it off or start it.
 *
 * The page is composition and form state only. Every rule (defaults, readiness, cost) lives in the create model and
 * every piece of presentation in the create components. A member who only types a name can create, because every
 * other value is resolved from the capabilities and the option lists without making the form dirty; a valid choice
 * of the member is never overwritten.
 */
export function CreateServerPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { showToast } = useAppToast();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  useDocumentTitle(t("Server_Create_Title"));

  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
    enabled: permissions.createServers,
    staleTime: 60_000,
  });

  // The schema depends on the capability and provider the values name, so the resolver derives its
  // requirements from the values it validates instead of from state that is only known after useForm.
  const resolver: Resolver<ServerCreationFormValues> = (values, context, options) => {
    const requested = findCapability(capabilities.data, values.gameIdentifier);
    const requestedProvider = requested?.runtimeProviderProfiles.find(
      (item) =>
        item.identifier.toLocaleLowerCase() ===
        values.runtimeProviderIdentifier.toLocaleLowerCase(),
    );
    const schema = createServerCreationSchema(
      {
        titleRequired: t("Server_Create_Name_Required"),
        titleTooLong: t("Server_Create_Name_TooLong"),
        selectionRequired: t("Hosting_Create_ValidationSelectionRequired"),
        invalidCapacity: t("Hosting_Create_ValidationCapacity"),
        invalidIdentifier: t("Hosting_Create_ValidationIdentifier"),
      },
      {
        requiresTerrain: requested?.requiresTerrain === true,
        terrainRequired: t("Server_Create_Content_Required", {
          label: requested?.terrainLabel ?? t("Server_Overview_Terrain"),
        }),
        requiresScenario: requested?.requiresScenario === true,
        scenarioRequired: t("Server_Create_Content_Required", {
          label: requested?.scenarioLabel ?? t("Server_Overview_Scenario"),
        }),
        storage: requestedProvider?.storage,
      },
    );
    return zodResolver(schema)(values, context, options);
  };
  const form = useForm<ServerCreationFormValues>({
    resolver,
    defaultValues: emptyValues,
    mode: "onBlur",
    shouldFocusError: false,
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
    isLaunchDisabled,
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
      "isLaunchDisabled",
    ],
  });
  useUnsavedChanges(form.formState.isDirty);

  const [touched, setTouched] = useState<ReadonlySet<CreateField>>(() => new Set());
  const [afterCreating, setAfterCreating] = useState<AfterCreating>("keep-off");
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [announcement, setAnnouncement] = useState("");
  const [advancedForced, setAdvancedForced] = useState(false);
  const [requestFailed, setRequestFailed] = useState(false);
  const errorRef = useRef<HTMLParagraphElement>(null);
  // Set while a create request runs, so a second activation before the next render cannot create twice.
  const creating = useRef(false);
  // The hourly figure last announced (or first seen), so only a changed value is announced.
  const announcedHourly = useRef<number | undefined>(undefined);

  /** Records that the member decided a field by hand, so a default never replaces the choice. */
  const markTouched = useCallback((field: CreateField) => {
    setTouched((previous) => (previous.has(field) ? previous : new Set(previous).add(field)));
  }, []);

  const capability = findCapability(capabilities.data, gameIdentifier);
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
  // The estimate needs only the game and the provider; it does not wait for a resolved size.
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
      permissions.createServers && Boolean(gameIdentifier) && Boolean(runtimeProviderIdentifier),
  });
  const costOwners = useQuery({
    queryKey: costOwnerQueryKey(user?.primaryKey ?? "anonymous"),
    queryFn: ({ signal }) => hostingService.creationCostOwners(signal),
    enabled: permissions.createServers && Boolean(user),
    staleTime: 30_000,
  });

  // Resolve every default without marking the form dirty. A field that has no input of its own (the game
  // with a single capability, the provider with a single provider) is registered first so resetField reaches it.
  useEffect(() => {
    if (!capabilities.data) return;
    const current = form.getValues();
    const next = resolveCreateDefaults({
      capabilities: capabilities.data,
      current,
      touched,
      terrains: terrains.data,
      scenarios: scenarios.data,
      modPacks: modPacks.data,
      profiles: configurationProfiles.data,
      storageDemand: storageEstimate.data,
      owners: costOwners.data,
    });
    const apply = (field: CreateField, value: ServerCreationFormValues[CreateField]) => {
      form.register(field);
      // The value comes from resolveCreateDefaults and already has the type of its own field.
      form.resetField(field, { defaultValue: value as never });
    };
    for (const field of createFieldOrder) {
      if (!Object.is(current[field], next[field])) apply(field, next[field]);
    }
  }, [
    capabilities.data,
    configurationProfiles.data,
    costOwners.data,
    form,
    modPacks.data,
    scenarios.data,
    storageEstimate.data,
    terrains.data,
    touched,
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
  ]);

  const strategy = storageStrategy;
  const capacityBytes = selectedStorageBytes(
    strategy,
    profile,
    storageEstimate.data,
    provider?.storage,
    externalStorageSizeGiB ?? undefined,
  );
  const storageMisfit = isStorageMisfit({
    strategy,
    profile,
    demand: storageEstimate.data,
    storage: provider?.storage,
    fixedExternalGiB: externalStorageSizeGiB,
  });
  const estimate = estimateCreateCost({
    profile,
    strategy,
    capacityBytes,
    storage: provider?.storage,
    estimateFetching: storageEstimate.isFetching,
  });
  const terrainRequired = capability?.requiresTerrain === true;
  const scenarioRequired = capability?.requiresScenario === true;
  const terrainMissing = terrainRequired && terrains.data !== undefined && terrainId === null;
  const scenarioMissing = scenarioRequired && scenarios.data !== undefined && scenarioId === null;
  const forceAdvanced = storageMisfit || terrainMissing || scenarioMissing;
  // Something inside Advanced options needs attention: open it once when that starts, so the member can close it
  // again afterwards. This is the guarded adjust-state-while-rendering pattern, never an effect.
  if (forceAdvanced !== advancedForced) {
    setAdvancedForced(forceAdvanced);
    if (forceAdvanced) setAdvancedOpen(true);
  }
  const readiness = deriveCreateReadiness({
    costOwnerId,
    owners: costOwners.data,
    ownersFailed: costOwners.isError && costOwners.data === undefined,
    requiredOptionsLoading:
      (terrainRequired && terrains.data === undefined && !terrains.isError) ||
      (scenarioRequired && scenarios.data === undefined && !scenarios.isError),
    requiredOptionsFailed:
      (terrainRequired && terrains.isError && terrains.data === undefined) ||
      (scenarioRequired && scenarios.isError && scenarios.data === undefined),
    provider,
    profile,
    estimateFetching: storageEstimate.isFetching,
    estimateFailed: storageEstimate.isError && storageEstimate.data === undefined,
    storageMisfit,
  });

  // A required terrain or scenario that has no value shows its error at once instead of waiting for a submit.
  useEffect(() => {
    const missing: ("terrainId" | "scenarioId")[] = [];
    if (terrainMissing) missing.push("terrainId");
    if (scenarioMissing) missing.push("scenarioId");
    if (missing.length > 0) void form.trigger(missing);
  }, [form, terrainMissing, scenarioMissing]);

  // Announce a changed hourly figure once, 600ms after the last change. The first known figure is only recorded.
  const hourly = estimate.kind === "known" ? estimate.hourly : undefined;
  useEffect(() => {
    if (hourly === undefined) return undefined;
    if (announcedHourly.current === undefined) {
      announcedHourly.current = hourly;
      return undefined;
    }
    if (announcedHourly.current === hourly) return undefined;
    const timer = setTimeout(() => {
      announcedHourly.current = hourly;
      setAnnouncement(t("Server_Create_Cost_Announce", { hourly: formatHourlyRate(hourly) }));
    }, 600);
    return () => clearTimeout(timer);
  }, [hourly, t]);

  const create = useMutation({
    mutationFn: (payload: Parameters<typeof hostingService.create>[0]) =>
      hostingService.create(payload),
  });

  const finishCreate = async (values: ServerCreationFormValues) => {
    if (readiness !== null || create.isPending || creating.current) return;
    creating.current = true;
    setRequestFailed(false);
    const startAfterCreate = afterCreating === "start" && !values.isLaunchDisabled;
    try {
      const server = await create.mutateAsync({
        ...values,
        title: values.title.trim(),
        externalStorageSizeGiB:
          values.runtimeStorageStrategy === "FixedExternal" ? values.externalStorageSizeGiB : null,
        configurationProfileRevisionId: null,
      });
      // Reset the baseline and render it before navigating, so leaving the page after a successful create
      // is never "unsaved changes", even when the invalidations below settle at once.
      flushSync(() => form.reset(values));
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet }),
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications }),
        queryClient.invalidateQueries({ queryKey: costOwnerQueryKey(user?.primaryKey ?? "") }),
      ]);
      showToast(
        t(startAfterCreate ? "Server_Create_Toast_Start" : "Server_Create_Toast_KeepOff", {
          name: values.title.trim(),
        }),
        "success",
      );
      // The workspace opens the shared start confirmation; this page never requests one itself.
      void navigate(
        `/command/servers/${server.primaryKey}`,
        startAfterCreate ? { state: createStartState } : undefined,
      );
    } catch {
      // The summary shows the fixed request error and the form stays available for correction. The owners are
      // loaded again so an owner frozen after this page loaded shows its early block (D-06).
      flushSync(() => setRequestFailed(true));
      errorRef.current?.focus();
      void costOwners.refetch();
    } finally {
      creating.current = false;
    }
  };
  /**
   * Moves focus to the first invalid field in DOM order. A field inside the collapsed Advanced options is revealed
   * first, so the focus never lands on something the member cannot see (RESEARCH Pitfall 3).
   */
  const focusFirstInvalid = (invalid: Partial<Record<CreateField, unknown>>) => {
    const first = invalidCreateFields(invalid)[0];
    if (first === undefined) return;
    if (advancedCreateFields.has(first)) flushSync(() => setAdvancedOpen(true));
    form.setFocus(first);
  };
  const submit = form.handleSubmit(finishCreate, focusFirstInvalid);

  /** Switching the game drops every value that belonged to the old game, so the defaults resolve again. */
  const changeGame = (identifier: string) => {
    const next = findCapability(capabilities.data, identifier);
    if (next !== undefined) {
      setAnnouncement(t("Server_Create_Game_Announce", { game: next.displayName }));
    }
    setTouched((previous) => {
      const next = new Set(previous);
      for (const field of createFieldOrder) {
        if (field !== "title" && field !== "costOwnerId") next.delete(field);
      }
      return next.add("gameIdentifier");
    });
    const options = { shouldDirty: true };
    form.setValue("runtimeProviderIdentifier", "", options);
    form.setValue("runtimeProfileIdentifier", "", options);
    form.setValue("runtimeStorageStrategy", "IncludedDisk", options);
    form.setValue("externalStorageSizeGiB", null, options);
    form.setValue("modPackDefinitionId", null, options);
    form.setValue("terrainId", null, options);
    form.setValue("scenarioId", null, options);
    form.setValue("configurationProfileId", null, options);
  };

  /** Switching the provider drops its size and storage, so the defaults of the new provider resolve. */
  const changeProvider = () => {
    setTouched((previous) => {
      const next = new Set(previous);
      next.delete("runtimeProfileIdentifier");
      next.delete("runtimeStorageStrategy");
      next.delete("externalStorageSizeGiB");
      return next.add("runtimeProviderIdentifier");
    });
  };

  /**
   * Applies a preset: it sets the configuration profile or mod pack it stands for and forces Keep it off (D-02).
   * A preset never starts anything, so it adds no blocking reason.
   */
  const choosePreset = (
    selection: Pick<ServerCreationFormValues, "configurationProfileId" | "modPackDefinitionId">,
    preset: CreatePreset,
  ) => {
    const options = { shouldDirty: true, shouldValidate: true };
    form.setValue("configurationProfileId", selection.configurationProfileId, options);
    form.setValue("modPackDefinitionId", selection.modPackDefinitionId, options);
    markTouched("configurationProfileId");
    markTouched("modPackDefinitionId");
    setAfterCreating("keep-off");
    setAnnouncement(
      t("Server_Create_Preset_Announce", {
        name: preset.kind === "default" ? t("Server_Create_Preset_Default") : preset.title,
      }),
    );
  };

  /** Turning launching off forces the server to stay off; turning it back on changes nothing. */
  const changeLaunchDisabled = (disabled: boolean) => {
    if (disabled) setAfterCreating("keep-off");
  };

  /** Opens Advanced options and moves focus to the cost owner select, which only exists once it is open. */
  const changeCostOwner = () => {
    flushSync(() => setAdvancedOpen(true));
    form.setFocus("costOwnerId");
  };

  const retryOptions = () => {
    if (capability?.supportsModPacks !== false) void modPacks.refetch();
    if (terrainRequired) void terrains.refetch();
    if (scenarioRequired) void scenarios.refetch();
    void configurationProfiles.refetch();
  };

  const presets = projectCreatePresets(configurationProfiles.data, modPacks.data);
  const presetValue = presetSelectValue({ configurationProfileId, modPackDefinitionId }, presets);
  const presetsLoading = configurationProfiles.isLoading || modPacks.isLoading;
  const presetsFailed =
    (configurationProfiles.isError && configurationProfiles.data === undefined) ||
    (modPacks.isError && modPacks.data === undefined);

  const errors = form.formState.errors;
  const ownerName = costOwnerDisplayName(costOwnerId, costOwners.data, user?.nickname);
  const strategyLabelKey: TranslationKey =
    strategy === "AutomaticExternal"
      ? "Hosting_Create_StorageAutomatic"
      : strategy === "FixedExternal"
        ? "Hosting_Create_StorageFixed"
        : "Hosting_Create_StorageIncluded";
  const storageLabel =
    capacityBytes === undefined
      ? t(strategyLabelKey)
      : t("Server_Create_Cost_StorageValue", {
          strategy: t(strategyLabelKey),
          capacity: formatBytes(capacityBytes),
        });

  return (
    <div className="next-hosting-page">
      <Breadcrumbs
        label={t("Server_Workspace_Breadcrumb_Label")}
        items={[
          { label: t("Hosting_Servers_Title"), to: "/command/servers" },
          { label: t("Server_Create_Title") },
        ]}
      />
      <PageHeader title={t("Server_Create_Title")} description={t("Server_Create_Description")} />
      {authLoading ? (
        <LoadingState />
      ) : !permissions.createServers ? (
        <ForbiddenState title={t("Hosting_Create_ForbiddenTitle")}>
          {t("Hosting_Create_ForbiddenDescription")}
        </ForbiddenState>
      ) : capabilities.isLoading ? (
        <LoadingState label={t("Hosting_Create_LoadingCapabilities")} />
      ) : capabilities.isError ? (
        <ErrorState
          onRetry={() => void capabilities.refetch()}
          retryLabel={t("Server_Create_Retry")}
        >
          {t("Hosting_Create_LoadError")}
        </ErrorState>
      ) : !capabilities.data?.length ? (
        <EmptyState title={t("Hosting_Create_NoCapabilitiesTitle")}>
          {t("Hosting_Create_NoCapabilitiesDescription")}
        </EmptyState>
      ) : (
        <form
          className="create-page"
          noValidate
          aria-busy={create.isPending}
          onSubmit={(event) => void submit(event)}
        >
          <p className="sr-only" aria-live="polite">
            {announcement}
          </p>
          <div className="create-page__main" inert={create.isPending}>
            <section className="create-page__section">
              <h2 className="create-section__title">{t("Server_Create_Section_Server")}</h2>
              <TextField
                label={t("Hosting_Create_Name")}
                hint={t("Server_Create_Name_Hint")}
                required
                autoComplete="off"
                maxLength={256}
                error={errors.title?.message}
                {...form.register("title")}
              />
              {capabilities.data.length > 1 ? (
                <SelectField
                  label={t("Hosting_Create_Game")}
                  options={capabilities.data.map((item) => ({
                    value: item.gameIdentifier,
                    label: item.displayName,
                  }))}
                  {...form.register("gameIdentifier", {
                    onChange: (event: { target: { value: string } }) =>
                      changeGame(event.target.value),
                  })}
                />
              ) : (
                <dl className="create-page__fact">
                  <dt>{t("Hosting_Create_Game")}</dt>
                  <dd>
                    <ApiText>{capabilities.data[0]?.displayName}</ApiText>
                  </dd>
                </dl>
              )}
              {capability?.description && (
                <p className="create-page__note">
                  <ApiText>{capability.description}</ApiText>
                </p>
              )}
              <PresetField
                presets={presets}
                value={presetValue}
                loading={presetsLoading}
                failed={presetsFailed}
                showAppliedLine={afterCreating === "keep-off"}
                onRetry={retryOptions}
                onChoose={choosePreset}
              />
              <SizeField
                key={`${gameIdentifier}:${provider?.identifier ?? ""}`}
                form={form}
                profiles={provider?.profiles ?? []}
                selected={profile}
                strategy={strategy}
                demand={storageEstimate.data}
                onMemberChange={markTouched}
              />
              <CostOwnerLine
                owners={costOwners.data}
                costOwnerId={costOwnerId}
                viewerName={user?.nickname}
                loading={costOwners.isLoading}
                failed={costOwners.isError && costOwners.data === undefined}
                onRetry={() => void costOwners.refetch()}
                onChange={changeCostOwner}
              />
            </section>
            {capability && (
              <AdvancedOptions
                form={form}
                open={advancedOpen}
                onOpenChange={setAdvancedOpen}
                capability={capability}
                availableProviders={availableProviders}
                unavailableProviders={unavailableProviders}
                storage={
                  <StorageOptions
                    key={`${gameIdentifier}:${provider?.identifier ?? ""}`}
                    form={form}
                    provider={provider}
                    profile={profile}
                    demand={storageEstimate.data}
                    estimateFetching={storageEstimate.isFetching}
                    estimateFailed={storageEstimate.isError && storageEstimate.data === undefined}
                    misfit={storageMisfit}
                    onRetryEstimate={() => void storageEstimate.refetch()}
                    onMemberChange={markTouched}
                  />
                }
                modPacks={optionList(modPacks)}
                terrains={optionList(terrains)}
                scenarios={optionList(scenarios)}
                profiles={optionList(configurationProfiles)}
                owners={costOwners.data}
                onRetryOptions={retryOptions}
                onMemberChange={markTouched}
                onProviderChange={changeProvider}
                onLaunchDisabledChange={changeLaunchDisabled}
              />
            )}
            <AfterCreatingChoice
              value={afterCreating}
              onChange={setAfterCreating}
              startDisabled={isLaunchDisabled}
              disabled={create.isPending}
            />
            <ActionLink variant="ghost" to="/command/servers">
              {t("Server_Create_Leave")}
            </ActionLink>
          </div>
          <CreateSummary
            estimate={estimate}
            sizeLabel={profile?.displayName}
            storageLabel={storageLabel}
            ownerLabel={ownerName}
            reason={readiness}
            reasonOwnerName={ownerName}
            afterCreating={isLaunchDisabled ? "keep-off" : afterCreating}
            pending={create.isPending}
            invalidCount={form.formState.submitCount > 0 ? invalidCreateFields(errors).length : 0}
            requestFailed={requestFailed}
            errorRef={errorRef}
          />
        </form>
      )}
    </div>
  );
}
