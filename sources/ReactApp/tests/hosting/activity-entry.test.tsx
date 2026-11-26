import { act, screen, within } from "@testing-library/react";
import i18n, { i18nReady } from "@/app/i18n";
import { ActivityEntry } from "@/components/server/ActivityEntry";
import { buildActivityEntries, type TimelineEntry } from "@/features/hosting/activityModel";
import { renderWithProviders } from "../helpers/renderWithProviders";
import {
  workspaceAutomaticJob,
  workspaceJob,
  workspaceOperation,
} from "../helpers/workspaceFixtures";

/* One activity entry (ACT-01, D-10): sentence, actor and relative time, anchored or compact. */

/** Four minutes after the fixture job was last updated. */
const now = Date.parse("2026-08-13T12:05:00Z");

/** Builds the timeline entry of a start job that was last updated four minutes before `now`. */
function startEntry(overrides: Parameters<typeof workspaceJob>[0] = {}): TimelineEntry {
  const job = workspaceJob({
    kind: "Start",
    status: "Running",
    requestedByName: "Falcon One",
    timeStampUpdated: "2026-08-13T12:01:00Z",
    ...overrides,
  });
  const [entry] = buildActivityEntries([job], []);
  if (!entry) throw new Error("expected one entry");
  return entry;
}

describe("ActivityEntry", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders an anchored article with sentence, type tag, requester and relative time", () => {
    const entry = startEntry();

    renderWithProviders(<ActivityEntry entry={entry} now={now} />);

    const article = screen.getByRole("article", { name: "“Start server” is in progress." });
    expect(article).toHaveAttribute("id", entry.id);
    expect(article).toHaveAttribute("tabindex", "-1");
    expect(within(article).getByText("Job")).toBeInTheDocument();
    expect(article).toHaveTextContent("Requested by Falcon One");
    expect(within(article).getByText("Falcon One").tagName).toBe("BDI");
    const time = article.querySelector("time");
    expect(time).toHaveAttribute("dateTime", "2026-08-13T12:01:00Z");
    expect(time).toHaveTextContent("4 minutes ago");
  });

  it("renders the compact variant without an anchor id or tab stop", () => {
    const entry = startEntry();

    renderWithProviders(<ActivityEntry entry={entry} now={now} compact />);

    const article = screen.getByRole("article", { name: "“Start server” is in progress." });
    expect(article).not.toHaveAttribute("id");
    expect(article).not.toHaveAttribute("tabindex");
    expect(article).toHaveTextContent("Requested by Falcon One");
    expect(article.querySelector("time")).toHaveTextContent("4 minutes ago");
  });

  it("never gives the anchored and the compact variant the same id", () => {
    const entry = startEntry();

    const { container } = renderWithProviders(
      <>
        <ActivityEntry entry={entry} now={now} />
        <ActivityEntry entry={entry} now={now} compact />
      </>,
    );

    const ids = [...container.querySelectorAll("[id]")].map((element) => element.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("states a failure in words and marks the icon decorative", () => {
    const entry = startEntry({ status: "Failed" });

    renderWithProviders(<ActivityEntry entry={entry} now={now} />);

    const article = screen.getByRole("article", { name: "“Start server” did not finish." });
    const icons = article.querySelectorAll(".activity-entry__icon");
    expect(icons).toHaveLength(1);
    expect(icons[0]).toHaveAttribute("aria-hidden", "true");
    expect(icons[0]).toHaveClass("activity-entry__icon--danger");
  });

  it("names a platform or hidden requester without a bdi element", () => {
    renderWithProviders(
      <>
        <ActivityEntry entry={startEntry({ requestedByName: null })} now={now} compact />
        <ActivityEntry entry={startEntry({ requestedByName: "Hidden member" })} now={now} compact />
      </>,
    );

    const [platform, hidden] = screen.getAllByRole("article");
    expect(platform).toHaveTextContent("Requested by The platform");
    expect(hidden).toHaveTextContent("Requested by A member");
    expect(platform?.querySelector("bdi")).toBeNull();
    expect(hidden?.querySelector("bdi")).toBeNull();
  });

  it("renders the German sentence, label and requester", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });

    renderWithProviders(<ActivityEntry entry={startEntry()} now={now} />);

    const article = screen.getByRole("article", { name: "„Server starten“ läuft." });
    expect(within(article).getByText("Auftrag")).toBeInTheDocument();
    expect(article).toHaveTextContent("Angefordert von Falcon One");
  });
  describe("automatic jobs", () => {
    /** Builds the timeline entry of an automatic stop job. */
    function automaticEntry(
      trigger: "Idle" | "MaxRuntime" | "Schedule",
      limit: number | null,
    ): TimelineEntry {
      const [built] = buildActivityEntries(
        [
          workspaceAutomaticJob(trigger, limit, {
            requestedByName: "Falcon One",
            timeStampUpdated: "2026-08-13T12:01:00Z",
          }),
        ],
        [],
      );
      if (!built) throw new Error("expected one entry");
      return built;
    }

    it("shows the trigger line instead of the actor line and keeps the sentence", () => {
      renderWithProviders(<ActivityEntry entry={automaticEntry("MaxRuntime", 6)} now={now} />);

      const article = screen.getByRole("article");
      expect(article).toHaveTextContent("Automatic: maximum runtime of 6 hours reached");
      expect(article).not.toHaveTextContent("Requested by");
      expect(article.querySelector("bdi")).toBeNull();
      expect(within(article).getByText("Job")).toBeInTheDocument();
      expect(article).toHaveAccessibleName(/Shut down and stop billing/);
    });

    it("uses the singular form for a limit of one", () => {
      renderWithProviders(
        <ActivityEntry entry={automaticEntry("MaxRuntime", 1)} now={now} compact />,
      );
      expect(screen.getByText("Automatic: maximum runtime of 1 hour reached")).toBeInTheDocument();
    });

    it("words the idle and schedule triggers", () => {
      const { unmount } = renderWithProviders(
        <ActivityEntry entry={automaticEntry("Idle", 30)} now={now} />,
      );
      expect(
        screen.getByText("Automatic: nobody was on the server for 30 minutes"),
      ).toBeInTheDocument();
      unmount();

      renderWithProviders(<ActivityEntry entry={automaticEntry("Schedule", null)} now={now} />);
      expect(screen.getByText("Automatic: schedule")).toBeInTheDocument();
    });

    it("renders the German trigger line", async () => {
      await act(async () => {
        await i18n.changeLanguage("de");
      });

      renderWithProviders(<ActivityEntry entry={automaticEntry("MaxRuntime", 6)} now={now} />);

      expect(
        screen.getByText("Automatisch: maximale Laufzeit von 6 Stunden erreicht"),
      ).toBeInTheDocument();
    });

    it("keeps the actor line for a job with an unknown trigger", () => {
      const [unknown] = buildActivityEntries(
        [
          workspaceJob({
            kind: "Stop",
            status: "Succeeded",
            requestedByName: "Falcon One",
            trigger: "SomethingNew" as never,
            triggerLimit: 3,
          }),
        ],
        [],
      );
      if (!unknown) throw new Error("expected one entry");

      renderWithProviders(<ActivityEntry entry={unknown} now={now} />);

      expect(screen.getByRole("article")).toHaveTextContent("Requested by Falcon One");
    });
  });

  it("shows the reason of a skipped scheduled run after its sentence", () => {
    const [skipped] = buildActivityEntries(
      [],
      [
        workspaceOperation({
          kind: "ScheduledRunSkipped" as never,
          status: "warning",
          summary: "The cost owner cannot pay for the start.",
        }),
      ],
    );
    if (!skipped) throw new Error("expected one entry");

    renderWithProviders(<ActivityEntry entry={skipped} now={now} />);

    const article = screen.getByRole("article");
    expect(article).toHaveTextContent("A scheduled run was skipped.");
    expect(within(article).getByText("The cost owner cannot pay for the start.")).toBeVisible();
    expect(article).toHaveClass("activity-entry--problem");
  });
});
