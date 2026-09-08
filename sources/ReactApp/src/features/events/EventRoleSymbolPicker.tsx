import { useId, useRef } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import {
  EventTacticalIcon,
  eventSlotRoleSymbolIconName,
  eventSlotRoleSymbols,
  normalizeEventSlotRoleSymbol,
  type EventSlotRoleSymbol,
} from "@/features/events/EventTacticalIcon";

const roleSymbolLabelKeys = {
  Automatic: "React_EventRoleSymbol_Automatic",
  Generic: "React_EventRoleSymbol_Generic",
  Rifleman: "React_EventRoleSymbol_Rifleman",
  AutomaticRifleman: "React_EventRoleSymbol_AutomaticRifleman",
  Grenadier: "React_EventRoleSymbol_Grenadier",
  AntiArmor: "React_EventRoleSymbol_AntiArmor",
  Medic: "React_EventRoleSymbol_Medic",
  RadioOperator: "React_EventRoleSymbol_RadioOperator",
  Leader: "React_EventRoleSymbol_Leader",
  Marksman: "React_EventRoleSymbol_Marksman",
  Engineer: "React_EventRoleSymbol_Engineer",
  Crew: "React_EventRoleSymbol_Crew",
  Pilot: "React_EventRoleSymbol_Pilot",
  Support: "React_EventRoleSymbol_Support",
} satisfies Record<EventSlotRoleSymbol, TranslationKey>;

export function EventRoleSymbolPicker({
  value,
  roleTitle,
  disabled = false,
  onChange,
}: {
  value: EventSlotRoleSymbol | null | undefined;
  roleTitle: string | null | undefined;
  disabled?: boolean;
  onChange(value: EventSlotRoleSymbol): void;
}) {
  const { t } = useTranslation();
  const detailsRef = useRef<HTMLDetailsElement>(null);
  const radioGroupName = useId();
  const selectedSymbol = normalizeEventSlotRoleSymbol(value);
  const selectedLabel = t(roleSymbolLabelKeys[selectedSymbol]);
  const closeAndFocusSummary = () => {
    if (!detailsRef.current) return;
    detailsRef.current.open = false;
    detailsRef.current.querySelector<HTMLElement>("summary")?.focus();
  };

  return (
    <details
      className="event-role-symbol-picker"
      ref={detailsRef}
      onKeyDown={(event) => {
        if (event.key !== "Escape" || !detailsRef.current?.open) return;
        event.preventDefault();
        closeAndFocusSummary();
      }}
    >
      <summary
        aria-disabled={disabled || undefined}
        onClick={(event) => {
          if (disabled) event.preventDefault();
        }}
        onKeyDown={(event) => {
          if (disabled && (event.key === "Enter" || event.key === " ")) event.preventDefault();
        }}
      >
        <EventTacticalIcon
          name={eventSlotRoleSymbolIconName(selectedSymbol, roleTitle)}
          className="event-role-symbol-picker__current-icon"
        />
        <span>
          <small>{t("React_EventRoleSymbol_Label")}</small>
          <strong>{selectedLabel}</strong>
        </span>
      </summary>

      <div className="event-role-symbol-picker__palette">
        <p>{t("React_EventRoleSymbol_Hint")}</p>
        <fieldset disabled={disabled}>
          <legend className="sr-only">{t("React_EventRoleSymbol_Label")}</legend>
          <div>
            {eventSlotRoleSymbols.map((symbol, index) => {
              const label = t(roleSymbolLabelKeys[symbol]);
              const optionId = `${radioGroupName}-option-${index}`;
              return (
                <label
                  className={symbol === selectedSymbol ? "is-selected" : undefined}
                  data-role-symbol={symbol}
                  htmlFor={optionId}
                  key={symbol}
                >
                  <input
                    className="sr-only"
                    id={optionId}
                    name={radioGroupName}
                    type="radio"
                    value={symbol}
                    checked={symbol === selectedSymbol}
                    onChange={() => {
                      onChange(symbol);
                      closeAndFocusSummary();
                    }}
                  />
                  <EventTacticalIcon name={eventSlotRoleSymbolIconName(symbol, roleTitle)} />
                  <span>{label}</span>
                </label>
              );
            })}
          </div>
        </fieldset>
      </div>
    </details>
  );
}
