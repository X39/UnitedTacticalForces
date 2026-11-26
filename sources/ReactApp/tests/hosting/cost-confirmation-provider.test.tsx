import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRef, useState } from "react";
import { i18nReady } from "@/app/i18n";
import {
  useCostConfirmation,
  type CostConfirmationRequest,
} from "@/components/server/useCostConfirmation";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob, ServerAction } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

const startAction = {
  id: "start",
  label: "Start server",
  description: "Starts the server.",
  enabled: true,
  pending: false,
  dangerous: false,
  costImpact: "StartsCost",
  costDescription: "Starts billing.",
  expectedDurationSeconds: null,
} as ServerAction;

const terminateAction = {
  ...startAction,
  id: "terminate",
  label: "Terminate",
  costImpact: "StopsCost",
  confirmationChallenge: "Alpha",
} as ServerAction;

function makeServer(actions: ServerAction[] = [startAction, terminateAction]): GameServer {
  return {
    primaryKey: "server-1",
    title: "Alpha",
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: null,
    costOwnerName: "Falcon One",
    isLaunchDisabled: false,
    deployment: null,
    actions,
    pendingJobCount: 0,
  } as unknown as GameServer;
}

const startRequest: CostConfirmationRequest = {
  serverId: "server-1",
  actionId: "start",
  source: "workspace-primary",
};

/** Page with one probe button per request plus a heading, as a routed page would render. */
function Probe({ requests }: { requests: Record<string, CostConfirmationRequest> }) {
  const { request } = useCostConfirmation();
  return (
    <main>
      <h1 tabIndex={-1}>Server page</h1>
      {Object.entries(requests).map(([label, value]) => (
        <button key={label} type="button" onClick={() => request(value)}>
          {label}
        </button>
      ))}
      <button
        type="button"
        onClick={() => {
          request(startRequest);
          request(startRequest);
        }}
      >
        request twice
      </button>
    </main>
  );
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((done, fail) => {
    resolve = done;
    reject = fail;
  });
  return { promise, resolve, reject };
}

const job = { primaryKey: "job-1" } as LifecycleJob;

describe("CostConfirmationProvider", () => {
  beforeAll(async () => i18nReady);

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(
    options: Parameters<typeof renderWithProviders>[1] = {},
    server = makeServer(),
    extraRequests: Record<string, CostConfirmationRequest> = {},
  ) {
    const serverSpy = vi.spyOn(hostingService, "server").mockResolvedValue(server);
    const executeSpy = vi.spyOn(hostingService, "executeAction").mockResolvedValue(job);
    const view = renderWithProviders(
      <Probe
        requests={{
          start: startRequest,
          "workspace-action terminate": { ...startRequest, actionId: "terminate" },
          "unknown action": { ...startRequest, actionId: "nope" },
          "deep link": { ...startRequest, source: "deep-link" },
          ...extraRequests,
        }}
      />,
      options,
    );
    view.queryClient.setQueryData(hostingQueryKeys.fleet, [server]);
    return { ...view, serverSpy, executeSpy, server };
  }

  it("opens from the fleet cache without a request call or an execution and focuses dismiss", async () => {
    const { serverSpy, executeSpy } = setup();
    await userEvent.setup().click(screen.getByRole("button", { name: "start" }));
    expect(await screen.findByRole("dialog", { name: "Start Alpha?" })).toBeVisible();
    expect(serverSpy).not.toHaveBeenCalled();
    expect(executeSpy).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Don't start server" })).toHaveFocus();
  });

  it("loads an uncached server before opening", async () => {
    const { serverSpy, queryClient } = setup();
    queryClient.setQueryData(hostingQueryKeys.fleet, []);
    await userEvent.setup().click(screen.getByRole("button", { name: "start" }));
    expect(await screen.findByRole("dialog", { name: "Start Alpha?" })).toBeVisible();
    expect(serverSpy).toHaveBeenCalledWith("server-1");
  });

  it("shows an error toast and no dialog for a missing server", async () => {
    const user = userEvent.setup();
    const missing = setup();
    missing.queryClient.setQueryData(hostingQueryKeys.fleet, []);
    missing.serverSpy.mockResolvedValue(undefined);
    await user.click(screen.getByRole("button", { name: "start" }));
    expect(
      await screen.findByText(
        "The servers could not be loaded. Check your connection and try again.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("shows an error toast and no dialog for an unknown action id", async () => {
    const { executeSpy } = setup();
    await userEvent.setup().click(screen.getByRole("button", { name: "unknown action" }));
    expect(
      await screen.findByText(
        "The servers could not be loaded. Check your connection and try again.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("executes once on confirm, then closes, toasts, invalidates and returns focus", async () => {
    const { executeSpy, queryClient } = setup();
    const invalidate = vi.spyOn(queryClient, "invalidateQueries");
    const user = userEvent.setup();
    const invoker = screen.getByRole("button", { name: "start" });
    await user.click(invoker);
    await user.click(await screen.findByRole("button", { name: "Start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(executeSpy).toHaveBeenCalledTimes(1);
    expect(executeSpy).toHaveBeenCalledWith("server-1", "start", undefined);
    expect(screen.getByText("Start requested for Alpha.")).toBeInTheDocument();
    const keys = invalidate.mock.calls.map((call) => call[0]?.queryKey);
    expect(keys).toEqual(
      expect.arrayContaining([
        hostingQueryKeys.fleet,
        hostingQueryKeys.server("server-1"),
        hostingQueryKeys.jobs("server-1"),
        hostingQueryKeys.operations("server-1"),
        hostingQueryKeys.notifications,
      ]),
    );
    // D-17: a submitted action never touches the cost alerts, which leave only through a dismissal.
    expect(keys).not.toContainEqual(hostingQueryKeys.costAlerts);
    await waitFor(() => expect(invoker).toHaveFocus());
  });

  it("keeps the dialog open on failure without rendering the raw error, then succeeds on retry", async () => {
    const { executeSpy } = setup();
    executeSpy.mockRejectedValueOnce(new Error("pq: relation internal_secret missing"));
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "start" }));
    await user.click(await screen.findByRole("button", { name: "Start server" }));
    const dialog = await screen.findByRole("dialog");
    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "The action could not be queued.",
    );
    expect(document.body).not.toHaveTextContent("internal_secret");
    expect(screen.getByRole("button", { name: "Start server" })).not.toHaveAttribute(
      "aria-disabled",
    );
    await user.click(screen.getByRole("button", { name: "Start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(executeSpy).toHaveBeenCalledTimes(2);
  });

  it("ignores cancel, close and dismiss while the request is pending", async () => {
    const { executeSpy } = setup();
    const pending = deferred<LifecycleJob>();
    executeSpy.mockReturnValue(pending.promise);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "start" }));
    await user.click(await screen.findByRole("button", { name: "Start server" }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => expect(dialog).toHaveAttribute("aria-busy", "true"));
    expect(screen.getByRole("button", { name: "Don't start server" })).toBeDisabled();

    const cancel = new Event("cancel", { cancelable: true });
    fireEvent(dialog, cancel);
    expect(cancel.defaultPrevented).toBe(true);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    const showModal = vi.spyOn(HTMLDialogElement.prototype, "showModal");
    (dialog as HTMLDialogElement).close();
    fireEvent(dialog, new Event("close"));
    expect(showModal).toHaveBeenCalled();
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    await act(async () => {
      pending.resolve(job);
      await pending.promise;
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it.each([false, true])(
    "opens one dialog for repeated requests (strict mode %s)",
    async (strict) => {
      setup({ strict });
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "request twice" }));
      await screen.findByRole("dialog");
      await user.click(screen.getByRole("button", { name: "start" }));
      expect(screen.getAllByRole("dialog")).toHaveLength(1);
    },
  );

  it("disables Confirm with the mapped reason when the live server makes the action unavailable", async () => {
    const { executeSpy, queryClient } = setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "start" }));
    await screen.findByRole("dialog");

    act(() => {
      queryClient.setQueryData(
        hostingQueryKeys.server("server-1"),
        makeServer([
          {
            ...startAction,
            enabled: false,
            unavailableReason: "Server launch is disabled.",
          } as ServerAction,
        ]),
      );
    });
    const confirm = await screen.findByRole("button", { name: "Start server" });
    await waitFor(() => expect(confirm).toHaveAttribute("aria-disabled", "true"));
    expect(screen.getByRole("dialog")).toHaveTextContent("Starting is turned off for this server.");
    await user.click(confirm);
    expect(executeSpy).not.toHaveBeenCalled();

    act(() => {
      queryClient.setQueryData(
        hostingQueryKeys.server("server-1"),
        makeServer([{ ...startAction, pending: true } as ServerAction]),
      );
    });
    await waitFor(() => expect(confirm).toHaveAttribute("aria-disabled", "true"));
    await user.click(confirm);
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("removes only the action parameter for the deep-link source", async () => {
    const { router } = setup({ route: "/servers/server-1?action=start&tab=ops" });
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "deep link" }));
    await user.click(await screen.findByRole("button", { name: "Don't start server" }));
    await waitFor(() => expect(router.state.location.search).toBe("?tab=ops"));

    await act(async () => {
      await router.navigate("/servers/server-1?action=start&tab=ops");
    });
    await user.click(screen.getByRole("button", { name: "deep link" }));
    await user.click(await screen.findByRole("button", { name: "Start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(router.state.location.search).toBe("?tab=ops");
  });

  it("leaves the URL alone for other sources", async () => {
    const { router } = setup({ route: "/servers/server-1?action=start&tab=ops" });
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "start" }));
    await user.click(await screen.findByRole("button", { name: "Don't start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(router.state.location.search).toBe("?action=start&tab=ops");
  });

  it("sends the typed challenge as the confirmation", async () => {
    const { executeSpy } = setup();
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "workspace-action terminate" }));
    await user.type(await screen.findByRole("textbox"), "Alpha");
    await user.click(screen.getByRole("button", { name: "Shut down without saving" }));
    await waitFor(() => expect(executeSpy).toHaveBeenCalledWith("server-1", "terminate", "Alpha"));
  });

  it("returns focus to the supplied element, else the heading, and closes on navigation", async () => {
    function FocusProbe() {
      const { request } = useCostConfirmation();
      const target = useRef<HTMLButtonElement>(null);
      const [invokerVisible, setInvokerVisible] = useState(true);
      return (
        <main>
          <h1 tabIndex={-1}>Server page</h1>
          <button ref={target} type="button">
            return target
          </button>
          <button type="button" onClick={() => request({ ...startRequest, returnFocus: target })}>
            with return focus
          </button>
          {invokerVisible && (
            <button
              type="button"
              onClick={() => {
                request(startRequest);
                setInvokerVisible(false);
              }}
            >
              vanishing invoker
            </button>
          )}
        </main>
      );
    }
    vi.spyOn(hostingService, "executeAction").mockResolvedValue(job);
    const view = renderWithProviders(<FocusProbe />);
    view.queryClient.setQueryData(hostingQueryKeys.fleet, [makeServer()]);
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "with return focus" }));
    await user.click(await screen.findByRole("button", { name: "Don't start server" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "return target" })).toHaveFocus(),
    );

    await user.click(screen.getByRole("button", { name: "vanishing invoker" }));
    await user.click(await screen.findByRole("button", { name: "Don't start server" }));
    await waitFor(() => expect(screen.getByRole("heading", { name: "Server page" })).toHaveFocus());

    await user.click(screen.getByRole("button", { name: "with return focus" }));
    await screen.findByRole("dialog");
    await act(async () => {
      await view.router.navigate("/elsewhere");
    });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("places focus again when the refreshed server replaces the control that asked", async () => {
    function ReplacedProbe() {
      const { request } = useCostConfirmation();
      const [pending, setPending] = useState(false);
      return (
        <main>
          <h1 tabIndex={-1}>Server page</h1>
          <button type="button" onClick={() => setPending(true)}>
            mark pending
          </button>
          {pending ? (
            <span>
              <button type="button" aria-disabled="true">
                In progress
              </button>
            </span>
          ) : (
            <button type="button" onClick={() => request(startRequest)}>
              asking control
            </button>
          )}
        </main>
      );
    }
    vi.spyOn(hostingService, "executeAction").mockResolvedValue(job);
    const view = renderWithProviders(<ReplacedProbe />);
    view.queryClient.setQueryData(hostingQueryKeys.fleet, [makeServer()]);
    const refreshed = deferred<void>();
    vi.spyOn(view.queryClient, "invalidateQueries").mockReturnValue(refreshed.promise);
    const user = userEvent.setup();

    const asking = screen.getByRole("button", { name: "asking control" });
    await user.click(asking);
    await user.click(await screen.findByRole("button", { name: "Start server" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(asking).toHaveFocus());

    // The refreshed server turns the control into its pending state: React mounts a new button and focus is lost.
    // fireEvent does not move focus the way a pointer click would, so the lost focus is the one under test.
    fireEvent.click(screen.getByRole("button", { name: "mark pending" }));
    expect(screen.getByRole("button", { name: "In progress" })).not.toHaveFocus();
    expect(document.body).toHaveFocus();

    await act(async () => {
      refreshed.resolve();
      await refreshed.promise;
    });
    await waitFor(() => expect(screen.getByRole("heading", { name: "Server page" })).toHaveFocus());
  });

  describe("onSubmitted", () => {
    function setupWithCallback(onSubmitted: () => void, second?: () => void) {
      return setup({}, makeServer(), {
        "start with callback": { ...startRequest, source: "fleet-card", onSubmitted },
        ...(second
          ? {
              "start with second callback": {
                ...startRequest,
                source: "fleet-card",
                onSubmitted: second,
              },
            }
          : {}),
      });
    }

    it("runs once after a successful action and before the first invalidation", async () => {
      const onSubmitted = vi.fn();
      const { executeSpy, queryClient } = setupWithCallback(onSubmitted);
      const invalidate = vi.spyOn(queryClient, "invalidateQueries");
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "start with callback" }));
      await user.click(await screen.findByRole("button", { name: "Start server" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

      expect(onSubmitted).toHaveBeenCalledTimes(1);
      const submittedAt = onSubmitted.mock.invocationCallOrder[0] ?? Number.NaN;
      expect(executeSpy.mock.invocationCallOrder[0]).toBeLessThan(submittedAt);
      expect(invalidate).toHaveBeenCalled();
      expect(submittedAt).toBeLessThan(invalidate.mock.invocationCallOrder[0] ?? Number.NaN);
    });

    it("does not run when the dialog is dismissed", async () => {
      const onSubmitted = vi.fn();
      setupWithCallback(onSubmitted);
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "start with callback" }));
      await user.click(await screen.findByRole("button", { name: "Don't start server" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

      expect(onSubmitted).not.toHaveBeenCalled();
    });

    it("does not run when the action is rejected", async () => {
      const onSubmitted = vi.fn();
      const { executeSpy } = setupWithCallback(onSubmitted);
      executeSpy.mockRejectedValueOnce(new Error("rejected"));
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "start with callback" }));
      await user.click(await screen.findByRole("button", { name: "Start server" }));
      expect(await within(await screen.findByRole("dialog")).findByRole("alert")).toBeVisible();

      expect(executeSpy).toHaveBeenCalledTimes(1);
      expect(onSubmitted).not.toHaveBeenCalled();
    });

    it("does not run when confirm is refused because the action became unavailable", async () => {
      const onSubmitted = vi.fn();
      const { executeSpy, queryClient } = setupWithCallback(onSubmitted);
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "start with callback" }));
      await screen.findByRole("dialog");
      act(() => {
        queryClient.setQueryData(
          hostingQueryKeys.server("server-1"),
          makeServer([{ ...startAction, enabled: false } as ServerAction]),
        );
      });
      const confirm = await screen.findByRole("button", { name: "Start server" });
      await waitFor(() => expect(confirm).toHaveAttribute("aria-disabled", "true"));
      await user.click(confirm);

      expect(executeSpy).not.toHaveBeenCalled();
      expect(onSubmitted).not.toHaveBeenCalled();
    });

    it("still toasts, invalidates and closes when the callback throws", async () => {
      const onSubmitted = vi.fn(() => {
        throw new Error("card went away");
      });
      const { queryClient } = setupWithCallback(onSubmitted);
      const invalidate = vi.spyOn(queryClient, "invalidateQueries");
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "start with callback" }));
      await user.click(await screen.findByRole("button", { name: "Start server" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

      expect(onSubmitted).toHaveBeenCalledTimes(1);
      expect(screen.getByText("Start requested for Alpha.")).toBeInTheDocument();
      expect(invalidate.mock.calls.map((call) => call[0]?.queryKey)).toEqual(
        expect.arrayContaining([
          hostingQueryKeys.fleet,
          hostingQueryKeys.server("server-1"),
          hostingQueryKeys.jobs("server-1"),
          hostingQueryKeys.operations("server-1"),
          hostingQueryKeys.notifications,
        ]),
      );
    });

    it("keeps the first request's callback when the same action is requested again", async () => {
      const first = vi.fn();
      const second = vi.fn();
      setupWithCallback(first, second);
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "start with callback" }));
      await screen.findByRole("dialog");
      await user.click(screen.getByRole("button", { name: "start with second callback" }));
      expect(screen.getAllByRole("dialog")).toHaveLength(1);
      await user.click(screen.getByRole("button", { name: "Start server" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

      expect(first).toHaveBeenCalledTimes(1);
      expect(second).not.toHaveBeenCalled();
    });
  });

  it("throws a clear error when the hook is used outside the provider", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    expect(() => render(<Probe requests={{}} />)).toThrow(
      "useCostConfirmation must be used inside CostConfirmationProvider.",
    );
  });
});
