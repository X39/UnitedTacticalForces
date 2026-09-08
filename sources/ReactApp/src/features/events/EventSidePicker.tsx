import { useTranslation } from "react-i18next";
import { EventSideMark } from "@/features/events/EventSideMark";
import { eventSidePresentation } from "@/features/events/eventSlotPresentation";

export const editableEventSides = [
  "West",
  "East",
  "Resistance",
  "Civilian",
  "AmbientLife",
  "Logic",
  "Empty",
] as const;

export type EditableEventSide = (typeof editableEventSides)[number];

export function EventSidePicker({
  value,
  label,
  emptyLabel,
  disabled = false,
  onChange,
}: {
  value: EditableEventSide;
  label: string;
  emptyLabel: string;
  disabled?: boolean;
  onChange(value: EditableEventSide): void;
}) {
  const { t } = useTranslation();
  return (
    <fieldset className="event-side-picker" disabled={disabled}>
      <legend>{label}</legend>
      <div>
        {editableEventSides.map((side) => {
          const presentation = eventSidePresentation(side);
          const sideLabel = presentation.labelKey ? t(presentation.labelKey) : emptyLabel;
          return (
            <button
              key={side}
              type="button"
              data-side={presentation.id}
              className={side === value ? "is-selected" : undefined}
              aria-pressed={side === value}
              title={sideLabel}
              onClick={() => onChange(side)}
            >
              <EventSideMark side={side} />
              <span>{sideLabel}</span>
            </button>
          );
        })}
      </div>
    </fieldset>
  );
}
