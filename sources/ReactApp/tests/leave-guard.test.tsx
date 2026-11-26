import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { Link } from "react-router";
import { i18nReady } from "@/app/i18n";
import { ConfirmDialog } from "@/components/primitives";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import { leavesWorkspaceSection, sectionTo } from "@/features/hosting/workspaceSections";
import { renderWithProviders } from "./helpers/renderWithProviders";

const route = "/command/servers/1?tab=settings";

/**
 * Mounts a guarded Settings view: a dirty toggle, links that leave or stay in the section and the three-action
 * leave dialog, exactly the way the feature plans wire the hook.
 */
function Harness({ onReview, initialDirty = true }: { onReview(): void; initialDirty?: boolean }) {
  const [dirty, setDirty] = useState(initialDirty);
  const guard = useLeaveGuard({
    dirty,
    shouldBlock: ({ currentLocation, nextLocation }) =>
      leavesWorkspaceSection(currentLocation, nextLocation, "settings"),
  });
  return (
    <div>
      <p data-testid="dirty">{dirty ? "dirty" : "clean"}</p>
      <button type="button" onClick={() => setDirty(true)}>
        Make dirty
      </button>
      <button type="button" onClick={() => setDirty(false)}>
        Make clean
      </button>
      <Link to={sectionTo("1", "files")}>Go to files</Link>
      <Link to={{ pathname: "/command/servers/1", search: "?tab=settings&action=start" }} replace>
        Replace action
      </Link>
      <Link to="/command/servers/2?tab=settings">Other server</Link>
      <ConfirmDialog
        open={guard.blocked}
        title="Leave without applying?"
        description="You have staged changes."
        cancelLabel="Stay here"
        confirmLabel="Discard and leave"
        confirmVariant="danger"
        initialFocus="dismiss"
        onCancel={guard.stay}
        onConfirm={() => {
          setDirty(false);
          guard.proceed();
        }}
        extraAction={{
          label: "Review changes",
          variant: "gold",
          onClick: () => {
            guard.stay();
            onReview();
          },
        }}
      />
    </div>
  );
}

function renderHarness(options: { route?: string; initialDirty?: boolean } = {}) {
  const onReview = vi.fn();
  const view = renderWithProviders(
    <Harness
      onReview={onReview}
      {...(options.initialDirty === undefined ? {} : { initialDirty: options.initialDirty })}
    />,
    { route: options.route ?? route, path: "/command/servers/:id" },
  );
  return { ...view, onReview };
}

function locationOf(router: { state: { location: { pathname: string; search: string } } }) {
  return `${router.state.location.pathname}${router.state.location.search}`;
}

describe("useLeaveGuard", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("blocks a link that leaves the section and offers Stay, Discard and Review in that order", async () => {
    const user = userEvent.setup();
    const { router } = renderHarness();

    await user.click(screen.getByRole("link", { name: "Go to files" }));

    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    const buttons = within(dialog)
      .getAllByRole("button")
      .map((button) => button.textContent);
    expect(buttons).toEqual(["Stay here", "Discard and leave", "Review changes"]);
    expect(within(dialog).getByRole("button", { name: "Stay here" })).toHaveFocus();
    expect(locationOf(router)).toBe("/command/servers/1?tab=settings");
  });

  it("keeps the location, the dirty state and the history after Stay", async () => {
    const user = userEvent.setup();
    const { router } = renderHarness({
      route: "/command/servers/1?tab=files",
      initialDirty: false,
    });
    await act(async () => {
      await router.navigate("/command/servers/1?tab=settings");
    });
    await user.click(screen.getByRole("button", { name: "Make dirty" }));

    await act(async () => {
      await router.navigate(-1);
    });
    const dialog = await screen.findByRole("dialog", { name: "Leave without applying?" });
    expect(locationOf(router)).toBe("/command/servers/1?tab=settings");

    await user.click(within(dialog).getByRole("button", { name: "Stay here" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(locationOf(router)).toBe("/command/servers/1?tab=settings");
    expect(screen.getByTestId("dirty")).toHaveTextContent("dirty");

    // The entry behind the current one is still the files entry: going back once the view is clean lands there.
    await user.click(screen.getByRole("button", { name: "Make clean" }));
    await act(async () => {
      await router.navigate(-1);
    });
    expect(locationOf(router)).toBe("/command/servers/1?tab=files");
  });

  it("proceeds to the target after Discard", async () => {
    const user = userEvent.setup();
    const { router } = renderHarness();

    await user.click(screen.getByRole("link", { name: "Go to files" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Discard and leave" }));

    expect(locationOf(router)).toBe("/command/servers/1?tab=files");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("closes the dialog, stays and calls the review callback once on Review", async () => {
    const user = userEvent.setup();
    const { router, onReview } = renderHarness();

    await user.click(screen.getByRole("link", { name: "Go to files" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Review changes" }));

    expect(onReview).toHaveBeenCalledOnce();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(locationOf(router)).toBe("/command/servers/1?tab=settings");
  });

  it("blocks a navigation to another server", async () => {
    const user = userEvent.setup();
    const { router } = renderHarness();

    await user.click(screen.getByRole("link", { name: "Other server" }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(locationOf(router)).toBe("/command/servers/1?tab=settings");
  });

  it("never blocks a replace that stays in the section", async () => {
    const user = userEvent.setup();
    const { router } = renderHarness();

    await user.click(screen.getByRole("link", { name: "Replace action" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(locationOf(router)).toBe("/command/servers/1?tab=settings&action=start");
  });

  it("blocks nothing while the view is not dirty", async () => {
    const user = userEvent.setup();
    const { router } = renderHarness({ initialDirty: false });

    await user.click(screen.getByRole("link", { name: "Go to files" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(locationOf(router)).toBe("/command/servers/1?tab=files");
  });

  it("registers beforeunload only while dirty and never uses window.confirm", async () => {
    const user = userEvent.setup();
    const confirm = vi.spyOn(window, "confirm");
    const add = vi.spyOn(window, "addEventListener");
    const remove = vi.spyOn(window, "removeEventListener");

    renderHarness({ initialDirty: false });
    expect(add.mock.calls.filter(([type]) => type === "beforeunload")).toHaveLength(0);

    await user.click(screen.getByRole("button", { name: "Make dirty" }));
    const registered = add.mock.calls.filter(([type]) => type === "beforeunload");
    expect(registered).toHaveLength(1);

    const event = new Event("beforeunload", { cancelable: true });
    Object.defineProperty(event, "returnValue", { writable: true, value: "unset" });
    window.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect((event as unknown as { returnValue: string }).returnValue).toBe("");

    await user.click(screen.getByRole("button", { name: "Make clean" }));
    expect(remove.mock.calls.some(([type]) => type === "beforeunload")).toBe(true);
    const afterClean = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(afterClean);
    expect(afterClean.defaultPrevented).toBe(false);

    await user.click(screen.getByRole("link", { name: "Go to files" }));
    expect(confirm).not.toHaveBeenCalled();
  });
});
