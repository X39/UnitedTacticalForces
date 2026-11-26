import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

/**
 * Every file of the automation feature: the browser shows what the control plane sent and never polls (UI-SPEC
 * freshness rules). The notice and the profile menu row read pushed frames and the server query, and the only clock
 * is the shared `useNow` hook, which is not part of this list.
 */
const noPollingFiles = [
  "features/hosting/automationModel.ts",
  "features/hosting/useAutomationSettings.ts",
  "features/hosting/useAutoStopNotice.ts",
  "features/hosting/AutomationPanel.tsx",
  "components/server/AutomationStatusList.tsx",
  "components/server/ScheduleRuleFields.tsx",
  "components/server/ScheduledStartConsentDialog.tsx",
  "components/server/AutoStopNotice.tsx",
];

/**
 * The files that hold the automation draft: the unsaved draft lives in React state only and is never written to
 * storage, the address bar, the router state or a log (UI-SPEC draft rules, T-68-42).
 */
const draftFiles = [
  "features/hosting/AutomationPanel.tsx",
  "components/server/ScheduleRuleFields.tsx",
  "features/hosting/useAutomationSettings.ts",
];

/** An interval timer or a query refetch interval: either would be client polling. */
const forbiddenPolling = /\bsetInterval\b|\brefetchInterval\b/;

/**
 * Every way the draft could leave React state: web storage, the history API, a search parameter write, the router
 * state of a navigation and any console call.
 */
const forbiddenDraftSinks =
  /\blocalStorage\b|\bsessionStorage\b|\bindexedDB\b|\bhistory\.(?:pushState|replaceState)\b|\bURLSearchParams\b|\bsetSearchParams\b|\blocation\.state\b|\bnavigate\s*\(|\bconsole\s*\./;

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

/**
 * Lists the places a draft could be persisted or logged in a source text.
 *
 * @param source The source text to scan.
 */
function findDraftSinks(source: string): string[] {
  const match = forbiddenDraftSinks.exec(stripComments(source));
  return match ? [match[0].replace(/\s+/g, "")] : [];
}

/**
 * Reads one source file of the app.
 *
 * @param file The path below `src`.
 */
function read(file: string): string {
  return readFileSync(resolve(sourceRoot, file), "utf8");
}

describe("automation source scan: no polling", () => {
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
    const source = read(file);
    expect(source.length).toBeGreaterThan(0);
    expect(findPolling(source)).toEqual([]);
  });
});

describe("automation source scan: the draft stays in React state", () => {
  it.each([
    ["localStorage.setItem('draft', value);", "localStorage"],
    ["sessionStorage.getItem('draft');", "sessionStorage"],
    ["window.history.replaceState(null, '', url);", "history.replaceState"],
    ["const params = new URLSearchParams(location.search);", "URLSearchParams"],
    ["setSearchParams({ idle: draft.idleMinutes });", "setSearchParams"],
    ["navigate('/x', { state: draft });", "navigate("],
    ["console.log(draft);", "console."],
  ])("finds the sink in the inline bad sample %s, so the scan is live", (sample, expected) => {
    expect(findDraftSinks(sample)).toEqual([expected]);
  });

  it("ignores comments and similar words in a clean sample", () => {
    const clean = [
      "/** The draft is never written to localStorage or logged with console.log. */",
      "// sessionStorage is not used",
      "const consoleLabel = 'navigate later';",
    ].join("\n");
    expect(findDraftSinks(clean)).toEqual([]);
  });

  it.each(draftFiles)(
    "%s never writes the draft to storage, the URL, router state or a log",
    (file) => {
      const source = read(file);
      expect(source.length).toBeGreaterThan(0);
      expect(findDraftSinks(source)).toEqual([]);
    },
  );
});
