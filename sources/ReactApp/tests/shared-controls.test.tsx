import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState, type FormEvent } from "react";
import { MemoryRouter } from "react-router";
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
    const search = within(toolbar).getByRole("textbox", { name: "Search members" });
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
