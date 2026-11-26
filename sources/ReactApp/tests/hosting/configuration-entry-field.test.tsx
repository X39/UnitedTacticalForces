import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ConfigurationEntryField } from "@/components/server/ConfigurationEntryField";
import type { ConfigurationContributionProps } from "@/features/hosting/implementations/ConfigurationContribution";
import type { GameConfigurationEntry } from "@/features/hosting/types";
import { configurationEntry } from "../helpers/configurationFixtures";

/* The configuration entry field: the label-row state slot reserved by capability (D-11) and the boolean switch (D-02). */

const noop = () => undefined;
const slotSelector = ".configuration-field__state";
const actionableSelector = ".configuration-field__state--actionable";

/** Renders the field with neutral callbacks; the reset callback is given unless the props override it. */
function field(
  entry: GameConfigurationEntry,
  props: Partial<Parameters<typeof ConfigurationEntryField>[0]> = {},
) {
  return render(
    <ConfigurationEntryField
      entry={entry}
      value=""
      staged={false}
      resetStaged={false}
      disabled={false}
      onChange={noop}
      onBlur={noop}
      onUndo={noop}
      onReset={noop}
      {...props}
    />,
  );
}

beforeAll(async () => {
  await i18nReady;
});

beforeEach(async () => {
  await i18n.changeLanguage("en");
});

describe("ConfigurationEntryField state slot", () => {
  const entry = configurationEntry({ key: "hostname", displayName: "Hostname", source: "Default" });

  it("renders the actionable slot in the label row whether or not the entry is staged", () => {
    const view = field(entry);
    const unstaged = view.container.querySelector(`.form-field__head > ${actionableSelector}`);
    expect(unstaged).not.toBeNull();
    expect(unstaged?.textContent).toBe("");
    view.unmount();

    const staged = field(entry, { staged: true, value: "x" });
    expect(staged.container.querySelectorAll(actionableSelector)).toHaveLength(1);
    expect(
      staged.container.querySelector(`.form-field__head > ${actionableSelector}`),
    ).not.toBeNull();
  });

  it("keeps the slot and its label row element tree stable while staging", () => {
    const view = field(entry);
    const slotBefore = view.container.querySelector(actionableSelector);
    const fieldChildrenBefore =
      view.container.querySelector(".configuration-field")?.children.length;
    view.rerender(
      <ConfigurationEntryField
        entry={entry}
        value="x"
        staged
        resetStaged={false}
        disabled={false}
        onChange={noop}
        onBlur={noop}
        onUndo={noop}
        onReset={noop}
      />,
    );
    expect(view.container.querySelector(actionableSelector)).toBe(slotBefore);
    expect(view.container.querySelector(".configuration-field")?.children.length).toBe(
      fieldChildrenBefore,
    );
  });

  it("does not make the slot depend on the disabled state", () => {
    const view = field(entry, { disabled: true });
    expect(view.container.querySelector(actionableSelector)).not.toBeNull();
  });

  it("shows the Changed badge and Undo inside the slot while staged", () => {
    const view = field(entry, { staged: true, value: "x" });
    const slot = view.container.querySelector(slotSelector) as HTMLElement;
    expect(slot).toHaveTextContent("Changed");
    expect(slot.contains(screen.getByRole("button", { name: "Undo change: Hostname" }))).toBe(true);
  });

  it("shows Reset for a Server-source entry while unstaged and the reset text plus Undo after Reset", () => {
    const serverEntry = { ...entry, source: "Server" };
    const view = field(serverEntry);
    const slot = view.container.querySelector(slotSelector) as HTMLElement;
    expect(
      slot.contains(screen.getByRole("button", { name: "Reset to inherited: Hostname" })),
    ).toBe(true);
    expect(screen.queryByRole("button", { name: "Undo change: Hostname" })).not.toBeInTheDocument();
    view.unmount();

    const reset = field(serverEntry, { resetStaged: true });
    const resetSlot = reset.container.querySelector(slotSelector) as HTMLElement;
    expect(resetSlot).toHaveTextContent("Will use the inherited value after you apply.");
    expect(resetSlot.contains(screen.getByRole("button", { name: "Undo change: Hostname" }))).toBe(
      true,
    );
    expect(
      screen.queryByRole("button", { name: "Reset to inherited: Hostname" }),
    ).not.toBeInTheDocument();
  });

  it("renders no actionable slot for a protected entry", () => {
    const view = field({ ...entry, sensitive: true });
    expect(view.container.querySelector(slotSelector)).toBeNull();
  });

  it("renders no actionable slot for a reader without a reset callback", () => {
    const view = field(entry, { onReset: undefined });
    expect(view.container.querySelector(actionableSelector)).toBeNull();
  });

  it("renders the same head row with the slot for a game contribution", () => {
    function Contribution({ id, value }: ConfigurationContributionProps) {
      return <input id={id} value={value} readOnly />;
    }
    const view = field(entry, { contribution: Contribution });
    expect(
      view.container.querySelector(`.form-field__head > ${actionableSelector}`),
    ).not.toBeNull();
    expect(view.container.querySelector(".form-field__head > label")).not.toBeNull();
  });

  it("calls Undo and Reset", async () => {
    const onUndo = vi.fn();
    const onReset = vi.fn();
    const view = field({ ...entry, source: "Server" }, { onUndo, onReset });
    screen.getByRole("button", { name: "Reset to inherited: Hostname" }).click();
    expect(onReset).toHaveBeenCalledTimes(1);
    view.unmount();
    field(entry, { onUndo, staged: true });
    screen.getByRole("button", { name: "Undo change: Hostname" }).click();
    expect(onUndo).toHaveBeenCalledTimes(1);
  });
});

describe("ConfigurationEntryField boolean switch", () => {
  const flag = configurationEntry({ key: "flag", displayName: "Flag", kind: "Boolean" });

  it("is checked for true and unchecked for false or an empty value", () => {
    const view = field(flag, { value: "true" });
    expect(screen.getByRole("switch", { name: "Flag" })).toBeChecked();
    view.unmount();

    const off = field(flag, { value: "false" });
    expect(screen.getByRole("switch", { name: "Flag" })).not.toBeChecked();
    off.unmount();

    field(flag, { value: "" });
    expect(screen.getByRole("switch", { name: "Flag" })).not.toBeChecked();
  });

  it("stages the strings true and false", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const view = field(flag, { value: "false", onChange });
    await user.click(screen.getByRole("switch", { name: "Flag" }));
    expect(onChange).toHaveBeenLastCalledWith("true");
    view.unmount();

    field(flag, { value: "true", onChange });
    await user.click(screen.getByRole("switch", { name: "Flag" }));
    expect(onChange).toHaveBeenLastCalledWith("false");
  });

  it("places the state slot in the switch label row", () => {
    const view = field(flag, { value: "true", staged: true });
    expect(
      view.container.querySelector(".form-field__head > .configuration-field__state--actionable"),
    ).not.toBeNull();
    expect(screen.getByRole("button", { name: "Undo change: Flag" })).toBeVisible();
  });

  it("keeps the disabled and hint wiring and is not marked required", () => {
    const required = { ...flag, required: true, description: "Turns it on." };
    field(required, { value: "true", disabled: true });
    const toggle = screen.getByRole("switch", { name: "Flag" });
    expect(toggle).toBeDisabled();
    expect(toggle).not.toHaveAttribute("aria-required");
    expect(toggle).toHaveAccessibleDescription(/Turns it on\./);
  });
});
