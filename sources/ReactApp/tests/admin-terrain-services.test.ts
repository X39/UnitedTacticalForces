import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  createTerrain,
  deactivateTerrain,
  loadTerrain,
  loadTerrainWindow,
  updateTerrain,
} from "@/features/admin/terrains";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

describe("terrain API adapter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("loads the active search window and total count through typed POST endpoints", async () => {
    const fetchMock = vi.fn().mockImplementation((url: URL) =>
      Promise.resolve(
        url.pathname.endsWith("/count")
          ? jsonResponse(9)
          : jsonResponse([
              {
                primaryKey: 41,
                gameIdentifier: "arma3",
                title: "Altis",
                imageAssetFk: null,
                imagePath: null,
                isActive: true,
              },
            ]),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await expect(loadTerrainWindow(" alt ", 80)).resolves.toEqual({
      totalCount: 9,
      terrains: [
        {
          primaryKey: 41,
          gameIdentifier: "arma3",
          title: "Altis",
          imageAssetFk: "",
          imagePath: "",
          isActive: true,
        },
      ],
    });

    const calls = fetchMock.mock.calls as [URL, RequestInit][];
    const listCall = calls.find(([url]) => url.pathname === "/api/terrains/all");
    const countCall = calls.find(([url]) => url.pathname === "/api/terrains/all/count");
    expect(listCall?.[0].searchParams.get("search")).toBe("alt");
    expect(listCall?.[0].searchParams.get("take")).toBe("80");
    expect(listCall?.[1].method).toBe("POST");
    expect(countCall?.[1].method).toBe("POST");
    expect(new Headers(listCall?.[1].headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("maps a missing terrain detail to the not-found state", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })));
    initializeApiClient(configuration);
    await expect(loadTerrain("9223372036854775807")).resolves.toBeUndefined();
  });

  it("uses the authoritative create, update, and deactivate endpoints", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        jsonResponse({
          primaryKey: 41,
          gameIdentifier: "arma3",
          title: "Altis",
          imageAssetFk: "c9a541fe-e715-4643-b085-d655514e518b",
          imagePath: "media-assets/c9a541fe-e715-4643-b085-d655514e518b/content",
          isActive: true,
        }),
      )
      .mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await createTerrain(
      { gameIdentifier: " arma3 ", title: " Altis " },
      "c9a541fe-e715-4643-b085-d655514e518b",
    );
    await updateTerrain("41", {
      title: "Altis 2035",
      imageAssetFk: "4d9c567f-b008-4f84-8ee2-2a938405bb2a",
    });
    await deactivateTerrain("41");

    const calls = fetchMock.mock.calls as [URL, RequestInit][];
    expect(calls.map(([url]) => url.pathname)).toEqual([
      "/api/terrains/create",
      "/api/terrains/41/update",
      "/api/terrains/41/delete",
    ]);
    expect(calls.map(([, init]) => init.method)).toEqual(["POST", "POST", "POST"]);
    expect(JSON.parse(String(calls[0]?.[1].body))).toEqual({
      gameIdentifier: "arma3",
      title: "Altis",
      imageAssetFk: "c9a541fe-e715-4643-b085-d655514e518b",
    });
    expect(JSON.parse(String(calls[1]?.[1].body))).toEqual({
      title: "Altis 2035",
      imageAssetFk: "4d9c567f-b008-4f84-8ee2-2a938405bb2a",
    });
  });
});
