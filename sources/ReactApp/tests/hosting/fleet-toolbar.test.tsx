import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createRef, useState, type ComponentProps } from "react";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { FleetToolbar } from "@/components/server/FleetToolbar";
import type { FleetFilter } from "@/features/hosting/fleetModel";

type ToolbarProps = ComponentProps<typeof FleetToolbar>;

const counts: Record<FleetFilter, number> = { all: 12, mine: 0, running: 3, off: 9 };

function renderToolbar(overrides: Partial<ToolbarProps> = {}) {
  const props: ToolbarProps = {
    search: "",
    onSearchChange: vi.fn(),
    filter: "all",
    counts,
    onFilterChange: vi.fn(),
    summary: null,
    ...overrides,
  };
  return {
    props,
    ...render(
      <I18nextProvider i18n={i18n}>
        <FleetToolbar {...props} />
      </I18nextProvider>,
    ),
  };
}

/** Holds the search text in state so the controlled field behaves like on the page. */
function StatefulToolbar({
  onSearchChange,
  searchInputRef,
}: Pick<ToolbarProps, "onSearchChange" | "searchInputRef">) {
  const [search, setSearch] = useState("");
  return (
    <I18nextProvider i18n={i18n}>
      <FleetToolbar
        search={search}
        onSearchChange={(value) => {
          onSearchChange(value);
          setSearch(value);
        }}
        {...(searchInputRef ? { searchInputRef } : {})}
        filter="all"
        counts={counts}
        onFilterChange={vi.fn()}
        summary={null}
      />
    </I18nextProvider>
  );
}

async function switchLanguage(language: "en" | "de"): Promise<void> {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

function summaryRegion(container: HTMLElement): HTMLElement {
  const region = container.querySelector<HTMLElement>(".fleet-toolbar__summary");
  if (!region) throw new Error("The result summary region is missing.");
  return region;
}

describe("FleetToolbar", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await switchLanguage("en");
  });

  it("renders the search field and the four counted chips in order", async () => {
    await switchLanguage("en");
    renderToolbar();

    const input = screen.getByRole("searchbox", { name: "Search servers" });
    expect(input).toHaveAttribute("placeholder", "Name, game, or cost owner");
    const group = screen.getByRole("group", { name: "Filter servers" });
    expect(
      within(group)
        .getAllByRole("button")
        .map((chip) => chip.textContent),
    ).toEqual(["All 12", "Mine 0", "Running 3", "Off 9"]);
  });

  it("presses the active filter and reports other filters, including a zero count", async () => {
    const user = userEvent.setup();
    const { props } = renderToolbar({ filter: "running" });

    const group = screen.getByRole("group", { name: "Filter servers" });
    expect(
      within(group)
        .getAllByRole("button")
        .map((chip) => chip.getAttribute("aria-pressed")),
    ).toEqual(["false", "false", "true", "false"]);

    const mine = screen.getByRole("button", { name: "Mine 0" });
    expect(mine).toBeEnabled();
    await user.click(mine);
    expect(props.onFilterChange).toHaveBeenLastCalledWith("mine");

    await user.click(screen.getByRole("button", { name: "Off 9" }));
    expect(props.onFilterChange).toHaveBeenLastCalledWith("off");
  });

  it("reports typing, clear and Escape and hands the input to the supplied ref", async () => {
    const user = userEvent.setup();
    const onSearchChange = vi.fn();
    const searchInputRef = createRef<HTMLInputElement>();
    render(<StatefulToolbar onSearchChange={onSearchChange} searchInputRef={searchInputRef} />);

    const input = screen.getByRole("searchbox", { name: "Search servers" });
    expect(searchInputRef.current).toBe(input);

    await user.type(input, "ab");
    expect(onSearchChange).toHaveBeenLastCalledWith("ab");

    await user.click(screen.getByRole("button", { name: "Clear search" }));
    expect(onSearchChange).toHaveBeenLastCalledWith("");
    expect(input).toHaveFocus();

    await user.type(input, "c");
    await user.keyboard("{Escape}");
    expect(onSearchChange).toHaveBeenLastCalledWith("");
    expect(input).toHaveValue("");
  });

  it("keeps an always-present polite summary region with plural and German text", async () => {
    await switchLanguage("en");
    const { container, rerender, props } = renderToolbar();

    const region = summaryRegion(container);
    expect(region).toHaveAttribute("aria-live", "polite");
    expect(region).toHaveAttribute("aria-atomic", "true");
    expect(region).toBeEmptyDOMElement();

    const renderWith = (summary: ToolbarProps["summary"]) =>
      rerender(
        <I18nextProvider i18n={i18n}>
          <FleetToolbar {...props} summary={summary} />
        </I18nextProvider>,
      );

    renderWith({ shown: 1, total: 1 });
    expect(summaryRegion(container)).toHaveTextContent("1 of 1 server shown");

    renderWith({ shown: 3, total: 12 });
    expect(summaryRegion(container)).toHaveTextContent("3 of 12 servers shown");

    await switchLanguage("de");
    expect(summaryRegion(container)).toHaveTextContent("3 von 12 Server angezeigt");
    expect(screen.getByRole("group", { name: "Server filtern" })).toBeVisible();

    renderWith(null);
    expect(summaryRegion(container)).toBeEmptyDOMElement();
  });

  it("formats counts and summary numbers for the current locale", async () => {
    await switchLanguage("en");
    const large: Record<FleetFilter, number> = { all: 1234, mine: 0, running: 1000, off: 234 };
    const { container } = renderToolbar({ counts: large, summary: { shown: 1000, total: 1234 } });

    expect(screen.getByRole("button", { name: "All 1,234" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Running 1,000" })).toBeVisible();
    expect(summaryRegion(container)).toHaveTextContent("1,000 of 1,234 servers shown");

    await switchLanguage("de");
    expect(screen.getByRole("button", { name: "Alle 1.234" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Laufend 1.000" })).toBeVisible();
    expect(summaryRegion(container)).toHaveTextContent("1.000 von 1.234 Server angezeigt");
  });
});
