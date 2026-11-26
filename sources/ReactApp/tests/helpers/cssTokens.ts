import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

/** An RGBA colour with channels in the 0 to 255 range and alpha in the 0 to 1 range. */
export interface Rgba {
  r: number;
  g: number;
  b: number;
  a: number;
}

/** Options for {@link parseCustomProperties}. */
export interface ParseOptions {
  /** When set, only `:root` rules nested in an at-rule whose prelude contains this text are read. */
  media?: string;
}

interface Rule {
  selector: string;
  parents: string[];
  body: string;
}

const projectRoot = resolve(import.meta.dirname, "..", "..");

/** Reads a UTF-8 file relative to the React application project root. */
export function readProjectFile(path: string): string {
  return readFileSync(join(projectRoot, path), "utf8");
}

function stripComments(css: string): string {
  return css.replace(/\/\*[\s\S]*?\*\//g, "");
}

function scanRules(css: string, parents: string[], rules: Rule[]): void {
  let index = 0;
  while (index < css.length) {
    const open = css.indexOf("{", index);
    if (open < 0) return;
    const rawPrelude = css.slice(index, open);
    const prelude = rawPrelude.slice(rawPrelude.lastIndexOf(";") + 1).trim();
    let depth = 1;
    let cursor = open + 1;
    while (cursor < css.length && depth > 0) {
      const character = css[cursor];
      if (character === "{") depth += 1;
      else if (character === "}") depth -= 1;
      cursor += 1;
    }
    const body = css.slice(open + 1, cursor - 1);
    if (prelude.startsWith("@")) scanRules(body, [...parents, prelude], rules);
    else rules.push({ selector: prelude, parents, body });
    index = cursor;
  }
}

/**
 * Returns every `--name: value` declaration found in `:root` rules, ignoring comments. Without
 * `options.media` only top-level `:root` rules count; with it only `:root` rules inside a matching at-rule count.
 * Later declarations override earlier ones, as in the cascade.
 */
export function parseCustomProperties(
  css: string,
  options: ParseOptions = {},
): Map<string, string> {
  const rules: Rule[] = [];
  scanRules(stripComments(css), [], rules);
  const properties = new Map<string, string>();
  for (const rule of rules) {
    if (rule.selector !== ":root") continue;
    const nested = rule.parents.length > 0;
    if (
      options.media === undefined
        ? nested
        : !rule.parents.some((p) => p.includes(options.media ?? ""))
    ) {
      continue;
    }
    for (const match of rule.body.matchAll(/(--[\w-]+)\s*:\s*([^;]+);?/g)) {
      properties.set(match[1]!, match[2]!.trim());
    }
  }
  return properties;
}

const variablePattern = /var\(\s*(--[\w-]+)\s*(?:,[^)]*)?\)/g;

/**
 * Resolves a custom property to a literal value by following `var()` references across the given maps. The first
 * map that declares a name wins. Throws on a cycle or when a referenced property is not declared anywhere.
 */
export function resolveToken(
  name: string,
  maps: readonly ReadonlyMap<string, string>[],
  trail: readonly string[] = [],
): string {
  if (trail.includes(name))
    throw new Error(`Custom property cycle: ${[...trail, name].join(" -> ")}`);
  const raw = maps.map((map) => map.get(name)).find((value) => value !== undefined);
  if (raw === undefined) throw new Error(`Custom property ${name} is not declared`);
  return raw.replace(variablePattern, (_whole, target: string) =>
    resolveToken(target, maps, [...trail, name]),
  );
}

function channel(text: string): number {
  const value = Number(text.trim());
  if (!Number.isFinite(value)) throw new Error(`Invalid colour channel: ${text}`);
  return value;
}

function alphaValue(text: string | undefined): number {
  if (text === undefined) return 1;
  const trimmed = text.trim();
  const value = trimmed.endsWith("%") ? Number(trimmed.slice(0, -1)) / 100 : Number(trimmed);
  if (!Number.isFinite(value)) throw new Error(`Invalid alpha value: ${text}`);
  return value;
}

/** Parses `#rgb`, `#rrggbb`, `rgb(r g b)`, `rgb(r, g, b)` and `rgb(r g b / p%)` into an {@link Rgba}. */
export function parseColor(value: string): Rgba {
  const text = value.trim().toLowerCase();
  const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/.exec(text);
  if (hex) {
    const digits = hex[1]!.length === 3 ? [...hex[1]!].map((d) => d + d).join("") : hex[1]!;
    return {
      r: parseInt(digits.slice(0, 2), 16),
      g: parseInt(digits.slice(2, 4), 16),
      b: parseInt(digits.slice(4, 6), 16),
      a: 1,
    };
  }
  const functional = /^rgba?\(\s*([^)]*)\)$/.exec(text);
  if (functional) {
    const [colour = "", alpha] = functional[1]!.split("/");
    const parts = colour.includes(",") ? colour.split(",") : colour.trim().split(/\s+/);
    const [r, g, b, commaAlpha] = parts;
    if (r === undefined || g === undefined || b === undefined)
      throw new Error(`Unsupported colour: ${value}`);
    return { r: channel(r), g: channel(g), b: channel(b), a: alphaValue(alpha ?? commaAlpha) };
  }
  throw new Error(`Unsupported colour: ${value}`);
}

/** Extracts the first colour literal from a shorthand value such as `2px solid #dec982`. */
export function extractColor(value: string): string {
  const match = /#[0-9a-fA-F]{3,6}\b|rgba?\([^)]*\)/.exec(value);
  if (!match) throw new Error(`No colour found in: ${value}`);
  return match[0];
}

/** Alpha-composites a translucent foreground over an opaque background and returns an opaque colour. */
export function compositeOver(foreground: Rgba, background: Rgba): Rgba {
  if (background.a !== 1) throw new Error("The background must be opaque");
  const mix = (top: number, bottom: number): number =>
    top * foreground.a + bottom * (1 - foreground.a);
  return {
    r: mix(foreground.r, background.r),
    g: mix(foreground.g, background.g),
    b: mix(foreground.b, background.b),
    a: 1,
  };
}

function relativeLuminance(colour: Rgba): number {
  const linear = (value: number): number => {
    const scaled = value / 255;
    return scaled <= 0.03928 ? scaled / 12.92 : ((scaled + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * linear(colour.r) + 0.7152 * linear(colour.g) + 0.0722 * linear(colour.b);
}

/** WCAG 2.x contrast ratio between two opaque colours. */
export function contrastRatio(foreground: Rgba, background: Rgba): number {
  if (foreground.a !== 1 || background.a !== 1) throw new Error("Contrast needs opaque colours");
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}
