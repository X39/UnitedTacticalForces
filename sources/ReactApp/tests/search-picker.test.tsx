import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import {
  SearchPicker,
  type PickerOption,
  type SearchPickerProps,
  type SearchPickerTexts,
} from "@/components/SearchPicker";

const texts: SearchPickerTexts = {
  loading: "Searching members",
  error: "Search failed",
  retry: "Retry search",
  noMatches: "No member matches",
  hintIdle: "Type to search",
  results: (count) => `${count} results`,
  clear: "Remove choice",
};

const alpha: PickerOption = { id: "alpha", label: "Alpha", detail: "alpha@example.test" };
const bravo: PickerOption = {
  id: "bravo",
  label: "Bravo",
  detail: "bravo@example.test",
  disabled: true,
  disabledReason: "Already has access",
};
const charlie: PickerOption = { id: "charlie", label: "Charlie" };
const delta: PickerOption = { id: "delta", label: "Delta", detail: "delta@example.test" };
const allOptions = [alpha, bravo, charlie, delta];

interface HarnessProps extends Partial<Omit<SearchPickerProps, "query" | "selected">> {
  /** Start text of the input. */
  initialQuery?: string;
  /** Start choice. */
  initialSelected?: PickerOption | null;
  /** When false the harness reports a choice but never shows the token, so focus stays in the input. */
  keepSelection?: boolean;
}

/** Renders a SearchPicker controlled by local state and forwards every callback to the optional spies. */
function Harness({
  initialQuery = "",
  initialSelected = null,
  keepSelection = true,
  onQueryChange,
  onSelect,
  onClear,
  ...rest
}: HarnessProps) {
  const [query, setQuery] = useState(initialQuery);
  const [selected, setSelected] = useState<PickerOption | null>(initialSelected);
  return (
    <SearchPicker
      label="Member"
      placeholder="Search members"
      options={allOptions}
      status="ready"
      texts={texts}
      onRetry={() => undefined}
      {...rest}
      query={query}
      selected={selected}
      onQueryChange={(value) => {
        onQueryChange?.(value);
        setQuery(value);
      }}
      onSelect={(option) => {
        onSelect?.(option);
        if (keepSelection) setSelected(option);
      }}
      onClear={() => {
        onClear?.();
        setSelected(null);
      }}
    />
  );
}

function input(): HTMLInputElement {
  return screen.getByRole("combobox", { name: "Member" });
}

describe("SearchPicker combobox contract", () => {
  it("exposes an ARIA 1.2 combobox with a list autocomplete that is collapsed until it opens", () => {
    render(<Harness />);

    const combobox = input();
    expect(combobox).toHaveAttribute("type", "text");
    expect(combobox).toHaveAttribute("aria-autocomplete", "list");
    expect(combobox).toHaveAttribute("aria-expanded", "false");
    expect(combobox).toHaveAttribute("aria-controls");
    expect(combobox).toHaveAttribute("autocomplete", "off");
    expect(combobox).toHaveAttribute("spellcheck", "false");
    expect(combobox).not.toHaveAttribute("aria-activedescendant");
    expect(combobox).toHaveAttribute("placeholder", "Search members");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("opens the listbox on typing and links it to the input", async () => {
    const user = userEvent.setup();
    const onQueryChange = vi.fn();
    render(<Harness onQueryChange={onQueryChange} />);

    await user.type(input(), "a");

    const listbox = screen.getByRole("listbox", { name: "Member" });
    expect(input()).toHaveAttribute("aria-expanded", "true");
    expect(input().getAttribute("aria-controls")).toBe(listbox.id);
    expect(onQueryChange).toHaveBeenCalledWith("a");
    expect(screen.getAllByRole("option")).toHaveLength(4);
  });

  it("opens on focus only when no minimum query length applies and calls onFocus", async () => {
    const user = userEvent.setup();
    const onFocus = vi.fn();
    const { unmount } = render(<Harness minQueryLength={0} onFocus={onFocus} />);

    await user.click(input());

    expect(onFocus).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("listbox")).toBeInTheDocument();
    unmount();

    render(<Harness />);
    await user.click(input());
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("opens with ArrowDown and activates the first enabled option", async () => {
    const user = userEvent.setup();
    render(<Harness initialQuery="a" />);
    await user.click(input());
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();

    await user.keyboard("{ArrowDown}");

    expect(screen.getByRole("listbox")).toBeInTheDocument();
    expect(screen.getByRole("option", { name: /Alpha/ })).toHaveAttribute("aria-selected", "true");
    expect(input()).toHaveFocus();
  });

  it("moves the active option with the arrow keys, wraps, skips disabled options and jumps with Home and End", async () => {
    const user = userEvent.setup();
    render(<Harness initialQuery="a" />);
    await user.click(input());
    const activeName = () =>
      screen
        .getAllByRole("option")
        .filter((option) => option.getAttribute("aria-selected") === "true")
        .map((option) => option.textContent);

    await user.keyboard("{ArrowDown}");
    expect(activeName()).toEqual(["Alpha alpha@example.test"]);
    expect(input().getAttribute("aria-activedescendant")).toBe(
      screen.getByRole("option", { name: /Alpha/ }).id,
    );

    await user.keyboard("{ArrowDown}");
    expect(activeName()).toEqual(["Charlie"]);
    await user.keyboard("{ArrowDown}");
    expect(activeName()).toEqual(["Delta delta@example.test"]);
    await user.keyboard("{ArrowDown}");
    expect(activeName()).toEqual(["Alpha alpha@example.test"]);
    await user.keyboard("{ArrowUp}");
    expect(activeName()).toEqual(["Delta delta@example.test"]);
    await user.keyboard("{Home}");
    expect(activeName()).toEqual(["Alpha alpha@example.test"]);
    await user.keyboard("{End}");
    expect(activeName()).toEqual(["Delta delta@example.test"]);

    const disabled = screen.getByRole("option", { name: /Bravo/ });
    expect(disabled).toHaveAttribute("aria-disabled", "true");
    expect(disabled).toHaveAttribute("aria-selected", "false");
    expect(disabled).toHaveTextContent("Already has access");
  });

  it("selects the active option with Enter and prevents the keydown default, otherwise leaves Enter alone", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness initialQuery="a" onSelect={onSelect} keepSelection={false} />);
    await user.click(input());

    expect(fireEvent.keyDown(input(), { key: "Enter" })).toBe(true);
    expect(onSelect).not.toHaveBeenCalled();

    await user.keyboard("{ArrowDown}{ArrowDown}");
    expect(fireEvent.keyDown(input(), { key: "Enter" })).toBe(false);

    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(charlie);
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("closes on Escape, clears the query on a second Escape and closes on Tab without selecting", async () => {
    const user = userEvent.setup();
    const onQueryChange = vi.fn();
    const onSelect = vi.fn();
    render(<Harness initialQuery="ar" onQueryChange={onQueryChange} onSelect={onSelect} />);
    await user.click(input());
    expect(screen.getByRole("listbox")).toBeInTheDocument();

    await user.keyboard("{Escape}");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(onQueryChange).not.toHaveBeenCalled();
    expect(input()).toHaveAttribute("aria-expanded", "false");

    await user.keyboard("{Escape}");
    expect(onQueryChange).toHaveBeenCalledWith("");
    expect(input()).toHaveValue("");

    await user.type(input(), "b");
    await user.keyboard("{ArrowDown}");
    expect(screen.getByRole("listbox")).toBeInTheDocument();
    await user.keyboard("{Tab}");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("selects on pointer down with the default prevented and ignores disabled options", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness initialQuery="a" onSelect={onSelect} keepSelection={false} />);
    await user.click(input());

    expect(
      fireEvent.pointerDown(screen.getByRole("option", { name: /Bravo/ }), { button: 0 }),
    ).toBe(false);
    expect(onSelect).not.toHaveBeenCalled();
    expect(input()).toHaveFocus();

    expect(
      fireEvent.pointerDown(screen.getByRole("option", { name: /Delta/ }), { button: 0 }),
    ).toBe(false);
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(delta);
    expect(input()).toHaveFocus();
  });

  it("keeps the keyboard-active option when a resting pointer reports a move without new coordinates", async () => {
    const user = userEvent.setup();
    render(<Harness initialQuery="a" />);
    await user.click(input());
    const option = (name: RegExp) => screen.getByRole("option", { name });

    fireEvent.pointerMove(option(/Charlie/), { pointerType: "mouse", clientX: 10, clientY: 10 });
    expect(option(/Charlie/)).toHaveAttribute("aria-selected", "true");

    await user.keyboard("{ArrowDown}");
    expect(option(/Delta/)).toHaveAttribute("aria-selected", "true");

    // The listbox scrolled under the resting pointer: same coordinates, so the keyboard keeps the option.
    fireEvent.pointerMove(option(/Charlie/), { pointerType: "mouse", clientX: 10, clientY: 10 });
    expect(option(/Delta/)).toHaveAttribute("aria-selected", "true");

    fireEvent.pointerMove(option(/Charlie/), { pointerType: "mouse", clientX: 12, clientY: 10 });
    expect(option(/Charlie/)).toHaveAttribute("aria-selected", "true");
  });

  it("ignores Enter and Escape while an IME composition is active", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness initialQuery="a" onSelect={onSelect} />);
    await user.click(input());
    await user.keyboard("{ArrowDown}");

    expect(fireEvent.keyDown(input(), { key: "Enter", isComposing: true })).toBe(true);
    expect(onSelect).not.toHaveBeenCalled();
    fireEvent.keyDown(input(), { key: "Escape", isComposing: true });
    expect(screen.getByRole("listbox")).toBeInTheDocument();

    fireEvent.keyDown(input(), { key: "Enter" });
    expect(onSelect).toHaveBeenCalledWith(alpha);
  });

  it("does not choose on touch or pen contact so a scroll gesture is safe, and chooses on the tap click", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness initialQuery="a" onSelect={onSelect} keepSelection={false} />);
    await user.click(input());
    const delta = () => screen.getByRole("option", { name: /Delta/ });

    for (const pointerType of ["touch", "pen"]) {
      fireEvent.pointerDown(delta(), { button: 0, pointerType });
    }
    // A scroll gesture ends in pointercancel and the browser never fires a click.
    fireEvent.pointerCancel(delta(), { pointerType: "touch" });
    expect(onSelect).not.toHaveBeenCalled();
    expect(screen.getByRole("listbox")).toBeInTheDocument();

    // jsdom drops `pointerType` from a click, so this click carries no type, like a browser that reports none.
    fireEvent.pointerDown(delta(), { button: 0, pointerType: "touch" });
    fireEvent.click(delta());
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: "delta" }));
  });

  it("makes the hovered option active without selecting it", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness initialQuery="a" onSelect={onSelect} />);
    await user.click(input());

    await user.hover(screen.getByRole("option", { name: /Charlie/ }));

    expect(screen.getByRole("option", { name: /Charlie/ })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(onSelect).not.toHaveBeenCalled();
  });
});

describe("SearchPicker chosen token", () => {
  it("replaces the input by a token that keeps the label and clears back to a focused input", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const onClear = vi.fn();
    render(<Harness initialQuery="a" onSelect={onSelect} onClear={onClear} />);
    await user.click(input());
    await user.keyboard("{ArrowDown}{Enter}");

    expect(onSelect).toHaveBeenCalledWith(alpha);
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    const token = screen.getByRole("group", { name: "Member" });
    expect(token).toHaveTextContent("Alpha");
    expect(token).toHaveTextContent("alpha@example.test");
    const clear = screen.getByRole("button", { name: "Remove choice" });
    expect(clear).toHaveFocus();

    await user.click(clear);

    expect(onClear).toHaveBeenCalledTimes(1);
    expect(input()).toHaveFocus();
    expect(screen.queryByRole("group")).not.toBeInTheDocument();
  });

  it("renders a preselected option as a token and disables the clear control with the picker", () => {
    render(<Harness initialSelected={charlie} disabled />);

    expect(screen.getByRole("group", { name: "Member" })).toHaveTextContent("Charlie");
    expect(screen.getByRole("button", { name: "Remove choice" })).toBeDisabled();
  });
});

describe("SearchPicker states", () => {
  it("shows the loading row, keeps the previous options and marks the input busy", async () => {
    const user = userEvent.setup();
    render(<Harness initialQuery="a" status="loading" />);
    await user.click(input());

    expect(screen.getByText("Searching members")).toBeInTheDocument();
    expect(screen.getAllByRole("option")).toHaveLength(4);
    expect(input()).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("shows an error row with a working retry that is announced as an alert", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<Harness initialQuery="a" status="error" options={[]} onRetry={onRetry} />);
    await user.click(input());

    expect(screen.getByRole("alert")).toHaveTextContent("Search failed");
    await user.click(screen.getByRole("button", { name: "Retry search" }));
    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(input()).toHaveFocus();

    await user.keyboard("{Enter}");
    expect(onRetry).toHaveBeenCalledTimes(2);
  });

  it("lets Tab reach the Retry control of the error row, activates it with the keyboard and closes on the next Tab", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <>
        <Harness initialQuery="a" status="error" options={[]} onRetry={onRetry} />
        <button type="button">After picker</button>
      </>,
    );
    await user.click(input());

    await user.tab();
    const retry = screen.getByRole("button", { name: "Retry search" });
    expect(retry).toHaveFocus();
    expect(screen.getByRole("alert")).toHaveTextContent("Search failed");

    await user.keyboard("{Enter}");
    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(input()).toHaveFocus();

    await user.tab();
    await user.tab();
    expect(screen.getByRole("button", { name: "After picker" })).toHaveFocus();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("closes on Escape from the Retry control and returns focus to the input", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<Harness initialQuery="a" status="error" options={[]} onRetry={onRetry} />);
    await user.click(input());
    await user.tab();
    expect(screen.getByRole("button", { name: "Retry search" })).toHaveFocus();

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Retry search" })).not.toBeInTheDocument();
    expect(input()).toHaveFocus();
    expect(onRetry).not.toHaveBeenCalled();
  });

  it("shift-Tab from the input with the error row open leaves the picker", async () => {
    const user = userEvent.setup();
    render(<Harness initialQuery="a" status="error" options={[]} />);
    await user.click(input());

    await user.tab({ shift: true });

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("ends the list with the refine row and announces it only when more matches exist", async () => {
    const user = userEvent.setup();
    const withRefine = { ...texts, refine: "Showing the first 4 matches. Type more." };
    const { rerender } = render(<Harness initialQuery="a" texts={withRefine} hasMore />);
    await user.click(input());

    expect(screen.getByRole("listbox")).toHaveTextContent(
      "Showing the first 4 matches. Type more.",
    );
    expect(screen.getAllByRole("option")).toHaveLength(4);
    expect(screen.getByRole("status")).toHaveTextContent(
      "4 results Showing the first 4 matches. Type more.",
    );

    rerender(<Harness initialQuery="a" texts={withRefine} hasMore={false} />);
    expect(screen.queryByText(/Showing the first/)).not.toBeInTheDocument();
  });

  it("shows the no-match row and announces it", async () => {
    const user = userEvent.setup();
    render(<Harness initialQuery="zz" options={[]} />);
    await user.click(input());

    expect(screen.getByRole("listbox")).toHaveTextContent("No member matches");
    expect(screen.queryAllByRole("option")).toHaveLength(0);
    expect(screen.getByRole("status")).toHaveTextContent("No member matches");
  });

  it("shows the idle hint under the input without a listbox and links it to the input", async () => {
    const user = userEvent.setup();
    render(<Harness status="idle" options={[]} describedBy="outside-hint" />);
    await user.click(input());

    const hint = screen.getByText("Type to search");
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    const describedBy = input().getAttribute("aria-describedby") ?? "";
    expect(describedBy.split(" ")).toEqual(expect.arrayContaining([hint.id, "outside-hint"]));
  });

  it("announces the result count only for a settled query", async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <SearchPicker
        label="Member"
        placeholder="Search members"
        query="a"
        onQueryChange={() => undefined}
        options={[alpha, charlie]}
        status="loading"
        selected={null}
        onSelect={() => undefined}
        onClear={() => undefined}
        texts={texts}
        onRetry={() => undefined}
      />,
    );
    await user.click(input());
    expect(screen.getByRole("status")).toBeEmptyDOMElement();

    rerender(
      <SearchPicker
        label="Member"
        placeholder="Search members"
        query="a"
        onQueryChange={() => undefined}
        options={[alpha, charlie]}
        status="ready"
        selected={null}
        onSelect={() => undefined}
        onClear={() => undefined}
        texts={texts}
        onRetry={() => undefined}
      />,
    );

    expect(screen.getByRole("status")).toHaveTextContent("2 results");
  });

  it("keeps the listbox closed while the picker is disabled", () => {
    render(<Harness initialQuery="a" disabled />);

    expect(input()).toBeDisabled();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("passes the input element to a supplied ref", () => {
    const ref = { current: null as HTMLInputElement | null };
    render(<Harness inputRef={ref} />);

    expect(ref.current).toBe(input());
  });
});
