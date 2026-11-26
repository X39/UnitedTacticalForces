import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, within } from "@testing-library/react";
import type { ReactElement } from "react";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { serverCostFacts } from "@/components/server/costConfirmationModel";
import { CostStrip } from "@/components/server/CostStrip";
import { ServerEndpoint } from "@/components/server/ServerEndpoint";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import { hostingService } from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";
import { fleetServer } from "../helpers/fleetFixtures";
import { beforeEach } from "vitest";

/** Builds a server with explicit cost estimates (the fixture leaves them unset). */
function costServer(
  options: Parameters<typeof fleetServer>[0],
  estimates: { hourly: number | null; monthly: number | null },
): GameServer {
  return {
    ...fleetServer(options),
    estimatedHourlyCostEur: estimates.hourly,
    estimatedMonthlyCostEur: estimates.monthly,
  } as unknown as GameServer;
}

/** Builds a server whose deployment carries the given connection endpoint. */
function endpointServer(connectionEndpoint: string | null): GameServer {
  const server = fleetServer({ status: "Running" });
  return {
    ...server,
    deployment: server.deployment && { ...server.deployment, connectionEndpoint },
  } as GameServer;
}

function renderIn(ui: ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <MemoryRouter>{ui}</MemoryRouter>
      </I18nextProvider>
    </QueryClientProvider>,
  );
}

/** Returns the dd that follows the dt with the given text. */
function valueOf(label: string): HTMLElement {
  const dt = screen.getByText(label, { selector: "dt" });
  const dd = dt.nextElementSibling;
  if (!(dd instanceof HTMLElement)) throw new Error(`No value for ${label}`);
  return dd;
}

describe("CostStrip", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(() => {
    vi.spyOn(hostingService, "accruedCost").mockImplementation(
      () => new Promise<never>(() => undefined),
    );
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("shows owner, formatted rates and Costing now, never Not costing, for a running may-cost server", () => {
    const server = costServer(
      { status: "Running", costOwnerName: "Falcon One" },
      { hourly: 0.0123, monthly: 8.99 },
    );

    renderIn(<CostStrip server={server} />);

    expect(valueOf("Cost owner")).toHaveTextContent("Falcon One");
    expect(valueOf("Per hour")).toHaveTextContent(formatHourlyRate(0.0123));
    expect(valueOf("Per month if left running")).toHaveTextContent(formatMoney(8.99));
    expect(valueOf("Cost now")).toHaveTextContent("Costing now");
    expect(screen.queryByText("Not costing")).toBeNull();
  });

  it("always renders five rows", () => {
    renderIn(<CostStrip server={costServer({ status: "Running" }, { hourly: 1, monthly: 2 })} />);

    expect(screen.getAllByRole("term")).toHaveLength(5);
    expect(screen.getAllByRole("definition")).toHaveLength(5);
  });

  it("shows No estimate and no amount of zero when both estimates are missing", () => {
    renderIn(
      <CostStrip server={costServer({ status: "Stopped" }, { hourly: null, monthly: null })} />,
    );

    expect(valueOf("Per hour")).toHaveTextContent("No estimate");
    expect(valueOf("Per month if left running")).toHaveTextContent("No estimate");
    expect(valueOf("Per hour").textContent).not.toMatch(/\d/);
    expect(valueOf("Per month if left running").textContent).not.toMatch(/\d/);
  });

  it.each([
    ["Assigned owner", "Assigned owner"],
    ["Unknown owner", "Cost owner unknown"],
  ])("renders the localized text for the API placeholder %s", (placeholder, expected) => {
    renderIn(
      <CostStrip
        server={costServer(
          { status: "Running", costOwnerName: placeholder },
          { hourly: 1, monthly: 2 },
        )}
      />,
    );

    expect(valueOf("Cost owner")).toHaveTextContent(expected);
  });

  it("keeps the accrued value in the data-slot wrapper and shows no digit while it loads", () => {
    renderIn(<CostStrip server={costServer({ status: "Running" }, { hourly: 1, monthly: 2 })} />);

    const slot = valueOf("This session so far").querySelector('[data-slot="accrued-cost"]');
    expect(slot).not.toBeNull();
    expect(slot).toHaveTextContent("Loading cost so far...");
    expect(slot?.textContent).not.toMatch(/\d/);
    expect(screen.queryByText("Not available yet")).toBeNull();
  });

  it("switches the term to Last session total and shows the stopped line once cost has stopped", async () => {
    const stopped = costServer(
      { status: "Stopped", possibleCost: false },
      { hourly: 1, monthly: 2 },
    );
    vi.mocked(hostingService.accruedCost).mockResolvedValue({
      gameServerId: stopped.primaryKey,
      session: {
        deploymentId: "d1",
        amount: 4.2,
        currency: "EUR",
        asOf: "2026-11-21T14:05:00Z",
        isFinal: true,
        unavailableReason: null,
        billingUnitSeconds: null,
        billedThrough: null,
      },
    });

    renderIn(<CostStrip server={stopped} />);

    const slot = (await screen.findByText("Last session total", { selector: "dt" }))
      .nextElementSibling;
    expect(slot?.querySelector('[data-slot="accrued-cost"]')).not.toBeNull();
    expect(slot).toHaveTextContent(formatMoney(4.2));
    expect(slot).toHaveTextContent("Cost stopped:");
    expect(screen.queryByText("This session so far", { selector: "dt" })).toBeNull();
  });

  it("shows the billing line inside the accrued data-slot wrapper while cost runs and after it stopped", async () => {
    const running = costServer({ status: "Running" }, { hourly: 1, monthly: 2 });
    vi.mocked(hostingService.accruedCost).mockResolvedValue({
      gameServerId: running.primaryKey,
      session: {
        deploymentId: "d1",
        amount: 1.2,
        currency: "EUR",
        asOf: "2026-11-21T14:05:00Z",
        isFinal: false,
        unavailableReason: null,
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T14:42:00Z",
      },
    });

    const live = renderIn(<CostStrip server={running} />);

    const liveSlot = (
      await screen.findByText("This session so far", { selector: "dt" })
    ).nextElementSibling?.querySelector('[data-slot="accrued-cost"]');
    await within(liveSlot as HTMLElement).findByText(
      /^Billed per started hour\. The next hour starts at /,
    );
    live.unmount();

    const stopped = costServer(
      { status: "Stopped", possibleCost: false },
      { hourly: 1, monthly: 2 },
    );
    vi.mocked(hostingService.accruedCost).mockResolvedValue({
      gameServerId: stopped.primaryKey,
      session: {
        deploymentId: "d1",
        amount: 1.2,
        currency: "EUR",
        asOf: "2026-11-21T14:05:00Z",
        isFinal: true,
        unavailableReason: null,
        billingUnitSeconds: 3600,
        billedThrough: "2026-11-21T14:42:00Z",
      },
    });
    renderIn(<CostStrip server={stopped} />);

    const finalSlot = (
      await screen.findByText("Last session total", { selector: "dt" })
    ).nextElementSibling?.querySelector('[data-slot="accrued-cost"]');
    expect(
      await within(finalSlot as HTMLElement).findByText("Billed per started hour."),
    ).toBeInTheDocument();
    expect(finalSlot).not.toHaveTextContent("next hour");
  });

  it("shows No session yet for a server that never started, never the dash", async () => {
    const fresh = costServer({ noDeployment: true }, { hourly: 1, monthly: 2 });
    vi.mocked(hostingService.accruedCost).mockResolvedValue({
      gameServerId: fresh.primaryKey,
      session: null,
    });

    renderIn(<CostStrip server={fresh} />);

    expect(await screen.findByText("No session yet")).toBeInTheDocument();
    expect(valueOf("This session so far").textContent).not.toContain("—");
  });

  it("shows the same hourly and monthly values as the shared cost facts", () => {
    const server = costServer({ status: "Running" }, { hourly: 0.456, monthly: 12.5 });
    const facts = serverCostFacts(server);

    renderIn(<CostStrip server={server} />);

    expect(valueOf("Per hour")).toHaveTextContent(formatHourlyRate(facts.hourly ?? Number.NaN));
    expect(valueOf("Per month if left running")).toHaveTextContent(
      formatMoney(facts.monthly ?? Number.NaN),
    );
  });

  it("renders the German labels", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });

    renderIn(<CostStrip server={costServer({ status: "Running" }, { hourly: 1, monthly: 2 })} />);

    expect(screen.getByText("Kostenträger", { selector: "dt" })).toBeInTheDocument();
    expect(screen.getByText("Pro Stunde", { selector: "dt" })).toBeInTheDocument();
    expect(screen.getByText("Lade bisherige Kosten ...")).toBeInTheDocument();
  });
});

describe("CostStrip cost alerts row", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(() => {
    vi.spyOn(hostingService, "accruedCost").mockImplementation(
      () => new Promise<never>(() => undefined),
    );
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  /** Builds a running server with the given alert summary. */
  function alertServer(
    costAlertThresholds: number[] | null,
    canManageCostAlerts: boolean | null,
  ): GameServer {
    return costServer(
      { status: "Running", costAlertThresholds, canManageCostAlerts },
      { hourly: 1, monthly: 2 },
    );
  }

  it("renders no Cost alerts row when the summary is absent", () => {
    renderIn(<CostStrip server={alertServer(null, null)} />);

    expect(screen.queryByText("Cost alerts", { selector: "dt" })).toBeNull();
    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getAllByRole("term")).toHaveLength(5);
  });

  it("renders no row for a viewer without access even when the manage flag is set", () => {
    renderIn(<CostStrip server={alertServer(null, true)} />);

    expect(screen.queryByText("Cost alerts", { selector: "dt" })).toBeNull();
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("reads None set and offers Set an alert to a manager when no amount is set", () => {
    const server = alertServer([], true);

    renderIn(<CostStrip server={server} />);

    expect(valueOf("Cost alerts")).toHaveTextContent("None set");
    const link = screen.getByRole("link", { name: "Set an alert" });
    expect(link).toHaveAttribute(
      "href",
      `/command/servers/cost-alerts?owner=${server.costOwnerId}`,
    );
    expect(link).toHaveClass("button--ghost");
  });

  it("reads None set without a link for a viewer who may see but not manage", () => {
    renderIn(<CostStrip server={alertServer([], false)} />);

    expect(valueOf("Cost alerts")).toHaveTextContent("None set");
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("lists the amounts in the money format and offers Manage alerts to a manager", () => {
    const server = alertServer([5, 10], true);

    renderIn(<CostStrip server={server} />);

    const unit = new Intl.ListFormat("en-US", { type: "unit" }).format([
      formatMoney(5),
      formatMoney(10),
    ]);
    expect(valueOf("Cost alerts")).toHaveTextContent(unit);
    expect(screen.getByRole("link", { name: "Manage alerts" })).toHaveAttribute(
      "href",
      `/command/servers/cost-alerts?owner=${server.costOwnerId}`,
    );
    expect(screen.queryByText("None set")).toBeNull();
  });

  it("lists the amounts without a link when the viewer may not manage them", () => {
    renderIn(<CostStrip server={alertServer([5, 10], false)} />);

    expect(valueOf("Cost alerts")).toHaveTextContent(formatMoney(5));
    expect(screen.queryByRole("link")).toBeNull();
  });

  /** Builds a server whose rights and archive state the cost owner link depends on. */
  function ownerLinkServer(canModify: boolean, isArchived: boolean): GameServer {
    return {
      ...costServer({ status: "Running", costOwnerName: "Falcon One" }, { hourly: 1, monthly: 2 }),
      canModify,
      isArchived,
    } as unknown as GameServer;
  }

  it("ends the cost owner row with Change cost owner for a modifier of a live server", () => {
    renderIn(<CostStrip server={ownerLinkServer(true, false)} />);

    const link = within(valueOf("Cost owner")).getByRole("link", { name: "Change cost owner" });
    expect(link).toHaveAttribute("href", "/?tab=settings#server-cost-owner");
    expect(valueOf("Cost owner").querySelector("bdi")?.textContent).toBe("Falcon One");
  });

  it("offers no Change cost owner link without canModify", () => {
    renderIn(<CostStrip server={ownerLinkServer(false, false)} />);

    expect(screen.queryByRole("link", { name: "Change cost owner" })).toBeNull();
    expect(valueOf("Cost owner")).toHaveTextContent("Falcon One");
  });

  it("offers no Change cost owner link for an archived server", () => {
    renderIn(<CostStrip server={ownerLinkServer(true, true)} />);

    expect(screen.queryByRole("link", { name: "Change cost owner" })).toBeNull();
  });

  it("lists three amounts in full without the more text", () => {
    renderIn(<CostStrip server={alertServer([1, 2, 3], true)} />);

    const row = valueOf("Cost alerts");
    expect(row).toHaveTextContent(formatMoney(3));
    expect(row.textContent).not.toContain("more");
  });

  it("shows the first three of five amounts and and 2 more", () => {
    renderIn(<CostStrip server={alertServer([1, 2, 3, 4, 5], true)} />);

    const row = valueOf("Cost alerts");
    expect(row).toHaveTextContent(formatMoney(3));
    expect(row).toHaveTextContent("and 2 more");
    expect(row.textContent).not.toContain(formatMoney(4));
    expect(row.textContent).not.toContain(formatMoney(5));
  });

  it("puts the Cost alerts row last, after the accrued row", () => {
    renderIn(<CostStrip server={alertServer([5], true)} />);

    const terms = screen.getAllByRole("term").map((term) => term.textContent);
    expect(terms).toEqual([
      "Cost owner",
      "Per hour",
      "Per month if left running",
      "Cost now",
      "This session so far",
      "Cost alerts",
    ]);
  });

  it("renders the German row with the German money format and link", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });

    renderIn(<CostStrip server={alertServer([5, 10, 20, 50], true)} />);

    expect(screen.getByText("Kostenwarnungen", { selector: "dt" })).toBeInTheDocument();
    const row = valueOf("Kostenwarnungen");
    // The German money format holds a no-break space, which toHaveTextContent would normalize only in the actual.
    expect(row.textContent).toContain(formatMoney(5));
    expect(row).toHaveTextContent("und 1 weitere");
    expect(screen.getByRole("link", { name: "Warnungen verwalten" })).toBeInTheDocument();
  });

  it("renders the German empty row with the set link", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });

    renderIn(<CostStrip server={alertServer([], true)} />);

    expect(valueOf("Kostenwarnungen")).toHaveTextContent("Keine eingerichtet");
    expect(screen.getByRole("link", { name: "Warnung einrichten" })).toBeInTheDocument();
  });
});

describe("ServerEndpoint", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  it("shows the endpoint in a code element and a Copy address button when it is present", () => {
    renderIn(<ServerEndpoint server={endpointServer("203.0.113.7:2302")} />);

    const code = screen.getByText("203.0.113.7:2302");
    expect(code.tagName).toBe("CODE");
    expect(screen.getByRole("button", { name: "Copy address" })).toBeInTheDocument();
    expect(screen.queryByText("Available while the server is running.")).toBeNull();
  });

  it("shows the running-only line and no button when the endpoint is absent", () => {
    renderIn(<ServerEndpoint server={endpointServer(null)} />);

    expect(screen.getByText("Available while the server is running.")).toBeInTheDocument();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("decides only by the field: a stopped server with an endpoint still shows it", () => {
    const server = {
      ...fleetServer({ status: "Stopped" }),
    } as GameServer;
    const withEndpoint = {
      ...server,
      deployment: server.deployment && { ...server.deployment, connectionEndpoint: "host:1" },
    } as GameServer;

    const { container } = renderIn(<ServerEndpoint server={withEndpoint} />);

    expect(within(container).getByText("host:1")).toBeInTheDocument();
  });

  it("shows the absent line for a server without a deployment", () => {
    renderIn(<ServerEndpoint server={fleetServer({ noDeployment: true })} />);

    expect(screen.getByText("Available while the server is running.")).toBeInTheDocument();
  });
});
