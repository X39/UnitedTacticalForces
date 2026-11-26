import { zodResolver } from "@hookform/resolvers/zod";
import {
  ArrowLeft20Regular,
  Calendar20Regular,
  Document20Regular,
  People20Regular,
  Settings20Regular,
} from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams, useSearchParams } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canEditEvent, canManageEvents, isAuthenticated } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { SelectField, TextAreaField, TextField } from "@/components/FormFields";
import { NotFoundPage } from "@/components/NotFoundPage";
import { PageStack } from "@/components/PageStack";
import { ConfirmDialog, Markdown, PageHeader, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import { EventEntityPicker } from "@/features/admin/EventEntityPicker";
import { EventObjectiveIconPicker } from "@/features/admin/EventObjectiveIconPicker";
import { EventSlotPlanner } from "@/features/admin/EventSlotPlanner";
import {
  applyEventObjectives,
  createEventFormSchema,
  createObjectiveDraft,
  eventAuthoringQueryKeys,
  eventAuthoringService,
  eventObjectiveIcons,
  eventToForm,
  isGuid,
  normalizeEventVisibility,
  parseEventObjectives,
  validateSlot,
  type EventAuthoringSnapshot,
  type EventFormValues,
  type EventObjectiveDraft,
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
import "@/features/admin/eventAuthoring.css";

type EditorTab = "details" | "briefing" | "objectives" | "slots";
type SaveMode = "all" | "slots";
type AssignmentOperation =
  | { kind: "assign"; slot: EventSlotDraft; person: EventPerson }
  | { kind: "unassign"; slot: EventSlotDraft };

function syntheticTerrain(snapshot: EventAuthoringSnapshot): Terrain | null {
  const event = snapshot.event;
  return event.terrainPrimaryKey == null
    ? null
    : {
        primaryKey: event.terrainPrimaryKey,
        title: event.terrainTitle,
        gameIdentifier: event.terrainGameIdentifier,
        imageAssetFk: "",
        imagePath: "",
        isActive: true,
      };
}

function syntheticModPack(snapshot: EventAuthoringSnapshot): FullModPackDefinition | null {
  const event = snapshot.event;
  return event.modPackRevisionPrimaryKey == null
    ? null
    : {
        primaryKey: event.modPackRevisionPrimaryKey,
        title: event.modPackDefinitionTitle,
        owner: null,
        modPackRevisions: [
          {
            primaryKey: event.modPackRevisionPrimaryKey,
            isActive: true,
          },
        ],
      };
}

function syntheticScenario(snapshot: EventAuthoringSnapshot): GameContentOption | null {
  const event = snapshot.event;
  return event.scenarioPrimaryKey == null
    ? null
    : {
        primaryKey: event.scenarioPrimaryKey,
        gameIdentifier: event.terrainGameIdentifier,
        title: event.scenarioTitle,
        versions: [],
      };
}

function Editor({ snapshot }: { snapshot: EventAuthoringSnapshot }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { showToast } = useAppToast();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const eventId = snapshot.event.primaryKey;
  const schema = useMemo(
    () =>
      createEventFormSchema({
        titleRequired: t("React_EventAuthoring_TitleRequired"),
        titleTooLong: t("React_EventAuthoring_TitleTooLong"),
        briefingRequired: t("React_EventAuthoring_BriefingRequired"),
        scheduleRequired: t("React_EventAuthoring_ScheduleRequired"),
        hostRequired: t("React_EventAuthoring_HostRequired"),
        terrainRequired: t("React_EventAuthoring_TerrainRequired"),
        modPackRequired: t("React_EventAuthoring_ModPackRequired"),
        imageRequired: t("React_EventAuthoring_ImageRequired"),
        minimumInvalid: t("React_EventAuthoring_MinimumInvalid"),
      }),
    [t],
  );
  const form = useForm<EventFormValues>({
    resolver: zodResolver(schema),
    defaultValues: eventToForm(snapshot.event),
    mode: "onBlur",
  });
  const watched = useWatch({ control: form.control });
  const [tab, setTab] = useState<EditorTab>("details");
  const [host, setHost] = useState<EventPerson | null>(snapshot.event.hostedBy);
  const [terrain, setTerrain] = useState<Terrain | null>(() => syntheticTerrain(snapshot));
  const [scenario, setScenario] = useState<GameContentOption | null>(() =>
    syntheticScenario(snapshot),
  );
  const [modPack, setModPack] = useState<FullModPackDefinition | null>(() =>
    syntheticModPack(snapshot),
  );
  const [slots, setSlots] = useState<EventSlotDraft[]>(snapshot.slots);
  const [slotsDirty, setSlotsDirty] = useState(false);
  const [objectives, setObjectives] = useState<EventObjectiveDraft[]>(() =>
    parseEventObjectives(snapshot.event.description),
  );
  const [pickerOpen, setPickerOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<MediaAsset>();
  const [removeObjective, setRemoveObjective] = useState<EventObjectiveDraft>();
  const dirty = form.formState.isDirty || slotsDirty;
  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
  });
  const leaveGuard = useLeaveGuard({ dirty });
  const descriptionField = form.register("description");
  useEffect(() => {
    if (searchParams.get("slotSetup") === "failed")
      showToast(t("React_EventAuthoring_PartialSlotFailure"), "warning");
  }, [searchParams, showToast, t]);

  const updateSlots = (next: EventSlotDraft[]) => {
    setSlots(next);
    setSlotsDirty(true);
  };

  const persistSlots = async () => {
    const active = slots.filter((slot) => !slot.isDeleted);
    const errors = active.flatMap((slot) =>
      validateSlot(slot, {
        groupRequired: t("React_EventAuthoring_SlotGroupRequired"),
        titleRequired: t("React_EventAuthoring_SlotTitleRequired"),
      }),
    );
    if (errors.length) throw new Error([...new Set(errors)].join(" "));
    for (const slot of slots.filter((item) => item.isDeleted && item.slotNumber != null))
      await eventAuthoringService.deleteSlot(eventId, slot.slotNumber!);
    for (const slot of active) {
      if (slot.slotNumber == null) {
        await eventAuthoringService.createSlot(eventId, slot);
        continue;
      }
      await eventAuthoringService.updateSlot(eventId, slot);
      if (slot.assignedToFk === slot.originalAssignedToFk) continue;
      if (slot.originalAssignedToFk)
        await eventAuthoringService.unassignSlot(
          eventId,
          slot.slotNumber,
          slot.originalAssignedToFk,
        );
      if (slot.assignedToFk)
        await eventAuthoringService.assignSlot(eventId, slot.slotNumber, slot.assignedToFk);
    }
    return eventAuthoringService.reloadSlots(eventId);
  };

  const saveMutation = useMutation({
    mutationFn: async ({ mode, values }: { mode: SaveMode; values?: EventFormValues }) => {
      if (mode === "all" && values) await eventAuthoringService.update(eventId, values);
      return persistSlots();
    },
    onSuccess: async (freshSlots, { mode, values }) => {
      setSlots(freshSlots);
      setSlotsDirty(false);
      if (mode === "all" && values) form.reset(values);
      showToast(
        mode === "slots"
          ? t("Pages_Events_Edit_Message_SlotPlanSaved")
          : t("React_EventAuthoring_SaveSuccess"),
        "success",
      );
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: eventAuthoringQueryKeys.event(eventId) }),
        queryClient.invalidateQueries({ queryKey: ["events", eventId] }),
        queryClient.invalidateQueries({ queryKey: ["command-events"] }),
      ]);
    },
    onError: (error, { mode }) =>
      showToast(
        formatResource(
          mode === "slots"
            ? t("Pages_Events_Edit_Message_SlotPlanSaveFailed")
            : t("React_EventAuthoring_SaveError"),
          error.message,
        ),
        "error",
      ),
  });

  const assignmentMutation = useMutation({
    mutationFn: async (operation: AssignmentOperation) => {
      const slot = operation.slot;
      if (slot.slotNumber == null) return operation;
      if (operation.kind === "assign") {
        if (slot.assignedToFk)
          await eventAuthoringService.unassignSlot(eventId, slot.slotNumber, slot.assignedToFk);
        await eventAuthoringService.assignSlot(
          eventId,
          slot.slotNumber,
          operation.person.primaryKey,
        );
      } else if (slot.assignedToFk) {
        await eventAuthoringService.unassignSlot(eventId, slot.slotNumber, slot.assignedToFk);
      }
      return operation;
    },
    onSuccess: async (operation) => {
      if (operation.slot.slotNumber == null) {
        const person = operation.kind === "assign" ? operation.person : null;
        updateSlots(
          slots.map((slot) =>
            slot.draftKey === operation.slot.draftKey
              ? {
                  ...slot,
                  assignedToFk: person?.primaryKey ?? "",
                  assignedUser: person,
                  assignmentCandidate: person,
                }
              : slot,
          ),
        );
      } else {
        setSlots(await eventAuthoringService.reloadSlots(eventId));
      }
      showToast(
        operation.kind === "assign"
          ? t("Pages_Events_Edit_Message_SlotAssignmentUpdated")
          : t("Pages_Events_Edit_Message_SlotAssignmentRemoved"),
        "success",
      );
    },
    onError: (_error, operation) =>
      showToast(
        operation.kind === "assign"
          ? t("Pages_Events_Edit_Message_SlotAssignFailed")
          : t("Pages_Events_Edit_Message_SlotUnassignFailed"),
        "error",
      ),
  });

  const applyObjectives = (next: EventObjectiveDraft[]) => {
    setObjectives(next);
    form.setValue("description", applyEventObjectives(form.getValues("description"), next), {
      shouldDirty: true,
      shouldValidate: true,
    });
  };
  const readiness = [
    ["Pages_Events_Edit_Readiness_TitleDefined", Boolean(watched.title?.trim())],
    ["Pages_Events_Edit_Readiness_ScheduleSet", Boolean(watched.scheduledForLocal)],
    ["Pages_Events_Edit_Readiness_HostAssigned", Boolean(watched.hostedByFk)],
    ["Pages_Events_Edit_Readiness_BriefingAdded", Boolean(watched.description?.trim())],
    ["Pages_Events_Edit_Readiness_ObjectivesAdded", objectives.some((item) => item.text.trim())],
    ["Pages_Events_Edit_Readiness_MinimumAcceptedSet", (watched.minimumAccepted ?? -1) >= 0],
  ] as const;
  const busy = saveMutation.isPending || assignmentMutation.isPending;
  const errors = form.formState.errors;
  const imageUrl = resolveImageUrl(watched.imagePath);
  const backPath = canManageEvents(user) ? "/command/events" : `/events/${eventId}`;

  const saveAll = form.handleSubmit(
    (values) => saveMutation.mutate({ mode: "all", values }),
    () => {
      setTab("details");
      showToast(t("Pages_Events_Edit_Status_NeedsAttention"), "warning");
    },
  );
  const saveSlots = () => saveMutation.mutate({ mode: "slots" });
  const saveCurrent = tab === "slots" ? saveSlots : () => void saveAll();
  const currentSaveDisabled = busy || (tab === "slots" ? !slotsDirty : !dirty);

  return (
    <PageStack className="event-editor-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Events")}
        title={watched.title?.trim() || t("Pages_Events_Edit_UntitledOperation")}
        description={t(
          "Auto_AuthoringWorkspaceForOperationMetadataBriefingMediaReadinessAnd_560063d1",
        )}
        meta={
          <div className="event-editor-header__meta">
            <StatusBadge tone={watched.isVisible ? "success" : "neutral"}>
              {watched.isVisible ? t("Public") : t("Hidden")}
            </StatusBadge>
            <span>
              <Calendar20Regular aria-hidden="true" />
              {formatDate(watched.scheduledForLocal)}
            </span>
            <span>
              <People20Regular aria-hidden="true" />
              {t("Host")}: {host?.nickname || t("Pages_Events_Edit_Status_HostPending")}
            </span>
            {dirty && <strong>{t("React_UnsavedChanges")}</strong>}
          </div>
        }
        actions={
          <>
            <ActionLink variant="ghost" to={backPath}>
              <ArrowLeft20Regular aria-hidden="true" />
              {t("Auto_BackToEvents_6f92c820")}
            </ActionLink>
            <ActionLink variant="ghost" to={`/events/${eventId}`}>
              <People20Regular aria-hidden="true" />
              {t("Auto_ViewSlots_ddcd3598")}
            </ActionLink>
            <ActionButton variant="gold" disabled={currentSaveDisabled} onClick={saveCurrent}>
              {saveMutation.isPending
                ? tab === "slots"
                  ? t("Pages_Events_Edit_ActionSavingSlots")
                  : t("Pages_Events_Edit_ActionSaving")
                : tab === "slots"
                  ? t("Pages_Events_Edit_ActionSaveSlotPlan")
                  : t("Pages_Events_Edit_ActionSaveChanges")}
            </ActionButton>
          </>
        }
      />
      <section className="command-panel event-editor-header">
        <nav
          className="event-editor-tabs"
          role="tablist"
          aria-label={t("React_EventAuthoring_EditorTabs")}
        >
          {(["details", "briefing", "objectives", "slots"] as const).map((item) => (
            <ActionButton
              key={item}
              fullWidth
              role="tab"
              aria-selected={tab === item}
              {...(tab === item ? { className: "is-active" } : {})}
              onClick={() => setTab(item)}
            >
              {item === "details" ? (
                <Settings20Regular aria-hidden="true" />
              ) : item === "briefing" ? (
                <Document20Regular aria-hidden="true" />
              ) : item === "objectives" ? (
                <Calendar20Regular aria-hidden="true" />
              ) : (
                <People20Regular aria-hidden="true" />
              )}
              {t(
                item === "details"
                  ? "Details"
                  : item === "briefing"
                    ? "Briefing"
                    : item === "objectives"
                      ? "Objectives"
                      : "Slots",
              )}
              {item === "slots" && (
                <span className="event-editor-tabs__count">
                  {slots.filter((slot) => !slot.isDeleted).length}
                </span>
              )}
            </ActionButton>
          ))}
        </nav>
      </section>

      <div className="event-editor-workspace">
        <main className="command-panel event-editor-main">
          {tab === "details" && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Details")}
                title={t("Auto_BasicInformation_6c092c9c")}
                status={
                  readiness.every(([, complete]) => complete)
                    ? t("Pages_Events_Edit_Status_Ready")
                    : t("Pages_Events_Edit_Status_NeedsAttention")
                }
              />
              <div className="event-authoring-form-grid">
                <div className="is-wide">
                  <TextField
                    {...form.register("title")}
                    label={t("Title")}
                    maxLength={1024}
                    disabled={busy}
                    error={errors.title?.message}
                  />
                </div>
                <TextField
                  {...form.register("scheduledForLocal")}
                  label={t("Schedule")}
                  type="datetime-local"
                  disabled={busy}
                  error={errors.scheduledForLocal?.message}
                />
                <EventEntityPicker<EventPerson>
                  label={t("Host")}
                  placeholder={t("Auto_SearchHost_abe3f02f")}
                  selected={host}
                  disabled={busy}
                  queryKey={`edit-host-${eventId}`}
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
                <TextField
                  {...form.register("minimumAccepted", { valueAsNumber: true })}
                  label={t("Auto_MinimumAccepted_05ac548c")}
                  type="number"
                  min={0}
                  disabled={busy}
                  error={errors.minimumAccepted?.message}
                />
                <SelectField
                  {...form.register("isVisible", { setValueAs: normalizeEventVisibility })}
                  label={t("Visibility")}
                  disabled={busy}
                  options={[
                    { value: "true", label: t("Public") },
                    { value: "false", label: t("Hidden") },
                  ]}
                />
                <EventEntityPicker<Terrain>
                  label={t("Terrain")}
                  placeholder={t("Auto_SearchTerrain_ca2ee95e")}
                  selected={terrain}
                  disabled={busy}
                  queryKey={`edit-terrain-${eventId}`}
                  load={async (search, signal) =>
                    (await loadTerrainWindow(search, 20, signal)).terrains
                  }
                  getKey={(item) => String(item.primaryKey)}
                  getLabel={(item) => item.title}
                  loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                  emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                  errorLabel={t("React_EventAuthoring_SelectorError")}
                  clearLabel={`${t("Remove")} ${t("Terrain")}`}
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
                  onClear={() => {
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
                />
                {watched.scenarioRequired && terrain && (
                  <EventEntityPicker<GameContentOption>
                    label={t("Pages_Events_Create_Step_Scenario_Label")}
                    placeholder={t("React_EventAuthoring_ScenarioSearchPlaceholder")}
                    selected={scenario}
                    disabled={busy}
                    queryKey={`edit-scenario-${eventId}-${terrain.gameIdentifier}`}
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
                  queryKey={`edit-modpack-${eventId}-${terrain?.gameIdentifier ?? "all"}`}
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
                  getLabel={(item) => item.title ?? ""}
                  loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                  emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                  errorLabel={t("React_EventAuthoring_SelectorError")}
                  clearLabel={`${t("Remove")} ${t("Auto_ModPackRevision_d199668f")}`}
                  onSelect={(item) => {
                    const revision = latestActiveRevision(item);
                    if (revision?.primaryKey == null) {
                      showToast(t("React_EventAuthoring_ModPackNoRevision"), "warning");
                      return;
                    }
                    setModPack(item);
                    form.setValue("modPackRevisionFk", revision.primaryKey, {
                      shouldDirty: true,
                      shouldValidate: true,
                    });
                  }}
                  onClear={() => {
                    setModPack(null);
                    form.setValue("modPackRevisionFk", null, {
                      shouldDirty: true,
                      shouldValidate: true,
                    });
                  }}
                />
              </div>
              <section className="event-authoring-resource-panel">
                <SectionHeading
                  kicker={t("Assets")}
                  title={t("Auto_TerrainModPackAndMissionImage_7e5fddd3")}
                  status={t("Pages_Events_Edit_Status_AssetsReady")}
                />
                <div className="event-authoring-image-grid">
                  <div className="event-authoring-image-preview">
                    {imageUrl ? (
                      <img
                        src={imageUrl}
                        alt={selectedAsset?.originalFileName || t("Auto_MissionImage_85cc2241")}
                      />
                    ) : (
                      <span>{t("Auto_NoMissionImage_4188cdba")}</span>
                    )}
                  </div>
                  <div>
                    <p>
                      {selectedAsset?.originalFileName ||
                        snapshot.event.imagePath ||
                        t("Auto_NoMissionImage_4188cdba")}
                    </p>
                    <ActionButton
                      variant="ghost"
                      disabled={busy}
                      onClick={() => setPickerOpen(true)}
                    >
                      {t("React_EventAuthoring_SelectImage")}
                    </ActionButton>
                    {imageUrl && (
                      <ActionButton
                        variant="ghost"
                        disabled={busy}
                        onClick={() => {
                          setSelectedAsset(undefined);
                          form.setValue("imageAssetFk", "", { shouldDirty: true });
                          form.setValue("imagePath", "", { shouldDirty: true });
                        }}
                      >
                        {t("Remove")}
                      </ActionButton>
                    )}
                  </div>
                </div>
              </section>
            </section>
          )}

          {tab === "briefing" && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Briefing")}
                title={t("Auto_CommanderSNotes_99b2f4e5")}
                status={formatResource(
                  t("Pages_Events_Edit_Count_Characters"),
                  watched.description?.length ?? 0,
                )}
              />
              <TextAreaField
                {...descriptionField}
                label={t("Auto_BriefingMarkdown_57d6b69f")}
                rows={18}
                disabled={busy}
                onBlur={(event) => {
                  void descriptionField.onBlur(event);
                  setObjectives(parseEventObjectives(event.currentTarget.value));
                }}
              />
              <section className="event-authoring-markdown-preview">
                <span className="kicker">{t("Auto_RenderedBriefing_9d4f9366")}</span>
                <Markdown>{watched.description ?? ""}</Markdown>
              </section>
            </section>
          )}

          {tab === "objectives" && (
            <section className="event-authoring-section">
              <SectionHeading
                kicker={t("Auto_MissionObjectives_9474605d")}
                title={t("Auto_PublicBriefingHighlights_dd5597fe")}
                status={formatResource(
                  t("Pages_Events_Edit_Count_PublicObjectives"),
                  objectives.filter((item) => item.text.trim()).length,
                )}
              />
              <div className="event-objective-list">
                {objectives.map((objective, index) => (
                  <article key={objective.draftKey}>
                    <EventObjectiveIconPicker
                      label={t("Icon")}
                      value={objective.icon}
                      options={eventObjectiveIcons}
                      disabled={busy}
                      onChange={(icon) =>
                        applyObjectives(
                          objectives.map((item) =>
                            item.draftKey === objective.draftKey ? { ...item, icon } : item,
                          ),
                        )
                      }
                    />
                    <TextField
                      label={formatResource(t("Pages_Events_Edit_LabelObjective"), index + 1)}
                      value={objective.text}
                      maxLength={150}
                      disabled={busy}
                      onChange={(event) =>
                        applyObjectives(
                          objectives.map((item) =>
                            item.draftKey === objective.draftKey
                              ? { ...item, text: event.target.value }
                              : item,
                          ),
                        )
                      }
                    />
                    <ActionButton
                      variant="ghost"
                      disabled={busy}
                      onClick={() => setRemoveObjective(objective)}
                    >
                      {t("Pages_Events_Edit_ActionRemoveObjective")}
                    </ActionButton>
                  </article>
                ))}
              </div>
              <ActionButton
                variant="ghost"
                disabled={busy}
                onClick={() =>
                  applyObjectives([...objectives, createObjectiveDraft(objectives.length)])
                }
              >
                {t("Pages_Events_Edit_ActionAddObjective")}
              </ActionButton>
              <section className="event-authoring-markdown-preview">
                <span className="kicker">{t("Auto_PublicPreview_f948bc45")}</span>
                <Markdown>{watched.description ?? ""}</Markdown>
              </section>
            </section>
          )}

          {tab === "slots" && (
            <EventSlotPlanner
              slots={slots}
              disabled={busy}
              saving={saveMutation.isPending}
              onChange={updateSlots}
              onSave={saveSlots}
              showSave={false}
              onAssign={(slot, person) =>
                assignmentMutation.mutate({ kind: "assign", slot, person })
              }
              onUnassign={(slot) => assignmentMutation.mutate({ kind: "unassign", slot })}
            />
          )}
        </main>

        <aside
          className="command-panel event-editor-audit"
          aria-label={t("Auto_EventAuditContext_53811520")}
        >
          <section>
            <span className="kicker">{t("Auto_EventDetails_f9c7f3d8")}</span>
            <dl>
              <dt>{t("Owner")}</dt>
              <dd>{snapshot.event.owner?.nickname || "—"}</dd>
              <dt>{t("Auto_OriginalSchedule_b5c2561d")}</dt>
              <dd>{formatDate(snapshot.event.scheduledForOriginal)}</dd>
              <dt>{t("Created")}</dt>
              <dd>{formatDate(snapshot.event.timeStampCreated)}</dd>
              <dt>{t("Auto_EventId_82e663fd")}</dt>
              <dd>{eventId}</dd>
            </dl>
          </section>
          <section>
            <span className="kicker">{t("Participation")}</span>
            <dl>
              <dt>{t("Accepted")}</dt>
              <dd>{snapshot.event.acceptedCount}</dd>
              <dt>{t("Maybe")}</dt>
              <dd>{snapshot.event.maybeCount}</dd>
              <dt>{t("Rejected")}</dt>
              <dd>{snapshot.event.rejectedCount}</dd>
              <dt>{t("Slots")}</dt>
              <dd>{slots.filter((slot) => !slot.isDeleted).length}</dd>
            </dl>
          </section>
          <section>
            <span className="kicker">{t("Auto_ReadinessChecklist_13d029ae")}</span>
            <Readiness rows={readiness} compact />
          </section>
        </aside>
      </div>

      <GalleryAssetPicker
        open={pickerOpen}
        selectedAssetId={watched.imageAssetFk ?? ""}
        uploadTags={["event"]}
        onSelect={(asset) => {
          setSelectedAsset(asset);
          form.setValue("imageAssetFk", asset.primaryKey, { shouldDirty: true });
          form.setValue("imagePath", asset.imagePath, { shouldDirty: true });
          setPickerOpen(false);
        }}
        onCancel={() => setPickerOpen(false)}
      />
      <ConfirmDialog
        open={Boolean(removeObjective)}
        title={t("React_EventAuthoring_RemoveObjectiveTitle")}
        description={t("Pages_Events_Edit_Tooltip_ConfirmRemoveObjective")}
        confirmLabel={t("Pages_Events_Edit_ActionConfirmRemoveObjective")}
        cancelLabel={t("React_EventAuthoring_RemoveObjective_Dismiss")}
        onConfirm={() => {
          if (removeObjective)
            applyObjectives(
              objectives.filter((item) => item.draftKey !== removeObjective.draftKey),
            );
          setRemoveObjective(undefined);
        }}
        onCancel={() => setRemoveObjective(undefined)}
      />
      <ConfirmDialog
        open={leaveGuard.blocked}
        title={t("React_EventAuthoring_Leave_Title")}
        description={t("React_EventAuthoring_Leave_Body")}
        cancelLabel={t("React_EventAuthoring_Leave_Stay")}
        confirmLabel={t("React_EventAuthoring_Leave_Leave")}
        initialFocus="dismiss"
        onCancel={leaveGuard.stay}
        onConfirm={leaveGuard.proceed}
      />
    </PageStack>
  );
}

/** Renders the shared page header above a state view, so every state of the editor route keeps one `h1`. */
function EditorStateFrame({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader title={t("Shared_CommandCenter_Nav_Events")} />
      {children}
    </PageStack>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { eventId } = useParams<{ eventId: string }>();
  const canLoad = isAuthenticated(user);
  const query = useQuery({
    queryKey: eventAuthoringQueryKeys.event(eventId ?? ""),
    // A missing event resolves to null: a query can never hold undefined, which would show a load error instead.
    queryFn: async ({ signal }) => (await eventAuthoringService.load(eventId!, signal)) ?? null,
    enabled: canLoad && isGuid(eventId),
  });

  if (!isGuid(eventId)) return <NotFoundPage />;
  if (!canLoad)
    return (
      <EditorStateFrame>
        <ForbiddenState title={t("Auto_EventAdministratorAccessRequired_d59039c1")}>
          {t("Auto_EventAuthoringIsVisibleOnlyToAuthorizedOperationPlanners_b8740e86")}
        </ForbiddenState>
      </EditorStateFrame>
    );
  if (query.isPending)
    return (
      <EditorStateFrame>
        <LoadingState label={t("Auto_LoadingEventEditor_f4351c62")} />
      </EditorStateFrame>
    );
  if (query.isError)
    return (
      <EditorStateFrame>
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("React_EventAuthoring_LoadError"), query.error.message)}
        </ErrorState>
      </EditorStateFrame>
    );
  if (!query.data)
    return (
      <EditorStateFrame>
        <EmptyState
          title={t("React_EventAuthoring_NotFoundTitle")}
          action={
            <ActionLink variant="ghost" to="/command/events">
              {t("Auto_BackToEvents_6f92c820")}
            </ActionLink>
          }
        >
          {t("Pages_Events_Detail_Error_NotFound")}
        </EmptyState>
      </EditorStateFrame>
    );
  if (!canEditEvent(user, query.data.event.hostedBy?.primaryKey))
    return (
      <EditorStateFrame>
        <ForbiddenState title={t("Auto_EventAdministratorAccessRequired_d59039c1")}>
          {t("Auto_EventAuthoringIsVisibleOnlyToAuthorizedOperationPlanners_b8740e86")}
        </ForbiddenState>
      </EditorStateFrame>
    );
  return <Editor snapshot={query.data} />;
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

function Readiness({
  rows,
  compact = false,
}: {
  rows: readonly (readonly [TranslationKey, boolean])[];
  compact?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <div className={compact ? "event-authoring-checklist is-compact" : "event-authoring-checklist"}>
      {rows.map(([label, complete]) => (
        <div key={label} className={complete ? "is-complete" : ""}>
          <StatusBadge tone={complete ? "success" : "warning"}>
            {complete
              ? t("Pages_Events_Edit_Status_Complete")
              : t("Pages_Events_Edit_Status_NeedsAttention")}
          </StatusBadge>
          <strong>{t(label)}</strong>
        </div>
      ))}
    </div>
  );
}

function formatDate(value: string | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.valueOf())
    ? "—"
    : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}
