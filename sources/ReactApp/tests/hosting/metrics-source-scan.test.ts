import { readFileSync } from "node:fs";
import { resolve } from "node:path";

/*
 * MET-05 and the supply-chain rule of Phase 67: the metrics UI polls nothing, starts no timer of its own, imports no
 * charting package and adds no dependency. History reloads come from REST and from frame-triggered invalidation.
 */

const projectRoot = resolve(import.meta.dirname, "..", "..");
const sourceRoot = resolve(projectRoot, "src");

/** The files of the metrics UI; the shared `useNow` clock lives elsewhere and is the only allowed time source. */
const metricsFiles = [
  "components/server/ServerMetricsPanel.tsx",
  "components/server/MetricTile.tsx",
  "components/server/MetricLineChart.tsx",
  "components/server/MetricTable.tsx",
  "features/hosting/metricsModel.ts",
  "features/hosting/metricsFrames.ts",
  "features/hosting/useServerMetrics.ts",
  "features/hosting/useMeasuredWidth.ts",
];

/** A react-query polling option or a raw timer: any of them would be client polling or an extra timer. */
const forbiddenTimers =
  /\brefetchInterval(?:InBackground)?\b|\bsetInterval\b|\bsetTimeout\b|\brequestIdleCallback\b/;

/** The charting packages the metrics UI must never import (the charts are hand-built SVG). */
const chartPackages = [
  "recharts",
  "d3",
  "d3-[a-z-]+",
  "chart\\.js",
  "react-chartjs-2",
  "victory[a-z-]*",
  "@visx/[a-z-]+",
  "@nivo/[a-z-]+",
  "echarts[a-z-]*",
  "apexcharts",
  "react-apexcharts",
  "plotly\\.js[a-z-]*",
  "react-plotly\\.js",
  "uplot[a-z-]*",
  "highcharts[a-z-]*",
  "chartist",
  "@mui/x-charts",
  "@fluentui/react-charting",
  "@fluentui/react-charts",
  "react-charts",
  "lightweight-charts",
  "billboard\\.js",
  "frappe-charts",
];

const chartPackagePattern = new RegExp(`^(?:${chartPackages.join("|")})(?:/.*)?$`);

/** The runtime dependencies this phase starts with; adding any package, even a dev tool, must be a decision. */
const recordedDependencies: Record<string, string> = {
  "@adobe/mdast-util-gridtables": "4.0.19",
  "@adobe/remark-gridtables": "3.0.20",
  "@fluentui/react-components": "9.74.6",
  "@fluentui/react-icons": "2.0.320",
  "@hookform/resolvers": "5.2.2",
  "@tanstack/react-query": "5.101.4",
  "@types/mdast": "4.0.4",
  "date-fns": "4.1.0",
  "highlight.js": "11.11.1",
  i18next: "26.3.6",
  "i18next-browser-languagedetector": "8.2.0",
  react: "19.2.8",
  "react-dom": "19.2.8",
  "react-hook-form": "7.85.0",
  "react-i18next": "17.0.11",
  "react-markdown": "10.1.0",
  "react-router": "7.18.2",
  "rehype-highlight": "7.0.2",
  "rehype-slug": "6.0.0",
  "remark-deflist": "1.0.0",
  "remark-directive": "4.0.0",
  "remark-flexible-markers": "1.3.5",
  "remark-gfm": "4.0.1",
  "unist-util-visit": "5.1.0",
  zod: "4.4.3",
};

/** Removes block and line comments so prose in documentation cannot trigger or hide a finding. */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
}

/**
 * Lists the polling and timer patterns found in a source text.
 *
 * @param source The source text to scan.
 */
function findTimers(source: string): string[] {
  const match = forbiddenTimers.exec(stripComments(source));
  return match ? [match[0]] : [];
}

/**
 * Lists the module specifiers a source text imports, requires or dynamically imports.
 *
 * @param source The source text to scan.
 */
function importedModules(source: string): string[] {
  const code = stripComments(source);
  const specifiers = [
    ...code.matchAll(/\bfrom\s+["']([^"']+)["']/g),
    ...code.matchAll(/\bimport\s*\(\s*["']([^"']+)["']\s*\)/g),
    ...code.matchAll(/\brequire\s*\(\s*["']([^"']+)["']\s*\)/g),
    ...code.matchAll(/\bimport\s+["']([^"']+)["']/g),
  ];
  return specifiers.map((match) => match[1] ?? "");
}

/**
 * Lists the imported charting packages of a source text.
 *
 * @param source The source text to scan.
 */
function findChartPackages(source: string): string[] {
  return importedModules(source).filter((specifier) => chartPackagePattern.test(specifier));
}

describe("metrics source scan", () => {
  it("finds a timer or a polling option in an inline bad sample, so the scan is live", () => {
    expect(findTimers("window.setInterval(tick, 1000);")).toEqual(["setInterval"]);
    expect(findTimers("setTimeout(() => refresh(), 10);")).toEqual(["setTimeout"]);
    expect(findTimers("useQuery({ queryKey, refetchInterval: 15_000 });")).toEqual([
      "refetchInterval",
    ]);
  });

  it("ignores comments and similar words in a clean sample", () => {
    const clean = [
      "/** There is no setInterval and no refetchInterval here. */",
      "// setTimeout is never called",
      "const intervalLabel = 3;",
    ].join("\n");
    expect(findTimers(clean)).toEqual([]);
  });

  it("finds a charting import in an inline bad sample, so the scan is live", () => {
    expect(findChartPackages('import { LineChart } from "recharts";')).toEqual(["recharts"]);
    expect(findChartPackages('import { scaleLinear } from "d3-scale";')).toEqual(["d3-scale"]);
    expect(findChartPackages('const chart = await import("chart.js/auto");')).toEqual([
      "chart.js/auto",
    ]);
    expect(findChartPackages('import { useState } from "react";')).toEqual([]);
  });

  it.each(metricsFiles)("%s holds no refetch interval and no raw timer", (file) => {
    const source = readFileSync(resolve(sourceRoot, file), "utf8");
    expect(source.length).toBeGreaterThan(0);
    expect(findTimers(source)).toEqual([]);
  });

  it.each(metricsFiles)("%s imports no charting package", (file) => {
    const source = readFileSync(resolve(sourceRoot, file), "utf8");
    expect(findChartPackages(source)).toEqual([]);
  });

  it("keeps the package.json dependencies equal to the recorded list", () => {
    const manifest = JSON.parse(readFileSync(resolve(projectRoot, "package.json"), "utf8")) as {
      dependencies: Record<string, string>;
      devDependencies: Record<string, string>;
    };

    expect(manifest.dependencies).toEqual(recordedDependencies);
  });

  it("lists no charting package in any dependency section", () => {
    const manifest = JSON.parse(readFileSync(resolve(projectRoot, "package.json"), "utf8")) as {
      dependencies: Record<string, string>;
      devDependencies: Record<string, string>;
    };
    const names = [...Object.keys(manifest.dependencies), ...Object.keys(manifest.devDependencies)];

    expect(names.filter((name) => chartPackagePattern.test(name))).toEqual([]);
  });
});

describe("metrics stylesheet contract", () => {
  const css = readFileSync(resolve(sourceRoot, "components/server/metrics.css"), "utf8");

  it("stacks the toolbar at 520px and makes the toolbar controls touch sized at 800px", () => {
    expect(css).toMatch(
      /@media \(max-width: 800px\)\s*\{\s*\.server-metrics__toolbar \.button\s*\{\s*min-height: var\(--utf-control-height-touch\)/,
    );
    expect(css).toMatch(
      /@media \(max-width: 520px\)\s*\{[^@]*\.server-metrics__toolbar\s*\{\s*flex-direction: column/,
    );
  });

  it("draws the forced-colour chart with system colours and no band", () => {
    const forced = /@media \(forced-colors: active\)\s*\{([\s\S]*)$/.exec(css)?.[1] ?? "";
    expect(forced).toMatch(/\.metric-chart__line[^{]*\{\s*stroke: CanvasText/);
    expect(forced).toMatch(/\.metric-chart__gridline\s*\{\s*stroke: GrayText/);
    expect(forced).toMatch(/\.metric-chart__band\s*\{\s*display: none/);
  });

  it("gives the plot and the table region the focus ring tokens", () => {
    for (const selector of [".metric-chart__plot:focus-visible", ".metric-table:focus-visible"]) {
      const block = new RegExp(`${selector.replace(/[.:]/g, "\\$&")}\\s*\\{([^}]*)\\}`).exec(css);
      expect(block?.[1]).toContain("outline: var(--utf-focus-ring)");
      expect(block?.[1]).toContain("outline-offset: var(--utf-focus-offset)");
    }
  });
});
