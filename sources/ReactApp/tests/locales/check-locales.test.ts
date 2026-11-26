import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { beforeEach } from "vitest";

const scriptPath = resolve(import.meta.dirname, "..", "..", "scripts", "check-locales.mjs");

type Flat = Record<string, string>;

let directory = "";

/**
 * Runs the checker against temporary locale files.
 *
 * @param en The English values by key.
 * @param de The German values by key.
 * @param allow The content of the identical-values allow list.
 * @param englishAllow The content of the English function word allow list.
 * @param sharedWords The content of the shared-words allow list; it is written to the temporary directory so no
 * temporary test reads the real list.
 * @returns The exit status and the combined output.
 */
function run(
  en: Flat,
  de: Flat,
  allow: Flat = {},
  englishAllow: Flat = {},
  sharedWords: Flat = {},
) {
  const enPath = join(directory, "en.json");
  const dePath = join(directory, "de.json");
  const allowPath = join(directory, "identical-allowed.json");
  const englishAllowPath = join(directory, "english-words-allowed.json");
  const sharedWordsPath = join(directory, "shared-words-allowed.json");
  writeFileSync(enPath, JSON.stringify(en));
  writeFileSync(dePath, JSON.stringify(de));
  writeFileSync(allowPath, JSON.stringify(allow));
  writeFileSync(englishAllowPath, JSON.stringify(englishAllow));
  writeFileSync(sharedWordsPath, JSON.stringify(sharedWords));
  const result = spawnSync(
    process.execPath,
    [
      scriptPath,
      "--en",
      enPath,
      "--de",
      dePath,
      "--allow",
      allowPath,
      "--english-allow",
      englishAllowPath,
      "--shared-words",
      sharedWordsPath,
    ],
    { encoding: "utf8" },
  );
  return { status: result.status, output: `${result.stdout}${result.stderr}` };
}

/**
 * Runs the unused-key check against a sample source directory.
 *
 * @param keys The English keys; the German values are the key names so no identical-value failure occurs.
 * @param sources The sample source files by relative path.
 * @returns The exit status and the combined output; the shared-words allow list is an empty temporary file.
 */
function runUnused(keys: readonly string[], sources: Record<string, string>) {
  const en = Object.fromEntries(keys.map((key) => [key, `English ${key}`]));
  const de = Object.fromEntries(keys.map((key) => [key, `Deutsch ${key}`]));
  const enPath = join(directory, "en.json");
  const dePath = join(directory, "de.json");
  const allowPath = join(directory, "identical-allowed.json");
  const srcPath = join(directory, "src");
  writeFileSync(enPath, JSON.stringify(en));
  writeFileSync(dePath, JSON.stringify(de));
  writeFileSync(allowPath, JSON.stringify({}));
  const englishAllowPath = join(directory, "english-words-allowed.json");
  writeFileSync(englishAllowPath, JSON.stringify({}));
  const sharedWordsPath = join(directory, "shared-words-allowed.json");
  writeFileSync(sharedWordsPath, JSON.stringify({}));
  mkdirSync(srcPath, { recursive: true });
  for (const [name, text] of Object.entries(sources)) writeFileSync(join(srcPath, name), text);
  const result = spawnSync(
    process.execPath,
    [
      scriptPath,
      "--en",
      enPath,
      "--de",
      dePath,
      "--allow",
      allowPath,
      "--english-allow",
      englishAllowPath,
      "--shared-words",
      sharedWordsPath,
      "--unused",
      "--src",
      srcPath,
    ],
    { encoding: "utf8" },
  );
  return { status: result.status, output: `${result.stdout}${result.stderr}` };
}

describe("check-locales", () => {
  beforeEach(() => {
    directory = mkdtempSync(join(tmpdir(), "utf-locales-"));
  });

  afterEach(() => {
    rmSync(directory, { recursive: true, force: true });
  });

  it("accepts equal key sets with equal placeholders", () => {
    const result = run(
      { Greeting: "Hello {{name}}", Count: "{0} items" },
      { Greeting: "Hallo {{name}}", Count: "{0} Einträge" },
    );

    expect(result.status).toBe(0);
    expect(result.output).not.toContain("[identical]");
  });

  it("fails on an English key without a German key", () => {
    const result = run({ Greeting: "Hello", Farewell: "Bye" }, { Greeting: "Hallo" });

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[missing-german\] Farewell/);
  });

  it("fails on an empty or whitespace German value", () => {
    const result = run({ A: "One", B: "Two" }, { A: "", B: "   " });

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[empty-german\] A/);
    expect(result.output).toMatch(/\[empty-german\] B/);
  });

  it("fails on a German key without an English key", () => {
    const result = run({ A: "One" }, { A: "Eins", Extra: "Zusatz" });

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[orphan-german\] Extra/);
  });

  it("fails when the placeholder sets differ", () => {
    const named = run({ A: "Hello {{name}}" }, { A: "Hallo" });
    const positional = run({ A: "Value {0}" }, { A: "Wert {1}" });

    expect(named.status).toBe(1);
    expect(named.output).toMatch(/\[placeholder\] A/);
    expect(positional.status).toBe(1);
    expect(positional.output).toMatch(/\[placeholder\] A/);
  });

  it("treats a format specifier as the same placeholder", () => {
    const result = run({ A: "Total {0}" }, { A: "Summe {0:N0}" });

    expect(result.status).toBe(0);
  });

  it("fails on a key containing a dot or a colon in either file", () => {
    const dotted = run({ "A.B": "One" }, { "A.B": "Eins" });
    const colon = run({ "A:B": "One" }, { "A:B": "Eins" });

    expect(dotted.status).toBe(1);
    expect(dotted.output).toMatch(/\[bad-key\] A\.B/);
    expect(colon.status).toBe(1);
    expect(colon.output).toMatch(/\[bad-key\] A:B/);
  });

  it("fails on identical values that are not allow-listed", () => {
    const failed = run({ Name: "Name" }, { Name: "Name" });

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(/\[identical\] Name - German equals English/);
  });

  it("accepts an allow-listed identical value", () => {
    const allowed = run({ Name: "Name" }, { Name: "Name" }, { Name: "same word in German" });

    expect(allowed.status).toBe(0);
    expect(allowed.output).not.toMatch(/\[identical\] Name/);
    expect(allowed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("fails on a German value that equals English apart from letter case", () => {
    const failed = run({ Total: "Total count" }, { Total: "Total Count" });

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(
      /\[identical-case\] Total - German equals English apart from letter case/,
    );
  });

  it("reports a byte-identical value only as identical, never also as identical-case", () => {
    const failed = run({ Name: "Name" }, { Name: "Name" });

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(/\[identical\] Name/);
    expect(failed.output).not.toMatch(/\[identical-case\] Name/);
  });

  it("accepts an allow-listed case-only value", () => {
    const allowed = run(
      { Total: "Total count" },
      { Total: "Total Count" },
      { Total: "technical term kept in the German UI" },
    );

    expect(allowed.status).toBe(0);
    expect(allowed.output).not.toMatch(/\[identical-case\]/);
    expect(allowed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("ignores whitespace runs when comparing apart from letter case", () => {
    const failed = run({ Total: "Total count" }, { Total: " TOTAL   count " });

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(/\[identical-case\] Total/);
  });

  it("keeps a stale allow-list entry as a warning", () => {
    const stale = run(
      { Name: "Name" },
      { Name: "Name" },
      { Name: "same word", Gone: "removed key" },
    );
    const differing = run({ Name: "Name" }, { Name: "Namen" }, { Name: "no longer identical" });

    expect(stale.status).toBe(0);
    expect(stale.output).toMatch(/\[allow-stale\] Gone/);
    expect(differing.status).toBe(0);
    expect(differing.output).toMatch(/\[allow-stale\] Name/);
  });

  it("fails on an allow-list entry without a reason", () => {
    const result = run({ Name: "Name" }, { Name: "Name" }, { Name: "  " });

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[allow-reason\] Name/);
  });

  it("fails on an English function word between German words", () => {
    const failed = run(
      { Terrain: "Terrain, mod pack and image" },
      { Terrain: "Karte, Modpaket and Bild" },
    );

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(
      /\[english-words\] Terrain - German value contains English function words: and\b/,
    );
  });

  it("names every English function word sorted and de-duplicated", () => {
    const failed = run(
      { Sentence: "One sentence here" },
      { Sentence: "This Satz has this Wort und has" },
    );

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(/\[english-words\] Sentence .*: has, this\s*$/m);
  });

  it("does not flag ordinary German words that look English", () => {
    const passed = run(
      {
        A: "Sign in",
        B: "What it was",
        C: "It will work",
        D: "Not today",
        E: "Do it",
        F: "The cat",
      },
      {
        A: "Melde dich an",
        B: "Was dieser Spielserver kann",
        C: "Er will arbeiten",
        D: "Not am Mann",
        E: "Do Donnerstag",
        F: "Die Katze in der Mitte",
      },
    );

    expect(passed.status).toBe(0);
    expect(passed.output).not.toMatch(/\[english-words\]/);
    expect(passed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("ignores function words inside placeholder names and URLs", () => {
    const passed = run(
      {
        A: "Value {{for}}",
        B: "See https://example.com/terms-of-use",
        C: "Count {0:N0} of {1}",
      },
      {
        A: "Wert {{for}}",
        B: "Siehe https://example.com/terms-of-use",
        C: "Anzahl {0:N0} von {1}",
      },
    );

    expect(passed.status).toBe(0);
    expect(passed.output).not.toMatch(/\[english-words\]/);
  });

  it("exempts a key from the function word scan through the identical allow list", () => {
    const allowed = run(
      { Brand: "Rock and Stone" },
      { Brand: "Rock and Stone" },
      { Brand: "proper noun or brand name" },
    );

    expect(allowed.status).toBe(0);
    expect(allowed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("exempts a key listed with a reason in the English function word allow list", () => {
    const allowed = run(
      { Title: "Stone and rock" },
      { Title: "Karte and Bild" },
      {},
      { Title: "product name that keeps an English conjunction" },
    );

    expect(allowed.status).toBe(0);
    expect(allowed.output).not.toMatch(/\[english-words\]/);
    expect(allowed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("fails on an English function word allow-list entry without a reason", () => {
    const result = run(
      { Title: "Stone and rock" },
      { Title: "Karte and Bild" },
      {},
      { Title: " " },
    );

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[allow-reason\] Title/);
  });

  it("warns about stale English function word allow-list entries", () => {
    const result = run(
      { Title: "Stone", Plain: "Rock" },
      { Title: "Stein", Plain: "Fels" },
      {},
      { Gone: "removed key", Title: "no longer contains a function word" },
    );

    expect(result.status).toBe(0);
    expect(result.output).toMatch(/\[allow-stale\] Gone/);
    expect(result.output).toMatch(/\[allow-stale\] Title/);
  });

  it("fails on a German value that repeats an English word of its source", () => {
    const failed = run(
      { Pending: "Mission image pending" },
      { Pending: "Mission image ausstehend" },
      {},
      {},
      { mission: "same word in German" },
    );

    expect(failed.status).toBe(1);
    expect(failed.output).toContain(
      "[english-carryover] Pending - German value repeats English words: image",
    );
  });

  it("names the carried-over words sorted and de-duplicated", () => {
    const failed = run(
      { Linked: "Linked accounts and settings" },
      { Linked: "Verknüpft accounts, settings und accounts" },
    );

    expect(failed.status).toBe(1);
    expect(failed.output).toMatch(/\[english-carryover\] Linked - .*: accounts, settings\s*$/m);
  });

  it("accepts a repeated word that the shared-words list documents", () => {
    const passed = run(
      { Open: "Open server" },
      { Open: "Server öffnen" },
      {},
      {},
      { server: "loanword used in the German UI" },
    );

    expect(passed.status).toBe(0);
    expect(passed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("does not report function words or look-alike words as carry-over", () => {
    const english = run(
      { Terrain: "Terrain, mod pack and image" },
      { Terrain: "Karte, Modpaket and Bild" },
    );
    const lookAlike = run({ A: "What it was" }, { A: "Was dieser Spielserver kann" });

    expect(english.output).toContain("[english-words]");
    expect(english.output).not.toContain("[english-carryover]");
    expect(lookAlike.status).toBe(0);
    expect(lookAlike.output).not.toContain("[english-carryover]");
  });

  it("ignores placeholders, URLs and underscore identifiers for carry-over", () => {
    const passed = run(
      {
        A: "Cookie utf_ui_variant at https://example.com/server for {{server}}",
        B: "Value {0}",
      },
      {
        A: "Keks utf_ui_variant unter https://example.com/server für {{server}}",
        B: "{0}",
      },
    );

    expect(passed.output).not.toContain("[english-carryover]");
    expect(passed.status).toBe(0);
  });

  it("treats a hyphenated compound as one word for carry-over", () => {
    const passed = run({ Nav: "Command Center navigation" }, { Nav: "Command-Center-Navigation" });

    expect(passed.status).toBe(0);
    expect(passed.output).not.toContain("[english-carryover]");
    expect(passed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
  });

  it("does not check keys of the allow lists and reports byte-identical values once", () => {
    const identicalAllowed = run(
      { Brand: "Mission image" },
      { Brand: "Mission image" },
      { Brand: "proper noun or brand name" },
    );
    const englishAllowed = run(
      { Title: "Mission image pending" },
      { Title: "Mission image ausstehend" },
      {},
      { Title: "product name that keeps English words" },
    );
    const identical = run({ Name: "Mission image" }, { Name: "Mission image" });

    expect(identicalAllowed.status).toBe(0);
    expect(englishAllowed.status).toBe(0);
    expect(englishAllowed.output).toMatch(/0 failure\(s\), 0 warning\(s\)/);
    expect(identical.output).toContain("[identical] Name");
    expect(identical.output).not.toContain("[english-carryover]");
  });

  it("fails on a shared word without a reason and warns about an unused one", () => {
    const empty = run({ A: "Open server" }, { A: "Server öffnen" }, {}, {}, { server: "  " });
    const stale = run(
      { A: "Stone" },
      { A: "Stein" },
      {},
      {},
      { server: "loanword used in the German UI" },
    );

    expect(empty.status).toBe(1);
    expect(empty.output).toContain(
      "[allow-reason] server - shared-word allow-list entry has no reason",
    );
    expect(stale.status).toBe(0);
    expect(stale.output).toMatch(/\[allow-stale\] server - shared word is no longer repeated/);
  });

  it("warns when an english-words allow-list key triggers neither English rule", () => {
    const result = run(
      { Title: "Stone" },
      { Title: "Stein" },
      {},
      { Title: "no longer triggers any English rule" },
    );

    expect(result.status).toBe(0);
    expect(result.output).toMatch(
      /\[allow-stale\] Title - english-words allow-list key no longer triggers/,
    );
  });

  it("fails on an unreferenced Server_ or Hosting_ key and only warns for other families", () => {
    const result = runUnused(["Server_Gone", "Hosting_Gone", "Other_Gone", "Server_Used"], {
      "a.tsx": 'export const label = t("Server_Used");',
    });

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[unused\] Server_Gone - no reference in src/);
    expect(result.output).toMatch(/\[unused\] Hosting_Gone - no reference in src/);
    expect(result.output).toMatch(/\[unused-warn\] Other_Gone - no reference in src/);
    expect(result.output).not.toMatch(/Server_Used - no reference/);
  });

  it("warns without failing when only a non-server key is unreferenced", () => {
    const result = runUnused(["Server_Used", "Other_Gone"], {
      "a.tsx": 'export const label = t("Server_Used");',
    });

    expect(result.status).toBe(0);
    expect(result.output).toMatch(/\[unused-warn\] Other_Gone/);
  });

  it("counts a literal, a lookup table, a type, a template, a concatenation and a plural base as references", () => {
    const result = runUnused(
      [
        "Server_Literal",
        "Server_Table",
        "Server_Typed",
        "Server_Action_Start_Label",
        "Server_Audit_Created",
        "Server_Count_one",
        "Server_Count_other",
      ],
      {
        "a.tsx": [
          'export const literal = t("Server_Literal");',
          'export const table: Record<string, string> = { start: "Server_Table" };',
          'export type Key = "Server_Typed";',
          "export const built = t(`Server_Action_${id}_Label`);",
          'export const joined = t("Server_Audit_" + kind);',
          'export const plural = t("Server_Count", { count: 2 });',
        ].join("\n"),
      },
    );

    expect(result.output).not.toMatch(/\[unused/);
    expect(result.status).toBe(0);
  });

  it("does not count a reference that sits only in a comment-free unrelated word", () => {
    const result = runUnused(["Server_Note"], {
      "a.tsx": "export const text = 'Server_Note is described here but never used';",
    });

    expect(result.status).toBe(1);
    expect(result.output).toMatch(/\[unused\] Server_Note/);
  });

  it("passes the unused-key check on the real locale files and source tree", () => {
    const result = spawnSync(process.execPath, [scriptPath, "--unused"], { encoding: "utf8" });

    expect(result.status, `${result.stdout}${result.stderr}`).toBe(0);
    expect(result.stdout).not.toMatch(/^\[unused\] /m);
  });

  it("passes on the real locale files with default paths", () => {
    const result = spawnSync(process.execPath, [scriptPath], { encoding: "utf8" });

    expect(result.status, `${result.stdout}${result.stderr}`).toBe(0);
    expect(result.stdout).toContain("0 failure(s), 0 warning(s)");
  });
});
