import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  loadCommandFieldReport,
  loadCommandFieldReports,
  runFieldReportAction,
  saveCommandFieldReport,
  type FieldReportFormValues,
} from "@/features/admin/fieldReports";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};
const reportId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
const form: FieldReportFormValues = {
  slug: "northstar",
  title: "Northstar",
  excerpt: "Summary",
  category: "After Action",
  markdown: "# Report",
  heroImageAssetFk: "",
  heroImagePath: "",
  heroImageAlt: "",
  isFeatured: false,
  sortOrder: 1,
  isPublished: false,
  publicationInput: "",
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

describe("field-report API adapter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("loads the searchable ledger and treats a missing detail as an empty result", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        jsonResponse({ reports: [{ primaryKey: reportId, title: "Northstar" }] }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 404 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await expect(loadCommandFieldReports(" north ")).resolves.toMatchObject({
      reports: [{ primaryKey: reportId, title: "Northstar" }],
    });
    await expect(loadCommandFieldReport(reportId)).resolves.toBeUndefined();

    const [ledgerUrl, ledgerInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(ledgerUrl.pathname).toBe("/api/command-center/field-reports-snapshot");
    expect(ledgerUrl.searchParams.get("take")).toBe("160");
    expect(ledgerUrl.searchParams.get("search")).toBe("north");
    expect(new Headers(ledgerInit.headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("uses the typed create, update, publish, delete, and restore endpoints", async () => {
    const response = { primaryKey: reportId, title: "Northstar" };
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse(response)));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await saveCommandFieldReport(undefined, form);
    await saveCommandFieldReport(reportId, form);
    await runFieldReportAction(reportId, "publish");
    await runFieldReportAction(reportId, "delete");
    await runFieldReportAction(reportId, "restore");

    const calls = fetchMock.mock.calls as [URL, RequestInit][];
    expect(calls.map(([url]) => url.pathname)).toEqual([
      "/api/field-reports",
      `/api/field-reports/admin/${reportId}`,
      `/api/field-reports/admin/${reportId}/publish`,
      `/api/field-reports/admin/${reportId}`,
      `/api/field-reports/admin/${reportId}/restore`,
    ]);
    expect(calls.map(([, init]) => init.method)).toEqual(["POST", "PUT", "POST", "DELETE", "POST"]);
    expect(JSON.parse(String(calls[0]?.[1].body))).toMatchObject({ title: "Northstar" });
  });
});
