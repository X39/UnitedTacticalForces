import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageEvents } from "@/auth/claims";
import {
  Markdown,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { EventEntityPicker } from "@/features/admin/EventEntityPicker";
import { EventSlotPlanner } from "@/features/admin/EventSlotPlanner";
import {
  createEventForm,
  createEventFormSchema,
  eventAuthoringService,
  validateSlot,
  type EventFormValues,
  type EventPerson,
  type EventSlotDraft,
} from "@/features/admin/eventAuthoring";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import {
  latestActiveRevision,
  modPackService,
  type FullModPackDefinition,
} from "@/features/admin/modPacks";
import type { MediaAsset } from "@/features/admin/siteImages";
import { loadTerrainWindow, type Terrain } from "@/features/admin/terrains";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameContentOption } from "@/features/hosting/types";

const finalStep = 6;

function selectedModPackLabel(modPack: FullModPackDefinition | null): string {
  return modPack?.title ?? "";
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const defaults = useMemo(() => createEventForm(), []);
  const schema = useMemo(
    () =>
      createEventFormSchema(
        {
          titleRequired: t("React_EventAuthoring_TitleRequired"),
          titleTooLong: t("React_EventAuthoring_TitleTooLong"),
          briefingRequired: t("React_EventAuthoring_BriefingRequired"),
          scheduleRequired: t("React_EventAuthoring_ScheduleRequired"),
          hostRequired: t("React_EventAuthoring_HostRequired"),
          terrainRequired: t("React_EventAuthoring_TerrainRequired"),
          modPackRequired: t("React_EventAuthoring_ModPackRequired"),
          imageRequired: t("React_EventAuthoring_ImageRequired"),
          minimumInvalid: t("React_EventAuthoring_MinimumInvalid"),
        },
      ),
    [t],
  );
  const form = useForm<EventFormValues>({
    resolver: zodResolver(schema),
    defaultValues: defaults,
    mode: "onBlur",
  });
  const watched = useWatch({ control: form.control });
  const [step, setStep] = useState(1);
  const [host, setHost] = useState<EventPerson | null>(null);
  const [terrain, setTerrain] = useState<Terrain | null>(null);
  const [scenario, setScenario] = useState<GameContentOption | null>(null);
  const [modPack, setModPack] = useState<FullModPackDefinition | null>(null);
  const [slots, setSlots] = useState<EventSlotDraft[]>([]);
  const [slotsDirty, setSlotsDirty] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<MediaAsset>();
  const [notice, setNotice] = useState<string>();
  const [slotErrors, setSlotErrors] = useState<string[]>([]);
  const canCreate = canManageEvents(user);
  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
  });
  const dirty = form.formState.isDirty || slotsDirty;
  useUnsavedChanges(dirty && !form.formState.isSubmitSuccessful);

  const createMutation = useMutation({
    mutationFn: async (nextValues: EventFormValues) => {
      const eventId = await eventAuthoringService.create(nextValues);
      let slotSetupFailed = false;
      for (const slot of slots) {
        try {
          await eventAuthoringService.createSlot(eventId, slot);
        } catch {
          slotSetupFailed = true;
        }
      }
      return { eventId, slotSetupFailed };
    },
    onSuccess: ({ eventId, slotSetupFailed }) => {
      form.reset(form.getValues());
      setSlotsDirty(false);
      navigate(`/command/events/${eventId}${slotSetupFailed ? "?slotSetup=failed" : ""}`, {
        replace: true,
      });
    },
    onError: () => setNotice(t("Pages_Events_Create_ErrorCreateFailed")),
  });

  const updateSlots = (next: EventSlotDraft[]) => {
    setSlots(next);
    setSlotsDirty(true);
    setSlotErrors([]);
  };
  const stageAssignment = (slot: EventSlotDraft, person: EventPerson | null) =>
    updateSlots(
      slots.map((item) =>
        item.draftKey === slot.draftKey
          ? {
              ...item,
              assignedToFk: person?.primaryKey ?? "",
              assignedUser: person,
              assignmentCandidate: person,
            }
          : item,
      ),
    );
  const validateAndCreate = form.handleSubmit((nextValues) => {
    const errors = slots.flatMap((slot) =>
      validateSlot(slot, {
        groupRequired: t("React_EventAuthoring_SlotGroupRequired"),
        titleRequired: t("React_EventAuthoring_SlotTitleRequired"),
      }),
    );
    setSlotErrors([...new Set(errors)]);
    if (errors.length) {
      setStep(5);
      return;
    }
    setNotice(undefined);
    createMutation.mutate(nextValues);
  });

  const stepLabels = [
    "Pages_Events_Create_Step_Scenario_Label",
    "Pages_Events_Create_Step_Schedule_Label",
    "Pages_Events_Create_Step_Assets_Label",
    "Pages_Events_Create_Step_Briefing_Label",
    "Pages_Events_Create_Step_Participants_Label",
    "Pages_Events_Create_Step_Review_Label",
  ] as const;
  const stepDescriptions = [
    "Pages_Events_Create_Step_Scenario_Description",
    "Pages_Events_Create_Step_Schedule_Description",
    "Pages_Events_Create_Step_Assets_Description",
    "Pages_Events_Create_Step_Briefing_Description",
    "Pages_Events_Create_Step_Participants_Description",
    "Pages_Events_Create_Step_Review_Description",
  ] as const;
  const isComplete = (index: number) => {
    switch (index) {
      case 1:
        return Boolean(watched.title?.trim());
      case 2:
        return Boolean(watched.scheduledForLocal && watched.hostedByFk);
      case 3:
        return true;
      case 4:
        return Boolean(watched.description?.trim());
      case 5:
        return slots.every((slot) => slot.group.trim() && slot.title.trim());
      default:
        return [1, 2, 3, 4, 5].every(isComplete);
    }
  };
  const errors = form.formState.errors;
  const imageUrl = resolveImageUrl(watched.imagePath);
  const busy = createMutation.isPending;

  if (!canCreate)
    return (
      <StatePanel kind="forbidden" title={t("Auto_EventAdministratorAccessRequired_d59039c1")}>
        {t("Auto_EventCreationIsVisibleOnlyToAuthorizedOperationPlanners_36297796")}
      </StatePanel>
    );

  return (
    <section className="event-authoring-page event-create-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Events")}
        title={t("Auto_CreateEvent_7a9f7d42")}
        description={t("Auto_DefineAndPublishANewOperationPackageWithSchedule_811a8b30")}
        actions={
          <Link className="button button--ghost" to="/command/events">
            {t("Auto_BackToEvents_6f92c820")}
          </Link>
        }
      />
      {notice && <StatePanel kind="error">{notice}</StatePanel>}
      {slotErrors.length > 0 && (
        <StatePanel kind="error" title={t("React_EventAuthoring_ValidationTitle")}>
          {slotErrors.join(" ")}
        </StatePanel>
      )}

      <section className="command-panel event-create-flow">
        <ol className="event-create-stepper" aria-label={t("Auto_CreateEventSteps_f8fea672")}>
          {stepLabels.map((label, index) => {
            const number = index + 1;
            return (
              <li
                key={label}
                className={step === number ? "is-active" : isComplete(number) ? "is-complete" : ""}
              >
                <button
                  type="button"
                  onClick={() => setStep(number)}
                  aria-current={step === number ? "step" : undefined}
                >
                  <span>{number}</span>
                  <strong>{t(label)}</strong>
                  <small>{t(stepDescriptions[index]!)}</small>
                </button>
              </li>
            );
          })}
        </ol>

        <form
          id="event-create-form"
          className="event-create-step"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            void validateAndCreate();
          }}
        >
          {step === 1 && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Scenario")}
                title={t("Auto_OperationIdentity_cdd656fa")}
                status={
                  isComplete(1)
                    ? t("Pages_Events_Create_Status_ScenarioReady")
                    : t("Pages_Events_Create_Status_TitleRequired")
                }
              />
              <div className="event-authoring-form-grid">
                <label className="is-wide">
                  <span>{t("Title")}</span>
                  <input
                    {...form.register("title")}
                    maxLength={1024}
                    aria-invalid={Boolean(errors.title)}
                  />
                  {errors.title && <small className="field-error">{errors.title.message}</small>}
                </label>
                <label>
                  <span>{t("Visibility")}</span>
                  <select
                    {...form.register("isVisible", { setValueAs: (value) => value === "true" })}
                  >
                    <option value="true">{t("Public")}</option>
                    <option value="false">{t("Hidden")}</option>
                  </select>
                </label>
              </div>
            </section>
          )}

          {step === 2 && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Schedule")}
                title={t("Auto_TimeAndHost_40e63453")}
                status={
                  isComplete(2)
                    ? t("Pages_Events_Create_Status_ScheduleReady")
                    : t("Pages_Events_Create_Status_ScheduleHostRequired")
                }
              />
              <div className="event-authoring-form-grid">
                <label>
                  <span>{t("Schedule")}</span>
                  <input
                    {...form.register("scheduledForLocal")}
                    type="datetime-local"
                    aria-invalid={Boolean(errors.scheduledForLocal)}
                  />
                  {errors.scheduledForLocal && (
                    <small className="field-error">{errors.scheduledForLocal.message}</small>
                  )}
                </label>
                <EventEntityPicker<EventPerson>
                  label={t("Host")}
                  placeholder={t("Auto_SearchHost_abe3f02f")}
                  selected={host}
                  disabled={busy}
                  queryKey="create-host"
                  load={(search, signal) => eventAuthoringService.searchUsers(search, signal)}
                  getKey={(person) => person.primaryKey}
                  getLabel={(person) => person.nickname}
                  loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                  emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                  errorLabel={t("React_EventAuthoring_SelectorError")}
                  onSelect={(person) => {
                    setHost(person);
                    form.setValue("hostedByFk", person.primaryKey, {
                      shouldDirty: true,
                      shouldValidate: true,
                    });
                  }}
                />
              </div>
            </section>
          )}

          {step === 3 && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Assets")}
                title={t("Auto_TerrainModPackAndImage_3f2b48a2")}
                status={
                  isComplete(3)
                    ? t("Pages_Events_Create_Status_AssetsReady")
                    : t("Pages_Events_Create_Status_TerrainModPackRequired")
                }
              />
              <div className="event-authoring-form-grid">
                <EventEntityPicker<Terrain>
                  label={t("Terrain")}
                  placeholder={t("Auto_SearchTerrain_ca2ee95e")}
                  selected={terrain}
                  disabled={busy}
                  queryKey="create-terrain"
                  load={async (search, signal) =>
                    (await loadTerrainWindow(search, 20, signal)).terrains
                  }
                  getKey={(item) => String(item.primaryKey)}
                  getLabel={(item) => item.title}
                  loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                  emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                  errorLabel={t("React_EventAuthoring_SelectorError")}
                  onSelect={(item) => {
                    setTerrain(item);
                    setScenario(null);
                    setModPack(null);
                    form.setValue("terrainFk", item.primaryKey, {
                      shouldDirty: true,
                      shouldValidate: true,
                    });
                    form.setValue("terrainGameIdentifier", item.gameIdentifier, {
                      shouldDirty: true,
                    });
                    const capability = capabilities.data?.find(
                      (candidate) => candidate.gameIdentifier === item.gameIdentifier,
                    );
                    form.setValue("scenarioFk", null, { shouldDirty: true });
                    form.setValue("scenarioRequired", capability?.requiresScenario === true);
                    form.setValue("modPackRequired", capability?.requiresModPack !== false);
                    form.setValue("modPackRevisionFk", null, { shouldDirty: true });
                  }}
                />
                {terrain && (
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      setTerrain(null);
                      setScenario(null);
                      setModPack(null);
                      form.setValue("terrainFk", null, { shouldDirty: true });
                      form.setValue("terrainGameIdentifier", "", { shouldDirty: true });
                      form.setValue("scenarioFk", null, { shouldDirty: true });
                      form.setValue("scenarioRequired", false);
                      form.setValue("modPackRevisionFk", null, { shouldDirty: true });
                      form.setValue("modPackRequired", false);
                    }}
                  >
                    {t("Remove")}
                  </button>
                )}
                {watched.scenarioRequired && terrain && (
                  <EventEntityPicker<GameContentOption>
                    label="Scenario"
                    placeholder="Search scenarios"
                    selected={scenario}
                    disabled={busy}
                    queryKey={`create-scenario-${terrain.gameIdentifier}`}
                    load={async (search, signal) =>
                      (await hostingService.scenarios(terrain.gameIdentifier, signal)).filter(
                        (item) => item.title.toLowerCase().includes(search.toLowerCase()),
                      )
                    }
                    getKey={(item) => item.primaryKey}
                    getLabel={(item) => item.title}
                    loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                    emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                    errorLabel={t("React_EventAuthoring_SelectorError")}
                    onSelect={(item) => {
                      setScenario(item);
                      form.setValue("scenarioFk", item.primaryKey, {
                        shouldDirty: true,
                        shouldValidate: true,
                      });
                    }}
                  />
                )}
                <EventEntityPicker<FullModPackDefinition>
                  label={t("Auto_ModPackRevision_d199668f")}
                  placeholder={t("Auto_SearchModPack_33ce4302")}
                  selected={modPack}
                  disabled={busy}
                  queryKey={`create-modpack-${terrain?.gameIdentifier ?? "all"}`}
                  load={(search, signal) =>
                    modPackService.list(
                      {
                        skip: 0,
                        take: 20,
                        myModPacksOnly: false,
                        ...(search ? { search } : {}),
                        ...(terrain?.gameIdentifier
                          ? { gameIdentifier: terrain.gameIdentifier }
                          : {}),
                      },
                      signal,
                    )
                  }
                  getKey={(item) => String(item.primaryKey ?? item.title)}
                  getLabel={selectedModPackLabel}
                  loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                  emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                  errorLabel={t("React_EventAuthoring_SelectorError")}
                  onSelect={(item) => {
                    const revision = latestActiveRevision(item);
                    if (revision?.primaryKey == null) {
                      setNotice(t("React_EventAuthoring_ModPackNoRevision"));
                      return;
                    }
                    setNotice(undefined);
                    setModPack(item);
                    form.setValue("modPackRevisionFk", revision.primaryKey, {
                      shouldDirty: true,
                      shouldValidate: true,
                    });
                  }}
                />
                {modPack && (
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      setModPack(null);
                      form.setValue("modPackRevisionFk", null, {
                        shouldDirty: true,
                        shouldValidate: true,
                      });
                    }}
                  >
                    {t("Remove")}
                  </button>
                )}
              </div>
              <div className="event-authoring-image-grid">
                <div className="event-authoring-image-preview">
                  {imageUrl ? (
                    <img
                      src={imageUrl}
                      alt={selectedAsset?.originalFileName || t("Auto_MissionImage_85cc2241")}
                    />
                  ) : (
                    <span>{t("Auto_MissionImagePending_b4d8ba2b")}</span>
                  )}
                </div>
                <div>
                  <span className="kicker">{t("Auto_MissionImage_85cc2241")}</span>
                  <p>
                    {selectedAsset?.originalFileName || t("Pages_Events_Create_Status_NotAttached")}
                  </p>
                  <button
                    className="button button--ghost"
                    type="button"
                    onClick={() => setPickerOpen(true)}
                  >
                    {t("React_EventAuthoring_SelectImage")}
                  </button>
                  {imageUrl && (
                    <button
                      className="button button--ghost"
                      type="button"
                      onClick={() => {
                        setSelectedAsset(undefined);
                        form.setValue("imageAssetFk", "", {
                          shouldDirty: true,
                          shouldValidate: true,
                        });
                        form.setValue("imagePath", "", { shouldDirty: true });
                      }}
                    >
                      {t("Remove")}
                    </button>
                  )}
                  {errors.imageAssetFk && (
                    <small className="field-error">{errors.imageAssetFk.message}</small>
                  )}
                </div>
              </div>
            </section>
          )}

          {step === 4 && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Briefing")}
                title={t("Auto_MissionDetails_6c819bdd")}
                status={formatResource(
                  t("Pages_Events_Create_Count_Characters"),
                  watched.description?.length ?? 0,
                )}
              />
              <label className="event-authoring-briefing-field">
                <span>{t("Auto_BriefingMarkdown_57d6b69f")}</span>
                <textarea
                  {...form.register("description")}
                  rows={16}
                  aria-invalid={Boolean(errors.description)}
                />
                {errors.description && (
                  <small className="field-error">{errors.description.message}</small>
                )}
              </label>
              <section className="event-authoring-markdown-preview">
                <span className="kicker">{t("Auto_RenderedBriefing_9d4f9366")}</span>
                <Markdown>{watched.description ?? ""}</Markdown>
              </section>
            </section>
          )}

          {step === 5 && (
            <section className="event-authoring-section">
              <div className="event-authoring-form-grid event-authoring-form-grid--compact">
                <label>
                  <span>{t("Auto_MinimumAccepted_05ac548c")}</span>
                  <input
                    {...form.register("minimumAccepted", { valueAsNumber: true })}
                    type="number"
                    min={0}
                    aria-invalid={Boolean(errors.minimumAccepted)}
                  />
                  {errors.minimumAccepted && (
                    <small className="field-error">{errors.minimumAccepted.message}</small>
                  )}
                </label>
                <label>
                  <span>{t("Visibility")}</span>
                  <select
                    {...form.register("isVisible", { setValueAs: (value) => value === "true" })}
                  >
                    <option value="true">{t("Public")}</option>
                    <option value="false">{t("Hidden")}</option>
                  </select>
                </label>
              </div>
              <EventSlotPlanner
                slots={slots}
                disabled={busy}
                saving={false}
                showSave={false}
                onChange={updateSlots}
                onSave={() => undefined}
                onAssign={(slot, person) => stageAssignment(slot, person)}
                onUnassign={(slot) => stageAssignment(slot, null)}
              />
            </section>
          )}

          {step === 6 && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Review")}
                title={t("Auto_ReviewYourOperationPackage_09d725e7")}
                status={
                  [1, 2, 3, 4, 5].every(isComplete)
                    ? t("Pages_Events_Create_Status_ReadyToCreate")
                    : t("Pages_Events_Create_Status_NeedsAttention")
                }
              />
              <div className="event-create-review-grid">
                <ReviewCard
                  title={t("Scenario")}
                  onEdit={() => setStep(1)}
                  editLabel={t("Pages_Events_Create_ActionEdit")}
                >
                  <ReviewItem
                    label={t("Title")}
                    value={watched.title || t("Pages_Events_Create_UntitledOperation")}
                  />
                  <ReviewItem
                    label={t("Visibility")}
                    value={watched.isVisible ? t("Public") : t("Hidden")}
                  />
                </ReviewCard>
                <ReviewCard
                  title={t("Auto_ScheduleHost_a62a2806")}
                  onEdit={() => setStep(2)}
                  editLabel={t("Pages_Events_Create_ActionEdit")}
                >
                  <ReviewItem label={t("Schedule")} value={formatDate(watched.scheduledForLocal)} />
                  <ReviewItem
                    label={t("Host")}
                    value={host?.nickname || t("Pages_Events_Create_Status_HostPending")}
                  />
                </ReviewCard>
                <ReviewCard
                  title={t("Assets")}
                  onEdit={() => setStep(3)}
                  editLabel={t("Pages_Events_Create_ActionEdit")}
                >
                  <ReviewItem
                    label={t("Terrain")}
                    value={terrain?.title || t("Pages_Events_Create_Status_TerrainPending")}
                  />
                  <ReviewItem
                    label={t("Auto_ModPack_f151fb94")}
                    value={
                      selectedModPackLabel(modPack) ||
                      t("Pages_Events_Create_Status_ModPackPending")
                    }
                  />
                  <ReviewItem
                    label={t("Auto_MissionImage_85cc2241")}
                    value={
                      imageUrl
                        ? t("Pages_Events_Create_Status_Attached")
                        : t("Pages_Events_Create_Status_NotAttached")
                    }
                  />
                </ReviewCard>
                <ReviewCard
                  title={t("Briefing")}
                  onEdit={() => setStep(4)}
                  editLabel={t("Pages_Events_Create_ActionEdit")}
                >
                  <ReviewItem
                    label={t("Length")}
                    value={formatResource(
                      t("Pages_Events_Create_Count_Characters"),
                      watched.description?.length ?? 0,
                    )}
                  />
                  <ReviewItem
                    label={t("Excerpt")}
                    value={(
                      watched.description || t("Pages_Events_Create_Status_BriefingPending")
                    ).slice(0, 180)}
                  />
                </ReviewCard>
                <ReviewCard
                  title={t("Auto_ParticipantsSlots_77a3eae4")}
                  onEdit={() => setStep(5)}
                  editLabel={t("Pages_Events_Create_ActionEdit")}
                >
                  <ReviewItem
                    label={t("Auto_MinimumAccepted_05ac548c")}
                    value={String(watched.minimumAccepted ?? 0)}
                  />
                  <ReviewItem
                    label={t("Auto_InitialSlots_9a5c0b97")}
                    value={String(slots.length)}
                  />
                </ReviewCard>
              </div>
              <div className="event-authoring-checklist">
                {[1, 2, 3, 4, 5].map((index) => (
                  <div key={index} className={isComplete(index) ? "is-complete" : ""}>
                    <StatusBadge tone={isComplete(index) ? "success" : "warning"}>
                      {isComplete(index)
                        ? t("Pages_Events_Create_Status_Complete")
                        : t("Pages_Events_Create_Status_NeedsAttention")}
                    </StatusBadge>
                    <strong>{t(stepLabels[index - 1]!)}</strong>
                  </div>
                ))}
              </div>
              <StatePanel kind="info">
                {t("Auto_OnceCreatedTheEventOwnerAndOriginalScheduleTimestamp_67063c0b")}
              </StatePanel>
            </section>
          )}
        </form>

        <footer className="event-create-command-bar">
          <div>
            <span>{t(stepLabels[step - 1]!)}</span>
            <strong>
              {[1, 2, 3, 4, 5].every(isComplete)
                ? t("Pages_Events_Create_Status_ReadyToCreate")
                : t("Pages_Events_Create_Status_NeedsAttention")}
            </strong>
          </div>
          <div>
            <button
              className="button button--ghost"
              type="button"
              disabled={step <= 1 || busy}
              onClick={() => setStep(Math.max(1, step - 1))}
            >
              {t("Previous")}
            </button>
            {step < finalStep ? (
              <button
                className="button button--primary"
                type="button"
                disabled={busy || !isComplete(step)}
                onClick={(event) => {
                  // The next-step button becomes the submit button at the final step. Prevent the
                  // browser's default action before React reuses that DOM position during this click.
                  event.preventDefault();
                  setStep(Math.min(finalStep, step + 1));
                }}
              >
                {t("Next")}
              </button>
            ) : (
              <button
                className="button button--primary"
                type="submit"
                form="event-create-form"
                disabled={busy || ![1, 2, 3, 4, 5].every(isComplete)}
              >
                {busy
                  ? t("Pages_Events_Create_ActionCreatingEvent")
                  : t("Pages_Events_Create_ActionCreateEvent")}
              </button>
            )}
          </div>
        </footer>
      </section>

      <GalleryAssetPicker
        open={pickerOpen}
        selectedAssetId={watched.imageAssetFk ?? ""}
        uploadTags={["event"]}
        onSelect={(asset) => {
          setSelectedAsset(asset);
          form.setValue("imageAssetFk", asset.primaryKey, {
            shouldDirty: true,
            shouldValidate: true,
          });
          form.setValue("imagePath", asset.imagePath, { shouldDirty: true });
          setPickerOpen(false);
        }}
        onCancel={() => setPickerOpen(false)}
      />
    </section>
  );
}

function SectionHeading({
  kicker,
  title,
  status,
}: {
  kicker: string;
  title: string;
  status: string;
}) {
  return (
    <header className="event-authoring-section-heading">
      <div>
        <span className="kicker">{kicker}</span>
        <h2>{title}</h2>
      </div>
      <span>{status}</span>
    </header>
  );
}

function ReviewCard({
  title,
  editLabel,
  onEdit,
  children,
}: {
  title: string;
  editLabel: string;
  onEdit(): void;
  children: ReactNode;
}) {
  return (
    <article>
      <header>
        <h3>{title}</h3>
        <button type="button" onClick={onEdit}>
          {editLabel}
        </button>
      </header>
      <dl>{children}</dl>
    </article>
  );
}

function ReviewItem({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </>
  );
}

function formatDate(value: string | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.valueOf())
    ? "—"
    : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}
