import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { EventObjectiveIconPicker } from "@/features/admin/EventObjectiveIconPicker";
import type { EventMissionIconName } from "@/features/events/EventTacticalIcon";

describe("event objective icon picker", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  it("shows visual radio options and updates the selected icon", async () => {
    await i18n.changeLanguage("en");
    const user = userEvent.setup();
    const changed = vi.fn();
    function Harness() {
      const [value, setValue] = useState<EventMissionIconName>("map");
      return (
        <I18nextProvider i18n={i18n}>
          <EventObjectiveIconPicker
            label="Icon"
            value={value}
            options={["objective", "map", "radio"]}
            onChange={(next) => {
              setValue(next);
              changed(next);
            }}
          />
        </I18nextProvider>
      );
    }
    render(<Harness />);

    const group = screen.getByRole("group", { name: /Icon Map/ });
    const radios = screen.getAllByRole("radio");
    expect(radios).toHaveLength(3);
    expect(screen.getByRole("radio", { name: "Map" })).toBeChecked();
    expect(group.querySelectorAll("[data-tactical-icon]")).toHaveLength(3);
    expect(group.querySelector('[data-tactical-icon="map"] svg')).toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: "Radio" }));

    expect(changed).toHaveBeenCalledWith("radio");
    expect(screen.getByRole("radio", { name: "Radio" })).toBeChecked();
    expect(screen.getByRole("group", { name: /Icon Radio/ })).toBeVisible();
  });

  it("uses localized option names", async () => {
    await i18n.changeLanguage("de");
    render(
      <I18nextProvider i18n={i18n}>
        <EventObjectiveIconPicker
          label="Symbol"
          value="medical"
          options={["medical", "rally"]}
          onChange={vi.fn()}
        />
      </I18nextProvider>,
    );

    expect(screen.getByRole("radio", { name: "Sanität" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Sammelpunkt" })).toBeEnabled();
  });
});
