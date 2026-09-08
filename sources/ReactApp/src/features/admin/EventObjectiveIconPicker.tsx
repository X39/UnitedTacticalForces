import { useId } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import {
  EventTacticalIcon,
  eventMissionIconName,
  type EventMissionIconName,
} from "@/features/events/EventTacticalIcon";

const objectiveIconLabelKeys = {
  document: "React_EventAuthoring_ObjectiveIcon_Document",
  extract: "React_EventAuthoring_ObjectiveIcon_Extract",
  host: "React_EventAuthoring_ObjectiveIcon_Host",
  intel: "React_EventAuthoring_ObjectiveIcon_Intel",
  map: "React_EventAuthoring_ObjectiveIcon_Map",
  medical: "React_EventAuthoring_ObjectiveIcon_Medical",
  modpack: "React_EventAuthoring_ObjectiveIcon_ModPack",
  objective: "React_EventAuthoring_ObjectiveIcon_Objective",
  radio: "React_EventAuthoring_ObjectiveIcon_Radio",
  rally: "React_EventAuthoring_ObjectiveIcon_Rally",
  slot: "React_EventAuthoring_ObjectiveIcon_Slot",
  stealth: "React_EventAuthoring_ObjectiveIcon_Stealth",
  support: "React_EventAuthoring_ObjectiveIcon_Support",
  time: "React_EventAuthoring_ObjectiveIcon_Time",
  warning: "React_EventAuthoring_ObjectiveIcon_Warning",
} satisfies Record<EventMissionIconName, TranslationKey>;

export function EventObjectiveIconPicker({
  label,
  value,
  options,
  disabled = false,
  onChange,
}: {
  label: string;
  value: string;
  options: readonly string[];
  disabled?: boolean;
  onChange(value: EventMissionIconName): void;
}) {
  const { t } = useTranslation();
  const groupName = useId();
  const selectedIcon = eventMissionIconName(value);
  const selectedLabel = t(objectiveIconLabelKeys[selectedIcon]);

  return (
    <fieldset className="event-objective-icon-picker" disabled={disabled}>
      <legend>
        <span>{label}</span> <strong>{selectedLabel}</strong>
      </legend>
      <div>
        {options.map((option, index) => {
          const icon = eventMissionIconName(option);
          const optionLabel = t(objectiveIconLabelKeys[icon]);
          const optionId = `${groupName}-option-${index}`;
          const selected = icon === selectedIcon;
          return (
            <label
              className={selected ? "is-selected" : undefined}
              data-icon={icon}
              htmlFor={optionId}
              key={icon}
              title={optionLabel}
            >
              <input
                className="sr-only"
                id={optionId}
                name={groupName}
                type="radio"
                value={icon}
                checked={selected}
                onChange={() => onChange(icon)}
              />
              <EventTacticalIcon name={icon} />
              <span className="sr-only">{optionLabel}</span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
