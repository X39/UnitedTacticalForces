import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode, useRef } from "react";
import { useFleetFocusFallback } from "@/features/hosting/useFleetFocusFallback";

/*
 * Focus fallback for a fleet card that changes section (UI-SPEC "After a confirmed action" rule 5). When the
 * focused control of a card unmounts because the card moved to another list, focus goes to the same server's
 * Open server link; focus is never moved while it is on another connected element.
 */

interface HarnessProps {
  /** Server ids rendered in the Active list. */
  active: string[];
  /** Server ids rendered in the Off list. */
  off: string[];
  /** Whether the fleet container is rendered at all. */
  showContainer?: boolean;
}

function Card({ id }: { id: string }) {
  return (
    <li>
      <article data-server-card={id} aria-label={`Card ${id}`}>
        <button type="button">{`Act ${id}`}</button>
        <a href={`#/servers/${id}`} data-server-id={id}>
          {`Open ${id}`}
        </a>
      </article>
    </li>
  );
}

function Harness({ active, off, showContainer = true }: HarnessProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  useFleetFocusFallback(containerRef, `${active.join(",")}|${off.join(",")}`);
  return (
    <>
      <input aria-label="Search" />
      {showContainer && (
        <div ref={containerRef}>
          <ul aria-label="Active">
            {active.map((id) => (
              <Card key={id} id={id} />
            ))}
          </ul>
          <ul aria-label="Off">
            {off.map((id) => (
              <Card key={id} id={id} />
            ))}
          </ul>
        </div>
      )}
    </>
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("useFleetFocusFallback", () => {
  it("moves focus to the Open server link when the focused card changes section", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<Harness active={["a", "b"]} off={[]} />);
    await user.click(screen.getByRole("button", { name: "Act a" }));
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Act a" }));

    rerender(<Harness active={["b"]} off={["a"]} />);

    expect(document.activeElement).toBe(screen.getByRole("link", { name: "Open a" }));
  });

  it("does not move focus that rests on another connected element", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<Harness active={["a"]} off={[]} />);
    await user.click(screen.getByRole("button", { name: "Act a" }));
    await user.click(screen.getByRole("textbox", { name: "Search" }));

    rerender(<Harness active={[]} off={["a"]} />);

    expect(document.activeElement).toBe(screen.getByRole("textbox", { name: "Search" }));
  });

  it("focuses nothing when no card control was ever focused", () => {
    const { rerender } = render(<Harness active={["a"]} off={[]} />);
    rerender(<Harness active={[]} off={["a"]} />);
    expect(document.activeElement).toBe(document.body);
  });

  it("focuses nothing and does not throw when the last focused card is gone", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<Harness active={["a", "b"]} off={[]} />);
    await user.click(screen.getByRole("button", { name: "Act a" }));

    expect(() => rerender(<Harness active={["b"]} off={[]} />)).not.toThrow();
    expect(document.activeElement).toBe(document.body);

    expect(() => rerender(<Harness active={[]} off={[]} showContainer={false} />)).not.toThrow();
    expect(document.activeElement).toBe(document.body);
  });

  it("removes its focus listener on unmount and renders under StrictMode without warnings", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const add = vi.spyOn(document, "addEventListener");
    const remove = vi.spyOn(document, "removeEventListener");
    const user = userEvent.setup();

    const { rerender, unmount } = render(
      <StrictMode>
        <Harness active={["a"]} off={[]} />
      </StrictMode>,
    );
    await user.click(screen.getByRole("button", { name: "Act a" }));
    rerender(
      <StrictMode>
        <Harness active={[]} off={["a"]} />
      </StrictMode>,
    );
    expect(document.activeElement).toBe(screen.getByRole("link", { name: "Open a" }));

    const focusListeners = add.mock.calls
      .filter(([type]) => type === "focusin")
      .map(([, listener]) => listener);
    expect(focusListeners.length).toBeGreaterThan(0);
    unmount();
    const removed = remove.mock.calls
      .filter(([type]) => type === "focusin")
      .map(([, listener]) => listener);
    for (const listener of focusListeners) expect(removed).toContain(listener);

    expect(error).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
  });

  it("finds the link of a server id that holds CSS selector characters", async () => {
    const user = userEvent.setup();
    const tricky = 'x"] , a[data-server-id="b';
    const { rerender } = render(<Harness active={[tricky, "b"]} off={[]} />);
    await user.click(screen.getByRole("button", { name: `Act ${tricky}` }));

    expect(() => rerender(<Harness active={["b"]} off={[tricky]} />)).not.toThrow();

    expect(document.activeElement).toBe(screen.getByRole("link", { name: `Open ${tricky}` }));
  });
});
