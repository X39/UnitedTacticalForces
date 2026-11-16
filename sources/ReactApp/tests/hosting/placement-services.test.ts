import { beforeEach } from "vitest";
import { initializeApiClient } from "@/api/client";
import { hostingService } from "@/features/hosting/services";

describe("placement API adapter", () => {
  beforeEach(() => {
    initializeApiClient({
      Api: { BaseUrl: "/api/" },
      Urls: { TeamSpeak3: "", Discord: "" },
      Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  it("reads the authoritative placement price from the existing storage resource", async () => {
    const placementEstimate = {
      hostCount: 2,
      estimatedComputeHourlyCostEur: 0.2,
      estimatedStorageHourlyCostEur: 0.04,
      estimatedHourlyCostEur: 0.24,
      estimatedMonthlyCostEur: null,
    };
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        Response.json({ placementEstimate, components: null, runtimeProfiles: null }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const controller = new AbortController();

    const result = await hostingService.storageDemand("server-1", controller.signal);

    expect(result.placementEstimate).toEqual(placementEstimate);
    expect(result.components).toEqual([]);
    expect(fetchMock.mock.calls[0]?.[0].pathname).toBe("/api/game-servers/server-1/storage-demand");
    expect(fetchMock.mock.calls[0]?.[1].signal).toBe(controller.signal);
  });

  it("keeps a missing estimate unavailable instead of constructing a zero price", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({})));
    expect((await hostingService.storageDemand("server-1")).placementEstimate).toBeNull();
  });

  it.each(["same-host", "separate-host"])(
    "saves %s using the existing typed configuration endpoint",
    async (placement) => {
      const fetchMock = vi.fn().mockResolvedValue(Response.json({ entries: [] }));
      vi.stubGlobal("fetch", fetchMock);

      await hostingService.updateConfiguration(
        "server-1",
        [{ key: "headlessClientPlacement", value: placement }],
        [],
      );

      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(fetchMock.mock.calls[0]?.[0].pathname).toBe(
        "/api/game-servers/server-1/configuration",
      );
      expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
        method: "PUT",
        body: JSON.stringify({
          values: [{ key: "headlessClientPlacement", value: placement }],
          resetKeys: [],
        }),
      });
    },
  );

  it("preserves typed choices and control-plane metadata when reading configuration", async () => {
    const entry = {
      key: "headlessClientPlacement",
      isControlPlaneOnly: true,
      targetPath: "",
      choices: [{ value: "same-host", label: "Same host", description: null }],
    };
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ entries: [entry] })));
    expect((await hostingService.configuration("server-1")).entries).toEqual([entry]);
  });
});
