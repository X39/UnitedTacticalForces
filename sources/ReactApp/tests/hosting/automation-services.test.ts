import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { createElement, type ReactNode } from "react";
import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  AutomationSettingsConflictError,
  AutomationSettingsForbiddenError,
  AutomationSettingsValidationError,
  hostingQueryKeys,
  hostingService,
} from "@/features/hosting/services";
import type { ServerAutomationUpdate } from "@/features/hosting/types";
import {
  automationSaveFailure,
  useSaveAutomationSettings,
} from "@/features/hosting/useAutomationSettings";
import { fleetServer, serverAutomation } from "../helpers/fleetFixtures";

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

/** A complete update as the panel sends it. */
const update: ServerAutomationUpdate = {
  version: 2,
  idleShutdownEnabled: true,
  idleShutdownMinutes: 30,
  maxRuntimeEnabled: true,
  maxRuntimeHours: 6,
  timeZone: "Europe/Berlin",
  schedules: [{ id: null, action: "Stop", days: [1, 2, 3], time: "23:00" }],
  startConsent: false,
};

describe("automation settings save", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("sends one PUT with the update body and returns the parsed block", async () => {
    const saved = serverAutomation({
      settings: { ...serverAutomation().settings, idleShutdownEnabled: true, version: 3 },
    });
    const calls = stubFetch(json(saved));

    expect(await hostingService.saveAutomationSettings("server-1", update)).toEqual(saved);
    expect(calls).toEqual([
      { path: "/api/game-servers/server-1/automation", method: "PUT", body: update },
    ]);
  });

  it("reports HTTP 409 as a typed conflict and HTTP 403 as a typed lost right", async () => {
    stubFetch(json({ error: "stale" }, 409), json({ title: "no" }, 403));

    await expect(hostingService.saveAutomationSettings("server-1", update)).rejects.toBeInstanceOf(
      AutomationSettingsConflictError,
    );
    await expect(hostingService.saveAutomationSettings("server-1", update)).rejects.toBeInstanceOf(
      AutomationSettingsForbiddenError,
    );
  });

  it("reports HTTP 400 with the field list of the API, or an empty list without one", async () => {
    stubFetch(
      json({ error: "bad", fields: ["schedules[0].time", "idleShutdownMinutes"] }, 400),
      json({ error: "bad" }, 400),
    );

    await expect(hostingService.saveAutomationSettings("server-1", update)).rejects.toMatchObject({
      name: "AutomationSettingsValidationError",
      fields: ["schedules[0].time", "idleShutdownMinutes"],
    });
    await expect(hostingService.saveAutomationSettings("server-1", update)).rejects.toMatchObject({
      name: "AutomationSettingsValidationError",
      fields: [],
    });
  });

  it.each([503, 500])(
    "rejects HTTP %s with a plain error that is not a typed outcome",
    async (status) => {
      stubFetch(json({ error: "down" }, status));

      const rejection = await hostingService
        .saveAutomationSettings("server-1", update)
        .catch((error: unknown) => error);

      expect(rejection).toBeInstanceOf(Error);
      expect(rejection).not.toBeInstanceOf(AutomationSettingsConflictError);
      expect(rejection).not.toBeInstanceOf(AutomationSettingsForbiddenError);
      expect(rejection).not.toBeInstanceOf(AutomationSettingsValidationError);
    },
  );
});

describe("automationSaveFailure", () => {
  it("maps the typed errors and everything else", () => {
    expect(automationSaveFailure(new AutomationSettingsConflictError())).toBe("conflict");
    expect(automationSaveFailure(new AutomationSettingsForbiddenError())).toBe("forbidden");
    expect(automationSaveFailure(new AutomationSettingsValidationError(["time"]))).toBe("invalid");
    expect(automationSaveFailure(new Error("down"))).toBe("error");
    expect(automationSaveFailure("text")).toBe("error");
    expect(automationSaveFailure(undefined)).toBe("error");
  });
});

describe("useSaveAutomationSettings", () => {
  afterEach(() => vi.unstubAllGlobals());

  /** Renders the hook in a fresh query client and returns both. */
  function renderSave(serverId: string) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client }, children);
    return { client, ...renderHook(() => useSaveAutomationSettings(serverId), { wrapper }) };
  }

  it("writes the saved block into the server cache entry and invalidates exactly that key", async () => {
    const saved = serverAutomation({
      settings: { ...serverAutomation().settings, maxRuntimeEnabled: true, version: 3 },
    });
    stubFetch(json(saved));
    const server = fleetServer({ index: 1, status: "Running" });
    const key = hostingQueryKeys.server(server.primaryKey);
    const jobsKey = hostingQueryKeys.jobs(server.primaryKey);
    const { client, result } = renderSave(server.primaryKey);
    client.setQueryData(key, server);
    client.setQueryData(jobsKey, []);
    const invalidate = vi.spyOn(client, "invalidateQueries");

    await act(async () => {
      await result.current.mutateAsync(update);
    });

    const cached = client.getQueryData<typeof server>(key);
    expect(cached?.automation).toEqual(saved);
    expect(cached?.title).toBe(server.title);
    expect(cached?.deployment?.status).toBe("Running");
    expect(invalidate).toHaveBeenCalledTimes(1);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: key, exact: true });
    expect(client.getQueryState(jobsKey)?.isInvalidated).toBe(false);
  });

  it("creates no cache entry when the server was never loaded and leaves the cache on failure", async () => {
    stubFetch(json(serverAutomation()), json({ error: "stale" }, 409));
    const { client, result } = renderSave("server-9");
    const key = hostingQueryKeys.server("server-9");

    await act(async () => {
      await result.current.mutateAsync(update);
    });
    expect(client.getQueryData(key)).toBeUndefined();

    const server = fleetServer({ index: 9 });
    client.setQueryData(key, server);
    await act(async () => {
      await result.current.mutateAsync(update).catch(() => undefined);
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(automationSaveFailure(result.current.error)).toBe("conflict");
    expect(client.getQueryData(key)).toBe(server);
  });
});
