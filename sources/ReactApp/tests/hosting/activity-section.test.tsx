import { act, fireEvent, screen, within } from "@testing-library/react";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { beforeEach } from "vitest";
import { useState, type ComponentProps } from "react";
import i18n, { i18nReady } from "@/app/i18n";
import { currentLocale } from "@/app/format";
import { ActivityEntry } from "@/components/server/ActivityEntry";
import { CostConfirmationContext } from "@/components/server/useCostConfirmation";
import { activityOperationsTake, buildActivityEntries } from "@/features/hosting/activityModel";
import { ActivitySection } from "@/features/hosting/sections/ActivitySection";
import type { HostingOperation } from "@/features/hosting/types";
import { fleetAction, fleetServer } from "../helpers/fleetFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";
import { workspaceCommand, workspaceJob, workspaceOperation } from "../helpers/workspaceFixtures";

/* The Activity section (ACT-01, ACT-02, D-10): one merged, day-grouped, newest-first timeline. */

type SectionProps = ComponentProps<typeof ActivitySection>;

/** The fixed "now" of every test: shortly after the fixture job times, so "Today" is 2026-08-13. */
const nowIso = "2026-08-13T12:10:00Z";

/** Builds the props of a loaded, idle section for `server`. */
function baseProps(server = fleetServer()): SectionProps {
  return {
    server,
    jobs: [],
    operations: [],
    loading: false,
    failed: false,
    fetching: false,
    dataUpdatedAt: Date.parse(nowIso),
    hasActiveJob: false,
    jobBusy: false,
    jobRequestFailed: false,
    onReload: vi.fn(),
    onRetryJob: vi.fn(),
    onSkipJob: vi.fn(),
  };
}

/** Renders the section with a loaded, idle default; `overrides` replace single props, `route` the location. */
function renderSection(overrides: Partial<SectionProps> = {}, route = "/") {
  const props: SectionProps = { ...baseProps(), ...overrides };
  return { props, ...renderWithProviders(<ActivitySection {...props} />, { route }) };
}

/** A finished job whose time is `at`. */
function finishedJob(at: string, kind: "Start" | "Stop" = "Start") {
  return workspaceJob({
    kind,
    status: "Succeeded",
    timeStampUpdated: at,
    timeStampCompleted: at,
  });
}

describe("ActivitySection timeline", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(nowIso));
  });

  afterEach(async () => {
    vi.useRealTimers();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("shows the heading and description above a Today group before a Yesterday group, newest first", () => {
    const todayNewer = finishedJob("2026-08-13T12:05:00Z");
    const todayOlder = finishedJob("2026-08-13T11:00:00Z", "Stop");
    const yesterday = finishedJob("2026-08-12T12:05:00Z");

    renderSection({ jobs: [yesterday, todayOlder, todayNewer] });

    expect(screen.getByRole("heading", { level: 2, name: "Activity" })).toBeInTheDocument();
    expect(
      screen.getByText("Jobs, server logs, and platform events for this server, newest first."),
    ).toBeInTheDocument();
    const headings = screen.getAllByRole("heading", { level: 3 });
    expect(headings.map((heading) => heading.textContent)).toEqual(["Today", "Yesterday"]);
    const today = screen.getByRole("region", { name: "Today" });
    const earlier = screen.getByRole("region", { name: "Yesterday" });
    expect(
      within(today)
        .getAllByRole("article")
        .map((article) => article.id),
    ).toEqual([`activity-job-${todayNewer.primaryKey}`, `activity-job-${todayOlder.primaryKey}`]);
    expect(
      within(earlier)
        .getAllByRole("article")
        .map((article) => article.id),
    ).toEqual([`activity-job-${yesterday.primaryKey}`]);
    expect(Boolean(today.compareDocumentPosition(earlier) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(
      true,
    );
  });

  it("heads an older day with weekday, day and month, and adds the year only for another year", () => {
    const sameYear = finishedJob("2026-08-10T12:05:00Z");
    const otherYear = finishedJob("2025-12-31T12:05:00Z");

    renderSection({ jobs: [sameYear, otherYear] });

    const withoutYear = new Intl.DateTimeFormat(currentLocale(), {
      weekday: "long",
      day: "numeric",
      month: "long",
    }).format(new Date(2026, 7, 10));
    const withYear = new Intl.DateTimeFormat(currentLocale(), {
      weekday: "long",
      day: "numeric",
      month: "long",
      year: "numeric",
    }).format(new Date(2025, 11, 31));
    const headings = screen.getAllByRole("heading", { level: 3 }).map((h) => h.textContent);
    expect(headings).toEqual([withoutYear, withYear]);
    expect(withoutYear).not.toContain("2026");
    expect(withYear).toContain("2025");
  });

  it("shows a loading panel and no timeline while loading", () => {
    renderSection({ loading: true });

    expect(screen.getByText("Loading operational activity…")).toBeInTheDocument();
    expect(screen.queryByRole("article")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Activity" })).toBeInTheDocument();
  });

  it("shows the load error with a Try again button that calls onReload", () => {
    const { props } = renderSection({ failed: true });

    expect(
      screen.getByText("Activity could not be loaded. Check your connection and try again."),
    ).toBeInTheDocument();
    act(() => screen.getByRole("button", { name: "Try again" }).click());
    expect(props.onReload).toHaveBeenCalledTimes(1);
  });

  it("shows No activity yet with its body text when there is nothing", () => {
    renderSection();

    expect(screen.getByRole("heading", { name: "No activity yet" })).toBeInTheDocument();
    expect(
      screen.getByText(
        "Jobs, server logs, and platform events for this server appear here once something happens.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole("article")).not.toBeInTheDocument();
  });

  it("marks the list busy while fetching and keeps the entries in place", () => {
    const job = finishedJob("2026-08-13T12:05:00Z");
    /** Test harness: the section with a fetching flag the test can flip through a button. */
    function Harness() {
      const [fetching, setFetching] = useState(false);
      return (
        <>
          <button type="button" onClick={() => setFetching(true)}>
            start refetch
          </button>
          <ActivitySection {...baseProps} jobs={[job]} fetching={fetching} />
        </>
      );
    }
    const baseProps: SectionProps = {
      server: fleetServer(),
      jobs: [],
      operations: [],
      loading: false,
      failed: false,
      fetching: false,
      dataUpdatedAt: Date.parse(nowIso),
      hasActiveJob: false,
      jobBusy: false,
      jobRequestFailed: false,
      onReload: vi.fn(),
      onRetryJob: vi.fn(),
      onSkipJob: vi.fn(),
    };
    const { container } = renderWithProviders(<Harness />);
    expect(container.querySelector(".activity-timeline")).toHaveAttribute("aria-busy", "false");

    act(() => screen.getByRole("button", { name: "start refetch" }).click());

    expect(container.querySelector(".activity-timeline")).toHaveAttribute("aria-busy", "true");
    expect(screen.getAllByRole("article")).toHaveLength(1);
    expect(screen.queryByText("Loading operational activity…")).not.toBeInTheDocument();
  });

  describe("audit window note", () => {
    /** Builds a full take of operations, newest at 10:00Z and one minute apart. */
    function fullTake(count: number): HostingOperation[] {
      return Array.from({ length: count }, (_, index) =>
        workspaceOperation({
          kind: "Recovery",
          summary: `Recovery ${index}`,
          timeStampCreated: new Date(
            Date.parse("2026-08-13T10:00:00Z") - index * 60_000,
          ).toISOString(),
        }),
      );
    }

    it("is inserted directly before the first entry older than the oldest operation", () => {
      const operations = fullTake(activityOperationsTake);
      const newer = finishedJob("2026-08-13T09:00:00Z");
      const older = finishedJob("2026-08-13T05:00:00Z");

      renderSection({ jobs: [newer, older], operations });
      fireEvent.click(screen.getByRole("button", { name: /^Jobs/ }));

      const note = screen.getByText(
        "Older platform events are not shown. Entries below this point list jobs and logs only.",
      );
      const olderArticle = document.getElementById(`activity-job-${older.primaryKey}`);
      expect(olderArticle).not.toBeNull();
      expect(olderArticle?.closest("li")).toBe(note.closest("li"));
      expect(
        Boolean(
          note.compareDocumentPosition(olderArticle as Node) & Node.DOCUMENT_POSITION_FOLLOWING,
        ),
      ).toBe(true);
      const newerArticle = document.getElementById(`activity-job-${newer.primaryKey}`);
      expect(newerArticle?.closest("li")?.contains(note)).toBe(false);
      expect(screen.getAllByText(/Older platform events are not shown/)).toHaveLength(1);
    });

    it("is absent when fewer operations than the take were returned", () => {
      const operations = fullTake(activityOperationsTake - 1);
      const older = finishedJob("2026-08-13T05:00:00Z");

      renderSection({ jobs: [older], operations });
      fireEvent.click(screen.getByRole("button", { name: /^Jobs/ }));

      expect(screen.queryByText(/Older platform events are not shown/)).not.toBeInTheDocument();
    });
  });
});

/** Returns the entry article with the given anchor id or fails the test. */
function articleById(id: string): HTMLElement {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Expected an article with id ${id}`);
  return element;
}

/** Decodes the text of a `data:text/plain` download link. */
function downloadText(link: HTMLElement): string {
  const href = link.getAttribute("href") ?? "";
  return decodeURIComponent(href.replace("data:text/plain;charset=utf-8,", ""));
}

describe("ActivitySection expanded entries", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(nowIso));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  /** A failed job with two commands, one of which has diagnostic output. */
  function failedJob() {
    return workspaceJob({
      kind: "Start",
      status: "Failed",
      stage: "installing",
      attemptCount: 3,
      failureCode: "install.failed",
      failureReason: "SteamCMD exited with code 8.",
      canRetry: true,
      canSkip: true,
      timeStampUpdated: "2026-08-13T12:05:00Z",
      timeStampCompleted: "2026-08-13T12:05:00Z",
      commands: [
        workspaceCommand({
          sequence: 2,
          actionId: "install",
          status: "Failed",
          stage: "installing",
          failureCode: "install.failed",
          failureReason: "SteamCMD exited with code 8.",
          diagnosticOutput: "line one\nline two",
          timeStampCompleted: "2026-08-13T12:04:00Z",
        }),
        workspaceCommand({
          sequence: 1,
          actionId: "prepare",
          status: "Succeeded",
          stage: "preparing",
          diagnosticOutput: null,
          timeStampCompleted: "2026-08-13T12:02:00Z",
        }),
      ],
    });
  }

  it("expands and collapses an entry with an accessible disclosure", () => {
    const job = finishedJob("2026-08-13T12:05:00Z");
    renderSection({ jobs: [job] });
    const article = articleById(`activity-job-${job.primaryKey}`);
    const button = within(article).getByRole("button", { name: "Show details" });
    expect(button).toHaveAttribute("aria-expanded", "false");
    expect(within(article).queryByText("Job ID")).not.toBeInTheDocument();

    fireEvent.click(button);

    const hide = within(article).getByRole("button", { name: "Hide details" });
    expect(hide).toHaveAttribute("aria-expanded", "true");
    const controlled = hide.getAttribute("aria-controls");
    expect(controlled).toBe(`activity-job-${job.primaryKey}-details`);
    expect(document.getElementById(controlled ?? "")).toContainElement(
      within(article).getByText("Job ID"),
    );

    fireEvent.click(hide);

    expect(within(article).getByRole("button", { name: "Show details" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
    expect(within(article).queryByText("Job ID")).not.toBeInTheDocument();
  });

  it("shows the job facts, every command with output, the protocol download and retry and skip", () => {
    const job = failedJob();
    const { props } = renderSection({ jobs: [job] });
    const article = articleById(`activity-job-${job.primaryKey}`);

    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));

    expect(within(article).getByText(job.primaryKey).tagName).toBe("CODE");
    for (const token of ["Failed", "installing", "install.failed"]) {
      const matches = within(article).getAllByText(token, { selector: '[lang="en"]' });
      expect(matches.length).toBeGreaterThan(0);
    }
    for (const label of [
      "Job ID",
      "Status",
      "Stage",
      "Attempts",
      "Created",
      "Started",
      "Completed",
      "Failure code",
    ]) {
      expect(within(article).getByText(label)).toBeInTheDocument();
    }
    expect(within(article).getByText("3")).toBeInTheDocument();
    const commandTitles = within(article)
      .getAllByRole("listitem")
      .map((item) => item.textContent ?? "");
    expect(commandTitles[0]).toContain("#1");
    expect(commandTitles[1]).toContain("#2");
    expect(within(article).getByText(/line one/).tagName).toBe("PRE");
    expect(within(article).getByText("No diagnostic output was reported.")).toBeInTheDocument();
    const download = within(article).getByRole("link", { name: "Download log" });
    expect(download).toHaveAttribute("download", `utf-lifecycle-${job.primaryKey}.txt`);
    const text = downloadText(download);
    expect(text).toContain(`Command: ${job.commands[0]?.primaryKey ?? ""}`);
    expect(text).toContain("\n\n---\n\n");

    fireEvent.click(within(article).getByRole("button", { name: "Try again" }));
    fireEvent.click(within(article).getByRole("button", { name: "Skip and continue" }));

    expect(props.onRetryJob).toHaveBeenCalledWith(job.primaryKey);
    expect(props.onSkipJob).toHaveBeenCalledWith(job.primaryKey);
  });

  it("offers neither retry nor skip when the job allows neither", () => {
    const job = workspaceJob({ status: "Failed", canRetry: false, canSkip: false });
    renderSection({ jobs: [job] });
    const article = articleById(`activity-job-${job.primaryKey}`);

    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));

    expect(within(article).queryByRole("button", { name: "Try again" })).not.toBeInTheDocument();
    expect(
      within(article).queryByRole("button", { name: "Skip and continue" }),
    ).not.toBeInTheDocument();
  });

  it("makes retry and skip unavailable with a reason while a request is in flight", () => {
    const job = failedJob();
    const { props } = renderSection({ jobs: [job], jobBusy: true });
    const article = articleById(`activity-job-${job.primaryKey}`);
    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));

    const retry = within(article).getByRole("button", { name: "Try again" });
    const skip = within(article).getByRole("button", { name: "Skip and continue" });
    expect(retry).toHaveAttribute("aria-disabled", "true");
    expect(skip).toHaveAttribute("aria-disabled", "true");
    expect(within(article).getAllByText("Sending your request.")).toHaveLength(2);
    fireEvent.click(retry);
    fireEvent.click(skip);
    expect(props.onRetryJob).not.toHaveBeenCalled();
    expect(props.onSkipJob).not.toHaveBeenCalled();
  });

  it("shows the request error in an alert above the timeline", () => {
    renderSection({ jobs: [failedJob()], jobRequestFailed: true });

    expect(screen.getByRole("alert")).toHaveTextContent(
      "The request could not be sent. Try again, or refresh the server.",
    );
  });

  it("expands a log entry to its output, Copy output and Download log", () => {
    const job = failedJob();
    const command = job.commands[0];
    if (!command) throw new Error("expected a command");
    renderSection({ jobs: [job] });
    const article = articleById(`activity-log-${command.primaryKey}`);

    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));

    const output = within(article).getByText(/line one/);
    expect(output.tagName).toBe("PRE");
    expect(output).toHaveAttribute("tabindex", "0");
    expect(output).toHaveAccessibleName(
      article.querySelector(".activity-entry__sentence")?.textContent ?? "",
    );
    expect(within(article).getByRole("button", { name: "Copy output" })).toBeInTheDocument();
    const download = within(article).getByRole("link", { name: "Download log" });
    expect(download).toHaveAttribute("download", `utf-command-${command.primaryKey}.txt`);
    expect(downloadText(download)).toContain("line one\nline two");
    expect(within(article).getByText("Command ID")).toBeInTheDocument();
    expect(within(article).getByText(command.primaryKey).tagName).toBe("CODE");
  });

  it("says so and offers no copy button for a command without output", () => {
    const job = failedJob();
    const command = job.commands[1];
    if (!command) throw new Error("expected a command");
    renderSection({ jobs: [job] });
    const article = articleById(`activity-log-${command.primaryKey}`);

    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));

    expect(within(article).getByText("No diagnostic output was reported.")).toBeInTheDocument();
    expect(article.querySelector("pre")).toBeNull();
    expect(within(article).queryByRole("button", { name: "Copy output" })).not.toBeInTheDocument();
    expect(within(article).getByRole("link", { name: "Download log" })).toBeInTheDocument();
  });

  it("expands an audit entry to the API summary and the technical evidence", () => {
    const operation = workspaceOperation({
      kind: "WorkerDegraded",
      status: "warning",
      summary: "Worker missed its heartbeat",
      traceId: "trace-4711",
    });
    renderSection({ operations: [operation] });
    const article = articleById(`activity-audit-${operation.primaryKey}`);

    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));

    expect(
      within(article).getByText("Worker missed its heartbeat", { selector: '[lang="en"]' }),
    ).toBeInTheDocument();
    expect(within(article).getByText("Technical evidence")).toBeInTheDocument();
    expect(within(article).getByText("Telemetry trace")).toBeInTheDocument();
    expect(within(article).getByText("trace-4711").tagName).toBe("CODE");
  });

  it("keeps the compact variant free of a disclosure button", () => {
    const job = failedJob();
    const [entry] = buildActivityEntries([job], []);
    if (!entry) throw new Error("expected an entry");

    renderWithProviders(
      <ActivityEntry
        entry={entry}
        now={Date.parse(nowIso)}
        compact
        expanded
        onToggle={vi.fn()}
        onRetryJob={vi.fn()}
      />,
    );

    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByText("Job ID")).not.toBeInTheDocument();
  });
});

/** Reads the count shown on a filter chip. */
function chipCount(label: string): number {
  const group = screen.getByRole("group", { name: "Filter activity" });
  const chip = within(group).getByRole("button", { name: new RegExp(`^${label}\\b`) });
  return Number(chip.querySelector(".filter-chip__count")?.textContent);
}

/** Returns the filter chip with the given label. */
function chip(label: string): HTMLElement {
  const group = screen.getByRole("group", { name: "Filter activity" });
  return within(group).getByRole("button", { name: new RegExp(`^${label}\\b`) });
}

/**
 * Per-test timeout for the slow interaction tests below. They click through many controls with real timers and
 * pass in about two seconds alone, but exceed the default 5 s when the full suite saturates the workers.
 */
const slowTestTimeout = 15_000;

describe("ActivitySection toolbar", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(nowIso));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  /** One failed job with a failed command, one finished job and one warning audit record. */
  function mixedData() {
    const failed = workspaceJob({
      kind: "Start",
      status: "Failed",
      timeStampUpdated: "2026-08-13T12:05:00Z",
      timeStampCompleted: "2026-08-13T12:05:00Z",
      commands: [
        workspaceCommand({
          status: "Failed",
          diagnosticOutput: "boom",
          timeStampCompleted: "2026-08-13T12:04:00Z",
        }),
      ],
    });
    const finished = finishedJob("2026-08-13T11:00:00Z", "Stop");
    const audit = workspaceOperation({
      kind: "WorkerDegraded",
      status: "warning",
      summary: "Worker missed its heartbeat",
      timeStampCreated: "2026-08-13T10:00:00Z",
    });
    return { jobs: [failed, finished], operations: [audit] };
  }

  /** The ids of the entry articles in document order. */
  function listedIds(): string[] {
    return screen.queryAllByRole("article").map((article) => article.id);
  }

  it("counts exactly what each filter lists and narrows both with Problems only", () => {
    renderSection(mixedData());

    expect(screen.getByRole("group", { name: "Filter activity" })).toBeInTheDocument();
    const all = listedIds();
    expect(all).toHaveLength(chipCount("All"));
    expect(chip("All")).toHaveAttribute("aria-pressed", "true");

    fireEvent.click(chip("Jobs"));

    expect(listedIds()).toHaveLength(chipCount("Jobs"));
    expect(listedIds().every((id) => id.startsWith("activity-job-"))).toBe(true);
    expect(chip("Jobs")).toHaveAttribute("aria-pressed", "true");

    fireEvent.click(chip("All"));
    const problems = screen.getByRole("button", { name: "Problems only" });
    expect(problems).toHaveAttribute("aria-pressed", "false");

    fireEvent.click(problems);

    expect(problems).toHaveAttribute("aria-pressed", "true");
    const narrowed = listedIds();
    expect(narrowed.length).toBeLessThan(all.length);
    expect(narrowed).toHaveLength(chipCount("All"));
    for (const label of ["Jobs", "Logs", "Audit"]) {
      fireEvent.click(chip(label));
      expect(listedIds()).toHaveLength(chipCount(label));
    }
    expect(chipCount("Jobs")).toBe(1);
    expect(chipCount("Logs")).toBe(1);
    expect(chipCount("Audit")).toBe(1);
  });

  it("shows the no-match panel and resets both filters with Show all activity", () => {
    renderSection({ jobs: [finishedJob("2026-08-13T12:05:00Z")] });

    fireEvent.click(chip("Logs"));

    expect(
      screen.getByRole("heading", { name: "Nothing matches this filter" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("There are no entries of this type in the recent activity."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("article")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Show all activity" }));

    expect(chip("All")).toHaveAttribute("aria-pressed", "true");
    expect(screen.getAllByRole("article")).toHaveLength(1);
  });

  it("shows the no-problems line for Problems only on All and resets it", () => {
    renderSection({ jobs: [finishedJob("2026-08-13T12:05:00Z")] });
    const problems = screen.getByRole("button", { name: "Problems only" });

    fireEvent.click(problems);

    expect(screen.getByText("There are no problems in the recent activity.")).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Nothing matches this filter" }),
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Show all activity" }));

    expect(problems).toHaveAttribute("aria-pressed", "false");
    expect(chip("All")).toHaveAttribute("aria-pressed", "true");
    expect(screen.getAllByRole("article")).toHaveLength(1);
  });

  it("keeps the chips with zero counts when there is no activity", () => {
    renderSection();

    expect(chipCount("All")).toBe(0);
    expect(chipCount("Jobs")).toBe(0);
    expect(chipCount("Logs")).toBe(0);
    expect(chipCount("Audit")).toBe(0);
  });

  it("requests the shared confirmation for Collect logs with the workspace-action source", () => {
    const request = vi.fn();
    const server = fleetServer({
      status: "Running",
      actions: [fleetAction("stop"), fleetAction("collect-logs")],
    });
    renderWithProviders(
      <CostConfirmationContext.Provider value={{ request }}>
        <ActivitySection {...baseProps(server)} />
      </CostConfirmationContext.Provider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Collect logs" }));

    expect(request).toHaveBeenCalledWith({
      serverId: server.primaryKey,
      actionId: "collect-logs",
      source: "workspace-action",
    });
  });

  it("disables Collect logs with a reason when the action is unavailable", () => {
    const request = vi.fn();
    const server = fleetServer({
      status: "Running",
      actions: [
        fleetAction("collect-logs", {
          enabled: false,
          unavailableReason: "The worker is not reachable.",
        }),
      ],
    });
    renderWithProviders(
      <CostConfirmationContext.Provider value={{ request }}>
        <ActivitySection {...baseProps(server)} />
      </CostConfirmationContext.Provider>,
    );

    const button = screen.getByRole("button", { name: "Collect logs" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAccessibleDescription(/\S/);
    fireEvent.click(button);
    expect(request).not.toHaveBeenCalled();
  });

  it("offers Download all logs as a link when a command exists", () => {
    const job = mixedData().jobs[0];
    if (!job) throw new Error("expected a job");
    renderSection({ jobs: [job] });

    const link = screen.getByRole("link", { name: "Download all logs" });

    expect(link).toHaveAttribute("download", "utf-server-protocols.txt");
    expect(downloadText(link)).toContain("UTF worker command protocol");
  });

  it("disables Download all logs with a reason when no command exists", () => {
    renderSection({ jobs: [finishedJob("2026-08-13T12:05:00Z")] });

    const button = screen.getByRole("button", { name: "Download all logs" });

    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAccessibleDescription("No diagnostic output was reported.");
    expect(screen.queryByRole("link", { name: "Download all logs" })).not.toBeInTheDocument();
  });

  /** Builds `count` finished jobs one minute apart, newest first. */
  function manyJobs(count: number) {
    return Array.from({ length: count }, (_, index) =>
      finishedJob(new Date(Date.parse("2026-08-13T12:00:00Z") - index * 60_000).toISOString()),
    );
  }

  it("shows 50 entries first and Show more activity adds 50 and focuses the first new one", () => {
    const jobs = manyJobs(120);
    renderSection({ jobs });
    expect(screen.getAllByRole("article")).toHaveLength(50);
    expect(chipCount("All")).toBe(120);

    fireEvent.click(screen.getByRole("button", { name: "Show more activity" }));

    expect(screen.getAllByRole("article")).toHaveLength(100);
    const firstNew = jobs[50];
    expect(document.activeElement).toBe(
      document.getElementById(`activity-job-${firstNew?.primaryKey}`),
    );

    fireEvent.click(screen.getByRole("button", { name: "Show more activity" }));

    expect(screen.getAllByRole("article")).toHaveLength(120);
    expect(screen.queryByRole("button", { name: "Show more activity" })).not.toBeInTheDocument();
    // Rendering 120 job entries three times is slow on a loaded machine; the 5 s default is too tight.
  }, 30_000);

  it("offers no Show more activity for 50 entries or fewer", () => {
    renderSection({ jobs: manyJobs(50) });

    expect(screen.getAllByRole("article")).toHaveLength(50);
    expect(screen.queryByRole("button", { name: "Show more activity" })).not.toBeInTheDocument();
  });

  describe("activity deep link", () => {
    const scrollIntoView = vi.fn();

    beforeEach(() => {
      scrollIntoView.mockReset();
      Element.prototype.scrollIntoView = scrollIntoView;
    });

    afterEach(() => {
      Reflect.deleteProperty(Element.prototype, "scrollIntoView");
    });

    it(
      "resets the filters, pages in, expands, scrolls without motion and focuses the entry",
      async () => {
        const jobs = manyJobs(120);
        const target = jobs[69];
        if (!target) throw new Error("expected the 70th job");
        const { router } = renderSection({ jobs });
        fireEvent.click(screen.getByRole("button", { name: "Problems only" }));
        fireEvent.click(chip("Jobs"));
        expect(screen.getByRole("button", { name: "Problems only" })).toHaveAttribute(
          "aria-pressed",
          "true",
        );

        await act(async () => {
          await router.navigate(`/?tab=activity#activity-job-${target.primaryKey}`);
        });

        expect(screen.getByRole("button", { name: "Problems only" })).toHaveAttribute(
          "aria-pressed",
          "false",
        );
        expect(chip("All")).toHaveAttribute("aria-pressed", "true");
        const article = document.getElementById(`activity-job-${target.primaryKey}`);
        expect(article).not.toBeNull();
        expect(screen.getAllByRole("article")).toHaveLength(100);
        expect(
          within(article as HTMLElement).getByRole("button", { name: "Hide details" }),
        ).toBeInTheDocument();
        expect(scrollIntoView).toHaveBeenCalledWith({ behavior: "auto" });
        expect(document.activeElement).toBe(article);
        expect(
          screen.queryByText("That entry is no longer in the recent activity."),
        ).not.toBeInTheDocument();
      },
      slowTestTimeout,
    );

    it("applies a link that is present when the section first renders with data", () => {
      const jobs = manyJobs(3);
      const target = jobs[2];
      if (!target) throw new Error("expected a job");
      renderSection({ jobs }, `/?tab=activity#activity-job-${target.primaryKey}`);

      const article = document.getElementById(`activity-job-${target.primaryKey}`);
      expect(document.activeElement).toBe(article);
      expect(scrollIntoView).toHaveBeenCalledTimes(1);
    });

    it("waits for the data before applying the link", () => {
      const jobs = manyJobs(3);
      const target = jobs[1];
      if (!target) throw new Error("expected a job");
      renderSection({ jobs, loading: true }, `/?tab=activity#activity-job-${target.primaryKey}`);

      expect(scrollIntoView).not.toHaveBeenCalled();
      expect(
        screen.queryByText("That entry is no longer in the recent activity."),
      ).not.toBeInTheDocument();
    });

    it("shows the missing-entry line and moves no focus for an unknown anchor", () => {
      renderSection(
        { jobs: manyJobs(3) },
        "/?tab=activity#activity-job-00000000-0000-4000-8000-ffffffffffff",
      );

      expect(
        screen.getByText("That entry is no longer in the recent activity."),
      ).toBeInTheDocument();
      expect(scrollIntoView).not.toHaveBeenCalled();
      expect(document.activeElement).toBe(document.body);
      expect(screen.getAllByRole("article")).toHaveLength(3);
    });

    it("matches the fragment only against entry ids, never as an object key", () => {
      renderSection({ jobs: manyJobs(2) }, "/?tab=activity#activity-__proto__");

      expect(
        screen.getByText("That entry is no longer in the recent activity."),
      ).toBeInTheDocument();
      expect(scrollIntoView).not.toHaveBeenCalled();
    });

    it("ignores a fragment that is not an activity anchor", () => {
      renderSection({ jobs: manyJobs(2) }, "/?tab=activity#something-else");

      expect(
        screen.queryByText("That entry is no longer in the recent activity."),
      ).not.toBeInTheDocument();
      expect(scrollIntoView).not.toHaveBeenCalled();
    });

    it("switches an audit echo target to the Audit filter so it can be shown", () => {
      const job = finishedJob("2026-08-13T12:05:00Z");
      const echo = workspaceOperation({
        kind: "LifecycleJobSucceeded",
        lifecycleJobId: job.primaryKey,
        timeStampCreated: "2026-08-13T12:05:30Z",
      });
      renderSection(
        { jobs: [job], operations: [echo] },
        `/?tab=activity#activity-audit-${echo.primaryKey}`,
      );

      expect(chip("Audit")).toHaveAttribute("aria-pressed", "true");
      expect(document.activeElement).toBe(
        document.getElementById(`activity-audit-${echo.primaryKey}`),
      );
    });
  });
});

describe("ActivitySection very long output", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  it("renders 5,000 lines of output inside its own bounded, scrolling box", () => {
    const output = Array.from({ length: 5000 }, (_, index) => `line ${index + 1}`).join("\n");
    const job = workspaceJob({
      status: "Failed",
      commands: [workspaceCommand({ status: "Failed", diagnosticOutput: output })],
    });
    const command = job.commands[0];
    if (!command) throw new Error("expected a command");
    renderSection({ jobs: [job] });
    const article = articleById(`activity-log-${command.primaryKey}`);

    const started = performance.now();
    fireEvent.click(within(article).getByRole("button", { name: "Show details" }));
    const elapsed = performance.now() - started;

    const pre = article.querySelector("pre");
    expect(pre).not.toBeNull();
    expect(pre?.textContent).toBe(output);
    expect(pre).toHaveClass("activity-details__output");
    expect(elapsed).toBeLessThan(200);
  });

  it("styles the output box to scroll in both axes within the output token and never widen the page", () => {
    const css = readFileSync(
      resolve(import.meta.dirname, "..", "..", "src", "components", "server", "activity.css"),
      "utf8",
    );
    const rule = /\.activity-details__output\s*\{([^}]*)\}/.exec(css)?.[1] ?? "";

    expect(rule).toContain("max-height: var(--utf-output-max-height)");
    expect(rule).toContain("overflow: auto");
    expect(rule).toContain("max-width: 100%");
    expect(rule).toContain("white-space: pre");
  });
});
