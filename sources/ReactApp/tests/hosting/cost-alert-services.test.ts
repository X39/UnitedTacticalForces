import { initializeApiClient } from "@/api/client";
import { identityService } from "@/api/services";
import type { PublicConfiguration } from "@/app/config";
import { CostAlertSettingsConflictError, hostingService } from "@/features/hosting/services";
import { costAlert, costAlertSettings } from "../helpers/costAlertFixtures";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

/** One recorded request of the stubbed fetch. */
interface RecordedCall {
  path: string;
  method: string | undefined;
  body: unknown;
}

/** Stubs fetch with the given responses in order and returns the recorded calls. */
function stubFetch(...responses: Response[]): RecordedCall[] {
  const calls: RecordedCall[] = [];
  const queue = [...responses];
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation((url: URL, init: RequestInit) => {
      calls.push({
        path: url.pathname,
        method: init.method,
        body: typeof init.body === "string" ? (JSON.parse(init.body) as unknown) : init.body,
      });
      return Promise.resolve(queue.shift() ?? new Response(null, { status: 204 }));
    }),
  );
  initializeApiClient(configuration);
  return calls;
}

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("cost alert client calls", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("lists the alerts from GET /cost-alerts and treats no body as an empty list", async () => {
    const calls = stubFetch(json([costAlert()]), new Response(null, { status: 204 }));

    expect(await hostingService.costAlerts()).toEqual([costAlert()]);
    expect(await hostingService.costAlerts()).toEqual([]);
    expect(calls.map((call) => [call.method ?? "GET", call.path])).toEqual([
      ["GET", "/api/cost-alerts"],
      ["GET", "/api/cost-alerts"],
    ]);
  });

  it("dismisses one alert with POST /cost-alerts/{id}/dismissal", async () => {
    const calls = stubFetch();

    await hostingService.dismissCostAlert("alert-1");

    expect(calls).toEqual([
      { path: "/api/cost-alerts/alert-1/dismissal", method: "POST", body: undefined },
    ]);
  });

  it("dismisses the shown alerts of a server with POST /cost-alerts/dismissals, the server id and the alert ids", async () => {
    const calls = stubFetch();

    await hostingService.dismissServerCostAlerts("server-1", ["a1", "a2"]);

    expect(calls).toEqual([
      {
        path: "/api/cost-alerts/dismissals",
        method: "POST",
        body: { gameServerId: "server-1", costAlertIds: ["a1", "a2"] },
      },
    ]);
  });

  it("reads the settings of one owner with GET /cost-owners/{id}/alert-settings", async () => {
    const calls = stubFetch(json(costAlertSettings()));

    expect(await hostingService.costAlertSettings("owner-1")).toEqual(costAlertSettings());
    expect(calls.map((call) => [call.method ?? "GET", call.path])).toEqual([
      ["GET", "/api/cost-owners/owner-1/alert-settings"],
    ]);
  });

  it.each([
    [403, "forbidden"],
    [404, "not-found"],
  ])("lets the page tell HTTP %s apart through the rejected error", async (status, code) => {
    stubFetch(json({ title: "no" }, status));

    await expect(hostingService.costAlertSettings("owner-1")).rejects.toMatchObject({
      status,
      code,
    });
  });

  it("returns null for the personal settings on 204 and the settings otherwise", async () => {
    const calls = stubFetch(new Response(null, { status: 204 }), json(costAlertSettings()));

    expect(await hostingService.personalCostAlertSettings()).toBeNull();
    expect(await hostingService.personalCostAlertSettings()).toEqual(costAlertSettings());
    expect(calls.every((call) => call.path === "/api/cost-owners/mine/alert-settings")).toBe(true);
  });

  it("saves with PUT and the full update and returns the saved settings", async () => {
    const saved = costAlertSettings({ revision: 2, thresholds: [5, 10, 25] });
    const calls = stubFetch(json(saved));
    const update = {
      revision: 1,
      thresholds: [5, 10, 25],
      discordEnabled: true,
      emailEnabled: false,
      language: "de",
    };

    expect(await hostingService.updateCostAlertSettings("owner-1", update)).toEqual(saved);
    expect(calls).toEqual([
      { path: "/api/cost-owners/owner-1/alert-settings", method: "PUT", body: update },
    ]);
  });

  it("reports a stale revision as a typed conflict and other failures unchanged", async () => {
    const update = {
      revision: 1,
      thresholds: [5],
      discordEnabled: true,
      emailEnabled: true,
      language: null,
    };
    stubFetch(json({ title: "stale" }, 409), json({ title: "boom" }, 500));

    await expect(hostingService.updateCostAlertSettings("owner-1", update)).rejects.toBeInstanceOf(
      CostAlertSettingsConflictError,
    );
    await expect(
      hostingService.updateCostAlertSettings("owner-1", update),
    ).rejects.not.toBeInstanceOf(CostAlertSettingsConflictError);
  });

  it("records the language with POST /users/me/locale and { locale }", async () => {
    const calls = stubFetch();

    await identityService.recordLocale("de");

    expect(calls).toEqual([
      { path: "/api/users/me/locale", method: "POST", body: { locale: "de" } },
    ]);
  });
});
