import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerActions } from "@/features/hosting/components";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob, ServerAction } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

const serverId = "server-1";

const terminate = {
  id: "terminate",
  label: "Terminate now",
  description: "Release all provider resources.",
  enabled: true,
  unavailableReason: null,
  dangerous: true,
  pending: false,
  order: 1,
  confirmationChallenge: "TERMINATE",
  confirmationText: "This removes the deployment.",
  costImpact: "StopsCost",
  costDescription: "Stops possible provider cost.",
  expectedDurationSeconds: null,
} as ServerAction;

function makeServer(actions: ServerAction[]): GameServer {
  return {
    primaryKey: serverId,
    title: "UTF One",
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: null,
    costOwnerName: "Operations",
    isLaunchDisabled: false,
    deployment: null,
    actions,
    pendingJobCount: 0,
  } as unknown as GameServer;
}

function renderActions(actions: ServerAction[]) {
  const server = makeServer(actions);
  vi.spyOn(hostingService, "server").mockResolvedValue(server);
  const executeSpy = vi
    .spyOn(hostingService, "executeAction")
    .mockResolvedValue({ primaryKey: "job-1" } as LifecycleJob);
  const view = renderWithProviders(<ServerActions server={server} actions={actions} />);
  view.queryClient.setQueryData(hostingQueryKeys.fleet, [server]);
  return { ...view, executeSpy };
}

describe("workspace lifecycle actions", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("opens the shared confirmation without executing and gates the challenge", async () => {
    const { executeSpy } = renderActions([terminate]);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Shut down without saving" }));
    const dialog = await screen.findByRole("dialog");
    expect(executeSpy).not.toHaveBeenCalled();

    const confirm = within(dialog).getByRole("button", { name: "Shut down without saving" });
    expect(confirm).toHaveAttribute("aria-disabled", "true");
    const input = within(dialog).getByRole("textbox");
    await user.type(input, "terminate");
    await user.click(confirm);
    expect(executeSpy).not.toHaveBeenCalled();

    await user.clear(input);
    await user.type(input, "TERMINATE");
    await waitFor(() => expect(confirm).not.toHaveAttribute("aria-disabled", "true"));
    await user.click(confirm);
    await waitFor(() => expect(executeSpy).toHaveBeenCalledTimes(1));
    expect(executeSpy).toHaveBeenCalledWith(serverId, "terminate", "TERMINATE");
  });

  it("shows the API description as English text and a confirm variant from the action id", () => {
    renderActions([terminate]);
    const description = screen.getByText("Release all provider resources.");
    expect(description.closest("[lang='en']")).not.toBeNull();
    expect(screen.getByRole("button", { name: "Shut down without saving" })).toHaveClass(
      "button--danger",
    );
  });

  it("renders an unknown action with its API label in an English element", () => {
    renderActions([{ ...terminate, id: "custom-thing", label: "Rotate keys" } as ServerAction]);
    const label = screen.getAllByText("Rotate keys")[0]!;
    expect(label.closest("[lang='en']")).not.toBeNull();
    // The grid never uses gold: the header carries the one gold main action of the view (XPF-70-04-1).
    const button = screen.getByRole("button", { name: "Rotate keys" });
    expect(button).toHaveClass("button--primary");
    expect(button).not.toHaveClass("button--gold");
  });

  it("marks an unavailable action aria-disabled with a visible mapped reason and opens nothing", async () => {
    const { executeSpy } = renderActions([
      { ...terminate, enabled: false, unavailableReason: "Server Operate permission is required." },
    ]);
    const button = screen.getByRole("button", { name: "Shut down without saving" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(
      screen.getByText(
        "You need permission to operate this server. Ask the server owner or an administrator.",
      ),
    ).toBeVisible();
    await userEvent.setup().click(button);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("explains the operator pause in English and German and opens nothing", async () => {
    const paused = {
      ...terminate,
      enabled: false,
      unavailableReason: "Hosting lifecycle execution is paused by the operator.",
    } as ServerAction;
    const { executeSpy, unmount } = renderActions([paused]);
    const button = screen.getByRole("button", { name: "Shut down without saving" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText(/Queued work is preserved/)).toBeVisible();
    await userEvent.setup().click(button);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(executeSpy).not.toHaveBeenCalled();
    unmount();

    await i18n.changeLanguage("de");
    renderActions([paused]);
    expect(screen.getByText(/Ausstehende Aufträge bleiben erhalten/)).toBeVisible();
    expect(screen.getByRole("button", { name: "Ohne Speichern herunterfahren" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
  });

  it("shows the in-progress label and the pending reason for a pending action", () => {
    renderActions([{ ...terminate, enabled: true, pending: true } as ServerAction]);
    const button = screen.getByRole("button", { name: "In progress" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(
      screen.getByText("Another change is still running. Try again when it finishes."),
    ).toBeVisible();
  });
});
