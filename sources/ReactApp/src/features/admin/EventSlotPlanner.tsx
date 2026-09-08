import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { ConfirmDialog, StatusBadge } from "@/components/primitives";
import { EventEntityPicker } from "@/features/admin/EventEntityPicker";
import {
  createSlotDraft,
  eventAuthoringService,
  type ArmaSide,
  type EventPerson,
  type EventSlotDraft,
} from "@/features/admin/eventAuthoring";
import { EventSideMark } from "@/features/events/EventSideMark";
import { EventSidePicker, type EditableEventSide } from "@/features/events/EventSidePicker";
import { eventSidePresentation } from "@/features/events/eventSlotPresentation";
import { EventRoleSymbolPicker } from "@/features/events/EventRoleSymbolPicker";
import {
  EventTacticalIcon,
  eventSlotRoleSymbolIconName,
} from "@/features/events/EventTacticalIcon";

function statusTone(slot: EventSlotDraft): "neutral" | "success" | "warning" | "info" {
  if (!slot.isVisible) return "neutral";
  if (slot.assignedToFk) return "success";
  return slot.isSelfAssignable ? "info" : "warning";
}

function avatarSource(person: EventPerson | null): string | undefined {
  return person?.avatar && person.avatarMimeType
    ? `data:${person.avatarMimeType};base64,${person.avatar}`
    : undefined;
}

function initials(name: string): string {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join("")
      .toLocaleUpperCase() || "?"
  );
}

export function EventSlotPlanner({
  slots,
  disabled,
  saving,
  onChange,
  onSave,
  onAssign,
  onUnassign,
  showSave = true,
}: {
  slots: EventSlotDraft[];
  disabled: boolean;
  saving: boolean;
  onChange(slots: EventSlotDraft[]): void;
  onSave(): void;
  onAssign(slot: EventSlotDraft, person: EventPerson): void;
  onUnassign(slot: EventSlotDraft): void;
  showSave?: boolean;
}) {
  const { t } = useTranslation();
  const [removeCandidate, setRemoveCandidate] = useState<EventSlotDraft>();
  const [groupAnchors, setGroupAnchors] = useState<Record<string, string>>({});
  const visibleSlots = slots.filter((slot) => !slot.isDeleted);
  const sideGroups = useMemo(() => {
    const grouped = new Map<
      string,
      { side: ArmaSide; order: number; groups: Map<string, EventSlotDraft[]> }
    >();
    for (const slot of visibleSlots) {
      const side = eventSidePresentation(slot.side);
      const name =
        (groupAnchors[slot.draftKey] ?? slot.group).trim() ||
        t("Pages_Events_Edit_Status_Ungrouped");
      const sideGroup = grouped.get(side.id) ?? {
        side: slot.side,
        order: side.order,
        groups: new Map<string, EventSlotDraft[]>(),
      };
      sideGroup.groups.set(name, [...(sideGroup.groups.get(name) ?? []), slot]);
      grouped.set(side.id, sideGroup);
    }
    return [...grouped.entries()]
      .sort(([, left], [, right]) => left.order - right.order)
      .map(([id, sideGroup]) => ({
        id,
        side: sideGroup.side,
        groups: [...sideGroup.groups.entries()].sort(([left], [right]) =>
          left.localeCompare(right),
        ),
      }));
  }, [groupAnchors, t, visibleSlots]);
  const updateSlot = (draftKey: string, update: Partial<EventSlotDraft>) =>
    onChange(slots.map((slot) => (slot.draftKey === draftKey ? { ...slot, ...update } : slot)));
  const removeSlot = (slot: EventSlotDraft) => {
    if (slot.slotNumber == null) onChange(slots.filter((item) => item.draftKey !== slot.draftKey));
    else updateSlot(slot.draftKey, { isDeleted: true });
    setRemoveCandidate(undefined);
  };
  const assignedCount = visibleSlots.filter((slot) => slot.assignedToFk).length;

  return (
    <section className="event-slot-planner" aria-label={t("Auto_EditableSlotPlan_f406ee7b")}>
      <header className="event-authoring-section-heading">
        <div>
          <span className="kicker">{t("Slots")}</span>
          <h2>{t("Auto_SlotPlan_7b3bf485")}</h2>
        </div>
        <button
          className="button button--ghost"
          type="button"
          disabled={disabled}
          onClick={() => onChange([...slots, createSlotDraft()])}
        >
          {t("Auto_AddSlot_21ac8a98")}
        </button>
      </header>

      {!visibleSlots.length ? (
        <div className="event-authoring-empty">
          {t("Auto_NoSlotsConfiguredAddAtLeastOneSlotFor_a7e2439d")}
        </div>
      ) : (
        <div className="event-slot-planner__sides">
          {sideGroups.map((sideGroup) => {
            const side = eventSidePresentation(sideGroup.side);
            const sideLabel = side.labelKey
              ? t(side.labelKey)
              : t("Pages_Events_Detail_Status_SidePending");
            const sideSlots = sideGroup.groups.flatMap(([, groupSlots]) => groupSlots);
            return (
              <section
                key={sideGroup.id}
                className="event-slot-side-section"
                data-side={sideGroup.id}
              >
                <header className="event-slot-side-section__header">
                  <div>
                    <EventSideMark side={sideGroup.side} />
                    <div>
                      <span>{t("Side")}</span>
                      <h3>{sideLabel}</h3>
                    </div>
                  </div>
                  <span>
                    {formatResource(
                      t("Pages_Events_Edit_Count_GroupAssigned"),
                      sideSlots.filter((slot) => slot.assignedToFk).length,
                      sideSlots.length,
                    )}
                  </span>
                </header>
                <div className="event-slot-side-section__groups">
                  {sideGroup.groups.map(([group, rows]) => (
                    <section key={`${sideGroup.id}|${group}`} className="event-slot-group">
                      <header>
                        <h4>{group}</h4>
                        <span>
                          {formatResource(
                            t("Pages_Events_Edit_Count_GroupAssigned"),
                            rows.filter((slot) => slot.assignedToFk).length,
                            rows.length,
                          )}
                        </span>
                      </header>
                      <div className="event-slot-group__rows">
                        {rows.map((slot) => {
                          const person = slot.assignmentCandidate ?? slot.assignedUser;
                          const image = avatarSource(person);
                          return (
                            <article
                              key={slot.draftKey}
                              className="event-slot-editor-row"
                              data-side={eventSidePresentation(slot.side).id}
                            >
                              <div className="event-slot-editor-row__identity">
                                <EventTacticalIcon
                                  name={eventSlotRoleSymbolIconName(slot.roleSymbol, slot.title)}
                                />
                                <div className="event-slot-editor-row__fields">
                                  <label>
                                    <span>{t("Role")}</span>
                                    <input
                                      value={slot.title}
                                      disabled={disabled}
                                      maxLength={1024}
                                      onChange={(event) =>
                                        updateSlot(slot.draftKey, { title: event.target.value })
                                      }
                                    />
                                  </label>
                                  <label>
                                    <span>{t("Group")}</span>
                                    <input
                                      value={slot.group}
                                      disabled={disabled}
                                      maxLength={1024}
                                      onFocus={() =>
                                        setGroupAnchors((current) =>
                                          current[slot.draftKey] === undefined
                                            ? { ...current, [slot.draftKey]: slot.group }
                                            : current,
                                        )
                                      }
                                      onChange={(event) =>
                                        updateSlot(slot.draftKey, { group: event.target.value })
                                      }
                                      onBlur={() =>
                                        setGroupAnchors((current) => {
                                          const next = { ...current };
                                          delete next[slot.draftKey];
                                          return next;
                                        })
                                      }
                                    />
                                  </label>
                                  <EventRoleSymbolPicker
                                    value={slot.roleSymbol}
                                    roleTitle={slot.title}
                                    disabled={disabled}
                                    onChange={(roleSymbol) =>
                                      updateSlot(slot.draftKey, { roleSymbol })
                                    }
                                  />
                                </div>
                              </div>

                              <EventSidePicker
                                value={slot.side as EditableEventSide}
                                label={t("Side")}
                                emptyLabel={t("Pages_Events_Detail_Status_SidePending")}
                                disabled={disabled}
                                onChange={(nextSide) =>
                                  updateSlot(slot.draftKey, { side: nextSide as ArmaSide })
                                }
                              />

                              <div className="event-slot-editor-row__assignment">
                                <div className="event-slot-editor-row__assignee">
                                  {person && image ? (
                                    <img src={image} alt="" />
                                  ) : (
                                    <span aria-hidden="true">
                                      {person ? initials(person.nickname) : "—"}
                                    </span>
                                  )}
                                  <div>
                                    <strong>
                                      {person?.nickname ||
                                        (slot.slotNumber == null
                                          ? t("Pages_Events_Edit_Status_UnassignedNewSlot")
                                          : formatResource(
                                              t("Pages_Events_Edit_Status_SlotUnassigned"),
                                              slot.slotNumber,
                                            ))}
                                    </strong>
                                    <small>
                                      {slot.slotNumber == null
                                        ? t("Pages_Events_Edit_Status_StagedAssignment")
                                        : t("Pages_Events_Edit_Status_SavedAssignment")}
                                    </small>
                                  </div>
                                </div>
                                <EventEntityPicker<EventPerson>
                                  label={t("Auto_AssignMember_c88914b5")}
                                  placeholder={t("Auto_SearchMember_ef216823")}
                                  selected={person}
                                  disabled={disabled}
                                  queryKey={`slot-user-${slot.draftKey}`}
                                  load={(search, signal) =>
                                    eventAuthoringService.searchUsers(search, signal)
                                  }
                                  getKey={(item) => item.primaryKey}
                                  getLabel={(item) => item.nickname}
                                  loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                                  emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                                  errorLabel={t("React_EventAuthoring_SelectorError")}
                                  renderOption={(nextPerson) => {
                                    const optionImage = avatarSource(nextPerson);
                                    return (
                                      <span className="event-person-picker-option">
                                        {optionImage ? (
                                          <img src={optionImage} alt="" width={32} height={32} />
                                        ) : (
                                          <span aria-hidden="true">
                                            {initials(nextPerson.nickname)}
                                          </span>
                                        )}
                                        <strong>{nextPerson.nickname}</strong>
                                      </span>
                                    );
                                  }}
                                  onSelect={(nextPerson) => onAssign(slot, nextPerson)}
                                />
                              </div>

                              <div className="event-slot-editor-row__status">
                                <StatusBadge tone={statusTone(slot)}>
                                  {!slot.isVisible
                                    ? t("Pages_Events_Edit_Status_Hidden")
                                    : slot.assignedToFk
                                      ? t("Pages_Events_Edit_Status_Assigned")
                                      : slot.isSelfAssignable
                                        ? t("Pages_Events_Edit_Status_Available")
                                        : t("Pages_Events_Edit_Status_Locked")}
                                </StatusBadge>
                                <label className="event-authoring-toggle">
                                  <input
                                    type="checkbox"
                                    checked={slot.isVisible}
                                    disabled={disabled}
                                    onChange={(event) =>
                                      updateSlot(slot.draftKey, { isVisible: event.target.checked })
                                    }
                                  />
                                  <span>{t("Visible")}</span>
                                </label>
                                <label className="event-authoring-toggle">
                                  <input
                                    type="checkbox"
                                    checked={slot.isSelfAssignable}
                                    disabled={disabled}
                                    onChange={(event) =>
                                      updateSlot(slot.draftKey, {
                                        isSelfAssignable: event.target.checked,
                                      })
                                    }
                                  />
                                  <span>{t("Auto_SelfAssign_fae0bc3e")}</span>
                                </label>
                              </div>

                              <div className="event-slot-editor-row__actions">
                                <button
                                  className="button button--ghost button--small"
                                  type="button"
                                  disabled={disabled || !slot.assignedToFk}
                                  onClick={() => onUnassign(slot)}
                                >
                                  {t("Pages_Events_Edit_ActionUnassignSlot")}
                                </button>
                                <button
                                  className="button button--ghost button--small"
                                  type="button"
                                  disabled={disabled}
                                  onClick={() => setRemoveCandidate(slot)}
                                >
                                  {t("Pages_Events_Edit_ActionRemoveSlot")}
                                </button>
                              </div>
                            </article>
                          );
                        })}
                      </div>
                    </section>
                  ))}
                </div>
              </section>
            );
          })}
        </div>
      )}

      <footer className="event-slot-planner__footer">
        <span>
          {formatResource(
            t("Pages_Events_Edit_Count_AssignedSlots"),
            assignedCount,
            visibleSlots.length,
          )}
        </span>
        {showSave && (
          <button
            className="button button--primary"
            type="button"
            disabled={disabled || saving}
            onClick={onSave}
          >
            {saving
              ? t("Pages_Events_Edit_ActionSavingSlots")
              : t("Pages_Events_Edit_ActionSaveSlotPlan")}
          </button>
        )}
      </footer>

      <ConfirmDialog
        open={Boolean(removeCandidate)}
        title={t("React_EventAuthoring_RemoveSlotTitle")}
        description={t("React_EventAuthoring_RemoveSlotDescription")}
        confirmLabel={t("Pages_Events_Edit_ActionRemoveSlot")}
        cancelLabel={t("Cancel")}
        onConfirm={() => removeCandidate && removeSlot(removeCandidate)}
        onCancel={() => setRemoveCandidate(undefined)}
      />
    </section>
  );
}
