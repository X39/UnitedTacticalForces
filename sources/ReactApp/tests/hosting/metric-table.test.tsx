import { render, screen, within } from "@testing-library/react";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { MetricTable } from "@/components/server/MetricTable";
import type { ChartMetric } from "@/features/hosting/metricsModel";
import type { GameServerMetricsHistory, ServerMetricsRange } from "@/features/hosting/types";
import { aggregate, historyPoint, metricsHistory } from "../helpers/metricsHistoryFixtures";

/*
 * D-07, D-09 and MET-04: the table view is the full-fidelity path. Real table, caption, column headers, newest
 * first, one spanning row per gap, no zero cells and a focusable scroll region.
 */

const bucketMs = 15_000;
const now = Date.parse("2026-11-21T14:00:00Z");

/** The time of the bucket that is the given number of buckets before now. */
function ago(buckets: number): number {
  return now - buckets * bucketMs;
}

function renderTable(
  history: GameServerMetricsHistory,
  options: {
    metric?: ChartMetric;
    title?: string;
    range?: ServerMetricsRange;
    nowMs?: number;
    playersNotReported?: boolean;
  } = {},
) {
  return render(
    <MetricTable
      metric={options.metric ?? "cpu"}
      title={options.title ?? "CPU load"}
      history={history}
      range={options.range ?? "1h"}
      nowMs={options.nowMs ?? now}
      playersNotReported={options.playersNotReported ?? false}
    />,
  );
}

/** The body rows of the rendered table. */
function bodyRows(): HTMLElement[] {
  return within(screen.getAllByRole("rowgroup")[1] as HTMLElement).getAllByRole("row");
}

describe("metric table", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("has a caption, four column headers and a focusable labelled scroll region", () => {
    renderTable(metricsHistory([historyPoint(ago(1))]));

    const region = screen.getByRole("region", { name: "CPU load, last hour" });
    expect(region).toHaveAttribute("tabindex", "0");
    const table = within(region).getByRole("table", { name: "CPU load, last hour" });
    expect(table.querySelector("caption")).toHaveTextContent("CPU load, last hour");
    const headers = within(table).getAllByRole("columnheader");
    expect(headers.map((header) => header.textContent)).toEqual([
      "Time",
      "Average",
      "Lowest",
      "Highest",
    ]);
    for (const header of headers) expect(header).toHaveAttribute("scope", "col");
  });

  it("lists readings newest first with average, lowest and highest", () => {
    renderTable(
      metricsHistory([
        historyPoint(ago(3), { cpu: aggregate(10, 5, 15) }),
        historyPoint(ago(2), { cpu: aggregate(20, 15, 25) }),
        historyPoint(ago(1), { cpu: aggregate(30, 25, 35) }),
      ]),
    );

    const rows = bodyRows();
    expect(rows).toHaveLength(3);
    const texts = rows.map((row) =>
      within(row)
        .getAllByRole("cell")
        .slice(1)
        .map((cell) => cell.textContent),
    );
    expect(texts).toEqual([
      ["30%", "25%", "35%"],
      ["20%", "15%", "25%"],
      ["10%", "5%", "15%"],
    ]);
    const stamps = rows.map((row) => row.querySelector("time")?.getAttribute("datetime"));
    expect(stamps).toEqual([
      new Date(ago(1)).toISOString(),
      new Date(ago(2)).toISOString(),
      new Date(ago(3)).toISOString(),
    ]);
  });

  it("shows one spanning row for every gap, including the stretch up to now, and no zero cell", () => {
    renderTable(
      metricsHistory([
        historyPoint(ago(10), { cpu: aggregate(10) }),
        historyPoint(ago(9), { cpu: null }),
        historyPoint(ago(8), { cpu: aggregate(12) }),
        historyPoint(ago(2), { cpu: aggregate(14) }),
      ]),
      { nowMs: now + 10 * bucketMs },
    );

    const rows = bodyRows();
    expect(rows).toHaveLength(6);
    const gaps = rows.filter((row) => within(row).queryAllByRole("cell").length === 1);
    expect(gaps).toHaveLength(3);
    for (const gap of gaps) {
      const cell = within(gap).getByRole("cell");
      expect(cell).toHaveAttribute("colspan", "4");
      expect(cell.textContent).toMatch(/^No readings from .+ to .+$/);
    }
    expect(screen.queryByText("0%")).toBeNull();
    expect(screen.getAllByRole("cell").filter((cell) => cell.textContent === "10%")).toHaveLength(
      3,
    );
  });

  it("shows the two empty sentences in one row when there is no reading", () => {
    renderTable(metricsHistory([]));

    const rows = bodyRows();
    expect(rows).toHaveLength(1);
    expect(within(rows[0] as HTMLElement).getByRole("cell")).toHaveAttribute("colspan", "4");
    expect(screen.getByText("No readings in this time range.")).toBeVisible();
    expect(screen.getByText("Readings are collected while the server is running.")).toBeVisible();
  });

  it("shows only the game-does-not-report sentence for empty players", () => {
    renderTable(metricsHistory([]), {
      metric: "players",
      title: "Players",
      playersNotReported: true,
    });

    expect(screen.getByText("This game does not report player counts.")).toBeVisible();
    expect(screen.queryByText("No readings in this time range.")).toBeNull();
  });

  it("adds the short date to the time of every row in the seven day range", () => {
    renderTable(metricsHistory([historyPoint(ago(1))], { range: "7d", bucketSeconds: 300 }), {
      range: "7d",
    });

    const time = bodyRows()[0]?.querySelector("time");
    expect(time?.textContent).toMatch(/11\/21\/26/);
    expect(screen.getByRole("table", { name: "CPU load, last 7 days" })).toBeVisible();
  });

  it("formats memory in gibibytes and players as whole numbers", () => {
    const history = metricsHistory([historyPoint(ago(1))]);

    const memory = renderTable(history, { metric: "memory", title: "Memory used" });
    expect(within(bodyRows()[0] as HTMLElement).getAllByRole("cell")[1]).toHaveTextContent("2 GiB");
    memory.unmount();
    renderTable(history, { metric: "players", title: "Players" });
    expect(within(bodyRows()[0] as HTMLElement).getAllByRole("cell")[1]).toHaveTextContent("4");
  });

  it("uses German captions, headers and number formats", async () => {
    await i18n.changeLanguage("de");
    renderTable(
      metricsHistory([historyPoint(ago(1), { memoryUsed: aggregate(2.5 * 1024 ** 3) })]),
      { metric: "memory", title: "Belegter Arbeitsspeicher" },
    );

    expect(
      screen.getByRole("table", { name: "Belegter Arbeitsspeicher, letzte Stunde" }),
    ).toBeVisible();
    expect(screen.getByRole("columnheader", { name: "Tiefstwert" })).toBeVisible();
    expect(screen.getAllByRole("cell", { name: "2,5 GiB" })).toHaveLength(3);
  });
});
