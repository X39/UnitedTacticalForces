import { resolve } from "node:path";
import {
  isTierA,
  loadFamilies,
  loadRules,
  scanSource,
  scanTree,
  type FileCounts,
} from "../ratchets/scan";

const projectRoot = resolve(import.meta.dirname, "..", "..");
const ratchetDirectory = resolve(import.meta.dirname, "..", "ratchets");
const rules = loadRules(ratchetDirectory);
const tierAPath = "src/features/hosting/Sample.tsx";

function literalsOf(source: string, path = tierAPath): number {
  return scanSource(path, source, rules).literals;
}

/** Lists the files whose literal count is above their family ceiling, split by quality tier. */
function filesAboveCeiling(tierA: boolean): string[] {
  const ceilings = new Map<string, FileCounts>();
  for (const family of loadFamilies(ratchetDirectory)) {
    for (const [path, entry] of Object.entries(family.files)) ceilings.set(path, entry);
  }
  const above: string[] = [];
  for (const [path, counts] of scanTree(projectRoot, rules)) {
    if (isTierA(path, rules) !== tierA) continue;
    const ceiling = ceilings.get(path)?.literals ?? 0;
    if (counts.literals > ceiling) {
      above.push(`${path}: ${counts.literals} literal text occurrence(s), ceiling ${ceiling}`);
    }
  }
  return above;
}

describe("hard-coded user-facing text scan", () => {
  it("counts JSX text with letters in an inline Tier A sample", () => {
    expect(literalsOf("export const A = () => <h2>Terrain and scenario</h2>;")).toBe(1);
  });

  it("counts literal values of the user-facing attributes", () => {
    const source = [
      "export const A = () => (",
      "  <>",
      '    <Field label="Scenario" />',
      '    <input placeholder="Search scenarios" aria-label="Search" />',
      '    <img alt="Preview of the terrain" title={"Terrain"} />',
      "  </>",
      ");",
    ].join("\n");

    expect(literalsOf(source)).toBe(5);
  });

  it("counts a literal string passed to the toast or the announcer", () => {
    const source = [
      "export function save(showToast: (m: string) => void, announce: (m: string) => void) {",
      '  showToast("Saved the server");',
      "  announce(`Removed the schedule`);",
      "  toast.showToast('Done');",
      "}",
    ].join("\n");

    expect(literalsOf(source)).toBe(3);
  });

  it("ignores numbers, neutral tokens, code-like elements and translated values", () => {
    const source = [
      "export const A = () => (",
      "  <>",
      "    <span>42</span>",
      "    <span>8 vCPU</span>",
      "    <span>16 GiB</span>",
      "    <span>EN</span>",
      "    <span>·</span>",
      "    <code>steamcmd +login anonymous</code>",
      "    <kbd>Enter</kbd>",
      "    <ApiText>Ubuntu Server</ApiText>",
      '    <Field label={t("Common_Name")} placeholder={placeholder} />',
      '    <input aria-label="" title="42" />',
      "  </>",
      ");",
      'export const run = () => showToast(t("Server_Saved"));',
      "export const dynamic = (name: string) => announce(`${name}`);",
    ].join("\n");

    expect(literalsOf(source)).toBe(0);
  });

  it("counts German letters as text", () => {
    expect(literalsOf("export const A = () => <p>Größe wählen</p>;")).toBe(1);
  });

  it("holds the literal text of every Tier A file at or below its ceiling", () => {
    expect(filesAboveCeiling(true)).toEqual([]);
  });

  it("holds the literal text of every other file at or below its ceiling", () => {
    expect(filesAboveCeiling(false)).toEqual([]);
  });
});
