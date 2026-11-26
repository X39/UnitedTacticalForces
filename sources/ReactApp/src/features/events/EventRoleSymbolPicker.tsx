import { useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { Disclosure } from "@/components/Disclosure";
import {
  EventTacticalIcon,
  eventSlotRoleSymbolIconName,
  eventSlotRoleSymbols,
  normalizeEventSlotRoleSymbol,
  type EventSlotRoleSymbol,
} from "@/features/events/EventTacticalIcon";
import "@/features/admin/eventAuthoring.css";

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

/**
 * Picks the tactical role symbol of an event slot from a visual palette.
 *
 * The palette expands through the shared {@link Disclosure}, so the toggle is a button with `aria-expanded`. Choosing a
 * symbol, like pressing Escape inside the palette, closes it and returns focus to the toggle. The palette itself is a
 * radio group and stays raw because its grid of visual choices is editor-specific.
 *
 * @param props.value The current symbol; an unknown or missing value reads as Automatic.
 * @param props.roleTitle The slot role title, used to resolve the Automatic symbol icon.
 * @param props.disabled Blocks opening the palette and choosing a symbol.
 * @param props.onChange Called with the chosen symbol.
 */
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
  const rootRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const radioGroupName = useId();
  const selectedSymbol = normalizeEventSlotRoleSymbol(value);
  const selectedLabel = t(roleSymbolLabelKeys[selectedSymbol]);
  const closeAndFocusToggle = () => {
    setOpen(false);
    rootRef.current?.querySelector<HTMLElement>(".disclosure__summary")?.focus();
  };

  return (
    <div
      className="event-role-symbol-picker"
      ref={rootRef}
      data-disabled={disabled || undefined}
      onKeyDown={(event) => {
        if (event.key !== "Escape" || !open) return;
        event.preventDefault();
        closeAndFocusToggle();
      }}
    >
      <Disclosure
        title={
          <>
            <EventTacticalIcon
              name={eventSlotRoleSymbolIconName(selectedSymbol, roleTitle)}
              className="event-role-symbol-picker__current-icon"
            />
            <span>
              <small>{t("React_EventRoleSymbol_Label")}</small>
              <strong>{selectedLabel}</strong>
            </span>
          </>
        }
        open={open}
        onOpenChange={(next) => {
          if (!disabled) setOpen(next);
        }}
      >
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
                        closeAndFocusToggle();
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
      </Disclosure>
    </div>
  );
}
