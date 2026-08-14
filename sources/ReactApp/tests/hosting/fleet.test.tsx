import { act, render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerFleetView, orderFleetServers } from "@/features/hosting/FleetPages";
import type { GameServer, ServerAction } from "@/features/hosting/types";

function server(
  title: string,
  options: { possibleCost?: boolean; pendingJobs?: number; action?: ServerAction } = {},
): GameServer {
  return {
    primaryKey: `${title.toLowerCase().replaceAll(" ", "-")}-id`,
    title,
    gameIdentifier: "arma3",
    runtimeProviderIdentifier: "hetzner",
    costOwnerName: "Assigned owner",
    pendingJobCount: options.pendingJobs ?? 0,
    timeStampUpdated: "2026-08-13T12:30:00Z",
    actions: options.action ? [options.action] : [],
    deployment: {
      status: "Running",
      stage: "worker-online",
      possibleCost: options.possibleCost ?? false,
      resources: [],
    },
  } as unknown as GameServer;
}

function renderFleet(servers: GameServer[], canCreate = false) {
  return render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter>
        <ServerFleetView servers={servers} canCreate={canCreate} />
      </MemoryRouter>
    </I18nextProvider>,
  );
}

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

  it("renders the permission-sensitive empty state", () => {
    const { rerender } = renderFleet([], false);

    expect(screen.getByRole("heading", { name: "No game servers yet" })).toBeInTheDocument();
    expect(
      screen.getByText("You have not been granted access to a game server yet."),
    ).toBeVisible();
    expect(screen.queryByRole("link", { name: "Create server" })).not.toBeInTheDocument();

    rerender(
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>
          <ServerFleetView servers={[]} canCreate />
        </MemoryRouter>
      </I18nextProvider>,
    );
    expect(screen.getByRole("link", { name: "Create server" })).toHaveAttribute(
      "href",
      "/command/servers/create",
    );
  });

  it("localizes status and preserves the classic deprovision challenge deep link", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    const terminate = {
      id: "terminate",
      enabled: true,
      pending: false,
    } as ServerAction;

    renderFleet([server("UTF Eins", { possibleCost: true, pendingJobs: 2, action: terminate })]);

    expect(screen.getByText("Läuft")).toBeVisible();
    expect(screen.getByText("2 ausstehend")).toBeVisible();
    expect(
      screen.getByText("Diese Bereitstellung kann weiterhin Kosten verursachen."),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Jetzt deprovisionieren" })).toHaveAttribute(
      "href",
      "/command/servers/utf-eins-id?action=terminate",
    );
  });

  it("keeps a server-authoritative unavailable action disabled and localizes its reason", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    const terminate = {
      id: "terminate",
      enabled: false,
      pending: false,
      unavailableReason: "Server Operate permission is required.",
    } as ServerAction;

    renderFleet([server("UTF Zwei", { action: terminate })]);

    expect(screen.getByRole("button", { name: "Jetzt deprovisionieren" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Jetzt deprovisionieren" })).toHaveAttribute(
      "title",
      "Die Berechtigung „Server bedienen“ ist erforderlich.",
    );
  });
});
