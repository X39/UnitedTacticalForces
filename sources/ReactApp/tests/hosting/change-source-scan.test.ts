import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

/** The files of the staged-change flow that must never persist or log draft or secret data. */
const draftFiles = [
  "features/hosting/serverChangeDraft.ts",
  "features/hosting/useServerChangeDraft.tsx",
  "features/hosting/alertSettingsModel.ts",
];

/** The files that show results to the member: they must additionally never print the text of an error. */
const surfaceFiles = [
  "features/hosting/useServerChangeApply.ts",
  "components/server/PendingChangesBar.tsx",
  "components/server/ReviewChangesDialog.tsx",
  "components/server/ChangeDiffItem.tsx",
  "features/hosting/sections/SettingsSection.tsx",
  "features/hosting/CostAlertSettingsPanel.tsx",
  "features/hosting/CostAlertsPage.tsx",
];

/** Browser persistence, cookies and console output: none may appear in code that holds drafts or secrets. */
const forbiddenPersistence =
  /\b(?:localStorage|sessionStorage|indexedDB)\b|document\s*\.\s*cookie|\bconsole\s*\./;

/** Reading the text of an error: it can echo submitted keys or values, so it is never printed. */
const forbiddenMessage = /\.\s*message\b/;

/** Removes block and line comments so prose in documentation cannot trigger or hide a finding. */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
}

/**
 * Lists the patterns found in a source text, as `name: matched text`.
 *
 * @param source The source text to scan.
 * @param checkMessage Whether reading an error text is a violation as well.
 */
function findViolations(source: string, checkMessage = true): string[] {
  const code = stripComments(source);
  const found: string[] = [];
  const persistence = forbiddenPersistence.exec(code);
  if (persistence) found.push(`persistence: ${persistence[0]}`);
  const message = checkMessage ? forbiddenMessage.exec(code) : null;
  if (message) found.push(`message: ${message[0]}`);
  return found;
}

describe("staged-change source scan", () => {
  it("finds a forbidden pattern in an inline bad sample, so the scan is live", () => {
    expect(findViolations('window.localStorage.setItem("draft", value);')).toHaveLength(1);
    expect(findViolations("sessionStorage.clear();")).toHaveLength(1);
    expect(findViolations("const db = indexedDB.open(name);")).toHaveLength(1);
    expect(findViolations("document.cookie = value;")).toHaveLength(1);
    expect(findViolations("console.log(secret);")).toHaveLength(1);
    expect(findViolations("setText(error.message);")).toHaveLength(1);
  });

  it("ignores comments and similar words in a clean sample", () => {
    const clean = [
      "/** Never writes to localStorage and never reads error.message. */",
      "// console.log is not used here",
      "const consoleWidth = 3;",
      "const messageCount = 2;",
    ].join("\n");
    expect(findViolations(clean)).toEqual([]);
  });

  it.each(draftFiles)("%s holds no storage, cookie or console access", (file) => {
    const source = readFileSync(resolve(sourceRoot, file), "utf8");
    expect(source.length).toBeGreaterThan(0);
    expect(findViolations(source, false)).toEqual([]);
  });

  it.each(surfaceFiles)("%s holds no storage, cookie, console or error-text access", (file) => {
    const source = readFileSync(resolve(sourceRoot, file), "utf8");
    expect(source.length).toBeGreaterThan(0);
    expect(findViolations(source)).toEqual([]);
  });
});
