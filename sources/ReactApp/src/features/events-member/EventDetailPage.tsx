import {
  ArrowDownload20Regular,
  ArrowLeft20Regular,
  ArrowSync20Regular,
  Box20Regular,
  CalendarLtr20Regular,
  Checkmark20Regular,
  Clock20Regular,
  Cube20Regular,
  Document20Regular,
  Map20Regular,
  PeopleTeam20Regular,
  PeopleTeam24Regular,
  Person20Regular,
  Settings20Regular,
} from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { eventService, queryKeys } from "@/api/services";
import type { EventAcceptance, FullEvent, FullUser } from "@/api/types";
import { currentLocale, formatDate, formatDateTime } from "@/app/format";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { canEditEvent } from "@/auth/claims";
import { NotFoundPage } from "@/components/NotFoundPage";
import { ConfirmDialog, Markdown, StatePanel, StatusBadge } from "@/components/primitives";
import {
  EventTacticalIcon,
  eventMissionIconName,
  eventRoleIconName,
} from "@/features/events/EventTacticalIcon";
import { EventSideMark } from "@/features/events/EventSideMark";
import { removeObjectiveBlock } from "@/features/events/eventBriefing";
import { eventFailureResourceKey } from "./eventErrors";
import {
  attendanceChangeNeedsSlotLossConfirmation,
  canSelfAssignSlot,
  currentUserSlot,
  eventAttendanceProgress,
  eventHasStarted,
  extractBriefingObjectives,
  formatEventTimeRemaining,
  groupEventSlots,
  isEventId,
  modPackDownloadPath,
  safeDownloadFileName,
  slotRowAction,
} from "./eventModel";
import { eventsMemberQueryKeys, eventsMemberService, saveBlob } from "./services";
import type { EventAssignee, EventSlot, EventSlotRow } from "./types";

export function EventDetailPage() {
  const { t, i18n } = useTranslation();
  const { eventId = "" } = useParams();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const validEventId = isEventId(eventId);
  const fallbackImage = useSiteImage(
    "home-operation-card",
    "/media/generated/mission-overwatch-banner.jpg",
  );
  const [attendanceConfirmation, setAttendanceConfirmation] = useState<EventAcceptance | null>(
    null,
  );

  const eventQuery = useQuery({
    queryKey: queryKeys.event(eventId),
    queryFn: ({ signal }) => eventService.detail(eventId, signal),
    enabled: validEventId,
    retry: false,
  });
  const slotsQuery = useQuery({
    queryKey: eventsMemberQueryKeys.slots(eventId),
    queryFn: ({ signal }) => eventsMemberService.slots(eventId, signal),
    enabled: validEventId,
    retry: false,
  });
  const assignedUserIds = useMemo(
    () => [
      ...new Set(
        (slotsQuery.data ?? [])
          .map((slot) => slot.assignedToFk)
          .filter((userId): userId is string => Boolean(userId)),
      ),
    ],
    [slotsQuery.data],
  );
  const assigneesQuery = useQuery({
    queryKey: eventsMemberQueryKeys.assignees(eventId, assignedUserIds),
    queryFn: ({ signal }) => eventsMemberService.assignees(assignedUserIds, signal),
    enabled: auth.isAuthenticated && assignedUserIds.length > 0,
    staleTime: 5 * 60 * 1000,
  });

  const invalidateEvent = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) }),
      queryClient.invalidateQueries({ queryKey: eventsMemberQueryKeys.slots(eventId) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.upcomingEvents }),
    ]);
  };
  const attendanceMutation = useMutation({
    mutationFn: (acceptance: EventAcceptance) => eventService.setAcceptance(eventId, acceptance),
    onSuccess: invalidateEvent,
  });
  const slotMutation = useMutation({
    mutationFn: (slotNumber: number) => eventsMemberService.assignSlot(eventId, slotNumber),
    onSuccess: invalidateEvent,
  });
  const downloadMutation = useMutation({
    mutationFn: async (event: FullEvent) => {
      const path = modPackDownloadPath(event);
      if (!path) return;
      const blob = await eventsMemberService.downloadModPack(path);
      saveBlob(blob, safeDownloadFileName(event.modPackDefinitionTitle));
    },
  });

  const event = eventQuery.data;
  const slots = useMemo(() => slotsQuery.data ?? [], [slotsQuery.data]);
  const mySlot = currentUserSlot(slots, auth.user);
  const sideGroups = useMemo(
    () =>
      groupEventSlots(
        slots,
        t("Pages_Events_Detail_Status_Ungrouped"),
        t("Pages_Events_Detail_Status_UnassignedRole"),
        i18n.resolvedLanguage,
      ),
    [i18n.resolvedLanguage, slots, t],
  );
  const briefingObjectives = useMemo(
    () => extractBriefingObjectives(event?.description),
    [event?.description],
  );
  const briefingBody = useMemo(
    () => removeObjectiveBlock(event?.description),
    [event?.description],
  );
  useDocumentTitle(event?.title ?? t("Pages_Events_Detail_PageTitleFallback"));

  if (!validEventId) return <NotFoundPage />;

  if (!eventQuery.isLoading && !eventQuery.isError && !event) {
    return (
      <div className="narrow-page">
        <StatePanel
          kind="empty"
          title={t("Auto_OperationUnavailable_9b70eeab")}
          action={
            <Link className="button button--ghost" to="/events/upcoming">
              {t("Auto_BackToOperations_3057840c")}
            </Link>
          }
        >
          {t("Pages_Events_Detail_Error_NotFound")}
        </StatePanel>
      </div>
    );
  }

  if (eventQuery.isLoading || slotsQuery.isLoading) {
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Auto_OperationIsLoading_e4663d01")}</StatePanel>
      </div>
    );
  }

  if (eventQuery.isError || slotsQuery.isError || !event) {
    return (
      <div className="narrow-page">
        <StatePanel
          kind="error"
          title={t("Auto_OperationUnavailable_9b70eeab")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void Promise.all([eventQuery.refetch(), slotsQuery.refetch()])}
            >
              <ArrowSync20Regular /> {t("React_UI_Retry")}
            </button>
          }
        >
          {t(eventFailureResourceKey(eventQuery.error ?? slotsQuery.error, "load"))}
        </StatePanel>
      </div>
    );
  }

  const started = eventHasStarted(event);
  const image =
    resolveImageUrl(event.imagePath) ?? resolveImageUrl(event.terrainImagePath) ?? fallbackImage;
  const openSlots = slots.filter(
    (slot) => slot.assignedToFk == null && slot.isVisible !== false,
  ).length;
  const totalSlots = slots.length;
  const canManageEvent = canEditEvent(auth.user, event.hostedBy?.primaryKey);
  const attendanceDisabled = !auth.isAuthenticated || started || attendanceMutation.isPending;
  const attendanceProgress = eventAttendanceProgress(event);
  const timeRemaining = formatEventTimeRemaining(event.scheduledFor);
  const firstOpenSlot = slots.find((slot) =>
    canSelfAssignSlot(slot, auth.user, event, slotMutation.isPending),
  );
  const setAttendance = (acceptance: EventAcceptance) => {
    if (attendanceChangeNeedsSlotLossConfirmation(mySlot, acceptance))
      setAttendanceConfirmation(acceptance);
    else attendanceMutation.mutate(acceptance);
  };

  return (
    <div className="em-event-page public-page">
      <section
        className="em-event-hero"
        style={{ "--hero-image": `url("${image.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div className="em-event-hero__inner">
          <Link className="em-event-hero__back" to="/events/upcoming">
            <ArrowLeft20Regular /> {t("Auto_BackToEvents_6f92c820")}
          </Link>
          <span className="em-operation-tag">{t("Operation")}</span>
          <h1>{event.title || t("Pages_Events_Detail_UntitledOperation")}</h1>
          <dl className="em-event-meta" aria-label={t("Auto_MissionMetadata_384bdc08")}>
            <div>
              <CalendarLtr20Regular aria-hidden="true" />
              <div>
                <dt>{t("Date")}</dt>
                <dd>
                  {event.scheduledFor
                    ? formatDate(event.scheduledFor, {
                        day: "2-digit",
                        month: "short",
                        year: "numeric",
                      })
                    : t("Pages_Events_Detail_Status_DatePending")}
                </dd>
                <small>
                  {event.scheduledFor
                    ? formatDate(event.scheduledFor, { weekday: "long" })
                    : t("Pages_Events_Detail_Status_SchedulePending")}
                </small>
              </div>
            </div>
            <div>
              <Clock20Regular aria-hidden="true" />
              <div>
                <dt>{t("Time")}</dt>
                <dd>
                  {event.scheduledFor
                    ? formatEventUtcTime(event.scheduledFor)
                    : t("Pages_Events_Detail_Status_TimePending")}
                </dd>
                <small>
                  {event.scheduledFor
                    ? formatResource(
                        t("Pages_Events_Detail_Format_LocalTime"),
                        formatDateTime(event.scheduledFor),
                      )
                    : t("Pages_Events_Detail_Status_LocalTimePending")}
                </small>
              </div>
            </div>
            <div>
              <Map20Regular aria-hidden="true" />
              <div>
                <dt>{t("Terrain")}</dt>
                <dd>{event.terrainTitle || t("Pages_Events_Detail_Status_TerrainPending")}</dd>
                <small>{t("Auto_MissionArea_783638f1")}</small>
              </div>
            </div>
            <div>
              <Cube20Regular aria-hidden="true" />
              <div>
                <dt>{t("Modpack")}</dt>
                <dd>
                  {event.modPackDefinitionTitle || t("Pages_Events_Detail_Status_ModPackPending")}
                </dd>
                <small>
                  {event.modPackRevisionPrimaryKey == null
                    ? t("Pages_Events_Detail_Status_LatestRevision")
                    : formatResource(
                        t("Pages_Events_Detail_Count_Revision"),
                        event.modPackRevisionPrimaryKey,
                      )}
                </small>
              </div>
            </div>
            <div>
              <Person20Regular aria-hidden="true" />
              <div>
                <dt>{t("Host")}</dt>
                <dd>{event.hostedBy.nickname || t("Pages_Events_Detail_Status_HostPending")}</dd>
                <small>{t("Auto_OperationLead_56551686")}</small>
              </div>
            </div>
          </dl>
        </div>
      </section>

      <div className="em-event-workspace">
        <div className="em-event-main">
          <section className="em-panel em-briefing-panel">
            <header className="em-panel-title">
              <span>
                <Document20Regular aria-hidden="true" />
              </span>
              <h2>{t("Auto_OperationBrief_b34e36ec")}</h2>
            </header>
            {event.description ? (
              <>
                {briefingObjectives.length > 0 && (
                  <ol
                    className="em-briefing-objectives"
                    aria-label={t("Auto_PublicBriefingHighlights_dd5597fe")}
                  >
                    {briefingObjectives.map((objective, index) => (
                      <li key={`${objective.icon}-${index}`}>
                        <EventTacticalIcon
                          name={eventMissionIconName(objective.icon)}
                          className="em-objective-icon"
                        />
                        <p>{objective.text}</p>
                      </li>
                    ))}
                  </ol>
                )}
                <div className="em-briefing-notes">
                  <span className="kicker">{t("Auto_FullBriefing_8df6347a")}</span>
                  <h3>{t("Auto_CommanderSNotes_99b2f4e5")}</h3>
                  <Markdown>{briefingBody}</Markdown>
                </div>
              </>
            ) : (
              <StatePanel kind="empty">
                {t("Auto_NoFullBriefingHasBeenPostedForThisOperation_574adb11")}
              </StatePanel>
            )}
          </section>

          <section className="em-panel em-roster-panel" aria-labelledby="slot-register-title">
            <header className="em-roster-panel__header">
              <div className="em-panel-title">
                <span>
                  <PeopleTeam20Regular aria-hidden="true" />
                </span>
                <h2 id="slot-register-title">{t("Auto_SlotRegister_20eeba00")}</h2>
              </div>
              <p>
                {openSlots} {t("Auto_OpenSlots_183c44b6")} / {totalSlots} {t("Slots")}
              </p>
            </header>
            {sideGroups.length === 0 ? (
              <StatePanel kind="empty">{t("Auto_NoSlotsConfigured_d0093e9c")}</StatePanel>
            ) : (
              <div className="em-slot-register">
                <div className="em-slot-register__head" aria-hidden="true">
                  <span>{t("Role")}</span>
                  <span>{t("Assigned")}</span>
                  <span>{t("Status")}</span>
                  <span />
                </div>
                {sideGroups.map((sideGroup) => (
                  <section
                    key={sideGroup.id}
                    className="em-slot-side"
                    data-side={sideGroup.id}
                    aria-labelledby={`slot-side-${sideGroup.id}`}
                  >
                    <header className="em-slot-side__header">
                      <div>
                        <EventSideMark side={sideGroup.id} className="em-slot-side__emblem" />
                        <div>
                          <span>{t("Side")}</span>
                          <h3 id={`slot-side-${sideGroup.id}`}>
                            {sideGroup.labelKey
                              ? t(sideGroup.labelKey)
                              : t("Pages_Events_Detail_Status_SidePending")}
                          </h3>
                        </div>
                      </div>
                      <span>
                        {formatResource(
                          t("Pages_Events_Detail_Count_GroupAssigned"),
                          sideGroup.assignedCount,
                          sideGroup.totalCount,
                        )}
                      </span>
                    </header>
                    <div className="em-slot-side__groups">
                      {sideGroup.groups.map((group) => (
                        <section key={`${sideGroup.id}|${group.name}`} className="em-slot-group">
                          <header>
                            <div>
                              <span className="em-slot-group__icon" aria-hidden="true" />
                              <h4>{group.name}</h4>
                            </div>
                            <span>
                              {formatResource(
                                t("Pages_Events_Detail_Count_GroupAssigned"),
                                group.assignedCount,
                                group.totalCount,
                              )}
                            </span>
                          </header>
                          {group.rows.map((row) => (
                            <SlotRow
                              key={row.key}
                              row={row}
                              event={event}
                              assignees={assigneesQuery.data ?? new Map()}
                              user={auth.user}
                              isAuthenticated={auth.isAuthenticated}
                              assigningSlotNumber={
                                slotMutation.isPending ? slotMutation.variables : undefined
                              }
                              onAssign={(slot) => slotMutation.mutate(slot.slotNumber)}
                            />
                          ))}
                        </section>
                      ))}
                    </div>
                  </section>
                ))}
              </div>
            )}
          </section>
        </div>

        <aside className="em-event-aside" aria-label={t("Attendance")}>
          {canManageEvent && (
            <section className="em-side-card em-event-control-card">
              <header className="em-side-card__header">
                <Settings20Regular aria-hidden="true" />
                <h2>{t("Auto_EventControls_ad2e84e1")}</h2>
              </header>
              <p>{t("Auto_OpenThisOperationInCommandManagement_604a555e")}</p>
              <Link className="button button--ghost" to={`/command/events/${eventId}`}>
                {t("Auto_ManageEvent_0a863949")}
              </Link>
            </section>
          )}

          <section className="em-side-card">
            <header className="em-side-card__header">
              <Checkmark20Regular aria-hidden="true" />
              <h2>{t("Auto_YourAttendance_71dff290")}</h2>
            </header>
            <div className="em-attendance-actions">
              <AttendanceButton
                label={t("Pages_Events_Detail_Status_Confirmed")}
                count={event.acceptedCount ?? 0}
                tone="success"
                disabled={attendanceDisabled}
                pending={attendanceMutation.isPending && attendanceMutation.variables === "accept"}
                onClick={() => setAttendance("accept")}
              />
              <AttendanceButton
                label={t("Pages_Events_Detail_Status_Pending")}
                count={event.maybeCount ?? 0}
                tone="warning"
                disabled={attendanceDisabled}
                pending={attendanceMutation.isPending && attendanceMutation.variables === "maybe"}
                onClick={() => setAttendance("maybe")}
              />
              <AttendanceButton
                label={t("Pages_Events_Detail_Status_Unavailable")}
                count={event.rejectedCount ?? 0}
                tone="danger"
                disabled={attendanceDisabled}
                pending={attendanceMutation.isPending && attendanceMutation.variables === "reject"}
                onClick={() => setAttendance("reject")}
              />
            </div>
            <button
              className="em-primary-slot-action"
              type="button"
              disabled={!firstOpenSlot || slotMutation.isPending}
              aria-busy={slotMutation.isPending}
              onClick={() => {
                if (firstOpenSlot) slotMutation.mutate(firstOpenSlot.slotNumber);
              }}
            >
              {slotMutation.isPending
                ? t("Pages_Events_Detail_ActionAssigningSlot")
                : !auth.isAuthenticated
                  ? t("Pages_Events_Detail_ActionSignInToAssign")
                  : started
                    ? t("Pages_Events_Detail_Status_OperationStarted")
                    : firstOpenSlot
                      ? t("Pages_Events_Detail_ActionAssignSlot")
                      : t("Pages_Events_Detail_Status_NoOpenSlots")}
            </button>
            {!auth.isAuthenticated && (
              <p>{t("Auto_SignInToManageAttendanceAndClaimOpenSlots_9bd2cdf2")}</p>
            )}
            {started && <p>{t("Auto_ThisOperationHasAlreadyStarted_d6da2397")}</p>}
            {attendanceMutation.isError && (
              <StatePanel kind="error">
                {t(eventFailureResourceKey(attendanceMutation.error, "attendance"))}
              </StatePanel>
            )}
            {slotMutation.isError && (
              <StatePanel kind="error">
                {t(eventFailureResourceKey(slotMutation.error, "slot"))}
              </StatePanel>
            )}
          </section>

          <section className="em-side-card em-slot-summary">
            <PeopleTeam24Regular className="em-slot-summary__icon" aria-hidden="true" />
            <div className="em-attendance-progress">
              <div>
                <strong>
                  {attendanceProgress.minimum > 0
                    ? formatResource(
                        t("Pages_Events_Detail_Count_AcceptedMinimum"),
                        attendanceProgress.accepted,
                        attendanceProgress.minimum,
                      )
                    : formatResource(
                        t("Pages_Events_Detail_Count_Accepted"),
                        attendanceProgress.accepted,
                      )}
                </strong>
                <small>
                  {t(
                    attendanceProgress.needsMoreSignups
                      ? "Pages_Events_Detail_Status_NeedsMoreSignups"
                      : "Pages_Events_Detail_Status_MinimumMet",
                  )}
                </small>
              </div>
              <div
                className="em-slot-meter"
                role="progressbar"
                aria-label={t("Pages_Events_Detail_Status_Confirmed")}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={Math.round(attendanceProgress.percentage)}
              >
                <span style={{ width: `${attendanceProgress.percentage}%` }} />
              </div>
            </div>
            <dl>
              <div>
                <dt>{t("Auto_OpenSlots_183c44b6")}</dt>
                <dd>{openSlots}</dd>
              </div>
              <div>
                <dt>{t("Auto_TotalSlots_7dea1c50")}</dt>
                <dd>{totalSlots}</dd>
              </div>
              <div>
                <dt>{t("Auto_YourSlots_ee1b12de")}</dt>
                <dd>{mySlot ? 1 : 0}</dd>
              </div>
              <div>
                <dt>{t("Auto_TimeRemaining_864927fe")}</dt>
                <dd>
                  {timeRemaining === "tbd"
                    ? t("Pages_Events_Detail_Status_ToBeDetermined")
                    : timeRemaining === "started"
                      ? t("Pages_Events_Detail_Status_Started")
                      : timeRemaining}
                </dd>
              </div>
            </dl>
          </section>

          <section className="em-side-card">
            <header className="em-side-card__header">
              <Box20Regular aria-hidden="true" />
              <h2>{t("Auto_MissionPackage_3ff65611")}</h2>
            </header>
            <dl className="em-package-list">
              <div>
                <dt>{t("Modpack")}</dt>
                <dd>{event.modPackDefinitionTitle || t("Pages_Events_Detail_Status_Pending")}</dd>
              </div>
              <div>
                <dt>{t("Terrain")}</dt>
                <dd>{event.terrainTitle || t("Pages_Events_Detail_Status_Pending")}</dd>
              </div>
              <div>
                <dt>{t("Host")}</dt>
                <dd>{event.hostedBy.nickname || t("Pages_Events_Detail_Status_Pending")}</dd>
              </div>
            </dl>
            <button
              className="em-primary-slot-action"
              type="button"
              disabled={!modPackDownloadPath(event) || downloadMutation.isPending}
              aria-busy={downloadMutation.isPending}
              onClick={() => downloadMutation.mutate(event)}
            >
              <ArrowDownload20Regular aria-hidden="true" /> {t("Common_DownloadModpack")}
            </button>
            {downloadMutation.isError && (
              <StatePanel kind="error">
                {t(eventFailureResourceKey(downloadMutation.error, "download"))}
              </StatePanel>
            )}
          </section>
        </aside>
      </div>

      <ConfirmDialog
        open={attendanceConfirmation != null}
        title={t("Dialogs_ConfirmSlotLoss_ConfirmSlotLossOnAcceptanceChange")}
        description={t("Dialogs_ConfirmSlotLoss_SelectingSlotWillRemoveSlotSelection")}
        confirmLabel={t("Dialogs_ConfirmSlotSelection_ConfirmAcceptanceStatusChange")}
        cancelLabel={t("Common_Cancel")}
        onCancel={() => setAttendanceConfirmation(null)}
        onConfirm={() => {
          if (attendanceConfirmation) attendanceMutation.mutate(attendanceConfirmation);
          setAttendanceConfirmation(null);
        }}
      />
    </div>
  );
}

function AttendanceButton({
  label,
  count,
  tone,
  disabled,
  pending,
  onClick,
}: {
  label: string;
  count: number;
  tone: "success" | "warning" | "danger";
  disabled: boolean;
  pending: boolean;
  onClick(): void;
}) {
  return (
    <button
      className={`em-attendance-choice em-attendance-choice--${tone}`}
      type="button"
      disabled={disabled}
      aria-busy={pending}
      onClick={onClick}
    >
      <span className="em-attendance-choice__mark" aria-hidden="true">
        {tone === "success" ? "✓" : tone === "warning" ? "?" : "×"}
      </span>
      <strong>{pending ? "…" : label}</strong>
      <span>{count}</span>
    </button>
  );
}

function formatEventUtcTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return `${new Intl.DateTimeFormat(currentLocale(), {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "UTC",
  }).format(date)} UTC`;
}

function assigneeAvatarSource(user: EventAssignee): string | undefined {
  if (!user.avatar) return undefined;
  const mimeType = user.avatarMimeType?.split(";", 1)[0]?.trim().toLocaleLowerCase();
  const safeMimeType =
    mimeType && /^image\/(?:png|jpe?g|gif|webp)$/.test(mimeType) ? mimeType : "image/png";
  return `data:${safeMimeType};base64,${user.avatar}`;
}

function assigneeInitials(name: string): string {
  return (
    name
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => Array.from(part)[0]?.toLocaleUpperCase() ?? "")
      .join("") || "?"
  );
}

function SlotRow({
  row,
  event,
  assignees,
  user,
  isAuthenticated,
  assigningSlotNumber,
  onAssign,
}: {
  row: EventSlotRow;
  event: FullEvent;
  assignees: Map<string, EventAssignee>;
  user: FullUser | undefined;
  isAuthenticated: boolean;
  assigningSlotNumber: number | undefined;
  onAssign(slot: EventSlot): void;
}) {
  const { t } = useTranslation();
  const mine = row.slots.some((slot) => slot.assignedToFk === user?.primaryKey);
  const action = slotRowAction(row, isAuthenticated ? user : undefined, event, assigningSlotNumber);
  const mutationPending = assigningSlotNumber != null;
  const assignedUsers = [
    ...new Map(
      row.slots
        .map((slot) => (slot.assignedToFk ? assignees.get(slot.assignedToFk) : undefined))
        .filter((assignee): assignee is EventAssignee => Boolean(assignee))
        .map((assignee) => [assignee.primaryKey, assignee]),
    ).values(),
  ];
  const assignedNames = assignedUsers.map((assignee) => assignee.nickname);
  const assignedLabel =
    assignedNames.length <= 1
      ? assignedNames[0]
      : `${assignedNames[0]} + ${assignedNames.length - 1}`;
  const slotNumbers = row.slots.map((slot) => slot.slotNumber).join(", ");
  const isHidden = row.slots.every((slot) => slot.isVisible === false);
  const rowStatus = isHidden
    ? { label: t("Pages_Events_Detail_Status_Hidden"), tone: "neutral" as const }
    : mine
      ? { label: t("Pages_Events_Detail_Status_YourSlot"), tone: "info" as const }
      : action.kind === "locked"
        ? { label: t("Pages_Events_Detail_Status_Locked"), tone: "warning" as const }
        : action.kind === "closed"
          ? { label: t("Pages_Events_Detail_Status_Closed"), tone: "warning" as const }
          : row.assignedCount > 1
            ? {
                label: t("Pages_Events_Detail_Status_ConfirmedTeam"),
                tone: "success" as const,
              }
            : row.assignedCount === 1
              ? {
                  label: t("Pages_Events_Detail_Status_ConfirmedMember"),
                  tone: "success" as const,
                }
              : row.openCount > 0
                ? {
                    label: t("Pages_Events_Detail_Status_Available"),
                    tone: "neutral" as const,
                  }
                : {
                    label: t("Pages_Events_Detail_Status_Assigned"),
                    tone: "neutral" as const,
                  };
  const actionLabel = {
    assign: t("Pages_EventView_AssignToSelf"),
    "claim-role": t("Pages_Events_Detail_ActionClaimRole"),
    assigned: t("Pages_Events_Detail_Status_Assigned"),
    assigning: t("Pages_Events_Detail_ActionAssigning"),
    closed: t("Pages_Events_Detail_Status_Closed"),
    current: t("Pages_Events_Detail_Status_YourSlot"),
    hidden: t("Pages_Events_Detail_Status_Hidden"),
    locked: t("Pages_Events_Detail_Status_Locked"),
    "sign-in": t("Pages_Events_Detail_ActionSignIn"),
  }[action.kind];

  return (
    <article className={`em-slot-row${mine ? " em-slot-row--mine" : ""}`}>
      <div className="em-slot-role-cell">
        <EventTacticalIcon name={eventRoleIconName(row.title)} className="em-slot-role-icon" />
        <div>
          <h5>{row.slots.length > 1 ? `${row.title} (x${row.slots.length})` : row.title}</h5>
          <small>
            {formatResource(
              row.slots.length > 1
                ? t("Pages_Events_Detail_Count_SlotNumbers")
                : t("Pages_Events_Detail_Count_SlotNumber"),
              slotNumbers,
            )}
          </small>
        </div>
      </div>
      <div className="em-slot-assignees">
        {assignedUsers.length > 0 && (
          <div className="em-slot-avatars" aria-hidden="true">
            {assignedUsers.slice(0, 3).map((assignee) => {
              const avatar = assigneeAvatarSource(assignee);
              return avatar ? (
                <img
                  className="em-slot-avatar"
                  key={assignee.primaryKey}
                  src={avatar}
                  alt=""
                  width={32}
                  height={32}
                />
              ) : (
                <span className="em-slot-avatar" key={assignee.primaryKey}>
                  {assigneeInitials(assignee.nickname)}
                </span>
              );
            })}
          </div>
        )}
        <div>
          <strong>
            {isHidden
              ? t("Pages_Events_Detail_Status_Hidden")
              : assignedLabel
                ? assignedLabel
                : row.assignedCount > 0
                  ? formatResource(
                      t("Pages_Events_Detail_Count_AssignedMembers"),
                      row.assignedCount,
                    )
                  : t("Auto_OpenSlot_00336ce6")}
          </strong>
          <small>
            {row.assignedCount > 0
              ? formatResource(t("Pages_Events_Detail_Count_AssignedMembers"), row.assignedCount)
              : `${row.openCount} ${t("Auto_OpenSlots_183c44b6")}`}
          </small>
        </div>
      </div>
      <div className="em-slot-status">
        <StatusBadge tone={rowStatus.tone}>{rowStatus.label}</StatusBadge>
        <small>{actionLabel}</small>
      </div>
      <button
        className={`em-slot-assign-button${mine ? " em-slot-assign-button--mine" : ""}`}
        type="button"
        disabled={mutationPending || (action.kind !== "assign" && action.kind !== "claim-role")}
        aria-busy={action.kind === "assigning"}
        onClick={() => {
          if (!mutationPending && (action.kind === "assign" || action.kind === "claim-role"))
            onAssign(action.slot);
        }}
      >
        {actionLabel}
      </button>
    </article>
  );
}
