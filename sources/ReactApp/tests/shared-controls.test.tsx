import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState, type FormEvent } from "react";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { ActionAnchor, ActionButton, ActionLink } from "@/components/ActionControl";
import { CommandListToolbar } from "@/components/CommandListToolbar";
import { PublicHero } from "@/components/PublicHero";

describe("shared action controls", () => {
  it("defaults buttons to a non-submitting type", () => {
    render(<ActionButton>Cancel</ActionButton>);

    expect(screen.getByRole("button", { name: "Cancel" })).toHaveAttribute("type", "button");
  });

  it("combines visual options with native button props", () => {
    render(
      <ActionButton
        variant="danger"
        fullWidth
        type="submit"
        disabled
        className="deployment-action"
        data-command="terminate"
      >
        Terminate
      </ActionButton>,
    );

    const button = screen.getByRole("button", { name: "Terminate" });
    expect(button).toHaveClass(
      "button",
      "button--danger",
      "button--full-width",
      "deployment-action",
    );
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("type", "submit");
    expect(button).toHaveAttribute("data-command", "terminate");
  });

  it("keeps compact router links and icon-only external anchors accessible", () => {
    render(
      <MemoryRouter>
        <ActionLink variant="gold" size="compact" to="/command" className="command-link">
          Command center
        </ActionLink>
        <ActionAnchor
          variant="glass"
          iconOnly
          aria-label="Open documentation"
          href="https://example.test/docs"
          target="_blank"
          rel="noreferrer"
        >
          <span aria-hidden="true">?</span>
        </ActionAnchor>
      </MemoryRouter>,
    );

    const routerLink = screen.getByRole("link", { name: "Command center" });
    expect(routerLink).toHaveClass("button", "button--gold", "button--small", "command-link");
    expect(routerLink).toHaveAttribute("href", "/command");

    const externalLink = screen.getByRole("link", { name: "Open documentation" });
    expect(externalLink).toHaveClass("button", "button--glass", "button--icon-only");
    expect(externalLink).toHaveAttribute("href", "https://example.test/docs");
    expect(externalLink).toHaveAttribute("target", "_blank");
    expect(externalLink).toHaveAttribute("rel", "noreferrer");
  });
});

describe("ActionButton with an unavailable reason", () => {
  it("stays focusable, exposes aria-disabled and the reason, and ignores clicks", async () => {
    const click = vi.fn();
    const user = userEvent.setup();
    render(
      <ActionButton unavailableReason="Ask an administrator." onClick={click}>
        Start
      </ActionButton>,
    );

    const button = screen.getByRole("button", { name: "Start" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).not.toHaveAttribute("disabled");
    expect(button).toHaveAccessibleDescription("Ask an administrator.");
    await user.tab();
    expect(button).toHaveFocus();
    await user.click(button);
    expect(click).not.toHaveBeenCalled();
  });

  it("does not submit an enclosing form", async () => {
    const submit = vi.fn((event: FormEvent<HTMLFormElement>) => event.preventDefault());
    const user = userEvent.setup();
    render(
      <form onSubmit={submit}>
        <ActionButton type="submit" unavailableReason="Not now.">
          Save
        </ActionButton>
      </form>,
    );

    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(submit).not.toHaveBeenCalled();
  });

  it("renders exactly as before without a reason", async () => {
    const click = vi.fn();
    const user = userEvent.setup();
    const { container } = render(<ActionButton onClick={click}>Run</ActionButton>);

    const button = screen.getByRole("button", { name: "Run" });
    expect(container.firstElementChild).toBe(button);
    expect(button).not.toHaveAttribute("aria-disabled");
    expect(button).not.toHaveAttribute("aria-describedby");
    await user.click(button);
    expect(click).toHaveBeenCalledOnce();
  });

  it("appends the reason to a caller description and supports the full-width wrapper", () => {
    const { container } = render(
      <>
        <span id="caller-help">Caller help.</span>
        <ActionButton fullWidth aria-describedby="caller-help" unavailableReason="Reason text.">
          Go
        </ActionButton>
      </>,
    );

    const button = screen.getByRole("button", { name: "Go" });
    expect(button.getAttribute("aria-describedby")?.split(" ")).toHaveLength(2);
    expect(button.getAttribute("aria-describedby")).toMatch(/^caller-help /);
    expect(button).toHaveAccessibleDescription("Caller help. Reason text.");
    expect(container.querySelector(".action-with-reason")).toHaveClass(
      "action-with-reason--full-width",
    );
  });
});

describe("CommandListToolbar", () => {
  it("updates its controlled search value, submits, and hosts page-specific controls", async () => {
    const submit = vi.fn((event: FormEvent<HTMLFormElement>) => event.preventDefault());
    const searchChange = vi.fn();
    const selectFilter = vi.fn();

    function ToolbarHarness() {
      const [searchValue, setSearchValue] = useState("initial");
      return (
        <CommandListToolbar
          className="members-admin-toolbar"
          ariaLabel="Member filters"
          onSubmit={submit}
          searchLabel="Search members"
          searchPlaceholder="Nickname or account"
          searchValue={searchValue}
          onSearchChange={(value) => {
            searchChange(value);
            setSearchValue(value);
          }}
          submitLabel="Apply search"
        >
          <div role="group" aria-label="Status filters">
            <button type="button" onClick={selectFilter}>
              Active only
            </button>
          </div>
        </CommandListToolbar>
      );
    }

    const user = userEvent.setup();
    render(<ToolbarHarness />);

    const toolbar = screen.getByRole("form", { name: "Member filters" });
    expect(toolbar).toHaveClass("toolbar", "members-admin-toolbar");
    const search = within(toolbar).getByRole("searchbox", { name: "Search members" });
    expect(search).toHaveValue("initial");
    expect(search).toHaveAttribute("placeholder", "Nickname or account");

    await user.clear(search);
    await user.type(search, "Falcon");
    expect(search).toHaveValue("Falcon");
    expect(searchChange).toHaveBeenLastCalledWith("Falcon");

    await user.click(within(toolbar).getByRole("button", { name: "Active only" }));
    expect(selectFilter).toHaveBeenCalledOnce();
    expect(submit).not.toHaveBeenCalled();

    await user.click(within(toolbar).getByRole("button", { name: "Apply search" }));
    expect(submit).toHaveBeenCalledOnce();
  });

  it("renders the shared search field and no raw text control of its own", async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    const submit = vi.fn((event: FormEvent<HTMLFormElement>) => event.preventDefault());
    const searchChange = vi.fn();
    render(
      <I18nextProvider i18n={i18n}>
        <CommandListToolbar
          ariaLabel="Role filters"
          onSubmit={submit}
          searchLabel="Search roles"
          searchValue="ops"
          onSearchChange={searchChange}
          submitLabel="Apply search"
        />
      </I18nextProvider>,
    );

    const toolbar = screen.getByRole("form", { name: "Role filters" });
    // The only text input is the shared SearchField input, which brings its own icon-only clear control.
    expect(toolbar.querySelectorAll("input")).toHaveLength(1);
    expect(toolbar.querySelector("input.search-control__input")).toBe(
      within(toolbar).getByRole("searchbox", { name: "Search roles" }),
    );
    expect(within(toolbar).getByRole("searchbox", { name: "Search roles" })).toHaveAttribute(
      "placeholder",
      "Search roles",
    );

    const user = userEvent.setup();
    await user.click(within(toolbar).getByRole("button", { name: "Clear search" }));
    expect(searchChange).toHaveBeenLastCalledWith("");
    expect(submit).not.toHaveBeenCalled();

    await user.type(within(toolbar).getByRole("searchbox"), "{Enter}");
    expect(submit).toHaveBeenCalledOnce();
  });
});

describe("PublicHero", () => {
  it("composes compact, single-column and labeled aside variants", () => {
    const { container } = render(
      <PublicHero
        compact
        singleColumn
        ariaLabel="Unit introduction"
        className="unit-hero"
        innerClassName="unit-hero__inner"
        copyClassName="unit-hero__copy"
        actionsClassName="unit-hero__actions"
        kicker="United Tactical Forces"
        title="Train. Plan. Execute."
        description="Combined-arms operations with purpose."
        actions={<button type="button">Join us</button>}
        aside={<p>Saturday operation ready</p>}
        asideClassName="unit-hero__briefing"
        asideLabel="Operational snapshot"
      />,
    );

    const hero = screen.getByRole("region", { name: "Unit introduction" });
    expect(hero).toHaveClass("public-hero", "public-hero--compact", "unit-hero");
    expect(screen.getByRole("heading", { level: 1, name: "Train. Plan. Execute." })).toBeVisible();
    expect(container.querySelector(".public-hero__inner")).toHaveClass(
      "public-hero__inner--single",
      "unit-hero__inner",
    );
    expect(container.querySelector(".public-hero__copy")).toHaveClass("unit-hero__copy");
    expect(container.querySelector(".public-hero__actions")).toHaveClass("unit-hero__actions");

    const aside = screen.getByRole("complementary", { name: "Operational snapshot" });
    expect(aside).toHaveClass("public-hero__panel", "unit-hero__briefing");
    expect(aside).toHaveTextContent("Saturday operation ready");
  });
});
