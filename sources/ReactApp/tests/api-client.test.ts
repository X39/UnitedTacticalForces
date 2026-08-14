import { ApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

describe("API request adapter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("sends cookies, the React variant, and JSON through the configured base", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await new ApiClient(configuration).request<{ ok: boolean }>("events", {
      method: "POST",
      body: { title: "Operation" },
    });

    expect(result).toEqual({ ok: true });
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/events");
    expect(init.credentials).toBe("include");
    expect(new Headers(init.headers).get("X-UTF-UI-Variant")).toBe("react");
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
    expect(init.body).toBe(JSON.stringify({ title: "Operation" }));
  });

  it("normalizes problem details and anonymous empty responses", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ detail: "Conflict", traceId: "trace-1" }), {
          status: 409,
          headers: { "Content-Type": "application/problem+json" },
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const client = new ApiClient(configuration);

    await expect(client.request("users/me", { expectedStatuses: [401] })).resolves.toBeUndefined();
    await expect(client.request("events")).rejects.toMatchObject({
      message: "Conflict",
      status: 409,
      code: "conflict",
      traceId: "trace-1",
    });
  });

  it("normalizes transport failures", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    await expect(new ApiClient(configuration).request("events")).rejects.toMatchObject({
      status: 0,
      code: "network",
    });
  });

  it("supports form encoding and empty successful responses", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    const body = new URLSearchParams({ title: "Operation" });

    await expect(
      new ApiClient(configuration).request("events", { method: "POST", body }),
    ).resolves.toBeUndefined();
    const [, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(init.body).toBe(body);
    expect(new Headers(init.headers).has("Content-Type")).toBe(false);
  });

  it("normalizes binary download failures and transport errors", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ detail: "Slow down", traceId: "trace-2" }), {
          status: 429,
          headers: { "Content-Type": "application/problem+json" },
        }),
      )
      .mockRejectedValueOnce(new TypeError("offline"));
    vi.stubGlobal("fetch", fetchMock);
    const client = new ApiClient(configuration);

    await expect(client.download("events/download")).rejects.toMatchObject({
      message: "Slow down",
      status: 429,
      code: "rate-limited",
      traceId: "trace-2",
    });
    await expect(client.download("events/download")).rejects.toMatchObject({
      status: 0,
      code: "network",
    });
  });
});
