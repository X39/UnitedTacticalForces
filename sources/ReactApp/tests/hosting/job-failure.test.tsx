import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { JobFailurePanel } from "@/components/server/JobFailurePanel";
import { operatePermissionReason } from "@/components/server/unavailableReasonMapping";
import { CostConfirmationContext } from "@/components/server/useCostConfirmation";
import { costSentenceKey, deriveFailure } from "@/features/hosting/jobFailure";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceCommand, workspaceJob } from "../helpers/workspaceFixtures";

/* Failure panel (WSP-05, D-09): what failed, whether it may still cost money, and the next step. */

const noop = () => undefined;

/** A failed job with the reason, the stage and the permissions the API would send. */
function failedJob(overrides: Partial<LifecycleJob> = {}): LifecycleJob {
  return workspaceJob({
    status: "Failed",
    stage: "provider-provisioning",
    failureCode: "provider-error",
    failureReason: "The provider rejected the request.",
    timeStampCompleted: "2026-08-13T12:04:00Z",
    ...overrides,
  });
}

/** A server that is costing money now and offers the graceful and the destructive cost-stop. */
function costlyServer(): GameServer {
  return fleetServer({
    status: "Running",
    actions: [fleetAction("stop"), fleetAction("terminate", { dangerous: true })],
  });
}

/** A server that does not cost money and offers only start. */
function freeServer(): GameServer {
  return fleetServer({ status: "Stopped", actions: [fleetAction("start")] });
}

function renderPanel(
  server: GameServer,
  jobs: LifecycleJob[],
  props: Partial<Parameters<typeof JobFailurePanel>[0]> = {},
) {
  return renderWithProviders(
    <JobFailurePanel
      server={server}
      jobs={jobs}
      busy={false}
      requestFailed={false}
      onRetry={noop}
      onSkip={noop}
      {...props}
    />,
  );
}

describe("deriveFailure", () => {
  it("returns null when a job is active, when the newest terminal job did not fail and without jobs", () => {
    const server = costlyServer();

    expect(deriveFailure(server, [failedJob(), workspaceJob({ status: "Running" })])).toBeNull();
    expect(
      deriveFailure(server, [
        failedJob({ timeStampCreated: "2026-08-13T12:00:00Z" }),
        workspaceJob({ status: "Succeeded", timeStampCreated: "2026-08-13T12:10:00Z" }),
      ]),
    ).toBeNull();
    expect(deriveFailure(server, [workspaceJob({ status: "Skipped" })])).toBeNull();
    expect(deriveFailure(server, [])).toBeNull();
    expect(deriveFailure(server, undefined)).toBeNull();
  });

  it("never reports a RetryPending job as a failure", () => {
    expect(deriveFailure(costlyServer(), [workspaceJob({ status: "RetryPending" })])).toBeNull();
  });

  it("returns the newest failed job, newest by creation time", () => {
    const newest = failedJob({ timeStampCreated: "2026-08-13T12:10:00Z" });
    const older = failedJob({ timeStampCreated: "2026-08-13T12:00:00Z" });

    expect(deriveFailure(costlyServer(), [older, newest])?.job).toBe(newest);
  });

  it("recommends stopping billing first, then retry, then asking the owner, then the details", () => {
    const retryable = failedJob({ canRetry: true });
    expect(deriveFailure(costlyServer(), [retryable])?.nextStep).toBe("stopBilling");
    expect(deriveFailure(freeServer(), [retryable])?.nextStep).toBe("retry");
    expect(deriveFailure(freeServer(), [failedJob({ canSkip: true })])?.nextStep).toBe("retry");

    const viewerServer = fleetServer({
      status: "Stopped",
      actions: [
        fleetAction("start", { enabled: false, unavailableReason: operatePermissionReason }),
      ],
    });
    expect(deriveFailure(viewerServer, [failedJob()])?.nextStep).toBe("askOwner");
    expect(deriveFailure(freeServer(), [failedJob()])?.nextStep).toBe("details");
  });

  it("offers no cost-stop when the server does not cost money", () => {
    const failure = deriveFailure(freeServer(), [failedJob()]);

    expect(failure?.cost).toBe("none");
    expect(failure?.costStop).toBeUndefined();
  });

  it("maps every cost to its sentence key", () => {
    expect(costSentenceKey("live")).toBe("Server_Failure_Cost_Live");
    expect(costSentenceKey("maybe")).toBe("Server_Failure_Cost_Maybe");
    expect(costSentenceKey("none")).toBe("Server_Failure_Cost_None");
  });
});

describe("JobFailurePanel", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders nothing while a job is active", () => {
    renderPanel(costlyServer(), [failedJob(), workspaceJob({ status: "Running" })]);

    expect(screen.queryByRole("region")).toBeNull();
    expect(screen.queryByRole("heading", { level: 2 })).toBeNull();
  });

  it("states what failed, that it still costs money and the stop-billing next step for a costly server", () => {
    renderPanel(costlyServer(), [failedJob()]);

    const region = screen.getByRole("region", { name: "“Start server” did not finish" });
    expect(within(region).getByRole("heading", { level: 2 })).toBeInTheDocument();
    expect(
      within(region).getByText("It stopped at: Preparing the cloud machine"),
    ).toBeInTheDocument();
    expect(within(region).getByText("The provider rejected the request.")).toHaveAttribute(
      "lang",
      "en",
    );
    expect(within(region).getByText("This server is still costing money.")).toBeInTheDocument();
    expect(within(region).getByText("Costing now")).toBeInTheDocument();
    expect(
      within(region).getByText(
        "Shut it down and stop billing so it stops costing money. You can try again afterwards.",
      ),
    ).toBeInTheDocument();
    expect(within(region).getAllByRole("button")[0]).toHaveAccessibleName(
      "Shut down and stop billing",
    );
    expect(region).not.toHaveAttribute("aria-live");
    expect(region).not.toHaveAttribute("role", "alert");
  });

  it("requests the shared confirmation for the stop action with the workspace-failure source", async () => {
    const request = vi.fn();
    const server = costlyServer();

    render(
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>
          <CostConfirmationContext.Provider value={{ request }}>
            <JobFailurePanel
              server={server}
              jobs={[failedJob()]}
              busy={false}
              requestFailed={false}
              onRetry={noop}
              onSkip={noop}
            />
          </CostConfirmationContext.Provider>
        </MemoryRouter>
      </I18nextProvider>,
    );
    await userEvent.click(screen.getByRole("button", { name: "Shut down and stop billing" }));

    expect(request).toHaveBeenCalledTimes(1);
    expect(request).toHaveBeenCalledWith({
      serverId: server.primaryKey,
      actionId: "stop",
      source: "workspace-failure",
    });
  });

  it("keeps the honest label when the only cost-stop is terminate", () => {
    const server = fleetServer({
      status: "Running",
      actions: [fleetAction("terminate", { dangerous: true })],
    });

    renderPanel(server, [failedJob()]);

    expect(screen.getByRole("button", { name: "Shut down without saving" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Shut down and stop billing" })).toBeNull();
  });

  it("shows retry and skip for a server without cost and calls the callbacks with the job id", async () => {
    const onRetry = vi.fn();
    const onSkip = vi.fn();
    const job = failedJob({ canRetry: true, canSkip: true });

    renderPanel(freeServer(), [job], { onRetry, onSkip });

    expect(screen.getByText("This server is not costing money.")).toBeInTheDocument();
    expect(
      screen.getByText("Try again, or skip it and continue if this change is not needed."),
    ).toBeInTheDocument();
    const retry = screen.getByRole("button", { name: "Try again" });
    const skip = screen.getByRole("button", { name: "Skip and continue" });
    expect(retry).toHaveClass("button--gold");
    expect(skip).toHaveClass("button--ghost");
    expect(screen.queryByRole("button", { name: /Shut down/ })).toBeNull();

    await userEvent.click(retry);
    await userEvent.click(skip);

    expect(onRetry).toHaveBeenCalledWith(job.primaryKey);
    expect(onSkip).toHaveBeenCalledWith(job.primaryKey);
  });

  it("makes the cost-stop the visual lead and retry a ghost button while cost may remain", () => {
    renderPanel(costlyServer(), [failedJob({ canRetry: true })]);

    expect(screen.getByRole("button", { name: "Shut down and stop billing" })).toHaveClass(
      "button--danger",
    );
    expect(screen.getByRole("button", { name: "Try again" })).toHaveClass("button--ghost");
  });

  it("disables retry and skip while a request is in flight without calling the callbacks", async () => {
    const onRetry = vi.fn();
    const onSkip = vi.fn();

    renderPanel(freeServer(), [failedJob({ canRetry: true, canSkip: true })], {
      busy: true,
      onRetry,
      onSkip,
    });
    const retry = screen.getByRole("button", { name: "Try again" });
    const skip = screen.getByRole("button", { name: "Skip and continue" });

    expect(retry).toHaveAttribute("aria-disabled", "true");
    expect(skip).toHaveAttribute("aria-disabled", "true");
    expect(screen.getAllByText("Sending your request.")).toHaveLength(2);
    await userEvent.click(retry);
    await userEvent.click(skip);
    expect(onRetry).not.toHaveBeenCalled();
    expect(onSkip).not.toHaveBeenCalled();
  });

  it("shows an empty alert slot until the request failed", () => {
    renderPanel(freeServer(), [failedJob({ canRetry: true })]);

    expect(screen.getByRole("alert")).toBeEmptyDOMElement();
  });

  it("shows the request error in the alert when the request failed", () => {
    renderPanel(freeServer(), [failedJob({ canRetry: true })], { requestFailed: true });

    expect(screen.getByRole("alert")).toHaveTextContent(
      "The request could not be sent. Try again, or refresh the server.",
    );
  });

  it("asks the owner and offers no retry or skip when the viewer lacks the operate permission", () => {
    const server = fleetServer({
      status: "Stopped",
      actions: [
        fleetAction("start", { enabled: false, unavailableReason: operatePermissionReason }),
      ],
    });

    renderPanel(server, [failedJob({ canRetry: false, canSkip: false })]);

    expect(
      screen.getByText("Ask the server owner or an administrator to try again."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Try again" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Skip and continue" })).toBeNull();
  });

  it("says no reason was reported and links to the job's activity entry", () => {
    const job = failedJob({
      failureReason: null,
      commands: [workspaceCommand({ status: "Failed" })],
    });

    renderPanel(freeServer(), [job]);

    expect(screen.getByText("No reason was reported.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open activity details" })).toHaveAttribute(
      "href",
      `/command/servers/${freeServer().primaryKey}?tab=activity#activity-job-${job.primaryKey}`,
    );
    expect(
      screen.getByText(
        "Open the activity details to see what happened. If it keeps failing, ask an administrator.",
      ),
    ).toBeInTheDocument();
  });
});
