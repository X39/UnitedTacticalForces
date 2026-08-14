import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  createKassenbuchEntry,
  createKassenbuchPayload,
  dateFilterInstant,
  deleteKassenbuchEntry,
  findKassenbuchEntry,
  loadKassenbuchPage,
  normalizeKassenbuchDirection,
  normalizeKassenbuchPage,
  updateKassenbuchEntry,
  validateKassenbuchForm,
  type KassenbuchFormValues,
} from "@/features/admin/vereinKassenbuch";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const validForm: KassenbuchFormValues = {
  bookedOn: "2026-08-14",
  direction: "Expense",
  amount: "125.50",
  currency: " eur ",
  category: " Infrastructure ",
  description: " Voice server renewal ",
  reference: " Invoice 42 ",
  documentNumber: " UTF-2026-42 ",
};

const entryDto = {
  primaryKey: 42,
  timeStampBooked: "2026-08-13T22:00:00Z",
  direction: "Expense" as const,
  amount: 125.5,
  currency: "eur",
  category: "Infrastructure",
  description: "Voice server renewal",
  reference: "Invoice 42",
  documentNumber: "UTF-2026-42",
  isDeleted: false,
};

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json" },
  });
}

describe("Kassenbuch API adapter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("normalizes legacy directions, optional values, and totals", () => {
    expect(normalizeKassenbuchDirection(1)).toBe("Expense");
    expect(normalizeKassenbuchDirection("0")).toBe("Income");
    expect(
      normalizeKassenbuchPage({
        entries: [{ ...entryDto, direction: 1 as never, currency: " eur " }],
        totalCount: 1,
        incomeTotal: 0,
        expenseTotal: 125.5,
        balance: -125.5,
        currency: " eur ",
      }),
    ).toMatchObject({
      totalCount: 1,
      balance: -125.5,
      currency: "EUR",
      entries: [{ direction: "Expense", currency: "EUR", reference: "Invoice 42" }],
    });
  });

  it("validates and trims a complete payload", () => {
    expect(validateKassenbuchForm(validForm)).toBeUndefined();
    expect(validateKassenbuchForm({ ...validForm, amount: "0" })).toBe("amount");
    expect(validateKassenbuchForm({ ...validForm, currency: "EU" })).toBe("currency");
    expect(validateKassenbuchForm({ ...validForm, bookedOn: "2026-02-31" })).toBe("date");
    expect(createKassenbuchPayload(validForm)).toMatchObject({
      direction: "Expense",
      amount: 125.5,
      currency: "EUR",
      category: "Infrastructure",
      description: "Voice server renewal",
      reference: "Invoice 42",
      documentNumber: "UTF-2026-42",
    });
    expect(dateFilterInstant("2026-08-14", true)).toMatch(/^2026-08-14T.*:59\.999Z$/);
  });

  it("sends typed filters and all authoritative mutations", async () => {
    const fetchMock = vi.fn().mockImplementation((url: URL, init: RequestInit) => {
      if (!init.method || init.method === "GET")
        return Promise.resolve(
          jsonResponse({
            entries: [entryDto],
            totalCount: 1,
            incomeTotal: 0,
            expenseTotal: 125.5,
            balance: -125.5,
            currency: "EUR",
          }),
        );
      return Promise.resolve(jsonResponse(entryDto));
    });
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const page = await loadKassenbuchPage({
      skip: 20,
      take: 100,
      from: "2026-08-01T00:00:00Z",
      to: "2026-08-31T23:59:59Z",
      search: " invoice ",
      category: " infrastructure ",
    });
    await createKassenbuchEntry(createKassenbuchPayload(validForm));
    await updateKassenbuchEntry(42, createKassenbuchPayload(validForm));
    await deleteKassenbuchEntry(42);

    expect(page.entries[0]?.primaryKey).toBe(42);
    const calls = fetchMock.mock.calls as [URL, RequestInit][];
    expect(calls.map(([, init]) => init.method ?? "GET")).toEqual(["GET", "POST", "PUT", "DELETE"]);
    expect(calls.map(([url]) => url.pathname)).toEqual([
      "/api/verein/kassenbuch",
      "/api/verein/kassenbuch",
      "/api/verein/kassenbuch/42",
      "/api/verein/kassenbuch/42",
    ]);
    expect(calls[0]?.[0].searchParams.get("search")).toBe("invoice");
    expect(calls[0]?.[0].searchParams.get("category")).toBe("infrastructure");
    expect(new Headers(calls[1]?.[1].headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("pages through the ledger when locating an edit target", async () => {
    const firstPage = Array.from({ length: 200 }, (_, index) => ({
      ...entryDto,
      primaryKey: index + 1,
    }));
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ entries: firstPage, totalCount: 201, currency: "EUR" }))
      .mockResolvedValueOnce(
        jsonResponse({
          entries: [{ ...entryDto, primaryKey: 201 }],
          totalCount: 201,
          currency: "EUR",
        }),
      );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await expect(findKassenbuchEntry(201)).resolves.toMatchObject({ primaryKey: 201 });
    expect((fetchMock.mock.calls[1]?.[0] as URL).searchParams.get("skip")).toBe("200");
  });
});
