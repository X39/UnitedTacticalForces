import { act, fireEvent, render, screen, within } from "@testing-library/react";
import type { ComponentProps } from "react";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { FleetLiveNotice } from "@/components/server/FleetLiveNotice";

type NoticeProps = ComponentProps<typeof FleetLiveNotice>;

function element(props: NoticeProps) {
  return (
    <I18nextProvider i18n={i18n}>
      <FleetLiveNotice {...props} />
    </I18nextProvider>
  );
}

function renderNotice(overrides: Partial<NoticeProps> = {}) {
  const props: NoticeProps = {
    status: "reconnecting",
    epoch: 1,
    onReconnect: vi.fn(),
    ...overrides,
  };
  const view = render(element(props));
  return {
    ...view,
    props,
    update: (next: Partial<NoticeProps>) => view.rerender(element({ ...props, ...next })),
  };
}

function advance(milliseconds: number): void {
  act(() => {
    vi.advanceTimersByTime(milliseconds);
  });
}

describe("FleetLiveNotice", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it.each(["connecting", "open"] as const)("renders nothing while %s", (status) => {
    vi.useFakeTimers();
    const { container } = renderNotice({ status });

    advance(60_000);

    expect(container).toBeEmptyDOMElement();
  });

  it("shows the paused notice only after the delay of continuous disconnection", () => {
    vi.useFakeTimers();
    const { container } = renderNotice();

    advance(2_999);
    expect(container).toBeEmptyDOMElement();

    advance(1);
    const panel = screen.getByRole("status");
    expect(panel).toHaveClass("state-panel--info");
    expect(within(panel).getByRole("heading", { name: "Live updates paused" })).toBeVisible();
    expect(panel).toHaveTextContent(
      "Changes to servers may be missing until the connection returns. Reconnecting automatically.",
    );
    expect(within(panel).getByRole("button", { name: "Reconnect now" })).toBeEnabled();
    const icon = panel.querySelector(".state-panel__icon");
    expect(icon).toHaveAttribute("aria-hidden", "true");
    expect(within(icon as HTMLElement).getAllByTestId("fluent-icon")).toHaveLength(1);
    expect(container.querySelector(".fleet-live-notice")).not.toBeNull();
  });

  it("honours a configured delay", () => {
    vi.useFakeTimers();
    const { container } = renderNotice({ delayMs: 500 });

    advance(499);
    expect(container).toBeEmptyDOMElement();
    advance(1);
    expect(screen.getByRole("status")).toBeVisible();
  });

  it("hides silently on recovery and waits the full delay again for a new disconnect epoch", () => {
    vi.useFakeTimers();
    const { container, update } = renderNotice({ epoch: 1 });
    advance(3_000);
    expect(screen.getByRole("status")).toBeVisible();

    update({ status: "open", epoch: 1 });
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    update({ status: "reconnecting", epoch: 2 });
    expect(container).toBeEmptyDOMElement();
    advance(2_999);
    expect(container).toBeEmptyDOMElement();
    advance(1);
    expect(screen.getByRole("status")).toBeVisible();
  });

  it("restarts the delay when a new epoch begins before the old notice appeared", () => {
    vi.useFakeTimers();
    const { container, update } = renderNotice({ epoch: 1 });
    advance(2_000);

    update({ status: "open", epoch: 1 });
    advance(5_000);
    expect(container).toBeEmptyDOMElement();

    update({ status: "reconnecting", epoch: 2 });
    advance(2_000);
    expect(container).toBeEmptyDOMElement();
    advance(1_000);
    expect(screen.getByRole("status")).toBeVisible();
  });

  it("switches to the unavailable copy in the same shell without restarting the delay", () => {
    vi.useFakeTimers();
    const { update } = renderNotice({ epoch: 4 });
    advance(3_000);

    update({ status: "unavailable", epoch: 4 });

    const panel = screen.getByRole("status");
    expect(panel).toHaveClass("state-panel--info");
    expect(
      within(panel).getByRole("heading", { name: "Live updates are unavailable" }),
    ).toBeVisible();
    expect(panel).toHaveTextContent(
      "Use Refresh servers to see the latest state. We keep trying in the background.",
    );
    expect(within(panel).getByRole("button", { name: "Reconnect now" })).toBeVisible();
  });

  it("shows the unavailable copy after the delay when the first status is unavailable", () => {
    vi.useFakeTimers();
    const { container } = renderNotice({ status: "unavailable" });

    advance(2_999);
    expect(container).toBeEmptyDOMElement();
    advance(1);
    expect(screen.getByRole("heading", { name: "Live updates are unavailable" })).toBeVisible();
  });

  it("calls onReconnect once per activation and never uses the warning or danger tone", () => {
    vi.useFakeTimers();
    const { container, props } = renderNotice();
    advance(3_000);

    fireEvent.click(screen.getByRole("button", { name: "Reconnect now" }));

    expect(props.onReconnect).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    const button = screen.getByRole("button", { name: "Reconnect now" });
    expect(button).toHaveClass("button--ghost");
    expect(
      container.querySelector(
        ".state-panel--warning, .state-panel--error, .button--danger, .button--gold",
      ),
    ).toBeNull();
  });
});
