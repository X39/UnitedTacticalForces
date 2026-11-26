import { act, render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { HostingOverviewView } from "@/features/hosting/FleetPages";
import { orderFleetServers } from "@/features/hosting/fleetModel";
import type { GameServer, ServerNotification } from "@/features/hosting/types";

function server(
  title: string,
  options: {
    possibleCost?: boolean;
    pendingJobs?: number;
    status?: string;
  } = {},
): GameServer {
  return {
    primaryKey: `${title.toLowerCase().replaceAll(" ", "-")}-id`,
    title,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "hetzner",
    costOwnerName: "Assigned owner",
    pendingJobCount: options.pendingJobs ?? 0,
    timeStampUpdated: "2026-08-13T12:30:00Z",
    actions: [],
    deployment: {
      status: options.status ?? "Running",
      stage: "worker-online",
      possibleCost: options.possibleCost ?? false,
      resources: [],
    },
  } as unknown as GameServer;
}

// Card, badge, cost, empty-state and terminate assertions of the removed fleet view now live in
// tests/hosting/fleet-card.test.tsx, tests/hosting/fleet-page.test.tsx and tests/hosting/member-access.test.tsx.
describe("certified hosting fleet", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("orders possible-cost and pending servers before the remaining fleet", () => {
    const ordered = orderFleetServers([
      server("Charlie"),
      server("Alpha", { pendingJobs: 1 }),
      server("Bravo", { possibleCost: true }),
    ]);

    expect(ordered.map((item) => item.title)).toEqual(["Bravo", "Alpha", "Charlie"]);
  });

  it("renders derived state in the overview attention list instead of the raw status label", () => {
    const stopped = server("UTF Neun", { status: "Stopped", possibleCost: true });
    const notification = {
      serverId: stopped.primaryKey,
      title: "UTF Neun",
      statusLabel: "RAW-STATUS-LABEL",
      possibleCost: true,
    } as unknown as ServerNotification;

    render(
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>
          <HostingOverviewView
            servers={[stopped]}
            notifications={[notification]}
            canViewFinance={false}
            canCreate={false}
          />
        </MemoryRouter>
      </I18nextProvider>,
    );

    expect(screen.getByText("Stop not confirmed")).toBeVisible();
    expect(screen.getByText("May still cost")).toBeVisible();
    expect(screen.queryByText("RAW-STATUS-LABEL")).not.toBeInTheDocument();
  });
});
