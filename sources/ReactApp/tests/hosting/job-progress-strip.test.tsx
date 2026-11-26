import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { CostConfirmationContext } from "@/components/server/useCostConfirmation";
import type { LifecycleJob } from "@/features/hosting/types";
import { JobProgressStrip } from "@/components/server/JobProgressStrip";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import {
  workspaceAutomaticJob,
  workspaceCommand,
  workspaceJob,
} from "../helpers/workspaceFixtures";

/* Progress strip (WSP-04, D-08): a named stage, never a percentage. */

const server = fleetServer({ status: "Provisioning" });

/** Freezes the clock five minutes after the fixture job was created; each test restores it afterwards. */
function freezeClock(): void {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-08-13T12:05:00Z"));
}

describe("JobProgressStrip", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders nothing when no job is active", () => {
    freezeClock();
    renderWithProviders(
      <JobProgressStrip
        server={server}
        jobs={[workspaceJob({ status: "Succeeded" }), workspaceJob({ status: "Failed" })]}
      />,
    );

    expect(screen.queryByText("Change in progress")).toBeNull();
    expect(screen.queryByRole("region")).toBeNull();
  });

  it("renders nothing without loaded jobs", () => {
    freezeClock();
    renderWithProviders(<JobProgressStrip server={server} jobs={undefined} />);

    expect(screen.queryByText("Change in progress")).toBeNull();
  });

  it("shows the action, the current step, step states, start time, requester, lag note and details link", () => {
    freezeClock();
    const job = workspaceJob({
      kind: "Start",
      status: "Running",
      stage: "running",
      requestedByName: "Falcon One",
      commands: [workspaceCommand({ status: "Running" })],
    });

    renderWithProviders(<JobProgressStrip server={server} jobs={[job]} />);

    expect(screen.getByText("Change in progress")).toBeInTheDocument();
    const region = screen.getByRole("region", { name: /Start server/ });
    expect(within(region).getByText("Start server")).toBeInTheDocument();
    expect(within(region).getAllByText("Working on the server").length).toBeGreaterThan(0);

    const list = within(region).getByRole("list", { name: "Steps" });
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(5);
    items.slice(0, 3).forEach((item) => expect(item).toHaveTextContent("done"));
    expect(items[3]).toHaveAttribute("aria-current", "step");
    expect(items[3]).toHaveTextContent("current step");
    expect(items[4]).toHaveTextContent("not started");
    expect(items.filter((item) => item.hasAttribute("aria-current"))).toHaveLength(1);

    expect(within(region).getByText("Started 4 minutes ago")).toBeInTheDocument();
    expect(within(region).getByText(/Requested by/)).toHaveTextContent("Requested by Falcon One");
    expect(within(region).getByText("Falcon One").tagName).toBe("BDI");
    expect(
      within(region).getByText("Steps can take up to 30 seconds to update here."),
    ).toBeInTheDocument();
    expect(within(region).getByRole("link", { name: "Open activity details" })).toHaveAttribute(
      "href",
      `/command/servers/${server.primaryKey}?tab=activity#activity-job-${job.primaryKey}`,
    );
  });

  it("shows the trigger line instead of the requester for an automatic job", () => {
    freezeClock();
    const job = workspaceAutomaticJob("MaxRuntime", 6, {
      status: "Running",
      stage: "running",
      requestedByName: "Falcon One",
    });

    renderWithProviders(<JobProgressStrip server={server} jobs={[job]} />);

    const region = screen.getByRole("region", { name: /Shut down and stop billing/ });
    expect(
      within(region).getByText("Automatic: maximum runtime of 6 hours reached"),
    ).toBeInTheDocument();
    expect(within(region).queryByText(/Requested by/)).toBeNull();
    expect(within(region).getByText("Shut down and stop billing")).toBeInTheDocument();
  });

  it("keeps the requester for a job with an unknown trigger", () => {
    freezeClock();
    const job = workspaceJob({
      kind: "Stop",
      actionId: "stop",
      status: "Running",
      requestedByName: "Falcon One",
      trigger: "SomethingNew" as never,
      triggerLimit: 3,
    });

    renderWithProviders(<JobProgressStrip server={server} jobs={[job]} />);

    expect(screen.getByText(/Requested by/)).toHaveTextContent("Requested by Falcon One");
  });

  it("shows the request time while the job is still queued", () => {
    freezeClock();
    renderWithProviders(
      <JobProgressStrip
        server={server}
        jobs={[workspaceJob({ status: "Queued", stage: "", timeStampStarted: null })]}
      />,
    );

    expect(screen.getByText("Requested 5 minutes ago")).toBeInTheDocument();
    expect(screen.getAllByText("Waiting to begin").length).toBeGreaterThan(0);
  });

  it("never renders a percentage or a progress bar, whatever the milestone percent", () => {
    freezeClock();
    const { container } = renderWithProviders(
      <JobProgressStrip
        server={server}
        jobs={[workspaceJob({ progressPercent: 90, commands: [workspaceCommand()] })]}
      />,
    );

    expect(container.textContent).not.toContain("%");
    expect(screen.queryByRole("progressbar")).toBeNull();
    expect(container.querySelector("progress")).toBeNull();
  });

  it("names the platform when the job has no requester", () => {
    freezeClock();
    renderWithProviders(
      <JobProgressStrip server={server} jobs={[workspaceJob({ requestedByName: null })]} />,
    );

    expect(screen.getByText(/Requested by/)).toHaveTextContent("Requested by The platform");
  });

  it("falls back to the generic Working line with the raw stage for an unknown stage", () => {
    freezeClock();
    renderWithProviders(
      <JobProgressStrip
        server={server}
        jobs={[workspaceJob({ status: "Running", stage: "something-new" })]}
      />,
    );

    expect(screen.queryByRole("list", { name: "Steps" })).toBeNull();
    expect(screen.getByText("Working")).toBeInTheDocument();
    const stage = screen.getByText("something-new");
    expect(stage).toHaveAttribute("lang", "en");
  });

  describe("retry mode", () => {
    const retryJob = (overrides: Partial<LifecycleJob> = {}) =>
      workspaceJob({
        status: "RetryPending",
        stage: "retry-pending",
        failureReason: "The provider timed out.",
        ...overrides,
      });

    it("shows the warning tone, the retry body, the reason, the live cost sentence and the shut-down button", async () => {
      freezeClock();
      const request = vi.fn();
      const costly = fleetServer({
        status: "Running",
        actions: [fleetAction("stop"), fleetAction("terminate", { dangerous: true })],
      });

      render(
        <I18nextProvider i18n={i18n}>
          <MemoryRouter>
            <CostConfirmationContext.Provider value={{ request }}>
              <JobProgressStrip server={costly} jobs={[retryJob()]} />
            </CostConfirmationContext.Provider>
          </MemoryRouter>
        </I18nextProvider>,
      );

      const region = screen.getByRole("region");
      expect(region).toHaveClass("job-progress--warning");
      expect(within(region).getByText("Waiting to retry")).toBeInTheDocument();
      expect(
        within(region).getByText("A step did not work. It will be tried again automatically."),
      ).toBeInTheDocument();
      expect(within(region).getByText("The provider timed out.")).toHaveAttribute("lang", "en");
      expect(within(region).getByText("This server is still costing money.")).toBeInTheDocument();
      expect(within(region).queryByRole("list", { name: "Steps" })).toBeNull();

      await userEvent.click(
        within(region).getByRole("button", { name: "Shut down and stop billing" }),
      );
      expect(request).toHaveBeenCalledWith({
        serverId: costly.primaryKey,
        actionId: "stop",
        source: "workspace-failure",
      });
    });

    it("shows the wait line and no cost-stop button for a RetryPending Terminate job", () => {
      freezeClock();
      const costly = fleetServer({
        status: "Terminating",
        actions: [fleetAction("terminate", { dangerous: true, enabled: false })],
      });

      renderWithProviders(
        <JobProgressStrip server={costly} jobs={[retryJob({ kind: "Terminate" })]} />,
      );

      expect(
        screen.getByText(
          "Shutting down is retried automatically until the cloud machine is gone. No action is needed.",
        ),
      ).toBeInTheDocument();
      expect(screen.queryByRole("button")).toBeNull();
    });

    it("offers no cost-stop button when the server does not cost money", () => {
      freezeClock();
      const free = fleetServer({ status: "Stopped", actions: [fleetAction("start")] });

      renderWithProviders(<JobProgressStrip server={free} jobs={[retryJob()]} />);

      expect(screen.getByText("This server is not costing money.")).toBeInTheDocument();
      expect(screen.queryByRole("button")).toBeNull();
    });
  });

  describe("queue line", () => {
    it("shows nothing for one active job", () => {
      freezeClock();
      renderWithProviders(<JobProgressStrip server={server} jobs={[workspaceJob()]} />);

      expect(screen.queryByText(/more change/)).toBeNull();
    });

    it("uses the singular for one waiting change and the plural for more", () => {
      freezeClock();
      const jobs = [
        workspaceJob({ timeStampCreated: "2026-08-13T12:03:00Z" }),
        workspaceJob({ status: "Queued", timeStampCreated: "2026-08-13T12:02:00Z" }),
      ];
      const { unmount } = renderWithProviders(<JobProgressStrip server={server} jobs={jobs} />);

      expect(screen.getByText("1 more change is waiting.")).toBeInTheDocument();
      unmount();

      renderWithProviders(
        <JobProgressStrip
          server={server}
          jobs={[
            ...jobs,
            workspaceJob({ status: "Queued", timeStampCreated: "2026-08-13T12:01:00Z" }),
          ]}
        />,
      );
      expect(screen.getByText("2 more changes are waiting.")).toBeInTheDocument();
    });
  });
});
