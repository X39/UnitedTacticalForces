import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { ApiError, initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { hostingQueryKeys } from "@/features/hosting/services";
import {
  costOwnerTransferFailure,
  transferBlocker,
  useCostOwnerTransfer,
} from "@/features/hosting/useCostOwnerTransfer";
import { fleetServer } from "../helpers/fleetFixtures";

/*
 * Tracer for D-14: one transfer runs from the hook through the service, refreshes exactly the server, the fleet,
 * the accrued cost and the owner options, and every failure is classified by status without showing server text.
 */

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const serverId = "f1ee7000-0000-4000-8000-000000000001";
const costOwnerId = "c0000000-0000-4000-8000-000000000002";
const optionsPrefix = ["hosting", "servers", serverId, "cost-owner", "options"];

function problem(status: number): Response {
  return new Response(JSON.stringify({ error: "Do not show this server text." }), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** Renders the hook under a query client whose `invalidateQueries` is spied on. */
function renderTransfer() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidate = vi.spyOn(queryClient, "invalidateQueries");
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const view = renderHook(() => useCostOwnerTransfer(serverId), { wrapper });
  return { ...view, invalidate };
}

describe("cost owner transfer", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("puts the chosen owner to the server endpoint and refreshes exactly the four affected entries", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);
    const { result, invalidate } = renderTransfer();

    await result.current.mutateAsync({ costOwnerId });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe(`/api/game-servers/${serverId}/cost-owner`);
    expect(init.method).toBe("PUT");
    expect(init.body).toBe(JSON.stringify({ costOwnerId }));
    await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(4));
    expect(invalidate.mock.calls.map(([filters]) => filters)).toEqual([
      { queryKey: hostingQueryKeys.server(serverId), exact: true },
      { queryKey: hostingQueryKeys.fleet, exact: true },
      { queryKey: hostingQueryKeys.accruedCost(serverId), exact: true },
      { queryKey: optionsPrefix },
    ]);
  });

  it("counts a 204 for the unchanged owner as a success", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
    initializeApiClient(configuration);
    const { result } = renderTransfer();

    await expect(result.current.mutateAsync({ costOwnerId })).resolves.toBeUndefined();
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it.each([
    [409, "conflict"],
    [400, "unavailable"],
    [403, "forbidden"],
    [404, "generic"],
    [500, "generic"],
  ] as const)("classifies HTTP %i as %s", async (status, expected) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(problem(status)));
    initializeApiClient(configuration);
    const { result } = renderTransfer();

    await expect(result.current.mutateAsync({ costOwnerId })).rejects.toBeInstanceOf(ApiError);
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(costOwnerTransferFailure(result.current.error)).toBe(expected);
  });

  it("classifies a network failure and a foreign error as generic", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));
    initializeApiClient(configuration);
    const { result } = renderTransfer();

    await expect(result.current.mutateAsync({ costOwnerId })).rejects.toBeInstanceOf(ApiError);
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(costOwnerTransferFailure(result.current.error)).toBe("generic");
    expect(costOwnerTransferFailure(new Error("boom"))).toBe("generic");
    expect(costOwnerTransferFailure(undefined)).toBe("generic");
  });

  it("refetches the exact server after a conflict and nothing else", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(problem(409)));
    initializeApiClient(configuration);
    const { result, invalidate } = renderTransfer();

    await expect(result.current.mutateAsync({ costOwnerId })).rejects.toBeInstanceOf(ApiError);

    await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(1));
    expect(invalidate).toHaveBeenCalledWith({
      queryKey: hostingQueryKeys.server(serverId),
      exact: true,
    });
  });

  it("refetches the owner options after an unavailable owner and nothing else", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(problem(400)));
    initializeApiClient(configuration);
    const { result, invalidate } = renderTransfer();

    await expect(result.current.mutateAsync({ costOwnerId })).rejects.toBeInstanceOf(ApiError);

    await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(1));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: optionsPrefix });
  });

  it("invalidates nothing after a forbidden or generic failure", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(problem(403)));
    initializeApiClient(configuration);
    const { result, invalidate } = renderTransfer();

    await expect(result.current.mutateAsync({ costOwnerId })).rejects.toBeInstanceOf(ApiError);
    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(invalidate).not.toHaveBeenCalled();
  });
});

describe("transfer availability", () => {
  it("blocks as running for any unresolved deployment, also a terminated one", () => {
    expect(transferBlocker(fleetServer({ status: "Running" }))).toBe("running");
    expect(transferBlocker(fleetServer({ status: "Provisioning" }))).toBe("running");
    expect(transferBlocker(fleetServer({ status: "Terminated" }))).toBe("running");
    expect(transferBlocker(fleetServer({ status: "Stopped", pendingJobCount: 2 }))).toBe("running");
  });

  it("blocks as jobs when no deployment exists and a job is pending", () => {
    expect(transferBlocker(fleetServer({ pendingJobCount: 1 }))).toBe("jobs");
  });

  it("allows a transfer for a server without a deployment and without pending jobs", () => {
    expect(transferBlocker(fleetServer())).toBeNull();
    expect(transferBlocker(fleetServer({ status: "Running", noDeployment: true }))).toBeNull();
  });
});
