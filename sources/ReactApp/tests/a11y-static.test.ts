import { readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import ts from "typescript";

/**
 * Static accessibility scans over the application source (Phase 69, FND-06, UI-SPEC "Accessibility Contract").
 *
 * Four rules hold for every `.tsx` file below `src`, generated code excluded:
 *
 * 1. `positive-tabindex`: no `tabIndex` greater than zero. A positive value overrides the document order for every
 *    keyboard user and cannot be kept consistent across a page.
 * 2. `icon-only-name`: every `ActionButton`, `ActionLink` and `ActionAnchor` marked `iconOnly` carries a non-empty
 *    `aria-label`, because the glyph has no text for a screen reader.
 * 3. `click-without-role`: no `onClick` on a `div`, `span`, `li` or `tr` unless the element also has a `role` and a
 *    key handler. Without both, the handler works with a pointer only. A `tr` has the implicit role `row`, so it needs
 *    the key handler only. Two shapes are not clickable controls and are skipped: a decorative `aria-hidden`
 *    element, such as a click-to-dismiss scrim, and a wrapper whose whole handler is `stopPropagation()`.
 * 4. `alert-made-polite`: no element whose role can be `alert` sets a constant `aria-live` other than `assertive`.
 *    An explicit `aria-live` overrides the implicit assertive politeness of the alert role, so a constant polite or
 *    off value turns an error into a polite announcement (WR-05).
 *
 * The scan works on the TypeScript syntax tree, so it reads attributes and properties rather than text. Each rule is
 * proven on an inline bad sample and an inline good sample, so a scan that stopped finding anything would fail here
 * before it silently approved the source.
 */

/** The identifier of an accessibility rule of this scan. */
type StaticRule =
  "positive-tabindex" | "icon-only-name" | "click-without-role" | "alert-made-polite";

/** One violation: the rule, the 1-based line and what is wrong. */
interface StaticFinding {
  /** The violated rule. */
  rule: StaticRule;
  /** The 1-based line of the offending node. */
  line: number;
  /** A readable description. */
  detail: string;
}

/** What a scan saw, which proves that it looked at something. */
interface StaticScan {
  /** The violations. */
  findings: StaticFinding[];
  /** The number of JSX elements visited. */
  elements: number;
  /** The number of `tabIndex` attributes and properties visited. */
  tabIndexes: number;
}

/** The element names that render a shared control and accept `iconOnly`. */
const actionControls = new Set(["ActionButton", "ActionLink", "ActionAnchor"]);

/** The elements that are not interactive by themselves. */
const inertElements = new Set(["div", "span", "li", "tr"]);

/** The handlers that make a clickable element reachable and operable with a keyboard. */
const keyHandlers = new Set(["onKeyDown", "onKeyUp", "onKeyPress"]);

/**
 * Reads the numeric value of a literal expression.
 *
 * @param node The expression, possibly wrapped in a JSX expression container.
 * @returns The number a numeric or numeric-string literal stands for; null for anything else.
 */
function numericValue(node: ts.Node | undefined): number | null {
  if (node === undefined) return null;
  if (ts.isJsxExpression(node)) return numericValue(node.expression ?? undefined);
  if (ts.isNumericLiteral(node)) return Number(node.text);
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
    const parsed = Number(node.text.trim());
    return node.text.trim() === "" || Number.isNaN(parsed) ? null : parsed;
  }
  if (ts.isPrefixUnaryExpression(node) && node.operator === ts.SyntaxKind.MinusToken) {
    const inner = numericValue(node.operand);
    return inner === null ? null : -inner;
  }
  return null;
}

/**
 * Tells whether a JSX attribute is present and not literally false or empty.
 *
 * @param attribute The attribute, if the element has it.
 * @returns True for a bare attribute, a `{true}` value and any non-literal expression.
 */
function isOn(attribute: ts.JsxAttribute | undefined): boolean {
  if (attribute === undefined) return false;
  const initializer = attribute.initializer;
  if (initializer === undefined) return true;
  if (ts.isJsxExpression(initializer) && initializer.expression) {
    return initializer.expression.kind !== ts.SyntaxKind.FalseKeyword;
  }
  return true;
}

/**
 * Tells whether an `aria-label` attribute can carry an accessible name.
 *
 * @param attribute The attribute, if the element has it.
 * @returns False when it is missing or a literal empty string.
 */
function hasName(attribute: ts.JsxAttribute | undefined): boolean {
  if (attribute === undefined) return false;
  const initializer = attribute.initializer;
  if (initializer === undefined) return false;
  const literal = ts.isJsxExpression(initializer) ? initializer.expression : initializer;
  if (literal && (ts.isStringLiteral(literal) || ts.isNoSubstitutionTemplateLiteral(literal))) {
    return literal.text.trim() !== "";
  }
  return true;
}

/**
 * Tells whether an element hides itself from assistive technology, such as a click-to-dismiss scrim. Its pointer
 * handler is a convenience: the dialog it covers keeps a keyboard way out.
 *
 * @param attributes The attributes of the element by name.
 * @returns True for `aria-hidden`, `aria-hidden="true"` and `aria-hidden={true}`.
 */
function isDecorativeClick(attributes: Map<string, ts.JsxAttribute>): boolean {
  const hidden = attributes.get("aria-hidden");
  if (hidden === undefined) return false;
  const initializer = hidden.initializer;
  if (initializer === undefined) return true;
  if (ts.isStringLiteral(initializer)) return initializer.text === "true";
  return (
    ts.isJsxExpression(initializer) && initializer.expression?.kind === ts.SyntaxKind.TrueKeyword
  );
}

/**
 * Tells whether an `onClick` only stops the event from reaching a parent handler. Such a wrapper makes nothing
 * clickable by itself: it keeps the controls inside from triggering the row.
 *
 * @param attribute The `onClick` attribute.
 * @returns True for an arrow function whose whole body is one `stopPropagation()` call.
 */
function onlyStopsPropagation(attribute: ts.JsxAttribute): boolean {
  const expression = attribute.initializer;
  if (!expression || !ts.isJsxExpression(expression) || !expression.expression) return false;
  const handler = expression.expression;
  if (!ts.isArrowFunction(handler)) return false;
  let body: ts.Node | undefined = handler.body;
  if (ts.isBlock(body) && body.statements.length === 1) {
    const statement = body.statements[0];
    body = statement && ts.isExpressionStatement(statement) ? statement.expression : undefined;
  }
  if (!body || !ts.isCallExpression(body)) return false;
  const callee = body.expression;
  return ts.isPropertyAccessExpression(callee) && callee.name.text === "stopPropagation";
}

/**
 * Tells whether a role attribute can yield the alert role: the string literal `alert`, or an expression that holds a
 * string literal `alert` anywhere, such as a conditional on a kind.
 *
 * @param attribute The `role` attribute, if the element has it.
 * @returns True when the literal `alert` is a possible value.
 */
function canBeAlert(attribute: ts.JsxAttribute | undefined): boolean {
  const initializer = attribute?.initializer;
  if (initializer === undefined) return false;
  const isAlertLiteral = (node: ts.Node): boolean =>
    (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) && node.text === "alert";
  const search = (node: ts.Node): boolean =>
    isAlertLiteral(node) || ts.forEachChild(node, search) === true;
  return search(initializer);
}

/**
 * Reads the value of a constant live attribute. A conditional expression is not constant: it may omit the attribute
 * for an alert, which is the accepted shape of `StatePanel`.
 *
 * @param attribute The `aria-live` attribute, if the element has it.
 * @returns The string a string literal or a `{"..."}` container stands for; null for anything else.
 */
function constantLive(attribute: ts.JsxAttribute | undefined): string | null {
  const initializer = attribute?.initializer;
  if (initializer === undefined) return null;
  const literal = ts.isJsxExpression(initializer) ? initializer.expression : initializer;
  if (literal && (ts.isStringLiteral(literal) || ts.isNoSubstitutionTemplateLiteral(literal))) {
    return literal.text;
  }
  return null;
}

/**
 * Scans one source text for the four rules.
 *
 * @param path The file path, used to pick the script kind.
 * @param source The source text.
 * @returns The findings and the evidence that the scan visited elements.
 */
export function scanAccessibility(path: string, source: string): StaticScan {
  const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const scan: StaticScan = { findings: [], elements: 0, tabIndexes: 0 };
  const lineOf = (node: ts.Node): number =>
    file.getLineAndCharacterOfPosition(node.getStart(file)).line + 1;

  const checkTabIndex = (node: ts.Node, value: ts.Node | undefined): void => {
    scan.tabIndexes++;
    const number = numericValue(value);
    if (number !== null && number > 0) {
      scan.findings.push({
        rule: "positive-tabindex",
        line: lineOf(node),
        detail: `tabIndex ${number} overrides the document order`,
      });
    }
  };

  const visit = (node: ts.Node): void => {
    if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      scan.elements++;
      const tag = node.tagName.getText(file);
      const attributes = new Map<string, ts.JsxAttribute>();
      for (const attribute of node.attributes.properties) {
        if (ts.isJsxAttribute(attribute)) attributes.set(attribute.name.getText(file), attribute);
      }
      if (actionControls.has(tag) && isOn(attributes.get("iconOnly"))) {
        if (!hasName(attributes.get("aria-label"))) {
          scan.findings.push({
            rule: "icon-only-name",
            line: lineOf(node),
            detail: `<${tag} iconOnly> has no aria-label`,
          });
        }
      }
      const live = constantLive(attributes.get("aria-live"));
      if (live !== null && live !== "assertive" && canBeAlert(attributes.get("role"))) {
        scan.findings.push({
          rule: "alert-made-polite",
          line: lineOf(node),
          detail: `<${tag} role="alert"> sets aria-live="${live}", which overrides the assertive alert`,
        });
      }
      const click = attributes.get("onClick");
      if (
        inertElements.has(tag) &&
        click !== undefined &&
        !isDecorativeClick(attributes) &&
        !onlyStopsPropagation(click)
      ) {
        // A table row already has the implicit role row, so only a div, span or li needs an explicit role.
        const hasRole = tag === "tr" || attributes.has("role");
        const hasKey = [...attributes.keys()].some((name) => keyHandlers.has(name));
        if (!hasRole || !hasKey) {
          const missing = [hasRole ? null : "a role", hasKey ? null : "a key handler"]
            .filter(Boolean)
            .join(" and ");
          scan.findings.push({
            rule: "click-without-role",
            line: lineOf(node),
            detail: `<${tag} onClick> has no ${missing}`,
          });
        }
      }
    }
    if (ts.isJsxAttribute(node) && /^tabindex$/i.test(node.name.getText(file))) {
      checkTabIndex(node, node.initializer);
    }
    if (
      ts.isPropertyAssignment(node) &&
      (ts.isIdentifier(node.name) || ts.isStringLiteral(node.name)) &&
      /^tabindex$/i.test(node.name.text)
    ) {
      checkTabIndex(node, node.initializer);
    }
    ts.forEachChild(node, visit);
  };
  visit(file);
  return scan;
}

/**
 * Scans every handwritten `.tsx` file below `src`.
 *
 * @param projectRoot The absolute project root.
 * @returns The findings as `path:line rule detail` lines, plus the number of files and elements visited.
 */
function scanTree(projectRoot: string): { lines: string[]; files: number; elements: number } {
  const entries = readdirSync(join(projectRoot, "src"), { recursive: true }) as string[];
  const lines: string[] = [];
  let files = 0;
  let elements = 0;
  for (const entry of entries.sort()) {
    const relative = `src/${entry.replaceAll("\\", "/")}`;
    if (!relative.endsWith(".tsx") || relative.startsWith("src/generated/")) continue;
    const scan = scanAccessibility(relative, readFileSync(join(projectRoot, relative), "utf8"));
    files++;
    elements += scan.elements;
    for (const finding of scan.findings) {
      lines.push(`${relative}:${finding.line} ${finding.rule}: ${finding.detail}`);
    }
  }
  return { lines, files, elements };
}

/** The rules of a bad sample, in order. */
function rulesOf(source: string): StaticRule[] {
  return scanAccessibility("sample.tsx", source).findings.map((finding) => finding.rule);
}

describe("static accessibility rules", () => {
  describe("positive tabindex", () => {
    it("flags a positive JSX tabIndex in every literal form", () => {
      expect(rulesOf(`const a = <div tabIndex={1} />;`)).toEqual(["positive-tabindex"]);
      expect(rulesOf(`const a = <div tabIndex="2" />;`)).toEqual(["positive-tabindex"]);
      expect(rulesOf(`const a = <div tabindex={3} />;`)).toEqual(["positive-tabindex"]);
    });

    it("flags a positive tabIndex in a spread props object", () => {
      expect(
        rulesOf(`const p = { role: "region", tabIndex: 4 }; const a = <div {...p} />;`),
      ).toEqual(["positive-tabindex"]);
    });

    it("accepts 0 and -1, which keep the document order", () => {
      expect(rulesOf(`const a = <div tabIndex={0} />;`)).toEqual([]);
      expect(rulesOf(`const a = <div tabIndex={-1} />;`)).toEqual([]);
      expect(rulesOf(`const p = { tabIndex: 0 };`)).toEqual([]);
    });
  });

  describe("icon-only controls", () => {
    it("flags an icon-only action control without a name", () => {
      expect(rulesOf(`const a = <ActionButton iconOnly><X /></ActionButton>;`)).toEqual([
        "icon-only-name",
      ]);
      expect(rulesOf(`const a = <ActionLink iconOnly to="/"><X /></ActionLink>;`)).toEqual([
        "icon-only-name",
      ]);
      expect(
        rulesOf(`const a = <ActionAnchor iconOnly={true} href="/"><X /></ActionAnchor>;`),
      ).toEqual(["icon-only-name"]);
    });

    it("flags an empty literal name", () => {
      expect(
        rulesOf(`const a = <ActionButton iconOnly aria-label=""><X /></ActionButton>;`),
      ).toEqual(["icon-only-name"]);
    });

    it("accepts a named icon-only control and a labelled text control", () => {
      expect(
        rulesOf(`const a = <ActionButton iconOnly aria-label={t("Close")}><X /></ActionButton>;`),
      ).toEqual([]);
      expect(
        rulesOf(`const a = <ActionButton iconOnly aria-label="Close"><X /></ActionButton>;`),
      ).toEqual([]);
      expect(rulesOf(`const a = <ActionButton>Save</ActionButton>;`)).toEqual([]);
      expect(rulesOf(`const a = <ActionButton iconOnly={false}>Save</ActionButton>;`)).toEqual([]);
    });
  });

  describe("click handlers on inert elements", () => {
    it("flags onClick on div, span, li and tr without a role and a key handler", () => {
      for (const tag of ["div", "span", "li", "tr"]) {
        expect(rulesOf(`const a = <${tag} onClick={go} />;`)).toEqual(["click-without-role"]);
      }
    });

    it("flags a role without a key handler and a key handler without a role", () => {
      expect(rulesOf(`const a = <div role="button" onClick={go} />;`)).toEqual([
        "click-without-role",
      ]);
      expect(rulesOf(`const a = <div onKeyDown={key} onClick={go} />;`)).toEqual([
        "click-without-role",
      ]);
    });

    it("needs a key handler on a table row but no explicit role", () => {
      expect(rulesOf(`const a = <tr onClick={go} />;`)).toEqual(["click-without-role"]);
      expect(rulesOf(`const a = <tr tabIndex={0} onKeyDown={key} onClick={go} />;`)).toEqual([]);
    });

    it("skips a decorative scrim and a stopPropagation wrapper but not other handlers", () => {
      expect(rulesOf(`const a = <div aria-hidden="true" onClick={close} />;`)).toEqual([]);
      expect(rulesOf(`const a = <div aria-hidden onClick={close} />;`)).toEqual([]);
      expect(rulesOf(`const a = <div onClick={(event) => event.stopPropagation()} />;`)).toEqual(
        [],
      );
      expect(
        rulesOf(`const a = <div onClick={(event) => { event.stopPropagation(); }} />;`),
      ).toEqual([]);
      expect(rulesOf(`const a = <div aria-hidden="false" onClick={close} />;`)).toEqual([
        "click-without-role",
      ]);
      expect(
        rulesOf(`const a = <div onClick={(event) => { event.stopPropagation(); go(); }} />;`),
      ).toEqual(["click-without-role"]);
    });

    it("accepts a role with a key handler and ignores native controls", () => {
      expect(
        rulesOf(`const a = <div role="button" tabIndex={0} onKeyDown={key} onClick={go} />;`),
      ).toEqual([]);
      expect(rulesOf(`const a = <button onClick={go}>Go</button>;`)).toEqual([]);
      expect(rulesOf(`const a = <section onClick={go} />;`)).toEqual([]);
    });
  });

  describe("alerts made polite", () => {
    it("flags an alert with a constant polite or off live attribute", () => {
      expect(rulesOf(`const a = <section role="alert" aria-live="polite" />;`)).toEqual([
        "alert-made-polite",
      ]);
      expect(rulesOf(`const a = <section role={"alert"} aria-live={"polite"} />;`)).toEqual([
        "alert-made-polite",
      ]);
      expect(rulesOf(`const a = <div role="alert" aria-live="off" />;`)).toEqual([
        "alert-made-polite",
      ]);
    });

    it("flags a conditional role that can yield alert with a constant polite live attribute", () => {
      expect(
        rulesOf(
          `const a = <section role={kind === "error" ? "alert" : "status"} aria-live="polite" />;`,
        ),
      ).toEqual(["alert-made-polite"]);
    });

    it("accepts an assertive alert, an alert without a live attribute and a polite status", () => {
      expect(rulesOf(`const a = <section role="alert" />;`)).toEqual([]);
      expect(rulesOf(`const a = <section role="alert" aria-live="assertive" />;`)).toEqual([]);
      expect(rulesOf(`const a = <section role="status" aria-live="polite" />;`)).toEqual([]);
      expect(rulesOf(`const a = <section aria-live="polite" />;`)).toEqual([]);
    });

    it("accepts the StatePanel shape: a conditional role with a conditional live attribute", () => {
      expect(
        rulesOf(
          `const a = <section role={kind === "error" ? "alert" : "status"} aria-live={kind === "error" ? undefined : "polite"} />;`,
        ),
      ).toEqual([]);
    });
  });

  describe("the source tree", () => {
    it("holds every rule for every handwritten component", () => {
      const projectRoot = resolve(import.meta.dirname, "..");
      const result = scanTree(projectRoot);
      // Non-vacuity: the scan must have read the application, not an empty folder.
      expect(result.files).toBeGreaterThan(100);
      expect(result.elements).toBeGreaterThan(2000);
      expect(result.lines).toEqual([]);
    });
  });
});
