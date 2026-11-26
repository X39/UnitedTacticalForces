import {
  compositeOver,
  contrastRatio,
  extractColor,
  parseColor,
  parseCustomProperties,
  readProjectFile,
  resolveToken,
} from "../helpers/cssTokens";

const TEXT_MINIMUM = 4.5;
const NON_TEXT_MINIMUM = 3;
const SURFACES = ["--utf-surface-base", "--utf-surface-raised", "--utf-surface-overlay"] as const;

const tokensCss = readProjectFile("src/styles/tokens.css");
const stylesCss = readProjectFile("src/styles.css");
const tokens = parseCustomProperties(tokensCss);
const legacy = parseCustomProperties(stylesCss);

interface Measurement {
  label: string;
  ratio: number;
}

function colorOf(
  name: string,
  tokenMap: ReadonlyMap<string, string> = tokens,
): ReturnType<typeof parseColor> {
  return parseColor(extractColor(resolveToken(name, [tokenMap, legacy])));
}

function foregroundNames(tokenMap: ReadonlyMap<string, string>): string[] {
  return [...tokenMap.keys()].filter((name) => /^--utf-(state|cost)-[a-z]+-fg$/.test(name));
}

/** Measures every state and cost foreground on its own tint composited over each surface. */
function measureTints(tokenMap: ReadonlyMap<string, string>): Measurement[] {
  return foregroundNames(tokenMap).flatMap((foreground) => {
    const family = foreground.slice(0, -"-fg".length);
    expect(tokenMap.has(`${family}-bg`), `${family}-bg is declared`).toBe(true);
    expect(tokenMap.has(`${family}-border`), `${family}-border is declared`).toBe(true);
    const tint = colorOf(`${family}-bg`, tokenMap);
    return SURFACES.map((surface) => {
      const background = compositeOver(tint, colorOf(surface, tokenMap));
      const ratio = contrastRatio(colorOf(foreground, tokenMap), background);
      return { label: `${foreground} on ${family}-bg over ${surface}`, ratio };
    });
  });
}

/** Measures the legacy `--quiet` text colour on the canvas and on every token surface. */
function measureQuiet(legacyMap: ReadonlyMap<string, string>): Measurement[] {
  const quiet = parseColor(extractColor(resolveToken("--quiet", [legacyMap])));
  return ["--canvas", ...SURFACES].map((background) => ({
    label: `--quiet on ${background}`,
    ratio: contrastRatio(
      quiet,
      parseColor(extractColor(resolveToken(background, [tokens, legacyMap]))),
    ),
  }));
}

function expectAtLeast(measurements: Measurement[], minimum: number): void {
  for (const { label, ratio } of measurements) {
    expect(ratio, `${label} ${ratio.toFixed(2)} below ${minimum}`).toBeGreaterThanOrEqual(minimum);
  }
}

describe("design token contrast guard", () => {
  it("keeps every state and cost foreground at 4.5:1 on its own 12% tint over each surface", () => {
    const measurements = measureTints(tokens);

    expect(foregroundNames(tokens)).toHaveLength(8);
    expect(measurements).toHaveLength(24);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });

  it("fails when a state tint is lightened above 12%", () => {
    const lightened = parseCustomProperties(
      tokensCss.replace(
        "--utf-state-danger-bg: rgb(228 119 119 / 12%)",
        "--utf-state-danger-bg: rgb(228 119 119 / 30%)",
      ),
    );

    const failing = measureTints(lightened).filter((entry) => entry.ratio < TEXT_MINIMUM);

    expect(failing.map((entry) => entry.label)).toContain(
      "--utf-state-danger-fg on --utf-state-danger-bg over --utf-surface-overlay",
    );
  });

  it("keeps text tokens at 4.5:1 on every surface and the canvas", () => {
    const backgrounds = [...SURFACES.map((name) => colorOf(name)), colorOf("--canvas", legacy)];
    const measurements = ["--utf-text", "--utf-text-muted", "--utf-text-subtle"].flatMap((name) =>
      backgrounds.map((background, index) => ({
        label: `${name} on background ${index}`,
        ratio: contrastRatio(colorOf(name), background),
      })),
    );

    expect(measurements).toHaveLength(12);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });

  it("keeps the legacy --quiet text colour at 4.5:1 on the canvas and all three surfaces", () => {
    const measurements = measureQuiet(legacy);

    expect(measurements).toHaveLength(4);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });

  it("fails when --quiet returns to the old #778190 value", () => {
    const regressed = parseCustomProperties(
      stylesCss.replace(/--quiet:\s*#[0-9a-fA-F]+;/, "--quiet: #778190;"),
    );

    const failing = measureQuiet(regressed).filter((entry) => entry.ratio < TEXT_MINIMUM);

    expect(failing.map((entry) => entry.label)).toEqual([
      "--quiet on --utf-surface-raised",
      "--quiet on --utf-surface-overlay",
    ]);
  });

  it("keeps the control border and the focus ring at 3:1 on every surface", () => {
    const focusRing = tokens.get("--utf-focus-ring");
    expect(focusRing).toBeDefined();
    const ring = parseColor(extractColor(resolveToken("--utf-focus-ring", [tokens, legacy])));
    const measurements = SURFACES.flatMap((surface) => {
      const background = colorOf(surface);
      return [
        {
          label: `--utf-border-control on ${surface}`,
          ratio: contrastRatio(colorOf("--utf-border-control"), background),
        },
        { label: `--utf-focus-ring on ${surface}`, ratio: contrastRatio(ring, background) },
      ];
    });

    expect(measurements).toHaveLength(6);
    expectAtLeast(measurements, NON_TEXT_MINIMUM);
  });

  it("keeps the chart line at 3:1 on every surface", () => {
    const measurements = SURFACES.map((surface) => ({
      label: `--utf-chart-line on ${surface}`,
      ratio: contrastRatio(colorOf("--utf-chart-line"), colorOf(surface)),
    }));

    expect(measurements).toHaveLength(3);
    expectAtLeast(measurements, NON_TEXT_MINIMUM);
  });

  it("defines the metrics layout and chart tokens with the grid aliasing the divider", () => {
    for (const name of [
      "--utf-metric-chart-min",
      "--utf-metric-chart-height",
      "--utf-metric-tooltip-width",
      "--utf-metric-refetch-opacity",
      "--utf-chart-line",
      "--utf-chart-band",
    ]) {
      expect(tokens.has(name), `${name} is declared`).toBe(true);
    }
    expect(tokens.get("--utf-chart-grid")).toBe("var(--utf-border-divider)");
  });

  it("defines every token the UI contract lists", () => {
    const expected = [
      ...["xs", "sm", "md", "lg", "xl", "2xl", "3xl"].map((size) => `--utf-space-${size}`),
      "--utf-control-height",
      "--utf-control-height-touch",
      "--utf-control-height-compact",
      "--utf-check-size",
      "--utf-switch-track-width",
      "--utf-switch-track-height",
      "--utf-switch-thumb-size",
      "--utf-day-chip-min",
      "--utf-dropzone-min-height",
      "--utf-review-name-min",
      "--utf-choice-card-min",
      "--utf-listbox-max-height",
      "--utf-hover",
      "--utf-dialog-width",
      "--utf-dialog-gutter",
      ...["body", "label", "heading", "display"].map((role) => `--utf-text-${role}`),
      "--utf-weight-regular",
      "--utf-weight-bold",
      ...["body", "label", "heading"].map((role) => `--utf-leading-${role}`),
      "--utf-text",
      "--utf-text-muted",
      "--utf-text-subtle",
      ...SURFACES,
      "--utf-border-divider",
      "--utf-border-control",
      ...["neutral", "info", "success", "warning", "danger"].flatMap((family) =>
        ["fg", "bg", "border"].map((part) => `--utf-state-${family}-${part}`),
      ),
      ...["live", "unconfirmed", "none"].flatMap((family) =>
        ["fg", "bg", "border"].map((part) => `--utf-cost-${family}-${part}`),
      ),
      "--utf-focus-ring",
      "--utf-focus-offset",
      "--utf-elevation-0",
      "--utf-elevation-1",
      "--utf-elevation-2",
      "--utf-radius",
      "--utf-radius-small",
      "--utf-motion-fast",
      "--utf-motion-base",
      "--utf-motion-slow",
      "--utf-motion-ease",
    ];

    expect(expected.filter((name) => !tokens.has(name))).toEqual([]);
    for (const name of expected) {
      expect(() => resolveToken(name, [tokens, legacy]), `${name} resolves`).not.toThrow();
    }
  });

  it("resolves all three motion durations to 0.01ms under reduced motion", () => {
    const reduced = parseCustomProperties(tokensCss, { media: "prefers-reduced-motion: reduce" });

    for (const name of ["--utf-motion-fast", "--utf-motion-base", "--utf-motion-slow"]) {
      expect(reduced.get(name), name).toBe("0.01ms");
      expect(tokens.get(name), `${name} base value`).toMatch(/^\d+ms$/);
    }
  });

  it("imports tokens.css before styles.css in main.tsx", () => {
    const lines = readProjectFile("src/main.tsx").split(/\r?\n/);
    const tokensLine = lines.findIndex((line) => /^import\s+"@\/styles\/tokens\.css";/.test(line));
    const stylesLine = lines.findIndex((line) => /^import\s+"@\/styles\.css";/.test(line));

    expect(tokensLine).toBeGreaterThanOrEqual(0);
    expect(stylesLine).toBeGreaterThanOrEqual(0);
    expect(tokensLine).toBeLessThan(stylesLine);
  });
});

describe("button variant contrast", () => {
  it("keeps the danger label at 4.5:1 on its tint over every surface", () => {
    const tint = colorOf("--utf-state-danger-bg");
    const measurements = SURFACES.map((surface) => ({
      label: `--utf-state-danger-fg on --utf-state-danger-bg over ${surface}`,
      ratio: contrastRatio(colorOf("--utf-state-danger-fg"), compositeOver(tint, colorOf(surface))),
    }));

    expect(measurements).toHaveLength(3);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });

  it("documents why the danger hover keeps the 12% tint: a 20% tint fails 4.5:1 on the overlay surface", () => {
    const danger = colorOf("--danger", legacy);
    const heavierTint = { ...danger, a: 0.2 };
    const overlay = colorOf("--utf-surface-overlay");

    const ratio = contrastRatio(
      colorOf("--utf-state-danger-fg"),
      compositeOver(heavierTint, overlay),
    );

    expect(ratio, `20% danger tint over overlay ${ratio.toFixed(2)}`).toBeLessThan(TEXT_MINIMUM);
  });

  it("keeps the text colour at 4.5:1 on the hover layer over every surface", () => {
    const hover = colorOf("--utf-hover");
    const measurements = SURFACES.map((surface) => ({
      label: `--utf-text on --utf-hover over ${surface}`,
      ratio: contrastRatio(colorOf("--utf-text"), compositeOver(hover, colorOf(surface))),
    }));

    expect(measurements).toHaveLength(3);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });
});

describe("control state contrast", () => {
  // The Disabled row of the control state table (--utf-border-divider) is deliberately not asserted: WCAG 1.4.3 and
  // 1.4.11 exempt inactive components, and the control keeps its label and a visible reason (C-13).

  it("keeps the checked fill and the selected card border at 3:1 on every surface", () => {
    const measurements = SURFACES.flatMap((surface) => [
      {
        label: `checked fill --utf-text on ${surface}`,
        ratio: contrastRatio(colorOf("--utf-text"), colorOf(surface)),
      },
      {
        label: `selected card border --utf-text on ${surface}`,
        ratio: contrastRatio(colorOf("--utf-text"), colorOf(surface)),
      },
    ]);

    expect(measurements).toHaveLength(6);
    expectAtLeast(measurements, NON_TEXT_MINIMUM);
  });

  it("keeps the off switch thumb at 3:1 against its track fill", () => {
    const ratio = contrastRatio(colorOf("--utf-text-muted"), colorOf("--utf-surface-overlay"));

    expectAtLeast(
      [{ label: "switch thumb --utf-text-muted on track --utf-surface-overlay", ratio }],
      NON_TEXT_MINIMUM,
    );
  });

  it("keeps the glyph on the checked fill and the choice card text at 4.5:1", () => {
    const measurements = [
      {
        label: "glyph --utf-surface-base on checked fill --utf-text",
        ratio: contrastRatio(colorOf("--utf-surface-base"), colorOf("--utf-text")),
      },
      ...(["--utf-surface-raised", "--utf-surface-overlay"] as const).map((surface) => ({
        label: `choice card text --utf-text on ${surface}`,
        ratio: contrastRatio(colorOf("--utf-text"), colorOf(surface)),
      })),
    ];

    expect(measurements).toHaveLength(3);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });
});

describe("search picker contrast", () => {
  // The active option is drawn with the --utf-hover layer over the overlay surface of the listbox; the raised surface
  // is measured too because the same fill can sit over a panel body.
  const PICKER_SURFACES = ["--utf-surface-overlay", "--utf-surface-raised"] as const;

  it("keeps the option name and the muted detail at 4.5:1 on the active option fill", () => {
    const hover = colorOf("--utf-hover");
    const measurements = (["--utf-text", "--utf-text-muted"] as const).flatMap((foreground) =>
      PICKER_SURFACES.map((surface) => ({
        label: `${foreground} on the active option fill over ${surface}`,
        ratio: contrastRatio(colorOf(foreground), compositeOver(hover, colorOf(surface))),
      })),
    );

    expect(measurements).toHaveLength(4);
    expectAtLeast(measurements, TEXT_MINIMUM);
  });

  it("keeps the listbox and token border at 3:1 against the overlay surface", () => {
    const ratio = contrastRatio(colorOf("--utf-border-control"), colorOf("--utf-surface-overlay"));

    expectAtLeast(
      [{ label: "--utf-border-control on --utf-surface-overlay", ratio }],
      NON_TEXT_MINIMUM,
    );
  });

  it("keeps the error row text at 4.5:1 on the listbox surface", () => {
    const ratio = contrastRatio(colorOf("--utf-state-danger-fg"), colorOf("--utf-surface-overlay"));

    expectAtLeast(
      [{ label: "--utf-state-danger-fg on --utf-surface-overlay", ratio }],
      TEXT_MINIMUM,
    );
  });
});

describe("css token helper", () => {
  it("measures black on white as 21 and composites alpha colours", () => {
    expect(contrastRatio(parseColor("#000"), parseColor("#ffffff"))).toBeCloseTo(21, 5);
    expect(contrastRatio(parseColor("#777"), parseColor("#777"))).toBeCloseTo(1, 5);
    expect(compositeOver(parseColor("rgb(255 0 0 / 50%)"), parseColor("#000000"))).toEqual({
      r: 127.5,
      g: 0,
      b: 0,
      a: 1,
    });
  });

  it("parses the supported colour notations", () => {
    expect(parseColor("#abc")).toEqual({ r: 170, g: 187, b: 204, a: 1 });
    expect(parseColor("rgb(1, 2, 3)")).toEqual({ r: 1, g: 2, b: 3, a: 1 });
    expect(parseColor("rgb(1 2 3 / 12%)")).toEqual({ r: 1, g: 2, b: 3, a: 0.12 });
    expect(() => parseColor("hsl(0 0% 0%)")).toThrow();
  });

  it("resolves var chains across maps and rejects cycles and missing targets", () => {
    const first = new Map([["--a", "var(--b)"]]);
    const second = new Map([["--b", "#123456"]]);

    expect(resolveToken("--a", [first, second])).toBe("#123456");
    expect(() => resolveToken("--a", [first])).toThrow(/not declared/);
    expect(() =>
      resolveToken("--x", [
        new Map([
          ["--x", "var(--y)"],
          ["--y", "var(--x)"],
        ]),
      ]),
    ).toThrow(/cycle/);
  });

  it("reads only root rules and ignores comments", () => {
    const css =
      "/* --c: 1; */ :root { --a: 1; } .x { --b: 2; } @media (min-width: 1px) { :root { --d: 3; } }";

    expect([...parseCustomProperties(css)]).toEqual([["--a", "1"]]);
    expect([...parseCustomProperties(css, { media: "min-width" })]).toEqual([["--d", "3"]]);
  });
});
