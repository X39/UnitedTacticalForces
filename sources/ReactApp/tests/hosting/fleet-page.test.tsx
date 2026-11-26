import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerFleetPage } from "@/features/hosting/FleetPages";
import { FleetLiveUpdatesContext } from "@/features/hosting/useFleetLiveUpdates";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

/*
 * Page-level behavior of the fleet at /command/servers (Plan 62-11): URL-backed search and filter, the cost line,
 * the Active and Off sections, the no-match and empty panels, quick start through the shared confirmation with the
 * submitted state, no polling, the delayed live notice, the polite announcements and the focus fallback.
 */

const authState = vi.hoisted(() => ({
  user: undefined as FullUser | undefined,
  isLoading: false,
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isAuthenticated: Boolean(authState.user),
    isLoading: authState.isLoading,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

/** The signed-in viewer's id; the fixtures' default creator is a different user. */
const meId = "a0000000-0000-4000-8000-00000000000a";

function userWithClaims(...identifiers: string[]): FullUser {
  return {
    primaryKey: meId,
    nickname: "Member",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: true,
    claims: identifiers.map((identifier, index) => ({
      primaryKey: index + 1,
      identifier,
      value: null,
      title: identifier,
      category: "Hosting",
      description: "",
      isPrefix: false,
      valueType: null,
    })),
    roles: [],
  } as unknown as FullUser;
}

/** An off server with an enabled start action, created by the viewer. */
function offServer(title = "Alpha", index = 1): GameServer {
  return fleetServer({ index, title, createdById: meId });
}

/** A running server that costs money now, with an enabled stop action. */
function runningServer(title = "Bravo", index = 2): GameServer {
  return fleetServer({ index, title, status: "Running" });
}

/** A stopped server whose stop is not confirmed, so it may still cost; the viewer pays for it. */
function maybeServer(title = "Charlie", index = 3): GameServer {
  return fleetServer({
    index,
    title,
    status: "Stopped",
    possibleCost: true,
    costOwnerUserId: meId,
  });
}

function standardFleet(): GameServer[] {
  return [offServer(), runningServer(), maybeServer()];
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

function renderPage(route = "/command/servers", ui: ReactElement = <ServerFleetPage />) {
  return renderWithProviders(ui, { route });
}

function searchBox(): HTMLInputElement {
  return screen.getByRole("searchbox", { name: "Search servers" }) as HTMLInputElement;
}

function chip(name: RegExp): HTMLElement {
  return within(screen.getByRole("group", { name: "Filter servers" })).getByRole("button", {
    name,
  });
}

function cardTitles(): string[] {
  return screen
    .queryAllByRole("article")
    .map((article) => within(article).getByRole("heading", { level: 3 }).textContent ?? "");
}

function sectionHeadings(): string[] {
  return screen.queryAllByRole("heading", { level: 2 }).map((heading) => heading.textContent ?? "");
}

function card(title: string): HTMLElement {
  return screen.getByRole("article", { name: title });
}

function search(router: { state: { location: { search: string } } }): URLSearchParams {
  return new URLSearchParams(router.state.location.search);
}

function politeRegion(container: HTMLElement): HTMLElement {
  const region = container.querySelector<HTMLElement>('.next-hosting-page > [aria-live="polite"]');
  expect(region).not.toBeNull();
  return region!;
}

async function useLanguage(language: "en" | "de") {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

describe("fleet page", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = userWithClaims("sv");
    authState.isLoading = false;
    vi.spyOn(hostingService, "notifications").mockResolvedValue([]);
  });

  afterEach(async () => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    await useLanguage("en");
  });

  describe("structure", () => {
    it.each([
      ["en", "Servers", ["Active (2)", "Off (1)"]],
      ["de", "Server", ["Aktiv (2)", "Aus (1)"]],
    ] as const)(
      "groups the fleet into Active then Off in %s",
      async (language, title, headings) => {
        await useLanguage(language);
        vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

        const { container } = renderPage();

        expect(await screen.findAllByRole("article")).toHaveLength(3);
        expect(screen.getByRole("heading", { level: 1, name: title })).toBeVisible();
        expect(sectionHeadings()).toEqual(headings);
        const active = screen.getByRole("region", { name: headings[0] });
        const off = screen.getByRole("region", { name: headings[1] });
        expect(
          within(active)
            .getAllByRole("heading", { level: 3 })
            .map((h) => h.textContent),
        ).toEqual(["Bravo", "Charlie"]);
        expect(
          within(off)
            .getAllByRole("heading", { level: 3 })
            .map((h) => h.textContent),
        ).toEqual(["Alpha"]);
        expect(off).toBeVisible();
        expect(container.querySelector("[aria-expanded]")).toBeNull();
      },
    );
  });

  describe("cost line and counts", () => {
    it("counts the whole fleet and keeps the count under any search or filter", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      const costLine = screen.getByRole("note");
      const text =
        "2 servers are costing money or may still cost money. Shut down servers you are not using.";
      expect(costLine).toHaveTextContent(text);
      expect(chip(/^All 3$/)).toHaveAttribute("aria-pressed", "true");
      expect(chip(/^Mine 2$/)).toBeVisible();
      expect(chip(/^Running 2$/)).toBeVisible();
      expect(chip(/^Off 1$/)).toBeVisible();

      await user.click(chip(/^Off 1$/));
      expect(screen.getByRole("note")).toHaveTextContent(text);

      await user.type(searchBox(), "Bravo");
      expect(screen.getByRole("note")).toHaveTextContent(text);
      expect(chip(/^All 1$/)).toBeVisible();
      expect(chip(/^Mine 0$/)).toBeVisible();
      expect(chip(/^Running 1$/)).toBeVisible();
      expect(chip(/^Off 0$/)).toBeVisible();
    });

    it("shows no cost line when no server costs", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([offServer(), offServer("Delta", 4)]);

      renderPage();

      await screen.findAllByRole("article");
      expect(screen.queryByRole("note")).not.toBeInTheDocument();
    });
  });

  describe("filters", () => {
    it("lists only costing servers under Running and renders Active only", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(chip(/^Running/));

      expect(chip(/^Running/)).toHaveAttribute("aria-pressed", "true");
      expect(chip(/^All/)).toHaveAttribute("aria-pressed", "false");
      expect(cardTitles()).toEqual(["Bravo", "Charlie"]);
      expect(sectionHeadings()).toEqual(["Active (2)"]);
      expect(screen.getByText("2 of 3 servers shown")).toBeVisible();
    });

    it("lists only off servers under Off and renders Off only", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(chip(/^Off/));

      expect(cardTitles()).toEqual(["Alpha"]);
      expect(sectionHeadings()).toEqual(["Off (1)"]);
      expect(screen.getByText("1 of 3 servers shown")).toBeVisible();
    });

    it("lists servers the viewer created or pays for under Mine", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(chip(/^Mine/));

      expect(cardTitles()).toEqual(["Charlie", "Alpha"]);
      expect(sectionHeadings()).toEqual(["Active (1)", "Off (1)"]);
      expect(screen.getByText("2 of 3 servers shown")).toBeVisible();
    });

    it("always renders Active in All with an empty line when nothing is active", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([offServer()]);

      renderPage();

      await screen.findAllByRole("article");
      expect(sectionHeadings()).toEqual(["Active (0)", "Off (1)"]);
      expect(screen.getByText("Nothing is running right now.")).toBeVisible();
    });
  });

  describe("search and URL state", () => {
    it("filters while typing and writes the trimmed text after 250 ms with replace", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      const { router } = renderPage("/command/servers?tab=keep");

      await screen.findAllByRole("article");
      await user.type(searchBox(), "  Bravo ");

      expect(cardTitles()).toEqual(["Bravo"]);
      expect(search(router).has("q")).toBe(false);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(250);
      });

      expect(search(router).get("q")).toBe("Bravo");
      expect(search(router).get("tab")).toBe("keep");
      expect(router.state.historyAction).toBe("REPLACE");
      expect(searchBox().value).toBe("  Bravo ");

      await user.clear(searchBox());

      await waitFor(() => expect(search(router).has("q")).toBe(false));
      expect(search(router).get("tab")).toBe("keep");
      expect(cardTitles()).toHaveLength(3);
    });

    it("writes a filter immediately with replace and keeps other parameters", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup();

      const { router } = renderPage("/command/servers?tab=keep");

      await screen.findAllByRole("article");
      await user.click(chip(/^Off/));

      expect(search(router).get("filter")).toBe("off");
      expect(search(router).get("tab")).toBe("keep");
      expect(router.state.historyAction).toBe("REPLACE");

      await user.click(chip(/^All/));
      expect(search(router).has("filter")).toBe(false);
    });

    it("restores the field and the view from a shared link", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage("/command/servers?q=Bravo&filter=running");

      await screen.findAllByRole("article");
      expect(searchBox().value).toBe("Bravo");
      expect(chip(/^Running/)).toHaveAttribute("aria-pressed", "true");
      expect(cardTitles()).toEqual(["Bravo"]);
    });

    it("shows All for an invalid filter value", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage("/command/servers?filter=everything");

      await screen.findAllByRole("article");
      expect(chip(/^All/)).toHaveAttribute("aria-pressed", "true");
      expect(cardTitles()).toHaveLength(3);
    });

    it("resets the field and the chips when the router navigates to the bare route", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      const { router } = renderPage("/command/servers?filter=running");

      await screen.findAllByRole("article");
      await user.type(searchBox(), "Bravo");
      await act(async () => {
        await vi.advanceTimersByTimeAsync(250);
      });
      expect(search(router).get("q")).toBe("Bravo");

      await act(async () => {
        await router.navigate("/command/servers");
      });

      expect(searchBox().value).toBe("");
      expect(chip(/^All/)).toHaveAttribute("aria-pressed", "true");
      expect(cardTitles()).toHaveLength(3);
    });
  });

  describe("search semantics", () => {
    function searchFleet(): GameServer[] {
      return [
        fleetServer({ index: 1, title: "Zoë Échelon", gameIdentifier: "arma3" }),
        fleetServer({ index: 2, title: "Night Ops", gameIdentifier: "dayz" }),
        fleetServer({ index: 3, title: "Training", costOwnerName: "Jürgen Müller" }),
        fleetServer({ index: 4, title: "Hidden", costOwnerName: "Assigned owner" }),
      ];
    }

    it.each([
      ["zoe echelon", ["Zoë Échelon"]],
      ["DAYZ", ["Night Ops"]],
      ["muller", ["Training"]],
      ["ops dayz", ["Night Ops"]],
      ["ops arma3", []],
      ["assigned", []],
    ] as const)("matches %j against title, game and cost owner", async (query, expected) => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(searchFleet());

      renderPage(`/command/servers?q=${encodeURIComponent(query)}`);

      if (expected.length > 0) {
        await screen.findAllByRole("article");
        expect(cardTitles()).toEqual(expected);
      } else {
        expect(await screen.findByRole("heading", { name: "No servers match" })).toBeVisible();
        expect(cardTitles()).toEqual([]);
      }
    });
  });

  describe("empty states", () => {
    it("replaces both sections with the no-match panel and clears search and filter", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup();

      const { router } = renderPage("/command/servers?q=nothing&filter=running&tab=keep");

      expect(await screen.findByRole("heading", { name: "No servers match" })).toBeVisible();
      expect(screen.getByText("Try a different search, or show all servers.")).toBeVisible();
      expect(sectionHeadings()).toEqual(["No servers match"]);

      await user.click(screen.getByRole("button", { name: "Clear search and filter" }));

      await waitFor(() => expect(cardTitles()).toHaveLength(3));
      expect(search(router).has("q")).toBe(false);
      expect(search(router).has("filter")).toBe(false);
      expect(search(router).get("tab")).toBe("keep");
      expect(searchBox().value).toBe("");
      expect(searchBox()).toHaveFocus();
    });

    it("shows the Mine copy for the Mine filter without search text", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([runningServer()]);

      renderPage("/command/servers?filter=mine");

      expect(
        await screen.findByRole("heading", { name: "No servers of your own yet" }),
      ).toBeVisible();
      expect(screen.getByText("Servers you own or pay for appear here.")).toBeVisible();
      expect(screen.queryByText("No servers match")).not.toBeInTheDocument();
    });

    it("shows the creator panel with the only, gold, create action and no toolbar", async () => {
      authState.user = userWithClaims("crt:sv");
      vi.spyOn(hostingService, "servers").mockResolvedValue([]);

      const { container } = renderPage();

      expect(await screen.findByRole("heading", { name: "No game servers yet" })).toBeVisible();
      expect(container.querySelectorAll(".button--gold")).toHaveLength(1);
      const links = screen.getAllByRole("link", { name: "Create server" });
      expect(links).toHaveLength(1);
      expect(links[0]).toHaveClass("button--gold");
      expect(screen.queryByRole("button", { name: "Create server" })).not.toBeInTheDocument();
      expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
      expect(screen.queryByRole("note")).not.toBeInTheDocument();
      expect(screen.queryByRole("region", { name: /Active|Off/ })).not.toBeInTheDocument();
    });

    it("shows the member panel with no toolbar, cost line or sections", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([]);

      renderPage();

      expect(
        await screen.findByRole("heading", { name: "No servers shared with you yet" }),
      ).toBeVisible();
      expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
      expect(screen.queryByRole("group", { name: "Filter servers" })).not.toBeInTheDocument();
      expect(screen.queryByRole("note")).not.toBeInTheDocument();
      expect(sectionHeadings()).toEqual(["No servers shared with you yet"]);
    });
  });

  describe("header", () => {
    it("shows a gold create link to a creator with servers", async () => {
      authState.user = userWithClaims("crt:sv");
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage();

      await screen.findAllByRole("article");
      const create = screen.getByRole("link", { name: "Create server" });
      expect(create).toHaveAttribute("href", "/command/servers/create");
      expect(create).toHaveClass("button--gold");
    });

    it("links to Cost alerts from the header with a ghost link", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage();

      await screen.findAllByRole("article");
      const link = screen.getByRole("link", { name: "Cost alerts" });
      expect(link).toHaveAttribute("href", "/command/servers/cost-alerts");
      expect(link).toHaveClass("button--ghost");
    });

    it("links to Cost alerts in German", async () => {
      await useLanguage("de");
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage();

      await screen.findAllByRole("article");
      expect(screen.getByRole("link", { name: "Kostenwarnungen" })).toHaveAttribute(
        "href",
        "/command/servers/cost-alerts",
      );
    });

    it("shows a disabled gold create button with the permission reason", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage();

      await screen.findAllByRole("article");
      const create = screen.getByRole("button", { name: "Create server" });
      expect(create).toHaveClass("button--gold");
      expect(create).toHaveAttribute("aria-disabled", "true");
      expect(create).toHaveAccessibleDescription(
        "You need permission to create servers. Ask an administrator.",
      );
    });

    it("refetches once on Refresh servers and disables the control while fetching", async () => {
      const second = deferred<GameServer[]>();
      const servers = vi
        .spyOn(hostingService, "servers")
        .mockResolvedValueOnce(standardFleet())
        .mockReturnValueOnce(second.promise);
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      const refresh = screen.getByRole("button", { name: "Refresh servers" });
      expect(refresh).toBeEnabled();

      await user.click(refresh);

      expect(servers).toHaveBeenCalledTimes(2);
      expect(refresh).toBeDisabled();
      await act(async () => second.resolve(standardFleet()));
      await waitFor(() => expect(refresh).toBeEnabled());
      expect(servers).toHaveBeenCalledTimes(2);
    });
  });

  describe("quick start", () => {
    const job = { primaryKey: "job-1" } as LifecycleJob;

    function startedServer(): GameServer {
      return fleetServer({ index: 1, title: "Alpha", status: "Running", createdById: meId });
    }

    it("confirms through the shared dialog, shows the submitted state and then the reported state", async () => {
      const second = deferred<GameServer[]>();
      vi.spyOn(hostingService, "servers")
        .mockResolvedValueOnce([offServer()])
        .mockReturnValueOnce(second.promise);
      vi.spyOn(hostingService, "server").mockResolvedValue(offServer());
      const execute = vi.spyOn(hostingService, "executeAction").mockResolvedValue(job);
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(within(card("Alpha")).getByRole("button", { name: "Start server" }));
      const dialog = await screen.findByRole("dialog");
      await user.click(within(dialog).getByRole("button", { name: "Start server" }));

      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
      expect(execute).toHaveBeenCalledTimes(1);
      const working = within(card("Alpha")).getByRole("button", { name: "In progress" });
      expect(working).toHaveAttribute("aria-disabled", "true");
      expect(working).toHaveAccessibleDescription(
        expect.stringContaining(
          "Request sent. This card updates as soon as the server changes.",
        ) as unknown as string,
      );
      expect(card("Alpha").querySelector(".server-state-badge")).toHaveTextContent("Off");

      await act(async () => second.resolve([startedServer()]));

      await waitFor(() =>
        expect(card("Alpha").querySelector(".server-state-badge")).toHaveTextContent("Running"),
      );
      expect(
        within(card("Alpha")).getByRole("button", { name: "Shut down and stop billing" }),
      ).not.toHaveAttribute("aria-disabled");
      expect(
        within(card("Alpha")).queryByRole("button", { name: "In progress" }),
      ).not.toBeInTheDocument();
    });

    it("leaves the card unchanged when the dialog is dismissed", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([offServer()]);
      vi.spyOn(hostingService, "server").mockResolvedValue(offServer());
      const execute = vi.spyOn(hostingService, "executeAction").mockResolvedValue(job);
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(within(card("Alpha")).getByRole("button", { name: "Start server" }));
      await user.click(await screen.findByRole("button", { name: "Don't start server" }));

      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
      expect(execute).not.toHaveBeenCalled();
      expect(
        within(card("Alpha")).getByRole("button", { name: "Start server" }),
      ).not.toHaveAttribute("aria-disabled");
    });

    it("leaves no submitted state after a rejected action", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([offServer()]);
      vi.spyOn(hostingService, "server").mockResolvedValue(offServer());
      const execute = vi
        .spyOn(hostingService, "executeAction")
        .mockRejectedValue(new Error("rejected"));
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(within(card("Alpha")).getByRole("button", { name: "Start server" }));
      const dialog = await screen.findByRole("dialog");
      await user.click(within(dialog).getByRole("button", { name: "Start server" }));
      expect(await within(dialog).findByRole("alert")).toBeVisible();
      await user.click(within(dialog).getByRole("button", { name: "Don't start server" }));

      await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
      expect(execute).toHaveBeenCalledTimes(1);
      expect(
        within(card("Alpha")).getByRole("button", { name: "Start server" }),
      ).not.toHaveAttribute("aria-disabled");
      expect(
        within(card("Alpha")).queryByRole("button", { name: "In progress" }),
      ).not.toBeInTheDocument();
    });
  });

  describe("no polling", () => {
    it("has no refetch interval and refetches only on Refresh servers", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const servers = vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      const { queryClient } = renderPage();

      await screen.findAllByRole("article");
      const query = queryClient.getQueryCache().find({ queryKey: hostingQueryKeys.fleet });
      expect(query).toBeDefined();
      expect(query!.observers.length).toBeGreaterThan(0);
      for (const observer of query!.observers) {
        expect(observer.options.refetchInterval).toBeUndefined();
      }

      await act(async () => {
        await vi.advanceTimersByTimeAsync(60_000);
      });
      expect(servers).toHaveBeenCalledTimes(1);

      await user.click(screen.getByRole("button", { name: "Refresh servers" }));
      await waitFor(() => expect(servers).toHaveBeenCalledTimes(2));
    });
  });

  describe("live notice", () => {
    it("appears after 3 seconds of disconnection and reconnects on request", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());
      const reconnectNow = vi.fn();
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      renderPage(
        "/command/servers",
        <FleetLiveUpdatesContext.Provider
          value={{
            status: "reconnecting",
            disconnectEpoch: 1,
            reconnectNow,
            subscribe: () => () => undefined,
            subscribeCostAlerts: () => () => undefined,
            markCostAlertsDismissed: () => undefined,
          }}
        >
          <ServerFleetPage />
        </FleetLiveUpdatesContext.Provider>,
      );

      await screen.findAllByRole("article");
      expect(screen.queryByText("Live updates paused")).not.toBeInTheDocument();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(3000);
      });

      expect(screen.getByText("Live updates paused")).toBeVisible();
      await user.click(screen.getByRole("button", { name: "Reconnect now" }));
      expect(reconnectNow).toHaveBeenCalledTimes(1);
    });

    it("shows no notice while the connection is open", async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.spyOn(hostingService, "servers").mockResolvedValue(standardFleet());

      renderPage(
        "/command/servers",
        <FleetLiveUpdatesContext.Provider
          value={{
            status: "open",
            disconnectEpoch: 1,
            reconnectNow: () => undefined,
            subscribe: () => () => undefined,
            subscribeCostAlerts: () => () => undefined,
            markCostAlertsDismissed: () => undefined,
          }}
        >
          <ServerFleetPage />
        </FleetLiveUpdatesContext.Provider>,
      );

      await screen.findAllByRole("article");
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(screen.queryByText("Live updates paused")).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Reconnect now" })).not.toBeInTheDocument();
    });
  });

  describe("announcements and focus", () => {
    it("announces a refetched change and keeps focus on a card that moved to Active", async () => {
      vi.spyOn(hostingService, "servers")
        .mockResolvedValueOnce([offServer(), runningServer()])
        .mockResolvedValueOnce([
          fleetServer({ index: 1, title: "Alpha", status: "Running", createdById: meId }),
          runningServer(),
        ]);

      const { container, queryClient } = renderPage();

      await screen.findAllByRole("article");
      expect(politeRegion(container)).toHaveTextContent("");
      const start = within(card("Alpha")).getByRole("button", { name: "Start server" });
      act(() => start.focus());
      expect(start).toHaveFocus();

      await act(async () => {
        await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet });
      });

      await waitFor(() =>
        expect(politeRegion(container)).toHaveTextContent("Alpha is now Running."),
      );
      expect(sectionHeadings()).toEqual(["Active (2)"]);
      const link = within(card("Alpha")).getByRole("link", { name: "Open server" });
      expect(link).toHaveFocus();
    });

    it("drops a server that stops costing from the Running filter without moving focus", async () => {
      vi.spyOn(hostingService, "servers")
        .mockResolvedValueOnce([runningServer(), runningServer("Charlie", 3)])
        .mockResolvedValueOnce([
          runningServer(),
          fleetServer({ index: 3, title: "Charlie", status: "Stopped", possibleCost: false }),
        ]);

      const { container, queryClient } = renderPage("/command/servers?filter=running");

      await screen.findAllByRole("article");
      expect(cardTitles()).toEqual(["Bravo", "Charlie"]);
      expect(screen.getByText("2 of 2 servers shown")).toBeVisible();
      act(() => searchBox().focus());

      await act(async () => {
        await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet });
      });

      await waitFor(() => expect(cardTitles()).toEqual(["Bravo"]));
      expect(screen.getByText("1 of 2 servers shown")).toBeVisible();
      expect(chip(/^Running 1$/)).toBeVisible();
      expect(politeRegion(container)).toHaveTextContent("Charlie is now Off.");
      expect(searchBox()).toHaveFocus();
    });
  });

  describe("states", () => {
    it("renders only the loading panel while the first load runs", async () => {
      vi.spyOn(hostingService, "servers").mockReturnValue(new Promise<GameServer[]>(() => {}));

      renderPage();

      expect(await screen.findByText("Loading game servers…")).toBeVisible();
      expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
      expect(screen.queryByRole("group", { name: "Filter servers" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Start server" })).not.toBeInTheDocument();
      expect(screen.queryByRole("article")).not.toBeInTheDocument();
    });

    it("shows the error panel with Try again when the first load fails", async () => {
      vi.spyOn(hostingService, "servers").mockRejectedValue(new Error("offline"));

      renderPage();

      expect(await screen.findByRole("button", { name: "Try again" })).toBeVisible();
      expect(screen.getByRole("alert")).toHaveTextContent(
        "The servers could not be loaded. Check your connection and try again.",
      );
      expect(screen.queryByRole("article")).not.toBeInTheDocument();
      expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    });

    it("keeps the stale cards below the error panel when a refetch fails", async () => {
      vi.spyOn(hostingService, "servers")
        .mockResolvedValueOnce(standardFleet())
        .mockRejectedValueOnce(new Error("offline"));
      const user = userEvent.setup();

      renderPage();

      await screen.findAllByRole("article");
      await user.click(screen.getByRole("button", { name: "Refresh servers" }));

      expect(await screen.findByRole("button", { name: "Try again" })).toBeVisible();
      expect(screen.getAllByRole("article")).toHaveLength(3);
    });

    it("marks the page busy during a background refetch and keeps its cards", async () => {
      const second = deferred<GameServer[]>();
      vi.spyOn(hostingService, "servers")
        .mockResolvedValueOnce(standardFleet())
        .mockReturnValueOnce(second.promise);
      const user = userEvent.setup();

      const { container } = renderPage();

      await screen.findAllByRole("article");
      const page = container.querySelector(".next-hosting-page")!;
      expect(page).toHaveAttribute("aria-busy", "false");

      await user.click(screen.getByRole("button", { name: "Refresh servers" }));

      expect(page).toHaveAttribute("aria-busy", "true");
      expect(screen.getAllByRole("article")).toHaveLength(3);
      await act(async () => second.resolve(standardFleet()));
      await waitFor(() => expect(page).toHaveAttribute("aria-busy", "false"));
    });

    it("never offers terminate on a card", async () => {
      vi.spyOn(hostingService, "servers").mockResolvedValue([
        fleetServer({
          index: 1,
          title: "Alpha",
          status: "Running",
          actions: [fleetAction("stop"), fleetAction("terminate")],
        }),
      ]);

      renderPage();

      await screen.findAllByRole("article");
      expect(screen.queryByRole("button", { name: "Shut down without saving" })).toBeNull();
      expect(screen.queryByRole("link", { name: "Shut down without saving" })).toBeNull();
    });
  });
});
