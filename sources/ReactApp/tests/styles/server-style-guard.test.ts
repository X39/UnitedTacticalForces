import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import {
  collectStyleRules,
  findOutlineRemovals,
  findStyleViolations,
  measureLegacyCss,
} from "../helpers/cssGuard";

const serverDirectory = resolve(import.meta.dirname, "..", "..", "src", "components", "server");
const sharedPrimitives = resolve(
  import.meta.dirname,
  "..",
  "..",
  "src",
  "components",
  "shared-primitives.css",
);

const actionControl = resolve(
  import.meta.dirname,
  "..",
  "..",
  "src",
  "components",
  "action-control.css",
);

const searchPicker = resolve(
  import.meta.dirname,
  "..",
  "..",
  "src",
  "components",
  "search-picker.css",
);

function serverStylesheets(): string[] {
  return readdirSync(serverDirectory).filter((name) => name.endsWith(".css"));
}

describe("server component style guard", () => {
  it("flags a raw hex colour", () => {
    expect(findStyleViolations(".a { color: #e1b968; }")).toEqual([
      expect.stringContaining("raw hex colour"),
    ]);
    expect(findStyleViolations(".a { background: #fff }")).toHaveLength(1);
  });

  it("flags colour functions that bypass tokens", () => {
    expect(findStyleViolations(".a { background: rgb(1 2 3 / 50%); }")).toEqual([
      expect.stringContaining("colour function"),
    ]);
  });

  it("flags a pixel literal in a spacing property", () => {
    expect(findStyleViolations(".a { padding: 4px 8px; }")).toHaveLength(2);
    expect(findStyleViolations(".a { gap: 2px; }")).toHaveLength(1);
    expect(findStyleViolations(".a { border-radius: 1px; }")).toHaveLength(1);
  });

  it("allows a 1px border width but no other border pixel width", () => {
    expect(findStyleViolations(".a { border: 1px solid var(--utf-state-info-border); }")).toEqual(
      [],
    );
    expect(findStyleViolations(".a { border-bottom: 1px dashed var(--x); }")).toEqual([]);
    expect(findStyleViolations(".a { border: 2px solid var(--x); }")).toHaveLength(1);
  });

  it("allows only the 520px and 800px media breakpoints", () => {
    expect(findStyleViolations("@media (max-width: 520px) { .a { display: block; } }")).toEqual([]);
    expect(findStyleViolations("@media (min-width: 800px) { .a { display: block; } }")).toEqual([]);
    expect(findStyleViolations("@media (max-width: 640px) { .a { display: block; } }")).toEqual([
      expect.stringContaining("media breakpoint"),
    ]);
  });

  it("ignores comments and reports accurate line numbers", () => {
    const css = "/* #fff 4px */\n.a {\n  color: var(--utf-text);\n  margin: 3px;\n}";
    expect(findStyleViolations(css)).toEqual([expect.stringContaining("line 4:")]);
  });

  it("accepts token references and unitless or relative values", () => {
    const css =
      ".a { padding: var(--utf-space-xs) var(--utf-space-sm); width: 1rem; line-height: 1.35; }";
    expect(findStyleViolations(css)).toEqual([]);
  });

  it("flags viewport-relative type but allows viewport units in layout widths", () => {
    expect(findStyleViolations(".a { font-size: clamp(1rem, 3vw, 2rem); }")).toEqual([
      expect.stringContaining("viewport-relative type"),
    ]);
    expect(findStyleViolations(".a { font: 700 4vw/1.2 var(--font-body); }")).toHaveLength(1);
    expect(findStyleViolations(".a { font-size: var(--utf-text-display); }")).toEqual([]);
    expect(
      findStyleViolations(
        ".a { width: min(var(--utf-dialog-width), 100vw - var(--utf-dialog-gutter)); }",
      ),
    ).toEqual([]);
  });

  it("flags an outline removal that keeps no replacement ring in the same rule", () => {
    expect(findStyleViolations(".a:focus-visible { outline: none; }")).toEqual([
      expect.stringContaining("removes the outline"),
    ]);
    expect(findStyleViolations(".a input { border: 0; outline: 0; }")).toHaveLength(1);
    expect(findStyleViolations(".a { outline-style: none; }")).toHaveLength(1);
    expect(findStyleViolations(".a { outline-width: 0; }")).toHaveLength(1);
    expect(findStyleViolations("@media (max-width: 800px) {\n.a { outline: none; }\n}")).toEqual([
      expect.stringContaining("line 2:"),
    ]);
  });

  it("accepts an outline removal that keeps a box-shadow or outline-offset ring", () => {
    expect(
      findStyleViolations(
        ".a:focus-visible { outline: none; box-shadow: 0 0 0 var(--utf-space-xs) var(--utf-text); }",
      ),
    ).toEqual([]);
    expect(
      findStyleViolations(
        ".a:focus-visible { outline: 0; outline-offset: var(--utf-focus-offset); }",
      ),
    ).toEqual([]);
    expect(findStyleViolations(".a { box-shadow: none; outline: none; }")).toHaveLength(1);
    expect(
      findStyleViolations(
        ".a:focus-visible { outline: var(--utf-focus-ring); outline-offset: 2px; }",
      ),
    ).toHaveLength(1);
  });

  it("reports every outline removal with its selector and replacement state", () => {
    const css =
      ".a { outline: none; box-shadow: var(--x); }\n.b, .c { outline: 0; }\n.d { outline: var(--y); }";

    expect(findOutlineRemovals(css)).toEqual([
      { selector: ".a", line: 1, replaced: true },
      { selector: ".b, .c", line: 2, replaced: false },
    ]);
  });

  it("collects leaf rules through media queries with their line numbers", () => {
    const css = "/* c */\n.a { color: red; }\n@media (max-width: 800px) {\n  .b { gap: 1px; }\n}";

    expect(collectStyleRules(css).map(({ selector, line }) => [selector, line])).toEqual([
      [".a", 2],
      [".b", 4],
    ]);
  });

  it("measures hex colours, off-grid pixel literals and outline removals", () => {
    const css = [
      ".a { color: #fff; background: #123456; margin: 3px 8px; border: 1px solid #000; }",
      ".b { padding: 10px; outline: none; } .c { border: 2px solid red; }",
    ].join("\n");

    expect(measureLegacyCss(css)).toEqual({ hex: 3, offGridPx: 3, outlineRemoval: 1 });
  });

  it("covers at least one real stylesheet", () => {
    expect(serverStylesheets().length).toBeGreaterThan(0);
  });

  it("covers the shared primitive stylesheet", () => {
    expect(existsSync(sharedPrimitives), "src/components/shared-primitives.css exists").toBe(true);
    expect(readFileSync(sharedPrimitives, "utf8").length).toBeGreaterThan(0);
  });

  it("keeps shared-primitives.css free of raw colours, pixel literals and viewport type", () => {
    expect(findStyleViolations(readFileSync(sharedPrimitives, "utf8"))).toEqual([]);
  });

  it("keeps action-control.css free of raw colours, pixel literals and viewport type", () => {
    expect(existsSync(actionControl), "src/components/action-control.css exists").toBe(true);
    expect(findStyleViolations(readFileSync(actionControl, "utf8"))).toEqual([]);
  });

  it("keeps search-picker.css free of raw colours, pixel literals and viewport type", () => {
    expect(existsSync(searchPicker), "src/components/search-picker.css exists").toBe(true);
    expect(findStyleViolations(readFileSync(searchPicker, "utf8"))).toEqual([]);
  });

  it("flags accent-color and appearance auto because they draw the browser mark", () => {
    expect(findStyleViolations(".a { accent-color: red; }")).toEqual([
      expect.stringContaining("accent-color"),
    ]);
    expect(findStyleViolations(".a { accent-color: var(--utf-text); }")).toHaveLength(1);
    expect(findStyleViolations(".a { appearance: auto; }")).toEqual([
      expect.stringContaining("appearance: auto"),
    ]);
    expect(findStyleViolations(".a { -webkit-appearance: auto }")).toHaveLength(1);
  });

  it("accepts appearance none and appearance base-select", () => {
    expect(findStyleViolations(".a { appearance: none; }")).toEqual([]);
    expect(findStyleViolations(".a { appearance: base-select; }")).toEqual([]);
  });

  it("flags a planted color-mix in a guarded button sheet", () => {
    expect(
      findStyleViolations(
        ".button:hover { background: color-mix(in srgb, var(--utf-text) 8%, transparent); }",
      ),
    ).toEqual([expect.stringContaining("colour function")]);
  });

  it("keeps gold out of every ghost hover rule", () => {
    const ghostHoverRules = collectStyleRules(readFileSync(actionControl, "utf8")).filter((rule) =>
      rule.selector.includes(".button--ghost:hover"),
    );
    expect(ghostHoverRules.length, "a .button--ghost:hover rule exists").toBeGreaterThan(0);
    for (const rule of ghostHoverRules) {
      expect(rule.body, `${rule.selector} mentions gold`).not.toMatch(/--gold/);
    }
  });

  it.each(serverStylesheets())("keeps %s free of raw colours and pixel literals", (name) => {
    const css = readFileSync(join(serverDirectory, name), "utf8");
    expect(findStyleViolations(css)).toEqual([]);
  });
});
