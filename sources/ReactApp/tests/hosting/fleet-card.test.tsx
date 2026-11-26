import { readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { FleetSection } from "@/components/server/FleetSection";
import { FleetServerCard } from "@/components/server/FleetServerCard";
import {
  deploymentStateReason,
  operatePermissionReason,
} from "@/components/server/unavailableReasonMapping";
import {
  CostConfirmationContext,
  type CostConfirmationRequest,
} from "@/components/server/useCostConfirmation";
import type { GameServer } from "@/features/hosting/types";
import { findStyleViolations } from "../helpers/cssGuard";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";

const serverDirectory = resolve(import.meta.dirname, "..", "..", "src", "components", "server");

function readStylesheet(name: string): string {
  return readFileSync(join(serverDirectory, name), "utf8");
}

async function switchLanguage(language: "en" | "de"): Promise<void> {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

/** Renders one card inside the providers it needs and returns the spies and the card article. */
function renderCard(server: GameServer, { submitted = false }: { submitted?: boolean } = {}) {
  const request = vi.fn<(value: CostConfirmationRequest) => void>();
  const onSubmitted = vi.fn<(serverId: string) => void>();
  render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter>
        <CostConfirmationContext.Provider value={{ request }}>
          <FleetServerCard server={server} submitted={submitted} onSubmitted={onSubmitted} />
        </CostConfirmationContext.Provider>
      </MemoryRouter>
    </I18nextProvider>,
  );
  const card = screen.getByRole("article", { name: server.title });
  return { request, onSubmitted, card, user: userEvent.setup() };
}

function stageLine(card: HTMLElement): HTMLElement | null {
  return card.querySelector<HTMLElement>(".fleet-card__stage");
}

function ownerValue(card: HTMLElement): string {
  const value = card.querySelector(".fleet-card__facts dd");
  if (!value) throw new Error("The cost owner row is missing.");
  return value.textContent;
}

describe("FleetServerCard", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await switchLanguage("en");
  });

  it("renders an off server with a gold Start server that only requests the confirmation", async () => {
    const server = fleetServer({ title: "Altis Ops" });
    const { card, request, onSubmitted, user } = renderCard(server);

    expect(within(card).getByRole("heading", { level: 3, name: "Altis Ops" })).toBeInTheDocument();
    expect(within(card).getByText("arma3")).toBeInTheDocument();
    expect(within(card).getByText("Off")).toBeInTheDocument();
    expect(within(card).getByText("Not costing")).toBeInTheDocument();
    expect(within(card).getByText("Cost owner")).toBeInTheDocument();
    expect(ownerValue(card)).toBe("Operations");

    const start = within(card).getByRole("button", { name: "Start server" });
    expect(start).toHaveClass("button--gold");
    expect(start).not.toHaveAttribute("aria-disabled");
    const open = within(card).getByRole("link", { name: "Open server" });
    expect(open).toHaveClass("button--ghost");

    await user.click(start);

    expect(request).toHaveBeenCalledTimes(1);
    const call = request.mock.calls[0]?.[0];
    expect(call).toEqual({
      serverId: server.primaryKey,
      actionId: "start",
      source: "fleet-card",
      onSubmitted: expect.any(Function),
    });
    call?.onSubmitted?.();
    expect(onSubmitted).toHaveBeenCalledWith(server.primaryKey);
  });

  it("renders a danger stop for a running server, disabled with its reason when the API disables it", async () => {
    const enabled = renderCard(fleetServer({ index: 1, status: "Running" }));
    const stop = within(enabled.card).getByRole("button", { name: "Shut down and stop billing" });
    expect(stop).toHaveClass("button--danger");
    expect(stop).not.toHaveAttribute("aria-disabled");

    const server = fleetServer({
      index: 2,
      status: "Running",
      actions: [
        fleetAction("stop", { enabled: false, unavailableReason: operatePermissionReason }),
      ],
    });
    const { card, request, user } = renderCard(server);
    const disabled = within(card).getByRole("button", { name: "Shut down and stop billing" });
    expect(disabled).toHaveClass("button--danger");
    expect(disabled).toHaveAttribute("aria-disabled", "true");
    expect(disabled).not.toBeDisabled();
    const reason = i18n.t("Server_Reason_Operate");
    expect(within(card).getByText(reason)).toBeVisible();
    expect(disabled).toHaveAccessibleDescription(expect.stringContaining(server.title));
    expect(disabled).toHaveAccessibleDescription(expect.stringContaining(reason));

    disabled.focus();
    expect(disabled).toHaveFocus();
    await user.click(disabled);
    await user.keyboard("{Enter}");
    expect(request).not.toHaveBeenCalled();
  });

  it.each([
    ["starting", "Provisioning", 0],
    ["stopping", "Stopping", 0],
    ["working", undefined, 1],
  ] as const)(
    "renders a disabled ghost In progress control while %s",
    (_state, status, pendingJobCount) => {
      const server = fleetServer({
        status,
        pendingJobCount,
        actions: [
          fleetAction("stop", { enabled: false, unavailableReason: deploymentStateReason }),
        ],
      });
      const { card } = renderCard(server);
      const control = within(card).getByRole("button", { name: "In progress" });
      expect(control).toHaveClass("button--ghost");
      expect(control).toHaveAttribute("aria-disabled", "true");
      expect(control).toHaveAccessibleDescription(expect.stringContaining(server.title));
      if (pendingJobCount > 0) {
        expect(within(card).getByText(i18n.t("Server_Reason_Pending"))).toBeVisible();
      } else {
        const stateLabel = i18n.t(
          status === "Provisioning" ? "Server_State_Starting" : "Server_State_Stopping",
        );
        expect(
          within(card).getByText(i18n.t("Server_Reason_State", { state: stateLabel })),
        ).toBeVisible();
      }
    },
  );

  it("shows the pending sentence for a starting server with a pending job", () => {
    const { card } = renderCard(fleetServer({ status: "Provisioning", pendingJobCount: 2 }));
    expect(within(card).getByText(i18n.t("Server_Reason_Pending"))).toBeVisible();
  });

  it.each([
    ["attention", "Failed", true],
    ["unconfirmed", "Stopped", true],
  ] as const)(
    "shows a disabled stop with the state reason for an %s server and never a teardown control",
    (_state, status, possibleCost) => {
      const server = fleetServer({
        status,
        possibleCost,
        actions: [
          fleetAction("stop", { enabled: false, unavailableReason: deploymentStateReason }),
          fleetAction("terminate"),
        ],
      });
      const { card } = renderCard(server);
      const stop = within(card).getByRole("button", { name: "Shut down and stop billing" });
      expect(stop).toHaveAttribute("aria-disabled", "true");
      expect(stop).toHaveClass("button--danger");
      const stateLabel = i18n.t(
        status === "Failed" ? "Server_State_Attention" : "Server_State_Unconfirmed",
      );
      expect(
        within(card).getByText(i18n.t("Server_Reason_State", { state: stateLabel })),
      ).toBeVisible();
      expect(within(card).getByRole("link", { name: "Open server" })).toBeInTheDocument();

      expect(within(card).getAllByRole("button")).toHaveLength(1);
      expect(within(card).queryByText(i18n.t("Server_Action_Terminate_Label"))).toBeNull();
      for (const link of within(card).getAllByRole("link")) {
        expect(link.getAttribute("href") ?? "").not.toContain("action=terminate");
      }
    },
  );

  it.each([
    ["an off server without a start action", fleetServer({ actions: [fleetAction("terminate")] })],
    [
      "an attention server without a stop action",
      fleetServer({ status: "Failed", actions: [fleetAction("terminate")] }),
    ],
    ["an unknown state", fleetServer({ status: "Rebalancing", actions: [fleetAction("stop")] })],
  ])("renders only a primary Open server link for %s", (_case, server) => {
    const { card } = renderCard(server);
    expect(within(card).queryAllByRole("button")).toHaveLength(0);
    const links = within(card).getAllByRole("link");
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAccessibleName("Open server");
    expect(links[0]).toHaveClass("button--primary");
    expect(links[0]?.getAttribute("href") ?? "").not.toContain("action=terminate");
  });

  it("shows a disabled In progress control with the request-sent reason after submission", async () => {
    const server = fleetServer({ status: "Running" });
    const { card, request, user } = renderCard(server, { submitted: true });
    const control = within(card).getByRole("button", { name: "In progress" });
    expect(control).toHaveAttribute("aria-disabled", "true");
    expect(control).toHaveClass("button--ghost");
    expect(
      within(card).getByText("Request sent. This card updates as soon as the server changes."),
    ).toBeVisible();
    expect(control).toHaveAccessibleDescription(expect.stringContaining(server.title));
    expect(within(card).getByText("Running")).toBeInTheDocument();
    expect(within(card).queryByRole("button", { name: "Shut down and stop billing" })).toBeNull();

    await user.click(control);
    expect(request).not.toHaveBeenCalled();
  });

  it("shows the stage line only while starting, stopping or working", () => {
    const starting = renderCard(fleetServer({ index: 1, status: "Provisioning" }));
    expect(stageLine(starting.card)).toHaveTextContent(
      i18n.t("Hosting_DeploymentStatus_Provisioning"),
    );
    const stopping = renderCard(fleetServer({ index: 2, status: "Stopping" }));
    expect(stageLine(stopping.card)).toHaveTextContent(i18n.t("Hosting_DeploymentStatus_Stopping"));
    const working = renderCard(fleetServer({ index: 3, pendingJobCount: 1 }));
    expect(stageLine(working.card)).toHaveTextContent("A queued change is running.");

    const quiet = [
      fleetServer({ index: 4 }),
      fleetServer({ index: 5, status: "Running" }),
      fleetServer({ index: 6, status: "Failed" }),
      fleetServer({ index: 7, status: "Stopped", possibleCost: true }),
    ];
    for (const server of quiet) {
      const { card } = renderCard(server);
      expect(stageLine(card)).toBeNull();
    }
  });

  it("renders owner placeholders as localized phrases and long titles in full", async () => {
    const hidden = renderCard(fleetServer({ index: 1, costOwnerName: "Assigned owner" }));
    expect(ownerValue(hidden.card)).toBe(i18n.t("Server_Confirm_OwnerHidden"));
    const unknown = renderCard(fleetServer({ index: 2, costOwnerName: "Unknown owner" }));
    expect(ownerValue(unknown.card)).toBe("Cost owner unknown");
    const empty = renderCard(fleetServer({ index: 3, costOwnerName: "", noDeployment: true }));
    expect(ownerValue(empty.card)).toBe("Cost owner unknown");
    const named = renderCard(fleetServer({ index: 4, costOwnerName: "Jäger <b>Unit</b>" }));
    expect(ownerValue(named.card)).toBe("Jäger <b>Unit</b>");

    const title = "A".repeat(200) + " " + "B".repeat(55);
    expect(title).toHaveLength(256);
    const long = renderCard(fleetServer({ index: 5, title }));
    expect(within(long.card).getByRole("heading", { level: 3 }).textContent).toBe(title);

    await switchLanguage("de");
    expect(ownerValue(hidden.card)).toBe("Zugewiesener Kostenträger");
    expect(ownerValue(unknown.card)).toBe("Kostenträger unbekannt");
  });

  it("labels the article by its heading and links Open server to the encoded server id", () => {
    const server = { ...fleetServer({ status: "Running" }), primaryKey: "srv/1?ä" } as GameServer;
    const { card } = renderCard(server);
    expect(card).toHaveAttribute("data-server-card", "srv/1?ä");
    const heading = within(card).getByRole("heading", { level: 3 });
    expect(card).toHaveAttribute("aria-labelledby", heading.id);
    expect(heading.id).toBe("server-srv/1?ä-title");

    const open = within(card).getByRole("link", { name: "Open server" });
    expect(open).toHaveAttribute("data-server-id", "srv/1?ä");
    expect(open).toHaveAttribute("href", `/command/servers/${encodeURIComponent("srv/1?ä")}`);
    expect(open).toHaveAccessibleDescription(server.title);
    const stop = within(card).getByRole("button", { name: "Shut down and stop billing" });
    expect(stop).toHaveAccessibleDescription(server.title);
  });

  it("renders the primary labels and the link in German", async () => {
    await switchLanguage("de");
    const off = renderCard(fleetServer({ index: 1 }));
    expect(within(off.card).getByRole("button", { name: "Server starten" })).toBeInTheDocument();
    expect(within(off.card).getByRole("link", { name: "Server öffnen" })).toBeInTheDocument();
    const running = renderCard(fleetServer({ index: 2, status: "Running" }));
    expect(
      within(running.card).getByRole("button", { name: "Herunterfahren und Abrechnung beenden" }),
    ).toBeInTheDocument();
  });

  it("renders the unknown badge and only Open server for an unmapped deployment status", () => {
    const { card } = renderCard(fleetServer({ status: "SomeFutureStatus" }));
    expect(within(card).getByText("Status unknown")).toBeInTheDocument();
    expect(within(card).queryAllByRole("button")).toHaveLength(0);
    expect(within(card).getAllByRole("link")).toHaveLength(1);
    expect(within(card).getByRole("link", { name: "Open server" })).toBeInTheDocument();
  });
});

describe("FleetSection", () => {
  it("renders a labelled section with one list item per card", () => {
    render(
      <FleetSection title="Active (2)" emptyText="Nothing is running right now.">
        <p key="a">First</p>
        <p key="b">Second</p>
      </FleetSection>,
    );
    const section = screen.getByRole("region", { name: "Active (2)" });
    expect(within(section).getByRole("heading", { level: 2, name: "Active (2)" })).toBeVisible();
    const list = within(section).getByRole("list");
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("First");
    expect(items[1]).toHaveTextContent("Second");
    expect(within(section).queryByText("Nothing is running right now.")).toBeNull();
  });

  it("renders the empty line and no list without children", () => {
    render(<FleetSection title="Active (0)" emptyText="Nothing is running right now." />);
    const section = screen.getByRole("region", { name: "Active (0)" });
    expect(within(section).getByText("Nothing is running right now.")).toBeVisible();
    expect(within(section).queryByRole("list")).toBeNull();
  });
});

describe("fleet card stylesheet", () => {
  it("uses an auto-fill grid with a min() against the fleet card token and never truncates", () => {
    const css = readStylesheet("fleet-card.css");
    const grid = /grid-template-columns:\s*([^;]+);/.exec(css)?.[1] ?? "";
    expect(grid).toContain("repeat(auto-fill,");
    expect(grid).not.toContain("auto-fit");
    expect(grid).toMatch(/min\(100%,\s*var\(--utf-fleet-card-min\)\)/);
    expect(css).not.toMatch(/text-overflow|ellipsis/);
  });

  it("keeps fleet-card.css and fleet-toolbar.css in the directory the server style guard scans", () => {
    const fleetStylesheets = ["fleet-card.css", "fleet-toolbar.css"];
    const stylesheets = readdirSync(serverDirectory).filter((name) => name.endsWith(".css"));
    expect(stylesheets).toEqual(expect.arrayContaining(fleetStylesheets));
    for (const name of fleetStylesheets) {
      expect(findStyleViolations(readStylesheet(name))).toEqual([]);
    }
  });
});
