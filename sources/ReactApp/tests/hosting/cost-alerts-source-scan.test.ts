import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

/**
 * The accrued cost and cost alert files: the browser displays what the control plane sent and never polls (D-02,
 * D-07). The alert files of the later Phase 66 plans are appended to this list by those plans.
 */
const noPollingFiles = [
  "components/server/AccruedCost.tsx",
  "features/hosting/accruedModel.ts",
  "features/hosting/useAccruedLive.ts",
  "features/hosting/costFrames.ts",
  "features/hosting/useAccruedStoppedAnnouncement.ts",
  "components/server/CostAlertBanner.tsx",
  "components/server/CostAlertMenuItem.tsx",
  "features/hosting/useCostAlerts.ts",
];

/** An interval timer or a query refetch interval: either would be client polling. */
const forbiddenPolling = /\bsetInterval\b|\brefetchInterval\b/;

/** Removes block and line comments so prose in documentation cannot trigger or hide a finding. */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
}

/**
 * Lists the polling patterns found in a source text.
 *
 * @param source The source text to scan.
 */
function findPolling(source: string): string[] {
  const match = forbiddenPolling.exec(stripComments(source));
  return match ? [match[0]] : [];
}

describe("cost alerts source scan", () => {
  it("finds polling in an inline bad sample, so the scan is live", () => {
    expect(findPolling("window.setInterval(tick, 1000);")).toEqual(["setInterval"]);
    expect(findPolling("useQuery({ queryKey, refetchInterval: 15_000 });")).toEqual([
      "refetchInterval",
    ]);
  });

  it("ignores comments and similar words in a clean sample", () => {
    const clean = [
      "/** There is no setInterval and no refetchInterval here. */",
      "// refetchInterval is never set",
      "const intervalLabel = 3;",
    ].join("\n");
    expect(findPolling(clean)).toEqual([]);
  });

  it.each(noPollingFiles)("%s holds no interval timer and no refetch interval", (file) => {
    const source = readFileSync(resolve(sourceRoot, file), "utf8");
    expect(source.length).toBeGreaterThan(0);
    expect(findPolling(source)).toEqual([]);
  });
});
