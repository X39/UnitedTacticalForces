import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import {
  CheckboxField,
  RadioGroupField,
  SelectField,
  SwitchField,
  TextAreaField,
  TextField,
} from "@/components/FormFields";
import { readProjectFile } from "./helpers/cssTokens";

interface HarnessValues {
  title: string;
  preset: string;
}

/** Binds a TextField and a SelectField to react-hook-form and exposes the form to the test. */
function Harness({
  onForm,
  titleError,
}: {
  onForm: (form: ReturnType<typeof useForm<HarnessValues>>) => void;
  titleError?: string;
}) {
  const form = useForm<HarnessValues>({ defaultValues: { title: "", preset: "small" } });
  useEffect(() => {
    onForm(form);
  });
  return (
    <form>
      <TextField
        label="Server name"
        hint="Shown in the fleet"
        error={titleError}
        {...form.register("title")}
      />
      <SelectField
        label="Preset"
        options={[
          { value: "small", label: "Small" },
          { value: "off", label: "Unavailable", disabled: true },
          {
            group: "Large presets",
            options: [
              { value: "large", label: "Large" },
              { value: "huge", label: "Huge" },
            ],
          },
        ]}
        {...form.register("preset")}
      />
    </form>
  );
}

describe("TextField", () => {
  it("is labelled, linked to its hint and not invalid without an error", () => {
    render(<TextField label="Server name" hint="Shown in the fleet" />);

    const input = screen.getByLabelText("Server name");
    expect(input.tagName).toBe("INPUT");
    const hint = screen.getByText("Shown in the fleet");
    expect(input).toHaveAttribute("aria-describedby", hint.id);
    expect(input).not.toHaveAttribute("aria-invalid", "true");
    expect(input).not.toBeRequired();
  });

  it("marks the input invalid and announces the error after the hint", () => {
    render(
      <TextField label="Server name" hint="Shown in the fleet" error="Enter a server name." />,
    );

    const input = screen.getByLabelText("Server name");
    expect(input).toHaveAttribute("aria-invalid", "true");
    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Enter a server name.");
    const hint = screen.getByText("Shown in the fleet");
    expect(input).toHaveAttribute("aria-describedby", `${hint.id} ${alert.id}`);
  });

  it("sets the native required attribute and hides the asterisk from assistive technology", () => {
    const { container } = render(<TextField label="Server name" required />);

    expect(screen.getByLabelText(/Server name/)).toBeRequired();
    const marker = container.querySelector(".form-field__required");
    expect(marker).toHaveAttribute("aria-hidden", "true");
  });

  it("merges a caller aria-describedby and honours a passed id", () => {
    render(<TextField label="Server name" id="custom" aria-describedby="extra" error="Bad." />);

    const input = screen.getByLabelText("Server name");
    expect(input).toHaveAttribute("id", "custom");
    expect(input).toHaveAttribute("aria-describedby", "custom-error extra");
  });

  it("uses the form-field wrapper and never the global field class", () => {
    const { container } = render(<TextField label="Server name" />);

    expect(container.querySelector(".field")).toBeNull();
    expect(container.firstElementChild).toHaveClass("form-field");
  });

  it("binds to react-hook-form and takes programmatic focus", async () => {
    const user = userEvent.setup();
    let form: ReturnType<typeof useForm<HarnessValues>> | undefined;
    render(<Harness onForm={(value) => (form = value)} />);

    const input = screen.getByLabelText("Server name");
    await user.type(input, "Alpha");
    expect(form?.getValues("title")).toBe("Alpha");

    await user.tab({ shift: true });
    expect(input).not.toHaveFocus();
    form?.setFocus("title");
    await waitFor(() => expect(input).toHaveFocus());
  });
});

describe("TextField trailing", () => {
  it("renders the trailing node in one row with the control, between the hint and the error", () => {
    render(
      <TextField
        label="Amount"
        hint="In euros"
        error="Enter an amount."
        trailing={<button type="button">Remove</button>}
      />,
    );

    const input = screen.getByLabelText("Amount");
    const row = input.parentElement as HTMLElement;
    expect(row).toHaveClass("form-field__row");
    expect(row).toContainElement(screen.getByRole("button", { name: "Remove" }));
    const field = row.parentElement as HTMLElement;
    expect(Array.from(field.children).map((child) => child.tagName)).toEqual([
      "LABEL",
      "P",
      "DIV",
      "P",
    ]);
    expect(field.children[1]).toHaveTextContent("In euros");
    expect(field.children[3]).toHaveTextContent("Enter an amount.");
    expect(input).toHaveAttribute("aria-invalid", "true");
  });

  it("renders without a row wrapper when no trailing node is given", () => {
    render(<TextField label="Server name" />);

    expect(screen.getByLabelText("Server name").parentElement).toHaveClass("form-field");
    expect(document.querySelector(".form-field__row")).toBeNull();
  });
});

describe("TextAreaField", () => {
  it("is a labelled textarea linked to its hint and not invalid without an error", () => {
    render(<TextAreaField label="Start parameters" hint="One per line" />);

    const area = screen.getByLabelText("Start parameters");
    expect(area.tagName).toBe("TEXTAREA");
    expect(area).toHaveClass("form-field__control", "form-field__control--multiline");
    expect(area).toHaveAttribute("aria-describedby", screen.getByText("One per line").id);
    expect(area).not.toHaveAttribute("aria-invalid");
    expect(area).not.toBeRequired();
  });

  it("marks the textarea invalid and announces the error after the hint", () => {
    render(<TextAreaField label="Start parameters" hint="One per line" error="Too long." />);

    const area = screen.getByLabelText("Start parameters");
    expect(area).toHaveAttribute("aria-invalid", "true");
    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Too long.");
    expect(area).toHaveAttribute(
      "aria-describedby",
      `${screen.getByText("One per line").id} ${alert.id}`,
    );
  });

  it("sets required, hides the asterisk and forwards rows, value and onChange", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { container } = render(
      <TextAreaField label="Start parameters" required rows={4} value="a" onChange={onChange} />,
    );

    const area = screen.getByLabelText(/Start parameters/);
    expect(area).toBeRequired();
    expect(area).toHaveAttribute("rows", "4");
    expect(area).toHaveValue("a");
    expect(container.querySelector(".form-field__required")).toHaveAttribute("aria-hidden", "true");
    await user.type(area, "b");
    expect(onChange).toHaveBeenCalled();
  });
});

describe("Phase 65 layout tokens", () => {
  it("defines the change bar reserve and the review dialog width", () => {
    const tokens = readProjectFile("src/styles/tokens.css");

    expect(tokens).toMatch(/--utf-change-bar-reserve:\s*12rem;/);
    expect(tokens).toMatch(/--utf-review-dialog-width:\s*44rem;/);
  });
});

describe("Phase 66 layout tokens", () => {
  it("defines the alert amount width and the reserved save status height", () => {
    const tokens = readProjectFile("src/styles/tokens.css");

    expect(tokens).toMatch(/--utf-alert-amount-width:\s*16rem;/);
    expect(tokens).toMatch(/--utf-alerts-status-min-height:\s*3rem;/);
  });
});

describe("SelectField", () => {
  it("renders plain options, a labelled optgroup and a disabled option", () => {
    const { container } = render(<Harness onForm={() => undefined} />);

    expect(screen.getByRole("option", { name: "Small" })).toBeEnabled();
    expect(screen.getByRole("option", { name: "Unavailable" })).toBeDisabled();
    const group = container.querySelector("optgroup");
    expect(group).toHaveAttribute("label", "Large presets");
    expect(group?.querySelectorAll("option")).toHaveLength(2);
  });

  it("binds to react-hook-form and updates the value on selection", async () => {
    const user = userEvent.setup();
    let form: ReturnType<typeof useForm<HarnessValues>> | undefined;
    render(<Harness onForm={(value) => (form = value)} />);

    await user.selectOptions(screen.getByLabelText("Preset"), "huge");
    expect(form?.getValues("preset")).toBe("huge");
  });

  it("wires the label, hint and error like the text field", () => {
    render(
      <SelectField
        label="Size"
        hint="Pick a size"
        error="Choose a size."
        required
        options={[{ value: "a", label: "A", lang: "de" }]}
      />,
    );

    const select = screen.getByLabelText(/Size/);
    expect(select.tagName).toBe("SELECT");
    expect(select).toBeRequired();
    expect(select).toHaveAttribute("aria-invalid", "true");
    const hint = screen.getByText("Pick a size");
    const alert = screen.getByRole("alert");
    expect(select).toHaveAttribute("aria-describedby", `${hint.id} ${alert.id}`);
    expect(screen.getByRole("option", { name: "A" })).toHaveAttribute("lang", "de");
  });

  it("uses the form-field wrapper and never the global field class", () => {
    const { container } = render(<SelectField label="Size" options={[]} />);

    expect(container.querySelector(".field")).toBeNull();
    expect(container.firstElementChild).toHaveClass("form-field");
  });

  it("renders the select control class and one decorative chevron next to the select", () => {
    render(<SelectField label="Size" options={[{ value: "a", label: "A" }]} />);

    const select = screen.getByLabelText("Size");
    expect(select).toHaveClass("form-field__control", "form-field__control--select");
    const wrapper = select.parentElement as HTMLElement;
    expect(wrapper).toHaveClass("form-field__select");
    const decoration = Array.from(wrapper.children).filter((child) => child !== select);
    expect(decoration).toHaveLength(1);
    expect(decoration[0]?.tagName.toLowerCase()).toBe("svg");
    expect(decoration[0]).toHaveAttribute("aria-hidden", "true");
  });

  it("removes the browser arrow and pads the end by the extra large space token", () => {
    render(<SelectField label="Size" options={[{ value: "a", label: "A" }]} />);

    const style = getComputedStyle(screen.getByLabelText("Size"));
    expect(style.getPropertyValue("appearance") || style.appearance).toBe("none");
    expect(style.getPropertyValue("padding-inline-end")).toBe("var(--utf-space-xl)");
    expect(style.getPropertyValue("min-height")).toBe("var(--utf-control-height)");
  });

  it("keeps the padded chevron rules in the shared stylesheet and draws no raw colour", () => {
    const css = readProjectFile("src/components/server/form-fields.css");

    expect(css).toMatch(/\.form-field__control--select\s*\{[^}]*appearance:\s*none;/);
    expect(css).toMatch(
      /\.form-field__control--select\s*\{[^}]*padding-inline-end:\s*var\(--utf-space-xl\);/,
    );
    expect(css).toMatch(/\.form-field__select-chevron\s*\{[^}]*pointer-events:\s*none;/);
  });
});

describe("labelEnd slot", () => {
  it("renders the label and the node in one head row for every field component", () => {
    const end = <span data-testid="state">Changed</span>;
    const { container } = render(
      <>
        <TextField label="Name" labelEnd={end} />
        <TextAreaField label="Notes" labelEnd={end} />
        <SelectField label="Size" options={[{ value: "a", label: "A" }]} labelEnd={end} />
        <SwitchField label="Idle" onLabel="On" offLabel="Off" labelEnd={end} />
      </>,
    );

    const heads = container.querySelectorAll(".form-field__head");
    expect(heads).toHaveLength(4);
    for (const head of heads) {
      expect(head.querySelector("label")).not.toBeNull();
      expect(head.querySelector("[data-testid=state]")).toHaveTextContent("Changed");
    }
    expect(screen.getByLabelText("Name")).toBeInTheDocument();
    expect(screen.getByLabelText("Notes")).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: "Idle" })).toBeInTheDocument();
  });

  it("leaves the markup unchanged when no node is given", () => {
    const { container } = render(
      <>
        <TextField label="Name" />
        <TextAreaField label="Notes" />
        <SelectField label="Size" options={[]} />
        <SwitchField label="Idle" onLabel="On" offLabel="Off" />
      </>,
    );

    expect(container.querySelector(".form-field__head")).toBeNull();
    expect(screen.getByLabelText("Name").previousElementSibling).toHaveClass("form-field__label");
  });
});

describe("select surface stylesheet", () => {
  it("keeps appearance none on the base rule and adds a base-select popup behind @supports", () => {
    const css = readProjectFile("src/components/server/form-fields.css");

    expect(css).toMatch(
      /\.form-field__control--select\s*\{[^}]*appearance:\s*none;[^}]*background-color:\s*var\(--utf-surface-raised\);/,
    );
    expect(css).toMatch(/@supports \(appearance: base-select\)/);
    expect(css).toMatch(
      /\.form-field__control--select,\s*\.form-field__control--select::picker\(select\)\s*\{[^}]*appearance:\s*base-select;/,
    );
    expect(css).toMatch(/option::checkmark/);
    expect(css).toMatch(/color-scheme:\s*dark;/);
  });
});

describe("required fields", () => {
  it("expose aria-required on the text input, textarea and select and keep the label marker", () => {
    const { container } = render(
      <>
        <TextField label="Server name" required />
        <TextAreaField label="Start parameters" required />
        <SelectField label="Size" required options={[{ value: "a", label: "A" }]} />
      </>,
    );

    for (const name of [/Server name/, /Start parameters/, /Size/]) {
      expect(screen.getByLabelText(name)).toHaveAttribute("aria-required", "true");
    }
    expect(container.querySelectorAll(".form-field__required")).toHaveLength(3);
  });

  it("does not set aria-required on optional fields and lets a caller value win", () => {
    render(
      <>
        <TextField label="Optional" />
        <TextField label="Explicit" required aria-required="false" />
      </>,
    );

    expect(screen.getByLabelText("Optional")).not.toHaveAttribute("aria-required");
    expect(screen.getByLabelText(/Explicit/)).toHaveAttribute("aria-required", "false");
  });

  it("shows a non-blank accessible name for a required select with an empty value", () => {
    render(
      <SelectField
        label="Map"
        required
        defaultValue=""
        options={[
          { value: "", label: "Not chosen yet" },
          { value: "a", label: "A" },
        ]}
      />,
    );

    const select = screen.getByLabelText(/Map/);
    expect(select).toHaveValue("");
    expect(screen.getByRole("option", { name: "Not chosen yet" })).toBeInTheDocument();
    expect(select).toHaveAttribute("aria-required", "true");
  });
});

describe("CheckboxField", () => {
  it("wraps the input in its label so the label text toggles it, and links the hint", async () => {
    const user = userEvent.setup();
    render(<CheckboxField label="Start after creation" hint="Starting incurs cost" />);

    const box = screen.getByRole("checkbox", { name: "Start after creation" });
    const hint = screen.getByText("Starting incurs cost");
    expect(box).toHaveAttribute("aria-describedby", hint.id);
    expect(box.closest("label")).toHaveTextContent("Start after creation");

    await user.click(screen.getByText("Start after creation"));
    expect(box).toBeChecked();
  });

  it("reports the checked state to react-hook-form", async () => {
    const user = userEvent.setup();
    let form: ReturnType<typeof useForm<{ start: boolean }>> | undefined;
    function CheckHarness() {
      const value = useForm<{ start: boolean }>({ defaultValues: { start: false } });
      useEffect(() => {
        form = value;
      });
      return <CheckboxField label="Start" {...value.register("start")} />;
    }
    render(<CheckHarness />);

    await user.click(screen.getByRole("checkbox", { name: "Start" }));
    expect(form?.getValues("start")).toBe(true);
  });

  it("announces an error and marks the checkbox invalid", () => {
    render(<CheckboxField label="Accept" error="Accept the terms." />);

    expect(screen.getByRole("checkbox", { name: "Accept" })).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Accept the terms.");
  });

  it("draws an aria-hidden box next to one native input and never hides an ancestor of the input", () => {
    const { container } = render(<CheckboxField label="Start" />);

    expect(container.querySelectorAll("input")).toHaveLength(1);
    const input = screen.getByRole("checkbox", { name: "Start" });
    expect(input).toHaveClass("check-mark__input");
    const box = input.nextElementSibling as HTMLElement;
    expect(box).toHaveClass("check-mark__box");
    expect(box).toHaveAttribute("aria-hidden", "true");
    expect(box.querySelector("svg")).not.toBeNull();
    expect(input.closest("[aria-hidden]")).toBeNull();
    expect(input.closest("label")).toHaveClass("form-field__check");
  });

  it("forwards ref, onChange, checked and disabled to the native input", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const ref = { current: null as HTMLInputElement | null };
    render(
      <>
        <CheckboxField label="On" ref={ref} onChange={onChange} />
        <CheckboxField label="Locked" disabled defaultChecked />
      </>,
    );

    expect(ref.current).toBe(screen.getByRole("checkbox", { name: "On" }));
    await user.click(screen.getByText("On"));
    expect(onChange).toHaveBeenCalledTimes(1);
    const locked = screen.getByRole("checkbox", { name: "Locked" });
    expect(locked).toBeDisabled();
    expect(locked).toBeChecked();
  });
});

describe("CheckboxField chip variant", () => {
  it("draws a toggle chip over the native checkbox with no visible box", async () => {
    const user = userEvent.setup();
    const { container } = render(<CheckboxField label="Mon" variant="chip" />);

    const input = screen.getByRole("checkbox", { name: "Mon" });
    const chip = input.closest("label") as HTMLElement;
    expect(chip).toHaveClass("toggle-chip");
    expect(input).toHaveClass("check-mark__input");
    expect(container.querySelector(".check-mark__box")).toBeNull();
    expect(container.querySelector(".form-field__check")).toBeNull();
    expect(chip).toHaveTextContent("Mon");
    await user.click(screen.getByText("Mon"));
    expect(input).toBeChecked();
  });

  it("keeps the native disabled attribute so a locked chip cannot be toggled", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<CheckboxField label="Tue" variant="chip" disabled onChange={onChange} />);

    const input = screen.getByRole("checkbox", { name: "Tue" });
    expect(input).toBeDisabled();
    await user.click(screen.getByText("Tue"));
    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getByText("Tue")).toBeVisible();
  });
});

describe("SwitchField", () => {
  it("is a switch named by its label that toggles with Space and reports checked natively", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SwitchField
        label="Idle shutdown"
        onLabel="Enabled"
        offLabel="Disabled"
        onChange={onChange}
      />,
    );

    const toggle = screen.getByRole("switch", { name: "Idle shutdown" });
    expect(toggle.tagName).toBe("INPUT");
    expect(toggle).toHaveAttribute("type", "checkbox");
    expect(toggle).not.toHaveAttribute("aria-checked");
    expect(toggle).not.toBeChecked();
    await user.tab();
    expect(toggle).toHaveFocus();
    await user.keyboard(" ");
    expect(toggle).toBeChecked();
    expect(onChange).toHaveBeenCalledTimes(1);
    await user.keyboard("{Enter}");
    expect(toggle).toBeChecked();
  });

  it("renders both state words aria-hidden and keeps them out of the accessible name", () => {
    const { container } = render(
      <SwitchField label="Idle shutdown" onLabel="Enabled" offLabel="Disabled" />,
    );

    const on = container.querySelector(".form-field__switch-state--on");
    const off = container.querySelector(".form-field__switch-state--off");
    expect(on).toHaveTextContent("Enabled");
    expect(on).toHaveAttribute("aria-hidden", "true");
    expect(off).toHaveTextContent("Disabled");
    expect(off).toHaveAttribute("aria-hidden", "true");
    const toggle = screen.getByRole("switch", { name: "Idle shutdown" });
    expect(toggle.closest("[aria-hidden]")).toBeNull();
    expect(container.querySelector(".switch-track")).toHaveAttribute("aria-hidden", "true");
    expect(toggle.nextElementSibling).toHaveClass("switch-track");
  });

  it("stays visible and not operable when disabled, with its state kept", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SwitchField
        label="Idle shutdown"
        onLabel="Enabled"
        offLabel="Disabled"
        disabled
        defaultChecked
        onChange={onChange}
      />,
    );

    const toggle = screen.getByRole("switch", { name: "Idle shutdown" });
    expect(toggle).toBeDisabled();
    expect(toggle).toBeChecked();
    await user.click(screen.getByText("Idle shutdown"));
    expect(onChange).not.toHaveBeenCalled();
    expect(toggle).toBeChecked();
    expect(screen.getByText("Idle shutdown")).toBeVisible();
  });

  it("links the hint and the error and forwards ref and name", () => {
    const ref = { current: null as HTMLInputElement | null };
    render(
      <SwitchField
        label="Idle shutdown"
        onLabel="Enabled"
        offLabel="Disabled"
        hint="Stops an idle server"
        error="Choose a value."
        name="idle"
        ref={ref}
      />,
    );

    const toggle = screen.getByRole("switch", { name: "Idle shutdown" });
    expect(ref.current).toBe(toggle);
    expect(toggle).toHaveAttribute("name", "idle");
    expect(toggle).toHaveAttribute("aria-invalid", "true");
    expect(toggle).toHaveAttribute(
      "aria-describedby",
      `${screen.getByText("Stops an idle server").id} ${screen.getByRole("alert").id}`,
    );
  });

  it("requires both state words at compile time", () => {
    // @ts-expect-error onLabel and offLabel are required.
    const missing = <SwitchField label="Idle shutdown" />;
    expect(missing).toBeTruthy();
  });
});

describe("RadioGroupField layouts", () => {
  it("renders choice cards by default with two or three options in equal columns", () => {
    const { container } = render(
      <RadioGroupField
        legend="Next step"
        name="next"
        value="a"
        options={[
          { value: "a", label: "A", help: "First" },
          { value: "b", label: "B", help: "Second" },
        ]}
        onChange={() => undefined}
      />,
    );

    expect(container.querySelector("fieldset")).toHaveAttribute("data-layout", "cards");
    expect(container.querySelectorAll("label.choice-card")).toHaveLength(2);
    expect(container.querySelector(".form-field__options")).toHaveAttribute("data-columns", "2");
    expect(screen.getByRole("radio", { name: /A/ })).toHaveAttribute(
      "aria-describedby",
      screen.getByText("First").id,
    );
  });

  it("stacks four or more cards and keeps native Arrow key selection", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { container } = render(
      <RadioGroupField
        legend="Size"
        name="size"
        value="a"
        options={["a", "b", "c", "d"].map((value) => ({ value, label: value.toUpperCase() }))}
        onChange={onChange}
      />,
    );

    expect(container.querySelector(".form-field__options")).not.toHaveAttribute("data-columns");
    await user.tab();
    expect(screen.getByRole("radio", { name: "A" })).toHaveFocus();
    await user.keyboard("{ArrowDown}");
    expect(onChange).toHaveBeenCalledWith("b");
  });

  it("renders a segmented bar with one segment per option and no help text", () => {
    const { container } = render(
      <RadioGroupField
        legend="Subject"
        name="subject"
        layout="segmented"
        value="user"
        options={[
          { value: "user", label: "User", help: "Never shown" },
          { value: "role", label: "Role" },
        ]}
        onChange={() => undefined}
      />,
    );

    expect(container.querySelector("fieldset")).toHaveAttribute("data-layout", "segmented");
    expect(container.querySelector("fieldset")?.tagName).toBe("FIELDSET");
    const bar = container.querySelector("div.segmented") as HTMLElement;
    expect(bar.querySelectorAll("label.segmented__option")).toHaveLength(2);
    expect(container.querySelector(".choice-card")).toBeNull();
    expect(screen.queryByText("Never shown")).toBeNull();
    expect(screen.getByRole("radio", { name: "User" })).toBeChecked();
    expect(screen.getByRole("group", { name: "Subject" })).toBeInTheDocument();
  });

  it("keeps a hidden legend in the accessible tree with the sr-only class", () => {
    render(
      <RadioGroupField
        legend="Subject"
        name="subject"
        layout="segmented"
        hideLegend
        value="user"
        options={[
          { value: "user", label: "User" },
          { value: "role", label: "Role" },
        ]}
        onChange={() => undefined}
      />,
    );

    expect(screen.getByRole("group", { name: "Subject" })).toBeInTheDocument();
    expect(screen.getByText("Subject")).toHaveClass("form-field__legend", "sr-only");
  });

  it("keeps the native disabled attribute on a drawn card", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <RadioGroupField
        legend="Next step"
        name="next"
        value="a"
        options={[
          { value: "a", label: "A" },
          { value: "b", label: "B", disabled: true },
        ]}
        onChange={onChange}
      />,
    );

    expect(screen.getByRole("radio", { name: "B" })).toBeDisabled();
    await user.click(screen.getByText("B"));
    expect(onChange).not.toHaveBeenCalled();
  });
});

describe("drawn marks stylesheet", () => {
  it("defines the card, segment, chip and switch shapes with a forced colors fallback", () => {
    const css = readProjectFile("src/components/server/form-fields.css");

    for (const selector of [".choice-card", ".segmented", ".toggle-chip", ".switch-track"]) {
      expect(css, selector).toContain(selector);
    }
    expect(css).toMatch(/@media \(forced-colors: active\)/);
    expect(css).toMatch(/border-radius:\s*calc\(var\(--utf-switch-track-height\) \/ 2\)/);
    expect(css).not.toMatch(/color-mix\(/);
  });

  it("draws no browser mark: accent-color is gone and the marks are project drawn", () => {
    const css = readProjectFile("src/components/server/form-fields.css");

    expect(css).not.toMatch(/accent-color/);
    expect(css).toMatch(/\.check-mark__input\s*\{[^}]*opacity:\s*0;/);
    expect(css).toMatch(/\.check-mark__box\b/);
  });
});

/** A stateful radio group used to observe onChange. */
function RadioHarness({
  onChange,
  disabled,
}: {
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const [value, setValue] = useState("small");
  return (
    <RadioGroupField
      legend={<h3>Size</h3>}
      name="size"
      value={value}
      disabled={disabled}
      reason="Large sizes are unavailable in this region."
      options={[
        { value: "small", label: "Small", help: "Two cores" },
        { value: "medium", label: "Medium" },
        { value: "large", label: "Large", disabled: true },
      ]}
      onChange={(next) => {
        onChange(next);
        setValue(next);
      }}
    />
  );
}

describe("RadioGroupField", () => {
  it("renders a fieldset with the legend node and one labelled radio per option", () => {
    render(<RadioHarness onChange={() => undefined} />);

    const group = screen.getByRole("group", { name: "Size" });
    expect(group.tagName).toBe("FIELDSET");
    expect(screen.getByRole("heading", { name: "Size" })).toBeInTheDocument();
    expect(screen.getAllByRole("radio")).toHaveLength(3);
    const small = screen.getByRole("radio", { name: /Small/ });
    expect(small).toBeChecked();
    expect(small).toHaveAttribute("aria-describedby", screen.getByText("Two cores").id);
  });

  it("reports the chosen value of an enabled option", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RadioHarness onChange={onChange} />);

    await user.click(screen.getByRole("radio", { name: "Medium" }));
    expect(onChange).toHaveBeenCalledWith("medium");
    expect(screen.getByRole("radio", { name: "Medium" })).toBeChecked();
  });

  it("keeps a disabled option visible with a linked reason and never reports it", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RadioHarness onChange={onChange} />);

    const large = screen.getByRole("radio", { name: "Large" });
    expect(large).toBeDisabled();
    const reason = screen.getByText("Large sizes are unavailable in this region.");
    expect(reason).toBeVisible();
    expect(large).toHaveAttribute("aria-describedby", reason.id);
    await user.click(large);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("draws each radio as an aria-hidden circle that is a sibling of the native input", () => {
    const { container } = render(<RadioHarness onChange={() => undefined} />);

    expect(container.querySelectorAll("input[type=radio]")).toHaveLength(3);
    for (const radio of screen.getAllByRole("radio")) {
      expect(radio).toHaveClass("check-mark__input");
      const box = radio.nextElementSibling as HTMLElement;
      expect(box).toHaveClass("check-mark__box");
      expect(box).toHaveAttribute("aria-hidden", "true");
      expect(radio.parentElement).toHaveClass("check-mark", "check-mark--radio");
      expect(radio.closest("[aria-hidden]")).toBeNull();
    }
  });

  it("disables every radio when the group is disabled", () => {
    render(<RadioHarness onChange={() => undefined} disabled />);

    for (const radio of screen.getAllByRole("radio")) expect(radio).toBeDisabled();
  });
});
