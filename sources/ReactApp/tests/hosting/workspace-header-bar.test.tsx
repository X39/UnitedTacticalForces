import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { CostConfirmationContext } from "@/components/server/useCostConfirmation";
import { WorkspaceHeader } from "@/components/server/WorkspaceHeader";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

describe("WorkspaceHeader", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("holds the heading, the state words and one cost-stop action for a running server that may cost", () => {
    const server = fleetServer({
      title: "Operation Falcon",
      status: "Running",
      actions: [fleetAction("stop"), fleetAction("terminate", { dangerous: true })],
    });

    renderWithProviders(<WorkspaceHeader server={server} />);

    const heading = screen.getByRole("heading", { level: 1, name: "Operation Falcon" });
    const header = heading.closest("header");
    expect(header).not.toBeNull();
    expect(within(header as HTMLElement).getByText("Costing now")).toBeInTheDocument();
    const buttons = within(header as HTMLElement).getAllByRole("button");
    expect(buttons).toHaveLength(1);
    expect(buttons[0]).toHaveAccessibleName("Shut down and stop billing");
  });

  it("requests the shared confirmation with the workspace-header source and the stop action id", async () => {
    const request = vi.fn();
    const server = fleetServer({
      status: "Running",
      actions: [fleetAction("stop"), fleetAction("terminate", { dangerous: true })],
    });

    render(
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>
          <CostConfirmationContext.Provider value={{ request }}>
            <WorkspaceHeader server={server} />
          </CostConfirmationContext.Provider>
        </MemoryRouter>
      </I18nextProvider>,
    );
    await userEvent.click(screen.getByRole("button", { name: "Shut down and stop billing" }));

    expect(request).toHaveBeenCalledTimes(1);
    expect(request).toHaveBeenCalledWith({
      serverId: server.primaryKey,
      actionId: "stop",
      source: "workspace-header",
    });
  });

  it("opens the stop confirmation dialog without executing the action", async () => {
    const server = fleetServer({
      title: "Operation Falcon",
      status: "Running",
      actions: [fleetAction("stop"), fleetAction("terminate", { dangerous: true })],
    });
    const executeSpy = vi.spyOn(hostingService, "executeAction");

    const { queryClient } = renderWithProviders(<WorkspaceHeader server={server} />);
    queryClient.setQueryData(hostingQueryKeys.fleet, [server]);
    await userEvent.click(screen.getByRole("button", { name: "Shut down and stop billing" }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(executeSpy).not.toHaveBeenCalled();
  });

  it("offers Start server for an off server with exactly one button of that name", () => {
    const server = fleetServer({ status: "Stopped", actions: [fleetAction("start")] });

    renderWithProviders(<WorkspaceHeader server={server} />);

    const header = screen.getByRole("heading", { level: 1 }).closest("header") as HTMLElement;
    expect(within(header).getAllByRole("button", { name: "Start server" })).toHaveLength(1);
  });

  it("shows In progress, aria-disabled and the pending reason for a pending primary action", () => {
    const server = fleetServer({
      status: "Running",
      actions: [fleetAction("stop", { pending: true })],
    });

    renderWithProviders(<WorkspaceHeader server={server} />);

    const button = screen.getByRole("button", { name: "In progress" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(
      screen.getByText("Another change is still running. Try again when it finishes."),
    ).toBeInTheDocument();
  });

  it("shows only the title and the state for a server without actions", () => {
    const server = fleetServer({ status: "Stopped", actions: [] });

    renderWithProviders(<WorkspaceHeader server={server} />);

    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("keeps a 256-character name in full in the heading text", () => {
    const title = "N".repeat(256);
    const server = fleetServer({ title, status: "Stopped" });

    renderWithProviders(<WorkspaceHeader server={server} />);

    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading.textContent).toBe(title);
    expect(heading).not.toHaveAttribute("title");
  });
});
