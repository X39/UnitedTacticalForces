import { readdirSync, readFileSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const stylesheet = resolve(
  import.meta.dirname,
  "..",
  "..",
  "src",
  "components",
  "action-control.css",
);

/** One style rule: its selector list, its declarations and whether it sits at the top level of the sheet. */
interface CssRule {
  selector: string;
  declarations: string[];
  topLevel: boolean;
}

/**
 * Removes every CSS comment so a comment that mentions a property cannot be read as a declaration.
 *
 * @param css The stylesheet text.
 * @returns The stylesheet text without comments.
 */
function stripComments(css: string): string {
  return css.replace(/\/\*[\s\S]*?\*\//g, "");
}

/**
 * Splits a stylesheet into its style rules, descending into at-rules such as media queries.
 *
 * @param css The stylesheet text, comments already stripped.
 * @param topLevel Whether the text is the top level of the sheet.
 * @returns Every style rule found, in document order.
 */
function collectRules(css: string, topLevel = true): CssRule[] {
  const rules: CssRule[] = [];
  let depth = 0;
  let selectorStart = 0;
  let bodyStart = 0;
  let selector = "";
  for (let index = 0; index < css.length; index++) {
    const char = css[index];
    if (char === "{") {
      if (depth === 0) {
        selector = css.slice(selectorStart, index).trim();
        bodyStart = index + 1;
      }
      depth++;
    } else if (char === "}") {
      depth--;
      if (depth === 0) {
        const body = css.slice(bodyStart, index);
        if (selector.startsWith("@")) {
          rules.push(...collectRules(body, false));
        } else {
          const declarations = body
            .split(";")
            .map((declaration) => declaration.trim())
            .filter((declaration) => declaration.length > 0);
          rules.push({ selector, declarations, topLevel });
        }
        selectorStart = index + 1;
      }
    }
  }
  return rules;
}

/**
 * Finds the shared top-level `.button` rule.
 *
 * @param css The stylesheet text, comments already stripped.
 * @returns The declarations of the rule, or an empty list when the rule is missing.
 */
function sharedButtonDeclarations(css: string): string[] {
  const rule = collectRules(css).find(
    (candidate) => candidate.topLevel && candidate.selector === ".button",
  );
  return rule?.declarations ?? [];
}

/**
 * Checks the shared `.button` rule against the wrap contract: a label wraps like the text around it.
 *
 * @param css The stylesheet text with comments.
 * @returns One message per violated requirement; empty when the contract holds.
 */
function findButtonWrapViolations(css: string): string[] {
  const declarations = sharedButtonDeclarations(stripComments(css));
  const violations: string[] = [];
  if (declarations.length === 0) return ["the top-level .button rule is missing"];
  const whiteSpace = declarations.filter((declaration) => /^white-space\s*:/.test(declaration));
  if (whiteSpace.length !== 1)
    violations.push(`.button declares white-space ${whiteSpace.length} times, expected once`);
  else if (!/^white-space\s*:\s*inherit$/.test(whiteSpace[0]!))
    violations.push(`.button declares "${whiteSpace[0]}", expected "white-space: inherit"`);
  if (!declarations.some((declaration) => /^overflow-wrap\s*:\s*break-word$/.test(declaration)))
    violations.push(".button does not declare overflow-wrap: break-word");
  return violations;
}

describe("shared button wrap contract", () => {
  it("lets the label wrap like the surrounding text and breaks an overlong word", () => {
    const css = readFileSync(stylesheet, "utf8");
    expect(findButtonWrapViolations(css)).toEqual([]);
  });

  it("flags the old single-line value", () => {
    const css =
      ".button {\n  display: inline-flex;\n  white-space: nowrap;\n  overflow-wrap: break-word;\n}";
    expect(findButtonWrapViolations(css)).toEqual([
      expect.stringContaining('expected "white-space: inherit"'),
    ]);
  });

  it("flags a second white-space declaration and a missing overflow-wrap", () => {
    const css = ".button {\n  white-space: inherit;\n  white-space: normal;\n}";
    expect(findButtonWrapViolations(css)).toEqual([
      expect.stringContaining("2 times"),
      expect.stringContaining("overflow-wrap"),
    ]);
  });

  it("ignores a commented-out declaration and a .button rule inside a media query", () => {
    const css =
      "@media (max-width: 520px) { .button { white-space: nowrap; } }\n" +
      ".button {\n  /* white-space: nowrap; */\n  white-space: inherit;\n  overflow-wrap: break-word;\n}";
    expect(findButtonWrapViolations(css)).toEqual([]);
  });

  it("reports a missing shared rule", () => {
    expect(findButtonWrapViolations(".card { color: red; }")).toEqual([
      "the top-level .button rule is missing",
    ]);
  });
});

const sourceDirectory = resolve(import.meta.dirname, "..", "..", "src");

/**
 * Lists every stylesheet below a directory.
 *
 * @param directory The directory to scan recursively.
 * @returns The absolute paths of all `.css` files found.
 */
function stylesheetsUnder(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return stylesheetsUnder(path);
    return entry.name.endsWith(".css") ? [path] : [];
  });
}

/**
 * Finds page-local wrap overrides: rules whose selector names the shared `.button` class and force the `normal`
 * white-space value that the shared rule already provides through inheritance.
 *
 * @param css The stylesheet text with comments.
 * @returns The selectors of the offending rules.
 */
function findLocalWrapOverrides(css: string): string[] {
  return collectRules(stripComments(css))
    .filter((rule) => /\.button(?![\w-])/.test(rule.selector))
    .filter((rule) =>
      rule.declarations.some((declaration) => /^white-space\s*:\s*normal\b/.test(declaration)),
    )
    .map((rule) => rule.selector);
}

describe("page-local button wrap overrides", () => {
  it("covers every stylesheet of the app", () => {
    expect(stylesheetsUnder(sourceDirectory).length).toBeGreaterThan(10);
  });

  it("flags a local override, also inside a media query and behind !important", () => {
    expect(findLocalWrapOverrides(".card .button { white-space: normal; }")).toEqual([
      ".card .button",
    ]);
    expect(
      findLocalWrapOverrides("@media (max-width: 520px) { .a > .button { white-space: normal } }"),
    ).toEqual([".a > .button"]);
    expect(findLocalWrapOverrides(".button.chip { white-space: normal !important; }")).toEqual([
      ".button.chip",
    ]);
  });

  it("ignores other selectors, other values and similarly named classes", () => {
    expect(findLocalWrapOverrides(".badge { white-space: normal; }")).toEqual([]);
    expect(findLocalWrapOverrides(".card .button { white-space: nowrap; }")).toEqual([]);
    expect(findLocalWrapOverrides(".button-row { white-space: normal; }")).toEqual([]);
    expect(findLocalWrapOverrides(".card .button { /* white-space: normal; */ }")).toEqual([]);
  });

  it.each(stylesheetsUnder(sourceDirectory).map((path) => [relative(sourceDirectory, path), path]))(
    "keeps %s free of a local button wrap override",
    (_name, path) => {
      expect(findLocalWrapOverrides(readFileSync(path!, "utf8"))).toEqual([]);
    },
  );
});
