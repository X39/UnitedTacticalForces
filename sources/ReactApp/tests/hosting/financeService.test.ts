import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { financeService } from "@/features/hosting/financeService";
import { beforeEach } from "vitest";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

function jsonResponse(value: unknown): Response {
  return new Response(JSON.stringify(value), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}

describe("hosting finance API adapter", () => {
  beforeEach(() => {
    vi.stubGlobal("crypto", { randomUUID: vi.fn(() => "90000000-0000-0000-0000-000000000001") });
  });

  afterEach(() => vi.unstubAllGlobals());

  it("normalizes owner pages and legacy enum values", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        owners: [
          {
            costOwnerId: "10000000-0000-0000-0000-000000000001",
            kind: 0,
            recentLedgerEntries: [{ id: "entry-1", kind: 3 }],
          },
        ],
        totalCount: 1,
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const result = await financeService.owners(" Alpha ", 0, 100);

    expect(result.owners[0]?.kind).toBe("User");
    expect(result.owners[0]?.recentLedgerEntries[0]?.kind).toBe("ExternalRefund");
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/cost-owners");
    expect(url.searchParams.get("search")).toBe("Alpha");
    expect(url.searchParams.get("take")).toBe("50");
    expect(new Headers(init.headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("records an externally completed refund and refreshes the selected owner", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ id: "entry-1", kind: "ExternalRefund" }))
      .mockResolvedValueOnce(
        jsonResponse({
          costOwnerId: "owner-1",
          kind: "User",
          recentLedgerEntries: [],
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await financeService.recordRefund("owner-1", 25, "SEPA-42", "Member request");

    const [mutationUrl, mutationInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [ownerUrl] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(mutationUrl.pathname).toBe("/api/cost-owners/owner-1/refunds");
    expect(mutationInit.method).toBe("POST");
    expect(JSON.parse(String(mutationInit.body))).toMatchObject({
      recordIdentifier: "90000000-0000-0000-0000-000000000001",
      amount: 25,
      currency: "EUR",
      externalReference: "SEPA-42",
      reason: "Member request",
    });
    expect(ownerUrl.pathname).toBe("/api/cost-owners/owner-1");
  });

  it("uses balanced correction and linked reversal endpoints with fresh operation IDs", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        jsonResponse({
          operationIdentifier: "operation-1",
          affectedLedgerEntries: null,
          entries: null,
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          operationIdentifier: "operation-2",
          affectedLedgerEntries: [],
          entries: [],
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const correction = await financeService.correctDeploymentCostOwner(
      "source-1",
      "deployment-1",
      "destination-1",
      4.5,
      "Attribution repair",
    );
    await financeService.reverseLedgerEntry("source-1", "entry-1", "Mistaken payment");

    expect(correction.affectedLedgerEntries).toEqual([]);
    expect(correction.entries).toEqual([]);
    const [correctionUrl, correctionInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [reversalUrl, reversalInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(correctionUrl.pathname).toBe(
      "/api/cost-owners/source-1/deployments/deployment-1/corrections",
    );
    expect(JSON.parse(String(correctionInit.body))).toMatchObject({
      operationIdentifier: "90000000-0000-0000-0000-000000000001",
      correctCostOwnerId: "destination-1",
      amount: 4.5,
    });
    expect(reversalUrl.pathname).toBe("/api/cost-owners/source-1/ledger-entries/entry-1/reversals");
    expect(JSON.parse(String(reversalInit.body))).toMatchObject({
      operationIdentifier: "90000000-0000-0000-0000-000000000001",
      reason: "Mistaken payment",
    });
  });
});
