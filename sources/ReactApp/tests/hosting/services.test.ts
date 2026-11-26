import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";

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

  it("loads the server access list from its path and defaults every missing list", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          users: [{ userId: "user-1", nickname: "Operator", detail: "#000001", permissions: null }],
          roles: null,
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const access = await hostingService.serverAccess("server-1");

    expect(access.users[0]?.permissions).toEqual([]);
    expect(access.roles).toEqual([]);
    expect(access.allowedPermissions).toEqual([]);
    expect(access.assignablePermissions).toEqual([]);
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers/server-1/access");
    expect(init.method ?? "GET").toBe("GET");
  });

  it("keeps the assignable permissions the access list reports", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          allowedPermissions: ["sv", "sv:view", "sv:operate"],
          assignablePermissions: ["sv:view"],
          users: [],
          roles: [],
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const access = await hostingService.serverAccess("server-1");

    expect(access.allowedPermissions).toEqual(["sv", "sv:view", "sv:operate"]);
    expect(access.assignablePermissions).toEqual(["sv:view"]);
  });

  it("searches server access candidates with the search text and a take of 21", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify([{ userId: "user-1", nickname: "Operator", detail: "#000001" }]),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify([{ roleId: 4, title: "Crew", description: "", memberCount: 2 }]),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const users = await hostingService.serverUserCandidates("server-1", "oper");
    const roles = await hostingService.serverRoleCandidates("server-1", "cr w");

    expect(users).toEqual([{ userId: "user-1", nickname: "Operator", detail: "#000001" }]);
    expect(roles).toEqual([{ roleId: 4, title: "Crew", description: "", memberCount: 2 }]);
    const [userUrl, userInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [roleUrl, roleInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(userUrl.pathname).toBe("/api/game-servers/server-1/access/users/options");
    expect(userUrl.searchParams.get("search")).toBe("oper");
    expect(userUrl.searchParams.get("take")).toBe("21");
    expect(userInit.method).toBe("GET");
    expect(roleUrl.pathname).toBe("/api/game-servers/server-1/access/roles/options");
    expect(roleUrl.searchParams.get("search")).toBe("cr w");
    expect(roleUrl.searchParams.get("take")).toBe("21");
    expect(roleInit.method).toBe("GET");
  });

  it("returns an empty candidate list for an empty body", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
    initializeApiClient(configuration);

    expect(await hostingService.serverUserCandidates("server-1", "")).toEqual([]);
    expect(await hostingService.serverRoleCandidates("server-1", "")).toEqual([]);
  });

  it("posts server access grants and revokes to the exact user and role endpoints", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await hostingService.changeServerUserAccess(
      "server-1",
      "10000000-0000-0000-0000-000000000001",
      "sv:view",
      true,
    );
    await hostingService.changeServerUserAccess(
      "server-1",
      "10000000-0000-0000-0000-000000000001",
      "sv:operate",
      false,
    );
    await hostingService.changeServerRoleAccess("server-1", 42, "sv:modify", true);
    await hostingService.changeServerRoleAccess("server-1", 42, "sv:manage-secrets", false);

    const calls = fetchMock.mock.calls as [URL, RequestInit][];
    const summary = calls.map(([url, init]) => [url.pathname, init.method, init.body]);
    expect(summary).toEqual([
      [
        "/api/game-servers/server-1/access/users/grant",
        "POST",
        JSON.stringify({ userId: "10000000-0000-0000-0000-000000000001", permission: "sv:view" }),
      ],
      [
        "/api/game-servers/server-1/access/users/revoke",
        "POST",
        JSON.stringify({
          userId: "10000000-0000-0000-0000-000000000001",
          permission: "sv:operate",
        }),
      ],
      [
        "/api/game-servers/server-1/access/roles/grant",
        "POST",
        JSON.stringify({ roleId: 42, permission: "sv:modify" }),
      ],
      [
        "/api/game-servers/server-1/access/roles/revoke",
        "POST",
        JSON.stringify({ roleId: 42, permission: "sv:manage-secrets" }),
      ],
    ]);
  });

  it("puts the chosen cost owner to the server cost owner endpoint and accepts a 204", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await expect(
      hostingService.changeServerCostOwner("server-1", "c0000000-0000-4000-8000-000000000002"),
    ).resolves.toBeUndefined();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers/server-1/cost-owner");
    expect(init.method).toBe("PUT");
    expect(init.body).toBe(JSON.stringify({ costOwnerId: "c0000000-0000-4000-8000-000000000002" }));
  });

  it("lists the cost owner options with the search text and a take of 21", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify([
          {
            costOwnerId: "c0000000-0000-4000-8000-000000000002",
            displayName: "Association",
            isFrozen: false,
            isCurrentUser: false,
            kind: "Verein",
          },
        ]),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const owners = await hostingService.serverCostOwnerOptions("server-1", "assoc");

    expect(owners.map((owner) => owner.displayName)).toEqual(["Association"]);
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers/server-1/cost-owner/options");
    expect(url.searchParams.get("search")).toBe("assoc");
    expect(url.searchParams.get("take")).toBe("21");
    expect(init.method).toBe("GET");
  });

  it("returns an empty owner list for an empty body and for an empty array", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
    initializeApiClient(configuration);
    expect(await hostingService.serverCostOwnerOptions("server-1", "")).toEqual([]);

    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }),
        ),
    );
    expect(await hostingService.serverCostOwnerOptions("server-1", "")).toEqual([]);
  });

  it("keeps the owner options keys under the server key so a fleet frame never touches them", () => {
    const server = hostingQueryKeys.server("server-1");
    const key = hostingQueryKeys.costOwnerOptions("server-1", "assoc");
    expect(key.slice(0, server.length)).toEqual([...server]);
    expect(key.slice(0, 5)).toEqual(["hosting", "servers", "server-1", "cost-owner", "options"]);
    expect(hostingQueryKeys.costOwnerOptions("server-1", "a")).not.toEqual(
      hostingQueryKeys.costOwnerOptions("server-1", "b"),
    );
  });

  it("builds every owner options key from the prefix a prefix invalidation uses", () => {
    const prefix = hostingQueryKeys.costOwnerOptionsPrefix("server-1");
    expect(prefix).toEqual(["hosting", "servers", "server-1", "cost-owner", "options"]);
    for (const search of ["", "a", "assoc"])
      expect(hostingQueryKeys.costOwnerOptions("server-1", search)).toEqual([...prefix, search]);
  });

  it("keeps the access query keys under the server key so a fleet frame never touches them", () => {
    const server = hostingQueryKeys.server("server-1");
    const keys = [
      hostingQueryKeys.access("server-1"),
      hostingQueryKeys.accessCandidates("server-1", "user", "oper"),
      hostingQueryKeys.accessCandidates("server-1", "role", ""),
    ];
    for (const key of keys) expect(key.slice(0, server.length)).toEqual([...server]);
    expect(hostingQueryKeys.accessCandidates("server-1", "user", "oper").slice(0, 4)).toEqual([
      ...hostingQueryKeys.access("server-1"),
    ]);
    expect(hostingQueryKeys.accessCandidates("server-1", "user", "a")).not.toEqual(
      hostingQueryKeys.accessCandidates("server-1", "role", "a"),
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
