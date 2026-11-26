import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createRef, useState } from "react";
import { SearchField } from "@/components/SearchField";

const labels = {
  label: "Search servers",
  placeholder: "Name, game, or cost owner",
  clearLabel: "Clear search",
};

/** Renders a SearchField controlled by local state and records every reported value. */
function ControlledField({
  initial = "",
  onChange,
}: {
  initial?: string;
  onChange: (value: string) => void;
}) {
  const [value, setValue] = useState(initial);
  return (
    <SearchField
      {...labels}
      value={value}
      onChange={(next) => {
        onChange(next);
        setValue(next);
      }}
    />
  );
}

describe("SearchField", () => {
  it("renders a labelled search input that reports every keystroke and shows clear only for a value", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ControlledField onChange={onChange} />);

    const input = screen.getByRole("searchbox", { name: "Search servers" });
    expect(input).toHaveAttribute("type", "search");
    expect(input).toHaveAttribute("placeholder", "Name, game, or cost owner");
    expect(input).toHaveAttribute("autocomplete", "off");
    expect(input).toHaveAttribute("spellcheck", "false");
    expect(screen.queryByRole("button", { name: "Clear search" })).not.toBeInTheDocument();

    await user.type(input, "arma");

    expect(onChange.mock.calls.map(([value]) => value as string)).toEqual([
      "a",
      "ar",
      "arm",
      "arma",
    ]);
    expect(input).toHaveValue("arma");
    expect(screen.getByRole("button", { name: "Clear search" })).toBeVisible();
  });

  it("clears and refocuses the input from the clear control", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ControlledField initial="alpha" onChange={onChange} />);

    await user.click(screen.getByRole("button", { name: "Clear search" }));

    expect(onChange).toHaveBeenCalledWith("");
    const input = screen.getByRole("searchbox", { name: "Search servers" });
    expect(input).toHaveValue("");
    expect(input).toHaveFocus();
    expect(screen.queryByRole("button", { name: "Clear search" })).not.toBeInTheDocument();
  });

  it("clears on Escape only when the field has a value and leaves other keys alone", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ControlledField initial="alpha" onChange={onChange} />);
    const input = screen.getByRole("searchbox", { name: "Search servers" });
    await user.click(input);

    await user.keyboard("{ArrowLeft}{Home}{Enter}");
    expect(onChange).not.toHaveBeenCalled();
    expect(input).toHaveValue("alpha");

    await user.keyboard("{Escape}");
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenLastCalledWith("");
    expect(input).toHaveValue("");
    expect(input).toHaveFocus();

    await user.keyboard("{Escape}");
    expect(onChange).toHaveBeenCalledTimes(1);

    expect(screen.queryAllByRole("button")).toHaveLength(0);
    expect(document.querySelector("[type='submit'], form")).toBeNull();
  });

  it("passes the input element to a supplied ref", () => {
    const ref = createRef<HTMLInputElement>();
    render(<SearchField {...labels} value="" onChange={vi.fn()} inputRef={ref} />);

    expect(ref.current).toBe(screen.getByRole("searchbox", { name: "Search servers" }));
  });

  it("gives the icon-only clear control an accessible name without visible text", () => {
    render(<SearchField {...labels} value="alpha" onChange={vi.fn()} />);

    const clear = screen.getByRole("button", { name: "Clear search" });
    expect(clear).toHaveTextContent("");
    expect(clear).toHaveClass("button--icon-only", "button--ghost");
    expect(clear).toHaveAttribute("type", "button");
    expect(screen.getByRole("searchbox")).toHaveAccessibleName("Search servers");
  });
});
