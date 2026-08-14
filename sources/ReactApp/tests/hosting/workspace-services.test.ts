import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { hostingService } from "@/features/hosting/services";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

describe("server workspace API adapter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("uses the typed configuration, profile, runtime, storage, and mod-pack endpoints", async () => {
    const fetchMock = vi.fn().mockImplementation((request: URL, init: RequestInit) => {
      const hasBody = init.method === "GET" || !init.method ? undefined : null;
      return Promise.resolve(
        request.pathname.endsWith("/configuration") && (!init.method || init.method === "GET")
          ? new Response(JSON.stringify({ serverId: "server-1", entries: null }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            })
          : new Response(hasBody, { status: 204 }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    expect((await hostingService.configuration("server-1")).entries).toEqual([]);
    await hostingService
      .updateConfiguration("server-1", [{ key: "maxPlayers", value: 64 }], ["password"])
      .catch(() => undefined);
    await hostingService.selectConfigurationProfile("server-1", "profile-1", "revision-1");
    await hostingService.changeRuntimeProfile("server-1", "cx32");
    await hostingService.changeRuntimeStorage("server-1", "FixedExternal", 100);
    await hostingService.changeModPack("server-1", 42);

    const calls = fetchMock.mock.calls.map(([url, init]) => ({
      path: (url as URL).pathname,
      method: (init as RequestInit).method,
      body: (init as RequestInit).body,
    }));
    expect(calls).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          path: "/api/game-servers/server-1/configuration",
          method: "PUT",
        }),
        expect.objectContaining({
          path: "/api/game-servers/server-1/configuration/profile",
          body: JSON.stringify({ profileId: "profile-1", pinnedRevisionId: "revision-1" }),
        }),
        expect.objectContaining({
          path: "/api/game-servers/server-1/runtime-profile",
          body: JSON.stringify({ runtimeProfileIdentifier: "cx32" }),
        }),
        expect.objectContaining({
          path: "/api/game-servers/server-1/runtime-storage",
          body: JSON.stringify({ strategy: "FixedExternal", externalStorageSizeGiB: 100 }),
        }),
        expect.objectContaining({
          path: "/api/game-servers/server-1/mod-pack",
          body: JSON.stringify({ modPackDefinitionId: 42 }),
        }),
      ]),
    );
  });

  it("supports browsing, binary upload, generated override confirmation, folders, delete and downloads", async () => {
    const fetchMock = vi.fn().mockImplementation((request: URL, init: RequestInit) =>
      Promise.resolve(
        !init.method || init.method === "GET"
          ? new Response(JSON.stringify([]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            })
          : init.method === "PUT" || init.method === "POST"
            ? new Response(JSON.stringify({ path: "server.cfg", name: "server.cfg" }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
              })
            : new Response(null, { status: 204 }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.files("server-1", "persistent", "config");
    await hostingService.uploadFile(
      "server-1",
      "persistent",
      "config/server.cfg",
      new Blob(["hostname=test"]),
      true,
    );
    await hostingService.createDirectory("server-1", "persistent", "profiles/main");
    await hostingService.deleteFile("server-1", "persistent", "profiles/main", true);

    const browseUrl = fetchMock.mock.calls[0]?.[0] as URL;
    const uploadUrl = fetchMock.mock.calls[1]?.[0] as URL;
    const uploadInit = fetchMock.mock.calls[1]?.[1] as RequestInit;
    const deleteUrl = fetchMock.mock.calls[3]?.[0] as URL;
    expect(browseUrl.searchParams.get("path")).toBe("config");
    expect(uploadUrl.searchParams.get("confirmGeneratedOverride")).toBe("true");
    expect(uploadInit.body).toBeInstanceOf(Blob);
    expect(deleteUrl.searchParams.get("recursive")).toBe("true");
    expect(hostingService.fileDownloadUrl("server-1", "live", "logs/server.log")).toContain(
      "/api/game-servers/server-1/files/live/content?path=logs%2Fserver.log",
    );
  });

  it("uses preservation and durable record deletion endpoints without provider calls", async () => {
    const fetchMock = vi.fn().mockImplementation((_request: URL, init: RequestInit) =>
      Promise.resolve(
        init.method === "POST"
          ? new Response(JSON.stringify({ primaryKey: "selection-1", path: "profiles" }), {
              status: 201,
              headers: { "Content-Type": "application/json" },
            })
          : new Response(null, { status: 204 }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.savePreservationSelection("server-1", {
      path: "profiles",
      scope: "RecursiveFolder",
      mode: "MirrorDeletions",
    });
    await hostingService.deletePreservationSelection("server-1", "selection-1");
    await hostingService.delete("server-1");

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect((fetchMock.mock.calls[0]?.[0] as URL).hostname).toBe("localhost");
    expect((fetchMock.mock.calls[2]?.[0] as URL).pathname).toBe("/api/game-servers/server-1");
  });
});
