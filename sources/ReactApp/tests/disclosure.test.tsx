import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createRef, useState } from "react";
import { Disclosure } from "@/components/Disclosure";
import { readProjectFile } from "./helpers/cssTokens";

/** A stateful host that records every reported open state. */
function Harness({ onOpenChange }: { onOpenChange: (open: boolean) => void }) {
  const [open, setOpen] = useState(false);
  return (
    <Disclosure
      title="Advanced options"
      summary="Default region and size"
      open={open}
      onOpenChange={(next) => {
        onOpenChange(next);
        setOpen(next);
      }}
    >
      <p>Hidden content</p>
    </Disclosure>
  );
}

describe("Disclosure", () => {
  it("starts closed with the summary line visible and the content wired to the button", () => {
    const { container } = render(<Harness onOpenChange={() => undefined} />);

    const button = screen.getByRole("button", { name: "Advanced options" });
    expect(button).toHaveAttribute("aria-expanded", "false");
    const content = container.querySelector(".disclosure__content");
    expect(content).toHaveAttribute("hidden");
    expect(button).toHaveAttribute("aria-controls", content?.id);
    expect(screen.getByText("Default region and size")).toBeVisible();
    expect(screen.getByText("Hidden content")).not.toBeVisible();
  });

  it("renders no native disclosure element", () => {
    const { container } = render(<Harness onOpenChange={() => undefined} />);

    expect(container.getElementsByTagName("details")).toHaveLength(0);
    expect(container.getElementsByTagName("summary")).toHaveLength(0);
    expect(screen.getByRole("button", { name: "Advanced options" })).toHaveAttribute(
      "type",
      "button",
    );
  });

  it("toggles through onOpenChange on click and reflects the state on the button", async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    render(<Harness onOpenChange={onOpenChange} />);
    const button = screen.getByRole("button", { name: "Advanced options" });

    await user.click(button);
    expect(onOpenChange).toHaveBeenLastCalledWith(true);
    expect(button).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("region", { name: "Advanced options" })).toBeVisible();
    expect(screen.getByText("Hidden content")).toBeVisible();

    await user.click(button);
    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    expect(button).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("Hidden content")).not.toBeVisible();
  });

  it("toggles with Enter and Space through the native button", async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    render(<Harness onOpenChange={onOpenChange} />);
    const button = screen.getByRole("button", { name: "Advanced options" });

    button.focus();
    await user.keyboard("{Enter}");
    expect(onOpenChange).toHaveBeenLastCalledWith(true);
    expect(button).toHaveAttribute("aria-expanded", "true");

    await user.keyboard(" ");
    expect(onOpenChange).toHaveBeenLastCalledWith(false);
    expect(button).toHaveAttribute("aria-expanded", "false");
    expect(onOpenChange).toHaveBeenCalledTimes(2);
  });

  it("opens when the parent sets open", () => {
    render(
      <Disclosure title="Advanced" open onOpenChange={() => undefined}>
        <p>Body</p>
      </Disclosure>,
    );

    expect(screen.getByRole("button", { name: "Advanced" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    expect(screen.getByRole("region", { name: "Advanced" })).toBeVisible();
  });

  it("names the button by the title and describes it by the summary line", () => {
    render(<Harness onOpenChange={() => undefined} />);

    const button = screen.getByRole("button", { name: "Advanced options" });
    const line = screen.getByText("Default region and size");
    expect(button).toHaveAttribute("aria-describedby", line.id);
    expect(button).toHaveAccessibleName("Advanced options");
    expect(button).toHaveAccessibleDescription("Default region and size");
  });

  it("labels the content region by the title", () => {
    const { container } = render(<Harness onOpenChange={() => undefined} />);

    const region = container.querySelector(".disclosure__content");
    expect(region).toHaveAttribute("role", "region");
    const title = screen.getByText("Advanced options");
    expect(region).toHaveAttribute("aria-labelledby", title.id);
  });

  it("hides the chevron icon from assistive technology", () => {
    render(<Harness onOpenChange={() => undefined} />);

    const icon = screen.getByRole("button", { name: "Advanced options" }).querySelector("svg");
    expect(icon).toHaveAttribute("aria-hidden", "true");
  });

  it("rotates the open chevron outside the motion media query, so reduced motion still shows the state", () => {
    const css = readProjectFile("src/components/server/form-fields.css");
    const motionBlocks = css
      .split("@media (prefers-reduced-motion: no-preference)")
      .slice(1)
      .map((block) => block.split(/\r?\n\}/)[0] ?? "");
    const chevronBlock = motionBlocks.find((block) => block.includes(".disclosure__chevron")) ?? "";

    expect(chevronBlock).toContain("transition: transform");
    expect(chevronBlock).not.toContain("rotate(");
    expect(css).toMatch(
      /\.disclosure\[data-open\] > \.disclosure__summary \.disclosure__chevron[^{]*\{\s*transform: rotate\(180deg\);/,
    );
  });

  describe("heading, id, badge, variant and button ref", () => {
    it("wraps the button in a heading named by the title alone", () => {
      render(
        <Disclosure
          title="Settings"
          summary="Five panels"
          badge="3 staged"
          headingLevel={2}
          open={false}
          onOpenChange={() => undefined}
        >
          <p>Body</p>
        </Disclosure>,
      );

      const heading = screen.getByRole("heading", { level: 2, name: "Settings" });
      expect(heading).toHaveClass("disclosure__heading");
      expect(heading).toContainElement(screen.getByRole("button", { name: "Settings" }));
      expect(heading).not.toHaveAccessibleName(expect.stringContaining("staged"));
      expect(heading).not.toHaveAccessibleName(expect.stringContaining("Five panels"));
    });

    it("renders an h3 for heading level 3", () => {
      render(
        <Disclosure title="Detail" headingLevel={3} open={false} onOpenChange={() => undefined}>
          <p>Body</p>
        </Disclosure>,
      );

      expect(screen.getByRole("heading", { level: 3, name: "Detail" })).toBeInTheDocument();
    });

    it("renders no heading without headingLevel", () => {
      render(
        <Disclosure title="Plain" open={false} onOpenChange={() => undefined}>
          <p>Body</p>
        </Disclosure>,
      );

      expect(screen.queryByRole("heading")).toBeNull();
    });

    it("puts the id on the outer element", () => {
      const { container } = render(
        <Disclosure title="Anchor" id="settings-anchor" open={false} onOpenChange={() => undefined}>
          <p>Body</p>
        </Disclosure>,
      );

      expect(container.firstElementChild).toHaveAttribute("id", "settings-anchor");
      expect(container.firstElementChild).toHaveClass("disclosure");
    });

    it("renders the badge before the chevron and describes the button by summary and badge", () => {
      render(
        <Disclosure
          title="Panel"
          summary="Line"
          badge="2 staged"
          open={false}
          onOpenChange={() => undefined}
        >
          <p>Body</p>
        </Disclosure>,
      );

      const button = screen.getByRole("button", { name: "Panel" });
      const badge = screen.getByText("2 staged");
      expect(badge).toHaveClass("disclosure__badge");
      expect(button).toContainElement(badge);
      const chevron = button.querySelector("svg");
      expect(
        badge.compareDocumentPosition(chevron as Element) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(button).toHaveAttribute(
        "aria-describedby",
        `${screen.getByText("Line").id} ${badge.id}`,
      );
      expect(button).toHaveAccessibleDescription("Line 2 staged");
      expect(button).toHaveAccessibleName("Panel");
    });

    it("adds the panel class for the panel variant only", () => {
      const { container, rerender } = render(
        <Disclosure title="Panel" variant="panel" open={false} onOpenChange={() => undefined}>
          <p>Body</p>
        </Disclosure>,
      );
      expect(container.firstElementChild).toHaveClass("disclosure--panel");

      rerender(
        <Disclosure title="Panel" open={false} onOpenChange={() => undefined}>
          <p>Body</p>
        </Disclosure>,
      );
      expect(container.firstElementChild).not.toHaveClass("disclosure--panel");
    });

    it("forwards the button ref", () => {
      const ref = createRef<HTMLButtonElement>();
      render(
        <Disclosure title="Focus me" buttonRef={ref} open={false} onOpenChange={() => undefined}>
          <p>Body</p>
        </Disclosure>,
      );

      expect(ref.current).toBe(screen.getByRole("button", { name: "Focus me" }));
    });

    it("toggles by keyboard inside a heading and keeps the content mounted while closed", async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      render(
        <Disclosure title="Heading" headingLevel={2} open={false} onOpenChange={onOpenChange}>
          <p>Kept</p>
        </Disclosure>,
      );

      expect(screen.getByText("Kept")).toBeInTheDocument();
      expect(screen.getByRole("region", { hidden: true })).toHaveAttribute("hidden");
      screen.getByRole("button", { name: "Heading" }).focus();
      await user.keyboard("{Enter}");
      expect(onOpenChange).toHaveBeenLastCalledWith(true);
      await user.keyboard(" ");
      expect(onOpenChange).toHaveBeenCalledTimes(2);
    });
  });
});
