import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { I18nextProvider } from "react-i18next";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { EventRoleSymbolPicker } from "@/features/events/EventRoleSymbolPicker";
import {
  eventSlotRoleSymbolIconName,
  eventSlotRoleSymbols,
  normalizeEventSlotRoleSymbol,
  type EventSlotRoleSymbol,
} from "@/features/events/EventTacticalIcon";

describe("event role symbol picker", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("resolves explicit, automatic, and unknown symbols safely", () => {
    expect(eventSlotRoleSymbols).toHaveLength(14);
    expect(eventSlotRoleSymbolIconName("Automatic", "Combat Medic")).toBe("medical");
    expect(eventSlotRoleSymbolIconName("Crew", "Pilot")).toBe("crew");
    expect(eventSlotRoleSymbolIconName("not-yet-supported", "Pilot")).toBe("pilot");
    expect(normalizeEventSlotRoleSymbol(null)).toBe("Automatic");
    expect(normalizeEventSlotRoleSymbol("not-yet-supported")).toBe("Automatic");
  });

  it("shows localized visual choices and restores summary focus after selection", async () => {
    const user = userEvent.setup();
    const changed = vi.fn();

    function Harness() {
      const [value, setValue] = useState<EventSlotRoleSymbol>("Automatic");
      return (
        <I18nextProvider i18n={i18n}>
          <EventRoleSymbolPicker
            value={value}
            roleTitle="Combat Medic"
            onChange={(next) => {
              setValue(next);
              changed(next);
            }}
          />
        </I18nextProvider>
      );
    }

    const { container } = render(<Harness />);
    const details = container.querySelector("details")!;
    const summary = details.querySelector("summary")!;

    expect(summary).toHaveTextContent("Automatic");
    expect(summary.querySelector('[data-tactical-icon="medical"]')).toBeInTheDocument();

    await user.click(summary);
    const group = screen.getByRole("group", { name: "Role symbol" });
    expect(within(group).getAllByRole("radio")).toHaveLength(14);
    expect(within(group).getByRole("radio", { name: "Automatic" })).toBeChecked();
    expect(within(group).getByText("Team / squad lead")).toBeVisible();
    expect(group.querySelectorAll("[data-tactical-icon]")).toHaveLength(14);

    await user.click(within(group).getByText("Pilot"));

    expect(changed).toHaveBeenCalledWith("Pilot");
    expect(details).not.toHaveAttribute("open");
    expect(summary).toHaveFocus();
    expect(summary).toHaveTextContent("Pilot");
    expect(summary.querySelector('[data-tactical-icon="pilot"]')).toBeInTheDocument();
  });

  it("closes on Escape and renders German role coverage", async () => {
    await i18n.changeLanguage("de");
    const user = userEvent.setup();
    const { container } = render(
      <I18nextProvider i18n={i18n}>
        <EventRoleSymbolPicker value="Leader" roleTitle="Medic" onChange={vi.fn()} />
      </I18nextProvider>,
    );
    const details = container.querySelector("details")!;
    const summary = details.querySelector("summary")!;

    expect(summary).toHaveTextContent("Trupp- / Gruppenführung");
    expect(summary.querySelector('[data-tactical-icon="host"]')).toBeInTheDocument();
    await user.click(summary);
    expect(screen.getByText("Pionier / EOD")).toBeVisible();
    screen.getByRole("radio", { name: "Trupp- / Gruppenführung" }).focus();

    await user.keyboard("{Escape}");

    expect(details).not.toHaveAttribute("open");
    expect(summary).toHaveFocus();
  });
});
