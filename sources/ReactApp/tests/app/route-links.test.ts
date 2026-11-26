import { readFileSync } from "node:fs";
import { dirname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";
import { matchRoute } from "@/app/routes";
import { isDeclaredQueryKey, urlParameters } from "@/app/urlParameters";
import { filesLocationTo } from "@/features/hosting/filesLocation";
import {
  activityEntryAnchor,
  activityEntryTo,
  sectionTo,
  type WorkspaceSection,
} from "@/features/hosting/workspaceSections";

const appRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");

/** The route prefixes of the app; a link literal that starts with one of them must resolve. */
const routePrefixes = [
  "/command",
  "/events",
  "/wiki",
  "/verein",
  "/unit",
  "/field-reports",
  "/join",
  "/settings",
  "/gdpr",
  "/authentication",
] as const;

/** Stands in for a template expression. It never occurs in source text. */
const placeholder = "\u0000";

/**
 * The values substituted for template expressions, tried in order. The route patterns use GUID, long and free
 * segments, so one sample per kind is enough to prove that a link can resolve.
 */
const samples = ["0a1b2c3d-0000-4000-8000-0123456789ab", "12345", "job"] as const;

const linkAttributeNames = new Set(["to", "href"]);
const linkPropertyNames = new Set(["to", "href"]);

interface LinkSite {
  file: string;
  line: number;
  /** The link text; template expressions are replaced by {@link placeholder}. */
  text: string;
  /** True for the location object form without a resolvable pathname (relative to the current route). */
  relative: boolean;
  /** The search text of an object form, when present. */
  search: string | null;
  /** The hash text of an object form, when present. */
  hash: string | null;
}

/** The most alternative texts one expression is expanded to; a link has at most a few conditional parts. */
const maxAlternatives = 16;

/**
 * Renders the possible texts of a string-like expression with {@link placeholder} for every part that is not a
 * literal. A conditional part contributes the texts of both of its branches.
 *
 * @returns The alternative texts, or null when the expression is not text-like at all.
 */
function textsOf(
  expression: ts.Expression,
  constants: ReadonlyMap<string, string>,
): string[] | null {
  if (ts.isParenthesizedExpression(expression)) return textsOf(expression.expression, constants);
  if (ts.isAsExpression(expression)) return textsOf(expression.expression, constants);
  if (ts.isStringLiteral(expression) || ts.isNoSubstitutionTemplateLiteral(expression))
    return [expression.text];
  if (ts.isConditionalExpression(expression)) {
    const whenTrue = textsOf(expression.whenTrue, constants) ?? [placeholder];
    const whenFalse = textsOf(expression.whenFalse, constants) ?? [placeholder];
    return [...whenTrue, ...whenFalse].slice(0, maxAlternatives);
  }
  if (ts.isTemplateExpression(expression)) {
    let texts = [expression.head.text];
    for (const span of expression.templateSpans) {
      const parts = textsOf(span.expression, constants) ?? [placeholder];
      texts = texts
        .flatMap((text) => parts.map((part) => text + part + span.literal.text))
        .slice(0, maxAlternatives);
    }
    return texts;
  }
  if (
    ts.isBinaryExpression(expression) &&
    expression.operatorToken.kind === ts.SyntaxKind.PlusToken
  ) {
    const left = textsOf(expression.left, constants);
    const right = textsOf(expression.right, constants);
    if (left === null && right === null) return null;
    return (left ?? [placeholder])
      .flatMap((text) => (right ?? [placeholder]).map((part) => text + part))
      .slice(0, maxAlternatives);
  }
  if (ts.isIdentifier(expression)) {
    const constant = constants.get(expression.text);
    return constant === undefined ? null : [constant];
  }
  return null;
}

/** Collects `const name = "literal"` declarations so a constant link target can be resolved. */
function collectConstants(source: ts.SourceFile): Map<string, string> {
  const constants = new Map<string, string>();
  const visit = (node: ts.Node) => {
    if (
      ts.isVariableDeclaration(node) &&
      ts.isIdentifier(node.name) &&
      node.initializer &&
      (ts.isStringLiteral(node.initializer) || ts.isNoSubstitutionTemplateLiteral(node.initializer))
    )
      constants.set(node.name.text, node.initializer.text);
    ts.forEachChild(node, visit);
  };
  visit(source);
  return constants;
}

/** Expands a link-valued expression into the expressions that can be its value (both branches of a condition). */
function alternatives(expression: ts.Expression): ts.Expression[] {
  if (ts.isParenthesizedExpression(expression)) return alternatives(expression.expression);
  if (ts.isConditionalExpression(expression))
    return [...alternatives(expression.whenTrue), ...alternatives(expression.whenFalse)];
  return [expression];
}

/**
 * Finds the internal link literals of one source text: `to=` and `href=` JSX attributes, `to:` and `href:`
 * properties of link tables, and the first argument of `navigate(`.
 */
function scanLinks(fileName: string, text: string): LinkSite[] {
  const source = ts.createSourceFile(fileName, text, ts.ScriptTarget.Latest, true);
  const constants = collectConstants(source);
  const sites: LinkSite[] = [];
  const lineOf = (node: ts.Node) =>
    source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;

  const add = (expression: ts.Expression) => {
    for (const candidate of alternatives(expression)) {
      if (ts.isObjectLiteralExpression(candidate)) {
        let pathnames: string[] | null = null;
        let search: string | null = null;
        let hash: string | null = null;
        for (const property of candidate.properties) {
          if (!ts.isPropertyAssignment(property) || !ts.isIdentifier(property.name)) continue;
          const values = textsOf(property.initializer, constants);
          if (property.name.text === "pathname") pathnames = values;
          if (property.name.text === "search") search = values?.[0] ?? null;
          if (property.name.text === "hash") hash = values?.[0] ?? null;
        }
        if (pathnames !== null)
          for (const pathname of pathnames) {
            if (pathname.startsWith(placeholder)) continue;
            sites.push({
              file: fileName,
              line: lineOf(candidate),
              text: pathname,
              relative: false,
              search,
              hash,
            });
          }
        else if (search !== null || hash !== null)
          sites.push({
            file: fileName,
            line: lineOf(candidate),
            text: "",
            relative: true,
            search,
            hash,
          });
        continue;
      }
      for (const value of textsOf(candidate, constants) ?? [])
        sites.push({
          file: fileName,
          line: lineOf(candidate),
          text: value,
          relative: false,
          search: null,
          hash: null,
        });
    }
  };

  const visit = (node: ts.Node) => {
    if (
      ts.isJsxAttribute(node) &&
      ts.isIdentifier(node.name) &&
      linkAttributeNames.has(node.name.text) &&
      node.initializer
    ) {
      if (ts.isStringLiteral(node.initializer)) add(node.initializer);
      else if (ts.isJsxExpression(node.initializer) && node.initializer.expression)
        add(node.initializer.expression);
    }
    if (
      ts.isPropertyAssignment(node) &&
      ts.isIdentifier(node.name) &&
      linkPropertyNames.has(node.name.text)
    ) {
      // Only string-like values count; object values are location objects handled at their own call sites.
      const values = textsOf(node.initializer, constants);
      if (values?.some((value) => value.startsWith("/"))) add(node.initializer);
    }
    if (
      ts.isCallExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === "navigate" &&
      node.arguments[0]
    )
      add(node.arguments[0]);
    ts.forEachChild(node, visit);
  };
  visit(source);
  return sites.filter(
    (site) => site.relative || routePrefixes.some((p) => site.text.startsWith(p)),
  );
}

function substitute(text: string, sample: string): string {
  return text.split(placeholder).join(sample);
}

function queryKeysOf(search: string): string[] {
  return search
    .replace(/^\?/, "")
    .split("&")
    .filter((part) => part !== "" && !part.includes(placeholder))
    .map((part) => part.split("=")[0]!);
}

function hashMatches(routeIds: readonly string[], hash: string): boolean {
  const anchor = hash.replace(/^#/, "");
  return routeIds.some((id) => {
    const declared = Object.hasOwn(urlParameters, id) ? urlParameters[id]!.hash : [];
    return declared.some((entry) =>
      typeof entry === "string" ? entry === anchor : entry.test(anchor),
    );
  });
}

/**
 * Checks one link against the route manifest and the URL parameter contract.
 *
 * @returns A failure message, or null when the link resolves and carries only declared keys and anchors.
 */
function violationOf(site: LinkSite): string | null {
  const where = `${site.file}:${site.line}`;
  const split = /^([^?#]*)(\?[^#]*)?(#.*)?$/.exec(site.text);
  const rawPath = split?.[1] ?? "";
  const rawQuery = site.search ?? split?.[2] ?? "";
  const rawHash = site.hash ?? split?.[3] ?? "";
  const label = substitute(`${site.text}${site.search ?? ""}${site.hash ?? ""}`, "${...}");

  let routeIds: string[];
  if (site.relative) {
    // A location without a pathname stays on the current route: any route may own the keys and the anchor.
    routeIds = Object.keys(urlParameters);
  } else {
    const resolved = samples
      .map((sample) => matchRoute(substitute(rawPath, sample)))
      .find((route) => route !== undefined);
    if (!resolved) return `${where} link "${label}" does not resolve through matchRoute`;
    routeIds = [resolved.id];
  }

  for (const key of queryKeysOf(rawQuery)) {
    if (!routeIds.some((id) => isDeclaredQueryKey(id, key)))
      return `${where} link "${label}" uses undeclared key "${key}" for ${site.relative ? "any route" : routeIds[0]!}`;
  }
  if (rawHash !== "") {
    const accepted = samples.some((sample) => hashMatches(routeIds, substitute(rawHash, sample)));
    if (!accepted) return `${where} link "${label}" uses an undeclared anchor "${rawHash}"`;
  }
  return null;
}

function listSourceFiles(): string[] {
  return ts.sys
    .readDirectory(resolve(appRoot, "src"), [".ts", ".tsx"])
    .filter((file) => !relative(appRoot, file).replace(/\\/g, "/").startsWith("src/generated/"));
}

describe("internal link resolution", () => {
  const links = listSourceFiles().flatMap((file) =>
    scanLinks(relative(appRoot, file).replace(/\\/g, "/"), readFileSync(file, "utf8")),
  );

  it("scans a non-vacuous number of internal links", () => {
    expect(links.length).toBeGreaterThanOrEqual(30);
    expect(links.filter((link) => !link.relative).length).toBeGreaterThanOrEqual(25);
  });

  it("resolves every internal link through matchRoute with declared keys and anchors", () => {
    const violations = links.map(violationOf).filter((message) => message !== null);
    expect(violations).toEqual([]);
  });

  it("accepts the automation deep link of the server settings", () => {
    const [site] = scanLinks(
      "sample.tsx",
      'export const a = <Link to="/command/servers/0a1b2c3d-0000-4000-8000-0123456789ab?tab=settings#server-automation" />;',
    );
    expect(site?.text).toContain("tab=settings#server-automation");
    expect(violationOf(site!)).toBeNull();
  });

  it("accepts the relative location of the automation notice link", () => {
    const [site] = scanLinks(
      "sample.tsx",
      'export const a = <ActionLink to={{ search: "?tab=settings", hash: "#server-automation" }} />;',
    );
    expect(site?.relative).toBe(true);
    expect(violationOf(site!)).toBeNull();
  });

  it("reports a link to an unknown path and a link with an undeclared key in an inline sample", () => {
    const sample = [
      'export const a = <Link to="/command/no-such-page" />;',
      "export const b = () => navigate(`/command/servers/${id}?bogus=1`);",
      'export const c = <a href="/command/servers/0a1b2c3d-0000-4000-8000-0123456789ab#nowhere" />;',
      'export const d = <Link to="/command/servers?filter=mine" />;',
      "export const e = <Link to={`/command/mod-packs/${id}`} />;",
      'export const f = <a href="https://example.org/command" />;',
    ].join("\n");
    const found = scanLinks("sample.tsx", sample);
    const messages = found.map(violationOf).filter((message) => message !== null);
    expect(found).toHaveLength(5);
    expect(messages).toHaveLength(3);
    expect(messages.join("\n")).toContain("does not resolve through matchRoute");
    expect(messages.join("\n")).toContain('undeclared key "bogus"');
    expect(messages.join("\n")).toContain('undeclared anchor "#nowhere"');
  });

  describe("link helpers", () => {
    const serverId = "0a1b2c3d-0000-4000-8000-0123456789ab";

    it.each<WorkspaceSection>(["overview", "activity", "settings", "files", "machine"])(
      "builds a resolving workspace link for %s",
      (section) => {
        const target = sectionTo(serverId, section);
        expect(matchRoute(target.pathname)?.id).toBe("server");
        for (const key of queryKeysOf(target.search))
          expect(isDeclaredQueryKey("server", key)).toBe(true);
      },
    );

    it("builds an activity entry link that resolves and carries a declared anchor", () => {
      for (const type of ["job", "log", "audit"] as const) {
        const target = activityEntryTo(serverId, activityEntryAnchor(type, "42"));
        expect(matchRoute(target.pathname)?.id).toBe("server");
        expect(queryKeysOf(target.search).every((key) => isDeclaredQueryKey("server", key))).toBe(
          true,
        );
        expect(hashMatches(["server"], target.hash)).toBe(true);
      }
    });

    it("builds a files link that resolves and carries only declared keys", () => {
      const target = filesLocationTo(
        `/command/servers/${serverId}`,
        new URLSearchParams("tab=files&action=start"),
        { path: "mods/cba", context: "live" },
      );
      expect(matchRoute(target.pathname)?.id).toBe("server");
      expect(queryKeysOf(target.search).sort()).toEqual(["area", "path", "tab"]);
      for (const key of queryKeysOf(target.search))
        expect(isDeclaredQueryKey("server", key)).toBe(true);
    });
  });
});
