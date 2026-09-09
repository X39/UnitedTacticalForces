import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { hostingService } from "@/features/hosting/services";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

describe("hosting API service", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("poll reads only the control-plane API and normalizes collection fields", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify([
          {
            primaryKey: "10000000-0000-0000-0000-000000000001",
            title: "UTF One",
            actions: null,
            deployment: { status: 3, resources: null },
          },
        ]),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const result = await hostingService.servers();

    expect(result[0]?.actions).toEqual([]);
    expect(result[0]?.deployment?.resources).toEqual([]);
    expect(result[0]?.deployment?.status).toBe("Running");
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers");
    expect(init.credentials).toBe("include");
    expect(new Headers(init.headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("submits only the server-authoritative action endpoint and supplied challenge", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ primaryKey: "job-1", commands: [] }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.executeAction("server-1", "terminate", "TERMINATE");

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers/server-1/actions/terminate");
    expect(init.method).toBe("POST");
    expect(init.body).toBe(JSON.stringify({ confirmation: "TERMINATE" }));
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("loads and normalizes configuration-profile access grants", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          allowedPermissions: ["cfg:view"],
          users: [
            {
              userId: "10000000-0000-0000-0000-000000000001",
              nickname: "Operator",
              permissions: null,
            },
          ],
          roles: null,
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const access = await hostingService.profileAccess("profile-1");

    expect(access.users[0]?.permissions).toEqual([]);
    expect(access.roles).toEqual([]);
    const [url] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/configuration-profiles/profile-1/access");
  });

  it("uses the exact user and role grant/revoke endpoints", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.changeProfileUserAccess(
      "profile-1",
      "10000000-0000-0000-0000-000000000001",
      "cfg:view",
      true,
    );
    await hostingService.changeProfileRoleAccess("profile-1", 42, "cfg:archive", false);

    const [userUrl, userInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [roleUrl, roleInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(userUrl.pathname).toBe("/api/configuration-profiles/profile-1/access/users/grant");
    expect(userInit.body).toBe(
      JSON.stringify({
        userId: "10000000-0000-0000-0000-000000000001",
        permission: "cfg:view",
      }),
    );
    expect(roleUrl.pathname).toBe("/api/configuration-profiles/profile-1/access/roles/revoke");
    expect(roleInit.body).toBe(JSON.stringify({ roleId: 42, permission: "cfg:archive" }));
  });

  it("publishes sparse revisions with a baseline and explicit resets", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.createProfileRevision(
      "profile-1",
      7,
      [{ key: "verifySignatures", value: 2 }],
      ["password"],
    );

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/configuration-profiles/profile-1/revisions");
    expect(init.body).toBe(
      JSON.stringify({
        baseRevisionNumber: 7,
        values: [{ key: "verifySignatures", value: 2 }],
        resetKeys: ["password"],
      }),
    );
  });

  it("searches access candidates and saves the matrix atomically", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify([{ userId: "user-1", nickname: "Operator" }]), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.profileUserCandidates("profile-1", "oper");
    await hostingService.updateProfileAccessMatrix("profile-1", {
      users: [{ userId: "user-1", permissions: ["cfg:view"] }],
      roles: [],
    });

    const [searchUrl] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [matrixUrl, matrixInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(searchUrl.pathname).toBe("/api/configuration-profiles/profile-1/access/users/options");
    expect(searchUrl.searchParams.get("search")).toBe("oper");
    expect(matrixUrl.pathname).toBe("/api/configuration-profiles/profile-1/access/matrix");
    expect(matrixInit.body).toBe(
      JSON.stringify({
        users: [{ userId: "user-1", permissions: ["cfg:view"] }],
        roles: [],
      }),
    );
  });
});
