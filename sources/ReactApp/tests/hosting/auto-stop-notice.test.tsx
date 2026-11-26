import { act, screen, waitFor, within } from "@testing-library/react";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { useEffect, useState } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AutoStopNotice } from "@/components/server/AutoStopNotice";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, LifecycleJob, ServerPendingStop } from "@/features/hosting/types";
import { costAlert } from "../helpers/costAlertFixtures";
import { fleetServer, serverAutomation } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceJob } from "../helpers/workspaceFixtures";

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-4000-8000-0000000000f1",
      nickname: "Falcon One",
      isBanned: false,
      isDeleted: false,
      isVereinMember: true,
      claims: [
        {
          primaryKey: 1,
          identifier: "sv:view",
          value: null,
          title: "View servers",
          category: "Hosting",
          description: "",
          isPrefix: false,
          valueType: null,
        },
      ],
      roles: [],
    } as unknown as FullUser,
    isAuthenticated: true,
    isLoading: false,
    providers: { steam: true, discord: true },
    providersLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

const now = "2026-11-21T14:00:00Z";
const stopsAt = "2026-11-21T14:10:00Z";

/** Builds a server whose automation block announces the given stop, or none. */
function serverWith(
  stop: ServerPendingStop | null,
  options: { canManage?: boolean; withAutomation?: boolean } = {},
): GameServer {
  const base = serverAutomation();
  return fleetServer({
    title: "UTF One",
    status: "Running",
    canManageAutomation: options.canManage ?? false,
    automation:
      options.withAutomation === false
        ? null
        : serverAutomation({ status: { ...base.status, pendingStop: stop } }),
  });
}

/** Builds a pending stop as the API sends it. */
function pending(reason: string, limit: number | null): ServerPendingStop {
  return { reason, stopsAt, limit } as ServerPendingStop;
}

/** The inputs the harness renders; a test replaces them to model a refetched server. */
interface HarnessState {
  server: GameServer;
  jobs: LifecycleJob[];
}

const harness: { set: (next: HarnessState) => void } = { set: () => undefined };

/** Renders the notice from state a test can replace, as the workspace page does from its queries. */
function Harness({ initial }: { initial: HarnessState }) {
  const [state, setState] = useState(initial);
  useEffect(() => {
    harness.set = setState;
  }, []);
  return <AutoStopNotice server={state.server} jobs={state.jobs} />;
}

function renderNotice(server: GameServer, jobs: LifecycleJob[] = []) {
  return renderWithProviders(<Harness initial={{ server, jobs }} />);
}

function replace(next: HarnessState): void {
  act(() => harness.set(next));
}

describe("automatic shutdown notice", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows the stop time, the idle reason with its limit, the consequence and the relative line", () => {
    vi.useFakeTimers({ now: Date.parse(now), toFake: ["Date", "setInterval", "clearInterval"] });
    renderNotice(serverWith(pending("Idle", 30)));

    const panel = screen.getByRole("status");
    expect(panel).toHaveClass("state-panel--warning");
    expect(within(panel).getByRole("heading", { level: 2 })).toHaveTextContent(
      `Automatic shutdown at ${formatInstant(stopsAt)}`,
    );
    expect(
      within(panel).getByText(
        "Nobody has been on the server for 30 minutes. It shuts down unless someone joins.",
      ),
    ).toBeVisible();
    expect(
      within(panel).getByText(
        "It saves its data, stops, and releases the cloud machine. Billing stops once the machine is confirmed gone.",
      ),
    ).toBeVisible();
    expect(within(panel).getByText("Shuts down in 10 minutes.")).toBeVisible();
    expect(within(panel).queryByText("UTF One")).toBeNull();
  });

  it("names the maximum runtime and schedule reasons", () => {
    const { unmount } = renderNotice(serverWith(pending("MaxRuntime", 6)));
    expect(screen.getByText("The server reached its maximum runtime of 6 hours.")).toBeVisible();
    unmount();

    renderNotice(serverWith(pending("Schedule", null)));
    expect(screen.getByText("A schedule shuts the server down at this time.")).toBeVisible();
  });

  it("falls back to the schedule sentence for an unknown reason and omits a count it does not have", () => {
    const { unmount } = renderNotice(serverWith(pending("SomethingNew", null)));
    expect(screen.getByText("A schedule shuts the server down at this time.")).toBeVisible();
    unmount();

    renderNotice(serverWith(pending("Idle", null)));
    expect(screen.queryByText(/Nobody has been on the server/)).toBeNull();
    expect(screen.getByText(/It saves its data, stops/)).toBeVisible();
  });

  it("offers the settings link only to viewers who may manage the automation", () => {
    const { unmount } = renderNotice(serverWith(pending("Idle", 30), { canManage: true }));
    const link = screen.getByRole("link", { name: "Change automatic shutdown" });
    expect(link.getAttribute("href")).toMatch(/\?tab=settings#server-automation$/);
    expect(link.className).toContain("button--ghost");
    unmount();

    renderNotice(serverWith(pending("Idle", 30), { canManage: false }));
    expect(screen.getByRole("heading", { level: 2 })).toBeVisible();
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("renders nothing and no placeholder without a pending stop or an automation block", () => {
    const { unmount } = renderNotice(serverWith(null));
    expect(screen.queryByRole("status")).toBeNull();
    unmount();

    renderNotice(serverWith(null, { withAutomation: false }));
    expect(screen.queryByRole("status")).toBeNull();
    expect(document.querySelector(".state-panel")).toBeNull();
  });

  it("refreshes the relative line on the clock inside an aria-live off element", () => {
    vi.useFakeTimers({ now: Date.parse(now), toFake: ["Date", "setInterval", "clearInterval"] });
    renderNotice(serverWith(pending("Idle", 30)));
    const line = screen.getByText("Shuts down in 10 minutes.");
    expect(line).toHaveAttribute("aria-live", "off");
    const title = screen.getByRole("heading", { level: 2 }).textContent;

    act(() => {
      vi.advanceTimersByTime(30_000);
    });

    expect(screen.getByText("Shuts down in 10 minutes.")).toBe(line);
    act(() => {
      vi.advanceTimersByTime(270_000);
    });
    expect(line).toHaveTextContent("Shuts down in 5 minutes.");
    expect(screen.getByRole("heading", { level: 2 }).textContent).toBe(title);
  });

  it("shows the cancellation toast for an idle stop that disappears without a stop job", async () => {
    renderNotice(serverWith(pending("Idle", 30)));
    expect(document.querySelector(".app-toast")).toBeNull();

    replace({ server: serverWith(null), jobs: [] });

    expect(
      await screen.findByText("Automatic shutdown cancelled: someone joined UTF One."),
    ).toBeVisible();
    expect(screen.queryByRole("heading", { level: 2 })).toBeNull();
  });

  it("uses the settings copy for every other reason", async () => {
    renderNotice(serverWith(pending("MaxRuntime", 6)));

    replace({ server: serverWith(null), jobs: [] });

    expect(
      await screen.findByText("Automatic shutdown cancelled: the settings for UTF One changed."),
    ).toBeVisible();
  });

  it("shows no toast when the stop disappears because a Stop job started", () => {
    renderNotice(serverWith(pending("Idle", 30)));

    replace({
      server: serverWith(null),
      jobs: [workspaceJob({ kind: "Stop", actionId: "stop", status: "Running" })],
    });

    expect(document.querySelector(".app-toast")).toBeNull();
  });

  it("shows no toast on the first render, for a server that never had a stop or after one toast", async () => {
    renderNotice(serverWith(null));
    expect(document.querySelector(".app-toast")).toBeNull();

    replace({ server: serverWith(pending("Idle", 30)), jobs: [] });
    replace({ server: serverWith(null), jobs: [] });
    await screen.findByText("Automatic shutdown cancelled: someone joined UTF One.");
    replace({ server: serverWith(null), jobs: [] });

    expect(document.querySelectorAll(".app-toast")).toHaveLength(1);
  });

  it("renders the German sentences at 320px without truncation", async () => {
    await i18n.changeLanguage("de");
    const width = window.innerWidth;
    Object.defineProperty(window, "innerWidth", { configurable: true, value: 320 });
    try {
      renderNotice(serverWith(pending("Idle", 30), { canManage: true }));

      const panel = screen.getByRole("status");
      expect(within(panel).getByRole("heading", { level: 2 })).toHaveTextContent(
        `Automatisches Herunterfahren um ${formatInstant(stopsAt)}`,
      );
      expect(
        within(panel).getByText(
          "Seit 30 Minuten war niemand auf dem Server. Er fährt herunter, wenn niemand beitritt.",
        ),
      ).toBeVisible();
      expect(
        within(panel).getByText(
          /Er speichert seine Daten, stoppt und gibt die Cloud-Maschine frei/,
        ),
      ).toBeVisible();
      expect(
        within(panel).getByRole("link", { name: "Automatisches Herunterfahren ändern" }),
      ).toBeVisible();
      for (const element of Array.from(panel.querySelectorAll("*"))) {
        expect(element.getAttribute("class") ?? "").not.toMatch(/truncate|ellipsis|clamp/i);
      }
    } finally {
      Object.defineProperty(window, "innerWidth", { configurable: true, value: width });
    }
  });

  it("uses no timer of its own and no refetch interval", () => {
    const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");
    for (const file of [
      "components/server/AutoStopNotice.tsx",
      "features/hosting/useAutoStopNotice.ts",
    ]) {
      const source = readFileSync(resolve(sourceRoot, file), "utf8")
        .replace(/\/\*[\s\S]*?\*\//g, "")
        .replace(/(^|[^:])\/\/.*$/gm, "$1");
      expect(source).not.toMatch(/\bsetInterval\b|\bsetTimeout\b|\brefetchInterval\b/);
    }
  });
});

describe("automatic shutdown notice in the workspace", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  /** Renders the whole workspace page with the requests of the Overview section answered. */
  function renderPage(target: GameServer) {
    vi.spyOn(hostingService, "server").mockResolvedValue(target);
    vi.spyOn(hostingService, "jobs").mockResolvedValue([]);
    vi.spyOn(hostingService, "operations").mockResolvedValue([]);
    vi.spyOn(hostingService, "files").mockResolvedValue([]);
    vi.spyOn(hostingService, "costAlerts").mockResolvedValue([
      costAlert({ serverId: target.primaryKey }),
    ]);
    return renderWithProviders(<ServerWorkspacePage />, {
      path: "/command/servers/:serverId",
      route: `/command/servers/${target.primaryKey}`,
    });
  }

  it("renders first in the notices, above the cost alert banner", async () => {
    renderPage(serverWith(pending("Idle", 30)));

    const heading = await screen.findByRole("heading", { name: /^Automatic shutdown at / });
    const banner = await screen.findByRole("heading", { name: /cost alert/i });
    const notice = heading.closest("section") as HTMLElement;
    const costPanel = banner.closest("section") as HTMLElement;
    expect(
      Boolean(notice.compareDocumentPosition(costPanel) & Node.DOCUMENT_POSITION_FOLLOWING),
    ).toBe(true);
  });

  it("renders no notice for a server without a pending stop", async () => {
    renderPage(serverWith(null));

    await screen.findByRole("heading", { name: "UTF One" });
    await waitFor(() => expect(hostingService.costAlerts).toHaveBeenCalled());
    expect(screen.queryByRole("heading", { name: /^Automatic shutdown at / })).toBeNull();
  });
});
