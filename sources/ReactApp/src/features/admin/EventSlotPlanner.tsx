import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { ConfirmDialog, StatusBadge } from "@/components/primitives";
import { EventEntityPicker } from "@/features/admin/EventEntityPicker";
import {
  createSlotDraft,
  eventAuthoringService,
  type ArmaSide,
  type EventPerson,
  type EventSlotDraft,
} from "@/features/admin/eventAuthoring";

const sides: readonly ArmaSide[] = [
  "Empty",
  "East",
  "West",
  "Resistance",
  "Civilian",
  "AmbientLife",
  "Logic",
];

function sideKey(side: ArmaSide): TranslationKey {
  return (side === "AmbientLife" ? "Ambient" : side) as TranslationKey;
}

function statusTone(slot: EventSlotDraft): "neutral" | "success" | "warning" | "info" {
  if (!slot.isVisible) return "neutral";
  if (slot.assignedToFk) return "success";
  return slot.isSelfAssignable ? "info" : "warning";
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
  const visibleSlots = slots.filter((slot) => !slot.isDeleted);
  const groups = useMemo(() => {
    const grouped = new Map<string, EventSlotDraft[]>();
    for (const slot of visibleSlots) {
      const name = slot.group.trim() || t("Pages_Events_Edit_Status_Ungrouped");
      grouped.set(name, [...(grouped.get(name) ?? []), slot]);
    }
    return [...grouped.entries()].sort(([left], [right]) => left.localeCompare(right));
  }, [t, visibleSlots]);
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
        <div className="event-slot-planner__groups">
          {groups.map(([group, groupSlots]) => {
            const rows = groupSlots ?? [];
            return (
              <section key={group} className="event-slot-group">
                <header>
                  <h3>{group}</h3>
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
                    const candidate = slot.assignmentCandidate;
                    return (
                      <article key={slot.draftKey} className="event-slot-editor-row">
                        <div className="event-slot-editor-row__fields">
                          <label>
                            <span>{t("Group")}</span>
                            <input
                              value={slot.group}
                              disabled={disabled}
                              maxLength={1024}
                              onChange={(event) =>
                                updateSlot(slot.draftKey, { group: event.target.value })
                              }
                            />
                          </label>
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
                            <span>{t("Side")}</span>
                            <select
                              value={slot.side}
                              disabled={disabled}
                              onChange={(event) =>
                                updateSlot(slot.draftKey, { side: event.target.value as ArmaSide })
                              }
                            >
                              {sides.map((side) => (
                                <option key={side} value={side}>
                                  {t(sideKey(side))}
                                </option>
                              ))}
                            </select>
                          </label>
                        </div>

                        <div className="event-slot-editor-row__assignment">
                          <div>
                            <strong>
                              {slot.assignedUser?.nickname ||
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
                          <EventEntityPicker<EventPerson>
                            label={t("Auto_AssignMember_c88914b5")}
                            placeholder={t("Auto_SearchMember_ef216823")}
                            selected={candidate}
                            disabled={disabled}
                            queryKey={`slot-user-${slot.draftKey}`}
                            load={(search, signal) =>
                              eventAuthoringService.searchUsers(search, signal)
                            }
                            getKey={(person) => person.primaryKey}
                            getLabel={(person) => person.nickname}
                            loadingLabel={t("React_EventAuthoring_SelectorLoading")}
                            emptyLabel={t("React_EventAuthoring_SelectorEmpty")}
                            errorLabel={t("React_EventAuthoring_SelectorError")}
                            onSelect={(person) =>
                              updateSlot(slot.draftKey, { assignmentCandidate: person })
                            }
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
                            disabled={
                              disabled || !candidate || candidate.primaryKey === slot.assignedToFk
                            }
                            onClick={() => candidate && onAssign(slot, candidate)}
                          >
                            {t("Pages_Events_Edit_ActionAssignSlot")}
                          </button>
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
