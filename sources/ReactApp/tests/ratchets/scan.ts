import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import ts from "typescript";

/** The shared rule set that drives both the ESLint restriction and the Vitest ratchet. */
export interface Rules {
  /** Lower-case HTML element names that must be replaced by a shared control. */
  elements: string[];
  /** Regular expression source matched against a className literal to find button look-alikes. */
  lookalikePattern: string;
  /** Files that implement the shared controls and are exempt from the control rules. */
  primitiveFiles: string[];
  /** JSX attribute names whose string literal value is user-facing text. */
  literalProps: string[];
  /** Call names whose first string literal argument is user-facing text. */
  literalCalls: string[];
  /** Language-neutral tokens that do not count as user-facing text. */
  neutralTokens: string[];
  /** Files and directories (relative to the project root) of the strictest quality tier. */
  tierAPaths: string[];
  /** The only files that may hold a family entry; each carries a reason and the list may only shrink. */
  allowedRemainders: AllowedRemainder[];
}

/** One file that may stay on the ratchet because the UI-SPEC records it as a permanent allowance. */
export interface AllowedRemainder {
  /** The file path relative to the project root. */
  file: string;
  /** The one-line reason the file keeps its raw control or literal text. */
  reason: string;
}

/** The per-file counts the ratchet holds. */
export interface FileCounts {
  /** Raw control opening elements. */
  controls: number;
  /** className literals that imitate a button. */
  lookalikes: number;
  /** Literal user-facing text occurrences. */
  literals: number;
}

/** One ceiling entry of a family file. */
export interface FamilyEntry extends FileCounts {
  /** Why the file is still on the ratchet. */
  reason: string;
}

/** The content of one `family.<name>.json` file. */
export interface FamilyFile {
  /** The family name, equal to the file name segment. */
  family: string;
  /** The plan that owns the family and converts its files. */
  owner: string;
  /** Ceilings keyed by the file path relative to the project root. */
  files: Record<string, FamilyEntry>;
}

const countFields = ["controls", "lookalikes", "literals"] as const;

const neutralElements = new Set(["code", "kbd", "ApiText"]);

/** Returns the zero counts. */
export function emptyCounts(): FileCounts {
  return { controls: 0, lookalikes: 0, literals: 0 };
}

/** Tells whether any count is above zero. */
export function hasViolations(counts: FileCounts): boolean {
  return countFields.some((field) => counts[field] > 0);
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function neutralPattern(rules: Rules): RegExp {
  const tokens = [...rules.neutralTokens].sort((a, b) => b.length - a.length).map(escapeRegExp);
  return new RegExp(`(?<![\\p{L}\\p{N}])(?:${tokens.join("|")})(?![\\p{L}\\p{N}])`, "gu");
}

function textOf(node: ts.Expression | undefined): string | null {
  if (!node) return null;
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) return node.text;
  return null;
}

function attributeLiteral(attribute: ts.JsxAttribute): string | null {
  const initializer = attribute.initializer;
  if (!initializer) return null;
  if (ts.isStringLiteral(initializer)) return initializer.text;
  if (ts.isJsxExpression(initializer)) return textOf(initializer.expression ?? undefined);
  return null;
}

/** The leading literal text of a className value: a literal or the head of a template literal. */
function classNameHead(attribute: ts.JsxAttribute): string | null {
  const initializer = attribute.initializer;
  if (!initializer) return null;
  if (ts.isStringLiteral(initializer)) return initializer.text;
  if (!ts.isJsxExpression(initializer) || !initializer.expression) return null;
  const expression = initializer.expression;
  if (ts.isTemplateExpression(expression)) return expression.head.text;
  return textOf(expression);
}

function calleeName(call: ts.CallExpression): string | null {
  const callee = call.expression;
  if (ts.isIdentifier(callee)) return callee.text;
  if (ts.isPropertyAccessExpression(callee)) return callee.name.text;
  return null;
}

function insideNeutralElement(node: ts.Node): boolean {
  for (let parent = node.parent; parent; parent = parent.parent) {
    if (ts.isJsxElement(parent) && neutralElements.has(parent.openingElement.tagName.getText())) {
      return true;
    }
  }
  return false;
}

/**
 * Counts raw controls, button look-alikes and literal user-facing text in one source text.
 *
 * @param path The file path relative to the project root, used to recognize primitive files.
 * @param source The source text to scan.
 * @param rules The shared rule set.
 */
export function scanSource(path: string, source: string, rules: Rules): FileCounts {
  const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const counts = emptyCounts();
  const isPrimitive = rules.primitiveFiles.includes(path);
  const elements = new Set(rules.elements);
  const props = new Set(rules.literalProps);
  const calls = new Set(rules.literalCalls);
  const lookalike = new RegExp(rules.lookalikePattern);
  const neutral = neutralPattern(rules);
  const isUserText = (text: string | null): boolean =>
    text !== null && /\p{L}{2}/u.test(text.replace(neutral, ""));

  const visit = (node: ts.Node): void => {
    if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      const tag = node.tagName;
      if (!isPrimitive && ts.isIdentifier(tag) && elements.has(tag.text)) counts.controls++;
    } else if (ts.isJsxAttribute(node) && ts.isIdentifier(node.name)) {
      const name = node.name.text;
      if (name === "className" && !isPrimitive) {
        const head = classNameHead(node);
        if (head !== null && lookalike.test(head)) counts.lookalikes++;
      }
      if (props.has(name) && isUserText(attributeLiteral(node))) counts.literals++;
    } else if (ts.isJsxText(node)) {
      if (isUserText(node.text) && !insideNeutralElement(node)) counts.literals++;
    } else if (ts.isCallExpression(node)) {
      const name = calleeName(node);
      if (name !== null && calls.has(name) && isUserText(textOf(node.arguments[0]))) {
        counts.literals++;
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(file);
  return counts;
}

/**
 * Scans every `.tsx` file below the source root.
 *
 * @param projectRoot The absolute project root; keys of the result are relative to it.
 * @param rules The shared rule set.
 */
export function scanTree(projectRoot: string, rules: Rules): Map<string, FileCounts> {
  const result = new Map<string, FileCounts>();
  const entries = readdirSync(join(projectRoot, "src"), { recursive: true }) as string[];
  for (const entry of entries.sort()) {
    const relative = `src/${entry.replaceAll("\\", "/")}`;
    if (!relative.endsWith(".tsx") || relative.startsWith("src/generated/")) continue;
    const source = readFileSync(join(projectRoot, relative), "utf8");
    result.set(relative, scanSource(relative, source, rules));
  }
  return result;
}

/** Reads `rules.json` from the ratchet directory. */
export function loadRules(ratchetDir: string): Rules {
  return JSON.parse(readFileSync(join(ratchetDir, "rules.json"), "utf8")) as Rules;
}

/** Reads every `family.<name>.json` file of the ratchet directory, ordered by file name. */
export function loadFamilies(ratchetDir: string): FamilyFile[] {
  return readdirSync(ratchetDir)
    .filter((name) => /^family\..+\.json$/.test(name))
    .sort()
    .map((name) => JSON.parse(readFileSync(join(ratchetDir, name), "utf8")) as FamilyFile);
}

/** Tells whether a path lies in the strictest quality tier. */
export function isTierA(path: string, rules: Rules): boolean {
  return rules.tierAPaths.some((entry) => path === entry || path.startsWith(`${entry}/`));
}

/**
 * Checks the family ceilings against the scanned counts.
 *
 * @param families Every family file.
 * @param counts The scanned per-file counts.
 * @returns A description for every problem; empty when the ratchet holds.
 */
export function checkRatchet(families: FamilyFile[], counts: Map<string, FileCounts>): string[] {
  const problems: string[] = [];
  const owners = new Map<string, string>();
  for (const family of families) {
    for (const [path, entry] of Object.entries(family.files)) {
      const previous = owners.get(path);
      if (previous !== undefined) {
        problems.push(`${path} is in two families: ${previous} and ${family.family}`);
      }
      owners.set(path, family.family);
      if (entry.reason.trim() === "") problems.push(`${family.family}: ${path} has no reason`);
      const current = counts.get(path) ?? emptyCounts();
      for (const field of countFields) {
        if (current[field] > entry[field]) {
          problems.push(
            `${family.family}: ${path} has ${current[field]} ${field}, above its ceiling ${entry[field]}`,
          );
        }
      }
    }
  }
  for (const [path, current] of counts) {
    if (hasViolations(current) && !owners.has(path)) {
      problems.push(
        `${path} violates (controls ${current.controls}, lookalikes ${current.lookalikes}, ` +
          `literals ${current.literals}) and is in no family`,
      );
    }
  }
  return problems;
}

/**
 * Lowers the ceilings of one family to the current counts.
 *
 * Entries that reach zero are removed. A ceiling is never raised; a file above its ceiling throws.
 *
 * @param family The family file to lower.
 * @param counts The scanned per-file counts.
 * @param seedPaths Violating files without an entry that are added with their current counts.
 */
export function lowerCeilings(
  family: FamilyFile,
  counts: Map<string, FileCounts>,
  seedPaths: string[] = [],
): FamilyFile {
  const files: Record<string, FamilyEntry> = {};
  for (const [path, entry] of Object.entries(family.files)) {
    const current = counts.get(path) ?? emptyCounts();
    for (const field of countFields) {
      if (current[field] > entry[field]) {
        throw new Error(
          `Refusing to raise ${family.family} ${path} ${field} from ${entry[field]} to ${current[field]}.`,
        );
      }
    }
    if (hasViolations(current)) files[path] = { ...current, reason: entry.reason };
  }
  for (const path of seedPaths) {
    const current = counts.get(path) ?? emptyCounts();
    if (hasViolations(current) && !(path in files)) {
      files[path] = { ...current, reason: `converted in ${family.owner}` };
    }
  }
  const sorted: Record<string, FamilyEntry> = {};
  for (const path of Object.keys(files).sort()) sorted[path] = files[path]!;
  return { family: family.family, owner: family.owner, files: sorted };
}

/**
 * Checks that the family entries are exactly the documented allowances.
 *
 * A family entry for a file outside the allowance list fails, so a converted file can never return to the
 * ratchet. An allowance without a reason or without a remaining family entry fails, so the list can only
 * shrink as allowances are converted.
 *
 * @param families Every family file.
 * @param rules The shared rule set that holds the allowance list.
 * @returns A description for every problem; empty when only documented allowances remain.
 */
export function checkAllowances(families: FamilyFile[], rules: Rules): string[] {
  const problems: string[] = [];
  const allowed = new Map(rules.allowedRemainders.map((entry) => [entry.file, entry.reason]));
  const entries = new Set<string>();
  for (const family of families) {
    for (const path of Object.keys(family.files)) {
      entries.add(path);
      if (!allowed.has(path)) {
        problems.push(`${family.family}: ${path} is on the ratchet but not in allowedRemainders`);
      }
    }
  }
  for (const [file, reason] of allowed) {
    if (reason.trim() === "") problems.push(`allowedRemainders: ${file} has no reason`);
    if (!entries.has(file)) {
      problems.push(`allowedRemainders: ${file} is no longer on the ratchet; remove the allowance`);
    }
  }
  return problems;
}
