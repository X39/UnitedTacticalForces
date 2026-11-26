import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n, { i18nReady } from "@/app/i18n";
import { FilterChipGroup, type FilterChipItem } from "@/components/FilterChipGroup";

type ChipId = "all" | "mine" | "running" | "off";

const items: FilterChipItem<ChipId>[] = [
  { id: "all", label: "All", count: 1234 },
  { id: "mine", label: "Mine", count: 0 },
  { id: "running", label: "Running", count: 3 },
  { id: "off", label: "Off" },
];

describe("FilterChipGroup", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders a named group with one pressed chip carrying the check icon and locale counts", async () => {
    await act(async () => {
      await i18n.changeLanguage("en");
    });
    render(
      <FilterChipGroup label="Filter servers" items={items} value="running" onChange={vi.fn()} />,
    );

    const group = screen.getByRole("group", { name: "Filter servers" });
    const chips = within(group).getAllByRole("button");
    expect(chips.map((chip) => chip.textContent)).toEqual([
      "All 1,234",
      "Mine 0",
      "Running 3",
      "Off",
    ]);
    expect(chips.map((chip) => chip.getAttribute("aria-pressed"))).toEqual([
      "false",
      "false",
      "true",
      "false",
    ]);
    chips.forEach((chip, index) => {
      expect(chip).toHaveClass("button", "button--ghost", "filter-chip");
      expect(chip).not.toHaveClass("button--gold");
      expect(chip).toBeEnabled();
      const icons = within(chip).queryAllByTestId("fluent-icon");
      expect(icons).toHaveLength(index === 2 ? 1 : 0);
    });
  });

  it("formats counts for German", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    render(<FilterChipGroup label="Server filtern" items={items} value="all" onChange={vi.fn()} />);

    expect(screen.getByRole("button", { name: "All 1.234" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("uses a supplied count formatter", () => {
    render(
      <FilterChipGroup
        label="Filter servers"
        items={items}
        value="all"
        onChange={vi.fn()}
        formatCount={(count) => `#${count}`}
      />,
    );

    expect(screen.getByRole("button", { name: "Mine #0" })).toBeVisible();
  });

  it("reports the activated chip, including the pressed one, without intercepting arrow keys", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <FilterChipGroup label="Filter servers" items={items} value="all" onChange={onChange} />,
    );

    await user.click(screen.getByRole("button", { name: "Mine 0" }));
    expect(onChange).toHaveBeenLastCalledWith("mine");

    await user.click(screen.getByRole("button", { name: /^All/ }));
    expect(onChange).toHaveBeenLastCalledWith("all");
    expect(onChange).toHaveBeenCalledTimes(2);

    await user.keyboard("{ArrowRight}");
    expect(screen.getByRole("button", { name: /^All/ })).toHaveFocus();
    expect(onChange).toHaveBeenCalledTimes(2);

    await user.tab();
    expect(screen.getByRole("button", { name: "Mine 0" })).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(onChange).toHaveBeenLastCalledWith("mine");
    await user.tab();
    expect(screen.getByRole("button", { name: "Running 3" })).toHaveFocus();
    await user.keyboard(" ");
    expect(onChange).toHaveBeenLastCalledWith("running");
    await user.tab();
    expect(screen.getByRole("button", { name: "Off" })).toHaveFocus();
  });

  it("names each chip by its label and count", async () => {
    await act(async () => {
      await i18n.changeLanguage("en");
    });
    render(<FilterChipGroup label="Filter servers" items={items} value="off" onChange={vi.fn()} />);

    const group = screen.getByRole("group", { name: "Filter servers" });
    expect(
      within(group)
        .getAllByRole("button")
        .map((chip) => chip.textContent),
    ).toHaveLength(4);
    expect(screen.getByRole("button", { name: "All 1,234" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Mine 0" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Running 3" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Off" })).toHaveAttribute("aria-pressed", "true");
  });
});
