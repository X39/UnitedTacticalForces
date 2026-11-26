/** Breakpoints in pixels that component CSS may use inside media queries. */
const allowedMediaBreakpoints = new Set(["520px", "800px"]);

/** Border properties on which a literal 1px width is allowed. */
const borderProperty =
  /^border(?:-(?:top|right|bottom|left|block|inline)(?:-(?:start|end))?)?(?:-width)?$/;

const hexColour = /#[0-9a-fA-F]{3,8}\b/;
const pixelLiteral = /(?<![\w.-])-?\d*\.?\d+px\b/g;
const colourFunction = /\b(?:rgb|rgba|hsl|hsla|hwb|lab|lch|oklab|oklch|color|color-mix)\(/i;

/** Properties that set a font size; a viewport-relative value in them makes the type scale fluid. */
const fontSizeProperty = /^(?:font|font-size)$/;
const viewportRelativeType =
  /\bclamp\(|(?<![\w.-])-?\d*\.?\d+(?:vw|vh|vmin|vmax|svw|svh|lvw|lvh|dvw|dvh)\b/i;

/** The property that, set to `auto`, gives a control its browser-drawn look back. */
const appearanceProperty = /^(?:-webkit-)?appearance$/;
const autoAppearanceValue = /^auto\b/i;

/** Replaces CSS comments with the same number of line breaks so reported line numbers stay accurate. */
function stripComments(css: string): string {
  return css.replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, ""));
}

function lineOf(css: string, index: number): number {
  return css.slice(0, index).split("\n").length;
}

function describePixelViolations(
  property: string,
  value: string,
  line: number,
  violations: string[],
): void {
  for (const match of value.matchAll(pixelLiteral)) {
    const literal = match[0];
    const allowed = literal === "1px" && borderProperty.test(property);
    if (!allowed) {
      violations.push(
        `line ${line}: pixel literal "${literal}" in "${property}" must be a --utf-* token`,
      );
    }
  }
}

/**
 * Scans component CSS for values that must come from the shared design tokens.
 *
 * Reported violations are raw hex colours, colour functions, pixel literals other than a 1px border width,
 * viewport-relative font sizes (`clamp()` and viewport units in `font` and `font-size`), media-query pixel
 * breakpoints other than 520px and 800px, rules that remove the outline without a replacement ring, any
 * `accent-color` declaration and an `appearance: auto` value (both bring back the browser-drawn mark; `appearance:
 * none` and `appearance: base-select` are accepted).
 *
 * @param css The stylesheet text.
 * @returns One message per violation, each prefixed with the 1-based line number; empty when clean.
 */
export function findStyleViolations(css: string): string[] {
  const source = stripComments(css);
  const violations: string[] = [];

  for (const match of source.matchAll(/@media([^{]*)\{/g)) {
    const prelude = match[1] ?? "";
    for (const literal of prelude.match(pixelLiteral) ?? []) {
      if (!allowedMediaBreakpoints.has(literal)) {
        violations.push(
          `line ${lineOf(source, match.index)}: media breakpoint "${literal}" is not 520px or 800px`,
        );
      }
    }
  }

  for (const match of source.matchAll(/([a-zA-Z-]+)\s*:\s*([^;{}]+)(?=;|\})/g)) {
    const property = (match[1] ?? "").toLowerCase();
    const value = match[2] ?? "";
    const line = lineOf(source, match.index);
    if (hexColour.test(value)) {
      violations.push(`line ${line}: raw hex colour in "${property}" must be a --utf-* token`);
    }
    if (colourFunction.test(value)) {
      violations.push(`line ${line}: colour function in "${property}" must be a --utf-* token`);
    }
    if (fontSizeProperty.test(property) && viewportRelativeType.test(value)) {
      violations.push(
        `line ${line}: viewport-relative type in "${property}" must be a --utf-text-* token`,
      );
    }
    if (property === "accent-color") {
      violations.push(
        `line ${line}: "accent-color" draws the browser mark; use the shared drawn controls`,
      );
    }
    if (appearanceProperty.test(property) && autoAppearanceValue.test(value.trim())) {
      violations.push(
        `line ${line}: "${property}: auto" draws the browser control; use the shared drawn controls`,
      );
    }
    describePixelViolations(property, value, line, violations);
  }

  for (const removal of findOutlineRemovals(css)) {
    if (!removal.replaced) {
      violations.push(
        `line ${removal.line}: "${removal.selector}" removes the outline without a box-shadow or outline-offset replacement ring`,
      );
    }
  }
  return violations;
}

/** One style rule of a stylesheet: its selector list, its declaration block and where it starts. */
export interface CssRule {
  /** The trimmed selector list, or the at-rule prelude for a leaf at-rule such as `@font-face`. */
  selector: string;
  /** The declaration block between the braces, comments removed. */
  body: string;
  /** The 1-based line of the selector. */
  line: number;
}

/**
 * Splits a stylesheet into its leaf rules, descending into nested at-rules such as media queries.
 *
 * @param css The stylesheet text.
 * @returns Every leaf rule in document order, with comments removed from the bodies.
 */
export function collectStyleRules(css: string): CssRule[] {
  const source = stripComments(css);
  const rules: CssRule[] = [];

  function scan(start: number, end: number): void {
    let cursor = start;
    while (cursor < end) {
      const open = source.indexOf("{", cursor);
      if (open < 0 || open >= end) return;
      const boundary = Math.max(
        source.lastIndexOf(";", open - 1),
        source.lastIndexOf("}", open - 1),
        cursor - 1,
      );
      let depth = 1;
      let close = open + 1;
      while (close < end && depth > 0) {
        const character = source[close];
        if (character === "{") depth += 1;
        else if (character === "}") depth -= 1;
        close += 1;
      }
      const rawPrelude = source.slice(boundary + 1, open);
      const selector = rawPrelude.trim();
      const body = source.slice(open + 1, close - 1);
      const leading = rawPrelude.length - rawPrelude.trimStart().length;
      if (selector.startsWith("@") && body.includes("{")) {
        scan(open + 1, close - 1);
      } else {
        rules.push({ selector, body, line: lineOf(source, boundary + 1 + leading) });
      }
      cursor = close;
    }
  }

  scan(0, source.length);
  return rules;
}

/** One declaration that removes the focus outline. */
export interface OutlineRemoval {
  /** The selector list of the rule that holds the declaration. */
  selector: string;
  /** The 1-based line of the rule. */
  line: number;
  /** Whether the same rule declares a `box-shadow` or an `outline-offset` replacement ring. */
  replaced: boolean;
}

const outlineRemovalValue = /^(?:none|0|0px)(?:\s|$)/i;

/**
 * Finds every declaration that removes the outline: `outline: none`, `outline: 0`, `outline-style: none` and
 * `outline-width: 0`.
 *
 * @param css The stylesheet text.
 * @returns One entry per removing declaration; `replaced` tells whether the rule keeps a replacement ring.
 */
export function findOutlineRemovals(css: string): OutlineRemoval[] {
  const removals: OutlineRemoval[] = [];
  for (const rule of collectStyleRules(css)) {
    const declarations = rule.body.split(";").flatMap((declaration) => {
      const separator = declaration.indexOf(":");
      if (separator < 0) return [];
      return [
        {
          property: declaration.slice(0, separator).trim().toLowerCase(),
          value: declaration.slice(separator + 1).trim(),
        },
      ];
    });
    const removing = declarations.filter(
      ({ property, value }) =>
        (property === "outline" && outlineRemovalValue.test(value)) ||
        (property === "outline-style" && /^none$/i.test(value)) ||
        (property === "outline-width" && /^0(?:px)?$/i.test(value)),
    );
    const replaced = declarations.some(
      ({ property, value }) =>
        property === "outline-offset" || (property === "box-shadow" && !/^none$/i.test(value)),
    );
    for (let index = 0; index < removing.length; index++) {
      removals.push({ selector: rule.selector, line: rule.line, replaced });
    }
  }
  return removals;
}

/** The literal counts the legacy stylesheet ratchet holds. */
export interface LegacyCssMeasure {
  /** Raw hex colour literals in declaration values, custom property definitions included. */
  hex: number;
  /** Pixel literals that are not a multiple of 4, other than a 1px border width. */
  offGridPx: number;
  /** Declarations that remove the focus outline. */
  outlineRemoval: number;
}

/**
 * Counts the raw hex colours, off-grid pixel literals and outline removals of a legacy stylesheet.
 *
 * @param css The stylesheet text.
 * @returns The three counts the ratchet compares against its ceilings.
 */
export function measureLegacyCss(css: string): LegacyCssMeasure {
  const source = stripComments(css);
  let hex = 0;
  let offGridPx = 0;
  for (const match of source.matchAll(/([a-zA-Z-]+)\s*:\s*([^;{}]+)(?=;|\})/g)) {
    const property = (match[1] ?? "").toLowerCase();
    const value = match[2] ?? "";
    hex += (value.match(new RegExp(hexColour.source, "g")) ?? []).length;
    for (const literal of value.match(pixelLiteral) ?? []) {
      const onGrid = Number.parseFloat(literal) % 4 === 0;
      const borderWidth = literal === "1px" && borderProperty.test(property);
      if (!onGrid && !borderWidth) offGridPx += 1;
    }
  }
  return { hex, offGridPx, outlineRemoval: findOutlineRemovals(css).length };
}
